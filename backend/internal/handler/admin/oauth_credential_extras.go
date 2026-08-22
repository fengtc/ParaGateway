package admin

import (
	"fmt"
	"strings"
)

// mergeOAuthCredentialExtras keeps OAuth tokens server-owned while allowing
// the account editor to attach the non-secret model routing configuration.
func mergeOAuthCredentialExtras(credentials, extras map[string]any) (map[string]any, error) {
	merged := make(map[string]any, len(credentials)+1)
	for key, value := range credentials {
		merged[key] = value
	}

	rawMapping, exists := extras["model_mapping"]
	if !exists {
		return merged, nil
	}

	mapping, err := normalizeOAuthModelMapping(rawMapping)
	if err != nil {
		return nil, err
	}
	merged["model_mapping"] = mapping
	return merged, nil
}

func normalizeOAuthModelMapping(raw any) (map[string]any, error) {
	var input map[string]any
	switch value := raw.(type) {
	case map[string]any:
		input = value
	case map[string]string:
		input = make(map[string]any, len(value))
		for from, to := range value {
			input[from] = to
		}
	default:
		return nil, fmt.Errorf("model_mapping must be an object")
	}

	result := make(map[string]any, len(input))
	for rawFrom, rawTo := range input {
		from := strings.TrimSpace(rawFrom)
		to, ok := rawTo.(string)
		to = strings.TrimSpace(to)
		if !ok || from == "" || to == "" {
			return nil, fmt.Errorf("model_mapping keys and values must be non-empty strings")
		}
		if len(from) > 240 || len(to) > 240 {
			return nil, fmt.Errorf("model_mapping names must not exceed 240 characters")
		}
		if star := strings.IndexByte(from, '*'); star >= 0 && (star != len(from)-1 || strings.LastIndexByte(from, '*') != star) {
			return nil, fmt.Errorf("model_mapping source wildcard must appear once at the end")
		}
		if strings.ContainsRune(to, '*') {
			return nil, fmt.Errorf("model_mapping target must not contain a wildcard")
		}
		result[from] = to
	}
	return result, nil
}
