using F8Urgentes.Data;
using F8Urgentes.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

public sealed class RecuperarModel(IUrgentRequestRepository repository) : PageModel
{
    [BindProperty] public string? Username { get; set; }
    [BindProperty] public string? RespuestaSecreta { get; set; }
    [BindProperty] public string? NuevaPassword { get; set; }
    [BindProperty] public string? ConfirmarPassword { get; set; }
    public string? PreguntaSecreta { get; private set; }
    public string? Message { get; private set; }

    public void OnGet(string? username)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            Username = username.Trim();
            var user = repository.FindUserByUsername(Username);
            PreguntaSecreta = user?.PreguntaSecreta;
        }
    }

    public IActionResult OnPost()
    {
        ModelState.Clear();
        Username = Username?.Trim();

        if (string.IsNullOrWhiteSpace(Username))
        {
            ModelState.AddModelError(string.Empty, "Ingresa tu usuario.");
            return Page();
        }

        var user = repository.FindUserByUsername(Username);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Usuario no encontrado.");
            return Page();
        }

        PreguntaSecreta = user.PreguntaSecreta;

        if (!string.IsNullOrEmpty(user.RespuestaHash))
        {
            if (string.IsNullOrWhiteSpace(RespuestaSecreta) ||
                !PasswordHasher.Verify(RespuestaSecreta.Trim().ToLowerInvariant(), user.RespuestaHash))
            {
                ModelState.AddModelError(string.Empty, "La respuesta a la pregunta secreta es incorrecta.");
                return Page();
            }
        }

        if (string.IsNullOrEmpty(NuevaPassword) || NuevaPassword.Length < 8)
        {
            ModelState.AddModelError(string.Empty, "La nueva contraseña debe tener al menos 8 caracteres.");
            return Page();
        }

        if (NuevaPassword != ConfirmarPassword)
        {
            ModelState.AddModelError(string.Empty, "Las contraseñas no coinciden.");
            return Page();
        }

        repository.UpdateUserPassword(user.Id, PasswordHasher.Hash(NuevaPassword));
        Message = "Contraseña actualizada correctamente.";
        Username = null;
        RespuestaSecreta = null;
        NuevaPassword = null;
        ConfirmarPassword = null;
        return Page();
    }
}
