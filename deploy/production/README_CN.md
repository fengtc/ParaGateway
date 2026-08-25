# ParaGateway 生产候选发布

本目录用于服务器候选端口发布和生产晋级。代码仍在本机修改、测试、提交并推送 GitHub；服务器只拉取指定提交。

- 生产后端：`127.0.0.1:8184`
- 候选后端：`127.0.0.1:8284`
- 生产网关：`127.0.0.1:8182`
- 候选网关：`127.0.0.1:8282`
- 源码镜像：`/opt/paragateway/source`
- 发布目录：`/opt/paragateway/releases/<release>`
- 配置目录：`/etc/paragateway/<release>`

候选实例必须使用隔离的、可写的临时数据库快照。后端启动可能执行迁移、bootstrap 和 seed，因此只读副本不能作为通用候选数据库；“候选不执行充值、扣款等业务写操作”是验证流程约束，不能替代数据库隔离。`deploy-candidate.sh` 要求候选与生产的 PostgreSQL 主机和库名组合不同、候选 Redis 使用独立 DB（默认 `15`），并为每个候选实例创建独立的 `/var/lib/paragateway-backend-<release>` 数据目录。发布配置分别保存为 `candidate.env` 和 `production.env`；候选 unit 只读取前者，晋级生成的生产 unit 只读取后者。

后端启动时会自动检查并应用迁移。标准候选流程要求设置当前生产后端的完整 `PRODUCTION_COMMIT`，并拒绝 `backend/migrations` 与生产提交存在任何差异的目标提交，因此本流程不会改变生产 schema。包含迁移的版本必须使用单独审批的数据库备份、迁移和回滚流程；unit 快照不能回滚数据库 schema。

候选和生产的内层网关只信任来自 `127.0.0.1/32` 的上一层代理，后端只信任回环网关。仓库中的 Caddy 校验仅检查候选发布目录自己的配置，不读取、修改或重载公网外层 Caddy。公网第一层代理必须覆盖客户端传入的 `X-Forwarded-For`，不能透传未经校验的伪造值。

候选验证通过、晋级生产后，在“系统设置 -> 安全高级设置”中关闭“信任代理转发 IP”，使 `server.trusted_proxies` 成为唯一可信来源。变更后从公网发起一条受控请求，确认审计日志和用量记录显示真实公网 IP；异常时先恢复该开关，再执行版本回滚。

先在 Windows 构建机生成带许可证的前端归档。脚本要求 Git 工作区干净，会清理 Release 缓存，拒绝包含 DevExpress 评估/过期警告或缺少许可证属性的构建，并且不会把许可证文件放入归档：

```powershell
$commit = (git rev-parse HEAD).Trim()
.\deploy\production\build-frontend-archive.ps1 -Commit $commit
```

```bash
export CANDIDATE_ENV_FILE=/etc/paragateway/candidate.env
export PRODUCTION_COMMIT=<当前生产后端完整提交号>
export PRODUCTION_ENV_FILE=/etc/paragateway/<production-release>/production.env
export PRODUCTION_CONFIG_FILE=/etc/paragateway/<production-release>/config.yaml
export FRONTEND_ARCHIVE=/var/tmp/paragateway-frontend-<commit>.tar.gz
export FRONTEND_ARCHIVE_SHA256=<build-frontend-archive.ps1 输出的 sha256>
./deploy-candidate.sh <commit>
./verify-candidate.sh <release>
./promote.sh <release>
```

首次从旧发布结构迁移时，`PRODUCTION_ENV_FILE` 可以指向当前生产实际使用的旧 `backend.env`；新流程会将其另存为目标发布的 `production.env`。运行 `deploy-candidate.sh` 前必须停止准确的旧候选实例并确认 `8282/8284` 已释放，脚本不会自动停止任何未知服务。

三个变更脚本共用 `/run/lock/paragateway-release.lock`，同一时刻只允许一个候选部署、晋级或回滚操作。候选部署会从当前 active production backend unit 解析实际使用的 `EnvironmentFile` 和 `config.yaml` credential，并记录规范化源路径；晋级时会再次核对路径和内容，防止把目标 release 自身副本误当成当前生产配置。

`promote.sh` 使用独立的生产 Caddy 和 systemd 模板，在切换前完成校验并保存当前两个 production unit 的原样快照。同一 release 只允许尝试晋级一次，避免覆盖最初的回滚点；切换后的重启、健康检查或前端提交身份验证失败时，脚本会自动恢复该快照。

只有在成功晋级后需要主动撤回，或者自动恢复未能完成时，才单独执行：

```bash
./rollback.sh <failed-release>
```

`rollback.sh` 恢复晋级前保存的原始 production unit，不从候选 unit 推导生产配置。手工回滚前还会保存当前已安装的 production unit；若回滚过程失败，脚本会尝试恢复这份回滚前快照。
