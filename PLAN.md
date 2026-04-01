# Recipe Ingredient Matching — Design Plan

## Overview

Add four interconnected features to RecipeDownloader:
1. Identify core proteins across all recipes and let users pick up to 2
2. Find recipe pairs sharing those proteins with overlapping ancillary ingredients
3. Track ingredients the user already has on hand (pantry)
4. Generate an optimized grocery list that minimizes waste

All new logic goes in `RecipeDownloader.Core`; UI additions go in `RecipeDownloader.App`.

---

## Feature 1: Core Protein Selection

### Problem
Users need to browse all discovered recipes by protein and select up to 2 proteins to plan meals around.

### Design

**New file: `Core/Classification/IngredientClassifier.cs`**

Classifies each `RecipeIngredient` into a category:

```csharp
public enum IngredientCategory
{
    Protein,
    Vegetable,
    Starch,
    Dairy,
    Seasoning,   // spices, herbs, sauces, oils
    Other
}
```

Implementation: a static dictionary mapping normalized ingredient name keywords to categories. For example:
- **Protein**: chicken, beef, pork, salmon, shrimp, tofu, turkey, thigh, breast, steak, ground beef, sausage, cod, tilapia
- **Vegetable**: broccoli, zucchini, pepper, onion, carrot, spinach, tomato, green beans, corn, kale, mushroom, lettuce
- **Starch**: rice, pasta, noodle, potato, bread, tortilla, couscous, quinoa, farro, bun
- **Dairy**: cheese, cream, butter, milk, yogurt, sour cream
- **Seasoning**: salt, pepper, garlic, olive oil, soy sauce, paprika, cumin, oregano, vinegar, stock, broth

The classifier scans `ingredient.Name` (falling back to `DisplayText`) against these keyword lists using case-insensitive substring matching. Unknown ingredients fall to `Other`.

> **Why keyword matching over ML/NLP?** The ingredient names from HelloFresh and Blue Apron are already semi-structured (e.g., "Boneless Chicken Thighs", "Jasmine Rice"). A curated keyword list is deterministic, fast, zero-dependency, and easy to extend. It covers the ~80-90% case well; the `Other` bucket catches edge cases without breaking anything.

**New file: `Core/Classification/ProteinAggregator.cs`**

```csharp
public class ProteinAggregator
{
    // Given a collection of RecipeData, returns distinct protein names
    // normalized to a canonical form (e.g., "Chicken Thighs" and
    // "Chicken Breasts" both map to "Chicken").
    public IReadOnlyList<ProteinSummary> GetAvailableProteins(
        IEnumerable<RecipeData> recipes);
}

public record ProteinSummary(
    string CanonicalName,       // e.g. "Chicken"
    int RecipeCount,            // how many recipes use it
    List<string> Variants);     // e.g. ["Chicken Thighs", "Chicken Breasts"]
```

Canonical normalization strips qualifiers ("boneless", "skinless", "ground") and maps to a root protein (chicken, beef, pork, salmon, etc.) via a small lookup table.

**UI: Protein picker (WPF)**

- New view `ProteinPickerView.xaml` — displayed after recipe discovery
- Shows a list/grid of proteins with recipe counts (e.g., "Chicken (47 recipes)")
- Checkboxes, max 2 selected — enforced in `ProteinPickerViewModel`
- "Find Matching Recipes" button proceeds to Feature 2

---

## Feature 2: Recipe Pair Matching by Ancillary Overlap

### Problem
Given the user's 2 chosen proteins, find pairs of recipes (one per protein) that share the most ancillary (non-protein) ingredients to reduce total shopping.

### Design

**New file: `Core/Matching/RecipeMatcher.cs`**

```csharp
public class RecipeMatcher
{
    // Returns ranked recipe pairs, scored by ancillary ingredient overlap.
    public IReadOnlyList<RecipePairMatch> FindBestPairs(
        IEnumerable<RecipeData> allRecipes,
        string protein1,
        string protein2,
        PantryInventory? pantry = null);  // optional, used in Feature 3
}

public record RecipePairMatch(
    RecipeData Recipe1,
    RecipeData Recipe2,
    IReadOnlyList<string> SharedIngredients,   // normalized names
    IReadOnlyList<string> UniqueToRecipe1,
    IReadOnlyList<string> UniqueToRecipe2,
    double OverlapScore);
```

**Matching algorithm:**

1. Partition recipes into two pools: those containing protein1, those containing protein2 (a recipe can appear in both if it has both proteins).
2. For each recipe, build a set of normalized ancillary ingredient names (everything classified as non-Protein).
3. For every (r1, r2) pair across the two pools, compute:
   - `shared = r1_ancillary ∩ r2_ancillary`
   - `overlapScore = |shared| / |r1_ancillary ∪ r2_ancillary|` (Jaccard similarity)
4. Rank pairs by `overlapScore` descending. Return top N (default 10).

