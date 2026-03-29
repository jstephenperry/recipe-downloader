using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers.BlueApron;

public static class BlueApronScraper
{
    /// <summary>
    /// Parses recipe URLs from Blue Apron's XML sitemap using a streaming reader
    /// to avoid truncation issues with large XML documents.
    /// </summary>
    public static List<Recipe> ParseRecipeUrlsFromSitemap(Stream xmlStream)
    {
        var recipes = new List<Recipe>();

        using var reader = XmlReader.Create(xmlStream);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "loc")
                continue;

            var loc = reader.ReadElementContentAsString();
            if (string.IsNullOrEmpty(loc) || !loc.Contains("/recipes/"))
                continue;

            var slug = loc.Split('/').Last();
            var name = slug.Replace("-", " ");
            name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
            recipes.Add(new Recipe(name, loc));
        }

        return recipes;
    }

    /// <summary>
    /// Quickly checks whether a recipe page has populated ingredient data.
    /// Used during discovery to exclude recipes without structured ingredients.
    /// </summary>
    public static bool PageHasIngredients(string html)
    {
        var data = ParseRecipeDataFromPage(html, "");
        return data is not null && data.Ingredients.Count > 0;
    }

    /// <summary>
    /// Extracts recipe data from a Blue Apron recipe page.
    /// The data is embedded in a React Query cache via window["__RQ_R_..."].push({...}) calls.
    /// </summary>
    public static RecipeData? ParseRecipeDataFromPage(string html, string sourceUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var scripts = doc.DocumentNode.SelectNodes("//script");
        if (scripts is null)
            return null;

        foreach (var script in scripts)
        {
            var text = script.InnerText;
            if (!text.Contains("__RQ_R_"))
                continue;

            // The page uses: window["__RQ_R_xxx_"].push({...});
            // Each push call contains an object with "mutations" and "queries" arrays.
            // We extract the JSON argument using balanced-brace matching.
            foreach (var jsonObj in ExtractPushPayloads(text))
            {
                var result = TryExtractFromPushPayload(jsonObj, sourceUrl);
                if (result is not null)
                    return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds all .push({...}) calls in the script text and extracts the JSON object
    /// argument using balanced brace counting (safe for deeply nested JSON).
    /// </summary>
    private static IEnumerable<string> ExtractPushPayloads(string text)
    {
        const string marker = ".push(";
        var searchFrom = 0;

        while (searchFrom < text.Length)
        {
            var pushIdx = text.IndexOf(marker, searchFrom, StringComparison.Ordinal);
            if (pushIdx < 0)
                yield break;

            var braceStart = pushIdx + marker.Length;
            if (braceStart >= text.Length || text[braceStart] != '{')
            {
                searchFrom = braceStart;
                continue;
            }

            // Balance braces to find the matching closing brace
            var depth = 0;
            var inString = false;
            var escape = false;
            var end = -1;

            for (var i = braceStart; i < text.Length; i++)
            {
                var c = text[i];

                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }

            if (end > braceStart)
            {
                yield return text[braceStart..(end + 1)];
                searchFrom = end + 1;
            }
            else
            {
                searchFrom = braceStart + 1;
            }
        }
    }

    private static RecipeData? TryExtractFromPushPayload(string json, string sourceUrl)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // The pushed object has { "mutations": [], "queries": [...] }
            if (!root.TryGetProperty("queries", out var queries) || queries.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var query in queries.EnumerateArray())
            {
                if (!query.TryGetProperty("queryKey", out var qk) || qk.ValueKind != JsonValueKind.Array)
                    continue;

                var isFetchProduct = false;
                foreach (var key in qk.EnumerateArray())
                {
                    if (key.GetString() == "fetchProduct")
                    {
                        isFetchProduct = true;
                        break;
                    }
                }

                if (!isFetchProduct)
                    continue;

                if (!query.TryGetProperty("state", out var state) ||
                    !state.TryGetProperty("data", out var data))
                    continue;

                data.TryGetProperty("product", out var productData);
                data.TryGetProperty("item", out var itemData);

                return BuildRecipeData(productData, itemData, sourceUrl);
            }
        }
        catch
        {
            // JSON parsing failed for this push call — skip it
        }

        return null;
    }

    private static RecipeData BuildRecipeData(JsonElement productData, JsonElement itemData, string sourceUrl)
    {
        var subtitle = GetString(productData, "subtitle");
        // Suppress placeholder subtitles
        if (subtitle is "N/A" or "n/a" or "" or null)
            subtitle = null;

        var recipe = new RecipeData
        {
            Provider = "Blue Apron",
            SourceUrl = sourceUrl,
            ScrapedAt = DateTimeOffset.Now,
            Title = GetString(productData, "name") ?? "Unknown Recipe",
            Subtitle = subtitle,
            Description = GetString(productData, "description"),
            ActiveCookTimeMinutes = GetPositiveInt(productData, "active_cook_time"),
            TotalCookTimeMinutes = GetPositiveInt(productData, "total_cook_time"),
            ServingsDisplay = NullIfEmpty(GetString(productData, "servings_display")),
            CuisineType = GetString(productData, "cuisine_type"),
            SpiceLevel = GetString(productData, "spice_level"),
            FeaturedImageUrl = GetImageUrl(productData, "featured_image"),
            ThumbnailImageUrl = GetImageUrl(productData, "thumbnail_image"),
        };

        // Ingredient and nutrition images from item
        if (itemData.ValueKind != JsonValueKind.Undefined)
        {
            recipe.IngredientImageUrl = GetImageUrl(itemData, "ingredient_image");
            recipe.NutritionImageUrl = GetImageUrl(itemData, "nutrition_image");
        }

        // Diet codes
        if (productData.TryGetProperty("diet_codes", out var dietCodes) && dietCodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var dc in dietCodes.EnumerateArray())
            {
                var s = dc.GetString();
                if (s is not null) recipe.DietCodes.Add(s);
            }
        }

        // Protein types
        if (productData.TryGetProperty("protein_types", out var proteins) && proteins.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in proteins.EnumerateArray())
            {
                var s = p.GetString();
                if (s is not null) recipe.ProteinTypes.Add(s);
            }
        }

        // Ingredients from item — skip entries with empty names
        if (itemData.ValueKind != JsonValueKind.Undefined &&
            itemData.TryGetProperty("ingredients", out var ingredients) &&
            ingredients.ValueKind == JsonValueKind.Array)
        {
            foreach (var ing in ingredients.EnumerateArray())
            {
                var name = GetString(ing, "name") ?? "";
                var qty = GetString(ing, "quantity_unicode_str") ?? "";
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(qty))
                    continue; // skip empty ingredient entries

                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Name = name,
                    DisplayText = $"{qty} {name}".Trim()
                });
            }
        }

        // Preparation steps from item — clean up titles
        if (itemData.ValueKind != JsonValueKind.Undefined &&
            itemData.TryGetProperty("preparation_steps", out var steps) &&
            steps.ValueKind == JsonValueKind.Array)
        {
            var stepNumber = 1;
            foreach (var step in steps.EnumerateArray())
            {
                recipe.Steps.Add(new RecipeStep
                {
                    StepNumber = stepNumber++,
                    Title = CleanStepTitle(GetString(step, "title")),
                    Instruction = StripHtml(GetString(step, "instruction") ?? ""),
                    ImageUrl = GetImageUrl(step, "image")
                });
            }
        }

        // Nutrition from item — only create if there's meaningful data
        if (itemData.ValueKind != JsonValueKind.Undefined)
        {
            var cal = GetPositiveInt(itemData, "calories_per_serving");
            var protein = GetPositiveInt(itemData, "grams_of_protein_per_serving");
            var fiber = GetPositiveInt(itemData, "grams_of_fiber_per_serving");

            if (cal.HasValue || protein.HasValue || fiber.HasValue)
            {
                recipe.Nutrition = new RecipeNutrition
                {
                    CaloriesPerServing = cal,
                    ProteinGrams = protein,
                    FiberGrams = fiber,
                };
            }
        }

        return recipe;
    }

    private static string? GetString(JsonElement el, string prop)
    {
        if (el.ValueKind == JsonValueKind.Undefined) return null;
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static int? GetInt(JsonElement el, string prop)
    {
        if (el.ValueKind == JsonValueKind.Undefined) return null;
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
    }

    /// <summary>Returns the int value only if it's greater than zero, otherwise null.</summary>
    private static int? GetPositiveInt(JsonElement el, string prop)
    {
        var val = GetInt(el, prop);
        return val is > 0 ? val : null;
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>
    /// Cleans step titles that contain embedded numbering like "4.\tAdd the vegetables"
    /// or leading/trailing whitespace.
    /// </summary>
    private static string? CleanStepTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        // Remove leading step numbers like "4.\t" or "4. " or "Step 4: "
        title = Regex.Replace(title, @"^(\d+\.?\s*\t?\s*|Step\s+\d+[:.]\s*)", "", RegexOptions.IgnoreCase).Trim();

        // Remove trailing colon if present
        if (title.EndsWith(':'))
            title = title[..^1].Trim();

        return string.IsNullOrWhiteSpace(title) ? null : title;
    }

    private static string? GetImageUrl(JsonElement el, string prop)
    {
        if (el.ValueKind == JsonValueKind.Undefined) return null;
        if (!el.TryGetProperty(prop, out var img)) return null;
        if (img.ValueKind == JsonValueKind.Object)
            return GetString(img, "image_url");
        return null;
    }

    /// <summary>
    /// Converts HTML-formatted instruction text to clean plain text.
    /// Blue Apron embeds &lt;b&gt; tags around ingredient names and &lt;br&gt; tags
    /// for line breaks within their instruction fields.
    /// </summary>
    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        // Use HtmlAgilityPack to properly decode and extract text
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var text = doc.DocumentNode.InnerText;

        // HtmlDecode handles &amp; &lt; etc.
        text = System.Net.WebUtility.HtmlDecode(text);

        // Collapse multiple whitespace/newlines into single spaces
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

}
