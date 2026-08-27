package workclassifier

import (
	"sort"
	"strings"
	"unicode"
	"unicode/utf8"
)

type keywordRule struct {
	category Category
	term     string
	weight   float64
}

var textRules = []keywordRule{
	{CategoryCoding, "compile error", 2}, {CategoryCoding, "unit test", 2},
	{CategoryCoding, "pull request", 2}, {CategoryCoding, "merge request", 2},
	{CategoryCoding, "source code", 2}, {CategoryCoding, "stack trace", 2},
	{CategoryCoding, "refactor", 1.5}, {CategoryCoding, "debug", 1.5},
	{CategoryCoding, "function", 1}, {CategoryCoding, "class", 1},
	{CategoryCoding, "api", 1}, {CategoryCoding, "git", 1},
	{CategoryCoding, "代码", 2}, {CategoryCoding, "编译", 2},
	{CategoryCoding, "单元测试", 2}, {CategoryCoding, "调试", 1.5},
	{CategoryCoding, "重构", 1.5}, {CategoryCoding, "接口", 1},

	{CategoryDocumentation, "write documentation", 2}, {CategoryDocumentation, "technical document", 2},
	{CategoryDocumentation, "user manual", 2}, {CategoryDocumentation, "release notes", 2},
	{CategoryDocumentation, "markdown", 1.5}, {CategoryDocumentation, "document", 1},
	{CategoryDocumentation, "文档", 2}, {CategoryDocumentation, "使用手册", 2},
	{CategoryDocumentation, "需求说明", 2}, {CategoryDocumentation, "技术方案", 2},
	{CategoryDocumentation, "撰写", 1},

	{CategoryAnalysis, "data analysis", 2}, {CategoryAnalysis, "statistical analysis", 2},
	{CategoryAnalysis, "sql query", 2}, {CategoryAnalysis, "pivot table", 2},
	{CategoryAnalysis, "dashboard", 1.5}, {CategoryAnalysis, "dataset", 1.5},
	{CategoryAnalysis, "分析", 2}, {CategoryAnalysis, "统计", 1.5},
	{CategoryAnalysis, "数据", 1}, {CategoryAnalysis, "报表", 1.5},

	{CategoryOperations, "production deployment", 2}, {CategoryOperations, "incident response", 2},
	{CategoryOperations, "kubernetes", 2}, {CategoryOperations, "systemd", 2},
	{CategoryOperations, "docker", 1.5}, {CategoryOperations, "monitoring", 1.5},
	{CategoryOperations, "运维", 2}, {CategoryOperations, "部署", 2},
	{CategoryOperations, "生产环境", 2}, {CategoryOperations, "故障处理", 2},
	{CategoryOperations, "监控", 1.5},

	{CategoryCommunication, "meeting minutes", 2}, {CategoryCommunication, "email reply", 2},
	{CategoryCommunication, "status update", 1.5}, {CategoryCommunication, "沟通", 2},
	{CategoryCommunication, "会议纪要", 2}, {CategoryCommunication, "邮件回复", 2},
	{CategoryCommunication, "工作汇报", 1.5},

	{CategoryLearning, "training material", 2}, {CategoryLearning, "learning plan", 2},
	{CategoryLearning, "study", 1.5}, {CategoryLearning, "tutorial", 1.5},
	{CategoryLearning, "学习", 2}, {CategoryLearning, "培训", 2},
	{CategoryLearning, "教程", 1.5}, {CategoryLearning, "调研", 1.5},

	{CategoryNonWork, "personal entertainment", 2}, {CategoryNonWork, "shopping list", 2},
	{CategoryNonWork, "movie recommendation", 2}, {CategoryNonWork, "travel itinerary", 2},
	{CategoryNonWork, "私人娱乐", 2}, {CategoryNonWork, "购物清单", 2},
	{CategoryNonWork, "电影推荐", 2}, {CategoryNonWork, "个人旅行", 2},
}

func classifyText(text string) (Category, float64, bool) {
	normalized := normalizeTransientText(text)
	if normalized == "" {
		return CategoryUnclassified, 0, false
	}
	scores := make(map[Category]float64)
	for _, rule := range textRules {
		if containsTerm(normalized, rule.term) {
			scores[rule.category] += rule.weight
		}
	}
	category, score, margin := bestScore(scores)
	minimumScore := 2.0
	if category == CategoryNonWork {
		minimumScore = 3.5
	}
	if score < minimumScore || margin < 0.75 {
		return CategoryUnclassified, 0, false
	}
	confidence := 0.62 + clamp(score-2, 0, 4)*0.05 + clamp(margin-0.75, 0, 2)*0.03
	return category, clamp(confidence, 0.62, 0.88), true
}

func normalizeTransientText(value string) string {
	if value == "" || !utf8.ValidString(value) {
		return ""
	}
	runes := []rune(value)
	if len(runes) > 16*1024 {
		value = string(runes[:16*1024])
	}
	return normalizeForMatching(value)
}

func bestScore(scores map[Category]float64) (Category, float64, float64) {
	type scoredCategory struct {
		category Category
		score    float64
	}
	ranked := make([]scoredCategory, 0, len(scores))
	for category, score := range scores {
		ranked = append(ranked, scoredCategory{category: category, score: score})
	}
	sort.Slice(ranked, func(i, j int) bool {
		if ranked[i].score == ranked[j].score {
			return ranked[i].category < ranked[j].category
		}
		return ranked[i].score > ranked[j].score
	})
	if len(ranked) == 0 {
		return CategoryUnclassified, 0, 0
	}
	margin := ranked[0].score
	if len(ranked) > 1 {
		margin -= ranked[1].score
	}
	return ranked[0].category, ranked[0].score, margin
}

func normalizeForMatching(value string) string {
	value = strings.ToLower(value)
	value = strings.Map(func(r rune) rune {
		if unicode.IsLetter(r) || unicode.IsDigit(r) {
			return r
		}
		return ' '
	}, value)
	return strings.Join(strings.Fields(value), " ")
}

func containsTerm(normalized, term string) bool {
	term = normalizeForMatching(term)
	if term == "" {
		return false
	}
	if containsHan(term) {
		return strings.Contains(normalized, term)
	}
	return strings.Contains(" "+normalized+" ", " "+term+" ")
}

func containsHan(value string) bool {
	for _, r := range value {
		if unicode.In(r, unicode.Han) {
			return true
		}
	}
	return false
}
