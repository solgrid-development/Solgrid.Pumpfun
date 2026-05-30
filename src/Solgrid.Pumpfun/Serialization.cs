using Solnet.Wallet;

namespace Solgrid.Pumpfun;

// single byte reader/writer for account data and instruction args,
// replaces the per-file helpers
internal static class ByteRW
{
    public static ulong U64(byte[] b, ref int o)
    {
        ulong v = 0;
        for (int i = 7; i >= 0; i--)
            v = (v << 8) | b[o + i];
        o += 8;
        return v;
    }

    public static long I128(byte[] b, ref int o)
    {
        var low = (long)U64(b, ref o);
        o += 8;
        return low;
    }

    public static bool Bool(byte[] b, ref int o) => b[o++] != 0;

    public static PublicKey Key(byte[] b, ref int o)
    {
        var k = new PublicKey(b[o..(o + 32)]);
        o += 32;
        return k;
    }

    public static PublicKey[] Keys(byte[] b, ref int o, int count)
    {
        var arr = new PublicKey[count];
        for (int i = 0; i < count; i++)
            arr[i] = Key(b, ref o);
        return arr;
    }

    public static void WriteU64(byte[] dst, int offset, ulong value)
    {
        for (int i = 0; i < 8; i++)
            dst[offset + i] = (byte)(value >> (8 * i));
    }
}
