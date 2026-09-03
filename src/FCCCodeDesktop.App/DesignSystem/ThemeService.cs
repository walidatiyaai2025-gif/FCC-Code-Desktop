using System.IO;
using System.Windows;

namespace FCCCodeDesktop.App.DesignSystem;

public sealed class ThemeService
{
    internal const string DarkThemeSource = "/FCCCodeDesktop.App;component/DesignSystem/Themes/Theme.Dark.xaml";
    internal const string LightThemeSource = "/FCCCodeDesktop.App;component/DesignSystem/Themes/Theme.Light.xaml";

    private readonly ResourceDictionary _rootResources;

    public ThemeService(ResourceDictionary rootResources)
    {
        ArgumentNullException.ThrowIfNull(rootResources);
        _rootResources = rootResources;
    }

    public AppearanceTheme? CurrentTheme => DetectCurrentTheme(_rootResources);

    public void Apply(AppearanceTheme theme)
    {
        if (!TryApply(theme, out var error))
        {
            throw new InvalidOperationException($"Unable to apply the {theme} appearance theme.", error);
        }
    }

    public bool TryApply(AppearanceTheme theme, out Exception? error)
    {
        error = null;

        try
        {
            var mergedDictionaries = _rootResources.MergedDictionaries;
            var existingThemes = mergedDictionaries.Where(IsThemeDictionary).ToArray();

            if (existingThemes.Length == 1 && GetThemeIdentity(existingThemes[0]) == theme)
            {
                return true;
            }

            var candidate = LoadTheme(theme);
            ValidateCandidate(candidate, theme);

            var insertionIndex = existingThemes.Length == 0
                ? mergedDictionaries.Count
                : existingThemes.Min(dictionary => mergedDictionaries.IndexOf(dictionary));

            mergedDictionaries.Insert(insertionIndex, candidate);

            try
            {
                foreach (var existingTheme in existingThemes)
                {
                    mergedDictionaries.Remove(existingTheme);
                }
            }
            catch
            {
                mergedDictionaries.Remove(candidate);
                throw;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception;
            return false;
        }
    }

    internal static AppearanceTheme? DetectCurrentTheme(ResourceDictionary rootResources)
    {
        ArgumentNullException.ThrowIfNull(rootResources);

        var identities = rootResources.MergedDictionaries
            .Select(GetThemeIdentity)
            .Where(theme => theme.HasValue)
            .Select(theme => theme!.Value)
            .ToArray();

        return identities.Length == 1 ? identities[0] : null;
    }

    internal static string GetThemeSource(AppearanceTheme theme) => theme switch
    {
        AppearanceTheme.Dark => DarkThemeSource,
        AppearanceTheme.Light => LightThemeSource,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unsupported appearance theme."),
    };

    private static ResourceDictionary LoadTheme(AppearanceTheme theme)
    {
        var source = GetThemeSource(theme);
        var loaded = System.Windows.Application.LoadComponent(new Uri(source, UriKind.Relative));
        if (loaded is not ResourceDictionary dictionary)
        {
            throw new InvalidDataException($"Theme component '{source}' did not load as a ResourceDictionary.");
        }

        return dictionary;
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary) =>
        GetThemeIdentity(dictionary).HasValue;

    private static AppearanceTheme? GetThemeIdentity(ResourceDictionary dictionary)
    {
        if (!dictionary.Contains("FccThemeName") || dictionary["FccThemeName"] is not string themeName)
        {
            return null;
        }

        return Enum.TryParse<AppearanceTheme>(themeName, ignoreCase: false, out var theme)
            ? theme
            : null;
    }

    private static void ValidateCandidate(ResourceDictionary dictionary, AppearanceTheme theme)
    {
        if (GetThemeIdentity(dictionary) != theme)
        {
            throw new InvalidDataException($"Loaded theme component does not identify itself as '{theme}'.");
        }
    }
}
