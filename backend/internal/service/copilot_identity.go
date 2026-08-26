package service

import (
	"maps"
	"strings"

	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
)

const accountOAuthProfileCredentialKey = "oauth_profile"
const legacyGitHubCopilotPlatform = "copilot"

// NormalizeGitHubCopilotIdentity converts the legacy Copilot API-key shape to
// the canonical OpenAI OAuth profile used by storage and scheduling. It always
// returns a cloned credentials map and never modifies the caller-owned map.
func NormalizeGitHubCopilotIdentity(
	platform string,
	accountType string,
	credentials map[string]any,
) (normalizedPlatform string, normalizedType string, normalizedCredentials map[string]any, isGitHubCopilot bool, err error) {
	normalizedPlatform = platform
	normalizedType = accountType
	normalizedCredentials = maps.Clone(credentials)

	if strings.EqualFold(strings.TrimSpace(platform), legacyGitHubCopilotPlatform) {
		if !strings.EqualFold(strings.TrimSpace(accountType), AccountTypeAPIKey) {
			return normalizedPlatform, normalizedType, normalizedCredentials, false, infraerrors.BadRequest(
				"COPILOT_IDENTITY_INVALID",
				"Legacy GitHub Copilot accounts must use type=apikey",
			)
		}

		legacyToken := trimmedCredentialString(credentials, "github_token")
		canonicalToken := trimmedCredentialString(credentials, "github_access_token")
		if legacyToken != "" && canonicalToken != "" && legacyToken != canonicalToken {
			return normalizedPlatform, normalizedType, normalizedCredentials, false, infraerrors.BadRequest(
				"COPILOT_GITHUB_TOKEN_CONFLICT",
				"github_token and github_access_token must match when both are provided",
			)
		}

		githubToken := canonicalToken
		if githubToken == "" {
			githubToken = legacyToken
		}
		if githubToken == "" {
			return normalizedPlatform, normalizedType, normalizedCredentials, false, infraerrors.BadRequest(
				"COPILOT_GITHUB_TOKEN_REQUIRED",
				"Legacy GitHub Copilot accounts require a non-empty github_token or github_access_token",
			)
		}

		if normalizedCredentials == nil {
			normalizedCredentials = make(map[string]any, 2)
		}
		delete(normalizedCredentials, "github_token")
		normalizedCredentials["github_access_token"] = githubToken
		normalizedCredentials[accountOAuthProfileCredentialKey] = CopilotOAuthProfile
		return PlatformOpenAI, AccountTypeOAuth, normalizedCredentials, true, nil
	}

	profile, ok := normalizedCredentials[accountOAuthProfileCredentialKey].(string)
	if !ok || !strings.EqualFold(strings.TrimSpace(profile), CopilotOAuthProfile) {
		return normalizedPlatform, normalizedType, normalizedCredentials, false, nil
	}
	if !strings.EqualFold(strings.TrimSpace(platform), PlatformOpenAI) ||
		!strings.EqualFold(strings.TrimSpace(accountType), AccountTypeOAuth) {
		return normalizedPlatform, normalizedType, normalizedCredentials, false, infraerrors.BadRequest(
			"COPILOT_IDENTITY_INVALID",
			"GitHub Copilot accounts must use platform=openai and type=oauth",
		)
	}

	normalizedCredentials[accountOAuthProfileCredentialKey] = CopilotOAuthProfile
	return PlatformOpenAI, AccountTypeOAuth, normalizedCredentials, true, nil
}

func trimmedCredentialString(credentials map[string]any, key string) string {
	value, _ := credentials[key].(string)
	return strings.TrimSpace(value)
}
