using System.Globalization;
using System.Runtime.CompilerServices;
using dat_sharp_engine.AssetManagement;
using dat_sharp_vfs;
using LanguageExtensions;
using SmartFormat;
using Tomlyn;

namespace dat_sharp_engine.Util;

/// <summary>
/// A class for handling localised strings
/// <para/>
/// Inspired by <a href="https://hotchaigames.medium.com/how-i-localized-my-c-game-5ecf74d40735">How I Localized My C#
/// Game <i>by David Taylor</i></a>
/// </summary>
public static class Localisation {
    /// <summary>The currently selected locale culture</summary>
    public static CultureInfo localeCulture { get; private set; }

    /// <summary>A dictionary of string translations</summary>
    private static readonly Dictionary<string, string> Strings = new();

    public static event UpdateLocaleStrings? LocaleStringsUpdateEvent;

    static Localisation() {}

    /// <summary>
    /// Initialise the Localisation Subsystem
    /// </summary>
    public static void Initialise() {
        SetLocalisation(CultureInfo.GetCultureInfo(EngineCVars.LocaleCVar.value));
        EngineCVars.LocaleCVar.OnChangeEvent += (_, cVar) => {
            SetLocalisation(CultureInfo.GetCultureInfo(cVar.value));
        };
    }

    /// <summary>
    /// Set the locale used by the translation
    /// </summary>
    /// <param name="culture">The new culture</param>
    private static void SetLocalisation(CultureInfo culture) {
        localeCulture = culture;

        // Load
        Strings.Clear();

        var localeName = $"{culture.Name}.lang";

        // Get the locales to add
        // Note US Locale is always applied first
        var isUs = culture.Name != "en-US";
        List<string> locales = ["engine/locales/en-US.lang"];
        if (isUs) locales.Add($"engine/locales/{localeName}");
        locales.Add("locales/en-US.lang");
        if (isUs) locales.Add($"locales/{localeName}");

        var localeAssets = locales
            .Where(path => AssetManager.instance.GetFileExists(path))
            .Select(path => AssetManager.instance.GetAsset<LocaleAsset>(path, AssetLoadMode.Eager))
            .ToArray();

        // Load all locales
        foreach (var localeAsset in localeAssets) {
            localeAsset.WaitForCpuLoad();
        }

        // Apply all locales in order
        foreach (var localeAsset in localeAssets) {
            Strings.AddRange(localeAsset.localeMap!, true);
        }

        LocaleStringsUpdateEvent?.Invoke(Strings, culture);
    }


    /// <summary>
    /// Localise the given string into the currently configured locale.
    /// </summary>
    /// <param name="localisable">The string to localise</param>
    /// <returns>
    ///     The localised version of the string, or <paramref name="localisable"/> if there doesn't exist a localisation
    /// </returns>
    public static string Localise(this string localisable) {
        return Strings.TryGetValue(localisable, out var value) ? value : localisable;
    }

    /// <summary>
    /// Mark a string that will be indirectly localised in the future.
    /// <para/>
    /// This is used for static analysis to extract translation keys.
    /// </summary>
    /// <param name="localisable">The string to localise</param>
    /// <returns><paramref name="localisable"/></returns>
    public static string WillLocalise(this string localisable) {
        return localisable;
    }

    /// <summary>
    /// Localise the <paramref name="format"/> string then format it as a <see cref="SmartFormat">Smart Format String</see>
    /// </summary>
    /// <param name="format">The string to localise</param>
    /// <param name="args">The arguments for the smart format</param>
    /// <returns>The localised and formatted string</returns>
    public static string SmartFormat(string format, params object?[] args) {
        return Smart.Format(localeCulture, format.Localise(), args);
    }

    /// <summary>
    /// Localise the format component of the <see cref="FormattableString"/>, then process the resulting string as a
    /// smart format using the formattable string's arguments.
    /// <para/>
    /// This cheeky method provides access to <see cref="SmartFormat">Smart Formatting</see> using
    /// <see cref="FormattableString"/> syntax. Care should be taken as some smart formatting features, like dot (".")
    /// syntax will be unavailable, and syntax highlighting may be very wrong (as it will be expecting a regular format
    /// string).
    /// </summary>
    /// <param name="formattable">The formattable string to localise and format</param>
    /// <returns>The localised and formatted string</returns>
    public static string Formattable(FormattableString formattable) {
        return Smart.Format(localeCulture, formattable.Format.Localise(), formattable.GetArguments());
    }

    public delegate void UpdateLocaleStrings(in Dictionary<string, string> strings, CultureInfo newCulture);
}

/// <summary>
/// An asset representing a locale mapping of strings to localised strings
/// </summary>
public class LocaleAsset : Asset {
    /// <summary>The mapping of strings to localised strings</summary>
    public Dictionary<string, string>? localeMap;

    /// <summary>
    /// Create a virtual LocaleAsset
    /// </summary>
    /// <param name="localeMap">The map of strings to localised strings</param>
    public LocaleAsset(Dictionary<string, string> localeMap) {
        this.localeMap = localeMap;
    }
    public LocaleAsset(string? path, DVfsFile? file) : base(path, file) { }

    protected override void CpuLoadAssetImpl(Stream assetData) {
        using TextReader reader = new StreamReader(assetData);

        var lang = Toml.ToModel(reader.ReadToEnd());
        localeMap = new Dictionary<string, string>(lang.Count);
        foreach (var (key, value) in lang) {
            localeMap.Add(key, (string) value);
        }
    }

    protected override void CpuUnloadAssetImpl() {
        localeMap = null;
    }
}