# ParaGateway 界面对齐矩阵

基准页面：`https://api.blsc.dev`（Go + Vue 管理端 v0.1.176）  
实现页面：`http://127.0.0.1:8080`（Go + Blazor WebAssembly + DevExpress 26.1.3）

状态说明：

- `待核查`：尚未完成结构、交互和 API 的逐项审计。
- `进行中`：已形成差异清单，正在修改。
- `已对齐`：源码、API、构建和实际浏览器行为均已验证。
- `不适用`：用户明确排除的功能。

## 管理端

| 顺序 | Vue 页面 | Blazor 页面 | 当前状态 | 主要验收项 |
|---:|---|---|---|---|
| 1 | `/admin/dashboard` 仪表盘 | `/admin/dashboard` | 已对齐 | 8 个统计卡、按权限显示批量生图与分组定价快捷操作、最近 24 小时日期范围、日/小时粒度、模型分布及按模型展开用户明细、用户消费榜及使用记录跳转、Token/缓存命中趋势、Top 12 用户趋势；已用临时隔离管理员及最小用量数据完成真实容器浏览器验证并清理全部测试数据 |
| 2 | `/admin/ops` 运维监控 | `/admin/ops` | 待核查 | 概览、吞吐/延迟/错误图表、日志、告警规则、通知与运行设置 |
| 3 | `/admin/users` 用户管理 | `/admin/users` | 待核查 | 搜索筛选、创建编辑、状态、额度、并发、身份、安全操作 |
| 4 | `/admin/groups` 分组管理 | `/admin/groups` | 待核查 | 分组 CRUD、平台配置、模型/计价、调度、利润控制、复制 |
| 5 | `/admin/channels/pricing` 渠道管理 | `/admin/channels/pricing` | 待核查 | 渠道 CRUD、分组绑定、计费来源、模型限制、价格配置 |
| 6 | `/admin/channels/monitor` 渠道监控 | `/admin/channels/monitor` | 待核查 | V1/V2 筛选、矩阵、模型/错误/用户统计、监控 CRUD 与历史 |
| 7 | `/admin/subscriptions` 订阅管理 | `/admin/subscriptions` | 待核查 | 列表、搜索筛选、创建编辑、状态与配额 |
| 8 | `/admin/accounts` 账号管理 | `/admin/accounts` | 待核查 | OAuth/密钥接入、账号 CRUD、批量操作、调度状态、用量窗口、模型测试 |
| 9 | `/admin/announcements` 公告 | `/admin/announcements` | 待核查 | 公告 CRUD、发布状态、时间范围与受众 |
| 10 | `/admin/proxies` IP 管理 | `/admin/proxies` | 待核查 | 代理 CRUD、测试、关联账号、状态与延迟 |
| 11 | `/admin/risk-control` 安全审计/风控 | `/admin/risk-control` | 待核查 | 配置、运行状态、日志、解封与策略操作 |
| 12 | `/admin/redeem` 兑换码 | `/admin/redeem` | 待核查 | 生成、批量修改、查询、使用记录与失效 |
| 13 | `/admin/promo-codes` 优惠码 | `/admin/promo-codes` | 待核查 | CRUD、适用范围、有效期、使用统计 |
| 14 | `/admin/usage` 使用记录 | `/admin/usage` | 待核查 | 日志/错误页签、组合筛选、统计、模型/端点分布、详情 |
| 15 | `/admin/audit-logs` 操作日志 | `/admin/audit-logs` | 待核查 | 搜索筛选、分页、操作详情 |
| 16 | `/admin/settings` 系统设置 | `/admin/settings` | 待核查 | 全部设置分区、保存、邮件模板、OAuth、安全与策略配置 |

## 管理员“我的账户”及普通用户端

| 顺序 | Vue 页面 | Blazor 页面 | 当前状态 | 主要验收项 |
|---:|---|---|---|---|
| 17 | `/keys` API 密钥 | `/keys`、`/api-keys` | 已对齐 | 当前用户分页/搜索/分组/状态筛选、排序、动态列持久化、创建与自定义密钥、编辑、分组、启停、IP/额度/速率/有效期、用量、额度与速率重置、客户端配置、CC-Switch 和删除；已完成真实容器浏览器验证 |
| 18 | `/usage` 使用记录 | `/usage` | 已对齐 | 用户统计、筛选、趋势、模型/分组/端点分布、错误与明细；已验证 Token/图片计费、列设置与 CSV 导出契约 |
| 19 | `/profile` 个人资料 | `/profile`、`/account` | 已对齐 | 官方资料总览、头像上传压缩/保存/删除、用户名、资料来源、OAuth/邮箱绑定、8 位密码规则、余额提醒及最多 3 个验证邮箱、TOTP 三步启用/禁用弹窗、Passkey 添加/重命名/密码确认删除；已完成 API 契约、明暗主题、移动端及真实容器浏览器验证 |
| 20 | 官方当前版本无 `/user-guide` 路由 | 不适用 | 不适用 | 已按官方路由源码复核；不额外虚构页面 |
| 21 | `/dashboard` 用户仪表盘 | `/dashboard` | 已对齐 | 可用余额、密钥/请求/费用、Token 与性能统计、五平台用量及配额、最近 7 天日期范围与日/小时粒度、模型分布、Token 趋势、最近调用和条件化快捷操作；已完成真实普通用户桌面端/移动端/深色主题、刷新交互与接口无错误验证 |
| 22 | `/redeem` 兑换码 | `/redeem` | 已对齐 | 兑换与历史 |
| 23 | `/subscriptions` 我的订阅 | `/subscriptions` | 已对齐 | 当前订阅、日/周/月进度、到期时间、高峰倍率与无限额度；支付续费入口按项目范围排除 |
| 24 | 官方全局公告体验 | `/announcements` + 全局公告铃铛 | 已对齐 | 未读、已读、列表、详情和重要公告弹窗 |
| 25 | `/available-channels` 可用渠道 | `/available-channels` | 已对齐 | 渠道、分组、模型和计价信息 |
| 26 | `/affiliate` 邀请返利 | `/affiliate` | 已对齐 | 邀请信息、记录、返利与额度转移 |
| 27 | `/monitor` 渠道状态 | `/monitor` | 已对齐 | V1/V2 状态模式、延迟、历史、筛选及管理员/用户数据范围 |
| 28 | `/model-plaza` 模型广场 | `/model-plaza` | 已对齐 | 公开/内嵌布局、联动筛选、模型搜索、折后价、官方价、阶梯/缓存/按次/按图计价与专属倍率 |
| 29 | `/batch-image` 批量生图 | `/batch-image` | 已对齐 | API Key、模型、任务、项目、下载、取消与删除 |

## 当前 Go 后端扩展页面

这些页面存在于当前 Go 后端，但不在上述线上 v0.1.170 主导航中。完成基准页面后再按对应后端功能验收：官方 OAuth、账号模型、用户属性、备份与数据、数据管理代理、错误透传规则、TLS 指纹模板、Prompt Audit、系统更新、联盟管理。

## 明确排除

支付、订单、支付计划、支付回调和支付二维码页面不实现；这是项目既定范围，不计入对齐缺口。
