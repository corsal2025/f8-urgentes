var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddRazorPages(options => options.RootDirectory = "/Dashboard/Pages");

var app = builder.Build();

app.UseStaticFiles();
app.MapRazorPages();

app.Run();
