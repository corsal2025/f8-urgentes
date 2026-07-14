using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Domain;

public class FolderDateTests
{
    [Fact]
    public void Parse_SC_ReturnsNullOk()
    {
        var result = FolderDate.Parse("S/C");
        Assert.Null(result.Value);
        Assert.Equal(FolderDateOutcome.SinCarpeta, result.Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Blank_ReturnsNullOk(string? raw)
    {
        var result = FolderDate.Parse(raw);
        Assert.Null(result.Value);
        Assert.Equal(FolderDateOutcome.SinCarpeta, result.Outcome);
    }

    [Fact]
    public void Parse_IsoString_ReturnsDateOnly()
    {
        var result = FolderDate.Parse("2024-06-15");
        Assert.Equal(new DateOnly(2024, 6, 15), result.Value);
        Assert.Equal(FolderDateOutcome.Ok, result.Outcome);
    }

    [Fact]
    public void Parse_ValidExcelSerial_ReturnsDateOnly()
    {
        // Excel serial 45000 = 2023-03-15
        var result = FolderDate.Parse("45000");
        Assert.Equal(FolderDateOutcome.Ok, result.Outcome);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public void Parse_OutOfRangeSerial_RealMayoH54Value_ReturnsNullFlagged()
    {
        var result = FolderDate.Parse("29262516");
        Assert.Null(result.Value);
        Assert.Equal(FolderDateOutcome.OutOfRange, result.Outcome);
    }

    [Fact]
    public void Parse_UnparseableText_ReturnsNullFlagged()
    {
        var result = FolderDate.Parse("no-es-una-fecha");
        Assert.Null(result.Value);
        Assert.Equal(FolderDateOutcome.Unparseable, result.Outcome);
    }
}
