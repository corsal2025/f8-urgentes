namespace F8Urgentes.Configuration;

public sealed class F8Options
{
    public const string SectionName = "F8";

    public string SqliteDbPath { get; set; } = "data/f8urgentes.db";
    public string? ExcelSourcePath { get; set; }
    public string? MatrizExcelPath { get; set; }
}
