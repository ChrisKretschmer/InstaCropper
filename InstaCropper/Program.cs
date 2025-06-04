using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

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
            int targetWidth = 1080;
            int targetHeight = 1350;

            // create a empty image called resized image of target size x target size - fill it white
            var resizedImage = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(targetWidth, targetHeight);
            resizedImage.Mutate(x => x
                .BackgroundColor(SixLabors.ImageSharp.Color.White) // Fill the image with white color
            );


            // copy the original image into the resized image, centered, scale it to fit within the target size
            var resizedOriginalImage = image.Clone(ctx => ctx
                .Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth - 30, targetHeight - 30),
                    Mode = ResizeMode.Max // Resize to fit within the target size
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
            resizedImage.Dispose(); // Dispose of the resized image to free resources
            Console.WriteLine($"Cropped image saved to: {outputPath}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing '{imagePath}': {ex.Message}");
    }
}