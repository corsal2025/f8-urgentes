using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Domain;

public sealed class FolderSectorCalculatorTests
{
    [Fact]
    public void For_DateBeforeCutoff_ReturnsArchivo()
    {
        Assert.Equal(FolderSector.Archivo, FolderSectorCalculator.For(new DateOnly(2023, 5, 31)));
    }

    [Fact]
    public void For_DateOnOrAfterCutoff_ReturnsOficina43()
    {
        Assert.Equal(FolderSector.Oficina43, FolderSectorCalculator.For(new DateOnly(2023, 6, 1)));
        Assert.Equal(FolderSector.Oficina43, FolderSectorCalculator.For(new DateOnly(2024, 1, 1)));
    }

    [Fact]
    public void For_NullDate_ReturnsNull()
    {
        Assert.Null(FolderSectorCalculator.For(null));
    }
}
