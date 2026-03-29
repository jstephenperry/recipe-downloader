using System.Text;
using System.Web;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Export;

/// <summary>
/// Generates self-contained HTML recipe cards from the universal RecipeData model.
/// Produces a two-page front/back layout mimicking HelloFresh PDF recipe cards.
/// Printable as a double-sided single sheet (letter size).
/// </summary>
public static class RecipeHtmlGenerator
{
    public static string Generate(RecipeData recipe)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>{Encode(recipe.Title)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(GetStyles());
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        BuildFrontPage(sb, recipe);
        BuildBackPage(sb, recipe);

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void BuildFrontPage(StringBuilder sb, RecipeData recipe)
    {
        sb.AppendLine("<div class=\"page front\">");

        // Hero image
        if (!string.IsNullOrEmpty(recipe.FeaturedImageUrl))
        {
            sb.AppendLine($"<div class=\"hero\"><img src=\"{Encode(recipe.FeaturedImageUrl)}\" alt=\"{Encode(recipe.Title)}\"></div>");
        }

        // Header: title, subtitle, description
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine($"<h1>{Encode(recipe.Title)}</h1>");
        if (!string.IsNullOrEmpty(recipe.Subtitle))
            sb.AppendLine($"<p class=\"subtitle\">{Encode(recipe.Subtitle)}</p>");
        if (!string.IsNullOrEmpty(recipe.Description))
            sb.AppendLine($"<p class=\"description\">{Encode(recipe.Description)}</p>");

        // Badges
        sb.AppendLine("<div class=\"meta\">");
        if (recipe.TotalCookTimeMinutes.HasValue)
            sb.AppendLine($"<span class=\"badge\"><span class=\"badge-icon\">&#9202;</span> {recipe.TotalCookTimeMinutes} min</span>");
        if (!string.IsNullOrEmpty(recipe.Difficulty))
            sb.AppendLine($"<span class=\"badge\">{Encode(recipe.Difficulty)}</span>");
        if (!string.IsNullOrEmpty(recipe.ServingsDisplay))
            sb.AppendLine($"<span class=\"badge\">{Encode(recipe.ServingsDisplay)}</span>");
        if (recipe.Nutrition?.CaloriesPerServing.HasValue == true)
            sb.AppendLine($"<span class=\"badge\">{recipe.Nutrition.CaloriesPerServing} cal</span>");
        if (!string.IsNullOrEmpty(recipe.CuisineType))
            sb.AppendLine($"<span class=\"badge\">{Encode(recipe.CuisineType)}</span>");
        sb.AppendLine("</div>"); // meta
        sb.AppendLine("</div>"); // header

        // Ingredients — 2-column grid or fallback image
        if (recipe.Ingredients.Count > 0)
        {
            sb.AppendLine("<div class=\"ingredients-section\">");
            sb.AppendLine("<h2>Ingredients</h2>");
            sb.AppendLine("<div class=\"ingredients-grid\">");
            foreach (var ing in recipe.Ingredients)
            {
                var display = !string.IsNullOrEmpty(ing.DisplayText)
                    ? ing.DisplayText
                    : $"{ing.Quantity} {ing.Unit} {ing.Name}".Trim();
                sb.AppendLine($"<div class=\"ingredient\"><span class=\"check\"></span>{Encode(display)}</div>");
            }
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
        }
        else if (!string.IsNullOrEmpty(recipe.IngredientImageUrl))
        {
            // Fallback: show the ingredient photo when no structured data is available
            sb.AppendLine("<div class=\"ingredients-section\">");
            sb.AppendLine("<h2>Ingredients</h2>");
            sb.AppendLine($"<img class=\"ingredient-img\" src=\"{Encode(recipe.IngredientImageUrl)}\" alt=\"Ingredients\">");
            sb.AppendLine("</div>");
        }

        // Tags / diet / allergens
        if (recipe.Tags.Count > 0 || recipe.Allergens.Count > 0 || recipe.DietCodes.Count > 0 || recipe.ProteinTypes.Count > 0)
        {
            sb.AppendLine("<div class=\"tags-bar\">");
            foreach (var tag in recipe.DietCodes) sb.AppendLine($"<span class=\"tag diet\">{Encode(tag)}</span>");
            foreach (var tag in recipe.ProteinTypes) sb.AppendLine($"<span class=\"tag\">{Encode(tag)}</span>");
            foreach (var tag in recipe.Tags) sb.AppendLine($"<span class=\"tag\">{Encode(tag)}</span>");
            foreach (var a in recipe.Allergens) sb.AppendLine($"<span class=\"tag allergen\">{Encode(a)}</span>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div>"); // page front
    }

    private static void BuildBackPage(StringBuilder sb, RecipeData recipe)
    {
        sb.AppendLine("<div class=\"page back\">");

        // Steps — 2-column grid
        if (recipe.Steps.Count > 0)
        {
            sb.AppendLine("<div class=\"steps-header\"><h2>Instructions</h2></div>");
            sb.AppendLine("<div class=\"steps-grid\">");
            foreach (var step in recipe.Steps.OrderBy(s => s.StepNumber))
            {
                sb.AppendLine("<div class=\"step-card\">");
                sb.AppendLine("<div class=\"step-top\">");
                sb.AppendLine($"<span class=\"step-num\">{step.StepNumber}</span>");
                if (!string.IsNullOrEmpty(step.Title))
                    sb.AppendLine($"<span class=\"step-title\">{Encode(step.Title)}</span>");
                sb.AppendLine("</div>");

                if (!string.IsNullOrEmpty(step.ImageUrl))
                    sb.AppendLine($"<img class=\"step-img\" src=\"{Encode(step.ImageUrl)}\" alt=\"Step {step.StepNumber}\">");

                sb.AppendLine($"<p class=\"step-text\">{Encode(step.Instruction)}</p>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
        }

        // Nutrition — text data or fallback image
        if (recipe.Nutrition is { } n && n.CaloriesPerServing.HasValue)
        {
            sb.AppendLine("<div class=\"nutrition-bar\">");
            sb.AppendLine("<span class=\"nutrition-title\">Nutrition (per serving)</span>");
            sb.AppendLine($"<span class=\"nut\">Calories: <b>{n.CaloriesPerServing}</b></span>");
            if (n.ProteinGrams.HasValue) sb.AppendLine($"<span class=\"nut\">Protein: <b>{n.ProteinGrams}g</b></span>");
            if (n.FatGrams.HasValue) sb.AppendLine($"<span class=\"nut\">Fat: <b>{n.FatGrams}g</b></span>");
            if (n.CarbGrams.HasValue) sb.AppendLine($"<span class=\"nut\">Carbs: <b>{n.CarbGrams}g</b></span>");
            if (n.FiberGrams.HasValue) sb.AppendLine($"<span class=\"nut\">Fiber: <b>{n.FiberGrams}g</b></span>");
            if (n.SodiumMg.HasValue) sb.AppendLine($"<span class=\"nut\">Sodium: <b>{n.SodiumMg}mg</b></span>");
            if (n.SugarGrams.HasValue) sb.AppendLine($"<span class=\"nut\">Sugar: <b>{n.SugarGrams}g</b></span>");
            sb.AppendLine("</div>");
        }
        else if (!string.IsNullOrEmpty(recipe.NutritionImageUrl))
        {
            sb.AppendLine("<div class=\"nutrition-bar\">");
            sb.AppendLine("<span class=\"nutrition-title\">Nutrition</span>");
            sb.AppendLine($"<img class=\"nutrition-img\" src=\"{Encode(recipe.NutritionImageUrl)}\" alt=\"Nutrition Facts\">");
            sb.AppendLine("</div>");
        }

        // Footer
        sb.AppendLine("<div class=\"footer\">");
        sb.AppendLine($"<span>Source: <a href=\"{Encode(recipe.SourceUrl)}\">{Encode(recipe.Provider)}</a></span>");
        sb.AppendLine($"<span>{recipe.ScrapedAt:yyyy-MM-dd}</span>");
        sb.AppendLine("</div>");

        sb.AppendLine("</div>"); // page back
    }

    private static string Encode(string? value) => HttpUtility.HtmlEncode(value ?? "");

    private static string GetStyles() => """
        /* === Reset & Base === */
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background: #e0e0e0; color: #333; line-height: 1.5;
        }

        /* === Page sizing — letter (8.5 x 11 in) === */
        .page {
            width: 8.5in; height: 11in;
            margin: 20px auto; padding: 0;
            background: #fff;
            box-shadow: 0 2px 12px rgba(0,0,0,0.15);
            overflow: hidden;
            position: relative;
            display: flex; flex-direction: column;
        }
        .front { page-break-after: always; }

        /* === FRONT PAGE === */

        .hero { flex: 0 0 auto; max-height: 4in; overflow: hidden; }
        .hero img { width: 100%; height: 4in; object-fit: cover; display: block; }

        .header { padding: 16px 28px 12px; }
        h1 { font-size: 22px; color: #1a1a1a; line-height: 1.2; }
        .subtitle { font-size: 14px; color: #666; margin-top: 2px; }
        .description { font-size: 12px; color: #777; margin-top: 6px; line-height: 1.4; }
        .meta { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 10px; }
        .badge {
            background: #f0f0f0; padding: 3px 10px; border-radius: 14px;
            font-size: 11px; color: #555; font-weight: 600;
            display: inline-flex; align-items: center; gap: 3px;
        }
        .badge-icon { font-size: 13px; }

        .ingredients-section { padding: 12px 28px; flex: 1 1 auto; overflow: hidden; }
        .ingredient-img { width: 100%; max-height: 3in; object-fit: contain; border-radius: 6px; }
        .ingredients-section h2 {
            font-size: 15px; color: #1a1a1a; margin-bottom: 8px;
            padding-bottom: 4px; border-bottom: 2px solid #91C788;
        }
        .ingredients-grid {
            display: grid; grid-template-columns: 1fr 1fr;
            gap: 2px 20px;
        }
        .ingredient {
            font-size: 11.5px; padding: 3px 0;
            border-bottom: 1px solid #f0f0f0;
            display: flex; align-items: baseline; gap: 6px;
        }
        .check {
            display: inline-block; width: 10px; height: 10px; flex-shrink: 0;
            border: 1.5px solid #91C788; border-radius: 2px; margin-top: 2px;
        }

        .tags-bar {
            padding: 8px 28px; display: flex; flex-wrap: wrap; gap: 4px;
            border-top: 1px solid #eee; margin-top: auto;
        }
        .tag {
            background: #e3f2fd; color: #1565c0; padding: 2px 8px;
            border-radius: 10px; font-size: 10px; font-weight: 500;
        }
        .tag.diet { background: #e8f5e9; color: #2e7d32; }
        .tag.allergen { background: #fff3e0; color: #e65100; }

        /* === BACK PAGE === */

        .steps-header { padding: 16px 28px 0; }
        .steps-header h2 {
            font-size: 16px; color: #1a1a1a;
            padding-bottom: 6px; border-bottom: 2px solid #91C788;
        }

        .steps-grid {
            display: grid; grid-template-columns: 1fr 1fr;
            gap: 8px; padding: 10px 28px; flex: 1 1 auto;
        }
        .step-card {
            border: 1px solid #eee; border-radius: 6px;
            padding: 8px; overflow: hidden;
        }
        .step-top {
            display: flex; align-items: center; gap: 8px; margin-bottom: 4px;
        }
        .step-num {
            background: #91C788; color: #fff; width: 22px; height: 22px;
            border-radius: 50%; display: inline-flex; align-items: center;
            justify-content: center; font-weight: 700; font-size: 12px;
            flex-shrink: 0;
        }
        .step-title { font-weight: 600; font-size: 11.5px; color: #1a1a1a; }
        .step-img {
            width: 100%; height: 120px; object-fit: cover;
            border-radius: 4px; margin-bottom: 4px;
        }
        .step-text { font-size: 10.5px; color: #444; line-height: 1.45; }

        .nutrition-bar {
            margin: 0 28px; padding: 8px 14px;
            background: #f8f9fa; border-radius: 6px;
            display: flex; flex-wrap: wrap; align-items: center; gap: 12px;
            border: 1px solid #eee;
        }
        .nutrition-title {
            font-weight: 700; font-size: 11px; color: #1a1a1a; margin-right: 4px;
        }
        .nut { font-size: 10.5px; color: #555; }
        .nut b { color: #333; }
        .nutrition-img { max-height: 1.2in; object-fit: contain; border-radius: 4px; }

        .footer {
            padding: 8px 28px; display: flex; justify-content: space-between;
            font-size: 9px; color: #aaa; margin-top: auto;
        }
        .footer a { color: #aaa; text-decoration: none; }

        /* === PRINT === */
        @page { size: letter; margin: 0; }
        @media print {
            body { background: #fff; }
            .page { box-shadow: none; margin: 0; width: 100%; height: 100%; }
        }
        """;
}
