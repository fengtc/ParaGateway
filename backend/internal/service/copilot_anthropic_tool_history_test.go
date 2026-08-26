package service

import (
	"encoding/json"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/pkg/apicompat"
	"github.com/stretchr/testify/require"
)

func TestSanitizeCopilotAnthropicToolHistory(t *testing.T) {
	t.Run("valid tool chain remains tool calls", func(t *testing.T) {
		chatReq := mustConvertCopilotAnthropicToChat(t, `{
			"model":"claude-opus-4-8",
			"max_tokens":64,
			"messages":[
				{"role":"assistant","content":[{"type":"tool_use","id":"toolu_ok","name":"Read","input":{"file_path":"a.go"}}]},
				{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_ok","content":[{"type":"text","text":"package a"}]},{"type":"text","text":"continue"}]}
			]
		}`)

		require.Len(t, chatReq.Messages, 3)
		require.Len(t, chatReq.Messages[0].ToolCalls, 1)
		require.Equal(t, "toolu_ok", chatReq.Messages[0].ToolCalls[0].ID)
		require.Equal(t, "tool", chatReq.Messages[1].Role)
		require.Equal(t, "toolu_ok", chatReq.Messages[1].ToolCallID)
		require.JSONEq(t, `"package a"`, string(chatReq.Messages[1].Content))
	})

	t.Run("missing result downgrades only incomplete tool use", func(t *testing.T) {
		chatReq := mustConvertCopilotAnthropicToChat(t, `{
			"model":"claude-opus-4-8",
			"max_tokens":64,
			"messages":[
				{"role":"assistant","content":[
					{"type":"tool_use","id":"toolu_keep","name":"Read","input":{"file_path":"a.go"}},
					{"type":"tool_use","id":"toolu_missing","name":"Bash","input":{"command":"ls"}}
				]},
				{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_keep","content":"package a"}]}
			]
		}`)

		require.Len(t, chatReq.Messages, 2)
		require.Len(t, chatReq.Messages[0].ToolCalls, 1)
		require.Equal(t, "toolu_keep", chatReq.Messages[0].ToolCalls[0].ID)
		require.Contains(t, string(chatReq.Messages[0].Content), "toolu_missing")
		require.Equal(t, "tool", chatReq.Messages[1].Role)
		require.Equal(t, "toolu_keep", chatReq.Messages[1].ToolCallID)
	})

	t.Run("orphan result becomes user text", func(t *testing.T) {
		chatReq := mustConvertCopilotAnthropicToChat(t, `{
			"model":"claude-opus-4-8",
			"max_tokens":64,
			"messages":[
				{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_orphan","content":"stale result"},{"type":"text","text":"next task"}]}
			]
		}`)

		require.Len(t, chatReq.Messages, 1)
		require.Equal(t, "user", chatReq.Messages[0].Role)
		require.Empty(t, chatReq.Messages[0].ToolCallID)
		require.Contains(t, string(chatReq.Messages[0].Content), "toolu_orphan")
		require.Contains(t, string(chatReq.Messages[0].Content), "next task")
	})
}

func mustConvertCopilotAnthropicToChat(t *testing.T, body string) *apicompat.ChatCompletionsRequest {
	t.Helper()

	var req apicompat.AnthropicRequest
	require.NoError(t, json.Unmarshal([]byte(body), &req))
	sanitizeCopilotAnthropicToolHistory(&req)
	chatReq, err := apicompat.AnthropicToChatCompletionsRequest(&req)
	require.NoError(t, err)
	return chatReq
}
