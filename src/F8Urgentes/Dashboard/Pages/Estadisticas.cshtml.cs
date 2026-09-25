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

    public int PendientesVencidos { get; private set; }

    // Physical folder classification (Caja module): every case falls in exactly one bucket.
    public IReadOnlyList<(string Label, int Count)> ClasificacionCarpetas { get; private set; } = [];
    public int CajasCerradas { get; private set; }
    public IReadOnlyList<(string Code, int Count)> CarpetasPorCaja { get; private set; } = [];
    public double? MedianaDiasConfirmacion { get; private set; }
    public double? P90DiasConfirmacion { get; private set; }
    public IReadOnlyList<(string MonthLabel, int Ingresos, int Subidas)> MensualIngresosSubidas { get; private set; } = [];
    public IReadOnlyList<(string Label, int Count)> IngresosPorDiaSemana { get; private set; } = [];
    public IReadOnlyList<(string Label, int Subidas, int Pendientes)> SectorOficinaEstado { get; private set; } = [];
    public IReadOnlyList<(string MonthLabel, int Dentro, int Fuera)> CumplimientoPorMes { get; private set; } = [];

    public void OnGet() => Load(DateOnly.FromDateTime(DateTime.Today));

    // Split from OnGet so tests can pin "today" (aging and the 12-month window depend on it).
    public void Load(DateOnly today)
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

        var diasPendientes = all
            .Where(r => r.EstadoActual != EstadoActualSubida && r.FechaPeticion is not null)
            .Select(r => DeadlineCalculator.BusinessDaysRemaining(r.FechaPeticion!.Value, today))
            .ToList();
        EnvejecimientoPendientes = BucketCounts(diasPendientes, PendientesBuckets)
            .Select(b => (b.BucketLabel, b.Count, Alert: b.BucketLabel == "+15 días hábiles"))
            .ToList();
        PendientesVencidos = diasPendientes.Count(d => d > 15);

        var diasOrdenados = confirmados.Select(c => (double)c.Dias).Order().ToList();
        MedianaDiasConfirmacion = Percentile(diasOrdenados, 0.5);
        P90DiasConfirmacion = Percentile(diasOrdenados, 0.9);

        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        var months = Enumerable.Range(0, 12).Select(firstMonth.AddMonths).ToList();
        MensualIngresosSubidas = months
            .Select(m => (
                MonthLabel: MonthLabel(m),
                Ingresos: all.Count(r => SameMonth(r.FechaPeticion, m)),
                Subidas: all.Count(r => r.EstadoActual == EstadoActualSubida && SameMonth(r.FechaDeSubida, m))))
            .ToList();
        CumplimientoPorMes = months
            .Select(m =>
            {
                var delMes = confirmados.Where(c => SameMonth(c.FechaDeSubida, m)).ToList();
                var dentro = delMes.Count(c => c.FechaDeSubida!.Value <= DeadlineCalculator.AddBusinessDays(c.FechaPeticion!.Value, 15));
                return (MonthLabel: MonthLabel(m), Dentro: dentro, Fuera: delMes.Count - dentro);
            })
            .ToList();

        DayOfWeek[] dias = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
        string[] diasLabel = ["Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom"];
        var porDia = dias
            .Select((d, i) => (Label: diasLabel[i], Count: all.Count(r => r.FechaPeticion?.DayOfWeek == d)))
            .ToList();
        // Weekends only shown when they actually have cases — normally empty and just noise.
        IngresosPorDiaSemana = porDia.Where((d, i) => i < 5 || d.Count > 0).ToList();

        ClasificacionCarpetas =
        [
            ("En caja cerrada", all.Count(r => r.CajaBoxId is not null)),
            ("En cola de caja", all.Count(r => r.CajaTransferredAt is not null && r.CajaBoxId is null)),
            ("Sin carpeta", all.Count(r => r.SinCarpeta && r.CajaTransferredAt is null)),
            ("Sin clasificar", all.Count(r => !r.SinCarpeta && r.CajaTransferredAt is null)),
        ];
        var boxes = repository.GetBoxes();
        CajasCerradas = boxes.Count;
        CarpetasPorCaja = boxes
            .OrderBy(b => b.Number)
            .Select(b => (Code: b.Code, Count: all.Count(r => r.CajaBoxId == b.Id)))
            .ToList();

        SectorOficinaEstado = all
            .GroupBy(r => string.IsNullOrWhiteSpace(r.MatrizSector) ? "(sin sector)" : r.MatrizSector!)
            .Select(g => (
                Label: g.Key,
                Subidas: g.Count(r => r.EstadoActual == EstadoActualSubida),
                Pendientes: g.Count(r => r.EstadoActual != EstadoActualSubida)))
            .OrderByDescending(x => x.Subidas + x.Pendientes)
            .ToList();
    }

    private static readonly string[] MesesCortos = ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];

    private static string MonthLabel(DateOnly m) => $"{MesesCortos[m.Month - 1]} {m.Year % 100:00}";

    private static bool SameMonth(DateOnly? date, DateOnly month) =>
        date is { } d && d.Year == month.Year && d.Month == month.Month;

    // Linear-interpolated percentile over an already-sorted list.
    private static double? Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) return null;
        var pos = (sorted.Count - 1) * p;
        var lo = (int)Math.Floor(pos);
        var hi = (int)Math.Ceiling(pos);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
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
