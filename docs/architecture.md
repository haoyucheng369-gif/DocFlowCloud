# 架构说明

DocFlowCloud 是一个用于学习以下能力的项目：

- HTTP API
- Outbox 发布
- RabbitMQ Topic 事件流
- 两个独立消费者
- Inbox 去重与抢占处理权
- Retry / DLQ / Backoff
- CorrelationId 日志链路

## 系统模块

### DocFlowCloud.Api

职责：

- 接收客户端请求
- 校验输入
- 创建任务
- 暴露 HealthChecks 和 Swagger
- 创建或透传 CorrelationId

关键文件：

- `src/DocFlowCloud.Api/Program.cs`
- `src/DocFlowCloud.Api/Controllers/JobsController.cs`
- `src/DocFlowCloud.Api/Middleware/CorrelationIdMiddleware.cs`
- `src/DocFlowCloud.Api/Middleware/GlobalExceptionMiddleware.cs`

### DocFlowCloud.Application

职责：

- 定义应用用例和集成消息契约
- 编排任务创建、任务重试
- 定义持久化、观测、消息、副作用接口

关键文件：

- `src/DocFlowCloud.Application/Jobs/JobService.cs`
- `src/DocFlowCloud.Application/Messaging/JobCreatedIntegrationMessage.cs`

### DocFlowCloud.Domain

职责：

- 放置业务规则和状态流转
- 把实体约束收口到模型内部

关键文件：

- `src/DocFlowCloud.Domain/Jobs/Job.cs`
- `src/DocFlowCloud.Domain/Inbox/InboxMessage.cs`
- `src/DocFlowCloud.Domain/Outbox/OutboxMessage.cs`

### DocFlowCloud.Infrastructure

职责：

- 实现 EF Core 持久化
- 实现 RabbitMQ 发布
- 提供配置和数据库迁移

关键文件：

- `src/DocFlowCloud.Infrastructure/Persistence/AppDbContext.cs`
- `src/DocFlowCloud.Infrastructure/Messaging/RabbitMqJobMessagePublisher.cs`
- `src/DocFlowCloud.Infrastructure/DependencyInjection.cs`

### DocFlowCloud.Worker

职责：

- 消费 `job.created`
- 通过 inbox claim 消息
- 执行任务副作用
- 处理 Retry / DLQ / Backoff / 错误分类

关键文件：

- `src/DocFlowCloud.Worker/RabbitMqWorker.cs`
- `src/DocFlowCloud.Worker/OutboxPublisherWorker.cs`
- `src/DocFlowCloud.Worker/JobSideEffectExecutor.cs`

### DocFlowCloud.NotificationService

职责：

- 消费同一个 `job.created` 事件
- 模拟发送邮件通知
- 维护自己独立的 inbox 消费记录

关键文件：

- `src/DocFlowCloud.NotificationService/NotificationWorker.cs`
- `src/DocFlowCloud.NotificationService/NotificationEmailSender.cs`

## 消息设计

### Outbox

Outbox 负责保证：

- 业务数据提交成功时，事件不会被忘记
- 消息发送可以异步进行
- 发布失败后可以重试

### Topic Exchange

当前系统只发布一个事件：

- routing key: `job.created`

两个队列订阅这个事件：

- `docflow.jobs`
- `docflow.notifications`

这意味着：

- API 只需要发布一个事件
- 不需要知道具体有哪些消费者
- 新增消费者时，只需要增加新的 queue binding

### Inbox

每个消费者都维护自己的 inbox 记录。

它负责：

- 去重
- 记录 claim 状态
- 标记成功或失败
- 支持超时接管和重试

两个消费者通过不同的 `ConsumerName` 区分，所以同一条消息可以：

- 被 Job Worker 处理一次
- 被 Notification Service 处理一次

## 当前可靠性能力

- `Outbox`
- `Inbox`
- `TryClaimAsync`
- 幂等键
- `Retry`
- `DLQ`
- `Backoff`
- 错误分类
- `CorrelationId`

## 当前项目定位

这个仓库更适合被理解成：

- 一个高质量的异步处理和可靠消息学习项目
- 一个微服务核心机制练习项目

它还不是完整的微服务平台，因为目前还没有系统化覆盖：

- API Gateway
- 认证授权平台
- OpenTelemetry
- 指标和可视化面板
- Saga / Compensation
- 部署自动化
