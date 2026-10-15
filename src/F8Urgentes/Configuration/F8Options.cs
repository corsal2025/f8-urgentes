using System.ComponentModel.DataAnnotations;

namespace F8Urgentes.Configuration;

public sealed class F8Options
{
    public const string SectionName = "F8";

    [Required(ErrorMessage = "SqliteDbPath is required")]
    public string SqliteDbPath { get; set; } = "data/f8urgentes.db";
    
    public string? ExcelSourcePath { get; set; }
    public string? MatrizExcelPath { get; set; }

    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>Name printed under the "Quien solicita" signature line of the monthly print list.
    /// Empty leaves the line blank to sign by hand.</summary>
    public string SolicitanteNombre { get; set; } = string.Empty;
}
