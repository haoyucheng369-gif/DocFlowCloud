namespace DocFlowCloud.Infrastructure.Messaging;

public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";

    public string TopicExchangeName { get; set; } = "docflow.events";
    public string QueueName { get; set; } = "docflow.jobs";
    public string NotificationQueueName { get; set; } = "docflow.notifications";
    public string RetryQueueName { get; set; } = "docflow.jobs.retry";
    public string DeadLetterQueueName { get; set; } = "docflow.jobs.dlq";
    public string JobCreatedRoutingKey { get; set; } = "job.created";
    public string JobQueueBindingKey { get; set; } = "job.created";
    public string NotificationQueueBindingKey { get; set; } = "job.*";
    public int ProcessingTimeoutSeconds { get; set; } = 300;
    public int StaleRecoveryScanSeconds { get; set; } = 30;
}
