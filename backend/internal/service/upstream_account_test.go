package service

import (
	"context"
	"errors"
	"io"
	"net/http"
	"strings"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/stretchr/testify/require"
)

type upstreamAccountMemoryRepo struct {
	items   map[string]UpstreamAccount
	deleted []string
}

func newUpstreamAccountMemoryRepo() *upstreamAccountMemoryRepo {
	return &upstreamAccountMemoryRepo{items: make(map[string]UpstreamAccount)}
}

func (r *upstreamAccountMemoryRepo) Create(_ context.Context, account *UpstreamAccount) error {
	r.items[account.ID] = *account
	return nil
}

func (r *upstreamAccountMemoryRepo) GetByID(_ context.Context, id string) (*UpstreamAccount, error) {
	account, ok := r.items[id]
	if !ok || account.DeletedAt != nil {
		return nil, ErrUpstreamAccountNotFound
	}
	copy := account
	return &copy, nil
}

func (r *upstreamAccountMemoryRepo) List(_ context.Context) ([]UpstreamAccount, error) {
	items := make([]UpstreamAccount, 0, len(r.items))
	for _, account := range r.items {
		if account.DeletedAt == nil {
			items = append(items, account)
		}
	}
	return items, nil
}

func (r *upstreamAccountMemoryRepo) Update(_ context.Context, account *UpstreamAccount) error {
	if _, ok := r.items[account.ID]; !ok {
		return ErrUpstreamAccountNotFound
	}
	r.items[account.ID] = *account
	return nil
}

func (r *upstreamAccountMemoryRepo) SoftDelete(_ context.Context, id string, deletedAt time.Time) error {
	account, ok := r.items[id]
	if !ok {
		return ErrUpstreamAccountNotFound
	}
	account.DeletedAt = &deletedAt
	account.IsActive = false
	r.items[id] = account
	r.deleted = append(r.deleted, id)
	return nil
}

type upstreamTestEncryptor struct{}

func (upstreamTestEncryptor) Encrypt(value string) (string, error) { return "encrypted:" + value, nil }
func (upstreamTestEncryptor) Decrypt(value string) (string, error) {
	if !strings.HasPrefix(value, "encrypted:") {
		return "", errors.New("invalid ciphertext")
	}
	return strings.TrimPrefix(value, "encrypted:"), nil
}

type upstreamRoundTripper func(*http.Request) (*http.Response, error)

func (f upstreamRoundTripper) RoundTrip(request *http.Request) (*http.Response, error) {
	return f(request)
}

func newTestUpstreamAccountService(repo UpstreamAccountRepository) *UpstreamAccountService {
	cfg := &config.Config{}
	cfg.Totp.EncryptionKeyConfigured = true
	return NewUpstreamAccountService(repo, upstreamTestEncryptor{}, nil, cfg)
}

func validUpstreamAPIKeyInput() UpstreamAccountInput {
	return UpstreamAccountInput{
		Name: "OpenAI primary", ProviderType: UpstreamProviderOpenAI,
		BaseURL: "https://upstream.example", AuthType: UpstreamAuthAPIKey,
		APIKey: "sk-secret-value", IsActive: true, Priority: 100, Weight: 100,
		MaxConcurrency: 8, RPMLimit: 120, CircuitBreakerThreshold: 3,
		CircuitBreakerCooldownSeconds: 60,
	}
}

func TestUpstreamAccountCreateEncryptsCredentialInIndependentRepository(t *testing.T) {
	repo := newUpstreamAccountMemoryRepo()
	svc := newTestUpstreamAccountService(repo)

	account, err := svc.Create(context.Background(), validUpstreamAPIKeyInput())

	require.NoError(t, err)
	require.NotEmpty(t, account.ID)
	require.Equal(t, "encrypted:sk-secret-value", repo.items[account.ID].CredentialCiphertext)
	require.Equal(t, "alue", repo.items[account.ID].CredentialHint)
	require.NotContains(t, repo.items[account.ID].CredentialCiphertext, `"api_key"`)
}

func TestUpstreamAccountUpdatePreservesSecretButBoundaryChangeRequiresReplacement(t *testing.T) {
	repo := newUpstreamAccountMemoryRepo()
	svc := newTestUpstreamAccountService(repo)
	created, err := svc.Create(context.Background(), validUpstreamAPIKeyInput())
	require.NoError(t, err)

	input := validUpstreamAPIKeyInput()
	input.APIKey = ""
	input.Name = "renamed"
	updated, err := svc.Update(context.Background(), created.ID, input)
	require.NoError(t, err)
	require.Equal(t, "renamed", updated.Name)
	require.Equal(t, "encrypted:sk-secret-value", updated.CredentialCiphertext)

	input.BaseURL = "https://another-upstream.example"
	_, err = svc.Update(context.Background(), created.ID, input)
	require.ErrorContains(t, err, "API Key")

	input.APIKey = "sk-replacement"
	updated, err = svc.Update(context.Background(), created.ID, input)
	require.NoError(t, err)
	require.Equal(t, "encrypted:sk-replacement", updated.CredentialCiphertext)
}

func TestUpstreamAccountSchedulingAndDeleteDoNotTouchOfficialAccounts(t *testing.T) {
	repo := newUpstreamAccountMemoryRepo()
	svc := newTestUpstreamAccountService(repo)
	created, err := svc.Create(context.Background(), validUpstreamAPIKeyInput())
	require.NoError(t, err)

	updated, err := svc.SetActive(context.Background(), created.ID, false)
	require.NoError(t, err)
	require.False(t, updated.IsActive)
	require.NoError(t, svc.Delete(context.Background(), created.ID))
	require.Equal(t, []string{created.ID}, repo.deleted)
	_, err = svc.Get(context.Background(), created.ID)
	require.ErrorIs(t, err, ErrUpstreamAccountNotFound)
}

func TestUpstreamAccountDraftTestUsesModelsAndProviderAuthentication(t *testing.T) {
	repo := newUpstreamAccountMemoryRepo()
	svc := newTestUpstreamAccountService(repo)
	var captured *http.Request
	svc.httpClient = &http.Client{Transport: upstreamRoundTripper(func(request *http.Request) (*http.Response, error) {
		captured = request.Clone(request.Context())
		return &http.Response{
			StatusCode: http.StatusOK,
			Header:     make(http.Header),
			Body:       io.NopCloser(strings.NewReader(`{"data":[{"id":"gpt-test"}]}`)),
			Request:    request,
		}, nil
	})}

	result, err := svc.TestDraft(context.Background(), validUpstreamAPIKeyInput())

	require.NoError(t, err)
	require.True(t, result.Success)
	require.Equal(t, 1, *result.ModelCount)
	require.Equal(t, "https://upstream.example/v1/models", captured.URL.String())
	require.Equal(t, "Bearer sk-secret-value", captured.Header.Get("Authorization"))
}

func TestUpstreamAccountRequiresFixedEncryptionKeyAndHTTPS(t *testing.T) {
	repo := newUpstreamAccountMemoryRepo()
	input := validUpstreamAPIKeyInput()
	input.BaseURL = "http://127.0.0.1:8080"
	_, err := newTestUpstreamAccountService(repo).Create(context.Background(), input)
	require.ErrorContains(t, err, "HTTPS")

	cfg := &config.Config{}
	svc := NewUpstreamAccountService(repo, upstreamTestEncryptor{}, nil, cfg)
	input = validUpstreamAPIKeyInput()
	_, err = svc.Create(context.Background(), input)
	require.ErrorContains(t, err, "TOTP_ENCRYPTION_KEY")
}
