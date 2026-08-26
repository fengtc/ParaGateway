package service

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"strings"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/pkg/logger"
	"github.com/Wei-Shaw/sub2api/internal/util/responseheaders"
	"github.com/gin-gonic/gin"
	"github.com/tidwall/gjson"
	"github.com/tidwall/sjson"
	"go.uber.org/zap"
)

const (
	copilotNativeMessagesEndpoint     = "/v1/messages"
	copilotNativeMessagesAPIVersion   = "2026-06-01"
	copilotNativeMessagesEditor       = "vscode/1.109.3"
	copilotNativeMessagesPlugin       = "copilot-chat/0.58.0"
	copilotNativeMessagesUserAgent    = "vscode_claude_code/2.1.112 (external, sdk-ts, agent-sdk/0.2.112)"
	copilotNativeMessagesVersion      = "2023-06-01"
	copilotNativeMessagesFallbackBody = 4 << 10
	copilotNativeMessagesPreambleMax  = 64 << 10
)

var copilotNativeMessagesAllowedBetas = map[string]struct{}{
	"advanced-tool-use-2025-11-20":    {},
	"context-management-2025-06-27":   {},
	"interleaved-thinking-2025-05-14": {},
}

func (s *OpenAIGatewayService) forwardCopilotAnthropic(
	ctx context.Context,
	c *gin.Context,
	account *Account,
	body []byte,
	defaultMappedModel string,
) (*OpenAIForwardResult, error) {
	result, handled, err := s.tryForwardCopilotNativeMessages(ctx, c, account, body, defaultMappedModel)
	if handled {
		return result, err
	}
	return s.forwardAnthropicViaRawChatCompletions(ctx, c, account, body, defaultMappedModel)
}

func (s *OpenAIGatewayService) tryForwardCopilotNativeMessages(
	ctx context.Context,
	c *gin.Context,
	account *Account,
	body []byte,
	defaultMappedModel string,
) (*OpenAIForwardResult, bool, error) {
	startTime := time.Now()
	if !json.Valid(body) {
		return nil, true, fmt.Errorf("copilot messages: invalid JSON")
	}

	originalModel := strings.TrimSpace(gjson.GetBytes(body, "model").String())
	if originalModel == "" {
		return nil, true, fmt.Errorf("copilot messages: model is required")
	}
	billingModel := resolveOpenAIForwardModel(account, originalModel, defaultMappedModel)
	upstreamModel := normalizeOpenAIModelForUpstream(account, billingModel)
	if !shouldUseCopilotNativeMessages(upstreamModel) {
		return nil, false, nil
	}

	nativeBody, metadataUserID, err := prepareCopilotNativeMessagesBody(body, upstreamModel)
	if err != nil {
		return nil, true, fmt.Errorf("copilot messages: prepare native request: %w", err)
	}
	clientStream := gjson.GetBytes(nativeBody, "stream").Bool()
	token, _, err := s.getRequestCredential(ctx, c, account)
	if err != nil {
		return nil, true, fmt.Errorf("copilot messages: native auth: %w", err)
	}
	if strings.TrimSpace(token) == "" {
		return nil, true, fmt.Errorf("copilot messages: account %d missing credential", account.ID)
	}

	baseURL, err := s.validateUpstreamBaseURL(account.GetOpenAIBaseURL())
	if err != nil {
		return nil, true, fmt.Errorf("copilot messages: invalid native base URL: %w", err)
	}
	targetURL := buildCopilotAPIURL(baseURL, copilotNativeMessagesEndpoint)
	SetActualOpenAIUpstreamEndpoint(c, copilotNativeMessagesEndpoint)
	resp, err := s.sendCopilotNativeMessagesRequest(ctx, c, account, targetURL, nativeBody, token, metadataUserID, true)
	if err != nil {
		return nil, true, err
	}
	if resp == nil || resp.Body == nil {
		return nil, true, errors.New("copilot messages: native upstream returned no response")
	}

	logger.L().Debug("copilot native messages upstream response",
		zap.Int64("account_id", account.ID),
		zap.String("model", upstreamModel),
		zap.Int("status", resp.StatusCode),
		zap.Bool("stream", clientStream),
		zap.Int64("latency_ms", time.Since(startTime).Milliseconds()),
	)

	if shouldFallbackCopilotNativeMessagesStatus(resp.StatusCode) {
		_, _ = io.Copy(io.Discard, io.LimitReader(resp.Body, copilotNativeMessagesFallbackBody))
		_ = resp.Body.Close()
		logger.L().Debug("copilot native messages rejected; falling back to chat completions",
			zap.Int64("account_id", account.ID),
			zap.String("model", upstreamModel),
			zap.Int("status", resp.StatusCode),
		)
		return nil, false, nil
	}
	if resp.StatusCode != http.StatusOK {
		defer func() { _ = resp.Body.Close() }()
		result, handleErr := s.handleAnthropicErrorResponse(resp, c, account, billingModel)
		return result, true, handleErr
	}

	if clientStream {
		result, started, streamErr := s.handleCopilotNativeMessagesStreamingResponse(
			c, resp, originalModel, billingModel, upstreamModel, startTime,
		)
		if streamErr != nil && !started {
			logger.L().Debug("copilot native messages failed before response started; falling back to chat completions",
				zap.Int64("account_id", account.ID),
				zap.String("model", upstreamModel),
				zap.Error(streamErr),
			)
			return nil, false, nil
		}
		return result, true, streamErr
	}

	result, valid, responseErr := s.handleCopilotNativeMessagesNonStreamingResponse(
		c, resp, originalModel, billingModel, upstreamModel, startTime,
	)
	if responseErr != nil && !valid {
		logger.L().Debug("copilot native messages returned an invalid response; falling back to chat completions",
			zap.Int64("account_id", account.ID),
			zap.String("model", upstreamModel),
			zap.Error(responseErr),
		)
		return nil, false, nil
	}
	return result, true, responseErr
}

