# ParaGateway 生产候选发布

本目录用于服务器候选端口发布和生产晋级。代码仍在本机修改、测试、提交并推送 GitHub；服务器只拉取指定提交。

- 生产后端：`127.0.0.1:8184`
- 候选后端：`127.0.0.1:8284`
- 生产网关：`127.0.0.1:8182`
- 候选网关：`127.0.0.1:8282`
- 源码镜像：`/opt/paragateway/source`
- 发布目录：`/opt/paragateway/releases/<release>`
- 配置目录：`/etc/paragateway/<release>`

候选实例必须使用只读数据库副本或临时数据库快照。`deploy-candidate.sh` 要求候选与生产的 PostgreSQL 主机和库名组合不同、候选 Redis 使用独立 DB（默认 `15`），并为每个候选实例创建独立的 `/var/lib/paragateway-backend-<release>` 数据目录。`promote.sh` 不执行数据库迁移。

先在 Windows 构建机生成带许可证的前端归档。脚本会清理 Release 缓存，拒绝包含 DevExpress 评估警告的构建，并且不会把许可证文件放入归档：

```powershell
$commit = (git rev-parse HEAD).Trim()
.\deploy\production\build-frontend-archive.ps1 -Commit $commit
```

```bash
export CANDIDATE_ENV_FILE=/etc/paragateway/candidate-readonly.env
export PRODUCTION_ENV_FILE=/etc/paragateway/<production-release>/backend.env
export PRODUCTION_CONFIG_FILE=/etc/paragateway/<production-release>/config.yaml
export FRONTEND_ARCHIVE=/var/tmp/paragateway-frontend-<commit>.tar.gz
./deploy-candidate.sh <commit>
./verify-candidate.sh <release>
./promote.sh <release>
./rollback.sh <previous-release>
```
