using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

// Seitenverhältnisse definieren
(string Name, int Width, int Height)[] aspectRatios = new[]
{
    ("1:1 (1080x1080)", 1080, 1080),
    ("4:5 (1080x1350)", 1080, 1350),
    ("16:9 (1080x608)", 1080, 608)
};

// Auswahlmenü anzeigen
int selected = 0;
ConsoleKey key;
do
{
    Console.Clear();
    Console.WriteLine("Wähle das gewünschte Seitenverhältnis (mit Pfeiltasten, Enter bestätigt):\n");
    for (int i = 0; i < aspectRatios.Length; i++)
    {
        if (i == selected)
            Console.Write("> ");
        else
            Console.Write("  ");
        Console.WriteLine(aspectRatios[i].Name);
    }

    key = Console.ReadKey(true).Key;
    if (key == ConsoleKey.UpArrow)
        selected = (selected == 0) ? aspectRatios.Length - 1 : selected - 1;
    else if (key == ConsoleKey.DownArrow)
        selected = (selected == aspectRatios.Length - 1) ? 0 : selected + 1;
} while (key != ConsoleKey.Enter);

int targetWidth = aspectRatios[selected].Width;
int targetHeight = aspectRatios[selected].Height;

if (args.Length == 0)
{
    Console.WriteLine("Please provide the path to one or more image files.");
    return;
}

foreach (var imagePath in args)
{
    if (!File.Exists(imagePath))
    {
        Console.WriteLine($"The file '{imagePath}' does not exist.");
        continue;
    }

    try
    {
        using (var image = SixLabors.ImageSharp.Image.Load(imagePath))
        {
            var resizedImage = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(targetWidth, targetHeight);
            resizedImage.Mutate(x => x
                .BackgroundColor(SixLabors.ImageSharp.Color.White)
            );

            var resizedOriginalImage = image.Clone(ctx => ctx
                .Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth - 30, targetHeight - 30),
                    Mode = ResizeMode.Max
                }));

            resizedImage.Mutate(x => x
                .DrawImage(resizedOriginalImage, new SixLabors.ImageSharp.Point((resizedImage.Width - resizedOriginalImage.Width) / 2, (resizedImage.Height - resizedOriginalImage.Height) / 2), 1f)
            );

            string? directoryName = Path.GetDirectoryName(imagePath);
            if (directoryName == null)
            {
                Console.WriteLine("The directory of the provided image path could not be determined.");
                continue;
            }

            string outputPath = Path.Combine(directoryName, $"{Path.GetFileNameWithoutExtension(imagePath)}_cropped{Path.GetExtension(imagePath)}");
            resizedImage.Save(outputPath);
            resizedImage.Dispose();
            Console.WriteLine($"Cropped image saved to: {outputPath}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing '{imagePath}': {ex.Message}");
    }
}