func prepareCopilotNativeMessagesBody(body []byte, upstreamModel string) ([]byte, string, error) {
	if !json.Valid(body) {
		return nil, "", errors.New("invalid JSON")
	}
	if strings.TrimSpace(upstreamModel) == "" {
		return nil, "", errors.New("model is required")
	}

	out, err := sjson.SetBytes(body, "model", upstreamModel)
	if err != nil {
		return nil, "", fmt.Errorf("set model: %w", err)
	}
	out = stripCopilotNativeCacheControlScope(out)
	out = applyToolsLastCacheBreakpoint(out)
	out = addMessageCacheBreakpoints(out)
	out = enforceCacheControlLimit(out)
	return out, gjson.GetBytes(out, "metadata.user_id").String(), nil
}

func stripCopilotNativeCacheControlScope(body []byte) []byte {
	invalidThinking, messagePaths, toolPaths, systemPaths := collectCacheControlPaths(body)
	paths := make([]string, 0, len(invalidThinking)+len(messagePaths)+len(toolPaths)+len(systemPaths))
	for _, item := range invalidThinking {
		paths = append(paths, item.path)
	}
	paths = append(paths, messagePaths...)
	paths = append(paths, toolPaths...)
	paths = append(paths, systemPaths...)

	out := body
	for _, path := range paths {
		scopePath := path + ".scope"
		if !gjson.GetBytes(out, scopePath).Exists() {
			continue
		}
		if next, err := sjson.DeleteBytes(out, scopePath); err == nil {
			out = next
		}
	}
	return out
}

func shouldUseCopilotNativeMessages(model string) bool {
	return strings.HasPrefix(strings.ToLower(strings.TrimSpace(model)), "claude-")
}

func shouldFallbackCopilotNativeMessagesStatus(status int) bool {
	switch status {
	case http.StatusBadRequest,
		http.StatusNotFound,
		http.StatusMethodNotAllowed,
		http.StatusUnsupportedMediaType,
		http.StatusUnprocessableEntity:
		return true
	default:
		return false
	}
}

