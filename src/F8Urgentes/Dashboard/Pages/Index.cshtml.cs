using F8Urgentes.Data;
using F8Urgentes.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

public sealed class IndexModel(IUrgentRequestRepository repository) : PageModel
{
    private const string EstadoActualSubida = "SUBIDA A CONASET";

    public IReadOnlyList<UrgentRequest> Requests { get; private set; } = [];
    public string? Month { get; private set; }
    public string? Estado { get; private set; }
    public string? EstadoActual { get; private set; }
    public bool? Flagged { get; private set; }
    public string? Search { get; private set; }
    public string? Tab { get; private set; }
    public int FlaggedCount { get; private set; }

    public void OnGet(string? month, string? estado, string? estadoActual, bool? flagged, string? search, string? tab = null)
    {
        Month = month;
        Estado = estado;
        EstadoActual = estadoActual;
        Flagged = flagged;
        Search = search;
        Tab = tab;

        var allMatching = repository.Query(new UrgentRequestFilter(month, estado, estadoActual, flagged), search);

        Requests = tab switch
        {
            "Pendientes" => allMatching.Where(r => r.EstadoActual != EstadoActualSubida).ToList(),
            "Subidas" => allMatching.Where(r => r.EstadoActual == EstadoActualSubida).ToList(),
            _ => allMatching,
        };

        FlaggedCount = repository.GetFlagged().Count;
    }

    public IActionResult OnPostSetEstado(long id, string estado)
    {
        var request = repository.FindById(id);
        if (request is not null)
        {
            request.Estado = EstadoCatalog.Canonicalize(estado);
            repository.Update(request);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostSetEstadoActual(long id, string estadoActual)
    {
        var request = repository.FindById(id);
        if (request is not null)
        {
            request.EstadoActual = EstadoCatalog.Canonicalize(estadoActual);
            repository.Update(request);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostSetPersonData(long id, string nombreCompleto, string rut)
    {
        var request = repository.FindById(id);
        if (request is not null)
        {
            request.NombreCompleto = nombreCompleto;
            request.Rut = rut;
            repository.Update(request);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostMarkUploaded(long id)
    {
        var request = repository.FindById(id);
        if (request is not null)
        {
            request.EstadoActual = EstadoActualSubida;
            request.FechaDeSubida = DateOnly.FromDateTime(DateTime.Today);
            repository.Update(request);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostDeleteCase(long id)
    {
        repository.Delete(id);
        return RedirectToPage();
    }

    public IActionResult OnPostAddManualCases(List<string> nombreCompleto, List<string> rut, List<string> estado)
    {
        var count = Math.Min(nombreCompleto.Count, Math.Min(rut.Count, estado.Count));
        for (var i = 0; i < count; i++)
        {
            var nombre = nombreCompleto[i];
            var rutValue = rut[i];
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(rutValue))
            {
                continue;
            }

            repository.Insert(new UrgentRequest
            {
                NombreCompleto = nombre,
                Rut = rutValue,
                Estado = string.IsNullOrWhiteSpace(estado[i]) ? null : EstadoCatalog.Canonicalize(estado[i]),
                FechaPeticion = DateOnly.FromDateTime(DateTime.Today),
                Origin = "Web",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        return RedirectToPage();
    }

    public int DiasHabilesRestantes(UrgentRequest request)
    {
        if (request.FechaPeticion is not { } fechaPeticion)
        {
            return 0;
        }

        var deadline = DeadlineCalculator.AddBusinessDays(fechaPeticion, 15);
        return DeadlineCalculator.BusinessDaysRemaining(DateOnly.FromDateTime(DateTime.Today), deadline);
    }
}
