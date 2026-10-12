using System.Buffers.Binary;
using System.Numerics;
namespace FreshFoundation;

// MurmurHash3 x86_32 is Austin Appleby's public-domain algorithm.
// This implementation follows the admitted algorithm facts, not editor source.
public static class ObservedChecksum
{
    public static uint Calculate(ReadOnlySpan<byte> bytes, uint seed = uint.MaxValue)
    {
        unchecked
        {
            uint result = seed;
            int whole = bytes.Length & ~3;
            for(int offset=0;offset<whole;offset+=4)
            {
                uint word = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
                result ^= Mix(word);
                result = BitOperations.RotateLeft(result,13) * 5 + 0xe6546b64;
            }
            uint tail = 0;
            for(int offset=whole;offset<bytes.Length;offset++) tail |= (uint)bytes[offset] << ((offset-whole)*8);
            if(whole != bytes.Length) result ^= Mix(tail);
            result ^= (uint)bytes.Length;
            result ^= result >> 16; result *= 0x85ebca6b;
            result ^= result >> 13; result *= 0xc2b2ae35;
            return result ^ (result >> 16);
        }
    }
    private static uint Mix(uint word) => unchecked(BitOperations.RotateLeft(word * 0xcc9e2d51,15) * 0x1b873593);
}