func copilotNativeMessagesHeaders(c *gin.Context, body []byte, metadataUserID, token string) http.Header {
	headers := make(http.Header)
	for key, value := range copilotHeaders(token, false) {
		headers.Set(key, value)
	}
	requestID := headers.Get("x-request-id")

	headers.Set("Accept", "application/json")
	headers.Set("Content-Type", "application/json")
	headers.Set("editor-version", copilotNativeMessagesEditor)
	headers.Set("editor-plugin-version", copilotNativeMessagesPlugin)
	headers.Set("User-Agent", copilotNativeMessagesUserAgent)
	headers.Set("x-github-api-version", copilotNativeMessagesAPIVersion)
	headers.Set("x-agent-task-id", requestID)
	headers.Set("x-interaction-type", "messages-proxy")
	headers.Set("openai-intent", "messages-proxy")
	headers.Set("x-initiator", copilotNativeMessagesInitiator(body))
	headers.Del("copilot-integration-id")
	if copilotNativeMessagesHasVision(body) {
		headers.Set("copilot-vision-request", "true")
	} else {
		headers.Del("copilot-vision-request")
	}

	if parsed := ParseMetadataUserID(metadataUserID); parsed != nil {
		headers.Set("editor-device-id", parsed.DeviceID)
		headers.Set("x-interaction-id", parsed.SessionID)
	}

	anthropicVersion := copilotNativeMessagesVersion
	if c != nil && c.Request != nil {
		if incoming := strings.TrimSpace(c.GetHeader("anthropic-version")); incoming != "" {
			anthropicVersion = incoming
		}
		if beta := filterCopilotNativeMessagesBetas(c.GetHeader("anthropic-beta")); beta != "" {
			headers.Set("anthropic-beta", beta)
		} else {
			headers.Del("anthropic-beta")
		}
	}
	headers.Set("anthropic-version", anthropicVersion)
	return headers
}

func filterCopilotNativeMessagesBetas(raw string) string {
	allowed := make([]string, 0, 3)
	seen := make(map[string]struct{}, 3)
	for _, beta := range parseAnthropicBetaHeader(raw) {
		if _, ok := copilotNativeMessagesAllowedBetas[beta]; !ok {
			continue
		}
		if _, duplicate := seen[beta]; duplicate {
			continue
		}
		seen[beta] = struct{}{}
		allowed = append(allowed, beta)
	}
	return strings.Join(allowed, ",")
}

func copilotNativeMessagesInitiator(body []byte) string {
	messages := gjson.GetBytes(body, "messages")
	if !messages.IsArray() {
		return "user"
	}
	items := messages.Array()
	if len(items) == 0 {
		return "user"
	}
	last := items[len(items)-1]
	if last.Get("role").String() != "user" {
		return "agent"
	}
	content := last.Get("content")
	if content.Type == gjson.String || !content.IsArray() {
		return "user"
	}
	for _, block := range content.Array() {
		if block.Get("type").String() != "tool_result" {
			return "user"
		}
	}
	return "agent"
}

func copilotNativeMessagesHasVision(body []byte) bool {
	messages := gjson.GetBytes(body, "messages")
	if !messages.IsArray() {
		return false
	}
	for _, message := range messages.Array() {
		content := message.Get("content")
		if !content.IsArray() {
			continue
		}
		for _, block := range content.Array() {
			if block.Get("type").String() == "image" {
				return true
			}
			if block.Get("type").String() != "tool_result" {
				continue
			}
			inner := block.Get("content")
			if !inner.IsArray() {
				continue
			}
			for _, item := range inner.Array() {
				if item.Get("type").String() == "image" {
					return true
				}
			}
		}
	}
	return false
}

func (s *OpenAIGatewayService) sendCopilotNativeMessagesRequest(
	ctx context.Context,
	c *gin.Context,
	account *Account,
	targetURL string,
	body []byte,
	token string,
	metadataUserID string,
	allowTokenRefresh bool,
) (*http.Response, error) {
	upstreamCtx, releaseUpstreamCtx := detachUpstreamContext(ctx)
	req, err := http.NewRequestWithContext(upstreamCtx, http.MethodPost, targetURL, bytes.NewReader(body))
	releaseUpstreamCtx()
	if err != nil {
		return nil, fmt.Errorf("copilot messages: build native request: %w", err)
	}
	req = req.WithContext(WithHTTPUpstreamProfile(req.Context(), HTTPUpstreamProfileOpenAI))
	for key, values := range copilotNativeMessagesHeaders(c, body, metadataUserID, token) {
		for _, value := range values {
			req.Header.Add(key, value)
		}
	}
	account.ApplyHeaderOverrides(req.Header)

	proxyURL := ""
	if account.Proxy != nil {
		proxyURL = account.Proxy.URL()
	}
	if s == nil || s.httpUpstream == nil {
		return nil, errors.New("copilot messages: upstream transport is not configured")
	}
	upstreamStart := time.Now()
	resp, err := s.httpUpstream.Do(req, proxyURL, account.ID, account.Concurrency)
	SetOpsLatencyMs(c, OpsUpstreamLatencyMsKey, time.Since(upstreamStart).Milliseconds())
	if err != nil {
		return nil, s.handleOpenAIUpstreamTransportError(ctx, c, account, err, false)
	}
	if allowTokenRefresh && resp != nil && resp.StatusCode == http.StatusUnauthorized && s.openAITokenProvider != nil {
		failedBody, _ := io.ReadAll(io.LimitReader(resp.Body, openAIUpstreamErrorBodyReadLimit))
		_ = resp.Body.Close()
		freshToken, refreshErr := s.openAITokenProvider.ForceRefresh(ctx, account)
		if refreshErr == nil && strings.TrimSpace(freshToken) != "" {
			return s.sendCopilotNativeMessagesRequest(ctx, c, account, targetURL, body, freshToken, metadataUserID, false)
		}
		resp.Body = io.NopCloser(bytes.NewReader(failedBody))
	}
	return resp, nil
}

