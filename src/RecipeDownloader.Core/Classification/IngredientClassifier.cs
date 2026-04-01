using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Classification;

/// <summary>
/// Classifies recipe ingredients into categories using keyword matching.
/// </summary>
public static class IngredientClassifier
{
    private static readonly Dictionary<string, IngredientCategory> KeywordMap = BuildKeywordMap();

    public static IngredientCategory Classify(RecipeIngredient ingredient)
    {
        var name = ingredient.Name;
        if (string.IsNullOrWhiteSpace(name))
            name = ingredient.DisplayText ?? "";

        var normalized = name.ToLowerInvariant();

        // Check exact and substring matches against keyword map
        // Longer keywords first to prefer specific matches (e.g., "sour cream" over "cream")
        foreach (var (keyword, category) in KeywordMap.OrderByDescending(kv => kv.Key.Length))
        {
            if (normalized.Contains(keyword, StringComparison.Ordinal))
                return category;
        }

        return IngredientCategory.Other;
    }

    public static IngredientCategory Classify(string normalizedName)
    {
        var lower = normalizedName.ToLowerInvariant();

        foreach (var (keyword, category) in KeywordMap.OrderByDescending(kv => kv.Key.Length))
        {
            if (lower.Contains(keyword, StringComparison.Ordinal))
                return category;
        }

        return IngredientCategory.Other;
    }

    private static Dictionary<string, IngredientCategory> BuildKeywordMap()
    {
        var map = new Dictionary<string, IngredientCategory>(StringComparer.Ordinal);

        // Proteins
        foreach (var kw in new[]
        {
            "chicken", "beef", "pork", "steak", "salmon", "shrimp", "prawn",
            "turkey", "lamb", "duck", "veal", "bison", "sausage", "bacon",
            "cod", "tilapia", "tuna", "mahi", "halibut", "trout", "catfish",
            "scallop", "crab", "lobster", "mussel", "clam", "anchov",
            "tofu", "tempeh", "seitan", "beyond", "impossible",
            "thigh", "breast", "tenderloin", "sirloin", "ribeye", "filet",
            "ground meat", "meatball", "chorizo", "pepperoni", "ham",
            "egg", "lentil", "chickpea", "black bean", "kidney bean",
            "white bean", "cannellini", "edamame"
        })
            map[kw] = IngredientCategory.Protein;

        // Vegetables
        foreach (var kw in new[]
        {
            "broccoli", "zucchini", "squash", "pepper", "bell pepper",
            "onion", "carrot", "spinach", "kale", "lettuce", "arugula",
            "tomato", "cucumber", "celery", "mushroom", "corn",
            "asparagus", "green bean", "snap pea", "snow pea",
            "cabbage", "cauliflower", "eggplant", "artichoke",
            "beet", "radish", "turnip", "sweet potato", "bok choy",
            "brussels sprout", "edamame", "fennel", "leek",
            "scallion", "green onion", "jalapeno", "poblano", "serrano",
            "avocado", "olive"
        })
            map.TryAdd(kw, IngredientCategory.Vegetable);

        // Starches
        foreach (var kw in new[]
        {
            "rice", "pasta", "noodle", "spaghetti", "penne", "fettuccine",
            "linguine", "macaroni", "orzo", "rigatoni", "fusilli",
            "potato", "bread", "tortilla", "bun", "roll", "pita",
            "couscous", "quinoa", "farro", "barley", "bulgur",
            "flour", "cornstarch", "panko", "breadcrumb",
            "gnocchi", "polenta", "grits", "oat"
        })
            map.TryAdd(kw, IngredientCategory.Starch);

        // Dairy
        foreach (var kw in new[]
        {
            "cheese", "cream cheese", "sour cream", "heavy cream",
            "cream", "butter", "milk", "yogurt", "mozzarella",
            "parmesan", "cheddar", "feta", "gouda", "ricotta",
            "monterey jack", "provolone", "swiss", "gruyere",
            "crema", "half-and-half", "whipping cream", "ghee"
        })
            map.TryAdd(kw, IngredientCategory.Dairy);

        // Seasonings (spices, herbs, sauces, oils, condiments)
        foreach (var kw in new[]
        {
            "salt", "pepper flake", "black pepper", "white pepper",
            "garlic", "ginger", "cumin", "paprika", "oregano", "basil",
            "thyme", "rosemary", "cilantro", "parsley", "dill", "chive",
            "cayenne", "cinnamon", "nutmeg", "turmeric", "coriander",
            "chili powder", "curry", "za'atar", "italian seasoning",
            "olive oil", "vegetable oil", "canola oil", "sesame oil",
            "soy sauce", "fish sauce", "hot sauce", "sriracha",
            "worcestershire", "vinegar", "ketchup", "mustard", "mayo",
            "mayonnaise", "honey", "maple syrup", "sugar",
            "stock", "broth", "bouillon",
            "tomato paste", "tomato sauce", "enchilada sauce",
            "teriyaki", "hoisin", "oyster sauce", "mirin",
            "lemon", "lime", "cooking spray"
        })
            map.TryAdd(kw, IngredientCategory.Seasoning);

        return map;
    }
}
