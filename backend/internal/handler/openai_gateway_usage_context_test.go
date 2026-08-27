package handler

import (
	"context"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/pkg/ctxkey"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/stretchr/testify/require"
)

func TestSubmitUsageRecordTaskCopiesRequestContext(t *testing.T) {
	parent := context.WithValue(context.Background(), ctxkey.ClientRequestID, "client-request-123")
	parent = context.WithValue(parent, ctxkey.RequestID, "request-456")

	var gotClientRequestID string
	var gotRequestID string
	h := &GatewayHandler{}
	h.submitUsageRecordTask(parent, func(ctx context.Context) {
		gotClientRequestID, _ = ctx.Value(ctxkey.ClientRequestID).(string)
		gotRequestID, _ = ctx.Value(ctxkey.RequestID).(string)
	})

	require.Equal(t, "client-request-123", gotClientRequestID)
	require.Equal(t, "request-456", gotRequestID)
}

func TestOpenAISubmitUsageRecordTaskCopiesRequestContext(t *testing.T) {
	parent := context.WithValue(context.Background(), ctxkey.ClientRequestID, "openai-client-request-123")
	parent = context.WithValue(parent, ctxkey.RequestID, "openai-request-456")
	parent = service.WithUsageWorkAttribution(parent, service.UsageWorkAttribution{
		ProjectRef: "paragateway", RepositoryRef: "fengtc/ParaGateway",
		SubmissionType: "code", WorkRelated: service.WorkRelatedWork,
		Category: service.WorkCategoryCoding, Confidence: 0.8,
		ClassificationSource: "local_rule", ClassifierVersion: "rules-v1",
	})

	var gotClientRequestID string
	var gotRequestID string
	var gotAttribution service.UsageWorkAttribution
	var gotAttributionOK bool
	h := &OpenAIGatewayHandler{}
	h.submitUsageRecordTask(parent, func(ctx context.Context) {
		gotClientRequestID, _ = ctx.Value(ctxkey.ClientRequestID).(string)
		gotRequestID, _ = ctx.Value(ctxkey.RequestID).(string)
		gotAttribution, gotAttributionOK = service.UsageWorkAttributionFromContext(ctx)
	})

	require.Equal(t, "openai-client-request-123", gotClientRequestID)
	require.Equal(t, "openai-request-456", gotRequestID)
	require.True(t, gotAttributionOK)
	require.Equal(t, "paragateway", gotAttribution.ProjectRef)
	require.Equal(t, "coding", gotAttribution.SubmissionType)
	require.Equal(t, service.WorkCategoryCoding, gotAttribution.Category)
}
