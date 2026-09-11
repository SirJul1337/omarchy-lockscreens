using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OmarchyLockscreens.Data;
using OmarchyLockscreens.Models;
using OmarchyLockscreens.Services;

var builder = WebApplication.CreateBuilder(args);

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);
// MySQL in production (Simply.com), SQLite for local dev. Chosen by whether a
// connection string is configured, so no secrets are needed to run locally.
var mysqlConn = builder.Configuration.GetConnectionString("MySql");
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (!string.IsNullOrWhiteSpace(mysqlConn))
        o.UseMySql(mysqlConn, ServerVersion.AutoDetect(mysqlConn));
    else
        o.UseSqlite($"Data Source={Path.Combine(dataDir, "lockscreens.db")}");
});

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddScoped<RegistrySync>();
builder.Services.AddHostedService<RegistrySyncService>();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// Single-admin login: a key from config, no GitHub, no user accounts. It
// guards /admin/sync, which is the only thing an admin does here now --
// designs arrive by pull request and are listed by the registry sync, so
// there is nothing to approve after the fact. Visitors never log in.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/admin/login";
        o.AccessDeniedPath = "/admin/login";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        o.Cookie.Name = "ols_admin";
    });
builder.Services.AddAuthorization(o =>
    o.AddPolicy("Admin", p => p.RequireRole("admin")));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Maintenance: `--reset-db` drops the TABLES (not the database — the MySQL
    // user can't recreate the database on shared hosting), recreates the schema
    // in place, and reseeds. Only runs when that CLI arg is passed (never IIS).
    if (args.Contains("--reset-db"))
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SET FOREIGN_KEY_CHECKS=0";
            await cmd.ExecuteNonQueryAsync();
            foreach (var t in new[] { "Votes", "Reports", "DesignVersions", "Designs", "Users", "AuditLogs" })
            {
                cmd.CommandText = $"DROP TABLE IF EXISTS `{t}`";
                await cmd.ExecuteNonQueryAsync();
            }
            cmd.CommandText = "SET FOREIGN_KEY_CHECKS=1";
            await cmd.ExecuteNonQueryAsync();
        }
        await db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>().CreateTablesAsync();
        await SeedData.EnsureSeededAsync(db);
        Console.WriteLine("reset-db: tables dropped, schema recreated, reseeded.");
        return;
    }
    db.Database.EnsureCreated();
    if (app.Environment.IsDevelopment()) await SeedData.EnsureSeededAsync(db);
}

// Force HTTPS. Loop-proof: redirects only when the proxy explicitly reports the
// original scheme was http (Simply's ANCM / a CDN sets X-Forwarded-Proto).
app.Use(async (ctx, next) =>
{
    var proto = ctx.Request.Headers["X-Forwarded-Proto"].ToString();
    if (!ctx.Request.IsHttps
        && string.Equals(proto, "http", StringComparison.OrdinalIgnoreCase)
        && HttpMethods.IsGet(ctx.Request.Method))
    {
        ctx.Response.Redirect($"https://{ctx.Request.Host.Host}{ctx.Request.Path}{ctx.Request.QueryString}", permanent: false);
        return;
    }
    await next();
});

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
// .qml is not a type the static file middleware knows, and it refuses to
// serve what it cannot name -- which 404s every design in the registry while
// the files sit right there on disk. Named here as plain text.
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".qml"] = "text/plain; charset=utf-8";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes,
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public, max-age=86400"
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// --- admin login / out (key-based, single admin) ---------------------------

app.MapPost("/admin/login", async (HttpContext ctx, IConfiguration config, [Microsoft.AspNetCore.Mvc.FromForm] string key) =>
{
    var expected = config["Admin:Key"];
    var ok = !string.IsNullOrEmpty(expected)
             && CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(key ?? ""), Encoding.UTF8.GetBytes(expected));
    if (!ok) return Results.Redirect("/admin/login?error=1");

    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "admin")],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(new ClaimsPrincipal(identity));
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapPost("/admin/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.Redirect("/");
});

// --- the registry the plugin fetches --------------------------------------

