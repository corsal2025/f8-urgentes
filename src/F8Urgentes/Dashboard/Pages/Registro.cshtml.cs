using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using F8Urgentes.Data;
using F8Urgentes.Domain;
using System.Security.Cryptography;
using System.Text;

namespace F8Urgentes.Dashboard.Pages;

public class RegistroModel : PageModel
{
    private readonly IUrgentRequestRepository _repo;

    public RegistroModel(IUrgentRequestRepository repo)
    {
        _repo = repo;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string PreguntaSecreta { get; set; } = string.Empty;

    [BindProperty]
    public string RespuestaSecreta { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError(string.Empty, "Debe ingresar usuario y contraseña.");
            return Page();
        }

        var existingUser = _repo.FindUserByUsername(Username);
        if (existingUser != null)
        {
            ModelState.AddModelError(string.Empty, "El nombre de usuario ya está en uso.");
            return Page();
        }

        using var sha256 = SHA256.Create();
        
        // Hash Password
        var pwdBytes = Encoding.UTF8.GetBytes(Password);
        var pwdHash = Convert.ToBase64String(sha256.ComputeHash(pwdBytes));

        // Hash Respuesta Secreta
        var respBytes = Encoding.UTF8.GetBytes(RespuestaSecreta.Trim().ToLower()); // normalized
        var respHash = Convert.ToBase64String(sha256.ComputeHash(respBytes));

        var nuevoUsuario = new Usuario
        {
            Username = Username,
            PasswordHash = pwdHash,
            PreguntaSecreta = PreguntaSecreta,
            RespuestaHash = respHash
        };

        _repo.InsertUser(nuevoUsuario);

        // Redirect to Login
        return RedirectToPage("/Login");
    }
}