**Category-aware sub-scoring (stretch refinement):**
Optionally weight overlaps by category — vegetable and starch overlaps are more valuable than seasoning overlaps (seasonings are small/cheap and often already on hand). Initial weights:
- Vegetable overlap: 3x
- Starch overlap: 3x
- Dairy overlap: 2x
- Seasoning overlap: 0.5x
- Other overlap: 1x

**Ingredient normalization (`Core/Classification/IngredientNormalizer.cs`):**

A critical supporting utility. Normalizes ingredient names for reliable set comparison:
- Lowercase
- Strip qualifiers: "boneless", "skinless", "fresh", "dried", "large", "small", "medium"
- Singularize simple plurals (tomatoes → tomato, peppers → pepper)
- Trim whitespace

Example: `"Fresh Baby Spinach"` → `"baby spinach"`, `"Red Bell Peppers"` → `"red bell pepper"`

**UI: Match results view**

- New view `RecipeMatchView.xaml`
- Displays ranked recipe pairs as cards
- Each card shows: recipe titles, shared ingredients (highlighted), unique ingredients per recipe
- Selecting a pair proceeds to the grocery list (Feature 4)

---

## Feature 3: Pantry Inventory (Ingredients on Hand)

### Problem
Users want to track what they already have so the grocery list only includes what they need to buy.

### Design

**New file: `Core/Models/PantryItem.cs`**

```csharp
public class PantryItem
{
    public string Name { get; set; } = "";           // normalized
    public string? DisplayName { get; set; }         // user-entered original
    public string? Quantity { get; set; }             // e.g., "2"
    public string? Unit { get; set; }                 // e.g., "lb"
    public DateTimeOffset AddedAt { get; set; }
}
```

**New file: `Core/Storage/PantryStore.cs`**

```csharp
public class PantryStore
{
    // Persists to %LocalAppData%\RecipeDownloader\pantry.json
    public Task<PantryInventory> LoadAsync(CancellationToken ct = default);
    public Task SaveAsync(PantryInventory inventory, CancellationToken ct = default);
}

public class PantryInventory
{
    public List<PantryItem> Items { get; set; } = [];
    public DateTimeOffset LastUpdated { get; set; }

    // Quick lookup: does the pantry contain this normalized ingredient?
    public bool Contains(string normalizedName);
}
```

Same JSON file pattern as `RecipeCatalogStore`.

**UI: Pantry management**

- New view `PantryView.xaml` — accessible from a tab or sidebar button
- Text input to add ingredients (with optional quantity/unit)
- List of current pantry items with delete buttons
- "Clear All" button
- Autocomplete suggestions drawn from all known ingredient names across discovered recipes

**Integration with Features 2 and 4:**
- `RecipeMatcher.FindBestPairs()` accepts an optional `PantryInventory` to boost pairs where more non-shared ingredients are already on hand
- The grocery list (Feature 4) subtracts pantry items automatically

---

## Feature 4: Optimized Grocery List

### Problem
Given a selected recipe pair and pantry inventory, produce a grocery list that minimizes waste — combining shared ingredients, subtracting pantry items, and consolidating quantities.

### Design

**New file: `Core/GroceryList/GroceryListGenerator.cs`**

```csharp
public class GroceryListGenerator
{
    public GroceryList Generate(
        RecipePairMatch selectedPair,
        PantryInventory pantry);
}

public class GroceryList
{
    public List<GroceryItem> Items { get; set; } = [];
    public List<GroceryItem> AlreadyOnHand { get; set; } = [];    // from pantry
    public int TotalUniqueIngredients { get; set; }
    public int IngredientsFromPantry { get; set; }
    public int SharedBetweenRecipes { get; set; }
}

public class GroceryItem
{
    public string Name { get; set; } = "";
    public string? CombinedQuantity { get; set; }   // merged across recipes
    public string? Unit { get; set; }
    public IngredientCategory Category { get; set; }
    public bool IsShared { get; set; }               // used in both recipes
    public List<string> UsedInRecipes { get; set; } = [];  // recipe titles
}
```

**Waste minimization strategy:**

1. **Quantity consolidation**: When the same ingredient appears in both recipes (e.g., "1 cup rice" + "1 cup rice" = "2 cups rice"), combine quantities. This is the primary waste reducer — buying one larger amount instead of two separate portions.

2. **Unit normalization**: Convert compatible units before combining:
   - oz ↔ lb (16 oz = 1 lb)
   - tsp ↔ tbsp (3 tsp = 1 tbsp)
   - cup ↔ tbsp (16 tbsp = 1 cup)
   - Express final quantity in the most practical unit (e.g., 24 oz → 1.5 lb)

3. **Pantry subtraction**: Remove items the user already has. For quantity-tracked pantry items, subtract and show only the deficit. For pantry items without quantity, assume fully covered.

4. **Category grouping**: Group the final list by `IngredientCategory` for organized shopping (Produce → Meat → Dairy → Dry Goods → Seasonings).

