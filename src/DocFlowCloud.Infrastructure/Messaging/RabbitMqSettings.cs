namespace DocFlowCloud.Infrastructure.Messaging;

public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";

    public string QueueName { get; set; } = "docflow.jobs";
    public string DeadLetterQueueName { get; set; } = "docflow.jobs.dlq";
    public int ProcessingTimeoutSeconds { get; set; } = 300;
}
