namespace RecipeDownloader.Core.Models;

/// <summary>
/// Universal recipe data model. All providers normalize their data into this structure.
/// Serialized to JSON alongside an HTML recipe card for non-PDF providers.
/// </summary>
public class RecipeData
{
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string Provider { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DateTimeOffset ScrapedAt { get; set; } = DateTimeOffset.Now;

    // Timing
    public int? ActiveCookTimeMinutes { get; set; }
    public int? TotalCookTimeMinutes { get; set; }
    public string? Difficulty { get; set; }

    // Servings
    public string? ServingsDisplay { get; set; }

    // Classification
    public string? CuisineType { get; set; }
    public List<string> DietCodes { get; set; } = [];
    public List<string> ProteinTypes { get; set; } = [];
    public string? SpiceLevel { get; set; }

    // Ingredients
    public List<RecipeIngredient> Ingredients { get; set; } = [];

    // Steps
    public List<RecipeStep> Steps { get; set; } = [];

    // Nutrition
    public RecipeNutrition? Nutrition { get; set; }

    // Images
    public string? FeaturedImageUrl { get; set; }
    public string? ThumbnailImageUrl { get; set; }
    public string? IngredientImageUrl { get; set; }
    public string? NutritionImageUrl { get; set; }

    // Tags / allergens
    public List<string> Tags { get; set; } = [];
    public List<string> Allergens { get; set; } = [];
}

public class RecipeIngredient
{
    public string Name { get; set; } = "";
    public string? Quantity { get; set; }
    public string? Unit { get; set; }
    /// <summary>Raw display string, e.g. "12 oz Boneless Chicken Thighs"</summary>
    public string? DisplayText { get; set; }
}

public class RecipeStep
{
    public int StepNumber { get; set; }
    public string? Title { get; set; }
    public string Instruction { get; set; } = "";
    public string? ImageUrl { get; set; }
}

public class RecipeNutrition
{
    public int? CaloriesPerServing { get; set; }
    public int? ProteinGrams { get; set; }
    public int? FiberGrams { get; set; }
    public int? FatGrams { get; set; }
    public int? CarbGrams { get; set; }
    public int? SodiumMg { get; set; }
    public int? SugarGrams { get; set; }
}
