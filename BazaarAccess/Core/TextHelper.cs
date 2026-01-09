using System.Text.RegularExpressions;

namespace BazaarAccess.Core;

/// <summary>
/// Utilidades para limpiar y formatear texto.
/// </summary>
public static class TextHelper
{
    // Regex para tags HTML comunes en Unity/TextMeshPro
    private static readonly Regex HtmlTagsRegex = new Regex(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex para tags específicos de TextMeshPro
    private static readonly Regex TmpTagsRegex = new Regex(
        @"<(color|size|b|i|u|s|sup|sub|sprite|link|font|material|quad|margin|mark|noparse|nobr|indent|line-height|line-indent|cspace|mspace|pos|voffset|width|style|rotate|allcaps|smallcaps|lowercase|uppercase|align|alpha|gradient)=[^>]*>|</(color|size|b|i|u|s|sup|sub|sprite|link|font|material|quad|margin|mark|noparse|nobr|indent|line-height|line-indent|cspace|mspace|pos|voffset|width|style|rotate|allcaps|smallcaps|lowercase|uppercase|align|alpha|gradient)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Limpia texto de tags HTML y de formato (br, color, etc).
    /// </summary>
    public static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Reemplazar <br>, <br/>, <br /> con espacio o salto
        text = Regex.Replace(text, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);

        // Eliminar todos los tags HTML/TMP
        text = HtmlTagsRegex.Replace(text, string.Empty);

        // Limpiar espacios múltiples
        text = Regex.Replace(text, @"\s+", " ");

        // Trim
        text = text.Trim();

        return text;
    }

    /// <summary>
    /// Limpia solo tags de formato pero preserva el texto.
    /// </summary>
    public static string CleanFormatTags(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Reemplazar <br> con newline
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

        // Eliminar tags de formato específicos de TMP
        text = TmpTagsRegex.Replace(text, string.Empty);

        // Eliminar cualquier tag restante
        text = HtmlTagsRegex.Replace(text, string.Empty);

        // Limpiar espacios múltiples pero preservar newlines
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n\s+", "\n");
        text = Regex.Replace(text, @"\s+\n", "\n");

        return text.Trim();
    }
}
