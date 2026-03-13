namespace DocFlowCloud.Application.Abstractions.Observability;

public interface ICorrelationContextAccessor
{
    string GetCorrelationId();
}
