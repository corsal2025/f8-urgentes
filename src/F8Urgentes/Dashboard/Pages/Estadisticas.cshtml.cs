using F8Urgentes.Data;
using F8Urgentes.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

public sealed class EstadisticasModel(IUrgentRequestRepository repository) : PageModel
{
    private const string EstadoActualSubida = "SUBIDA A CONASET";

    public int Total { get; private set; }
    public int Subidas { get; private set; }
    public int Pendientes { get; private set; }
    public int NeedsReview { get; private set; }
    public int Marcados { get; private set; }
    public int PendienteCarpeta { get; private set; }
    public IReadOnlyList<(string Label, int Count)> PorEstado { get; private set; } = [];
    public IReadOnlyList<(string Label, int Count)> PorEstadoActual { get; private set; } = [];
    public IReadOnlyList<(string Label, int Count)> PorSector { get; private set; } = [];
    public IReadOnlyList<(string Label, int Count)> PorSectorOficina { get; private set; } = [];
    public IReadOnlyList<(string Label, int Count)> PorOrigen { get; private set; } = [];
    public IReadOnlyList<(string WeekLabel, int Count)> IngresosPorSemana { get; private set; } = [];
    public double? TiempoPromedioConfirmacionDias { get; private set; }

    // Legal deadline is 15 business days from FechaPeticion (DeadlineCalculator, same math as the
    // per-row deadline chip on Index). Only judged over cases that are actually done — a still-open
    // case isn't "on time" or "late" yet, that's what EnvejecimientoPendientes is for.
    public int PlazoDentro { get; private set; }
    public int PlazoFuera { get; private set; }

    public IReadOnlyList<(string BucketLabel, int Count)> HistogramaDiasConfirmacion { get; private set; } = [];
    public IReadOnlyList<(string BucketLabel, int Count, bool Alert)> EnvejecimientoPendientes { get; private set; } = [];

    public void OnGet()
    {
        var all = repository.GetAll();

        Total = all.Count;
        Subidas = all.Count(r => r.EstadoActual == EstadoActualSubida);
        Pendientes = Total - Subidas;
        NeedsReview = all.Count(r => r.NeedsReview);
        Marcados = all.Count(r => r.Marked);
        PendienteCarpeta = all.Count(r => r.PendienteCarpeta);

        PorEstado = CountBy(all, r => string.IsNullOrWhiteSpace(r.Estado) ? "(sin estado)" : r.Estado!);

        PorEstadoActual = CountBy(all, r =>
            string.IsNullOrWhiteSpace(r.EstadoActual) ? "PENDIENTE" : r.EstadoActual!);

        PorSector = CountBy(all, r => r.Sector switch
        {
            FolderSector.Archivo => "Archivo",
            FolderSector.Oficina43 => "Oficina 43",
            _ => "(sin fecha penúltima carpeta)",
        });

        PorSectorOficina = CountBy(all, r =>
            string.IsNullOrWhiteSpace(r.MatrizSector) ? "(sin sector)" : r.MatrizSector!);

        PorOrigen = CountBy(all, r => r.Origin switch
        {
            "Matriz" => "Matriz (automatico)",
            "Import" => "Importado (Excel historico)",
            "Web" => "Manual (dashboard)",
            var other => other,
        });

        IngresosPorSemana = all
            .Where(r => r.FechaPeticion is not null)
            .GroupBy(r => StartOfWeek(r.FechaPeticion!.Value))
            .OrderBy(g => g.Key)
            .Select(g => (WeekLabel: g.Key.ToString("dd/MM"), Count: g.Count()))
            .ToList();

        var confirmados = all
            .Where(r => r.FechaPeticion is not null && r.FechaDeSubida is not null && r.FechaDeSubida >= r.FechaPeticion)
            .Select(r => (Dias: r.FechaDeSubida!.Value.DayNumber - r.FechaPeticion!.Value.DayNumber, r.FechaPeticion, r.FechaDeSubida))
            .ToList();
        TiempoPromedioConfirmacionDias = confirmados.Count > 0 ? confirmados.Average(c => c.Dias) : null;

        HistogramaDiasConfirmacion = BucketCounts(confirmados.Select(c => c.Dias), DiasBuckets);

        foreach (var c in confirmados)
        {
            var deadline = DeadlineCalculator.AddBusinessDays(c.FechaPeticion!.Value, 15);
            if (c.FechaDeSubida!.Value <= deadline) PlazoDentro++; else PlazoFuera++;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var diasPendientes = all
            .Where(r => r.EstadoActual != EstadoActualSubida && r.FechaPeticion is not null)
            .Select(r => DeadlineCalculator.BusinessDaysRemaining(r.FechaPeticion!.Value, today))
            .ToList();
        EnvejecimientoPendientes = BucketCounts(diasPendientes, PendientesBuckets)
            .Select(b => (b.BucketLabel, b.Count, Alert: b.BucketLabel == "+15 días hábiles"))
            .ToList();
    }

    // (upper bound inclusive, label) — last entry's bound is ignored, it catches everything above.
    private static readonly (int Upper, string Label)[] DiasBuckets =
    [
        (5, "0-5 días"), (10, "6-10 días"), (15, "11-15 días"), (20, "16-20 días"), (int.MaxValue, "21+ días"),
    ];

    private static readonly (int Upper, string Label)[] PendientesBuckets =
    [
        (5, "0-5 días hábiles"), (10, "6-10 días hábiles"), (15, "11-15 días hábiles"), (int.MaxValue, "+15 días hábiles"),
    ];

    private static IReadOnlyList<(string BucketLabel, int Count)> BucketCounts(IEnumerable<int> values, (int Upper, string Label)[] buckets)
    {
        var counts = new int[buckets.Length];
        foreach (var v in values)
        {
            var i = Array.FindIndex(buckets, b => v <= b.Upper);
            counts[i < 0 ? buckets.Length - 1 : i]++;
        }
        return buckets.Select((b, i) => (b.Label, counts[i])).ToList();
    }

    // Monday-start ISO week bucket for the "ingresos por semana" trend.
    private static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek) - 1;
        return date.AddDays(-diff);
    }

    private static IReadOnlyList<(string Label, int Count)> CountBy(
        IReadOnlyList<UrgentRequest> requests, Func<UrgentRequest, string> selector) =>
        requests
            .GroupBy(selector)
            .Select(g => (Label: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
}
