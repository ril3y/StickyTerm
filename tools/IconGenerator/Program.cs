using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

class IconGenerator
{
    static void Main(string[] args)
    {
        var outputDir = args.Length > 0 ? args[0] : @"..\..\ComPortManager\Resources";
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, "app.ico");

        // Generate icons at different sizes
        var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
        var images = new List<Bitmap>();

        foreach (var size in sizes)
        {
            images.Add(CreateIcon(size));
        }

        // Save as ICO
        SaveAsIco(images, outputPath);
        Console.WriteLine($"Icon saved to: {outputPath}");

        // Also save PNG versions for reference
        foreach (var size in sizes)
        {
            var pngPath = Path.Combine(outputDir, $"icon-{size}.png");
            using var bitmap = CreateIcon(size);
            bitmap.Save(pngPath, ImageFormat.Png);
            Console.WriteLine($"PNG saved to: {pngPath}");
        }

        // Cleanup
        foreach (var img in images)
        {
            img.Dispose();
        }

        Console.WriteLine("Done!");
    }

    static Bitmap CreateIcon(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float scale = size / 64f;

            // Background rounded rectangle
            using (var bgBrush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(size, size),
                Color.FromArgb(74, 144, 217),   // #4A90D9
                Color.FromArgb(43, 87, 151)))   // #2B5797
            {
                using var bgPath = CreateRoundedRectPath(2 * scale, 2 * scale, 60 * scale, 60 * scale, 8 * scale);
                g.FillPath(bgBrush, bgPath);
            }

            // Connector body
            using (var connBrush = new SolidBrush(Color.FromArgb(224, 224, 224)))
            using (var connPen = new Pen(Color.FromArgb(102, 102, 102), Math.Max(1, scale)))
            {
                using var connPath = CreateRoundedRectPath(12 * scale, 16 * scale, 40 * scale, 32 * scale, 4 * scale);
                g.FillPath(connBrush, connPath);
                g.DrawPath(connPen, connPath);
            }

            // Inner dark area
            using (var innerBrush = new SolidBrush(Color.FromArgb(51, 51, 51)))
            {
                using var innerPath = CreateRoundedRectPath(16 * scale, 20 * scale, 32 * scale, 24 * scale, 2 * scale);
                g.FillPath(innerBrush, innerPath);
            }

            // Pins - gold color
            using (var pinBrush = new SolidBrush(Color.FromArgb(255, 215, 0)))
            using (var pinPen = new Pen(Color.FromArgb(184, 134, 11), Math.Max(0.5f, 0.5f * scale)))
            {
                float pinRadius = Math.Max(1.5f, 3 * scale);

                // Top row
                DrawPin(g, 22 * scale, 28 * scale, pinRadius, pinBrush, pinPen);
                DrawPin(g, 32 * scale, 28 * scale, pinRadius, pinBrush, pinPen);
                DrawPin(g, 42 * scale, 28 * scale, pinRadius, pinBrush, pinPen);

                // Bottom row
                DrawPin(g, 27 * scale, 38 * scale, pinRadius, pinBrush, pinPen);
                DrawPin(g, 37 * scale, 38 * scale, pinRadius, pinBrush, pinPen);
            }

            // Arrow (only for larger sizes)
            if (size >= 32)
            {
                using (var arrowBrush = new SolidBrush(Color.FromArgb(76, 175, 80)))
                using (var arrowPen = new Pen(Color.FromArgb(46, 125, 50), Math.Max(0.5f, 0.5f * scale)))
                {
                    var arrowPoints = new PointF[]
                    {
                        new PointF(48 * scale, 32 * scale),
                        new PointF(54 * scale, 32 * scale),
                        new PointF(54 * scale, 28 * scale),
                        new PointF(60 * scale, 32 * scale),
                        new PointF(54 * scale, 36 * scale),
                        new PointF(54 * scale, 32 * scale)
                    };
                    g.FillPolygon(arrowBrush, arrowPoints);
                    g.DrawPolygon(arrowPen, arrowPoints);
                }
            }
        }

        return bitmap;
    }

    static void DrawPin(Graphics g, float x, float y, float radius, Brush brush, Pen pen)
    {
        g.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
        g.DrawEllipse(pen, x - radius, y - radius, radius * 2, radius * 2);
    }

    static GraphicsPath CreateRoundedRectPath(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();

        if (radius <= 0)
        {
            path.AddRectangle(new RectangleF(x, y, width, height));
            return path;
        }

        float diameter = radius * 2;

        // Ensure radius isn't larger than half the smallest dimension
        diameter = Math.Min(diameter, Math.Min(width, height));
        radius = diameter / 2;

        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    static void SaveAsIco(List<Bitmap> images, string path)
    {
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // ICO header
        bw.Write((short)0);            // Reserved
        bw.Write((short)1);            // Type (1 = ICO)
        bw.Write((short)images.Count); // Number of images

        // Calculate data offset
        int dataOffset = 6 + (16 * images.Count); // Header + entries

        var imageDataList = new List<byte[]>();

        // Write image entries
        foreach (var img in images)
        {
            using var ms = new MemoryStream();
            img.Save(ms, ImageFormat.Png);
            var pngData = ms.ToArray();
            imageDataList.Add(pngData);

            bw.Write((byte)(img.Width >= 256 ? 0 : img.Width));   // Width (0 = 256)
            bw.Write((byte)(img.Height >= 256 ? 0 : img.Height)); // Height (0 = 256)
            bw.Write((byte)0);       // Color palette
            bw.Write((byte)0);       // Reserved
            bw.Write((short)1);      // Color planes
            bw.Write((short)32);     // Bits per pixel
            bw.Write(pngData.Length); // Size of image data
            bw.Write(dataOffset);    // Offset to image data

            dataOffset += pngData.Length;
        }

        // Write image data
        foreach (var data in imageDataList)
        {
            bw.Write(data);
        }
    }
}
