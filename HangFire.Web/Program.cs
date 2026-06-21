using Hangfire;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("HangfireConnection")!;

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseColouredConsoleLogProvider()
    .UseSqlServerStorage(connectionString));

builder.Services.AddHangfireServer();

var app = builder.Build();

//app.UseHangfireDashboard("/hangfire");
app.MapHangfireDashboard("/hangfire");
RecurringJob.AddOrUpdate("sample-job", () => Console.WriteLine("Hangfire recurring job executed!"), Cron.Daily);
BackgroundJob.Enqueue(() => Console.WriteLine("Hangfire background job executed!"));

app.MapGet("/", () => "Hello World!");

app.Run();

