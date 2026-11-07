# Mphoto

Editor de imágenes libre y gratuito para Windows, con una interfaz moderna inspirada en los grandes editores (Photoshop/GIMP) y motor de renderizado [SkiaSharp](https://github.com/mono/SkiaSharp).

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

Puedes compilarlo tú mismo o descargar la versión lista para usar desde la sección de releases del repositorio.

## Compilar

Requisitos: Windows y el SDK de .NET 8.

```
dotnet build Mphoto.sln -c Release
```

Para generar un único ejecutable autocontenido (no necesita .NET instalado):

```
dotnet publish Mphoto.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Pruebas

```
dotnet run --project tests/Mphoto.CoreTests -c Release
```

## Licencia

MIT © 2026 MSoft
