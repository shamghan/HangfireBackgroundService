using Hangfire;
using HangFire.Web;
using HangFire.Web.Jobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var connectionString = builder.Configuration.GetConnectionString("HangfireConnection")!;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services.AddRazorPages();

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

app.UseAuthentication();
app.UseAuthorization();

// kevin@tactics.co is a member of that role.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    if (dbContext.Database.GetPendingMigrations().Any())
    {
        await dbContext.Database.MigrateAsync();
    }

    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        var role = new IdentityRole("Admin");
        await roleManager.CreateAsync(role);
    }

    var user = await userManager.FindByEmailAsync("ghansham@hangfire.com");
    if (user == null)
    {
        user = new IdentityUser { UserName = "ghansham@hangfire.com", Email = "ghansham@hangfire.com", EmailConfirmed = true };
        var createResult = await userManager.CreateAsync(user, "Admin@123");
        if (!createResult.Succeeded)
        {
            throw new Exception($"Failed to create seed user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }
    }

    if (!(await userManager.IsInRoleAsync(user, "Admin")))
    {
        await userManager.AddToRoleAsync(user, "Admin");
    }

    var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate("sample-job", () => Console.WriteLine("Hangfire recurring job executed!"), Cron.Daily);

    var backgroundJobClient = services.GetRequiredService<IBackgroundJobClient>();
    backgroundJobClient.Enqueue(() => Console.WriteLine("Hangfire background job executed!"));
}

app.MapGet("/", () => Results.Redirect("/hangfire"));

var url = "https://consultwithgriff.com/rss.xml";
var directory = $"c:\\rss";
var filename = "consultwithgriff.json";
var tempPath = Path.Combine(directory, filename);

RecurringJob.AddOrUpdate<WebPuller>("pull-rss-feed",
    p => p.GetRssItemUrlsAsync(url, tempPath), "55 9 * * *");
//https://crontab.guru/#55_9_*_*_*

//Remove recurring job
//RecurringJob.RemoveIfExists("pull-rss-feed");

//trigger job on demand
app.MapGet("/pull-feed", () =>
{
    RecurringJob.TriggerJob("pull-rss-feed");
});

app.MapGet("/pull", (IBackgroundJobClient client) =>
{
    var url = "https://consultwithgriff.com/rss.xml";
    var directory = $"c:\\rss";
    var filename = "consultwithgriff.json";
    var tempPath = Path.Combine(directory, filename);

    // TODO: background work
    client.Enqueue<WebPuller>(p => p.GetRssItemUrlsAsync(url, tempPath));
});
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

app.MapHangfireDashboard(new DashboardOptions()
{
    Authorization = new[] { new HangFireAuthorizationFilter() }
}).RequireAuthorization("AdminOnly");
app.MapRazorPages();
app.Run();

