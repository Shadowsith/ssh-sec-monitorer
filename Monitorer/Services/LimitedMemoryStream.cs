namespace Monitorer.Services;

/// <summary>MemoryStream that refuses to grow beyond a maximum size (protects against huge remote files).</summary>
public class LimitedMemoryStream : MemoryStream
{
    private readonly long _maxBytes;
    private readonly string _tooLargeMessage;

    public LimitedMemoryStream(long maxBytes, string tooLargeMessage)
    {
        _maxBytes = maxBytes;
        _tooLargeMessage = tooLargeMessage;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureCapacityLeft(count);
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureCapacityLeft(buffer.Length);
        base.Write(buffer);
    }

    public override void WriteByte(byte value)
    {
        EnsureCapacityLeft(1);
        base.WriteByte(value);
    }

    private void EnsureCapacityLeft(int count)
    {
        if (Length + count > _maxBytes)
            throw new InvalidOperationException(_tooLargeMessage);
    }
}
