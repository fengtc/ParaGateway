# ParaGateway

本目录是官方 `Wei-Shaw/sub2api` 的独立主机版本：后端保持官方 Go 实现，前端把官方 Vue/Vite 管理界面替换为 `.NET 10 Blazor WebAssembly + DevExpress Blazor`。部署不依赖 Cloudflare Worker。

## 为什么使用 Blazor WebAssembly

Vue/Vite 的生产构建结果是静态 HTML、JavaScript、CSS 和字体/图片资源；它不要求 Node.js 在生产服务器上运行。Blazor WebAssembly 的发布结果同样是静态 `wwwroot`，因此可以由 Nginx、Caddy 或对象存储托管。

本项目已经使用 WASM，原因是：

- 与用户指定的 .NET/DevExpress 技术栈一致，管理表格使用 `DxGrid` 等 DevExpress 组件。
- Go API、SSE、WebSocket 和模型网关仍由官方 Go 服务直接负责，前端没有复制业务逻辑。
- 独立主机只需要 Nginx/Caddy + Go + PostgreSQL + Redis，不需要 ASP.NET Core 运行时。
- 相比 Blazor Server，不会为每个管理页面保持 SignalR 电路，也不需要额外的 BFF 才能把静态前端和 Go 服务放在同一入口。

Blazor Server 仍适合必须把密钥和业务逻辑留在服务器端的内部系统；本项目的 API 密钥、OAuth 凭据和数据库访问都留在官方 Go 后端，WASM 只调用 API，所以 WASM 更符合当前边界。

## 目录

- `backend`：官方 Go 后端的完整副本。
- `reference_sub2api`：上游源码参考副本，包含官方 Vue 前端。
- `frontend-blazor`：Blazor WASM 管理端和契约测试。
- `deploy/standalone`：PostgreSQL、Redis、Go、Nginx、Caddy 的独立部署编排。
- `deploy/standalone/data/frontend`：最新 Release 的完整发布目录；Nginx 挂载其中的 `wwwroot`。

`backend` 以 `reference_sub2api/backend` 的官方 Go 实现为基础；ParaGateway 在代码中永久关闭首次进入后台的强制确认拦截，同时保留无害的兼容状态接口，其他 API、数据库和网关协议保持兼容。

## 功能边界

已对齐官方非支付功能：认证、注册、邮箱验证、密码找回、OAuth/Passkey、个人资料与安全、API Keys、用量、兑换码、订阅状态、邀请返利、模型广场、可用渠道、批量生图、渠道状态，以及管理员的用户、分组、渠道、上游账号/OAuth、模型同步、代理、公告、审计、运维监控、风控、Prompt Audit、TLS 指纹、错误透传、备份、数据管理、系统设置和系统操作。

按要求没有在 Blazor 前端暴露在线支付流程：没有购买、订单、支付二维码、Stripe 或 Airwallex 路由和 API 调用。兑换码、订阅状态、配额和管理员订阅管理仍保留，它们不是在线支付页面。官方 Go 后端的支付源代码未删除，以便后续同步上游版本；部署时不会产生支付前端入口。

首次安装向导也已保留：`/setup` 对齐官方的 `/setup/status`、`/setup/test-db`、`/setup/test-redis` 和 `/setup/install` 契约。Compose 默认使用 `AUTO_SETUP=true`，也可以在手工部署时使用该向导。

## DevExpress 版本

用户描述的 `6.1.3` 不是当前可解析的 Blazor 包版本；本机可用且已验证的是 `DevExpress.Blazor` 和 `DevExpress.Blazor.Themes` `26.1.3`。生产构建必须使用合法 DevExpress 许可证，不能把许可证文件、私有 NuGet 源或凭据提交到仓库。

## v0.1.179 选择性升级

当前后端版本为 `0.1.179`。本次在保留 ParaGateway 合规、OAuth、日志、迁移和 Blazor 定制的前提下，选择性回移了官方 v0.1.179 的 adaptive 国产模型协议、Composite 路由扩展、渠道计费倍率、代理探测、Responses input-token 预检、Responses/WebSocket/failover 修复及相关协议兼容改动。

官方迁移编号 226-228 与 ParaGateway 现有迁移冲突，因此保持官方 SQL 内容不变并重编号：

