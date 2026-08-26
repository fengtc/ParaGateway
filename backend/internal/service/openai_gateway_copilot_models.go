package service

import (
	"bytes"
	"context"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"regexp"
	"strings"
)

var copilotLiveClaudeModelDotPattern = regexp.MustCompile(`claude-(?:sonnet|opus|haiku)-\d+\.\d+`)

// FetchGitHubCopilotModels requests the account-specific root /models catalog.
// It preserves GitHub's response envelope and only normalizes dotted Claude
// model IDs for clients that expect the public hyphenated spelling.
func (s *OpenAIGatewayService) FetchGitHubCopilotModels(ctx context.Context, account *Account) ([]byte, error) {
	if s == nil || s.httpUpstream == nil {
		return nil, fmt.Errorf("copilot models: upstream client is not configured")
	}
	if account == nil || !account.IsGitHubCopilot() {
		return nil, fmt.Errorf("copilot models: canonical GitHub Copilot account is required")
	}

	token, _, err := s.getRequestCredential(ctx, nil, account)
	if err != nil {
		return nil, fmt.Errorf("copilot models: get access token: %w", err)
	}
	if strings.TrimSpace(token) == "" {
		return nil, fmt.Errorf("copilot models: account %d has no access token", account.ID)
	}

	baseURL := account.GetOpenAIBaseURL()
	validatedBaseURL := ""
	if s.cfg != nil {
		validatedBaseURL, err = s.validateUpstreamBaseURL(baseURL)
	} else {
		var parsed *url.URL
		parsed, err = url.ParseRequestURI(baseURL)
		if err == nil && (!strings.EqualFold(parsed.Scheme, "https") || strings.TrimSpace(parsed.Host) == "") {
			err = fmt.Errorf("HTTPS URL with host is required")
		}
		if err == nil {
			validatedBaseURL = parsed.String()
		}
	}
	if err != nil {
		return nil, fmt.Errorf("copilot models: invalid base URL: %w", err)
	}

	request, err := http.NewRequestWithContext(ctx, http.MethodGet, buildCopilotAPIURL(validatedBaseURL, "/models"), nil)
	if err != nil {
		return nil, fmt.Errorf("copilot models: build request: %w", err)
	}
	request = request.WithContext(WithHTTPUpstreamProfile(request.Context(), HTTPUpstreamProfileOpenAI))
	for key, value := range copilotHeaders(token, false) {
		request.Header.Set(key, value)
	}
	request.Header.Set("Accept", "application/json")
	account.ApplyHeaderOverrides(request.Header)

	proxyURL := ""
	if account.ProxyID != nil && account.Proxy != nil {
		proxyURL = account.Proxy.URL()
	}
	response, err := s.httpUpstream.Do(request, proxyURL, account.ID, account.Concurrency)
	if err != nil {
		return nil, fmt.Errorf("copilot models: request failed: %w", err)
	}
	if response == nil || response.Body == nil {
		return nil, fmt.Errorf("copilot models: upstream returned no response")
	}
	defer func() { _ = response.Body.Close() }()

	body, err := io.ReadAll(response.Body)
	if err != nil {
		return nil, fmt.Errorf("copilot models: read response: %w", err)
	}
	if response.StatusCode != http.StatusOK {
		return nil, fmt.Errorf("copilot models: upstream returned HTTP %d", response.StatusCode)
	}

	return copilotLiveClaudeModelDotPattern.ReplaceAllFunc(body, func(match []byte) []byte {
		return bytes.ReplaceAll(match, []byte{'.'}, []byte{'-'})
	}), nil
}
