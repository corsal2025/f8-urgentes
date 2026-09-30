using F8Urgentes.Data;
using F8Urgentes.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

public sealed class SinCarpetasModel(IUrgentRequestRepository repository) : PageModel
{
    public IReadOnlyList<UrgentRequest> SinCarpetasCases { get; private set; } = [];
    public string? Search { get; private set; }

    [TempData]
    public string? Message { get; set; }

    public void OnGet(string? search)
    {
        Search = search;
        var all = repository.GetAll().Where(r => r.SinCarpeta);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            var qClean = q.Replace(".", "").Replace("-", "").ToUpperInvariant();
            all = all.Where(r =>
                (!string.IsNullOrEmpty(r.NombreCompleto) && r.NombreCompleto.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.Rut) && r.Rut.Replace(".", "").Replace("-", "").ToUpperInvariant().Contains(qClean)) ||
                (!string.IsNullOrEmpty(r.CodigoF8) && r.CodigoF8.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        SinCarpetasCases = all.OrderByDescending(r => r.Id).ToList();
    }

    public IActionResult OnPostRevertSinCarpeta(long id)
    {
        repository.SetSinCarpeta(id, false);
        Message = "Caso revertido y devuelto a la lista de solicitudes activas.";
        return RedirectToPage(new { search = Search });
    }

    public IActionResult OnPostSendToCaja(long id)
    {
        repository.SendToCaja(id, DateTimeOffset.UtcNow);
        Message = "Caso enviado a la cola de Caja.";
        return RedirectToPage(new { search = Search });
    }

    public IActionResult OnPostDelete(long id)
    {
        repository.Delete(id);
        Message = "Solicitud eliminada.";
        return RedirectToPage(new { search = Search });
    }
}
