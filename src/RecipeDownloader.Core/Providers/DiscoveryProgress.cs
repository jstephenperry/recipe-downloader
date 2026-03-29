namespace RecipeDownloader.Core.Providers;

public record DiscoveryProgress(string Phase, int Current, int Total, string? Detail = null);
