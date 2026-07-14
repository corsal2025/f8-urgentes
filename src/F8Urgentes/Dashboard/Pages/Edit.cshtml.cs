using System.ComponentModel.DataAnnotations;
using F8Urgentes.Data;
using F8Urgentes.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace F8Urgentes.Dashboard.Pages;

public sealed class EditModel(IUrgentRequestRepository repository) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet(long? id)
    {
        if (id is null)
        {
            Input = new InputModel();
            return;
        }

        var existing = repository.FindById(id.Value);
        if (existing is null)
        {
            Input = new InputModel();
            return;
        }

        Input = new InputModel
        {
            Id = existing.Id,
            FechaPeticion = existing.FechaPeticion,
            Nombres = existing.Nombres,
            Apellidos = existing.Apellidos,
            NombreCompleto = existing.NombreCompleto,
            Rut = existing.Rut,
            FechaUltimaCarpeta = existing.FechaUltimaCarpeta,
            CodigoF8 = existing.CodigoF8,
            FechaPenultimaCarpeta = existing.FechaPenultimaCarpeta,
            Estado = existing.Estado,
            EstadoActual = existing.EstadoActual,
            FechaDeSubida = existing.FechaDeSubida,
        };
    }

    public IActionResult? OnPost()
    {
        if (!Rut.TryParse(Input.Rut, out var parsedRut) || !parsedRut.IsValid)
        {
            ModelState.AddModelError(nameof(Input.Rut), "RUT inválido: verifique el dígito verificador.");
            return Page();
        }

        UrgentRequest request;
        if (Input.Id is { } id)
        {
            var existing = repository.FindById(id);
            if (existing is null)
            {
                ModelState.AddModelError(string.Empty, "La solicitud no existe.");
                return Page();
            }
            request = existing;
        }
        else
        {
            request = new UrgentRequest { Origin = "Web", SourceSheet = null, CreatedAt = DateTimeOffset.UtcNow };
        }

        request.FechaPeticion = Input.FechaPeticion;
        request.Nombres = Input.Nombres;
        request.Apellidos = Input.Apellidos;
        request.NombreCompleto = Input.NombreCompleto;
        request.Rut = parsedRut.ToString();
        request.RutRaw = Input.Rut;
        request.FechaUltimaCarpeta = Input.FechaUltimaCarpeta;
        request.CodigoF8 = Input.CodigoF8;
        request.FechaPenultimaCarpeta = Input.FechaPenultimaCarpeta;
        request.Estado = string.IsNullOrWhiteSpace(Input.Estado) ? null : EstadoCatalog.Canonicalize(Input.Estado);
        request.EstadoActual = string.IsNullOrWhiteSpace(Input.EstadoActual) ? null : EstadoCatalog.Canonicalize(Input.EstadoActual);
        request.FechaDeSubida = Input.FechaDeSubida;

        if (Input.Id is null)
        {
            repository.Insert(request);
        }
        else
        {
            repository.Update(request);
        }

        return RedirectToPage("Index");
    }

    public sealed class InputModel
    {
        public long? Id { get; set; }
        public DateOnly? FechaPeticion { get; set; }
        public string? Nombres { get; set; }
        public string? Apellidos { get; set; }
        [Required]
        public string? NombreCompleto { get; set; }
        [Required]
        public string? Rut { get; set; }
        public DateOnly? FechaUltimaCarpeta { get; set; }
        public string? CodigoF8 { get; set; }
        public DateOnly? FechaPenultimaCarpeta { get; set; }
        public string? Estado { get; set; }
        public string? EstadoActual { get; set; }
        public DateOnly? FechaDeSubida { get; set; }
    }
}
