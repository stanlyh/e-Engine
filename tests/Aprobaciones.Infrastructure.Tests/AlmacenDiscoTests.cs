using Aprobaciones.Application.Documentos;
using Aprobaciones.Infrastructure.Archivos;
using Microsoft.Extensions.Options;

namespace Aprobaciones.Infrastructure.Tests;

public sealed class AlmacenDiscoTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "aprobaciones-tests", Guid.NewGuid().ToString("N"));
    private readonly AlmacenDisco _almacen;

    public AlmacenDiscoTests() =>
        _almacen = new AlmacenDisco(Options.Create(new OpcionesDocumentos { RutaArchivos = _carpeta }), TimeProvider.System);

    public void Dispose()
    {
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
    }

    [Fact]
    public async Task Guarda_lee_y_elimina()
    {
        var contenido = "%PDF-1.7 contenido"u8.ToArray();

        var guardado = await _almacen.GuardarAsync(7, "factura.pdf", ".pdf", "application/pdf", contenido, CancellationToken.None);

        Assert.Matches(@"^\d{4}/\d{2}/7/[0-9a-f]{32}\.pdf$", guardado.RutaRelativa);
        Assert.Equal(contenido.LongLength, guardado.TamanoBytes);
        Assert.Equal(64, guardado.HashSha256.Length);
        await using (var leido = _almacen.Abrir(guardado.RutaRelativa))
        using (var memoria = new MemoryStream())
        {
            await leido.CopyToAsync(memoria);
            Assert.Equal(contenido, memoria.ToArray());
        }

        _almacen.Eliminar(guardado.RutaRelativa);
        Assert.Throws<FileNotFoundException>(() => _almacen.Abrir(guardado.RutaRelativa));
    }

    [Theory]
    [InlineData("../fuera.txt")]
    [InlineData("2026/../../fuera.txt")]
    [InlineData(@"C:\Windows\win.ini")]
    public void No_permite_rutas_fuera_del_almacen(string ruta) =>
        Assert.Throws<InvalidOperationException>(() => _almacen.Abrir(ruta));
}
