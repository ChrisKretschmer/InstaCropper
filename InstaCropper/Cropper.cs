using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace InstaCropper;

/// <summary>A named Instagram canvas size.</summary>
public readonly record struct AspectRatio(string Name, int Width, int Height);

/// <summary>
/// Pure, side-effect-light image-fitting logic, kept separate from the interactive
/// console front-end in <c>Program.cs</c> so it can be unit tested.
/// </summary>
public static class Cropper
{
    /// <summary>Total margin (px) reserved around the fitted image — 15px per side.</summary>
    public const int Margin = 30;

    /// <summary>Suffix appended to the original file name for the generated output.</summary>
    public const string OutputSuffix = "_cropped";

    /// <summary>Selectable Instagram canvas sizes, in menu order.</summary>
    public static readonly AspectRatio[] AspectRatios =
    {
        new("1:1 (1080x1080)", 1080, 1080),
        new("4:5 (1080x1350)", 1080, 1350),
        new("16:9 (1080x608)", 1080, 608),
    };

    /// <summary>Selectable background colors, in menu order.</summary>
    public static readonly string[] ColorOptions = { "White", "Black" };

    /// <summary>Maps a color menu index to an ImageSharp color.</summary>
    public static Color ColorByIndex(int index) =>
        index == 0 ? Color.White : Color.Black;

    /// <summary>
    /// Resolves an aspect-ratio token (a numeric index, or a prefix/substring of a
    /// menu name such as <c>"1:1"</c> or <c>"1080x1350"</c>) to its menu index.
    /// </summary>
    public static int FindAspectRatio(string token)
    {
        token = token.Trim();

        if (int.TryParse(token, out int index) && index >= 0 && index < AspectRatios.Length)
            return index;

        for (int i = 0; i < AspectRatios.Length; i++)
        {
            if (AspectRatios[i].Name.StartsWith(token, StringComparison.OrdinalIgnoreCase) ||
                AspectRatios[i].Name.Contains(token, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new ArgumentException(
            $"Unknown aspect ratio '{token}'. Options: {string.Join(", ", AspectRatios.Select(a => a.Name))}.");
    }

    /// <summary>Resolves a color token (e.g. <c>"white"</c>, <c>"Black"</c>) to its menu index.</summary>
    public static int FindColor(string token)
    {
        token = token.Trim();
        for (int i = 0; i < ColorOptions.Length; i++)
        {
            if (ColorOptions[i].Equals(token, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new ArgumentException(
            $"Unknown background color '{token}'. Options: {string.Join(", ", ColorOptions)}.");
    }

    /// <summary>
    /// Normalizes a path argument as it may arrive from a shell or a macOS droplet:
    /// trims surrounding whitespace and a single layer of matching quotes. This guards
    /// against the long-standing "paths with spaces get mangled" bug when the app is
    /// launched from a bundled macOS .app.
    /// </summary>
    public static string NormalizePath(string raw)
    {
        string path = raw.Trim();
        if (path.Length >= 2 &&
            ((path[0] == '"' && path[^1] == '"') || (path[0] == '\'' && path[^1] == '\'')))
        {
            path = path[1..^1];
        }
        return path;
    }

    /// <summary>
    /// Derives the output path for a source image: <c>&lt;dir&gt;/&lt;name&gt;_cropped&lt;ext&gt;</c>.
    /// Paths containing spaces are handled correctly because the whole path is treated as
    /// a single value (the historic macOS bug was in argument splitting, not here).
    /// </summary>
    public static string GetOutputPath(string inputPath)
    {
        string? directory = Path.GetDirectoryName(inputPath);
        string fileName = $"{Path.GetFileNameWithoutExtension(inputPath)}{OutputSuffix}{Path.GetExtension(inputPath)}";
        return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
    }

    /// <summary>
    /// Produces a new canvas of the target size, filled with <paramref name="background"/>,
    /// with <paramref name="source"/> shrunk to fit (preserving aspect ratio) and centered.
    /// The source image is never mutated.
    /// </summary>
    public static Image<Rgba32> CreateCanvas(Image source, int targetWidth, int targetHeight, Color background)
    {
        var canvas = new Image<Rgba32>(targetWidth, targetHeight);
        canvas.Mutate(ctx => ctx.BackgroundColor(background));

        using Image resized = source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(targetWidth - Margin, targetHeight - Margin),
            Mode = ResizeMode.Max,
        }));

        var position = new Point(
            (canvas.Width - resized.Width) / 2,
            (canvas.Height - resized.Height) / 2);

        canvas.Mutate(ctx => ctx.DrawImage(resized, position, 1f));
        return canvas;
    }

    /// <summary>
    /// Loads <paramref name="inputPath"/>, fits it onto the target canvas, and saves the
    /// result next to the original as <c>*_cropped.&lt;ext&gt;</c>. Returns the output path.
    /// The original file is never modified.
    /// </summary>
    public static string ProcessFile(string inputPath, int targetWidth, int targetHeight, Color background)
    {
        using Image source = Image.Load(inputPath);
        using Image<Rgba32> canvas = CreateCanvas(source, targetWidth, targetHeight, background);
        string outputPath = GetOutputPath(inputPath);
        canvas.Save(outputPath);
        return outputPath;
    }
}
