using F8Urgentes.Data;
using F8Urgentes.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

public sealed class IndexModel(IUrgentRequestRepository repository) : PageModel
{
    public IReadOnlyList<UrgentRequest> Requests { get; private set; } = [];
    public string? Month { get; private set; }
    public string? Estado { get; private set; }
    public string? EstadoActual { get; private set; }
    public bool? Flagged { get; private set; }
    public string? Search { get; private set; }
    public int FlaggedCount { get; private set; }

    public void OnGet(string? month, string? estado, string? estadoActual, bool? flagged, string? search)
    {
        Month = month;
        Estado = estado;
        EstadoActual = estadoActual;
        Flagged = flagged;
        Search = search;

        Requests = repository.Query(new UrgentRequestFilter(month, estado, estadoActual, flagged), search);
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
}
