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

builder.Services.AddSingleton<IUrgentRequestRepository>(_ =>
    new UrgentRequestRepository($"Data Source={f8Options.SqliteDbPath}"));

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
