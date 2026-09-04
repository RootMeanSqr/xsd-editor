using System.Text;
using XsdEditor.Core;

namespace XsdEditor.Core.Tests;

/// <summary>
/// That reading a file and writing it back is a byte-level identity, byte-order mark
/// included.
/// </summary>
public class SourceFileTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("xsdeditor-sourcefile-").FullName;

    [Fact]
    public void A_byte_order_mark_survives_the_round_trip()
    {
        // File.ReadAllText discards the BOM, so text read that way and written back is not
        // the file that was read. Nothing caught it because the reference corpus has none.
        var bytes = new List<byte>(Encoding.UTF8.Preamble.ToArray());
        bytes.AddRange(Encoding.UTF8.GetBytes("<a>café</a>"));
        var path = Write("bom.xsd", [.. bytes]);

        var file = SourceFile.Read(path);

        Assert.Equal(Encoding.UTF8.Preamble.ToArray(), file.Preamble);
        Assert.Equal("<a>café</a>", file.Text);
        Assert.Equal(bytes, file.ToBytes(file.Text));
    }

    [Fact]
    public void A_file_without_a_byte_order_mark_does_not_gain_one()
    {
        var bytes = Encoding.UTF8.GetBytes("<a>café</a>");
        var path = Write("plain.xsd", bytes);

        var file = SourceFile.Read(path);

        Assert.Empty(file.Preamble);
        Assert.Equal(bytes, file.ToBytes(file.Text));
    }

    [Fact]
    public void Line_endings_are_not_transcoded()
    {
        // XE-086 pins the round-trip suite to keep-source so it cannot pass on one CI runner
        // and fail on another.
        var bytes = Encoding.UTF8.GetBytes("<a>\r\n  <b/>\r\n</a>\r\n");
        var path = Write("crlf.xsd", bytes);

        var file = SourceFile.Read(path);

        Assert.Contains("\r\n", file.Text, StringComparison.Ordinal);
        Assert.Equal(bytes, file.ToBytes(file.Text));
    }

    [Fact]
    public void Read_rejects_a_null_path()
    {
        Assert.Throws<ArgumentNullException>(() => SourceFile.Read(null!));
    }

    /// <summary>Removes the temporary directory this test class wrote into.</summary>
    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
