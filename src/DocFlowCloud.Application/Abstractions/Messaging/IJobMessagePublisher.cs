using System;
using System.Collections.Generic;
using System.Text;

namespace DocFlowCloud.Application.Abstractions.Messaging
{
    public interface IJobMessagePublisher
    {
        Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default);
    }
}
