using System.Collections.ObjectModel;
using System.Text;

namespace FCCCodeDesktop.App.Conversation;

public enum ConversationContentBlockKind
{
    Paragraph = 0,
    Heading = 1,
    Bullet = 2,
    Code = 3,
    DiffHeader = 4,
    DiffAdded = 5,
    DiffRemoved = 6,
    DiffContext = 7,
}

public sealed record ConversationContentBlock(
    ConversationContentBlockKind Kind,
    string Text,
    string? Language = null);

public static class ConversationContentParser
{
    public const int MaxRenderedSourceCharacters = 1024 * 1024;
    public const int MaxLanguageIdentifierLength = 32;

    public static IReadOnlyList<ConversationContentBlock> Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<ConversationContentBlock>();
        }

        var wasTruncated = text.Length > MaxRenderedSourceCharacters;
        var bounded = wasTruncated ? text[..MaxRenderedSourceCharacters] : text;
        var normalized = bounded.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var blocks = new List<ConversationContentBlock>();
        var paragraph = new StringBuilder();
        var fenced = new StringBuilder();
        var inFence = false;
        string? fenceLanguage = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (!inFence)
                {
                    FlushParagraph(blocks, paragraph);
                    inFence = true;
                    fenceLanguage = NormalizeLanguage(line[3..]);
                    fenced.Clear();
                }
                else
                {
                    FlushFence(blocks, fenced, fenceLanguage);
                    inFence = false;
                    fenceLanguage = null;
                }

                continue;
            }

            if (inFence)
            {
                if (fenced.Length > 0)
                {
                    fenced.Append('\n');
                }

                fenced.Append(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(blocks, paragraph);
                continue;
            }

            if (TryParseHeading(line, out var heading))
            {
                FlushParagraph(blocks, paragraph);
                blocks.Add(new ConversationContentBlock(ConversationContentBlockKind.Heading, heading));
                continue;
            }

            if (TryParseBullet(line, out var bullet))
            {
                FlushParagraph(blocks, paragraph);
                blocks.Add(new ConversationContentBlock(ConversationContentBlockKind.Bullet, $"• {bullet}"));
                continue;
            }

            if (paragraph.Length > 0)
            {
                paragraph.Append('\n');
            }

            paragraph.Append(line);
        }

        if (inFence)
        {
            FlushFence(blocks, fenced, fenceLanguage);
        }
        else
        {
            FlushParagraph(blocks, paragraph);
        }

        if (wasTruncated)
        {
            blocks.Add(
                new ConversationContentBlock(
                    ConversationContentBlockKind.Paragraph,
                    $"Rendering limited to the first {MaxRenderedSourceCharacters:N0} characters; the durable message remains unchanged."));
        }

        return new ReadOnlyCollection<ConversationContentBlock>(blocks);
    }

    private static void FlushParagraph(List<ConversationContentBlock> blocks, StringBuilder paragraph)
    {
        if (paragraph.Length == 0)
        {
            return;
        }

        blocks.Add(new ConversationContentBlock(ConversationContentBlockKind.Paragraph, paragraph.ToString()));
        paragraph.Clear();
    }

    private static void FlushFence(
        List<ConversationContentBlock> blocks,
        StringBuilder fenced,
        string? language)
    {
        var text = fenced.ToString();
        if (string.Equals(language, "diff", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "patch", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in text.Split('\n'))
            {
                blocks.Add(new ConversationContentBlock(ClassifyDiffLine(line), line, language));
            }
        }
        else
        {
            blocks.Add(new ConversationContentBlock(ConversationContentBlockKind.Code, text, language));
        }

        fenced.Clear();
    }

    private static ConversationContentBlockKind ClassifyDiffLine(string line)
    {
        if (line.StartsWith("+++", StringComparison.Ordinal)
            || line.StartsWith("---", StringComparison.Ordinal)
            || line.StartsWith("@@", StringComparison.Ordinal)
            || line.StartsWith("diff ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("index ", StringComparison.OrdinalIgnoreCase))
        {
            return ConversationContentBlockKind.DiffHeader;
        }

        if (line.StartsWith('+'))
        {
            return ConversationContentBlockKind.DiffAdded;
        }

        if (line.StartsWith('-'))
        {
            return ConversationContentBlockKind.DiffRemoved;
        }

        return ConversationContentBlockKind.DiffContext;
    }

    private static bool TryParseHeading(string line, out string heading)
    {
        var markerCount = 0;
        while (markerCount < line.Length && markerCount < 6 && line[markerCount] == '#')
        {
            markerCount++;
        }

        if (markerCount > 0 && markerCount < line.Length && line[markerCount] == ' ')
        {
            heading = line[(markerCount + 1)..].Trim();
            return heading.Length > 0;
        }

        heading = string.Empty;
        return false;
    }

    private static bool TryParseBullet(string line, out string bullet)
    {
        if (line.Length >= 3
            && (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)))
        {
            bullet = line[2..].Trim();
            return bullet.Length > 0;
        }

        bullet = string.Empty;
        return false;
    }

    private static string? NormalizeLanguage(string language)
    {
        var normalized = language.Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= MaxLanguageIdentifierLength
            ? normalized
            : normalized[..MaxLanguageIdentifierLength];
    }
}
