using Hangfire;
using HangFire.Web.Jobs;
using Microsoft.Data.SqlClient;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("HangfireConnection")!;

builder.Services.AddHttpClient();
builder.Services.AddTransient<WebPuller>();
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

var url = "https://consultwithgriff.com/rss.xml";
var directory = $"c:\\rss";
var filename = "consultwithgriff.json";
var tempPath = Path.Combine(directory, filename);

RecurringJob.AddOrUpdate<WebPuller>("pull-rss-feed",
    p => p.GetRssItemUrlsAsync(url, tempPath), "55 9 * * *");
//https://crontab.guru/#55_9_*_*_*

//Remove recurring job
//RecurringJob.RemoveIfExists("pull-rss-feed");


//app.MapGet("/pull", (IBackgroundJobClient client) =>
//{
//    var url = "https://consultwithgriff.com/rss.xml";
//    var directory = $"c:\\rss";
//    var filename = "consultwithgriff.json";
//    var tempPath = Path.Combine(directory, filename);

//    // TODO: background work
//    client.Enqueue<WebPuller>(p => p.GetRssItemUrlsAsync(url, tempPath));
//});
app.MapGet("/sync", (IBackgroundJobClient client) =>
{
    var directory = $"c:\\rss";
    var filename = "consultwithgriff.json";

    var path = Path.Combine(directory, filename);
    var json = File.ReadAllText(path);
    var rssItemUrls = JsonSerializer.Deserialize<List<string>>(json);

    var outputPath = Path.Combine(directory, "output");
    if (!Directory.Exists(outputPath))
        Directory.CreateDirectory(outputPath);

    if (rssItemUrls == null || rssItemUrls.Count == 0)
        return;

    var delayInSeconds = 5;

    foreach (var url in rssItemUrls)
    {
        var u = new Uri(url);
        var stub = u.Segments.Last();

        // trim trailing slash, if any and add .html
        if (stub.EndsWith("/"))
            stub = stub.Substring(0, stub.Length - 1);

        stub += ".html";

        var filePath = Path.Combine(outputPath, stub);
        var dt = DateTimeOffset.UtcNow.AddSeconds(delayInSeconds);
        client.Schedule<WebPuller>(
            p => p.DownloadFileFromUrl(url, filePath),
            dt);

        delayInSeconds += 5;
    }
});
app.Run();

