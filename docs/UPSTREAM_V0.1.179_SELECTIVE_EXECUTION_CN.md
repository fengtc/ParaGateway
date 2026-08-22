# Sub2API v0.1.179 选择性回移执行记录

更新时间：2026-08-21（Asia/Shanghai）

## 本次结果

完整选择性候选已写回主工作区，后端版本为 `0.1.179`。写回范围严格限定为候选中有差异的 `backend` 和 `frontend-blazor` 源码，共 165 个文件，其中新增 23 个、更新 142 个；没有复制 `.git`、`.env`、数据库数据、构建缓存、截图或临时目录，也没有删除主工作区独有文件。

写回前的 142 个原文件及逐文件 SHA-256 清单保存在：

仓库外的带时间戳备份目录。

隔离候选仍保存在：

仓库外的隔离候选目录。

## 已回移内容

后端在保留 ParaGateway 合规、OAuth、日志、迁移和运维定制的前提下，回移了官方 v0.1.179 的以下能力和修复：

- Kimi、智谱和 DeepSeek 的 adaptive Chat Completions / Anthropic / Responses 协议支持。
- Composite 路由对 Codex 和国产 provider 的扩展。
- fast、flex、input、output、cache write、cache read 渠道计费倍率。
- 可配置代理探测 URL 与校验。
- Responses input-token 预检、Responses WebSocket/failover、Chat bridge、工具映射、reasoning cache 和 Grok 工具/图片相关修复。
- 使用 `GROUPING SETS` 的用量聚合和 effective model 索引。

Blazor 管理端已同步：

- 国产 provider 创建弹窗的 adaptive 协议、默认 Base URL、账号模式切换和自定义 URL 保留。
- `api_base_urls` 与兼容 `base_url` 的请求序列化。
- 分组 `long_context_pricing_enabled` 编辑和 payload。
- 渠道六类计费倍率 JSON 的正数校验与完整透传。

## 迁移映射

官方 226-228 与 ParaGateway 已存在的本地迁移编号冲突，因此官方 SQL 内容保持不变并重编号：

- 官方 `226_add_usage_log_effective_model_indexes_notx.sql` -> 本地 `229_add_usage_log_effective_model_indexes_notx.sql`
- 官方 `227_composite_routes_add_cn_providers.sql` -> 本地 `230_composite_routes_add_cn_providers.sql`
- 官方 `228_channel_pricing_multipliers.sql` -> 本地 `231_channel_pricing_multipliers.sql`

迁移文件 SHA-256：

| 文件 | SHA-256 |
| --- | --- |
| `229_add_usage_log_effective_model_indexes_notx.sql` | `62A2480E55A6EE9499699F8052F859A4CA7C33C0CEA4B9340679B78F99D4B9F0` |
| `230_composite_routes_add_cn_providers.sql` | `21D81A064828E8A544992F98E949053E45E9A135BC011F129147675D94612DDF` |
| `231_channel_pricing_multipliers.sql` | `CC7D231CC28660C791F924B32C35D8CFCE2A474842A0A329DDD61EA7AE2DE3A9` |

不要把官方原编号 226-228 再复制到本仓库，否则可能触发迁移编号或 checksum 冲突。

## 验证证据

