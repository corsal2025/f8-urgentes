using F8Urgentes.Data;
using F8Urgentes.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

/// <summary>Print queue for a single sector: cases marked or pendiente-carpeta, not yet included
/// in a printed batch (SectorPdfGeneratedAt is null). Ported from OutlookComunaRouter's
/// SectorF8Model, adapted to UrgentRequest/IUrgentRequestRepository.</summary>
public sealed class SectorF8Model(IUrgentRequestRepository repository) : PageModel
{
    public FolderSector SelectedSector { get; private set; }
    public IReadOnlyList<UrgentRequest> Cases { get; private set; } = [];

    public void OnGet(FolderSector sector)
    {
        SelectedSector = sector;
        Cases = Pending(sector).OrderBy(c => c.MarkedAt).ToList();
    }

    public IActionResult OnPostMarkPrinted(FolderSector sector)
    {
        MarkAllVisibleAsPrinted(sector);
        return new EmptyResult();
    }

    public IActionResult OnPostRemoveOne(long id, FolderSector sector)
    {
        repository.SetSectorPdfGenerated(id, DateTimeOffset.UtcNow);
        return RedirectToPage(new { sector });
    }

    public IActionResult OnPostClearAll(FolderSector sector)
    {
        MarkAllVisibleAsPrinted(sector);
        return RedirectToPage(new { sector });
    }

    private void MarkAllVisibleAsPrinted(FolderSector sector)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in Pending(sector))
        {
            repository.SetSectorPdfGenerated(item.Id, now);
        }
    }

    private IEnumerable<UrgentRequest> Pending(FolderSector sector) =>
        // Caja cases (queued or boxed) are excluded from the print queue: their physical folder is
        // already being handled through the Caja workflow, not the sector-print workflow.
        repository.GetAll().Where(c => c.Sector == sector && (c.Marked || c.PendienteCarpeta) && c.SectorPdfGeneratedAt is null && c.CajaTransferredAt is null);
}
