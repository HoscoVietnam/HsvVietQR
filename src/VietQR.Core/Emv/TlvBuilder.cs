using System.Text;

namespace VietQR.Core;

/// <summary>Builder TLV thuần — không allocation thừa, test offline.</summary>
public sealed class TlvBuilder
{
    private static readonly string[] TwoDigitLookup = Enumerable.Range(0, 100).Select(i => i.ToString("D2")).ToArray();
    private readonly StringBuilder _buffer = new();

    public TlvBuilder Add(string tag, string? value)
    {
        if (string.IsNullOrEmpty(value)) return this;

        int byteLength = Encoding.UTF8.GetByteCount(value);
        if (byteLength > 99)
            throw new ArgumentOutOfRangeException(nameof(value), $"TLV tag {tag} vượt 99 byte UTF-8.");

        _buffer.Append(tag)
               .Append(TwoDigitLookup[byteLength])
               .Append(value);
        return this;
    }

    public string Build() => _buffer.ToString();

    public int Length => _buffer.Length;

    public override string ToString() => Build();
}
