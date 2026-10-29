# Meta RemovIG

**Limpiador de metadatos de imágenes para Windows.** Elimina EXIF, IPTC, XMP y Content Credentials (C2PA) y regenera la imagen copiando solo los píxeles a un archivo nuevo.

Aplicación de escritorio hecha con **WPF y .NET 8**, sin dependencias externas.

> English: a Windows desktop app that strips all metadata (EXIF, IPTC, XMP, C2PA/Content Credentials) from images by re-encoding only the pixels into a fresh file. Built with WPF on .NET 8, no third-party packages.

---

## Características

- Selección **múltiple** de imágenes, o arrastrar y soltar sobre la ventana.
- Elimina **EXIF** (ubicación GPS, modelo de cámara, fecha...), **IPTC**, **XMP** y **C2PA/JUMBF**.
- Reconstruye la imagen desde cero: píxeles copiados a un bitmap nuevo.
- Aplica la **orientación EXIF** antes de borrarla, para que la foto no salga girada.
- Conserva el **perfil de color ICC** para que los colores no cambien.
- JPG → JPG (calidad 95). Resto de formatos → PNG sin pérdida.
- **Verifica** el resultado: comprueba que el archivo nuevo ya no contiene marcas C2PA/IPTC.
- Tú eliges **dónde guardar** el resultado. Los originales no se tocan.
- Interfaz moderna en tema oscuro con barra de progreso.

## Descarga

Ve a la sección **[Releases](../../releases)** y descarga `MetaRemovig.exe`.
Requiere [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) si no lo tienes instalado.

## Uso

1. Abre la aplicación y pulsa **Sube tu imagen** (o arrastra archivos a la ventana).
2. Selecciona todas las imágenes que quieras.
3. Espera a que termine el procesado.
4. Pulsa **Guardar imágenes…** y elige la carpeta de destino.

Los archivos nuevos se guardan con el sufijo `_limpia`.

## Compilar desde el código

Necesitas el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) en Windows 10/11.

```bash
git clone https://github.com/pedropentester/meta-removig.git
cd meta-removig
dotnet run
```

Para generar un ejecutable único:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

El resultado queda en `bin/Release/net8.0-windows/win-x64/publish/`.

## Cómo funciona

1. Se decodifica la imagen con WPF (`BitmapDecoder`).
2. Se lee la orientación EXIF y se aplica a los píxeles.
3. Se copian los píxeles a un `BitmapSource` completamente nuevo.
4. Se codifica sin metadatos (`JpegBitmapEncoder` / `PngBitmapEncoder`).
5. Se busca en el archivo resultante cualquier firma de C2PA/JUMBF o de IPTC `DigitalSourceType` para confirmar que se han eliminado.

## Limitaciones

- Recomprimir un JPG no es idéntico bit a bit (a calidad 95 es imperceptible). Si necesitas cero pérdida, usa PNG.
- **No elimina marcas de agua invisibles** incrustadas en los propios píxeles (por ejemplo, SynthID), porque forman parte de la imagen.
- No impide que las plataformas analicen visualmente el contenido con sus propios clasificadores.
- HEIC/WebP dependen de que Windows tenga instalados los códecs correspondientes.

## Uso responsable

Esta herramienta está pensada para **privacidad**: evitar compartir sin querer datos como tu ubicación, tu dispositivo o la fecha en que se tomó una foto.

Eliminar metadatos no te exime de las normas de cada plataforma. Si publicas contenido realista creado o modificado de forma significativa con IA, muchas redes sociales (Instagram, Facebook, YouTube, TikTok...) piden que lo indiques. Usa la herramienta de forma honesta y no para engañar a otras personas.

## Licencia

[MIT](LICENSE)
