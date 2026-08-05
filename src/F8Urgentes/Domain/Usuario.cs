namespace F8Urgentes.Domain;

public sealed class Usuario
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PreguntaSecreta { get; set; } = string.Empty;
    public string RespuestaHash { get; set; } = string.Empty;
}
