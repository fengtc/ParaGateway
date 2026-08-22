package service

import (
	"context"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/stretchr/testify/require"
)

type adminComplianceRepoStub struct {
	values   map[string]string
	setCalls int
}

func (r *adminComplianceRepoStub) Get(ctx context.Context, key string) (*Setting, error) {
	if value, ok := r.values[key]; ok {
		return &Setting{Key: key, Value: value}, nil
	}
	return nil, ErrSettingNotFound
}

func (r *adminComplianceRepoStub) GetValue(ctx context.Context, key string) (string, error) {
	setting, err := r.Get(ctx, key)
	if err != nil {
		return "", err
	}
	return setting.Value, nil
}

func (r *adminComplianceRepoStub) Set(ctx context.Context, key, value string) error {
	r.setCalls++
	return nil
}

func (r *adminComplianceRepoStub) GetMultiple(ctx context.Context, keys []string) (map[string]string, error) {
	return map[string]string{}, nil
}

func (r *adminComplianceRepoStub) SetMultiple(ctx context.Context, settings map[string]string) error {
	return nil
}

func (r *adminComplianceRepoStub) GetAll(ctx context.Context) (map[string]string, error) {
	return map[string]string{}, nil
}

func (r *adminComplianceRepoStub) Delete(ctx context.Context, key string) error {
	return nil
}

func TestAdminComplianceStatusIsPermanentlyOptional(t *testing.T) {
	repo := &adminComplianceRepoStub{values: map[string]string{
		"admin_compliance_acknowledgement:1": `{"version":"v2026.01.01"}`,
	}}
	svc := NewSettingService(repo, &config.Config{})

	status, err := svc.GetAdminComplianceStatus(context.Background(), 1)
	require.NoError(t, err)
	require.False(t, status.Required)
	require.Equal(t, AdminComplianceVersion, status.Version)
	require.Empty(t, status.DocumentPathZH)
	require.Empty(t, status.DocumentURLZH)
	require.Empty(t, status.AckPhraseZH)
	require.Nil(t, status.Acknowledgement)

	acknowledged, err := svc.IsAdminComplianceAcknowledged(context.Background(), 1)
	require.NoError(t, err)
	require.True(t, acknowledged)
}

func TestAcceptAdminComplianceIsNoOpCompatibilityEndpoint(t *testing.T) {
	repo := &adminComplianceRepoStub{}
	svc := NewSettingService(repo, &config.Config{})

	status, err := svc.AcceptAdminCompliance(context.Background(), AdminComplianceAcceptInput{
		AdminUserID: 42,
		Phrase:      "",
		Language:    "zh-CN",
	})
	require.NoError(t, err)
	require.False(t, status.Required)
	require.Zero(t, repo.setCalls)
}
