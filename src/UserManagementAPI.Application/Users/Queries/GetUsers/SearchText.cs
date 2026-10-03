using System.Globalization;
using System.Text;

namespace UserManagementAPI.Application.Users.Queries.GetUsers;

/// <summary>
/// What the user search treats as the same text: lower case, with diacritics
/// removed, so "petrovic" finds Petrović and "dordevic" finds Đorđević
/// (ADR 0020).
///
/// Against the database, EF Core translates <see cref="Fold"/> to PostgreSQL's
/// lower(unaccent(...)) — the mapping is registered in AppDbContext — so the
/// folding happens on the column, in SQL. The body below runs when the same
/// LINQ runs in memory: it drops combining marks after Unicode decomposition
/// and maps đ, which has no decomposition, to d. That covers the Latin letters
/// the data holds; unaccent's rule file covers more, and the two agree on these
/// letters, not beyond them.
/// </summary>
public static class SearchText
{
    public static string Fold(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            folded.Append(character == 'đ' ? 'd' : character);
        }

        return folded.ToString().Normalize(NormalizationForm.FormC);
    }
}