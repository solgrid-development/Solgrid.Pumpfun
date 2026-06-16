using System.Security.Cryptography;
using System.Text;
using Solnet.Wallet;

namespace Solgrid.Pumpfun.Events;

public abstract record PumpEvent;

// anchor appends fields on upgrades, so parse sequentially and leave
// whatever the era did not have yet at default. old txs (105 bytes)
// still decode, new txs fill everything.
public record TradeEvent(
    PublicKey Mint,
    ulong SolAmount,
    ulong TokenAmount,
    bool IsBuy,
    PublicKey User,
    long Timestamp,
    ulong VirtualSolReserves,
    ulong VirtualTokenReserves,
    ulong RealSolReserves,
    ulong RealTokenReserves,
    PublicKey FeeRecipient,
    ulong FeeBasisPoints,
    ulong Fee,
    PublicKey Creator,
    ulong CreatorFeeBasisPoints,
    ulong CreatorFee,
    bool TrackVolume,
    ulong CurrentSolVolume,
    string IxName,
    bool MayhemMode,
    ulong Cashback,
    ulong BuybackFee,
    PublicKey QuoteMint,
    ulong QuoteAmount,
    ulong VirtualQuoteReserves,
    ulong RealQuoteReserves) : PumpEvent
{
    public static readonly byte[] Disc = DiscOf("Trade");

    public static TradeEvent? TryParse(byte[] raw)
    {
        if (raw.Length < 8 + 105 - 8)
            return null;
        for (int i = 0; i < 8; i++)
            if (raw[i] != Disc[i])
                return null;

        int o = 8;
        if (raw.Length < o + 105)
            return null;

        var mint = R.Key(raw, ref o);
        var sol = R.U64(raw, ref o);
        var tok = R.U64(raw, ref o);
        var isBuy = R.Bool(raw, ref o);
        var user = R.Key(raw, ref o);
        var ts = (long)R.U64(raw, ref o);
        var vsol = R.U64(raw, ref o);
        var vtok = R.U64(raw, ref o);

        // pre-creator-fee era stops here
        if (raw.Length <= o)
        {
            return new TradeEvent(mint, sol, tok, isBuy, user, ts, vsol, vtok,
                0, 0, new PublicKey(new byte[32]), 0, 0, new PublicKey(new byte[32]), 0, 0,
                false, 0, "", false, 0, 0, new PublicKey(new byte[32]), 0, 0, 0);
        }

        var rsol = R.U64(raw, ref o);
        var rtok = R.U64(raw, ref o);
        var feeRecipient = R.Key(raw, ref o);
        var feeBps = R.U64(raw, ref o);
        var fee = R.U64(raw, ref o);
        var creator = R.Key(raw, ref o);
        var creatorBps = R.U64(raw, ref o);
        var creatorFee = R.U64(raw, ref o);
        var track = R.Bool(raw, ref o);
        R.U64(raw, ref o); // total_unclaimed_tokens
        R.U64(raw, ref o); // total_claimed_tokens
        var vol = R.U64(raw, ref o);
        R.U64(raw, ref o); // last_update_timestamp

        var ixName = "";
        if (raw.Length > o + 4)
        {
            ixName = R.Str(raw, ref o);
        }

        var mayhem = raw.Length > o && R.Bool(raw, ref o);
        R.U64(raw, ref o);            // cashback_fee_basis_points
        var cashback = R.U64(raw, ref o);
        R.U64(raw, ref o);            // buyback_fee_basis_points
        var buyback = R.U64(raw, ref o);

        // shareholders vec, skip
        if (raw.Length > o + 4)
        {
            var n = (int)BitConverter.ToUInt32(raw, o);
            o += 4 + n * 40;
        }

        var quoteMint = new PublicKey(new byte[32]);
        ulong quoteAmount = 0, vqr = 0, rqr = 0;
        if (raw.Length >= o + 32 + 24)
        {
            quoteMint = R.Key(raw, ref o);
            quoteAmount = R.U64(raw, ref o);
            vqr = R.U64(raw, ref o);
            rqr = R.U64(raw, ref o);
        }

        return new TradeEvent(mint, sol, tok, isBuy, user, ts, vsol, vtok,
            rsol, rtok, feeRecipient, feeBps, fee, creator, creatorBps, creatorFee,
            track, vol, ixName, mayhem, cashback, buyback, quoteMint, quoteAmount, vqr, rqr);
    }

    private static byte[] DiscOf(string name)
        => SHA256.HashData(Encoding.UTF8.GetBytes("event:" + name))[..8];
}

internal static class R
{
    public static ulong U64(byte[] b, ref int o)
    {
        ulong v = 0;
        for (int i = 7; i >= 0; i--)
            v = (v << 8) | b[o + i];
        o += 8;
        return v;
    }

    public static bool Bool(byte[] b, ref int o) => b[o++] != 0;

    public static PublicKey Key(byte[] b, ref int o)
    {
        var k = new PublicKey(b[o..(o + 32)]);
        o += 32;
        return k;
    }

    public static string Str(byte[] b, ref int o)
    {
        var len = (int)BitConverter.ToUInt32(b, o);
        o += 4;
        if (len == 0 || o + len > b.Length)
            return "";
        var s = Encoding.UTF8.GetString(b, o, len);
        o += len;
        return s;
    }
}
