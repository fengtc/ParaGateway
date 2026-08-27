package service

import (
	"context"
	"testing"
	"time"
)
type workDistributionStub struct{ rows []WorkDistributionAggregate }
func(s workDistributionStub)GetAggregates(context.Context,WorkDistributionFilter)([]WorkDistributionAggregate,error){return s.rows,nil}
func TestWorkDistributionSummaryAggregatesSamplesAndConfidence(t *testing.T){
	now:=time.Now();svc:=NewWorkDistributionService(workDistributionStub{rows:[]WorkDistributionAggregate{{UserID:1,Email:"dev@example.com",Department:"研发",Role:"研发",WorkRelated:"work",Category:"coding",Requests:3,TotalTokens:300,ConfidenceSum:2.4,ConfidenceSampleCount:3}}})
	result,err:=svc.GetSummary(context.Background(),WorkDistributionSummaryFilter{WorkDistributionFilter:WorkDistributionFilter{StartTime:now.Add(-time.Hour),EndTime:now},Metric:"requests"});if err!=nil{t.Fatal(err)}
	if result.Coverage.TotalRequests!=3||len(result.Users)!=1||result.AverageConfidence==nil{t.Fatalf("unexpected result: %+v",result)}
}
