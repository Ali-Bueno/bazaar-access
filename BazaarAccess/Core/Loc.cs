using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using TheBazaar.Localization;

namespace BazaarAccess.Core;

/// <summary>
/// The mod's own translations, following the language the player picked in the game.
///
/// Every language ships embedded in the DLL, so the mod can never end up mute for a missing file.
/// A matching file in the "BazaarAccess-lang" folder next to the DLL overrides it key by key, which
/// lets anyone fix a translation without recompiling - and lets a new mod version add keys without
/// invalidating an edited file, because only the keys present on disk are overridden.
///
/// Words that belong to the game's own vocabulary (Shield, Burn, Bronze...) are NOT translated here:
/// see <see cref="GameVocabulary"/>, which reads them from the game so the mod says exactly the same
/// word the card does.
/// </summary>
public static class Loc
{
    private const string FallbackLanguage = "en-US";
    private const string LangFolderName = "BazaarAccess-lang";
    private const string ResourcePrefix = "BazaarAccess.Localization.";

    private static readonly Dictionary<string, string> Strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string _language;

    // The game's preferences may not be loaded yet when the mod wakes up. Until we have read a real
    // language from them we keep retrying, so we don't get stuck on the fallback for the whole session.
    private static bool _languageConfirmed;

    public static string CurrentLanguage => _language ?? FallbackLanguage;

    public static void Initialize()
    {
        LoadInto(Fallback, FallbackLanguage);
        Reload();

        try
        {
            LocalizationService.LocaleChanged += OnGameLocaleChanged;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Could not subscribe to the game's language change: {ex.Message}");
        }
    }

    public static void Shutdown()
    {
        try
        {
            LocalizationService.LocaleChanged -= OnGameLocaleChanged;
        }
        catch
        {
            // Nothing to do on teardown.
        }
    }

    private static void OnGameLocaleChanged()
    {
        _languageConfirmed = false;
        Reload();
        Plugin.Logger.LogInfo($"Language changed, mod texts reloaded: {CurrentLanguage}");
    }

    /// <summary>Translates a key. Unknown keys fall back to English, then to the key itself.</summary>
    public static string T(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        RetryLanguageIfUnconfirmed();

        if (Strings.TryGetValue(key, out string text)) return text;
        if (Fallback.TryGetValue(key, out text)) return text;

        Plugin.Logger.LogWarning($"Missing translation key: {key}");
        return key;
    }

    /// <summary>Translates a key and fills its {0}, {1}... placeholders.</summary>
    public static string T(string key, params object[] args)
    {
        string template = T(key);

        if (args == null || args.Length == 0) return template;

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            // A translation with a broken placeholder must not silence the mod.
            Plugin.Logger.LogWarning($"Bad placeholders in translation '{key}': {template}");
            return template;
        }
    }

    /// <summary>
    /// Translates a count-dependent key, picking "<paramref name="key"/>.one" or ".other" by the
    /// current language's rule. The count is not passed automatically: place it in args where the
    /// sentence needs it.
    /// </summary>
    public static string Plural(string key, int count, params object[] args)
    {
        string suffix = UsesSingular(count) ? ".one" : ".other";
        return T(key + suffix, args);
    }

    // Enough for the nine languages the game ships: some have no singular/plural split at all, and
    // French and Portuguese also treat zero as singular.
    private static bool UsesSingular(int count)
    {
        switch (CurrentLanguage)
        {
            case "zh-CN":
            case "ko-KR":
            case "tr-TR":
                return false;
            case "fr-FR":
            case "pt-BR":
                return count == 0 || count == 1;
            default:
                return count == 1;
        }
    }

    private static void Reload()
    {
        string language = DetectLanguage();

        Strings.Clear();

        // English first, so a partially translated language still answers every key.
        foreach (var pair in Fallback)
            Strings[pair.Key] = pair.Value;

        if (!string.Equals(language, FallbackLanguage, StringComparison.OrdinalIgnoreCase))
            LoadInto(Strings, language);

        _language = language;
    }

    private static void RetryLanguageIfUnconfirmed()
    {
        if (_languageConfirmed) return;

        string language = DetectLanguage();
        if (!_languageConfirmed) return;              // preferences still not ready
        if (string.Equals(language, _language, StringComparison.OrdinalIgnoreCase)) return;

        Reload();
    }

    private static string DetectLanguage()
    {
        try
        {
            string code = PlayerPreferences.Data?.LanguageCode;
            if (!string.IsNullOrEmpty(code))
            {
                _languageConfirmed = true;
                return code;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"Game language not readable yet: {ex.Message}");
        }

        return FallbackLanguage;
    }

    /// <summary>
    /// Loads a language's embedded catalogue, then lets the file on disk override the keys it defines.
    /// </summary>
    private static void LoadInto(Dictionary<string, string> target, string language)
    {
        int embedded = ParseInto(target, ReadEmbedded(language));
        int overrides = ParseInto(target, ReadFromDisk(language));

        if (embedded == 0 && overrides == 0)
            Plugin.Logger.LogWarning($"No translations found for {language}, using English");
        else
            Plugin.Logger.LogInfo($"Loaded {language}: {embedded} key(s), {overrides} overridden from disk");
    }

    private static string ReadEmbedded(string language)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(ResourcePrefix + language + ".txt"))
            {
                if (stream == null) return null;
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Could not read embedded translations for {language}: {ex.Message}");
            return null;
        }
    }

    private static string ReadFromDisk(string language)
    {
        try
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(folder)) return null;

            string path = Path.Combine(Path.Combine(folder, LangFolderName), language + ".txt");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Could not read the translations folder for {language}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses "key = value" lines. '#' starts a comment, "\n" in a value becomes a line break.
    /// Returns how many keys were read.
    /// </summary>
    private static int ParseInto(Dictionary<string, string> target, string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;

        int count = 0;

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            int separator = line.IndexOf('=');
            if (separator <= 0) continue;

            string key = line.Substring(0, separator).Trim();
            if (key.Length == 0) continue;

            string value = line.Substring(separator + 1).Trim().Replace("\\n", "\n");

            target[key] = value;
            count++;
        }

        return count;
    }
}
