using F8Urgentes.Data;
using F8Urgentes.Domain;
using F8Urgentes.Services;
using Microsoft.AspNetCore.DataProtection;

namespace F8Urgentes.Configuration;

public static class HostingExtensions
{
    public static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<F8Options>()
            .Bind(builder.Configuration.GetSection(F8Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var f8Options = builder.Configuration.GetSection(F8Options.SectionName).Get<F8Options>() ?? new F8Options();
        builder.Services.AddSingleton(f8Options);

        var sqliteDbPath = Path.IsPathRooted(f8Options.SqliteDbPath)
            ? f8Options.SqliteDbPath
            : Path.Combine(AppContext.BaseDirectory, f8Options.SqliteDbPath);
        
        Directory.CreateDirectory(Path.GetDirectoryName(sqliteDbPath)!);

        builder.Services.AddSingleton<IUrgentRequestRepository>(_ =>
            new UrgentRequestRepository($"Data Source={sqliteDbPath}"));

        builder.Services.AddOptions<SmtpOptions>()
            .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations();
        builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetDirectoryName(sqliteDbPath)!, "keys")));

        builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.LogoutPath = "/Logout";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });

        builder.Services.AddRazorPages(options => 
        {
            options.RootDirectory = "/Dashboard/Pages";
            options.Conventions.AuthorizeFolder("/");
            options.Conventions.AllowAnonymousToPage("/Login");
            options.Conventions.AllowAnonymousToPage("/Recuperar");
            options.Conventions.AllowAnonymousToPage("/Registro");
        });

        return builder;
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.Services.GetRequiredService<IUrgentRequestRepository>().EnsureSchema();

        var options = app.Services.GetRequiredService<F8Options>();
        if (!string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            var repository = app.Services.GetRequiredService<IUrgentRequestRepository>();
            if (repository.FindUserByUsername(options.AdminUsername) is null)
            {
                repository.InsertUser(new Usuario
                {
                    Username = options.AdminUsername,
                    PasswordHash = PasswordHasher.Hash(options.AdminPassword),
                });
            }
        }

        app.UseStaticFiles();
        
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorPages();

        return app;
    }
}
