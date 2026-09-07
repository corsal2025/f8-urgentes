using F8Urgentes.Data;
using F8Urgentes.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

/// <summary>Monthly print batch: completed cases (SUBIDA A CONASET) whose FechaDeSubida falls
/// in the selected month — that's the date actually populated when a case is marked uploaded,
/// unlike FechaPenultimaCarpeta which is often blank. Marking a batch as printed sets
/// ImpresoMensualAt so the end-of-month reminder on the dashboard knows the previous month is
/// covered.
///
/// General rule: a person is NEVER listed here without a penultimate folder date. Cases with
/// FechaPenultimaCarpeta == null are excluded from the batch (list, print, and MarkPrinted).</summary>
public sealed class ImpresionMensualModel(IUrgentRequestRepository repository) : PageModel
{
    private const string EstadoActualSubida = "SUBIDA A CONASET";

    public string Month { get; private set; } = string.Empty;
    public IReadOnlyList<UrgentRequest> Cases { get; private set; } = [];
    public string Folio { get; private set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; private set; }

    public void OnGet(string? month)
    {
        Month = string.IsNullOrWhiteSpace(month)
            ? PreviousMonthKey(DateOnly.FromDateTime(DateTime.Today))
            : month;

        Cases = ForMonth(Month).OrderBy(c => c.FechaDeSubida).ThenBy(c => c.Id).ToList();
        GeneratedAt = DateTimeOffset.Now;
        Folio = $"F8-Mensual-{Month.Replace("-", "")}-{GeneratedAt:HHmm}";
    }

    public IActionResult OnPostMarkPrinted(string month)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in ForMonth(month))
        {
            repository.SetImpresoMensual(item.Id, now);
        }
        return RedirectToPage(new { month });
    }

    private IEnumerable<UrgentRequest> ForMonth(string month) =>
        repository.GetAll().Where(c => BelongsToMonthlyBatch(c, month));

    /// <summary>
    /// Single source of truth for "is this case part of <paramref name="month"/>'s print batch".
    /// Shared with the dashboard's end-of-month reminder (IndexModel.ShouldRemindMonthlyPrint) so
    /// the banner can't outlive the batch — a case that isn't in the batch (no penultimate-folder
    /// date, wrong month, still in progress) must not keep the reminder alive either.
    /// </summary>
    public static bool BelongsToMonthlyBatch(UrgentRequest c, string month) =>
        c.EstadoActual == EstadoActualSubida &&
        c.FechaPenultimaCarpeta is not null &&
        c.FechaDeSubida is not null &&
        c.FechaDeSubida.Value.ToString("yyyy-MM") == month;

    public static string PreviousMonthKey(DateOnly today)
    {
        var previous = today.AddMonths(-1);
        return $"{previous.Year:D4}-{previous.Month:D2}";
    }
}
