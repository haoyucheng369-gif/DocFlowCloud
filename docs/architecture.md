# 架构说明

DocFlowCloud 当前是一个**异步文档转 PDF 系统**。

它的目标不是只做一个简单上传接口，而是通过具体业务把下面这些能力串起来：
- HTTP API
- React 前端
- Outbox 发布
- RabbitMQ Topic 事件驱动
- 两个独立消费者
- Inbox 去重与 TryClaim 抢占处理权
- Retry / DLQ / Backoff
- CorrelationId 日志链路
- 本地共享存储 / Azure Blob 可切换文件存储

## 系统模块

### DocFlowCloud.Api

职责：
- 接收上传文件
- 校验输入
- 创建文档转 PDF 任务
- 查询任务
- 下载转换结果
- 创建或透传 CorrelationId

关键文件：
- `src/DocFlowCloud.Api/Program.cs`
- `src/DocFlowCloud.Api/Controllers/JobsController.cs`
- `src/DocFlowCloud.Api/Middleware/CorrelationIdMiddleware.cs`
- `src/DocFlowCloud.Api/Middleware/GlobalExceptionMiddleware.cs`

### DocFlowCloud.Application

职责：
- 编排创建任务、重试任务、下载结果
- 定义消息契约和 DTO
- 定义文件存储抽象 `IFileStorage`

关键文件：
- `src/DocFlowCloud.Application/Jobs/JobService.cs`
- `src/DocFlowCloud.Application/Jobs/DocumentToPdfJobPayload.cs`
- `src/DocFlowCloud.Application/Jobs/DocumentToPdfJobResult.cs`
- `src/DocFlowCloud.Application/Abstractions/Storage/IFileStorage.cs`

### DocFlowCloud.Domain

职责：
- 定义 `Job` 状态机
- 定义 `InboxMessage` 状态机
- 定义 `OutboxMessage`

关键文件：
- `src/DocFlowCloud.Domain/Jobs/Job.cs`
- `src/DocFlowCloud.Domain/Inbox/InboxMessage.cs`
- `src/DocFlowCloud.Domain/Outbox/OutboxMessage.cs`

### DocFlowCloud.Infrastructure

职责：
- 实现 EF Core 持久化
- 实现 RabbitMQ 发布
- 实现本地文件存储和 Azure Blob 存储
- 负责依赖注入和配置绑定

关键文件：
- `src/DocFlowCloud.Infrastructure/DependencyInjection.cs`
- `src/DocFlowCloud.Infrastructure/Persistence/AppDbContext.cs`
- `src/DocFlowCloud.Infrastructure/Messaging/RabbitMqJobMessagePublisher.cs`
- `src/DocFlowCloud.Infrastructure/Storage/LocalFileStorage.cs`
- `src/DocFlowCloud.Infrastructure/Storage/AzureBlobFileStorage.cs`

### DocFlowCloud.Worker

职责：
- 扫描并发布 Outbox
- 消费 `job.created`
- 通过 Inbox/Claim 保证安全消费
- 执行文档转 PDF
- 处理 Retry / DLQ / Backoff / 自动恢复

关键文件：
- `src/DocFlowCloud.Worker/OutboxPublisherWorker.cs`
- `src/DocFlowCloud.Worker/RabbitMqWorker.cs`
- `src/DocFlowCloud.Worker/JobSideEffectExecutor.cs`
- `src/DocFlowCloud.Worker/StaleInboxRecoveryWorker.cs`

### DocFlowCloud.NotificationService

职责：
- 订阅同一个 `job.created` 事件
- 模拟发送通知
- 自己维护独立 Inbox 状态

关键文件：
- `src/DocFlowCloud.NotificationService/NotificationWorker.cs`
- `src/DocFlowCloud.NotificationService/NotificationEmailSender.cs`

### DocFlowCloud.Web

职责：
- 上传文件并创建任务
- 查看任务列表
- 查看任务详情
- 轮询任务状态
- 下载 PDF
- 对失败任务执行重试

前端技术：
- React
- TypeScript
- Tailwind
- TanStack Query
- react-hook-form
- zod

## 业务数据与文件存储

### 任务数据

数据库里的 `Jobs` 仍然是业务主表，保存：
- 任务名
- 类型
- 状态
- 重试次数
- 错误信息
- `PayloadJson`
- `ResultJson`

### 文件存储

当前采用“**数据库存逻辑 key，存储层存文件内容**”的方式。

也就是：
- 原文件保存到存储层
- 转换后的 PDF 也保存到存储层
- 数据库里只保存：
  - `InputStorageKey`
  - `OutputStorageKey`

这样做的好处：
- 不把 Base64 文件内容塞进数据库
- 不把物理绝对路径写死在业务记录里
- 后面从本地目录切到 NAS / Azure Blob 时更容易迁移

## 消息设计

### Outbox

Outbox 保证：
- Job 创建成功时，消息不会被忘掉
- 消息发布可以异步进行
- 发布失败后可以继续重试

### Topic Exchange

当前系统发布一个核心事件：

- routing key: `job.created`

这个事件会被两个队列订阅：
- `docflow.jobs`
- `docflow.notifications`

含义是：
- API 只发布一次事件
- Job Worker 和 Notification Service 各自独立消费

### Inbox

每个消费者都维护自己的 Inbox 记录。

它解决：
- 消息去重
- 并发抢占处理权
- 处理中状态记录
- 失败状态记录
- 超时接管

## 当前可靠性能力

- Outbox
- Inbox
- TryClaim
- 业务幂等
- Retry
- DLQ
- Backoff
- 错误分类
- CorrelationId
- Stale Inbox 自动恢复

## 当前项目定位

这个仓库更适合理解成：
- 一个文档异步处理系统样板
- 一个带消息可靠性和恢复能力的企业应用练手项目

它已经覆盖了很多企业项目里很重要的核心能力，但还没有扩展到完整平台层，例如：
- API Gateway
- 统一认证平台
- 完整可观测性平台
- CI/CD 和云部署流水线
