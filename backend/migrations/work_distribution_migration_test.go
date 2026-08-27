package migrations

import (
	"os"
	"strings"
	"testing"
)
func TestWorkDistributionMigrationContainsOnlyClassificationTable(t *testing.T){data,err:=os.ReadFile("236_work_distribution.sql");if err!=nil{t.Fatal(err)};sql:=strings.ToLower(string(data));if !strings.Contains(sql,"create table if not exists usage_work_classifications"){t.Fatal("classification table missing")};for _,name:=range[]string{"usage_work_metadata","usage_work_reviews"}{if strings.Contains(sql,name){t.Fatalf("unexpected table %s",name)}}}
