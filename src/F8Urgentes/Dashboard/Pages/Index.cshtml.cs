   using F8Urgentes.Configuration;
using F8Urgentes.Data;
using F8Urgentes.Domain;
using F8Urgentes.Matriz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

public sealed class IndexModel(IUrgentRequestRepository repository, F8Options? options = null) : PageModel
{
    private const string EstadoActualSubida = "SUBIDA A CONASET";
    private const string EstadoActualCertificado = "CREAR CERTIFICADO";

    [TempData]
    public string? SyncMessage { get; set; }

    public IReadOnlyList<UrgentRequest> Requests { get; private set; } = [];
    public IReadOnlyList<string> DuplicateRuts { get; private set; } = [];
    public bool? Flagged { get; private set; }
    public string? Search { get; private set; }
    public string? Tab { get; private set; }
    public int FlaggedCount { get; private set; }
    public int SinAgregarCount { get; private set; }

    public void OnGet(
        bool? flagged,
        string? search,
        string? tab = null)
    {
        Flagged = flagged;
        Search = search;
        Tab = tab;

        // Sin-carpeta cases stay here (grayed via row-confirmed) and are reversible from Index itself.
        var allMatching = repository.Query(new UrgentRequestFilter(Flagged: flagged), search)
            .Where(r => r.CajaTransferredAt is null)
            .ToList();

        var filtered = tab switch
        {
            "Pendientes" => allMatching.Where(r => r.EstadoActual != EstadoActualSubida).ToList(),
            "Subidas" => allMatching.Where(r => r.EstadoActual == EstadoActualSubida).ToList(),
            "Certificados" => allMatching.Where(r => r.EstadoActual == EstadoActualCertificado).ToList(),
            "SinAgregar" => allMatching.Where(r => r.Origin == "SinAgregar").ToList(),
            _ => allMatching,
        };

        // Completed cases (SUBIDA A CONASET) drop to the bottom, everything still in progress
        // stays on top in the order it arrived — so newly loaded/pending cases are always the
        // first thing the operator sees instead of mixed in among finished ones. Within the
        // completed group, sort by FechaDeSubida so the monthly print workflow can walk them
        // in the order they were closed out.
        Requests = filtered
            .OrderBy(r => r.EstadoActual == EstadoActualSubida ? 1 : 0)
            .ThenBy(r => r.EstadoActual == EstadoActualSubida ? r.FechaDeSubida : null)
            .ThenBy(r => r.Id)
            .ToList();

        var all = repository.GetAll();

        DuplicateRuts = all
            .Where(r => !string.IsNullOrWhiteSpace(r.Rut))
            .GroupBy(r => r.Rut!)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        FlaggedCount = repository.GetFlagged().Count;
        SinAgregarCount = allMatching.Count(r => r.Origin == "SinAgregar");
    }

    public string BuildUrl(
        string? newTab = null,
        bool? newFlagged = null)
    {
        var targetTab = newTab ?? Tab ?? "Todos";
        var targetFlagged = newFlagged ?? Flagged;
        var targetSearch = Search;

        var query = new List<string>();
        if (!string.IsNullOrEmpty(targetTab) && targetTab != "Todos")
        {
            query.Add($"tab={Uri.EscapeDataString(targetTab)}");
        }
        if (targetFlagged == true)
        {
            query.Add("flagged=true");
        }
        if (!string.IsNullOrEmpty(targetSearch))
        {
            query.Add($"search={Uri.EscapeDataString(targetSearch)}");
        }

        return "/Index" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
    }

    public IActionResult OnPostSetEstadoActual(long id, string estadoActual)
    {
        var request = repository.FindById(id);
        if (request is not null)
        {
            var normalized = EstadoCatalog.NormalizeForPersistence(estadoActual, isEstado: false);
            var unknown = EstadoCatalog.IsUnknownEstadoActual(estadoActual);
            request.EstadoActual = normalized;
            if (normalized == EstadoActualSubida)
            {
                request.FechaDeSubida = DateOnly.FromDateTime(DateTime.Today);
            }
            else if (normalized != EstadoActualSubida && request.FechaDeSubida is not null)
            {
                request.FechaDeSubida = null;
            }

            if (unknown)
            {
                request.NeedsReview = true;
                repository.Update(request);
                repository.AddFlag(id, "EstadoActual", ImportFlag.ReasonCodes.UnknownEstadoActual, estadoActual);
            }
            else
            {
                request.NeedsReview = false;
                repository.Update(request);
                repository.ClearFlags(id);
            }
        }

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            return new JsonResult(new
            {
                fechaDeSubida = request?.FechaDeSubida?.ToString("dd/MM/yyyy") ?? "—",
                estadoActual = request?.EstadoActual
            });
        }

