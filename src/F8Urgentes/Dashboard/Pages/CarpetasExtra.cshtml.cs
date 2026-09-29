using System.ComponentModel.DataAnnotations;
using F8Urgentes.Data;
using F8Urgentes.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

/// <summary>
/// Sección de "carpetas extras": solicitudes que no entraron en la importación histórica
/// porque el sistema aún no existía (penúltimas carpetas antiguas) o que se solicitan
/// esporádicamente fuera del flujo del Excel diario. Comparten la misma tabla UrgentRequest
/// con Origen = "Extra", por lo que también aparecen en Index/Estadisticas, pero se
/// gestiona aquí de forma independiente para no mezclarlas con el flujo de importación.
/// </summary>
public sealed class CarpetasExtraModel(IUrgentRequestRepository repository) : PageModel
{
    private const string OrigenExtra = "Extra";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<UrgentRequest> ExtraCases { get; private set; } = [];

    public void OnGet()
    {
        LoadList();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            LoadList();
            return Page();
        }

        if (!Rut.TryParse(Input.Rut, out var parsedRut) || !parsedRut.IsValid)
        {
            ModelState.AddModelError("Input.Rut", "RUT inválido: verifique el dígito verificador.");
            LoadList();
            return Page();
        }

        var nombreCompleto = ResolveNombreCompleto();
        var request = new UrgentRequest
        {
            NombreCompleto = nombreCompleto,
            Nombres = Input.Nombres,
            Apellidos = Input.Apellidos,
            Rut = parsedRut.ToString(),
            RutRaw = Input.Rut,
            CodigoF8 = string.IsNullOrWhiteSpace(Input.CodigoF8) ? null : Input.CodigoF8.Trim(),
            FechaPenultimaCarpeta = Input.fechaPenultimaCarpeta,
            Estado = EstadoCatalog.NormalizeForPersistence(Input.Estado, isEstado: true),
            EstadoActual = EstadoCatalog.NormalizeForPersistence(Input.EstadoActual, isEstado: false),
            FechaPeticion = Input.fechaPeticion ?? DateOnly.FromDateTime(DateTime.Now.Date),
            Origin = OrigenExtra,
            CreatedAt = DateTimeOffset.UtcNow,
            NeedsReview = EstadoCatalog.IsUnknownEstado(Input.Estado) || EstadoCatalog.IsUnknownEstadoActual(Input.EstadoActual),
        };

        var id = repository.Insert(request);
        if (EstadoCatalog.IsUnknownEstado(Input.Estado))
        {
            repository.AddFlag(id, "Estado", ImportFlag.ReasonCodes.UnknownEstado, Input.Estado);
        }
        if (EstadoCatalog.IsUnknownEstadoActual(Input.EstadoActual))
        {
            repository.AddFlag(id, "EstadoActual", ImportFlag.ReasonCodes.UnknownEstadoActual, Input.EstadoActual);
        }

        // Limpiar el formulario para el siguiente ingreso
        Input = new InputModel();
        ModelState.Clear();

        LoadList();
        return Page();
    }

    public IActionResult OnPostDelete(long id)
    {
        repository.Delete(id);
        ModelState.AddModelError(string.Empty, "Caso extra eliminado.");
        LoadList();
        return Page();
    }

    /// <summary>Envía el caso a la cola de Caja (mismo flujo que en Index).</summary>
    public IActionResult OnPostSendToCaja(long id)
    {
        repository.SendToCaja(id, DateTimeOffset.UtcNow);
        ModelState.AddModelError(string.Empty, "Caso enviado a la cola de Caja.");
        LoadList();
        return Page();
    }

    private void LoadList()
    {
        // Mismo criterio que Index: un caso enviado a Caja deja de listarse aquí y pasa a
        // /Caja (cola abierta o caja cerrada), que es donde se sigue su ciclo físico.
        ExtraCases = repository.GetAll()
            .Where(r => r.Origin == OrigenExtra && r.CajaTransferredAt is null)
            .OrderBy(r => r.Id)
            .ToList();
    }

    private string ResolveNombreCompleto()
    {
        if (!string.IsNullOrWhiteSpace(Input.nombreCompleto))
        {
            return Input.nombreCompleto.Trim();
        }
        return string.Join(" ", new[] { Input.Nombres, Input.Apellidos }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public sealed class InputModel
    {
        [Required]
        [Display(Name = "Nombre completo")]
        public string? nombreCompleto { get; set; }

        public string? Nombres { get; set; }
        public string? Apellidos { get; set; }

        [Required]
        [Display(Name = "RUT")]
        public string? Rut { get; set; }

        [Display(Name = "Código F8")]
        public string? CodigoF8 { get; set; }

        [Display(Name = "Fecha petición")]
        public DateOnly? fechaPeticion { get; set; }

        [Display(Name = "Fecha penúltima carpeta")]
        public DateOnly? fechaPenultimaCarpeta { get; set; }

        [Display(Name = "Estado")]
        public string? Estado { get; set; }

        [Display(Name = "Estado actual")]
        public string? EstadoActual { get; set; }
    }
}