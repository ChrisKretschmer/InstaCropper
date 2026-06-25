using InstaCropper;

// InstaCropper fits images onto a standard Instagram canvas without cropping the
// subject. It works two ways:
//   * Interactively (drag images onto the executable / run in a terminal): the
//     aspect ratio and background color are chosen with an arrow-key menu.
//   * Non-interactively (e.g. launched from the bundled macOS droplet, or in a
//     script): the same options are supplied as flags so no TTY is required.
//
//   InstaCropper [--ratio <name|index>] [--color <White|Black>] <image> [<image> ...]

var files = new List<string>();
int? ratioIndex = null;
int? colorIndex = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--ratio" or "-r" when i + 1 < args.Length:
            ratioIndex = Cropper.FindAspectRatio(args[++i]);
            break;
        case "--color" or "-c" when i + 1 < args.Length:
            colorIndex = Cropper.FindColor(args[++i]);
            break;
        case "--help" or "-h":
            PrintUsage();
            return;
        default:
            // Anything that isn't a recognized flag is treated as an image path.
            // NormalizePath strips stray quotes/whitespace so paths with spaces survive.
            files.Add(Cropper.NormalizePath(args[i]));
            break;
    }
}

// Only the interactive menu requires a real console; everything else can run headless.
bool interactive = !Console.IsInputRedirected && !Console.IsOutputRedirected;

ratioIndex ??= interactive
    ? SelectMenu(
        "Select the desired aspect ratio using the arrow keys. Enter confirms the selection:",
        Cropper.AspectRatios.Select(a => a.Name).ToArray())
    : 1; // default: 4:5 (1080x1350)

colorIndex ??= interactive
    ? SelectMenu(
        "Select the background color (use arrow keys, Enter confirms):",
        Cropper.ColorOptions)
    : 0; // default: White

AspectRatio ratio = Cropper.AspectRatios[ratioIndex.Value];
var backgroundColor = Cropper.ColorByIndex(colorIndex.Value);

if (files.Count == 0)
{
    Console.WriteLine("Please provide the path to one or more image files.");
    if (!interactive)
        PrintUsage();
    return;
}

foreach (string imagePath in files)
{
    if (!File.Exists(imagePath))
    {
        Console.WriteLine($"The file '{imagePath}' does not exist.");
        continue;
    }

    try
    {
        string outputPath = Cropper.ProcessFile(imagePath, ratio.Width, ratio.Height, backgroundColor);
        Console.WriteLine($"Cropped image saved to: {outputPath}");
    }
    catch (Exception ex)
    {
        // Fail soft per file so one bad image doesn't abort the whole batch.
        Console.WriteLine($"Error processing '{imagePath}': {ex.Message}");
    }
}

// Arrow-key driven console menu: Up/Down to move (with wrap-around), Enter to confirm.
// Returns the selected index. Redraws (and clears the screen) on each key press.
static int SelectMenu(string title, string[] options)
{
    int selected = 0;
    ConsoleKey key;
    do
    {
        Console.Clear();
        Console.WriteLine(title + "\n");
        for (int i = 0; i < options.Length; i++)
        {
            Console.Write(i == selected ? "> " : "  ");
            Console.WriteLine(options[i]);
        }

        key = Console.ReadKey(true).Key;
        if (key == ConsoleKey.UpArrow)
            selected = (selected == 0) ? options.Length - 1 : selected - 1;
        else if (key == ConsoleKey.DownArrow)
            selected = (selected == options.Length - 1) ? 0 : selected + 1;
    } while (key != ConsoleKey.Enter);

    return selected;
}

static void PrintUsage()
{
    Console.WriteLine("Usage: InstaCropper [--ratio <name|index>] [--color <White|Black>] <image> [<image> ...]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -r, --ratio   Aspect ratio. Index or name prefix, e.g. \"1:1\", \"4:5\", \"16:9\".");
    Console.WriteLine("  -c, --color   Background color: White or Black.");
    Console.WriteLine("  -h, --help    Show this help.");
    Console.WriteLine();
    Console.WriteLine("When run in a terminal without --ratio/--color, an interactive menu is shown.");
    Console.WriteLine("Aspect ratios:");
    for (int i = 0; i < Cropper.AspectRatios.Length; i++)
        Console.WriteLine($"  {i}: {Cropper.AspectRatios[i].Name}");
}
