using System.Globalization;
using SyncForge.Core.History;

namespace SyncForge.Core.Tests;

public class CsvWriterTests
{
    private static string Row(char sep, params string?[] cells)
    {
        using var sw = new StringWriter();
        new CsvWriter(sw, sep).WriteRow(cells);
        return sw.ToString().TrimEnd('\r', '\n');
    }

    [Fact]
    public void PlainValues_AreUnquoted() => Assert.Equal("a;b;123", Row(';', "a", "b", "123"));

    [Fact]
    public void Separator_Quote_Newline_AreQuoted()
    {
        Assert.Equal("\"a;b\"", Row(';', "a;b"));
        Assert.Equal("\"er sagte \"\"hallo\"\"\"", Row(';', "er sagte \"hallo\""));
        Assert.Equal("\"zwei\nZeilen\"", Row(';', "zwei\nZeilen"));
        Assert.Equal("a,b", Row(';', "a,b")); // Komma ist bei ";" kein Trennzeichen
        Assert.Equal("\"a,b\"", Row(',', "a,b"));
    }

    [Theory]
    [InlineData("=SUMME(A1)", "'=SUMME(A1)")]
    [InlineData("+49", "'+49")]
    [InlineData("-x", "'-x")]
    [InlineData("@cmd", "'@cmd")]
    public void FormulaLikeCells_AreDefused(string input, string expected) => Assert.Equal(expected, Row(';', input));

    [Fact]
    public void NullAndEmpty_BecomeEmptyCells() => Assert.Equal(";;x", Row(';', null, "", "x"));

    [Fact]
    public void Separator_FollowsCulture()
    {
        Assert.Equal(';', CsvWriter.SeparatorFor(new CultureInfo("de-DE")));
        Assert.Equal(',', CsvWriter.SeparatorFor(new CultureInfo("en-US")));
    }

    [Fact]
    public void Numbers_AndDates_AreLanguageIndependent()
    {
        Assert.Equal("1234567", CsvWriter.Number(1_234_567));
        var local = new DateTimeOffset(2026, 11, 15, 14, 30, 5, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 15, 14, 30, 5)));
        Assert.Equal("2026-11-15 14:30:05", CsvWriter.Date(local));
    }
}
