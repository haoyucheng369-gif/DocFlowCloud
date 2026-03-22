using System.Text.Json;
using DocFlowCloud.Application.Exceptions;
using DocFlowCloud.Worker;

namespace DocFlowCloud.UnitTests.Worker;

public sealed class MessageFailureClassificationTests
{
    [Fact]
    public void Classify_JobNotFound_ReturnsDeadLetter()
    {
        var disposition = MessageFailureClassification.Classify(new JobNotFoundException(Guid.NewGuid()));

        Assert.Equal(MessageFailureDisposition.DeadLetter, disposition);
    }

    [Fact]
    public void Classify_InvalidPayload_ReturnsDeadLetter()
    {
        var disposition = MessageFailureClassification.Classify(new JsonException("bad json"));

        Assert.Equal(MessageFailureDisposition.DeadLetter, disposition);
    }

    [Fact]
    public void Classify_UnknownException_ReturnsRetry()
    {
        var disposition = MessageFailureClassification.Classify(new TimeoutException("transient"));

        Assert.Equal(MessageFailureDisposition.Retry, disposition);
    }
}
