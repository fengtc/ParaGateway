package repository

import (
	"strings"
	"testing"
	"github.com/Wei-Shaw/sub2api/internal/service"
)

func TestUsageWorkPersistenceOnlyWritesClassificationTable(t *testing.T) {
	row := prepareUsageWork(&service.UsageLog{WorkAttribution:&service.UsageWorkAttribution{Category:service.WorkCategoryCoding,WorkRelated:service.WorkRelatedWork,Confidence:.8,ClassificationSource:"local_rule"}})
	row.usageLogID=1
	query,_:=buildUsageWorkUpsertQuery([]usageWorkPrepared{row})
	if !strings.Contains(query,"INSERT INTO usage_work_classifications") { t.Fatal("classification insert missing") }
	if strings.Contains(query,"usage_work_metadata") || strings.Contains(query,"usage_work_reviews") { t.Fatal("unexpected extra work table") }
}
