using DocFlowCloud.Api.Extensions;
using DocFlowCloud.Api.Validators;
using DocFlowCloud.Application.Jobs;
using DocFlowCloud.Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;
using DocFlowCloud.Infrastructure.Persistence;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateJobRequestValidator>();

builder.Services.AddHealthChecks();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DocFlowCloud.Infrastructure.Persistence.AppDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JobService>();

var app = builder.Build();

app.UseGlobalExceptionMiddleware();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();