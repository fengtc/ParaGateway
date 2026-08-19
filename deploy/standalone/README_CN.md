# ParaGateway 独立主机部署

这是以官方 `Wei-Shaw/sub2api` Go 后端为基础、配套 ParaGateway Blazor WASM 管理端的独立部署方案，不依赖 Cloudflare Worker。

## 架构

- `backend`：官方 `Wei-Shaw/sub2api` Go 服务，运行在独立主机上。
- `postgres`、`redis`：官方 Go 服务使用的持久化依赖。
- `frontend`：Blazor WebAssembly 发布后的静态文件，由 Nginx 提供服务。
- `gateway`：Caddy 统一入口，将 Go API/模型网关和前端 SPA 分流。

前端发布后的 `wwwroot` 是静态资源，不需要 ASP.NET Core 运行时，也不依赖 Cloudflare Worker。服务器端不会还原或编译 DevExpress 包。

## 发布前端

DevExpress 包来自开发机或构建机上的合法 DevExpress 安装/许可证源，不能把私有 NuGet 源、许可证文件或凭据放进仓库，也不能指望 Linux 服务器访问 Windows 本地源。

生产发行版必须使用有效的 DevExpress 26.1.3 许可证构建。若构建出现 `DX1000`、`DX1001` 等评估许可证警告，应先在构建机完成正式许可证配置，再生成用于生产环境的静态文件。

在具备 .NET 10 SDK、DevExpress 26.1.3 包和许可证的构建机上执行：

```powershell
$taskTemp = (Resolve-Path .tmp).Path
$env:TEMP = $taskTemp
$env:TMP = $taskTemp
dotnet publish frontend-blazor\ParaGateway.Frontend.csproj `
  -c Release `
  -o .tmp\frontend-publish `
  --no-restore `
  -p:UseAppHost=false `
  --nologo
```

将发布目录中的 `wwwroot` 内容复制到服务器的 `deploy/standalone/data/frontend`：

```bash
mkdir -p deploy/standalone/data/frontend
cp -a /path/to/wwwroot/. deploy/standalone/data/frontend/
```

`index.html` 必须位于 `data/frontend/index.html`。Compose 为前端配置了健康检查；未复制发行版时，前端不会被误认为已就绪。

## 启动

```bash
cd deploy/standalone
cp .env.example .env
# 编辑 .env，至少设置 POSTGRES_PASSWORD 和 JWT_SECRET
docker compose up -d
```

浏览器访问 `http://服务器地址:8080`（可通过 `SERVER_PORT` 修改）。Caddy 会把官方 Go 网关的 `/api`、`/v1`、`/v1beta`、`/backend-api`、`/antigravity`、`/responses`、`/messages`、`/chat`、`/images`、`/videos`、`/realtime`、`/live`、`/alpha` 等路径原样转发，Nginx/Caddy 均关闭 SSE 缓冲并保留 WebSocket Upgrade；其它路径由 Blazor WASM 的 SPA 回退处理。首次启动由 `AUTO_SETUP=true` 自动执行数据库迁移和管理员初始化。

前端发行版不包含支付页面、支付导航或支付 API 调用；后端上游支付代码未删除，以便后续同步官方版本。兑换码、订阅状态、配额和管理员订阅管理仍然保留，它们不等同于在线支付。

## ParaGateway 品牌与首次登录

前端页面、页面标题、favicon 和导航 Logo 使用 ParaGateway 品牌。默认 Compose 配置设置 `PARAGATEWAY_DISABLE_ADMIN_COMPLIANCE=true`，因此首次进入后台不会显示上游的管理员合规确认弹窗，也不会因未写入确认记录而阻断管理员 API。该开关只影响合规确认中间件；设为 `false` 后可恢复上游 API 的强制确认拦截（ParaGateway 前端不再挂载该弹窗，需由自定义客户端处理确认接口）。系统不会代替管理员提交确认记录。

## 登录验证码

前端对齐官方 Go 后端支持的 Cloudflare Turnstile、腾讯天御验证码和阿里云验证码。只有管理员在后端设置中启用验证码后，浏览器才会加载对应厂商的外部 SDK；默认部署不依赖这些脚本。官方后端按单一验证码提供商工作，生产环境只应同时启用其中一种。

本目录的 `nginx.conf` 已为上述验证码 SDK 配置 CSP 域名，且没有加入 Stripe 或 Airwallex 等支付域名。如果在 Nginx/Caddy 之前另设 CDN、WAF 或反向代理并覆盖安全响应头，必须同步保留这些 CSP 来源，否则验证码会被浏览器拦截。真实上线前应使用实际站点密钥逐项验证登录、注册、找回密码、OAuth 创建账号和 Passkey 登录流程。

更新前端时，只需在构建机重新发布并同步 `data/frontend`，然后执行 `docker compose restart frontend gateway`。不需要重新构建 Go 镜像。

## 数据目录

- `data/postgres`：PostgreSQL 数据
- `data/redis`：Redis 持久化数据
- `data/backend`：Go 的 `config.yaml`、安装锁、定价缓存和日志
- `data/caddy`、`data/caddy-config`：Caddy 状态

如果未设置 `ADMIN_PASSWORD`，官方 Go 服务会在首次自动安装时生成一次性管理员密码并写入后端日志；请立即保存。生产环境应设置固定的 `TOTP_ENCRYPTION_KEY`（32 字节、即 64 个十六进制字符），这样 TOTP、备份凭据和其它加密字段在容器重建后仍可解密。上游账号的 WIF 身份联合会用该密钥加密 Client Secret；未显式配置固定密钥时，后端会拒绝创建或切换到 WIF，避免把无法跨重启解密的密文写入数据库。

请先阅读上游 README 的环境变量和安全要求，再在生产环境配置 HTTPS、反向代理信任范围和强随机密钥。

## 本机烟测结果

在 Windows Docker Desktop（Linux/ARM64）上已验证 `docker compose config --quiet`、`docker compose up -d --build`，并确认 PostgreSQL、Redis、Go 后端、Nginx 前端和 Caddy 网关均处于运行状态。`http://127.0.0.1:8080/` 返回 Blazor 首页，`/health` 返回 `{"status":"ok"}`，`/setup/status` 返回 `needs_setup=false`；管理员密码登录和 `auth/me` 也已通过。测试不会调用真实模型上游。
