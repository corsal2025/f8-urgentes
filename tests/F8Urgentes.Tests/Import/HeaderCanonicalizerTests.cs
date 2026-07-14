using F8Urgentes.Import;

namespace F8Urgentes.Tests.Import;

public class HeaderCanonicalizerTests
{
    private static readonly string[] JunioJulioHeaders =
    [
        "FECHA PETICION", "NOMBRES", "APELLIDOS", "NOMBRE COMPLETO", "RUT",
        "FECHA ULTIMA CARPETA", "CODIGO F8", "FECHA PENULTIMA CARPETA", "ESTADO", "ESTADO ACTUAL", "FECHA DE SUBIDA",
    ];

    // MAYO: 10 columns, reordered, FECHA PETICION last, no FECHA DE SUBIDA.
    private static readonly string[] MayoHeaders =
    [
        "NOMBRES", "APELLIDOS", "NOMBRE COMPLETO", "RUT",
        "FECHA ULTIMA CARPETA", "CODIGO F8", "FECHA PENULTIMA CARPETA", "ESTADO", "ESTADO ACTUAL", "FECHA PETICION",
    ];

    [Fact]
    public void Canonicalize_JunioJulioLayout_MapsAllElevenFields()
    {
        var map = HeaderCanonicalizer.Canonicalize(JunioJulioHeaders);

        Assert.Equal(11, map.Count);
        Assert.Equal(0, map[HeaderCanonicalizer.FechaPeticion]);
        Assert.Equal(10, map[HeaderCanonicalizer.FechaDeSubida]);
    }

    [Fact]
    public void Canonicalize_MayoLayout_MapsFieldsByNameWithoutFechaDeSubida()
    {
        var map = HeaderCanonicalizer.Canonicalize(MayoHeaders);

        Assert.Equal(10, map.Count);
        Assert.Equal(9, map[HeaderCanonicalizer.FechaPeticion]);
        Assert.False(map.ContainsKey(HeaderCanonicalizer.FechaDeSubida));
    }

    [Fact]
    public void Canonicalize_CaseWhitespaceAccentVariants_ResolveToSameKey()
    {
        var map = HeaderCanonicalizer.Canonicalize(["  fecha   de subida  ", "Fecha De Subida"]);

        Assert.Single(map);
        Assert.Equal(0, map[HeaderCanonicalizer.FechaDeSubida]);
    }
}
