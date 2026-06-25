# CLAUDE.md

Guidance for AI assistants (and humans) working in this repository.

## Overview

InstaCropper is a small .NET 8 console application that resizes arbitrary
images so they fit within standard Instagram canvas sizes without cropping the
subject. Each source image is shrunk to fit (preserving aspect ratio) and
centered on a solid-color background of the chosen target size. The original
file is never modified; a new `*_cropped.<ext>` file is written next to it.

It can be driven two ways:

- **Interactively** — drag images onto the executable / run in a terminal: an
  arrow-key menu asks for the aspect ratio and background color.
- **Non-interactively** — flags (`--ratio`, `--color`) supply those choices so
  no TTY is needed. This is how the bundled macOS droplet runs it.

## Tech stack

- **Language / runtime:** C# on .NET 8 (`net8.0`), `OutputType` Exe.
- **Project style:** Top-level statements (no explicit `Main` in `Program.cs`),
  implicit usings enabled, nullable reference types enabled.
- **Image library:** [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) `3.1.8`.
  Stay on the **3.1.x** line — it is the last Apache-2.0 licensed release; 4.x
  requires a paid Six Labors license and fails the build without one. The
  `NU1902` advisory warning on 3.1.8 is known and non-blocking.
- **Tests:** xUnit (`InstaCropper.Tests`).
- **IDE:** Visual Studio 2022 solution (`InstaCropper.sln`).

## Repository layout

```
InstaCropper.sln                  Visual Studio solution (app + test project)
InstaCropper/
  InstaCropper.csproj             App project file + NuGet references
  Program.cs                      Console front-end: arg parsing, interactive menu, batch loop
  Cropper.cs                      Testable image-fitting logic (no console I/O)
InstaCropper.Tests/
  InstaCropper.Tests.csproj       xUnit test project (references the app)
  CropperTests.cs                 Unit tests for Cropper
macos/
  InstaCropper.applescript        Droplet source (drag-and-drop handler)
  build-macos-app.sh              Builds InstaCropper.app (osacompile + dotnet publish)
.github/workflows/dotnet.yml      CI: build/test everywhere; release on master & tags
README.md                         User-facing documentation
License.md                        MIT license
.gitignore                        Ignores /.vs, bin/, obj/
```

## How the program works

### `Cropper.cs` (the logic)

All pure / testable logic lives in the static `Cropper` class so it can be unit
tested without a console:

- `AspectRatios` — the hard-coded canvas sizes (`1:1`, `4:5`, `16:9`), in menu
  order, as `AspectRatio(Name, Width, Height)` records.
- `ColorOptions` / `ColorByIndex` — `White` / `Black` background choices.
- `FindAspectRatio(token)` / `FindColor(token)` — resolve a CLI token (index or
  name prefix) to a menu index; throw `ArgumentException` on unknown input.
- `NormalizePath(raw)` — trims whitespace and a single layer of surrounding
  quotes from an incoming path argument (defensive hardening for the macOS
  "paths with spaces" issue).
- `GetOutputPath(inputPath)` — derives `<dir>/<name>_cropped<ext>`.
- `CreateCanvas(source, w, h, background)` — returns a new `Image<Rgba32>` of the
  target size, filled with the background, with the source resized to fit within
  `(target - Margin)` using `ResizeMode.Max` and centered. Never mutates source.
- `ProcessFile(inputPath, w, h, background)` — load → `CreateCanvas` → save next
  to the original; returns the output path.

Key constants: `Margin = 30` (15px per side), `OutputSuffix = "_cropped"`, canvas
pixel format `Rgba32`.

### `Program.cs` (the front-end)

Top-level statements only. Flow:

1. Parse `args` into image paths plus optional `--ratio`/`-r`, `--color`/`-c`,
   `--help`/`-h`. Non-flag args are image paths (run through `NormalizePath`).
2. Decide interactivity via `Console.IsInputRedirected`/`IsOutputRedirected`.
   - If a ratio/color wasn't given: show the arrow-key `SelectMenu` when
     interactive, otherwise fall back to defaults (4:5, White).
3. If no image paths were given, print a hint (and usage when non-interactive).
4. For each path: skip missing files, else `Cropper.ProcessFile` and print the
   output path. **Per-file try/catch** keeps one bad image from aborting the batch.

