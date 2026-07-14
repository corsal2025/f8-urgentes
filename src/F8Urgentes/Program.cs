using F8Urgentes.Configuration;
using F8Urgentes.Data;

// ContentRootPath pinned to the exe's own folder so every relative path in config
// (SqliteDbPath, ExcelSourcePath) resolves the same way regardless of how the app
// is launched, mirroring the reference project's pattern.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

var f8Options = builder.Configuration.GetSection(F8Options.SectionName).Get<F8Options>() ?? new F8Options();
builder.Services.AddSingleton(f8Options);

// Microsoft.Data.Sqlite resolves relative connection-string paths against
// Environment.CurrentDirectory, NOT AppContext.BaseDirectory/ContentRootPath — so a
// bare relative SqliteDbPath silently follows wherever the process happens to be
// launched from (e.g. `dotnet run` sets CWD to the project folder). Resolve to an
// absolute path explicitly so the DB always lands in the same place regardless of
// launch method, matching the reference project's stated intent.
var sqliteDbPath = Path.IsPathRooted(f8Options.SqliteDbPath)
    ? f8Options.SqliteDbPath
    : Path.Combine(AppContext.BaseDirectory, f8Options.SqliteDbPath);
Directory.CreateDirectory(Path.GetDirectoryName(sqliteDbPath)!);

builder.Services.AddSingleton<IUrgentRequestRepository>(_ =>
    new UrgentRequestRepository($"Data Source={sqliteDbPath}"));

builder.Services.AddRazorPages(options => options.RootDirectory = "/Dashboard/Pages");

var app = builder.Build();

app.Services.GetRequiredService<IUrgentRequestRepository>().EnsureSchema();

// One-time historical import, run instead of starting the host (reference precedent: --add-user/--smoke-test).
var importArgs = ImportCliArgs.TryParseImportArgs(args);
if (importArgs is not null)
{
    var repository = app.Services.GetRequiredService<IUrgentRequestRepository>();
    var path = importArgs.Value.Path ?? f8Options.ExcelSourcePath
        ?? throw new InvalidOperationException("No import path given and F8:ExcelSourcePath is not configured.");

    var result = F8Urgentes.Import.ExcelUrgentImporter.Import(path, repository, importArgs.Value.Force);
    Console.WriteLine(result);
    return;
}

app.UseStaticFiles();
app.MapRazorPages();

app.Run();
