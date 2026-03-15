using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Abstractions.Processing;
using DocFlowCloud.Application.Jobs;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocFlowCloud.Worker;

// 副作用执行器：
// 这里承载真正的“任务处理逻辑”。当前实现的是简单文档转 PDF，
// 支持图片、txt、md、html 四类输入。
public sealed class JobSideEffectExecutor : IJobSideEffectExecutor
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<JobSideEffectExecutor> _logger;

    public JobSideEffectExecutor(ILogger<JobSideEffectExecutor> logger)
    {
        _logger = logger;
    }

    public Task<string> ExecuteAsync(
        Guid jobId,
        string jobType,
        string payloadJson,
        string idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        // 先记录本次处理的关键上下文，方便以后通过日志排查问题。
        _logger.LogInformation(
            "Executing external side effect. JobId: {JobId}, JobType: {JobType}, IdempotencyKey: {IdempotencyKey}, CorrelationId: {CorrelationId}",
            jobId,
            jobType,
            idempotencyKey,
            correlationId);

        // 这里演示“如果以后要调第三方 HTTP 服务，哪些技术头需要继续传下去”。
        var outboundHeaders = new Dictionary<string, string>
        {
            [CorrelationConstants.HeaderName] = correlationId
        };

        // 当前版本只支持简单文档转 PDF，其他类型直接视为不支持。
        if (!string.Equals(jobType, JobService.DocumentToPdfJobType, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported job type '{jobType}'.");
        }

        // 反序列化任务 payload，取出上传的原始文件内容。
        var payload = JsonSerializer.Deserialize<DocumentToPdfJobPayload>(payloadJson, JsonSerializerOptions)
            ?? throw new JsonException("DocumentToPdf payload is invalid.");

        var fileBytes = Convert.FromBase64String(payload.FileBytesBase64);
        var pdfBytes = GeneratePdf(payload, fileBytes);
        var result = new DocumentToPdfJobResult
        {
            OutputFileName = $"{Path.GetFileNameWithoutExtension(payload.OriginalFileName)}.pdf",
            PdfBytesBase64 = Convert.ToBase64String(pdfBytes),
            GeneratedAtUtc = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Document to PDF conversion completed. JobId: {JobId}, OutputFileName: {OutputFileName}",
            jobId,
            result.OutputFileName);

        // 把结果重新序列化成结构化 JSON，后面会写回 Job.ResultJson。
        var resultJson = JsonSerializer.Serialize(new
        {
            generatedAtUtc = result.GeneratedAtUtc,
            status = "OK",
            jobType = JobService.DocumentToPdfJobType,
            idempotencyKey,
            correlationId,
            outboundHeaders,
            outputFileName = result.OutputFileName,
            pdfBytesBase64 = result.PdfBytesBase64
        }, JsonSerializerOptions);

        return Task.FromResult(resultJson);
    }

    private static byte[] GeneratePdf(DocumentToPdfJobPayload payload, byte[] fileBytes)
    {
        // 当前按文件类型分支：
        // 图片直接嵌入 PDF；文本类先提取纯文本再排版到 PDF。
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(12));

                if (IsImage(payload))
                {
                    page.Content().Image(fileBytes).FitArea();
                    return;
                }

                var text = ExtractText(payload, fileBytes);
                page.Content().Column(column =>
                {
                    column.Item().Text(payload.OriginalFileName).Bold().FontSize(16);
                    column.Item().PaddingTop(12).Text(text);
                });
            });
        }).GeneratePdf();
    }

    private static bool IsImage(DocumentToPdfJobPayload payload)
    {
        return payload.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || IsExtension(payload, ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp");
    }

    private static string ExtractText(DocumentToPdfJobPayload payload, byte[] fileBytes)
    {
        var rawText = Encoding.UTF8.GetString(fileBytes);

        if (payload.ContentType.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
            IsExtension(payload, ".html", ".htm"))
        {
            return ConvertHtmlToPlainText(rawText);
        }

        if (payload.ContentType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase) ||
            IsExtension(payload, ".md"))
        {
            return ConvertMarkdownToPlainText(rawText);
        }

        return rawText;
    }

    private static string ConvertHtmlToPlainText(string html)
    {
        var withLineBreaks = Regex.Replace(html, @"<(br|/p|/div|/li|/h[1-6])\b[^>]*>", Environment.NewLine, RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withLineBreaks, "<[^>]+>", string.Empty, RegexOptions.Singleline);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    private static string ConvertMarkdownToPlainText(string markdown)
    {
        var text = markdown
            .Replace("### ", string.Empty)
            .Replace("## ", string.Empty)
            .Replace("# ", string.Empty)
            .Replace("**", string.Empty)
            .Replace("__", string.Empty)
            .Replace("`", string.Empty);

        text = Regex.Replace(text, @"!\[[^\]]*\]\([^)]+\)", string.Empty);
        text = Regex.Replace(text, @"\[(?<text>[^\]]+)\]\([^)]+\)", "${text}");
        text = Regex.Replace(text, @"^\s*[-*+]\s+", string.Empty, RegexOptions.Multiline);
        return text.Trim();
    }

    private static bool IsExtension(DocumentToPdfJobPayload payload, params string[] extensions)
    {
        var extension = Path.GetExtension(payload.OriginalFileName);
        return extensions.Any(x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase));
    }
}
