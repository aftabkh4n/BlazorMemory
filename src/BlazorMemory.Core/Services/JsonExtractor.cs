namespace BlazorMemory.Core.Services;

/// <summary>
/// Shared JSON-extraction helpers used by LLM-based extractors whose models
/// often wrap valid JSON in prose or Markdown code fences. Kept internal so
/// this stays a helper rather than a public promise.
/// </summary>
internal static class JsonExtractor
{
    /// <summary>
    /// Returns the substring bounded by the outermost JSON array or object
    /// brackets in <paramref name="raw"/>, after stripping surrounding code
    /// fences. Falls back to the fence-stripped string if no brackets found.
    /// </summary>
    public static string ExtractJson(string raw)
    {
        var s = StripCodeFences(raw);

        var arrStart = s.IndexOf('[');
        var objStart = s.IndexOf('{');
        if (arrStart < 0 && objStart < 0) return s;

        int start; char close;
        if (arrStart < 0 || (objStart >= 0 && objStart < arrStart))
        { start = objStart; close = '}'; }
        else
        { start = arrStart; close = ']'; }

        var end = s.LastIndexOf(close);
        return end > start ? s[start..(end + 1)] : s;
    }

    /// <summary>
    /// Strips a single leading and trailing Markdown code fence (```lang ... ```)
    /// from the input, otherwise returns it unchanged.
    /// </summary>
    public static string StripCodeFences(string raw)
    {
        var s = raw.Trim();
        if (!s.StartsWith("```")) return s;
        var nl = s.IndexOf('\n');
        if (nl >= 0) s = s[(nl + 1)..].Trim();
        var fence = s.LastIndexOf("```");
        if (fence >= 0) s = s[..fence].Trim();
        return s;
    }
}
