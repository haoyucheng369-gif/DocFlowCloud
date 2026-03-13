using System.Text.Json;
using DocFlowCloud.Application.Exceptions;

namespace DocFlowCloud.Worker;

public enum MessageFailureDisposition
{
    Retry,
    DeadLetter
}

public static class MessageFailureClassification
{
    public static MessageFailureDisposition Classify(Exception exception)
    {
        return exception switch
        {
            JobNotFoundException => MessageFailureDisposition.DeadLetter,
            InvalidJobStateException => MessageFailureDisposition.DeadLetter,
            JsonException => MessageFailureDisposition.DeadLetter,
            NotSupportedException => MessageFailureDisposition.DeadLetter,
            _ => MessageFailureDisposition.Retry
        };
    }
}
