using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Domain;

public class EstadoCatalogTests
{
    [Theory]
    [InlineData("SUBIR CON F8")]
    [InlineData("PRIMERA LICENCIA")]
    [InlineData("CAMBIO DE DOMICILIO")]
    [InlineData("CREAR CERTIFICADO")]
    [InlineData("CARPETA SUBIDA")]
    public void IsKnownEstado_KnownValues_ReturnsTrue(string value)
    {
        Assert.True(EstadoCatalog.IsKnownEstado(value));
    }

    [Theory]
    [InlineData("SUBIDA A CONASET")]
    [InlineData("PENDIENTE")]
    [InlineData("CREAR CERTIFICADO")]
    [InlineData("")]
    public void IsKnownEstadoActual_KnownValues_ReturnsTrue(string value)
    {
        Assert.True(EstadoCatalog.IsKnownEstadoActual(value));
    }

    [Fact]
    public void Canonicalize_TrimsAndUppercasesMixedCase()
    {
        Assert.Equal("PRIMERA LICENCIA", EstadoCatalog.Canonicalize("  primera licencia  "));
    }

    [Fact]
    public void IsKnownEstado_UnknownValue_ReturnsFalse()
    {
        Assert.False(EstadoCatalog.IsKnownEstado("OTRO VALOR"));
    }
}
