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
- `deploy/standalone/data/frontend`：最新 Release 发布的静态前端。

`backend` 以 `reference_sub2api/backend` 的官方 Go 实现为基础；ParaGateway 仅增加了可选的 `PARAGATEWAY_DISABLE_ADMIN_COMPLIANCE` 部署开关，用于在独立主机部署中关闭首次进入后台的强制确认拦截，其他 API、数据库和网关协议保持兼容。

## 功能边界

已对齐官方非支付功能：认证、注册、邮箱验证、密码找回、OAuth/Passkey、个人资料与安全、API Keys、用量、兑换码、订阅状态、邀请返利、模型广场、可用渠道、批量生图、渠道状态，以及管理员的用户、分组、渠道、上游账号/OAuth、模型同步、代理、公告、审计、运维监控、风控、Prompt Audit、TLS 指纹、错误透传、备份、数据管理、系统设置和系统操作。

按要求没有在 Blazor 前端暴露在线支付流程：没有购买、订单、支付二维码、Stripe 或 Airwallex 路由和 API 调用。兑换码、订阅状态、配额和管理员订阅管理仍保留，它们不是在线支付页面。官方 Go 后端的支付源代码未删除，以便后续同步上游版本；部署时不会产生支付前端入口。

首次安装向导也已保留：`/setup` 对齐官方的 `/setup/status`、`/setup/test-db`、`/setup/test-redis` 和 `/setup/install` 契约。Compose 默认使用 `AUTO_SETUP=true`，也可以在手工部署时使用该向导。

## DevExpress 版本

用户描述的 `6.1.3` 不是当前可解析的 Blazor 包版本；本机可用且已验证的是 `DevExpress.Blazor` 和 `DevExpress.Blazor.Themes` `26.1.3`。生产构建必须使用合法 DevExpress 许可证，不能把许可证文件、私有 NuGet 源或凭据提交到仓库。

## 验证与发布

在 Windows 构建机上把临时目录指向仓库内可写目录后执行：

```powershell
$taskTemp = (Resolve-Path .tmp).Path
$env:TEMP = $taskTemp
$env:TMP = $taskTemp
dotnet test frontend-blazor\Tests\ParaGateway.Frontend.Tests.csproj --no-restore --nologo
dotnet publish frontend-blazor\ParaGateway.Frontend.csproj -c Release -o .tmp\frontend-publish-final --no-restore -p:UseAppHost=false --nologo
```

将发布目录的 `wwwroot` 同步到 `deploy/standalone/data/frontend` 后，在服务器执行：

```bash
cd deploy/standalone
cp .env.example .env
# 编辑 .env，设置强随机 POSTGRES_PASSWORD、JWT_SECRET，按需设置管理员和加密密钥
docker compose up -d
```

默认入口是 `http://服务器地址:8080`。Caddy 将 `/api`、`/v1`、`/v1beta`、模型网关、SSE、WebSocket 和 setup API 转发到 Go 服务，其余路径交给 Nginx 的 Blazor SPA 回退。正式环境应在 Caddy 配置 HTTPS，并限制反向代理信任范围。

本次本机验证结果：前端 Release 测试程序集通过 69 项契约测试；Docker Compose 已成功重建并启动 PostgreSQL、Redis、Go、Nginx、Caddy 五个容器；后端 `internal/server/middleware` 测试包通过，入口首页、`/health`、`/setup/status`、管理员登录和管理员统计 API 均已实测通过。当前环境没有宿主机 Go CLI，因此 Go 测试在后端 Docker 容器中执行；真实模型上游、OAuth、SSE 和 WebSocket 仍需在配置上游凭据后单独联调。首次构建测试若遇到 Windows `obj`/临时目录权限错误，应将 `TEMP`、`TMP` 和项目输出目录指向可写路径。
