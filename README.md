# Mphoto

Editor de imágenes con aspecto de Adobe Photoshop, 100 % gratuito y open source. Funciona en Windows, con una interfaz moderna inspirada en los grandes editores y motor de renderizado [SkiaSharp](https://github.com/mono/SkiaSharp).

## Funciones

### Capas
- Crear, duplicar, eliminar, subir y bajar capas.
- Visibilidad, opacidad y modos de fusión.
- Combinar hacia abajo y limpiar capa.

### Herramientas
- Mover la vista, zoom, selección rectangular, elíptica y lazo.
- Recorte, pincel, borrador, relleno, formas, texto y cuentagotas.
- Color principal y secundario, intercambio rápido y paleta de colores.

### Edición
- Deshacer y rehacer.
- Seleccionar todo, deseleccionar, invertir la selección y borrar su contenido.
- Cambiar el tamaño de la imagen, recortar a la selección.
- Girar 90° a izquierda y derecha, girar 180° y voltear en ambos ejes.

### Ajustes y filtros
- Brillo y contraste, tono y saturación, desaturar, sepia e invertir colores.
- Desenfoque, enfoque, pixelado y relieve.

### Archivos
- Abrir y guardar PNG, JPEG y WebP, con soporte de transparencia.
- Guardar como y exportar una copia sin modificar el documento original.

## Descargar

- **MphotoSetup.exe**: instalador para todos los usuarios de Windows 10/11 de 64 bits (68 MB). Crea accesos directos, entrada en Aplicaciones instaladas y un desinstalador.
- **Mphoto.exe**: versión portable, sin instalación (165 MB). No necesita .NET: es un único ejecutable autocontenido.

Ambos están en la sección de releases del repositorio.

## Compilar

Requisitos: Windows y el SDK de .NET 8.

```
dotnet build Mphoto.sln -c Release
```

Para generar un único ejecutable autocontenido (no necesita .NET instalado):

```
dotnet publish Mphoto.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Para generar el instalador (crea `publish\MphotoSetup.exe`):

```
powershell -File build-setup.ps1
```

## Pruebas

```
dotnet run --project tests/Mphoto.CoreTests -c Release
```

## Licencia

Apache License 2.0 © 2026 MSoft. Consulta [LICENSE](LICENSE) para el texto completo.
