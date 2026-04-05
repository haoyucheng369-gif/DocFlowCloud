using System.Diagnostics;

namespace DocFlowCloud.Application.Abstractions.Observability;

public static class DocFlowCloudTracing
{
    public const string SourceName = "DocFlowCloud";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
