namespace DocFlowCloud.Application.Abstractions.Messaging;

public interface IJobMessagePublisher
{
    Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task PublishRawAsync(string payloadJson, CancellationToken cancellationToken = default);
}