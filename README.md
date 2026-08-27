# Recipe Downloader

Recipe Downloader is a desktop application for discovering and downloading recipes from
supported meal kit providers, matching recipes by shared ingredients, and generating
consolidated grocery lists.

It ships two desktop hosts that share all of their logic:

- **Avalonia** — runs on Windows, macOS, and Linux.
- **WPF** — the original Windows-only host.

## Supported providers

| Provider | Discovery | Recipe data | Notes |
| --- | --- | --- | --- |
| HelloFresh | Per-letter sitemap pages | HelloFresh platform payload, falling back to schema.org JSON-LD | Also downloads the printable recipe card PDF |
| Blue Apron | `recipes/sitemap.xml` | Embedded React Query cache | See the note below |
| Factor | `sitemap_index.xml` → recipe sitemap | HelloFresh platform payload | ~8,900 recipes; prepared meals, so instructions are reheating steps |
| Home Chef | Weekly menu listings | schema.org microdata | Roughly the current week's menu, refreshed each week |

Every provider writes a `.json` file with the normalized recipe data plus a rendered `.html`
recipe card. HelloFresh additionally saves the provider's own PDF.

> **Blue Apron:** the site is currently behind a Cloudflare interstitial that rejects plain
> HTTP clients, so discovery returns no results until that changes. The provider code is
> unchanged and still parses saved pages correctly.

## Adding a provider

Most of the work is already shared. `RecipeDownloader.Core/Providers/Shared` contains:

- `JsonLdRecipeParser` — schema.org Recipe in JSON-LD, which most commercial recipe sites publish.
- `MicrodataRecipeParser` — schema.org Recipe as HTML microdata.
- `HelloFreshPlatformParser` — the recipe payload used by HelloFresh Group brands.
- `SitemapReader` — sitemaps and sitemap indexes, including gzipped ones.
- `RecipeProviderBase` / `SitemapRecipeProvider` — concurrency limiting, throttling, progress
  reporting, validation, and artifact writing.

A provider whose pages carry JSON-LD and whose site publishes a sitemap needs only a name, a
sitemap URL, a URL filter, and a parser call — see `FactorProvider` for the shape.

## Requirements

- .NET 10 SDK
- Windows only for the WPF host; the Avalonia host runs anywhere

## Build

```bash
dotnet build RecipeDownloader.sln
```

This works on all three platforms. `Directory.Build.props` enables Windows targeting so the
solution restores everywhere; the WPF host still only *runs* on Windows.

To build a single project:

```bash
dotnet build src/RecipeDownloader.Core/RecipeDownloader.Core.csproj
dotnet build src/RecipeDownloader.App.Avalonia/RecipeDownloader.App.Avalonia.csproj
```

## Run

Cross-platform (Windows, macOS, Linux):

```bash
dotnet run --project src/RecipeDownloader.App.Avalonia/RecipeDownloader.App.Avalonia.csproj
```

Windows-only WPF host:

```bash
dotnet run --project src/RecipeDownloader.App/RecipeDownloader.App.csproj
```

## Basic usage

1. Launch the application.
2. Choose an output directory with **Browse...**.
3. Select a provider from the left sidebar.
4. Click **Discover Recipes** to load the latest recipe list.
5. Select one or more recipes.
6. Click **Download Selected** or **Download All**.
7. Use **Pantry** and **Meal Planner** to match recipes and build a grocery list.

## Project layout

| Project | Target | Purpose |
| --- | --- | --- |
| `RecipeDownloader.Core` | `net10.0` | Providers, parsers, matching, grocery lists, storage |
| `RecipeDownloader.App.Shared` | `net10.0` | View models and platform-service abstractions |
| `RecipeDownloader.App.Avalonia` | `net10.0` | Cross-platform desktop host |
| `RecipeDownloader.App` | `net10.0-windows` | WPF desktop host |

Each host implements `IFolderPicker`, `IClipboardService`, and `IFileLauncher` for its
platform; everything above those three interfaces is shared.

## Storage locations

Settings, cached catalogs, and the pantry live under the platform's local application data
directory:

- Windows — `%LocalAppData%\RecipeDownloader`
- macOS — `~/Library/Application Support/RecipeDownloader`
- Linux — `$XDG_DATA_HOME/RecipeDownloader` (usually `~/.local/share/RecipeDownloader`)

## License

This project is licensed under the BSD 3-Clause License. See [LICENSE](LICENSE).
