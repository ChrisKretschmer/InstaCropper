# CLAUDE.md

Guidance for AI assistants (and humans) working in this repository.

## Overview

InstaCropper is a small .NET 8 console application that resizes arbitrary
images so they fit within standard Instagram canvas sizes without cropping the
subject. Each source image is shrunk to fit (preserving aspect ratio) and
centered on a solid-color background of the chosen target size. The original
file is never modified; a new `*_cropped.<ext>` file is written next to it.

Typical usage is "drag images onto the executable": the image file paths arrive
as command-line arguments, and the app interactively asks for the target aspect
ratio and background color before processing.

## Tech stack

- **Language / runtime:** C# on .NET 8 (`net8.0`), `OutputType` Exe.
- **Project style:** Top-level statements (no explicit `Main`), implicit usings
  enabled, nullable reference types enabled.
- **Image library:** [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) `3.1.8`
  (the only NuGet dependency).
- **IDE:** Visual Studio 2022 solution (`InstaCropper.sln`).

## Repository layout

```
InstaCropper.sln              Visual Studio solution (single project)
InstaCropper/
  InstaCropper.csproj         Project file + NuGet references
  Program.cs                  Entire application (top-level statements)
.github/workflows/dotnet.yml  CI: restore, build, test on push/PR to master
README.md                     Short user-facing description
License.md                    MIT license
.gitignore                    Ignores /.vs, bin/, obj/
```

There is currently **no test project** despite the CI `dotnet test` step (it is
a no-op because no test assemblies exist).

## How the program works (`Program.cs`)

The entire app is ~100 lines of top-level statements. Flow:

1. `SelectMenu(title, options)` — a local helper that renders an arrow-key
   driven console menu (Up/Down to move with wrap-around, Enter to confirm) and
   returns the selected index. It clears the screen on each redraw.
2. Prompt for an **aspect ratio** from a hard-coded list:
   - `1:1 (1080x1080)`
   - `4:5 (1080x1350)`
   - `16:9 (1080x608)`
3. Prompt for a **background color**: `White` or `Black`.
4. If no image paths were passed as args, print a hint and exit.
5. For each path in `args`:
   - Skip (with a message) if the file does not exist.
   - Load the image, create a new canvas of the target size filled with the
     background color, resize the original to fit within `(target - 30)` on each
     dimension using `ResizeMode.Max` (preserves aspect ratio), center it, and
     draw it onto the canvas.
   - Save to `<dir>/<name>_cropped<ext>` and print the output path.
   - Any per-image exception is caught and reported; processing continues with
     the next file.

Key constants/conventions baked into the code:
- A fixed **30px total margin** (15px per side) around the fitted image.
- Output naming suffix is `_cropped`, preserving the original extension.
- Canvas pixel format is `Rgba32`.

## Build, run, and test

All commands run from the repository root.

```bash
dotnet restore                    # restore NuGet packages
dotnet build                      # build (Debug by default)
dotnet build -c Release           # release build
dotnet run --project InstaCropper -- path/to/image1.jpg path/to/image2.png
dotnet test                       # currently no tests exist (no-op)
```

Notes:
- The app is **interactive**: it reads arrow keys and Enter from the console, so
  it expects a real terminal. Image paths are positional arguments after `--`.
- Output files are written **next to each input image**, not to a separate
  output directory.

## CI

`.github/workflows/dotnet.yml` runs on push and pull requests targeting
`master`. It sets up .NET 8, then runs `dotnet restore`, `dotnet build
--no-restore`, and `dotnet test --no-build`. Keep the build warning-free and
ensure these commands pass before pushing.

## Conventions & guidance for changes

- **Match the existing style.** The codebase favors top-level statements and a
  single `Program.cs`. Keep new logic small and readable; only introduce
  additional files/classes if a change genuinely warrants it.
- **Nullable is enabled** — handle nullable results explicitly (e.g. the
  existing `Path.GetDirectoryName` null check).
- **Fully-qualified ImageSharp types** are used in places (e.g.
  `SixLabors.ImageSharp.Color`) to avoid ambiguity with `System.Drawing`-style
  names; follow that pattern when touching image code.
- **Never overwrite source images** — always write to a derived output path.
- **Fail soft per file** — keep the per-image try/catch so one bad file doesn't
  abort the whole batch.
- If you add user-configurable options (new aspect ratios, colors, margins),
  extend the existing hard-coded arrays / `SelectMenu` flow rather than adding a
  separate arg-parsing layer, unless asked otherwise.
- If you add tests, create a separate test project and add it to the solution so
  the existing `dotnet test` CI step becomes meaningful.

## Known doc drift

The `README.md` describes only the 1080x1350 white-background behavior, but the
code now offers a menu of three aspect ratios and two background colors. If you
change user-facing behavior, update `README.md` to match.
