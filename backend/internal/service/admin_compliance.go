package service

import (
	"context"
	"time"
)

// AdminComplianceVersion is returned only for compatibility with clients that
// still know the upstream compliance-status contract. ParaGateway never
// requires or records this acknowledgement.
const AdminComplianceVersion = "disabled"

type AdminComplianceAcknowledgement struct {
	Version     string    `json:"version"`
	DocumentZH  string    `json:"document_zh"`
	DocumentEN  string    `json:"document_en"`
	AdminUserID int64     `json:"admin_user_id"`
	IPAddress   string    `json:"ip_address,omitempty"`
	UserAgent   string    `json:"user_agent,omitempty"`
	AcceptedAt  time.Time `json:"accepted_at"`
}

type AdminComplianceStatus struct {
	Required        bool                            `json:"required"`
	Version         string                          `json:"version"`
	DocumentPathZH  string                          `json:"document_path_zh"`
	DocumentPathEN  string                          `json:"document_path_en"`
	DocumentURLZH   string                          `json:"document_url_zh"`
	DocumentURLEN   string                          `json:"document_url_en"`
	AckPhraseZH     string                          `json:"ack_phrase_zh"`
	AckPhraseEN     string                          `json:"ack_phrase_en"`
	Acknowledgement *AdminComplianceAcknowledgement `json:"acknowledgement,omitempty"`
}

type AdminComplianceAcceptInput struct {
	AdminUserID int64
	Phrase      string
	Language    string
	IPAddress   string
	UserAgent   string
}

// GetAdminComplianceStatus permanently reports that no acknowledgement is
// required. It intentionally performs no settings lookup, so an old or missing
// acknowledgement record can never lock an administrator out.
func (s *SettingService) GetAdminComplianceStatus(_ context.Context, _ int64) (*AdminComplianceStatus, error) {
	return &AdminComplianceStatus{
		Required: false,
		Version:  AdminComplianceVersion,
	}, nil
}

func (s *SettingService) IsAdminComplianceAcknowledged(_ context.Context, _ int64) (bool, error) {
	return true, nil
}

// AcceptAdminCompliance remains as a harmless compatibility endpoint for an
// older frontend. No phrase is required and no acknowledgement data is saved.
func (s *SettingService) AcceptAdminCompliance(ctx context.Context, input AdminComplianceAcceptInput) (*AdminComplianceStatus, error) {
	return s.GetAdminComplianceStatus(ctx, input.AdminUserID)
}
