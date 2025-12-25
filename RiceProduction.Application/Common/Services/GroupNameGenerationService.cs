namespace RiceProduction.Application.Common.Services;

public class GroupNameGenerationService
{
    /// <summary>
    /// Generates a structured group name
    /// Format: CLUSTER-SEASON-VARIETY-G##
    /// Example: CLS-W24-JAS-G01
    /// </summary>
    public string GenerateGroupName(
        string clusterName,
        string seasonName,
        int year,
        string varietyName,
        int groupNumber)
    {
        var clusterAbbr = GetAbbreviation(clusterName, 3);
        var seasonAbbr = GetSeasonAbbreviation(seasonName);
        var yearShort = (year % 100).ToString("D2");
        var varietyAbbr = GetAbbreviation(varietyName, 3);

        return $"{clusterAbbr}-{seasonAbbr}{yearShort}-{varietyAbbr}-G{groupNumber:D2}";
    }

    /// <summary>
    /// Gets an abbreviation from a text string
    /// </summary>
    private string GetAbbreviation(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "UNK";

        // Try to get initials from words
        var words = text.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            var initials = string.Join("", words.Select(w => w[0])).ToUpper();
            return initials.Length > maxLength ? initials.Substring(0, maxLength) : initials;
        }

        // Otherwise just take first characters
        return text.Length > maxLength ? text.Substring(0, maxLength).ToUpper() : text.ToUpper();
    }

    /// <summary>
    /// Maps season names to abbreviations
    /// </summary>
    private string GetSeasonAbbreviation(string seasonName)
    {
        // Map common season names to abbreviations
        var seasonMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "winter", "W" },
            { "spring", "SP" },
            { "summer", "SU" },
            { "autumn", "A" },
            { "fall", "F" },
            { "dong", "D" },    // Vietnamese winter
            { "xuan", "X" },    // Vietnamese spring
            { "he", "H" },      // Vietnamese summer
            { "thu", "T" },     // Vietnamese autumn
            { "mua dong", "MD" },
            { "mua xuan", "MX" },
            { "mua he", "MH" },
            { "mua thu", "MT" }
        };

        foreach (var kvp in seasonMap)
        {
            if (seasonName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return GetAbbreviation(seasonName, 2);
    }
}

