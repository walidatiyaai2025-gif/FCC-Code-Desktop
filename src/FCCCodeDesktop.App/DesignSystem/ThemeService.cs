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
            var source = GetThemeSource(theme);
            var mergedDictionaries = _rootResources.MergedDictionaries;
            var existingThemes = mergedDictionaries.Where(IsThemeDictionary).ToArray();

            if (existingThemes.Length == 1 && SourceMatches(existingThemes[0], source))
            {
                return true;
            }

            var candidate = new ResourceDictionary
            {
                Source = new Uri(source, UriKind.Relative),
            };

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

        foreach (var dictionary in rootResources.MergedDictionaries.Where(IsThemeDictionary))
        {
            if (SourceMatches(dictionary, DarkThemeSource))
            {
                return AppearanceTheme.Dark;
            }

            if (SourceMatches(dictionary, LightThemeSource))
            {
                return AppearanceTheme.Light;
            }
        }

        return null;
    }

    internal static string GetThemeSource(AppearanceTheme theme) => theme switch
    {
        AppearanceTheme.Dark => DarkThemeSource,
        AppearanceTheme.Light => LightThemeSource,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unsupported appearance theme."),
    };

    private static bool IsThemeDictionary(ResourceDictionary dictionary) =>
        SourceMatches(dictionary, DarkThemeSource) || SourceMatches(dictionary, LightThemeSource);

    private static bool SourceMatches(ResourceDictionary dictionary, string source)
    {
        var originalSource = dictionary.Source?.OriginalString.Replace('\\', '/');
        return originalSource is not null && originalSource.EndsWith(source, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateCandidate(ResourceDictionary dictionary, AppearanceTheme theme)
    {
        if (dictionary["FccThemeName"] is not string themeName ||
            !string.Equals(themeName, theme.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Theme resource dictionary '{dictionary.Source}' does not identify itself as '{theme}'.");
        }
    }
}