        return RedirectToPage();
    }

    public IActionResult OnPostSetPersonData(long id, string nombreCompleto, string rut)
    {
        var request = repository.FindById(id);
        if (request is not null)
        {
            request.NombreCompleto = nombreCompleto;
            // Normalize however the operator typed it (with/without dots, with/without dash,
            // missing the leading zero) into the canonical stored form — display then adds
            // dots back via RutFormatter, so storage and formatting stay independent.
            request.Rut = Rut.TryParse(rut, out var parsed) ? parsed.ToString() : rut;
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

            // Marcar/Pendiente carpeta son flags de la cola de impresión para casos en curso.
            // Una vez subido el caso, ya no pertenece a esa cola.
            if (request.Marked)
            {
                repository.SetMarked(id, false);
            }
        }

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            return new JsonResult(new
            {
                fechaDeSubida = request?.FechaDeSubida?.ToString("dd/MM/yyyy") ?? DateOnly.FromDateTime(DateTime.Today).ToString("dd/MM/yyyy"),
                estadoActual = EstadoActualSubida
            });
        }

        return RedirectToPage();
    }

    public IActionResult OnPostSincronizar()
    {
        try
        {
            var result = MatrizSyncService.Sync(ResolveMatrizPath(), repository);
            SyncMessage = result.NoOp
                ? "Sincronizacion: configura F8:MatrizExcelPath en appsettings para habilitarla."
                : result.Summary() + (result.Alerts.Count > 0 ? " Detalle: " + string.Join(" | ", result.Alerts) : string.Empty);
        }
        catch (Exception ex)
        {
            SyncMessage = $"Error al sincronizar: {ex.Message}";
        }
        return RedirectToPage();
    }

    private string? ResolveMatrizPath()
    {
        var matrizPath = options?.MatrizExcelPath;
        if (string.IsNullOrWhiteSpace(matrizPath))
        {
            return null;
        }
        return Path.IsPathRooted(matrizPath)
            ? matrizPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, matrizPath));
    }

    public static string EstadoRowClass(string? estado) => estado switch
    {
        "SUBIR CON F8" => "estado-row-f8",
        "PRIMERA LICENCIA" => "estado-row-licencia",
        "CAMBIO DE DOMICILIO" => "estado-row-domicilio",
        "CREAR CERTIFICADO" => "estado-row-certificado",
        "CARPETA SUBIDA" => "estado-row-subida",
        "PENDIENTE" => "estado-row-pendiente",
        "DENEGADA" => "estado-row-denegada",
        _ => "",
    };

    public static string EstadoActualBadgeClass(string? estadoActual) => estadoActual switch
    {
        EstadoActualSubida => "badge-confirmed",
        "CREAR CERTIFICADO" => "badge-uploaded",
        "PENDIENTE" or null or "" => "badge-pending",
        _ => "badge-review",
    };

    public IActionResult OnPostSetCodigoF8(long id, string? codigoF8)
    {
        var request = repository.FindById(id);
        if (request is not null)
        {
            request.CodigoF8 = string.IsNullOrWhiteSpace(codigoF8) ? null : codigoF8.Trim();
            repository.Update(request);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostToggleMarked(long id, string? markedValue)
    {
        repository.SetMarked(id, markedValue == "on");
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            var request = repository.FindById(id);
            return new JsonResult(new { marked = request?.Marked ?? false });
        }

        return RedirectToPage();
    }

    public IActionResult OnPostSetFechaPenultimaCarpeta(long id, string? fecha)
    {
        var request = repository.FindById(id);
        if (request is not null)
        {
            var trimmed = fecha?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                request.FechaPenultimaCarpeta = null;
                repository.Update(request);
            }
            else if (DateOnly.TryParseExact(trimmed, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "d/MM/yyyy", "dd/M/yyyy"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                request.FechaPenultimaCarpeta = parsedDate;
                repository.Update(request);
            }
            else
            {
                return new JsonResult(new { message = $"Fecha no reconocida: \"{fecha}\"." }) { StatusCode = StatusCodes.Status400BadRequest };
            }
        }

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            return new JsonResult(new { fecha = request?.FechaPenultimaCarpeta?.ToString("yyyy-MM-dd") ?? "" });
        }

        return RedirectToPage();
    }

    public IActionResult OnPostDeleteCase(long id)
    {
        repository.Delete(id);
        return RedirectToPage();
    }

    public IActionResult OnPostAddManualCases(List<string> nombreCompleto, List<string> rut, List<string>? estado = null, List<string?>? fechaPenultimaCarpeta = null, List<string?>? matrizSector = null, List<string?>? codigoF8 = null)
    {
        var count = Math.Min(nombreCompleto.Count, rut.Count);
        for (var i = 0; i < count; i++)
        {
            var nombre = nombreCompleto[i];
            var rutValue = rut[i];
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(rutValue))
            {
                continue;
            }

            var rawEstado = (estado is not null && i < estado.Count) ? estado[i] : null;
            var normalizedEstado = string.IsNullOrWhiteSpace(rawEstado) ? null : EstadoCatalog.NormalizeForPersistence(rawEstado, isEstado: true);
            var canonicalRut = Rut.TryParse(rutValue, out var parsedRut) ? parsedRut.ToString() : rutValue;
            var rawSector = (matrizSector is not null && i < matrizSector.Count && !string.IsNullOrWhiteSpace(matrizSector[i]))
                ? matrizSector[i]!.Trim()
                : null;
            var rawCodigoF8 = (codigoF8 is not null && i < codigoF8.Count && !string.IsNullOrWhiteSpace(codigoF8[i]))
                ? codigoF8[i]!.Trim()
                : null;

            DateOnly? parsedPenultimaDate = null;
            if (fechaPenultimaCarpeta is not null && i < fechaPenultimaCarpeta.Count && !string.IsNullOrWhiteSpace(fechaPenultimaCarpeta[i]))
            {
                var rawFecha = fechaPenultimaCarpeta[i]?.Trim();
                if (DateOnly.TryParseExact(rawFecha, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "d/MM/yyyy", "dd/M/yyyy"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
                {
                    parsedPenultimaDate = d;
                }
                else if (SpanishDateFormatter.TryParseSlashMonthDate(rawFecha, out var sm))
                {
                    parsedPenultimaDate = sm;
                }
                else if (SpanishDateFormatter.TryParseLongDate(rawFecha, out var ld))
                {
                    parsedPenultimaDate = ld;
                }
                else
                {
                    var fRes = FolderDate.Parse(rawFecha);
                    if (fRes.Outcome is FolderDateOutcome.Ok or FolderDateOutcome.SinCarpeta)
                    {
                        parsedPenultimaDate = fRes.Value;
                    }
                }
            }

            var insertedId = repository.Insert(new UrgentRequest
            {
                NombreCompleto = nombre,
                Rut = canonicalRut,
                RutRaw = rutValue,
                MatrizSector = rawSector,
                CodigoF8 = rawCodigoF8,
                Estado = normalizedEstado,
                FechaPenultimaCarpeta = parsedPenultimaDate,
                FechaPeticion = DateOnly.FromDateTime(DateTime.Today),
                Origin = "SinAgregar",
                CreatedAt = DateTimeOffset.UtcNow,
                NeedsReview = EstadoCatalog.IsUnknownEstado(rawEstado),
            });
            if (EstadoCatalog.IsUnknownEstado(rawEstado))
            {
                repository.AddFlag(insertedId, "Estado", ImportFlag.ReasonCodes.UnknownEstado, rawEstado);
            }
        }
        return RedirectToPage();
    }
}
