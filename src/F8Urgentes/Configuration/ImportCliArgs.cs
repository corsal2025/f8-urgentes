namespace F8Urgentes.Configuration;

public static class ImportCliArgs
{
    /// <summary>
    /// Parses `--import [path]` and `--force` out of the process args.
    /// Returns null when `--import` is not present. Path is null when the
    /// flag was given without an explicit path (caller falls back to
    /// F8Options.ExcelSourcePath).
    /// </summary>
    public static (string? Path, bool Force)? TryParseImportArgs(string[] args)
    {
        var importIndex = Array.IndexOf(args, "--import");
        if (importIndex < 0)
        {
            return null;
        }

        string? path = null;
        if (importIndex + 1 < args.Length && !args[importIndex + 1].StartsWith("--", StringComparison.Ordinal))
        {
            path = args[importIndex + 1];
        }

        var force = args.Contains("--force");
        return (path, force);
    }
}