app.MapGet("/api/v1/registry.json", async (HttpContext ctx, AppDbContext db) =>
{
    var designs = await db.Designs
        .AsNoTracking()
        // Community designs only: a built-in is already installed, has no
        // mirrored file behind it, and the seeded rows carry a random hash, so
        // listing one would give the plugin an entry that cannot pass the very
        // check that is meant to protect the user.
        .Where(d => d.Status == DesignStatus.Approved && d.LiveVersion != null
                 && d.LiveVersion.ReviewedBy == RegistrySync.SyncActor)
        .Include(d => d.LiveVersion)
        .OrderByDescending(d => d.Likes)
        .ThenBy(d => d.PublicId)
        .ToListAsync();

    // Absolute, because the plugin fetches these with curl and has no page to
    // resolve a relative path against.
    var origin = $"{ctx.Request.Scheme}://{ctx.Request.Host}";

    // A row this cannot read is skipped, not thrown on. This endpoint is the
    // plugin's only source of designs, and one malformed FilesJson taking the
    // whole registry down with a 500 would take every design away from every
    // user -- which is exactly what a row written by an older version of the
    // site did, because it records the filename under "path" and not "file".
    static string? FileName(JsonElement f)
    {
        if (f.ValueKind != JsonValueKind.Object) return null;
        if (f.TryGetProperty("file", out var n) && n.GetString() is { Length: > 0 } s1)
            return s1;
        // Older rows carry only a repo-relative path.
        if (f.TryGetProperty("path", out var p) && p.GetString() is { Length: > 0 } s2)
            return s2.Split('/').Last();
        return null;
    }

    var entries = designs.Select(d =>
    {
        JsonElement files;
        try { files = JsonSerializer.Deserialize<JsonElement>(d.LiveVersion!.FilesJson); }
        catch { return null; }

        JsonElement qmlFile = default, previewFile = default;
        if (files.ValueKind == JsonValueKind.Array)
            foreach (var f in files.EnumerateArray())
            {
                var kind = f.TryGetProperty("kind", out var k) ? k.GetString() : null;
                if (kind == "preview") previewFile = f; else qmlFile = f;
            }
        var first = qmlFile;
        var name = FileName(first);
        if (name is null) return null;   // nothing installable: leave it out
        var previewName = FileName(previewFile);

        var sha = first.TryGetProperty("sha256", out var sh) ? sh.GetString() : null;
        var size = first.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var bytes)
            ? bytes : 0L;

        return (object?)new
        {
            id = d.PublicId,
            name = d.Name,
            author = d.OwnerLogin,
            description = d.Description,
            tags = d.TagList,
            version = d.LiveVersion.Number,
            // The plugin wants one file it can name, hash and check, not a
            // list it has to guess its way through.
            qml = new
            {
                file = name,
                url = $"{origin}/designs/{d.PublicId}/{name}",
                sha256 = sha,
                size,
            },
            preview = previewName is null
                ? null : $"{origin}/designs/{d.PublicId}/{previewName}",
            likes = d.Likes,
            installs = d.Installs,
        };
    }).Where(e => e is not null).ToList();

    // Deliberately not part of what gets hashed: a timestamp in the payload
    // gives a different ETag on every request, and the conditional GET the
    // plugin makes every few minutes can then never come back 304. The
    // content is what identifies the content.
    var body = JsonSerializer.Serialize(new { schemaVersion = 1, designs = entries });
    var etag = '"' + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)))[..16] + '"';

    ctx.Response.Headers.CacheControl = "public, max-age=300, stale-while-revalidate=3600";
    ctx.Response.Headers.ETag = etag;
    if (ctx.Request.Headers.IfNoneMatch.ToString().Contains(etag))
        return Results.StatusCode(StatusCodes.Status304NotModified);

    var payload = JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        generated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        designs = entries,
    });
    return Results.Content(payload, "application/json");
});

// Run a sync now rather than waiting for the timer. Admin only: it reaches out
// to the designs repository and writes to wwwroot.
app.MapPost("/admin/sync", async (AppDbContext db, RegistrySync sync) =>
    Results.Text((await sync.RunAsync(db)).ToString()))
   .RequireAuthorization("Admin");

app.Run();
