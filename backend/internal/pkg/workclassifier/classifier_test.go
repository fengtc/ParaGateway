package workclassifier

import "testing"

func TestClassifyTransientText(t *testing.T) {
	tests := []struct{ text string; category Category }{
		{"请修复这个 API 的单元测试和编译错误", CategoryCoding},
		{"撰写项目技术文档和使用手册", CategoryDocumentation},
		{"分析数据报表并执行 SQL 查询", CategoryAnalysis},
		{"部署生产环境并检查 systemd 日志", CategoryOperations},
	}
	for _, test := range tests {
		result := Classify(Input{TransientText: test.text})
		if result.Category != test.category { t.Fatalf("%q: got %s, want %s", test.text, result.Category, test.category) }
		if result.ClassificationSource != SourceLocalRule { t.Fatalf("unexpected source %s", result.ClassificationSource) }
	}
}

func TestClassifyUnknownTextAsUnclassified(t *testing.T) {
	result := Classify(Input{TransientText: "hello"})
	if result.Category != CategoryUnclassified || result.WorkRelated != WorkRelationUncertain { t.Fatalf("unexpected result: %+v", result) }
}
