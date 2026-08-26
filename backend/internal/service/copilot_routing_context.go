package service

import (
	"context"

	"github.com/Wei-Shaw/sub2api/internal/pkg/ctxkey"
)

// WithGitHubCopilotOnly restricts account selection to the canonical GitHub
// Copilot identity: openai + oauth + oauth_profile=github_copilot.
func WithGitHubCopilotOnly(ctx context.Context) context.Context {
	if ctx == nil {
		ctx = context.Background()
	}
	return context.WithValue(ctx, ctxkey.GitHubCopilotOnly, true)
}

// IsGitHubCopilotOnly reports whether the request came through the legacy
// /copilot/v1 compatibility surface.
func IsGitHubCopilotOnly(ctx context.Context) bool {
	if ctx == nil {
		return false
	}
	required, _ := ctx.Value(ctxkey.GitHubCopilotOnly).(bool)
	return required
}
