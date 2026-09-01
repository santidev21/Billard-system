using System.Globalization;
using System.Text;
using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class Tenant : Entity
{
    private Tenant()
    {
    }

    public Tenant(string name)
    {
        Name = name;
        Slug = GenerateSlug(name);
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    public void Rename(string name)
    {
        Name = name;
        Slug = GenerateSlug(name);
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    private static string GenerateSlug(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        var slug = sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace(' ', '-')
            .Replace('_', '-');

        var result = new StringBuilder(slug.Length);
        foreach (var c in slug)
        {
            if (char.IsLetterOrDigit(c) || c == '-')
            {
                result.Append(c);
            }
        }

        var finalSlug = result.ToString().Trim('-');
        while (finalSlug.Contains("--"))
        {
            finalSlug = finalSlug.Replace("--", "-");
        }

        return string.IsNullOrEmpty(finalSlug) ? "local" : finalSlug;
    }
}
