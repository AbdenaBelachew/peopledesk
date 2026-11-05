using PeopleDesk.Application.Common;
using Xunit;

namespace PeopleDesk.UnitTests;

public class CsvBuilderTests
{
    [Fact]
    public void Escapes_Commas_Quotes_AndNewlines()
    {
        var csv = CsvBuilder.Build(
            ["Name", "Note"],
            [["Doe, John", "He said \"hi\""], ["Line\nBreak", "plain"]]);

        Assert.Contains("\"Doe, John\"", csv);
        Assert.Contains("\"He said \"\"hi\"\"\"", csv);
        Assert.Contains("\"Line\nBreak\"", csv);
        Assert.EndsWith("plain\n", csv);
    }

    [Fact]
    public void AddsUtf8Bom_ForExcelCompatibility()
    {
        var bytes = CsvBuilder.BuildBytes(["A"], [["1"]]);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public void NullCells_BecomeEmpty()
    {
        var csv = CsvBuilder.Build(["A", "B"], [[null, "x"]]);
        Assert.Equal("A,B\n,x\n", csv.Replace("\r\n", "\n"));
    }
}
