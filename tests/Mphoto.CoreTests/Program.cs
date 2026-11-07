using Mphoto.Core;

var passed = 0;
var failed = 0;

void Check(string name, bool ok)
{
    if (ok)
    {
        passed++;
        Console.WriteLine("  OK    " + name);
    }
    else
    {
        failed++;
        Console.WriteLine("  FALLO " + name);
    }
}

(int B, int G, int R, int A) Pixel(byte[] pixels, int width, int x, int y)
{
    var i = (y * width + x) * 4;
    return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
}

void SetPixel(byte[] pixels, int width, int x, int y, byte b, byte g, byte r, byte a)
{
    var i = (y * width + x) * 4;
    pixels[i] = b;
    pixels[i + 1] = g;
    pixels[i + 2] = r;
    pixels[i + 3] = a;
}

var temp = Path.Combine(Path.GetTempPath(), "MphotoTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);

try
{
    Console.WriteLine("Documento y capas");

    var document = new Document(4, 3);
    Check("Documento nuevo con una capa", document.Layers.Count == 1 && document.Width == 4 && document.Height == 3);
    var background = Pixel(document.Layers[0].Pixels, 4, 0, 0);
    Check("Fondo blanco opaco", background is (255, 255, 255, 255));

    document = new Document(2, 2);
    var red = DocumentOps.AddLayer(document, "Rojo");
    SetPixel(red.Pixels, 2, 0, 0, 0, 0, 255, 255);
    var composite = Compositor.Composite(document);
    Check("Capa normal tapa el fondo", Pixel(composite, 2, 0, 0) is (0, 0, 255, 255));

    document = new Document(1, 1);
    var multiply = DocumentOps.AddLayer(document, "Multiplicar");
    multiply.Blend = BlendMode.Multiply;
    SetPixel(multiply.Pixels, 1, 0, 0, 128, 128, 128, 255);
    composite = Compositor.Composite(document);
    var multiplied = Pixel(composite, 1, 0, 0);
    Check("Modo multiplicar", Math.Abs(multiplied.R - 128) <= 1 && multiplied.A == 255);

    document = new Document(1, 1);
    var half = DocumentOps.AddLayer(document, "Negro 50%");
    half.Opacity = 0.5;
    SetPixel(half.Pixels, 1, 0, 0, 0, 0, 0, 255);
    composite = Compositor.Composite(document);
    var blended = Pixel(composite, 1, 0, 0);
    Check("Opacidad al 50%", Math.Abs(blended.R - 127) <= 1);

    document = new Document(1, 1);
    var hidden = DocumentOps.AddLayer(document, "Oculta");
    hidden.Visible = false;
    SetPixel(hidden.Pixels, 1, 0, 0, 0, 0, 255, 255);
    composite = Compositor.Composite(document);
    Check("Capa oculta se ignora", Pixel(composite, 1, 0, 0) is (255, 255, 255, 255));

    Console.WriteLine("Selecciones");

    document = new Document(10, 10);
    SelectionOps.SelectRectangle(document.Selection, 2, 3, 4, 5);
    var bounds = SelectionOps.Bounds(document.Selection);
    Check("Límites de selección rectangular", bounds == (2, 3, 4, 5));
    Check("Máscara dentro y fuera", document.Selection.Mask![3 * 10 + 2] == 255 && document.Selection.Mask[0] == 0);

    SelectionOps.SelectAll(document.Selection);
    SelectionOps.Invert(document.Selection);
    Check("Invertir selección completa", SelectionOps.IsEmpty(document.Selection));

    SelectionOps.SelectEllipse(document.Selection, 0, 0, 10, 10);
    Check("Elipse incluye el centro y excluye la esquina",
        document.Selection.Mask![5 * 10 + 5] > 0 && document.Selection.Mask[0] == 0);

    SelectionOps.SelectPolygon(document.Selection, new[]
    {
        new Pt(1, 1), new Pt(8, 1), new Pt(5, 8)
    });
    bounds = SelectionOps.Bounds(document.Selection);
    Check("Polígono genera máscara", bounds.Width > 0 && bounds.Height > 0 && document.Selection.Mask![8] == 0);
    Check("Contorno de selección", SelectionOps.Outline(document.Selection).Count > 0);

    Console.WriteLine("Herramientas de dibujo");

    document = new Document(20, 20);
    var paintLayer = DocumentOps.AddLayer(document, "Pintura");
    DrawOps.Stroke(paintLayer, null, new[] { new Pt(10, 10) }, 3, 1, 1, 0xFFFF0000);
    Check("Pincel pinta", Pixel(paintLayer.Pixels, 20, 10, 10).A > 200 && Pixel(paintLayer.Pixels, 20, 10, 10).R > 200);

    SelectionOps.SelectRectangle(document.Selection, 0, 0, 5, 5);
    DrawOps.Stroke(paintLayer, document.Selection.Mask, new[] { new Pt(15, 15) }, 3, 1, 1, 0xFF00FF00);
    Check("El pincel respeta la selección", Pixel(paintLayer.Pixels, 20, 15, 15).A == 0);

    document = new Document(20, 20);
    paintLayer = DocumentOps.AddLayer(document, "Borrado");
    DrawOps.Shape(paintLayer, null, ShapeKind.Rectangle, 0, 0, 20, 20, 0xFFFF0000, 1, 1, true);
    DrawOps.Stroke(paintLayer, null, new[] { new Pt(10, 10) }, 5, 1, 1, 0xFF000000, erase: true);
    Check("Goma reduce alfa", Pixel(paintLayer.Pixels, 20, 10, 10).A < 128);

    document = new Document(50, 50);
    var shapeLayer = DocumentOps.AddLayer(document, "Forma");
    DrawOps.Shape(shapeLayer, null, ShapeKind.Rectangle, 10, 10, 20, 20, 0xFF0000FF, 3, 1, false);
    Check("Rectángulo con contorno", Pixel(shapeLayer.Pixels, 50, 10, 20).A > 0 && Pixel(shapeLayer.Pixels, 50, 20, 20).A == 0);

    document = new Document(200, 80);
    var textLayer = DocumentOps.AddLayer(document, "Texto");
    DrawOps.Text(textLayer, null, "Mphoto", 10, 50, "Segoe UI", 32, 0xFF000000, true, false, 1);
    var ink = 0;
    for (var i = 3; i < textLayer.Pixels.Length; i += 4)
    {
        if (textLayer.Pixels[i] > 0) ink++;
    }
    Check("Texto dibuja píxeles", ink > 50);

    document = new Document(9, 5);
    var fillLayer = DocumentOps.AddLayer(document, "Relleno");
    DrawOps.Shape(fillLayer, null, ShapeKind.Rectangle, 0, 0, 9, 5, 0xFFFFFFFF, 1, 1, true);
    for (var y = 0; y < 5; y++)
    {
        SetPixel(fillLayer.Pixels, 9, 4, y, 0, 0, 0, 255);
    }
    var filled = DrawOps.FloodFill(fillLayer, null, 1, 1, 0xFFFF0000, 10, out var fillBounds);
    Check("Relleno conectado", filled && Pixel(fillLayer.Pixels, 9, 0, 0).R > 200 && Pixel(fillLayer.Pixels, 9, 8, 0).R == 255 && Pixel(fillLayer.Pixels, 9, 8, 0).G == 255);
    Check("Relleno devuelve límites", fillBounds.Width >= 4 && fillBounds.Height == 5);

    Console.WriteLine("Ajustes y filtros");

    document = new Document(2, 2);
    var adjustLayer = DocumentOps.AddLayer(document, "Ajustes");
    SetPixel(adjustLayer.Pixels, 2, 0, 0, 255, 255, 255, 255);
    Adjustments.Invert(adjustLayer, null);
    Check("Invertir", Pixel(adjustLayer.Pixels, 2, 0, 0).R == 0);

    SetPixel(adjustLayer.Pixels, 2, 0, 0, 100, 100, 100, 255);
    Adjustments.BrightnessContrast(adjustLayer, null, 30, 0);
    Check("Brillo aumenta", Pixel(adjustLayer.Pixels, 2, 0, 0).R > 100);

    SetPixel(adjustLayer.Pixels, 2, 0, 0, 200, 100, 50, 255);
    Adjustments.Desaturate(adjustLayer, null);
    var gray = Pixel(adjustLayer.Pixels, 2, 0, 0);
    Check("Escala de grises", gray.R == gray.G && gray.G == gray.B);

    SetPixel(adjustLayer.Pixels, 2, 0, 0, 0, 0, 255, 255);
    Adjustments.HueSaturation(adjustLayer, null, 120, 0);
    var shifted = Pixel(adjustLayer.Pixels, 2, 0, 0);
    Check("Tono gira el color", shifted.G > 150 && shifted.B < 150);

    SetPixel(adjustLayer.Pixels, 2, 0, 0, 0, 0, 255, 255);
    SetPixel(adjustLayer.Pixels, 2, 1, 0, 0, 0, 255, 255);
    var mask = new byte[4];
    mask[0] = 255;
    Adjustments.Invert(adjustLayer, mask);
    Check("Ajuste respeta la máscara", Pixel(adjustLayer.Pixels, 2, 0, 0).R == 0 && Pixel(adjustLayer.Pixels, 2, 1, 0).R == 255);

    document = new Document(16, 16);
    var filterLayer = DocumentOps.AddLayer(document, "Filtro");
    DrawOps.Shape(filterLayer, null, ShapeKind.Rectangle, 0, 0, 16, 16, 0xFFFFFFFF, 1, 1, true);
    SetPixel(filterLayer.Pixels, 16, 8, 8, 0, 0, 0, 255);
    Filters.Blur(filterLayer, null, 2);
    var blurredCenter = Pixel(filterLayer.Pixels, 16, 8, 8);
    var blurredNeighbour = Pixel(filterLayer.Pixels, 16, 9, 8);
    Check("Desenfoque suaviza", blurredCenter.R > 0 && blurredNeighbour.R < 255);

    document = new Document(8, 8);
    filterLayer = DocumentOps.AddLayer(document, "Pixelar");
    for (var y = 0; y < 8; y++)
    {
        for (var x = 0; x < 8; x++)
        {
            SetPixel(filterLayer.Pixels, 8, x, y, (byte)(x * 30), (byte)(y * 30), 100, 255);
        }
    }
    Filters.Pixelate(filterLayer, null, 4);
    var blockA = Pixel(filterLayer.Pixels, 8, 0, 0);
    var blockB = Pixel(filterLayer.Pixels, 8, 3, 3);
    Check("Pixelado agrupa bloques", blockA == blockB);

    document = new Document(6, 6);
    filterLayer = DocumentOps.AddLayer(document, "Enfoque");
    DrawOps.Shape(filterLayer, null, ShapeKind.Rectangle, 0, 0, 6, 6, 0xFF808080, 1, 1, true);
    SetPixel(filterLayer.Pixels, 6, 3, 3, 255, 255, 255, 255);
    var beforeSharpen = Pixel(filterLayer.Pixels, 6, 2, 3);
    Filters.Sharpen(filterLayer, null, 150);
    Check("Enfoque modifica el borde", Pixel(filterLayer.Pixels, 6, 2, 3) != beforeSharpen);

    Console.WriteLine("Historial");

    document = new Document(10, 10);
    var historyLayer = DocumentOps.AddLayer(document, "Historial");
    var history = new HistoryManager();
    var edit = EditRecorder.Record(historyLayer, 0, 0, 10, 10,
        () => DrawOps.Stroke(historyLayer, null, new[] { new Pt(5, 5) }, 2, 1, 1, 0xFFFF0000), "Pincel");
    history.Push(edit);
    Check("Historial registra edición", history.CanUndo && Pixel(historyLayer.Pixels, 10, 5, 5).A > 0);
    history.Undo();
    Check("Deshacer restaura", Pixel(historyLayer.Pixels, 10, 5, 5).A == 0);
    history.Redo();
    Check("Rehacer reaplica", Pixel(historyLayer.Pixels, 10, 5, 5).A > 0);

    Console.WriteLine("Operaciones de documento");

    document = new Document(10, 10);
    var marker = DocumentOps.AddLayer(document, "Marca");
    SetPixel(marker.Pixels, 10, 5, 5, 0, 0, 255, 255);
    DocumentOps.Resize(document, 20, 20);
    var resized = Pixel(document.Layers[1].Pixels, 20, 10, 10);
    Check("Redimensionar", document.Width == 20 && resized.R > 200);

    document = new Document(10, 10);
    marker = DocumentOps.AddLayer(document, "Marca");
    SetPixel(marker.Pixels, 10, 8, 8, 0, 0, 255, 255);
    DocumentOps.Crop(document, 6, 6, 4, 4);
    Check("Recortar", document.Width == 4 && document.Height == 4 && Pixel(document.Layers[1].Pixels, 4, 2, 2).R > 200);

    document = new Document(4, 3);
    marker = DocumentOps.AddLayer(document, "Marca");
    SetPixel(marker.Pixels, 4, 0, 0, 0, 0, 255, 255);
    DocumentOps.Rotate(document, 90);
    Check("Girar 90 grados", document.Width == 3 && document.Height == 4 && Pixel(document.Layers[1].Pixels, 3, 2, 0).R > 200);

    document = new Document(4, 3);
    marker = DocumentOps.AddLayer(document, "Marca");
    SetPixel(marker.Pixels, 4, 0, 0, 0, 0, 255, 255);
    DocumentOps.Flip(document, true);
    Check("Voltear horizontal", Pixel(document.Layers[1].Pixels, 4, 3, 0).R > 200);

    document = new Document(2, 2);
    DocumentOps.AddLayer(document, "Dos");
    var duplicated = DocumentOps.DuplicateLayer(document, 1);
    Check("Duplicar capa", document.Layers.Count == 3 && duplicated.Name.Contains("copia"));
    Check("Eliminar capa", DocumentOps.DeleteLayer(document, 2) && document.Layers.Count == 2);
    Check("No eliminar la última capa", !DocumentOps.DeleteLayer(new Document(2, 2), 0));

    document = new Document(1, 1);
    var upper = DocumentOps.AddLayer(document, "Arriba");
    SetPixel(upper.Pixels, 1, 0, 0, 0, 0, 255, 255);
    upper.Opacity = 0.5;
    Check("Combinar hacia abajo", DocumentOps.MergeDown(document, 1) && document.Layers.Count == 1);
    var merged = Pixel(document.Layers[0].Pixels, 1, 0, 0);
    Check("Resultado de combinar", merged.R > 240 && merged.G > 100 && merged.G < 160);

    Console.WriteLine("Archivos");

    var pngPath = Path.Combine(temp, "prueba.png");
    var pixels = new byte[] { 0, 0, 255, 128 };
    ImageIo.Save(pngPath, pixels, 1, 1, ImageFormat.Png);
    var (loaded, width, height) = ImageIo.Load(pngPath);
    Check("PNG conserva alfa", width == 1 && height == 1 && loaded[2] > 240 && Math.Abs(loaded[3] - 128) <= 2);

    var jpegPath = Path.Combine(temp, "prueba.jpg");
    var jpegPixels = new byte[8 * 8 * 4];
    for (var i = 0; i < jpegPixels.Length; i += 4)
    {
        jpegPixels[i] = 0;
        jpegPixels[i + 1] = 0;
        jpegPixels[i + 2] = 255;
        jpegPixels[i + 3] = 255;
    }
    ImageIo.Save(jpegPath, jpegPixels, 8, 8, ImageFormat.Jpeg, 95);
    (loaded, _, _) = ImageIo.Load(jpegPath);
    Check("JPEG se guarda y carga", loaded[3] == 255 && loaded[2] > 150);

    var webpPath = Path.Combine(temp, "prueba.webp");
    ImageIo.Save(webpPath, pixels, 1, 1, ImageFormat.Webp, 90);
    (loaded, _, _) = ImageIo.Load(webpPath);
    Check("WebP se guarda y carga", loaded[2] > 200 && loaded[3] > 0);

    document = new Document(20, 20);
    var overlay = DocumentOps.AddLayer(document, "Región");
    SetPixel(overlay.Pixels, 20, 10, 10, 0, 128, 255, 200);
    var whole = Compositor.Composite(document);
    var region = new byte[20 * 20 * 4];
    Compositor.CompositeRegion(document, 5, 5, 10, 10, region);
    var same = true;
    for (var y = 5; y < 15; y++)
    {
        for (var x = 5; x < 15; x++)
        {
            if (Pixel(whole, 20, x, y) != Pixel(region, 20, x, y)) same = false;
        }
    }
    Check("Composición por región coincide", same);

    SelectionOps.SelectRectangle(document.Selection, 0, 0, 5, 5);
    DocumentOps.Resize(document, 30, 30);
    Check("Redimensionar limpia la selección", !document.Selection.IsActive);
}
finally
{
    try
    {
        Directory.Delete(temp, true);
    }
    catch
    {
        // Los archivos temporales pueden quedar bloqueados; no afecta al resultado.
    }
}

Console.WriteLine();
Console.WriteLine($"Resultado: {passed} comprobaciones, {failed} fallos.");
return failed == 0 ? 0 : 1;
