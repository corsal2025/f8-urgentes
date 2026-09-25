using F8Urgentes.Dashboard.Pages;
using F8Urgentes.Data;
using F8Urgentes.Domain;
using F8Urgentes.Services;

namespace F8Urgentes.Tests.Pages;

public sealed class RecuperarPageModelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UrgentRequestRepository _repository;

    public RecuperarPageModelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"f8urgentes-recuperar-tests-{Guid.NewGuid():N}.db");
        _repository = new UrgentRequestRepository($"Data Source={_dbPath};Pooling=False");
        _repository.EnsureSchema();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void OnPost_CorrectSecretAnswer_UpdatesPassword()
    {
        _repository.InsertUser(new Usuario
        {
            Username = "testuser",
            PasswordHash = PasswordHasher.Hash("oldpassword123"),
            PreguntaSecreta = "Mascota",
            RespuestaHash = PasswordHasher.Hash("firulais"),
        });

        var model = new RecuperarModel(_repository)
        {
            Username = "testuser",
            RespuestaSecreta = "Firulais",
            NuevaPassword = "newpassword123",
            ConfirmarPassword = "newpassword123",
        };

        var result = model.OnPost();

        Assert.NotNull(result);
        Assert.Equal("Contraseña actualizada correctamente.", model.Message);

        var updatedUser = _repository.FindUserByUsername("testuser")!;
        Assert.True(PasswordHasher.Verify("newpassword123", updatedUser.PasswordHash));
    }

    [Fact]
    public void OnPost_WrongSecretAnswer_RejectsPasswordChange()
    {
        _repository.InsertUser(new Usuario
        {
            Username = "testuser",
            PasswordHash = PasswordHasher.Hash("oldpassword123"),
            PreguntaSecreta = "Mascota",
            RespuestaHash = PasswordHasher.Hash("firulais"),
        });

        var model = new RecuperarModel(_repository)
        {
            Username = "testuser",
            RespuestaSecreta = "gatito",
            NuevaPassword = "newpassword123",
            ConfirmarPassword = "newpassword123",
        };

        model.OnPost();

        Assert.False(model.ModelState.IsValid);
        var updatedUser = _repository.FindUserByUsername("testuser")!;
        Assert.True(PasswordHasher.Verify("oldpassword123", updatedUser.PasswordHash));
    }
}
