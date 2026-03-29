# Recipe Downloader

Recipe Downloader is a Windows desktop application for discovering and downloading recipes from supported meal kit providers.

Currently supported providers:

- HelloFresh
- Blue Apron

The application can discover recipe catalogs from those providers and save recipe files to a local output folder:

- HelloFresh downloads the recipe PDF and also exports JSON and HTML when structured recipe data is available.
- Blue Apron exports the recipe as JSON and HTML.

## Requirements

- .NET 10 SDK
- Windows to run the WPF desktop application

## Build

From the repository root:

```bash
dotnet build RecipeDownloader.sln
```

Because the app project targets WPF (`net10.0-windows`), the full solution is intended to run on Windows.

If you only need to compile the solution from a non-Windows environment, enable Windows targeting explicitly:

```bash
dotnet build RecipeDownloader.sln -p:EnableWindowsTargeting=true
```

You can also build just the core scraping library:

```bash
dotnet build src/RecipeDownloader.Core/RecipeDownloader.Core.csproj
```

## Run

On Windows, start the application with:

```bash
dotnet run --project src/RecipeDownloader.App/RecipeDownloader.App.csproj
```

## Basic Usage

1. Launch the application.
2. Choose an output directory with **Browse...**.
3. Select a provider from the left sidebar.
4. Click **Discover Recipes** to load the latest recipe list.
5. Select one or more recipes.
6. Click **Download Selected** or **Download All**.
7. Open the downloaded files from your chosen output folder.

The application stores settings in:

- `%LocalAppData%\RecipeDownloader\settings.json`

Cached recipe catalogs are also stored under:

- `%LocalAppData%\RecipeDownloader`

## License

This project is licensed under the BSD 3-Clause License. See [LICENSE](LICENSE).