func (s *OpenAIGatewayService) handleCopilotNativeMessagesNonStreamingResponse(
	c *gin.Context,
	resp *http.Response,
	originalModel string,
	billingModel string,
	upstreamModel string,
	startTime time.Time,
) (*OpenAIForwardResult, bool, error) {
	defer func() { _ = resp.Body.Close() }()
	body, err := readUpstreamResponseBodyLimited(resp.Body, resolveUpstreamResponseReadLimit(s.cfg))
	if err != nil {
		return nil, false, fmt.Errorf("copilot native messages: read response: %w", err)
	}
	if err := validateCopilotNativeMessageResponse(body); err != nil {
		return nil, false, fmt.Errorf("copilot native messages: invalid response: %w", err)
	}

	usage := openAIUsageFromClaudeUsage(parseClaudeUsageFromResponseBody(body))
	responseModel := strings.TrimSpace(gjson.GetBytes(body, "model").String())
	responseID := strings.TrimSpace(gjson.GetBytes(body, "id").String())
	if s.responseHeaderFilter != nil {
		responseheaders.WriteFilteredHeaders(c.Writer.Header(), resp.Header, s.responseHeaderFilter)
	}
	contentType := resp.Header.Get("Content-Type")
	if contentType == "" {
		contentType = "application/json"
	}
	c.Data(http.StatusOK, contentType, body)
	return &OpenAIForwardResult{
		RequestID:             resp.Header.Get("x-request-id"),
		ResponseID:            responseID,
		Usage:                 usage,
		Model:                 originalModel,
		BillingModel:          billingModel,
		UpstreamModel:         upstreamModel,
		UpstreamResponseModel: responseModel,
		UpstreamEndpoint:      copilotNativeMessagesEndpoint,
		Stream:                false,
		ResponseHeaders:       resp.Header.Clone(),
		Duration:              time.Since(startTime),
	}, true, nil
}

func validateCopilotNativeMessageResponse(body []byte) error {
	if len(bytes.TrimSpace(body)) == 0 {
		return errors.New("empty body")
	}
	var payload struct {
		ID      string          `json:"id"`
		Type    string          `json:"type"`
		Role    string          `json:"role"`
		Content json.RawMessage `json:"content"`
		Usage   json.RawMessage `json:"usage"`
	}
	if err := json.Unmarshal(body, &payload); err != nil {
		return fmt.Errorf("decode JSON: %w", err)
	}
	if strings.TrimSpace(payload.ID) == "" {
		return errors.New("missing message id")
	}
	if payload.Type != "message" {
		return fmt.Errorf("unexpected type %q", payload.Type)
	}
	if payload.Role != "assistant" {
		return fmt.Errorf("unexpected role %q", payload.Role)
	}
	var content []json.RawMessage
	if err := json.Unmarshal(payload.Content, &content); err != nil {
		return fmt.Errorf("content is not an array: %w", err)
	}
	var usage map[string]json.RawMessage
	if err := json.Unmarshal(payload.Usage, &usage); err != nil || usage == nil {
		if err == nil {
			err = errors.New("missing usage object")
		}
		return fmt.Errorf("invalid usage: %w", err)
	}
	return nil
}

