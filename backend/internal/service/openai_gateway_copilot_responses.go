package service

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"strings"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/pkg/apicompat"
	"github.com/gin-gonic/gin"
	"github.com/tidwall/gjson"
)

// isCopilotUnsupportedAPIForModel matches the exact structured error used by
// Copilot when a model cannot serve Chat Completions but can serve Responses.
func isCopilotUnsupportedAPIForModel(statusCode int, body []byte) bool {
	return statusCode == http.StatusBadRequest &&
		strings.EqualFold(strings.TrimSpace(gjson.GetBytes(body, "error.code").String()), "unsupported_api_for_model")
}

// forwardCopilotChatCompletionsViaResponses mirrors the standalone Worker's
// bounded fallback: only an explicit unsupported_api_for_model response from
// /chat/completions reaches this path, and /responses is attempted once.
func (s *OpenAIGatewayService) forwardCopilotChatCompletionsViaResponses(
	ctx context.Context,
	c *gin.Context,
	account *Account,
	body []byte,
	defaultMappedModel string,
) (*OpenAIForwardResult, error) {
	if account == nil || !account.IsGitHubCopilot() {
		return nil, fmt.Errorf("copilot responses fallback requires a GitHub Copilot account")
	}
	startTime := time.Now()

	var chatReq apicompat.ChatCompletionsRequest
	if err := json.Unmarshal(body, &chatReq); err != nil {
		writeChatCompletionsError(c, http.StatusBadRequest, "invalid_request_error", "Failed to parse request body")
		return nil, fmt.Errorf("parse Copilot chat request: %w", err)
	}
	originalModel := strings.TrimSpace(chatReq.Model)
	if originalModel == "" {
		writeChatCompletionsError(c, http.StatusBadRequest, "invalid_request_error", "model is required")
		return nil, fmt.Errorf("missing model in Copilot chat request")
	}
	clientStream := chatReq.Stream
	billingModel := resolveOpenAIForwardModel(account, originalModel, defaultMappedModel)
	upstreamModel := normalizeCopilotModel(billingModel)

	converted, err := apicompat.ChatCompletionsToResponses(&chatReq)
	if err != nil {
		return nil, fmt.Errorf("convert Copilot chat request to responses: %w", err)
	}
	inputText, promotedInstructions, err := copilotResponsesInputText(converted.Input)
	if err != nil {
		return nil, fmt.Errorf("normalize Copilot responses input: %w", err)
	}
	converted.Model = upstreamModel
	converted.Input, _ = json.Marshal(inputText)
	converted.Instructions = joinCopilotInstructions(converted.Instructions, promotedInstructions)
	converted.Stream = true
	storeFalse := false
	converted.Store = &storeFalse
	// The Worker intentionally omits standard OpenAI sampling/limit/tier fields
	// from Copilot Responses because that endpoint rejects them for some models.
	converted.MaxOutputTokens = nil
	converted.Temperature = nil
	converted.TopP = nil
	converted.ServiceTier = ""

	upstreamBody, err := json.Marshal(converted)
	if err != nil {
		return nil, fmt.Errorf("marshal Copilot responses request: %w", err)
	}
	token, _, err := s.getRequestCredential(ctx, c, account)
	if err != nil {
		return nil, err
	}
	baseURL, err := s.validateUpstreamBaseURL(account.GetOpenAIBaseURL())
	if err != nil {
		return nil, err
	}
	targetURL := buildCopilotAPIURL(baseURL, "/responses")
	resp, err := s.sendCCUpstreamRequest(ctx, c, account, targetURL, upstreamBody, true, token, account.GetOpenAIUserAgent(), "")
	if err != nil {
		return nil, err
	}
	defer func() { _ = resp.Body.Close() }()

	if resp.StatusCode >= http.StatusBadRequest {
		respBody, upstreamMsg := s.readOpenAIUpstreamError(resp)
		if failoverErr := s.failoverOpenAIUpstreamHTTPError(ctx, c, account, resp, respBody, upstreamMsg, upstreamModel); failoverErr != nil {
			return nil, failoverErr
		}
		return s.handleChatCompletionsErrorResponse(resp, c, account, billingModel)
	}

	var result *OpenAIForwardResult
	if clientStream {
		result, err = s.handleChatStreamingResponse(resp, c, account, originalModel, billingModel, upstreamModel, startTime, len(body))
	} else {
		result, err = s.handleChatBufferedStreamingResponse(resp, c, account, originalModel, billingModel, upstreamModel, startTime)
	}
	if result != nil && converted.Reasoning != nil && converted.Reasoning.Effort != "" {
		effort := converted.Reasoning.Effort
		result.ReasoningEffort = &effort
	}
	return result, err
}

func joinCopilotInstructions(existing string, promoted []string) string {
	parts := make([]string, 0, len(promoted)+1)
	if existing = strings.TrimSpace(existing); existing != "" {
		parts = append(parts, existing)
	}
	for _, value := range promoted {
		if value = strings.TrimSpace(value); value != "" {
			parts = append(parts, value)
		}
	}
	return strings.Join(parts, "\n\n")
}

func copilotResponsesInputText(raw json.RawMessage) (string, []string, error) {
	var text string
	if err := json.Unmarshal(raw, &text); err == nil {
		return text, nil, nil
	}

	var items []apicompat.ResponsesInputItem
	if err := json.Unmarshal(raw, &items); err != nil {
		return "", nil, err
	}
	type inputLine struct {
		role string
		text string
	}
	lines := make([]inputLine, 0, len(items))
	promoted := make([]string, 0, 2)
	for _, item := range items {
		itemText := strings.TrimSpace(copilotResponsesInputItemText(item))
		if itemText == "" {
			continue
		}
		role := strings.ToLower(strings.TrimSpace(item.Role))
		if role == "system" || role == "developer" {
			promoted = append(promoted, itemText)
			continue
		}
		lines = append(lines, inputLine{role: role, text: itemText})
	}
	if len(lines) == 1 && lines[0].role == "user" {
		return lines[0].text, promoted, nil
	}
	formatted := make([]string, 0, len(lines))
	for _, line := range lines {
		if line.role == "" {
			formatted = append(formatted, line.text)
		} else {
			formatted = append(formatted, line.role+": "+line.text)
		}
	}
	return strings.Join(formatted, "\n\n"), promoted, nil
}

func copilotResponsesInputItemText(item apicompat.ResponsesInputItem) string {
	if len(item.Content) > 0 {
		var text string
		if err := json.Unmarshal(item.Content, &text); err == nil {
			return text
		}
		var parts []apicompat.ResponsesContentPart
		if err := json.Unmarshal(item.Content, &parts); err == nil {
			values := make([]string, 0, len(parts))
			for _, part := range parts {
				switch part.Type {
				case "text", "input_text", "output_text":
					if part.Text != "" {
						values = append(values, part.Text)
					}
				case "input_image":
					if part.ImageURL != "" {
						values = append(values, "[image: "+part.ImageURL+"]")
					}
				}
			}
			return strings.Join(values, "\n")
		}
	}
	switch item.Type {
	case "function_call":
		arguments := item.Arguments
		if strings.TrimSpace(arguments) == "" {
			arguments = "{}"
		}
		return "function_call " + item.Name + ": " + arguments
	case "function_call_output":
		callID := ""
		if item.CallID != "" {
			callID = " " + item.CallID
		}
		return "function_call_output" + callID + ": " + item.Output
	}
	return ""
}
