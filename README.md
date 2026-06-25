# InstaCropper

A small .NET 8 tool that fits any image onto a standard Instagram canvas
**without cropping the subject**. The image is shrunk to fit (keeping its aspect
ratio) and centered on a solid-color background of the size you choose. The
original file is never modified — a new `*_cropped.<ext>` file is written next
to it.

## Features

* Three Instagram canvas sizes:
  * `1:1` — 1080 × 1080
  * `4:5` — 1080 × 1350
  * `16:9` — 1080 × 608
* White or black background
* Processes multiple images in one run
* Drag images onto the app, or run it from the command line
* Original images are always preserved

## Usage

### Drag and drop

* **Windows:** drag one or more image files onto the executable. Use the
  arrow-key menu to pick the aspect ratio and background color.
* **macOS:** drag image files onto **InstaCropper.app** (see
  [Releases](../../releases)). A dialog asks for the aspect ratio and background
  color, then the cropped images are written next to the originals. Paths with
  spaces are handled correctly.

### Command line

```bash
InstaCropper [--ratio <name|index>] [--color <White|Black>] <image> [<image> ...]
```

* `-r, --ratio` — aspect ratio by index (`0`/`1`/`2`) or name prefix
  (`1:1`, `4:5`, `16:9`).
* `-c, --color` — background color: `White` or `Black`.
* `-h, --help` — show usage.

When run in a terminal without `--ratio`/`--color`, the interactive arrow-key
menu is shown. When the options are supplied (or input is redirected), it runs
headless — this is how the macOS droplet drives it.

Example:

```bash
InstaCropper --ratio 4:5 --color white "My Photos/sunset beach.jpg"
# -> writes "My Photos/sunset beach_cropped.jpg"
```

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet restore
dotnet build -c Release
dotnet test                                   # runs the unit test suite
dotnet run --project InstaCropper -- image.jpg
```

### Building the macOS app

On macOS (needs the .NET 8 SDK):

```bash
macos/build-macos-app.sh 1.0.0 artifacts
# -> artifacts/InstaCropper.app
```

The app is an AppleScript droplet bundling self-contained .NET binaries for both
Apple Silicon and Intel Macs.

## Releases

Builds are produced by CI (see
[`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml)) for **macOS**
(the `.app` droplet), **Windows x64** and **Linux x64** (self-contained
single-file binaries):

* **Push to `master`** builds all platforms and uploads them as downloadable
  **workflow artifacts**, named from the last real tag with the patch bumped and
  a `-dev.<commit>` suffix. No tag or release is created.
* **Pushing a `v*` tag** (created manually, only for real releases) publishes a
  GitHub release for that version with all platform zips attached.

## License

[MIT](License.md)
