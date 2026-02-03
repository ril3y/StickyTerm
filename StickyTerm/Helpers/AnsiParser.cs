using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace StickyTerm.Helpers;

/// <summary>
/// Represents a segment of text with ANSI color/style attributes.
/// </summary>
public class AnsiTextSegment
{
    public string Text { get; set; } = string.Empty;
    public Color? ForegroundColor { get; set; }
    public Color? BackgroundColor { get; set; }
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    public DateTime? Timestamp { get; set; }
}

/// <summary>
/// Parses ANSI escape sequences and converts them to styled text segments.
/// Supports standard SGR (Select Graphic Rendition) codes.
/// </summary>
public partial class AnsiParser
{
    // Standard 8 ANSI colors
    private static readonly Color[] StandardColors =
    [
        Color.FromRgb(0, 0, 0),       // 0: Black
        Color.FromRgb(205, 49, 49),   // 1: Red
        Color.FromRgb(13, 188, 121),  // 2: Green
        Color.FromRgb(229, 229, 16),  // 3: Yellow
        Color.FromRgb(36, 114, 200),  // 4: Blue
        Color.FromRgb(188, 63, 188),  // 5: Magenta
        Color.FromRgb(17, 168, 205),  // 6: Cyan
        Color.FromRgb(229, 229, 229), // 7: White
    ];

    // Bright ANSI colors (8-15)
    private static readonly Color[] BrightColors =
    [
        Color.FromRgb(102, 102, 102), // 8: Bright Black (Gray)
        Color.FromRgb(241, 76, 76),   // 9: Bright Red
        Color.FromRgb(35, 209, 139),  // 10: Bright Green
        Color.FromRgb(245, 245, 67),  // 11: Bright Yellow
        Color.FromRgb(59, 142, 234),  // 12: Bright Blue
        Color.FromRgb(214, 112, 214), // 13: Bright Magenta
        Color.FromRgb(41, 184, 219),  // 14: Bright Cyan
        Color.FromRgb(255, 255, 255), // 15: Bright White
    ];

    // Regex to match ANSI escape sequences: ESC [ params m
    [GeneratedRegex(@"\x1B\[([0-9;]*)m")]
    private static partial Regex AnsiSequenceRegex();

    // Current style state
    private Color? _currentForeground;
    private Color? _currentBackground;
    private bool _isBold;
    private bool _isItalic;
    private bool _isUnderline;

    /// <summary>
    /// Parses text containing ANSI escape sequences into styled segments.
    /// </summary>
    public IEnumerable<AnsiTextSegment> Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
            yield break;

        var regex = AnsiSequenceRegex();
        int lastIndex = 0;

        foreach (Match match in regex.Matches(input))
        {
            // Output text before this escape sequence
            if (match.Index > lastIndex)
            {
                var text = input[lastIndex..match.Index];
                if (!string.IsNullOrEmpty(text))
                {
                    yield return CreateSegment(text);
                }
            }

            // Process the escape sequence
            ProcessEscapeSequence(match.Groups[1].Value);

            lastIndex = match.Index + match.Length;
        }