- 官方 `226_add_usage_log_effective_model_indexes_notx.sql` -> 本地 `229_add_usage_log_effective_model_indexes_notx.sql`
- 官方 `227_composite_routes_add_cn_providers.sql` -> 本地 `230_composite_routes_add_cn_providers.sql`
- 官方 `228_channel_pricing_multipliers.sql` -> 本地 `231_channel_pricing_multipliers.sql`

不要再把官方 226-228 原编号文件复制到本仓库，否则会与已登记迁移发生编号或 checksum 冲突。

v0.1.179 将 OpenAI 长上下文计费门控从“分组 AND 账号”改为“分组 OR 账号”。生产升级前必须同时审计分组和账号的 `long_context_pricing_enabled`；任一侧启用后，超过 272k 上下文的请求可能按输入 2 倍、输出 1.5 倍计费。

## 验证与发布

在 Windows 构建机上把临时目录指向仓库内可写目录后执行：

```powershell
$taskTemp = (Resolve-Path .tmp).Path
$env:TEMP = $taskTemp
$env:TMP = $taskTemp
dotnet test frontend-blazor\Tests\ParaGateway.Frontend.Tests.csproj --no-restore --nologo
$commit = (git rev-parse HEAD).Trim()
.\deploy\production\build-frontend-archive.ps1 -Commit $commit
```

归档脚本会从当前进程的 `DevExpress_License`、`DEVEXPRESS_LICENSE_FILE` 或
`%APPDATA%\DevExpress\DevExpress_License.txt` 读取许可证，只注入当前构建进程；
它要求 Git 工作区干净，会先清理前端 Release 缓存，并在发现 `DX1000/DX1001/DX1002/DX1003`
或缺少唯一 DevExpress 许可证属性时停止，
不会把许可证文件写入前端归档。不要绕过该脚本直接生成生产前端归档。

将完整发布目录同步到 `deploy/standalone/data/frontend`，确认静态入口位于
`data/frontend/wwwroot/index.html` 后，在服务器执行：

```bash
cd deploy/standalone
cp .env.example .env
# 编辑 .env，设置强随机 POSTGRES_PASSWORD、JWT_SECRET，按需设置管理员和加密密钥
docker compose up -d
```

默认入口是 `http://服务器地址:8080`。Caddy 将 `/api`、`/v1`、`/v1beta`、模型网关、SSE、WebSocket 和 setup API 转发到 Go 服务，其余路径交给 Nginx 的 Blazor SPA 回退。正式环境应在 Caddy 配置 HTTPS，并限制反向代理信任范围。

本次 v0.1.179 本机验证结果：

- `dotnet test`：295 通过、0 失败、0 跳过。
- `go test ./... -count=1`：Go 全仓全部包通过。
- Blazor Release 发布成功；仅有既有 DevExpress WASM P/Invoke 裁剪警告。
- PostgreSQL 17 隔离集成验证通过：全量迁移、重复执行、两个实例并发迁移锁、229 无效并发索引恢复，以及 229-231 的迁移记录、有效索引、Composite 约束、六个倍率列和六个正数约束均已核实。
- 后端 Dockerfile 已改为 Go builder + Alpine runtime 多阶段构建，生产镜像不再包含 Go 工具链、模块缓存和源码。
- `linux/amd64` 最终候选 `paragateway-backend:v0.1.179-amd64` 为 116,867,727 字节，镜像 ID `sha256:017af76e6a5241b3de8fb902ee59b37f09dcfe470ce55b82b2c65717ee960510`；容器内 `./main --version` 返回 `ParaGateway 0.1.179`。
- Browser 插件不可用，改用本机 Playwright 1.60.0 + Chrome 验证登录、账号管理、Kimi/智谱/DeepSeek adaptive 地址、账号模式切换、分组长上下文开关和 390x844 移动视口；目标页面控制台错误、页面异常和请求失败均为 0。

真实模型上游、OAuth、SSE 和 WebSocket 仍需使用生产凭据做部署后冒烟，凭据不得写入仓库、聊天或前端发布目录。首次构建测试若遇到 Windows `obj`/临时目录权限错误，应将 `TEMP`、`TMP` 和项目输出目录指向可写路径。
