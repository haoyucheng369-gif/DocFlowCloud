# 架构说明

`DocFlowCloud` 当前是一个异步文档转 PDF 系统，用来展示：

- ASP.NET Core API
- React 前端
- Outbox / Inbox 可靠性模式
- Azure Blob 文件存储
- Azure SQL 持久化
- Azure Service Bus 云上消息链
- SignalR 实时状态更新
- GitHub Actions + GHCR + Azure Container Apps 交付链

## 系统模块

### `DocFlowCloud.Api`

职责：

- 接收上传文件
- 创建转换任务
- 查询任务和结果
- 下载 PDF
- 提供 SignalR Hub
- 在 testbed 中消费 `api-realtime` subscription 并推送实时更新

关键文件：

- `src/DocFlowCloud.Api/Program.cs`
- `src/DocFlowCloud.Api/Controllers/JobsController.cs`
- `src/DocFlowCloud.Api/Realtime/ServiceBusJobStatusUpdatesConsumer.cs`

### `DocFlowCloud.Application`

职责：

- 编排任务创建、重试、结果下载
- 定义应用层 DTO / integration message
- 定义文件存储和消息发布抽象

关键文件：

- `src/DocFlowCloud.Application/Jobs/JobService.cs`
- `src/DocFlowCloud.Application/Messaging/JobCreatedIntegrationMessage.cs`

### `DocFlowCloud.Domain`

职责：

- 定义 `Job`
- 定义 `InboxMessage`
- 定义 `OutboxMessage`
- 定义状态规则

### `DocFlowCloud.Infrastructure`

职责：

- EF Core 持久化
- Local / Azure Blob 存储实现
- RabbitMQ 与 Azure Service Bus provider
- 依赖注入与环境切换

说明：

- **本地 Development**：继续保留 RabbitMQ provider
- **云上 Testbed**：切换到 Azure Service Bus provider

关键文件：

- `src/DocFlowCloud.Infrastructure/DependencyInjection.cs`
- `src/DocFlowCloud.Infrastructure/Messaging/ServiceBusJobMessagePublisher.cs`
- `src/DocFlowCloud.Infrastructure/Storage/AzureBlobFileStorage.cs`

### `DocFlowCloud.Worker`

职责：

- 扫描并发布 Outbox
- 消费任务消息
- 执行文档转换
- 写入结果文件和最终状态

关键文件：

- `src/DocFlowCloud.Worker/OutboxPublisherWorker.cs`
- `src/DocFlowCloud.Worker/ServiceBusWorker.cs`
- `src/DocFlowCloud.Worker/JobSideEffectExecutor.cs`
- `src/DocFlowCloud.Worker/StaleInboxRecoveryWorker.cs`

### `DocFlowCloud.NotificationService`

职责：

- 订阅任务创建事件
- 执行通知逻辑
- 维护自己的 Inbox 处理状态

关键文件：

- `src/DocFlowCloud.NotificationService/ServiceBusNotificationWorker.cs`

### `DocFlowCloud.Web`

职责：

- 上传文件
- 创建任务
- 查看任务列表和详情
- 订阅 SignalR 状态更新
- 下载 PDF

## 数据和存储

### 数据库

主数据库记录：

- `Jobs`
- `InboxMessages`
- `OutboxMessages`

### 文件存储

系统不把文件内容塞进数据库，而是：

- 原文件存到 Local 或 Azure Blob
- 结果 PDF 存到 Local 或 Azure Blob
- 数据库只存 `InputStorageKey` / `OutputStorageKey`

## 消息架构

### 本地

- RabbitMQ

### Testbed

- Azure Service Bus Topic：`job-events`
- Subscription：
  - `worker`
  - `notification`
  - `api-realtime`

## 当前项目定位

这个仓库现在更适合理解成：

- 一个带真实异步链、可靠性模式和云上 testbed 的全栈样板项目
- 一个能够展示应用开发、云部署、CI/CD、运行时配置管理的工程化项目

## 后续可以继续增强的方向

- Key Vault 完整收口
- Production 环境
- Terraform
- Observability / Dashboard / Alerts
