using F8Urgentes.Configuration;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.ConfigureServices();

var app = builder.Build();

app.ConfigurePipeline();

app.Run();
