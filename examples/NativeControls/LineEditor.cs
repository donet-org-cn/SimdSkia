using System.Globalization;
using System.Text;

namespace SimdSkia.NativeControls;

// Basic single-line Latin/CJK editing. Indices always sit on Unicode scalar
// boundaries; complex-script shaping and multiline editing are out of scope.
internal sealed class LineEditor
{
    internal const int Limit = 128;
    internal string Text { get; private set; } = "Hello, SimdSkia!";
    internal int Caret { get; private set; } = 15;
    internal int Anchor { get; private set; } = 15;
    internal int Start => Math.Min(Caret, Anchor);
    internal int End => Math.Max(Caret, Anchor);
    internal string Selected => Text[Start..End];
    internal void SelectAll() { Anchor = 0; Caret = Text.Length; }
    internal void MoveTo(int index, bool extend = false)
    {
        index = Math.Clamp(index, 0, Text.Length);
        if (index > 0 && index < Text.Length && char.IsLowSurrogate(Text[index])) index--;
        Caret = index;
        if (!extend) Anchor = index;
    }
    internal void Left(bool extend)
    {
        if (!extend && Start != End) { MoveTo(Start); return; }
        int next = Math.Max(0, Caret - 1);
        if (next > 0 && char.IsLowSurrogate(Text[next])) next--;
        MoveTo(next, extend);
    }
    internal void Right(bool extend)
    {
        if (!extend && Start != End) { MoveTo(End); return; }
        int next = Caret;
        if (next < Text.Length) next += char.IsHighSurrogate(Text[next]) ? 2 : 1;
        MoveTo(next, extend);
    }
    internal void Delete(bool backspace)
    {
        if (Start == End)
        {
            if (backspace) Left(true); else Right(true);
        }
        int start = Start;
        Text = Text.Remove(start, End - start);
        MoveTo(start);
    }
    internal bool Insert(string input, Func<int, bool> hasGlyph, out string error)
    {
        // A single-line paste replaces line breaks/tabs with spaces.
        input = input.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        if (Text.Length - (End - Start) + input.Length > Limit)
        { error = "最多输入 128 个 UTF-16 字符"; return false; }
        ReadOnlySpan<char> rest = input;
        while (!rest.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(rest, out var rune, out int count) != System.Buffers.OperationStatus.Done)
            { error = "无效的 Unicode 字符"; return false; }
            var category = Rune.GetUnicodeCategory(rune);
            if (rune.Value is >= 0x0590 and <= 0x1cff || category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark || !hasGlyph(rune.Value))
            { error = "此示例支持基本中英文；当前字符需要其他字体或排版"; return false; }
            rest = rest[count..];
        }
        int start = Start;
        Text = Text.Remove(start, End - start).Insert(start, input);
        MoveTo(start + input.Length);
        error = "";
        return true;
    }
}
