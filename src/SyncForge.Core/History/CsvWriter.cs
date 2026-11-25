using System.Globalization;
using System.Text;

namespace SyncForge.Core.History;

/// <summary>
/// Schreibt CSV, das Excel per Doppelklick richtig öffnet: Trennzeichen aus den Regionseinstellungen (";" in
/// Deutschland, "," in den USA), UTF-8 mit BOM, Anführungszeichen nur wo nötig. Zellen, die mit =, +, -, @ beginnen,
/// werden mit einem Apostroph entschärft, damit Tabellenprogramme sie nie als Formel ausführen.
/// </summary>
public sealed class CsvWriter(TextWriter writer, char separator)
{
    /// <summary>Trennzeichen passend zu den Regionseinstellungen von Windows.</summary>
    public static char SeparatorFor(CultureInfo culture) =>
        string.IsNullOrEmpty(culture.TextInfo.ListSeparator) ? ',' : culture.TextInfo.ListSeparator.Trim() is { Length: > 0 } s ? s[0] : ',';

    public static Encoding Encoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public void WriteRow(IEnumerable<string?> cells) =>
        writer.WriteLine(string.Join(separator, cells.Select(c => Escape(c ?? string.Empty, separator))));

    /// <summary>Zahl unabhängig von der Sprache (ohne Tausendertrennzeichen).</summary>
    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Zeitpunkt in Ortszeit, von Tabellenprogrammen als Datum erkannt.</summary>
    public static string Date(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    internal static string Escape(string value, char separator)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        var needsQuotes = value.Contains(separator) || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
                          || (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])));
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
