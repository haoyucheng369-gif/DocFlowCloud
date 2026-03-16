using DocFlowCloud.Api.Contracts;
using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace DocFlowCloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// Jobs API 控制器：
// 对外提供任务创建、任务查询、结果下载和业务级重试入口。
public sealed class JobsController : ControllerBase
{
    private static readonly HashSet<string> SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
        ".txt", ".md", ".html", ".htm"
    ];

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
        // 保留原有的 JSON 创建入口，方便继续测试通用异步任务。
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

    [HttpPost("document-to-pdf")]
    [HttpPost("image-to-pdf")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateDocumentToPdf([FromForm] CreateDocumentToPdfForm request, CancellationToken cancellationToken)
    {
        // 新增的简单文档转换入口：
        // 当前支持 image、txt、md、html 四类输入，由 Worker 异步转成 PDF。
        var file = request.File;

        if (file.Length == 0)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["file"] = ["文件不能为空。"]
            }));
        }

        if (!IsSupported(file))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["file"] = ["当前版本支持：图片、txt、md、html。"]
            }));
        }

        // 先把上传内容读入内存，再交给应用层创建 Job 和 Outbox。
        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        var jobId = await _jobService.CreateDocumentToPdfAsync(
            request.Name,
            file.FileName,
            file.ContentType,
            memoryStream.ToArray(),
            cancellationToken);

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
        // 查询单个任务详情。
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
        // 查询任务列表，供前端轮询和列表页使用。
        var jobs = await _jobService.GetAllAsync(cancellationToken);
        return Ok(jobs);
    }

    [HttpGet("{id:guid}/result-file")]
    public async Task<IActionResult> DownloadResultFile(Guid id, CancellationToken cancellationToken)
    {
        // 任务成功后，由这个接口返回最终生成出来的 PDF 文件。
        var file = await _jobService.GetResultFileAsync(id, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        // 业务级重试入口：失败任务可以通过这里重新进入主流程。
        await _jobService.RetryAsync(id, cancellationToken);
        return NoContent();
    }

    private static bool IsSupported(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (SupportedExtensions.Contains(extension))
        {
            return true;
        }

        return file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || file.ContentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase)
            || file.ContentType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase)
            || file.ContentType.Equals("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