- `git diff --check`：通过。
- `dotnet test frontend-blazor/Tests/ParaGateway.Frontend.Tests.csproj --no-restore --nologo`：295 通过、0 失败、0 跳过。
- `go test ./... -count=1`：Go 全仓全部包通过。
- Blazor Release 发布：成功；仅有既有 DevExpress WASM P/Invoke 裁剪警告。
- PostgreSQL 17 隔离集成：全量迁移、重复 `ApplyMigrations`、两个实例并发迁移锁均通过。
- 迁移 229：两个 effective model 表达式索引均存在且 `indisvalid=true`，无效并发索引清理和重试测试通过。
- 迁移 230：Composite constraint 已包含 `kimi`、`zhipu`、`deepseek`。
- 迁移 231：六个 `NUMERIC(12,6)` 倍率列和六个 `> 0` 约束均已核实。
- 后端 Dockerfile 已改为 Go builder + Alpine runtime 多阶段构建，运行镜像不再携带 Go 工具链、模块缓存和源码。
- 最终 `linux/amd64` 候选 `paragateway-backend:v0.1.179-amd64`：镜像 ID `sha256:017af76e6a5241b3de8fb902ee59b37f09dcfe470ce55b82b2c65717ee960510`，大小 116,867,727 字节；容器内 `./main --version` 返回 `ParaGateway 0.1.179`。
- 最终镜像内 `/app/main` SHA-256 为 `817975ee24815314df16ec559e663cf7a8542ba5ad8cdaaf4025ed4582d219f0`，与已通过全仓测试的原单阶段镜像完全一致；原镜像仍以 `paragateway-backend:v0.1.179-amd64-single-stage` 保留在本机。
- 瘦身镜像的独立临时 PostgreSQL/Redis 栈已验证首次全量迁移、健康检查、229-231 对象和标准化 checksum，以及重启后的幂等检查；临时容器、网络和 tmpfs 数据均已删除。
- Docker Hub 元数据请求当前返回 `EOF`，因此标准多阶段 Dockerfile 的联网重建尚未完成；本次交付镜像由已验证的同一 amd64 二进制和最小运行文件离线组装并完成上述运行验证。
- 发布前端保存在仓库外的发布目录，入口 `wwwroot/index.html` 已核实。
- 不含实际 `.env`、数据库或 Redis 数据的生产交付包保存在仓库外；后端归档 SHA-256 `34f71aebf24229b77575c81fd5798244f2e48f110ad417fb35b6e6e09dccecfe`，前端归档 SHA-256 `d06bb8ef0d3e97bb89aaad170f029ef27db5c990ad4e34bd52e6e6a78be06914`。
- Browser 插件不可用，使用 Playwright 1.60.0 + Chrome 验证登录、`/admin/accounts`、Kimi/智谱/DeepSeek adaptive 地址、账号模式切换后的自定义地址保留、`/admin/groups` 长上下文开关，以及 1440x1000 和 390x844 视口。目标页面控制台错误、页面异常、请求失败和移动端横向溢出均为 0。

以上是本机隔离和构建证据，不等于生产部署成功。

## 长上下文计费门禁

v0.1.179 将 OpenAI 长上下文计费从“分组 AND 账号”改为“分组 OR 账号”。生产迁移前必须查询实际分组和账号开关；任一侧启用后，超过 272k 上下文的请求可能按输入 2 倍、输出 1.5 倍计费。未经生产数据审计，不应启动新版本后端。

## 生产部署状态

截至 2026-08-21，本轮没有对生产数据库、Redis、镜像、Compose、配置或静态资源做任何修改，也没有执行生产迁移。原因不是源码或迁移测试失败，而是当前没有可信且可用的生产控制通道：

- 当时配置的远程 Docker context 无法访问，Docker API 返回 `502 Bad Gateway`。
- 同一远程主机的应用入口和 `/health` 均返回 `502`，HTTPS 握手失败，无法核对在线版本。
- 远程主机的 SSH 连接超时。
- 其他已配置 SSH 主机要么无可用公钥，要么出现主机指纹变化；未绕过 StrictHostKeyChecking，也未修改 `known_hosts`。

恢复 VPN/SSH 或 Docker API 后，生产步骤必须按以下顺序执行：

1. 确认实际主机和 `/opt/paragateway` 目录，并核对当前 Compose、运行镜像和应用版本。
2. 执行 PostgreSQL `pg_dump`、Redis 持久化快照、Compose/配置/前后端静态资源和当前镜像备份，记录可恢复路径。
3. 只读审计分组和账号的长上下文计费开关，确认 OR 语义的影响范围。
4. 上传已验证源码/镜像和完整 Blazor Release 目录，保留旧镜像与旧静态目录。
5. 由单个新后端实例启动并执行 229-231，核实 `schema_migrations` 文件名和 checksum；不要手工复制官方 226-228。
6. 验证 `/health`、版本 `0.1.179`、登录/OAuth、Responses、WebSocket、Composite、计费日志和迁移对象。
7. 任一门禁失败时停止滚动部署，恢复旧镜像/静态目录；数据库按已验证的前向兼容策略处理，不在缺少备份时做破坏性回退。
