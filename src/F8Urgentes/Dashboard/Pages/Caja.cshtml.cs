using F8Urgentes.Data;
using F8Urgentes.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

/// <summary>Caja module: tracks which physical box each case's folder ends up packed into. A case
/// enters the open queue via Index's "Caja" button (IndexModel.OnPostSendToCaja) and disappears
/// from Casos until "Cerrar Caja" packs it into a numbered box, or it's removed back to Casos.
/// Adapted from CambioDeDomicilio's Caja page (see reference in the task) to F8Urgentes'
/// UrgentRequest/IUrgentRequestRepository — box codes here use the "-PUC" suffix instead of "-CD".</summary>
[IgnoreAntiforgeryToken]
public sealed class CajaModel(IUrgentRequestRepository repository) : PageModel
{
    public IReadOnlyList<UrgentRequest> Queue { get; private set; } = [];
    public IReadOnlyList<Box> ClosedBoxes { get; private set; } = [];
    public Dictionary<long, int> BoxCounts { get; private set; } = [];
    public int NextBoxNumber { get; private set; } = 1;

    /// <summary>Set when viewing a single closed box's printable detail (?boxId=N).</summary>
    public Box? SelectedBox { get; private set; }
    public IReadOnlyList<UrgentRequest> SelectedBoxCases { get; private set; } = [];

    [TempData]
    public string? Message { get; set; }

    public void OnGet(long? boxId)
    {
        Load(boxId);
    }

    /// <summary>Closes the current queue into a new box with the operator's manual code (e.g.
    /// A1-PUC), then goes straight to that box's printable detail.</summary>
    public IActionResult OnPostCerrarCaja([FromForm] string? boxNumber, [FromForm] string? boxCode)
    {
        var nextNum = repository.GetBoxes().Count > 0 ? repository.GetBoxes().Max(b => b.Number) + 1 : 1;
        var raw = !string.IsNullOrWhiteSpace(boxNumber) ? boxNumber : boxCode;
        var normalizedCode = FormatBoxCode(raw, nextNum);

        var box = repository.CloseBox(normalizedCode, DateTimeOffset.UtcNow);
        Message = $"Caja {box.Code} cerrada exitosamente.";
        return RedirectToPage(new { boxId = box.Id });
    }

    /// <summary>Reopens a closed box, sending all its cases back to the open queue.</summary>
    public IActionResult OnPostReopenBox([FromForm] long boxId)
    {
        var box = repository.FindBoxById(boxId);
        var code = box?.Code ?? $"#{boxId}";
        repository.ReopenBox(boxId);
        Message = $"Caja {code} reabierta con éxito. Sus casos volvieron a la cola para que puedas quitar los que sobren o corregirlos.";
        return RedirectToPage("/Caja", new { boxId = (long?)null });
    }

    /// <summary>Removes an individual case from an already closed box and returns it to Casos.</summary>
    public IActionResult OnPostRemoveFromClosedBox([FromForm] long id, [FromForm] long boxId)
    {
        repository.RemoveCaseFromClosedBox(id);
        Message = "Caso quitado de la caja y devuelto a Casos.";
        return RedirectToPage(new { boxId });
    }

    /// <summary>Removes a single case from the Caja queue and returns it to Casos.</summary>
    public IActionResult OnPostUndoSingle(long id)
    {
        repository.UndoCajaQueue(id);
        Message = "Caso quitado de la cola de caja y devuelto a Casos.";
        return RedirectToPage();
    }

    /// <summary>Removes multiple selected cases from the Caja queue and returns them to Casos.</summary>
    public IActionResult OnPostUndoBatch([FromForm] List<long> selectedIds)
    {
        if (selectedIds is { Count: > 0 })
        {
            foreach (var id in selectedIds)
            {
                repository.UndoCajaQueue(id);
            }
            Message = $"{selectedIds.Count} caso(s) quitados de la cola de caja y devueltos a Casos.";
        }
        return RedirectToPage();
    }

    public static string FormatBoxCode(string? boxNumberOrCode, int defaultNumber)
    {
        if (string.IsNullOrWhiteSpace(boxNumberOrCode))
            return $"A{defaultNumber}-PUC";

        var trimmed = boxNumberOrCode.Trim().ToUpperInvariant();
        if (trimmed.EndsWith("-PUC", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4].Trim();
        else if (trimmed.EndsWith("PUC", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^3].Trim();

        var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^([A-Z]*)\s*(\d+)$");
        if (match.Success)
        {
            var prefix = string.IsNullOrEmpty(match.Groups[1].Value) ? "A" : match.Groups[1].Value;
            var num = match.Groups[2].Value;
            return $"{prefix}{num}-PUC";
        }

        if (int.TryParse(trimmed, out var n))
            return $"A{n}-PUC";

        return $"{trimmed}-PUC";
    }

    private void Load(long? boxId)
    {
        Queue = repository.GetCajaQueue();
        ClosedBoxes = repository.GetBoxes();
        NextBoxNumber = ClosedBoxes.Count > 0 ? ClosedBoxes.Max(b => b.Number) + 1 : 1;
        BoxCounts = ClosedBoxes.ToDictionary(b => b.Id, b => repository.GetCasesByBoxId(b.Id).Count);

        if (boxId is { } id)
        {
            SelectedBox = repository.FindBoxById(id);
            SelectedBoxCases = SelectedBox is null ? [] : repository.GetCasesByBoxId(id);
        }
    }
}
