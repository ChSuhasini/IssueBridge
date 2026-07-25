using System.Net.Http.Headers;
using IssueBridge.Api.Assistant.Tools;
using IssueBridge.Api.Data;
using IssueBridge.Api.GitHub;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<IssueBridgeDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("IssueBridge")
        ?? "Data Source=issuebridge.db"));

builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName));

builder.Services.AddHttpClient<IGitHubIssuesClient, GitHubIssuesClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GitHubOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl);
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("IssueBridge", "1.0"));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    if (!string.IsNullOrEmpty(options.Token))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
    }
});

builder.Services.AddScoped<ISyncService, SyncService>();

// Operations Assistant: read-only tool-executor layer. No Anthropic client or
// controller wired up yet — this just makes the five tools resolvable via DI
// so the agent loop (added next) has a registry to call into.
builder.Services.AddScoped<IAssistantTool, GetOpenIssuesTool>();
builder.Services.AddScoped<IAssistantTool, GetHighPriorityIssuesTool>();
builder.Services.AddScoped<IAssistantTool, GetIssueDetailsTool>();
builder.Services.AddScoped<IAssistantTool, GetDashboardSummaryTool>();
builder.Services.AddScoped<IAssistantTool, GetIssuesByAssigneeTool>();
builder.Services.AddScoped<AssistantToolExecutor>();

// Single-user demo project with no auth yet, so any origin can call the API —
// revisit this once real authentication exists.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