func (s *OpenAIGatewayService) handleCopilotNativeMessagesStreamingResponse(
	c *gin.Context,
	resp *http.Response,
	originalModel string,
	billingModel string,
	upstreamModel string,
	startTime time.Time,
) (*OpenAIForwardResult, bool, error) {
	defer func() { _ = resp.Body.Close() }()

	writeStreamHeaders := s.newStreamHeaderWriter(c, resp.Header)
	usage := &ClaudeUsage{}
	var firstTokenMs *int
	clientDisconnected := false
	sawMessageStart := false
	sawMessageStop := false
	pendingLines := make([]string, 0, 4)
	pendingBytes := 0
	requestID := resp.Header.Get("x-request-id")
	responseID := ""
	responseModel := ""

	writeLine := func(line string) {
		if clientDisconnected {
			return
		}
		writeStreamHeaders()
		if _, err := fmt.Fprintln(c.Writer, line); err != nil {
			clientDisconnected = true
			return
		}
		if line == "" {
			c.Writer.Flush()
		}
	}

	scanner := s.newUpstreamSSEScanner(resp.Body)
	var preambleErr error
	for scanner.Scan() {
		line := scanner.Text()
		eventType := ""
		if data, ok := extractAnthropicSSEDataLine(line); ok {
			data = strings.TrimSpace(data)
			parseSSEUsagePassthrough(data, usage)
			eventType = strings.TrimSpace(gjson.Get(data, "type").String())
			if eventType == "message_start" {
				responseID = strings.TrimSpace(gjson.Get(data, "message.id").String())
				responseModel = strings.TrimSpace(gjson.Get(data, "message.model").String())
			}
			if firstTokenMs == nil && copilotNativeMessagesEventStartsOutput(data) {
				ms := int(time.Since(startTime).Milliseconds())
				firstTokenMs = &ms
			}
		}

		if !sawMessageStart {
			pendingBytes += len(line) + 1
			if pendingBytes > copilotNativeMessagesPreambleMax {
				preambleErr = fmt.Errorf("preamble exceeds %d bytes without message_start", copilotNativeMessagesPreambleMax)
				break
			}
			pendingLines = append(pendingLines, line)
			if eventType != "message_start" {
				continue
			}
			sawMessageStart = true
			for _, pendingLine := range pendingLines {
				writeLine(pendingLine)
			}
			pendingLines = nil
			continue
		}

		if eventType == "message_stop" {
			sawMessageStop = true
		}
		writeLine(line)
	}

	result := &OpenAIForwardResult{
		RequestID:             requestID,
		ResponseID:            responseID,
		Usage:                 openAIUsageFromClaudeUsage(usage),
		Model:                 originalModel,
		BillingModel:          billingModel,
		UpstreamModel:         upstreamModel,
		UpstreamResponseModel: responseModel,
		UpstreamEndpoint:      copilotNativeMessagesEndpoint,
		Stream:                true,
		ResponseHeaders:       resp.Header.Clone(),
		Duration:              time.Since(startTime),
		FirstTokenMs:          firstTokenMs,
		ClientDisconnect:      clientDisconnected,
	}
	if preambleErr != nil {
		return result, sawMessageStart, fmt.Errorf("copilot native messages: stream invalid: %w", preambleErr)
	}
	if err := scanner.Err(); err != nil {
		return result, sawMessageStart, fmt.Errorf("copilot native messages: stream usage incomplete: %w", err)
	}
	if !sawMessageStart {
		return result, false, errors.New("copilot native messages: stream invalid: missing message_start")
	}
	if !sawMessageStop {
		return result, true, errors.New("copilot native messages: stream usage incomplete: missing message_stop")
	}
	return result, true, nil
}

func openAIUsageFromClaudeUsage(usage *ClaudeUsage) OpenAIUsage {
	if usage == nil {
		return OpenAIUsage{}
	}
	inputTokens := max(usage.InputTokens, 0)
	cacheCreation := max(usage.CacheCreationInputTokens, 0)
	cacheRead := max(usage.CacheReadInputTokens, 0)
	return OpenAIUsage{
		InputTokens:              inputTokens + cacheCreation + cacheRead,
		OutputTokens:             max(usage.OutputTokens, 0),
		CacheCreationInputTokens: cacheCreation,
		CacheReadInputTokens:     cacheRead,
	}
}

func copilotNativeMessagesEventStartsOutput(data string) bool {
	if data == "" || data == "[DONE]" {
		return false
	}
	parsed := gjson.Parse(data)
	switch parsed.Get("type").String() {
	case "content_block_start":
		return parsed.Get("content_block").Exists()
	case "content_block_delta":
		return parsed.Get("delta.text").String() != "" ||
			parsed.Get("delta.partial_json").String() != "" ||
			parsed.Get("delta.thinking").String() != ""
	default:
		return false
	}
}
