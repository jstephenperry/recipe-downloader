namespace RecipeDownloader.Core.Models;

public record Recipe(
    string Name,
    string SourceUrl,
    string? PdfUrl = null,
    DateTimeOffset? DiscoveredAt = null);
