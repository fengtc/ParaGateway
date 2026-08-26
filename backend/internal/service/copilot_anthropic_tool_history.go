package service

import (
	"encoding/json"
	"fmt"
	"strings"

	"github.com/Wei-Shaw/sub2api/internal/pkg/apicompat"
)

// sanitizeCopilotAnthropicToolHistory preserves the legacy Copilot behavior for
// malformed Claude tool histories. Copilot validates that every assistant
// tool_use is followed immediately by a matching user tool_result; incomplete
// histories are kept as readable text instead of being silently discarded.
func sanitizeCopilotAnthropicToolHistory(req *apicompat.AnthropicRequest) {
	if req == nil || len(req.Messages) == 0 {
		return
	}

	expectedByUserIndex := make(map[int]map[string]struct{})
	for i := range req.Messages {
		msg := &req.Messages[i]
		switch msg.Role {
		case "assistant":
			toolUses := copilotAnthropicToolUseIDs(msg.Content)
			if len(toolUses) == 0 {
				continue
			}

			nextIdx := i + 1
			nextResults := map[string]struct{}{}
			if nextIdx < len(req.Messages) && req.Messages[nextIdx].Role == "user" {
				nextResults = copilotAnthropicToolResultIDs(req.Messages[nextIdx].Content)
			}

			kept := make(map[string]struct{}, len(toolUses))
			missing := make(map[string]struct{})
			for id := range toolUses {
				if _, ok := nextResults[id]; ok {
					kept[id] = struct{}{}
				} else {
					missing[id] = struct{}{}
				}
			}
			if len(missing) > 0 {
				msg.Content = downgradeCopilotAnthropicToolUses(msg.Content, missing)
			}
			if len(kept) > 0 && nextIdx < len(req.Messages) && req.Messages[nextIdx].Role == "user" {
				expectedByUserIndex[nextIdx] = kept
			}

		case "user":
			results := copilotAnthropicToolResultIDs(msg.Content)
			if len(results) == 0 {
				continue
			}

			expected := expectedByUserIndex[i]
			orphaned := make(map[string]struct{})
			for id := range results {
				if _, ok := expected[id]; !ok {
					orphaned[id] = struct{}{}
				}
			}
			if len(orphaned) > 0 {
				msg.Content = downgradeCopilotAnthropicToolResults(msg.Content, orphaned)
			}
		}
	}
}

func copilotAnthropicToolUseIDs(content json.RawMessage) map[string]struct{} {
	ids := make(map[string]struct{})
	var blocks []json.RawMessage
	if err := json.Unmarshal(content, &blocks); err != nil {
		return ids
	}
	for _, raw := range blocks {
		var block struct {
			Type string `json:"type"`
			ID   string `json:"id"`
		}
		if err := json.Unmarshal(raw, &block); err == nil && block.Type == "tool_use" && block.ID != "" {
			ids[block.ID] = struct{}{}
		}
	}
	return ids
}

func copilotAnthropicToolResultIDs(content json.RawMessage) map[string]struct{} {
	ids := make(map[string]struct{})
	var blocks []json.RawMessage
	if err := json.Unmarshal(content, &blocks); err != nil {
		return ids
	}
	for _, raw := range blocks {
		var block struct {
			Type      string `json:"type"`
			ToolUseID string `json:"tool_use_id"`
		}
		if err := json.Unmarshal(raw, &block); err == nil && block.Type == "tool_result" && block.ToolUseID != "" {
			ids[block.ToolUseID] = struct{}{}
		}
	}
	return ids
}

func downgradeCopilotAnthropicToolUses(content json.RawMessage, ids map[string]struct{}) json.RawMessage {
	return rewriteCopilotAnthropicContentBlocks(content, func(raw json.RawMessage) (json.RawMessage, bool) {
		var block apicompat.AnthropicContentBlock
		if err := json.Unmarshal(raw, &block); err != nil || block.Type != "tool_use" {
			return raw, false
		}
		if _, ok := ids[block.ID]; !ok {
			return raw, false
		}

		input := strings.TrimSpace(string(block.Input))
		if input == "" || input == "null" {
			input = "{}"
		}
		text := fmt.Sprintf("[tool_use omitted: id=%s name=%s input=%s]", block.ID, block.Name, input)
		return marshalCopilotAnthropicTextBlock(text), true
	})
}

func downgradeCopilotAnthropicToolResults(content json.RawMessage, ids map[string]struct{}) json.RawMessage {
	return rewriteCopilotAnthropicContentBlocks(content, func(raw json.RawMessage) (json.RawMessage, bool) {
		var block apicompat.AnthropicContentBlock
		if err := json.Unmarshal(raw, &block); err != nil || block.Type != "tool_result" {
			return raw, false
		}
		if _, ok := ids[block.ToolUseID]; !ok {
			return raw, false
		}

		contentText := strings.TrimSpace(copilotAnthropicToolResultText(block.Content))
		if contentText == "" {
			contentText = "(empty)"
		}
		text := fmt.Sprintf("[tool_result omitted: tool_use_id=%s content=%s]", block.ToolUseID, contentText)
		return marshalCopilotAnthropicTextBlock(text), true
	})
}

func rewriteCopilotAnthropicContentBlocks(
	content json.RawMessage,
	rewrite func(json.RawMessage) (json.RawMessage, bool),
) json.RawMessage {
	var blocks []json.RawMessage
	if err := json.Unmarshal(content, &blocks); err != nil {
		return content
	}

	changed := false
	for i, raw := range blocks {
		if replacement, ok := rewrite(raw); ok {
			blocks[i] = replacement
			changed = true
		}
	}
	if !changed {
		return content
	}

	out, err := json.Marshal(blocks)
	if err != nil {
		return content
	}
	return out
}

func marshalCopilotAnthropicTextBlock(text string) json.RawMessage {
	out, _ := json.Marshal(apicompat.AnthropicContentBlock{Type: "text", Text: text})
	return out
}

func copilotAnthropicToolResultText(raw json.RawMessage) string {
	if len(raw) == 0 || string(raw) == "null" {
		return ""
	}

	var text string
	if err := json.Unmarshal(raw, &text); err == nil {
		return text
	}

	var blocks []json.RawMessage
	if err := json.Unmarshal(raw, &blocks); err != nil {
		return strings.TrimSpace(string(raw))
	}

	parts := make([]string, 0, len(blocks))
	for _, blockRaw := range blocks {
		var block struct {
			Type string `json:"type"`
			Text string `json:"text"`
		}
		if err := json.Unmarshal(blockRaw, &block); err != nil {
			continue
		}
		if block.Type == "text" {
			if block.Text != "" {
				parts = append(parts, block.Text)
			}
			continue
		}
		parts = append(parts, strings.TrimSpace(string(blockRaw)))
	}
	return strings.Join(parts, "\n")
}
