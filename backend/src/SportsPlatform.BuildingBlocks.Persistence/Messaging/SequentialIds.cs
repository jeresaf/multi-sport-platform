using System.Buffers.Binary;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>
/// UUIDv7s that increase strictly within the process: the 12 <c>rand_a</c> bits count IDs made in the same
/// millisecond (RFC 9562, section 6.2, method 1). Outbox message IDs use them, so the messages of one save are
/// inserted, and get their <c>sequence</c>, in the order they were published, whichever way EF orders the inserts.
/// </summary>
public static class SequentialIds
{
    private const int _maxCounter = 0xFFF;

    private static readonly Lock _gate = new();
    private static long _lastMilliseconds;
    private static int _counter;

    public static Guid Next()
    {
        Span<byte> bytes = stackalloc byte[16];
        Guid.CreateVersion7().TryWriteBytes(bytes, bigEndian: true, out _);
        var milliseconds = (long)(BinaryPrimitives.ReadUInt64BigEndian(bytes) >> 16);

        int counter;
        lock (_gate)
        {
            if (milliseconds > _lastMilliseconds)
            {
                _lastMilliseconds = milliseconds;
                _counter = 0;
            }
            else if (++_counter > _maxCounter)
            {
                // More than 4,096 in one millisecond (or the clock went back): borrow the next millisecond.
                _lastMilliseconds++;
                _counter = 0;
            }

            milliseconds = _lastMilliseconds;
            counter = _counter;
        }

        bytes[0] = (byte)(milliseconds >> 40);
        bytes[1] = (byte)(milliseconds >> 32);
        bytes[2] = (byte)(milliseconds >> 24);
        bytes[3] = (byte)(milliseconds >> 16);
        bytes[4] = (byte)(milliseconds >> 8);
        bytes[5] = (byte)milliseconds;
        bytes[6] = (byte)(0x70 | (counter >> 8));
        bytes[7] = (byte)counter;
        return new Guid(bytes, bigEndian: true);
    }
}
