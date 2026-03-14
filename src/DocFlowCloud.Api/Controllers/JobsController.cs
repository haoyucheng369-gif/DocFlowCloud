using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace DocFlowCloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// API 入口控制器：
// 只负责接收 HTTP 请求和返回 HTTP 响应，不直接写业务规则。
public sealed class JobsController : ControllerBase
{
    private readonly ICorrelationContextAccessor _correlationContextAccessor;
    private readonly JobService _jobService;

    public JobsController(JobService jobService, ICorrelationContextAccessor correlationContextAccessor)
    {
        _jobService = jobService;
        _correlationContextAccessor = correlationContextAccessor;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest request, CancellationToken cancellationToken)
    {
        // 创建任务成功后，把 jobId 和 correlationId 一起返回，方便后续查日志和排障。
        var jobId = await _jobService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = jobId },
            new
            {
                jobId,
                correlationId = _correlationContextAccessor.GetCorrelationId()
            });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        // 单条任务查询。
        var job = await _jobService.GetByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(job);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        // 任务列表查询。
        var jobs = await _jobService.GetAllAsync(cancellationToken);
        return Ok(jobs);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        // 业务级恢复入口：明确失败后的 Job 可以通过这个 API 重新进入主流程。
        await _jobService.RetryAsync(id, cancellationToken);

        return NoContent();
    }
}