`SelectMenu` is the local arrow-key menu helper (Up/Down wrap-around, Enter
confirms, clears the screen each redraw). It requires a real console, so it is
only invoked on the interactive path.

## The macOS app (`macos/`)

Drag-and-drop on a `.app` bundle delivers files via Apple Events, not argv, and
runs without a TTY — so the app is an **AppleScript droplet** that drives the
.NET binary non-interactively:

- `InstaCropper.applescript` — `on open` handler: asks for ratio/color via
  `choose from list`, picks the binary matching `uname -m`, and invokes it with
  each path passed through `quoted form` (this is the real fix for the
  spaces-in-path bug). `on run` shows usage when launched with no files.
- `build-macos-app.sh <version> [out]` — `osacompile`s the droplet, then
  `dotnet publish`es self-contained single-file binaries for **both** `osx-arm64`
  and `osx-x64` into `Contents/Resources/bin/<rid>/`, and patches `Info.plist`
  (version, bundle id, `CFBundleDocumentTypes` = `public.image`).

Requires macOS tooling (`osacompile`, `PlistBuddy`); the `dotnet publish` half
cross-compiles fine from Linux. The app is unsigned (no notarization), so users
may need to allow it via Gatekeeper.

## Build, run, and test

All commands run from the repository root.

```bash
dotnet restore
dotnet build -c Release
dotnet test                                   # runs InstaCropper.Tests (xUnit)
dotnet run --project InstaCropper -- path/to/image1.jpg path/to/image2.png
dotnet run --project InstaCropper -- --ratio 4:5 --color white "My Photos/a b.jpg"
```

Output files are written **next to each input image**, not to a separate
directory.

## CI & releases (`.github/workflows/dotnet.yml`)

- Triggers: push to `master`, push of a `v*` tag, and PRs targeting `master`.
- Jobs (the packaging/release jobs only run on master pushes or `v*` tags):
  - **`build`** (ubuntu): `restore` → `build -c Release` → `test`. Runs on every
    push/PR and gates everything else.
  - **`version`** (ubuntu): computes the version once and exposes it as job
    outputs (`version`, `is_release`) for the jobs below.
    - **Master push:** `v<MAJOR>.<MINOR>.<PATCH+1>-dev.<sha>` (base = latest
      **real** release tag, `-dev` tags excluded by a regex); `is_release=false`.
    - **Tag push:** the exact tagged version; `is_release=true`.
  - **`package-macos`** (macos-latest): builds the `.app` (needs macOS tooling),
    zips it, uploads it as a workflow artifact.
  - **`package-desktop`** (ubuntu): cross-publishes self-contained single-file
    **win-x64** and **linux-x64** binaries on Linux, zips each, uploads them as a
    workflow artifact. (Each platform builds on its natural runner; only macOS
    uses the macOS runner.)
  - **`release`** (ubuntu, `is_release` only): downloads every platform artifact
    and publishes a GitHub release with all zips via `gh`. Holds the
    `contents: write` permission. **Tags are created manually, only for real
    releases** — dev builds never create a tag or release.
- Keep the build warning-free (the ImageSharp `NU1902` advisory aside) and tests
  green before pushing.

## Conventions & guidance for changes

- **Match the existing style.** Front-end stays in `Program.cs` (top-level
  statements); pure/testable logic goes in `Cropper.cs`. Add new files/classes
  only when a change genuinely warrants it.
- **Keep logic testable.** New image/path behavior belongs in `Cropper` with a
  test in `CropperTests.cs`, not buried in the console loop.
- **Nullable is enabled** — handle nullable results explicitly.
- **Fully-qualify or import ImageSharp types** deliberately to avoid ambiguity
  with `System.Drawing`-style names.
- **Never overwrite source images** — always write to the derived output path.
- **Fail soft per file** — keep the per-image try/catch in the batch loop.
- **Extend, don't re-architect.** New aspect ratios/colors go in the
  `Cropper.AspectRatios` / `Cropper.ColorOptions` arrays (the menu, CLI flags,
  and droplet all read from there). Keep the AppleScript droplet's ratio/color
  lists in sync with those arrays.
- **Stay on ImageSharp 3.1.x** (license) — see the Tech stack note.
- If you change user-facing behavior, update `README.md` to match.