        // Output remaining text after last escape sequence
        if (lastIndex < input.Length)
        {
            var text = input[lastIndex..];
            if (!string.IsNullOrEmpty(text))
            {
                yield return CreateSegment(text);
            }
        }
    }

    /// <summary>
    /// Parses byte data containing ANSI escape sequences.
    /// </summary>
    public IEnumerable<AnsiTextSegment> Parse(byte[] data, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var text = encoding.GetString(data);
        return Parse(text);
    }

    /// <summary>
    /// Resets the parser state to default (no colors, no styles).
    /// </summary>
    public void Reset()
    {
        _currentForeground = null;
        _currentBackground = null;
        _isBold = false;
        _isItalic = false;
        _isUnderline = false;
    }

    private AnsiTextSegment CreateSegment(string text)
    {
        return new AnsiTextSegment
        {
            Text = text,
            ForegroundColor = _currentForeground,
            BackgroundColor = _currentBackground,
            IsBold = _isBold,
            IsItalic = _isItalic,
            IsUnderline = _isUnderline
        };
    }

    private void ProcessEscapeSequence(string parameters)
    {
        if (string.IsNullOrEmpty(parameters))
        {
            // ESC[m is equivalent to ESC[0m (reset)
            Reset();
            return;
        }

        var codes = parameters.Split(';');
        int i = 0;

        while (i < codes.Length)
        {
            if (!int.TryParse(codes[i], out int code))
            {
                i++;
                continue;
            }

            switch (code)
            {
                case 0: // Reset
                    Reset();
                    break;
                case 1: // Bold
                    _isBold = true;
                    break;
                case 3: // Italic
                    _isItalic = true;
                    break;
                case 4: // Underline
                    _isUnderline = true;
                    break;
                case 22: // Normal intensity (not bold)
                    _isBold = false;
                    break;
                case 23: // Not italic
                    _isItalic = false;
                    break;
                case 24: // Not underlined
                    _isUnderline = false;
                    break;

                // Standard foreground colors (30-37)
                case >= 30 and <= 37:
                    _currentForeground = StandardColors[code - 30];
                    break;

                // Default foreground
                case 39:
                    _currentForeground = null;
                    break;

                // Standard background colors (40-47)
                case >= 40 and <= 47:
                    _currentBackground = StandardColors[code - 40];
                    break;

                // Default background
                case 49:
                    _currentBackground = null;
                    break;

                // Bright foreground colors (90-97)
                case >= 90 and <= 97:
                    _currentForeground = BrightColors[code - 90];
                    break;

                // Bright background colors (100-107)
                case >= 100 and <= 107:
                    _currentBackground = BrightColors[code - 100];
                    break;

                // Extended color mode (256-color or 24-bit)
                case 38: // Set foreground
                    i = ProcessExtendedColor(codes, i + 1, isForeground: true);
                    continue;

                case 48: // Set background
                    i = ProcessExtendedColor(codes, i + 1, isForeground: false);
                    continue;
            }

            i++;
        }
    }

    private int ProcessExtendedColor(string[] codes, int startIndex, bool isForeground)
    {
        if (startIndex >= codes.Length)
            return startIndex;

        if (!int.TryParse(codes[startIndex], out int mode))
            return startIndex;

        if (mode == 5 && startIndex + 1 < codes.Length)
        {
            // 256-color mode: 38;5;n or 48;5;n
            if (int.TryParse(codes[startIndex + 1], out int colorIndex))
            {
                var color = Get256Color(colorIndex);
                if (isForeground)
                    _currentForeground = color;
                else
                    _currentBackground = color;
            }
            return startIndex + 2;
        }
        else if (mode == 2 && startIndex + 3 < codes.Length)
        {
            // 24-bit color mode: 38;2;r;g;b or 48;2;r;g;b
            if (int.TryParse(codes[startIndex + 1], out int r) &&
                int.TryParse(codes[startIndex + 2], out int g) &&
                int.TryParse(codes[startIndex + 3], out int b))
            {
                var color = Color.FromRgb((byte)r, (byte)g, (byte)b);
                if (isForeground)
                    _currentForeground = color;
                else
                    _currentBackground = color;
            }
            return startIndex + 4;
        }

        return startIndex + 1;
    }

    private static Color Get256Color(int index)
    {
        if (index < 0 || index > 255)
            return Colors.White;

        // Standard colors (0-7)
        if (index < 8)
            return StandardColors[index];

        // Bright colors (8-15)
        if (index < 16)
            return BrightColors[index - 8];

        // 216 color cube (16-231)
        if (index < 232)
        {
            int i = index - 16;
            int r = (i / 36) * 51;
            int g = ((i / 6) % 6) * 51;
            int b = (i % 6) * 51;
            return Color.FromRgb((byte)r, (byte)g, (byte)b);
        }

        // Grayscale (232-255)
        int gray = (index - 232) * 10 + 8;
        return Color.FromRgb((byte)gray, (byte)gray, (byte)gray);
    }

    /// <summary>
    /// Strips all ANSI escape sequences from text.
    /// </summary>
    public static string StripAnsi(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return AnsiSequenceRegex().Replace(input, string.Empty);
    }
}
