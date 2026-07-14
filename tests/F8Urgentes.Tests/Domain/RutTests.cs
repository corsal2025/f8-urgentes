using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Domain;

public class RutTests
{
    [Fact]
    public void TryParse_StripsDots()
    {
        Assert.True(Rut.TryParse("15.949.558-2", out var rut));
        Assert.Equal("15949558-2", rut.ToString());
    }

    [Fact]
    public void TryParse_AcceptsNoDots()
    {
        Assert.True(Rut.TryParse("15949558-2", out var rut));
        Assert.Equal("15949558-2", rut.ToString());
    }

    [Fact]
    public void TryParse_UppercasesLowercaseK()
    {
        Assert.True(Rut.TryParse("7654321-k", out var rut));
        Assert.Equal("7654321-K", rut.ToString());
    }

    [Fact]
    public void TryParse_ValidCheckDigit_IsValidTrue()
    {
        Assert.True(Rut.TryParse("15949558-2", out var rut));
        Assert.True(rut.IsValid);
    }

    [Fact]
    public void TryParse_InvalidCheckDigit_ParseSucceedsButIsValidFalse()
    {
        Assert.True(Rut.TryParse("15949558-9", out var rut));
        Assert.False(rut.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_BlankOrNull_ReturnsFalse(string? raw)
    {
        Assert.False(Rut.TryParse(raw, out _));
    }

    [Fact]
    public void ToString_ReturnsCanonicalForm()
    {
        Assert.True(Rut.TryParse("7.654.321-K", out var rut));
        Assert.Equal("7654321-K", rut.ToString());
    }
}
