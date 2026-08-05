using F8Urgentes.Configuration;
using F8Urgentes.Data;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.ConfigureServices();

var app = builder.Build();

var importArgs = ImportCliArgs.TryParseImportArgs(args);
if (importArgs is not null)
{
    var repository = app.Services.GetRequiredService<IUrgentRequestRepository>();
    var f8Options = app.Services.GetRequiredService<F8Options>();
    var path = importArgs.Value.Path ?? f8Options.ExcelSourcePath
        ?? throw new InvalidOperationException("No import path given and F8:ExcelSourcePath is not configured.");

    var result = F8Urgentes.Import.ExcelUrgentImporter.Import(path, repository, importArgs.Value.Force);
    Console.WriteLine(result);
    return;
}

app.ConfigurePipeline();

app.Run();