5. **Waste scoring (display only)**: Show a "waste efficiency" metric:
   ```
   efficiency = sharedIngredients / totalUniqueIngredients
   ```
   Higher overlap = less waste. Displayed to the user so they can compare pairs.

**New file: `Core/GroceryList/QuantityParser.cs`**

Parses quantity strings (including Unicode fractions like ½, ¼, ⅓) into decimals for arithmetic, and formats results back to user-friendly strings.

**UI: Grocery list view**

- New view `GroceryListView.xaml`
- Grouped by category with section headers
- Shared ingredients visually marked (e.g., badge or highlight)
- "On hand" items shown struck-through or in a collapsible section
- Summary stats: "X items to buy, Y already on hand, Z shared between recipes"
- Print/export button (reuse the HTML generation pattern from `RecipeHtmlGenerator`)

---

## New Files Summary

### Core project (`RecipeDownloader.Core`)

| File | Purpose |
|------|---------|
| `Classification/IngredientCategory.cs` | Enum for ingredient categories |
| `Classification/IngredientClassifier.cs` | Keyword-based ingredient classification |
| `Classification/IngredientNormalizer.cs` | Name normalization for matching |
| `Classification/ProteinAggregator.cs` | Extract/group distinct proteins from recipes |
| `Matching/RecipeMatcher.cs` | Jaccard-based recipe pair scoring |
| `Matching/RecipePairMatch.cs` | Match result record |
| `Models/PantryItem.cs` | Pantry item model |
| `Storage/PantryStore.cs` | JSON persistence for pantry |
| `GroceryList/GroceryListGenerator.cs` | Builds optimized grocery list |
| `GroceryList/GroceryItem.cs` | Grocery item model |
| `GroceryList/GroceryList.cs` | Grocery list model |
| `GroceryList/QuantityParser.cs` | Fraction/unit parsing and arithmetic |

### App project (`RecipeDownloader.App`)

| File | Purpose |
|------|---------|
| `ViewModels/ProteinPickerViewModel.cs` | Protein selection (max 2) |
| `ViewModels/RecipeMatchViewModel.cs` | Display matched recipe pairs |
| `ViewModels/PantryViewModel.cs` | Pantry CRUD |
| `ViewModels/GroceryListViewModel.cs` | Grocery list display + export |
| `Views/ProteinPickerView.xaml` | Protein selection UI |
| `Views/RecipeMatchView.xaml` | Match results UI |
| `Views/PantryView.xaml` | Pantry management UI |
| `Views/GroceryListView.xaml` | Grocery list UI |

---

## Modified Files

| File | Change |
|------|--------|
| `Core/Models/RecipeData.cs` | No changes — existing model is sufficient |
| `App/ViewModels/MainViewModel.cs` | Add navigation to new views, wire up pantry store |
| `App/Views/MainWindow.xaml` | Add navigation buttons/tabs for Pantry and Meal Planner |
| `App/App.xaml.cs` | Initialize new stores and view models |
| `App/Resources/Styles.xaml` | Add styles for new UI components |

---

## Data Flow

```
[Discovery] → RecipeData[] (existing)
      │
      ▼
[IngredientClassifier] → classifies each ingredient
      │
      ▼
[ProteinAggregator] → ProteinSummary[] → UI: pick 2
      │
      ▼
[RecipeMatcher] → RecipePairMatch[] (ranked) → UI: pick a pair
      │                    ▲
      │              PantryInventory (optional boost)
      ▼
[GroceryListGenerator] → GroceryList → UI: shopping list
      ▲
      │
PantryInventory (subtract on-hand items)
```

---

## Implementation Order

1. **`IngredientNormalizer`** + **`IngredientClassifier`** — foundation, no dependencies
2. **`ProteinAggregator`** — depends on classifier
3. **`PantryStore`** + **`PantryItem`** — independent of above, can parallelize
4. **`RecipeMatcher`** — depends on classifier + normalizer
5. **`QuantityParser`** — independent utility
6. **`GroceryListGenerator`** — depends on matcher + pantry + quantity parser
7. **UI views** — after core logic is solid, build in order: Pantry → Protein Picker → Match Results → Grocery List

---

## Key Design Decisions

1. **Keyword-based classification over ML**: Deterministic, fast, zero external dependencies. Recipe ingredient names from meal-kit providers are already well-structured. Easy to extend the keyword dictionary.

2. **Jaccard similarity for matching**: Simple, well-understood metric. Category weighting adds nuance without complexity. O(n×m) pair comparison is fine for typical catalog sizes (~100-500 recipes per provider).

3. **Separate PantryStore**: Pantry is user-specific state, independent of provider catalogs. Deserves its own persistence file and lifecycle.

4. **Quantity consolidation as primary waste reducer**: The biggest source of waste is buying duplicate small quantities of the same ingredient. Combining them into a single purchase is the highest-impact optimization.

5. **All new logic in Core**: Keeps it testable and UI-independent. The WPF app just wires up view models to Core services.
