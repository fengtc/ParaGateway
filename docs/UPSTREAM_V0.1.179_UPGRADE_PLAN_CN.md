# Sub2API v0.1.179 升级执行记录

更新时间：2026-08-21（Asia/Shanghai）

## 当前结论

本次已经建立 `upgrade/v0.1.179` 分支，并在隔离目录获取了官方 v0.1.179 源码。当前工作区的未提交修改均保留。

暂不直接替换生产后端或执行数据库迁移。ParaGateway 的后端不是干净的官方 v0.1.178 快照，官方 v0.1.178 -> v0.1.179 的后端补丁在当前代码上产生约 130 个真实文件冲突；自动覆盖会丢失 ParaGateway 的合规、OAuth、日志、迁移执行器和其他定制。

## 版本基线

| 项目 | 值 |
| --- | --- |
| 官方仓库 | `Wei-Shaw/sub2api` |
| 目标 tag | `v0.1.179` |
| 目标 commit | `75f88be5f75c27771836b586f7de1503afa0e3bc` |
| 上一官方 tag commit | `e0c48a19ed794a565e3858662520afe0a1f9f0ba` |
| 官方发布时间 | `2026-08-20 15:06:32`（北京时间） |
| ParaGateway 原分支 | `integration/v0.1.178` |
| 当前升级分支 | `upgrade/v0.1.179` |
| 当前基线提交 | `ad4b2299086b475a3a47d7a2d08e09dd96d02d25` |

官方发布页：<https://github.com/Wei-Shaw/sub2api/releases/tag/v0.1.179>

## 隔离资料

- 官方 v0.1.179：仓库外的隔离升级工作区
- 官方 v0.1.178 基线：仓库外的隔离基线工作区
- ParaGateway 升级候选快照：仓库外的隔离候选工作区
- 官方差异补丁：仓库外保存，仅用于审阅

上述资料不参与生产部署，也没有覆盖当前主工作区。

## 数据库迁移映射

官方 v0.1.179 新增的三个迁移不能沿用原编号，因为 ParaGateway 已经使用了相同前缀：

| 官方文件 | ParaGateway 现有冲突 | 计划新文件名 | 执行方式 |
| --- | --- | --- | --- |
| `226_add_usage_log_effective_model_indexes_notx.sql` | `226_channel_monitor_quota_mode.sql` | `229_add_usage_log_effective_model_indexes_notx.sql` | 非事务，并发索引 |
| `227_composite_routes_add_cn_providers.sql` | `227_seed_department_user_attribute.sql` | `230_composite_routes_add_cn_providers.sql` | 事务 |
| `228_channel_pricing_multipliers.sql` | `228_normalize_paragateway_branding.sql` | `231_channel_pricing_multipliers.sql` | 事务 |

目前只记录映射，没有把 SQL 直接加入当前源码。原因是后端功能代码尚未完成三方合并，提前执行 schema 变更会造成“数据库已升级、应用未支持”的不一致。

正式移植时必须同时完成：

1. 保持官方 SQL 内容不变，只改文件前缀和必要的迁移测试名称。
2. 更新迁移 checksum 兼容测试，不修改已经在任何环境执行过的旧迁移。
3. 在独立 PostgreSQL 数据库完整执行当前迁移链，确认 `schema_migrations` 文件名和 checksum。
4. 单独验证 `229_*_notx.sql` 的 `CREATE INDEX CONCURRENTLY`，不能放入事务。

## 计费护栏

v0.1.179 将长上下文计费门控从“分组开关 AND 账号开关”改为“任一开关启用”。升级前必须导出并确认所有分组的 `long_context_pricing_enabled`，尤其是可能超过 272k 上下文的 OpenAI 账号。

如果需要保持旧账单口径，升级后的相关分组必须显式设置为 `false`。升级验证要对同一请求比较：输入 token、输出 token、长上下文倍率、`total_cost`、`actual_cost` 和用量日志。

## 功能回归矩阵

后端合并后必须逐项验证：

- Chat Completions 流式和非流式故障转移；
- Anthropic Fast mode（`speed: "fast"`）计费；
- Responses、Responses WebSocket 多轮会话和 429 切换；
- 客户端工具映射、历史 item id、`tool_search` 输出；
- DeepSeek thinking 和 encrypted-only reasoning；
- Grok 图片、view_image、xhigh 推理档位；
- Kimi、智谱 GLM、DeepSeek 的 `adaptive` 协议和请求头覆写；
- Composite 的 Codex、Kimi、智谱、DeepSeek 路由；
- Fast/Flex 及上下文区间倍率；
- `/v1/responses/input_tokens` 预检；
- 渠道监控配额抓取、状态码判定和非法模式组合校验；
- 管理端用户、账号、分组、渠道、用量统计以及 ParaGateway Blazor API 契约。

每个用例需要记录实际路由、HTTP 状态、故障转移、token、费用和用量日志，不能只以页面能打开作为通过标准。

## 本地验证状态

- [x] 官方 v0.1.179 tag 已下载并记录 commit。
- [x] 当前工作区已建立独立升级分支。
- [x] 官方迁移与当前迁移编号冲突已确认。
- [x] 官方后端差异已在隔离副本中试合并，冲突文件已记录。
- [ ] ParaGateway 后端三方合并。
- [ ] 迁移重编号后的 PostgreSQL 升级演练。
- [ ] Go 单元/集成测试。
- [ ] Docker 镜像构建和启动验证。
- [ ] Blazor 前端与新 API 字段回归。
- [ ] 灰度部署、账单抽样和回滚演练。

当前环境没有 Go CLI，也没有运行中的 Docker Desktop，因此不能把后端测试或数据库迁移标记为通过。前端 .NET 验证可以在临时目录指向仓库内可写目录后执行：

```powershell
$taskTemp = (Resolve-Path .tmp).Path
$env:TEMP = $taskTemp
$env:TMP = $taskTemp
dotnet test frontend-blazor\Tests\ParaGateway.Frontend.Tests.csproj --no-restore --nologo
```

## 回滚边界

在后端合并、迁移演练和账单抽样全部通过前，不应上传新镜像或替换生产静态资源。生产切换前需同时保留：

- 当前后端镜像和 `wwwroot` 归档；
- PostgreSQL 全量备份；
- Redis 快照；
- 当前 `schema_migrations` 内容；
- 新旧版本的部署目录和配置快照。

数据库迁移是前向变更；如果新代码回滚，必须先确认新字段和索引对旧版本兼容，不能只回滚容器。
