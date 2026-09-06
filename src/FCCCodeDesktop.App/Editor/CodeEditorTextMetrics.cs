namespace FCCCodeDesktop.App.Editor;

public readonly record struct CodeEditorCaretPosition(int Line, int Column);

public static class CodeEditorTextMetrics
{
    public static int CountLogicalLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        var lineCount = 1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                lineCount++;
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
            }
            else if (text[index] == '\n')
            {
                lineCount++;
            }
        }

        return lineCount;
    }

    public static CodeEditorCaretPosition GetCaretPosition(string? text, int caretIndex)
    {
        text ??= string.Empty;
        var boundedCaretIndex = Math.Clamp(caretIndex, 0, text.Length);
        var line = 1;
        var column = 1;

        for (var index = 0; index < boundedCaretIndex; index++)
        {
            if (text[index] == '\r')
            {
                line++;
                column = 1;
                if (index + 1 < boundedCaretIndex && text[index + 1] == '\n')
                {
                    index++;
                }
            }
            else if (text[index] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new CodeEditorCaretPosition(line, column);
    }
}
