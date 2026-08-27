package repository

import (
	"testing"
	"time"
	"github.com/Wei-Shaw/sub2api/internal/service"
)

func TestBuildWorkScopeWhereIncludesRequestedFilters(t *testing.T) {
	where,args:=buildWorkScopeWhere(service.WorkDistributionFilter{StartTime:time.Now().Add(-time.Hour),EndTime:time.Now(),UserID:7,Department:"研发",Role:"工程师"})
	if len(args)!=5 { t.Fatalf("got %d args",len(args)) }
	for _,needle:=range []string{"ul.user_id","department_value.value","job_role_value.value"} { if !containsSQL(where,needle){t.Fatalf("missing %s",needle)} }
}
func containsSQL(value,needle string)bool{for i:=0;i+len(needle)<=len(value);i++{if value[i:i+len(needle)]==needle{return true}};return false}
