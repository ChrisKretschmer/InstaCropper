using InstaCropper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace InstaCropper.Tests;

public class CropperTests
{
    private static Image<Rgba32> SolidImage(int width, int height, Color color)
    {
        var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.BackgroundColor(color));
        return image;
    }

    // ---- Option resolution -------------------------------------------------

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("1:1", 0)]
    [InlineData("4:5", 1)]
    [InlineData("16:9", 2)]
    [InlineData("1080x1350", 1)]
    [InlineData("  4:5  ", 1)]
    public void FindAspectRatio_resolves_index_and_name(string token, int expected)
    {
        Assert.Equal(expected, Cropper.FindAspectRatio(token));
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("99")]
    [InlineData("-1")]
    public void FindAspectRatio_rejects_unknown(string token)
    {
        Assert.Throws<ArgumentException>(() => Cropper.FindAspectRatio(token));
    }

    [Theory]
    [InlineData("White", 0)]
    [InlineData("white", 0)]
    [InlineData("BLACK", 1)]
    [InlineData("  black ", 1)]
    public void FindColor_resolves_name(string token, int expected)
    {
        Assert.Equal(expected, Cropper.FindColor(token));
    }

    [Fact]
    public void FindColor_rejects_unknown()
    {
        Assert.Throws<ArgumentException>(() => Cropper.FindColor("green"));
    }

    [Fact]
    public void ColorByIndex_maps_to_white_and_black()
    {
        Assert.Equal(Color.White, Cropper.ColorByIndex(0));
        Assert.Equal(Color.Black, Cropper.ColorByIndex(1));
    }

    // ---- Path handling (the macOS "spaces in path" bug) --------------------

    [Theory]
    [InlineData("plain.jpg", "plain.jpg")]
    [InlineData("  padded.png  ", "padded.png")]
    [InlineData("\"quoted path.jpg\"", "quoted path.jpg")]
    [InlineData("'single quoted.png'", "single quoted.png")]
    [InlineData("  \"/Users/me/My Photos/a b.jpg\"  ", "/Users/me/My Photos/a b.jpg")]
    public void NormalizePath_strips_quotes_and_whitespace(string raw, string expected)
    {
        Assert.Equal(expected, Cropper.NormalizePath(raw));
    }

    [Fact]
    public void GetOutputPath_appends_suffix_and_preserves_directory_with_spaces()
    {
        string input = Path.Combine("My Photos", "sunset beach.jpg");
        string output = Cropper.GetOutputPath(input);

        Assert.Equal(Path.Combine("My Photos", "sunset beach_cropped.jpg"), output);
    }

    [Fact]
    public void GetOutputPath_handles_filename_without_directory()
    {
        Assert.Equal("pic_cropped.png", Cropper.GetOutputPath("pic.png"));
    }

    [Fact]
    public void GetOutputPath_preserves_extension_case()
    {
        Assert.Equal("photo_cropped.JPG", Cropper.GetOutputPath("photo.JPG"));
    }

    // ---- Canvas composition ------------------------------------------------

    [Theory]
    [InlineData(1080, 1080)]
    [InlineData(1080, 1350)]
    [InlineData(1080, 608)]
    public void CreateCanvas_matches_target_dimensions(int width, int height)
    {
        using Image<Rgba32> source = SolidImage(400, 400, Color.Red);
        using Image<Rgba32> canvas = Cropper.CreateCanvas(source, width, height, Color.White);

        Assert.Equal(width, canvas.Width);
        Assert.Equal(height, canvas.Height);
    }

    [Fact]
    public void CreateCanvas_fills_corners_with_background_and_centers_subject()
    {
        using Image<Rgba32> source = SolidImage(400, 400, Color.Red);
        using Image<Rgba32> canvas = Cropper.CreateCanvas(source, 1080, 1080, Color.White);

        var white = new Rgba32(255, 255, 255, 255);
        var red = new Rgba32(255, 0, 0, 255);

        // Corners fall inside the margin, so they must be the background color.
        Assert.Equal(white, canvas[0, 0]);
        Assert.Equal(white, canvas[canvas.Width - 1, canvas.Height - 1]);

        // The centered subject covers the middle of the canvas.
        Assert.Equal(red, canvas[canvas.Width / 2, canvas.Height / 2]);
    }

    [Fact]
    public void CreateCanvas_uses_black_background_when_requested()
    {
        using Image<Rgba32> source = SolidImage(400, 400, Color.Red);
        using Image<Rgba32> canvas = Cropper.CreateCanvas(source, 1080, 608, Color.Black);

        Assert.Equal(new Rgba32(0, 0, 0, 255), canvas[0, 0]);
    }

    [Fact]
    public void CreateCanvas_keeps_subject_within_margin()
    {
        // A wide image should be scaled so its width never exceeds target - margin.
        using Image<Rgba32> source = SolidImage(2000, 1000, Color.Red);
        using Image<Rgba32> canvas = Cropper.CreateCanvas(source, 1080, 1080, Color.White);

        var white = new Rgba32(255, 255, 255, 255);
        // The full left/right margin columns must remain background.
        Assert.Equal(white, canvas[0, canvas.Height / 2]);
        Assert.Equal(white, canvas[canvas.Width - 1, canvas.Height / 2]);
    }

    [Fact]
    public void CreateCanvas_does_not_mutate_the_source()
    {
        using Image<Rgba32> source = SolidImage(400, 300, Color.Red);
        using Image<Rgba32> canvas = Cropper.CreateCanvas(source, 1080, 1080, Color.White);

        Assert.Equal(400, source.Width);
        Assert.Equal(300, source.Height);
    }

    // ---- End-to-end file processing ----------------------------------------

    [Fact]
    public void ProcessFile_writes_cropped_file_next_to_source()
    {
        string dir = Path.Combine(Path.GetTempPath(), "instacropper_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Use a name with a space to exercise the path-handling fix end to end.
            string input = Path.Combine(dir, "my photo.png");
            using (Image<Rgba32> source = SolidImage(640, 480, Color.Red))
            {
                source.Save(input);
            }

            string output = Cropper.ProcessFile(input, 1080, 1350, Color.White);

            Assert.Equal(Path.Combine(dir, "my photo_cropped.png"), output);
            Assert.True(File.Exists(output));
            Assert.True(File.Exists(input)); // original is preserved

            using Image<Rgba32> result = Image.Load<Rgba32>(output);
            Assert.Equal(1080, result.Width);
            Assert.Equal(1350, result.Height);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
