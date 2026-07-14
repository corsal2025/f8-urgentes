using System.Globalization;

namespace F8Urgentes.Domain;

/// <summary>
/// Chilean RUT value object: body (numeric) + check digit ('0'-'9' or 'K').
/// Parsing always strips dots/spaces and normalizes the check digit to
/// uppercase; check-digit validity is tracked separately via <see cref="IsValid"/>
/// so callers can store-but-flag rather than reject.
/// </summary>
public readonly struct Rut
{
    public string Body { get; }
    public char CheckDigit { get; }
    public bool IsValid { get; }

    private Rut(string body, char checkDigit, bool isValid)
    {
        Body = body;
        CheckDigit = checkDigit;
        IsValid = isValid;
    }

    public static bool TryParse(string? raw, out Rut rut)
    {
        rut = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Replace(".", string.Empty).Replace(" ", string.Empty).Trim();
        if (cleaned.Length < 2)
        {
            return false;
        }

        string body;
        char check;
        var dashIndex = cleaned.LastIndexOf('-');
        if (dashIndex >= 0)
        {
            body = cleaned[..dashIndex];
            check = cleaned[(dashIndex + 1)..].Length > 0 ? char.ToUpperInvariant(cleaned[(dashIndex + 1)..][0]) : '\0';
        }
        else
        {
            body = cleaned[..^1];
            check = char.ToUpperInvariant(cleaned[^1]);
        }

        if (body.Length == 0 || !body.All(char.IsDigit))
        {
            return false;
        }

        var expected = ComputeCheckDigit(body);
        var isValid = expected == check;
        rut = new Rut(body, check, isValid);
        return true;
    }

    private static char ComputeCheckDigit(string body)
    {
        var sum = 0;
        var multiplier = 2;
        for (var i = body.Length - 1; i >= 0; i--)
        {
            sum += (body[i] - '0') * multiplier;
            multiplier = multiplier == 7 ? 2 : multiplier + 1;
        }

        var remainder = 11 - (sum % 11);
        return remainder switch
        {
            11 => '0',
            10 => 'K',
            _ => (char)('0' + remainder),
        };
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Body}-{CheckDigit}");
}
