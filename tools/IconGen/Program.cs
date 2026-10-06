using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: IconGen <logo.png> <out.ico>");
    return 1;
}

var pngPath = Path.GetFullPath(args[0]);
var icoPath = Path.GetFullPath(args[1]);

if (!File.Exists(pngPath))
{
    Console.Error.WriteLine($"input not found: {pngPath}");
    return 1;
}

int[] sizes = [16, 24, 32, 48, 64, 128, 256];

var source = new BitmapImage();
source.BeginInit();
source.CacheOption = BitmapCacheOption.OnLoad;
source.UriSource = new Uri(pngPath);
source.EndInit();

var frames = new List<byte[]>(sizes.Length);

foreach (var size in sizes)
{
    var visual = new DrawingVisual();
    using (var ctx = visual.RenderOpen())
    {
        ctx.DrawImage(source, new Rect(0, 0, size, size));
    }

    var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    target.Render(visual);

    var converted = new FormatConvertedBitmap(target, PixelFormats.Bgra32, null, 0);

    var stride = size * 4;
    var pixels = new byte[stride * size];
    converted.CopyPixels(pixels, stride, 0);

    using var frame = new MemoryStream();
    using var writer = new BinaryWriter(frame);

    // BITMAPINFOHEADER (height doubled to include the AND mask).
    writer.Write(40);
    writer.Write(size);
    writer.Write(size * 2);
    writer.Write((short)1);
    writer.Write((short)32);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);

    // XOR data, bottom-up.
    for (var y = size - 1; y >= 0; y--)
    {
        writer.Write(pixels, y * stride, stride);
    }

    // AND mask: 1bpp, row padded to 4 bytes, all zero (alpha channel is authoritative).
    var maskStride = ((size + 31) / 32) * 4;
    writer.Write(new byte[maskStride * size]);

    writer.Flush();
    frames.Add(frame.ToArray());
}

Directory.CreateDirectory(Path.GetDirectoryName(icoPath)!);

using var file = File.Create(icoPath);
using var ico = new BinaryWriter(file);

ico.Write((short)0);
ico.Write((short)1);
ico.Write((short)frames.Count);

var offset = 6 + (16 * frames.Count);

for (var i = 0; i < frames.Count; i++)
{
    var size = sizes[i];
    var dim = (byte)(size >= 256 ? 0 : size);

    ico.Write(dim);
    ico.Write(dim);
    ico.Write((byte)0);
    ico.Write((byte)0);
    ico.Write((short)1);
    ico.Write((short)32);
    ico.Write(frames[i].Length);
    ico.Write(offset);

    offset += frames[i].Length;
}

foreach (var frame in frames)
{
    ico.Write(frame);
}

Console.WriteLine($"wrote {icoPath} ({frames.Count} sizes)");
return 0;
