using System.Security.Claims;
using F8Urgentes.Configuration;
using F8Urgentes.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

public class LoginModel : PageModel
{
    private readonly F8Options _options;

    public LoginModel(F8Options options)
    {
        _options = options;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync([FromServices] F8Urgentes.Data.IUrgentRequestRepository repo)
    {
        var user = repo.FindUserByUsername(Username);
        bool isValid = false;

        // Verify password
        if (user != null)
        {
            if (user.Username == _options.AdminUsername &&
                !string.IsNullOrEmpty(_options.AdminPassword) &&
                Password == _options.AdminPassword)
            {
                isValid = true;
            }
            else
            {
                isValid = PasswordHasher.Verify(Password, user.PasswordHash);
                if (!isValid && VerifyLegacySha256(Password, user.PasswordHash))
                {
                    // Migrate accounts created by previous releases after successful login.
                    repo.UpdateUserPassword(user.Id, PasswordHasher.Hash(Password));
                    isValid = true;
                }
            }
        }

        if (isValid)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user!.Username),
                new Claim(ClaimTypes.Role, user.Username == _options.AdminUsername ? "Admin" : "User")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            return RedirectToPage("/Index");
        }

        ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
        return Page();
    }

    private static bool VerifyLegacySha256(string password, string storedHash)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var actual = Convert.ToBase64String(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(actual),
            System.Text.Encoding.UTF8.GetBytes(storedHash));
    }
}
