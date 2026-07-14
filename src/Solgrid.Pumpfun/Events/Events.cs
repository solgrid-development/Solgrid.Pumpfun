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
    // discriminators are explicit in the official IDL, anchor's
    // sha256("event:X") formula does NOT match what the program emits
    public static readonly byte[] Disc = { 189, 219, 127, 211, 78, 230, 97, 238 };

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
}

public record MigrationEvent(
    PublicKey User,
    PublicKey Mint,
    ulong MintAmount,
    ulong SolAmount,
    ulong PoolMigrationFee,
    PublicKey BondingCurve,
    long Timestamp,
    PublicKey Pool,
    PublicKey QuoteMint) : PumpEvent
{
    public static readonly byte[] Disc = { 189, 233, 93, 185, 92, 148, 234, 148 };

    public static MigrationEvent? TryParse(byte[] raw)
    {
        if (raw.Length < 8 + 32 * 5 + 8 * 4)
            return null;
        for (int i = 0; i < 8; i++)
            if (raw[i] != Disc[i])
                return null;

        int o = 8;
        var user = R.Key(raw, ref o);
        var mint = R.Key(raw, ref o);
        var mintAmount = R.U64(raw, ref o);
        var solAmount = R.U64(raw, ref o);
        var migrationFee = R.U64(raw, ref o);
        var curve = R.Key(raw, ref o);
        var ts = (long)R.U64(raw, ref o);
        var pool = R.Key(raw, ref o);
        var quoteMint = R.Key(raw, ref o);

        return new MigrationEvent(user, mint, mintAmount, solAmount, migrationFee, curve, ts, pool, quoteMint);
    }
}

// pumpswap swap fills. same wire shape for buy and sell up to the
// pool/user keys, only the quote field name differs
public record AmmSwapEvent(
    bool IsBuy,
    long Timestamp,
    ulong BaseAmount,
    ulong QuoteAmount,
    PublicKey Pool,
    PublicKey User) : PumpEvent
{
    public static readonly byte[] BuyDisc = { 103, 244, 82, 31, 44, 245, 119, 119 };
    public static readonly byte[] SellDisc = { 62, 47, 55, 10, 165, 3, 220, 42 };

    public static AmmSwapEvent? TryParse(byte[] raw)
    {
        if (raw.Length < 184)
            return null;

        bool isBuy;
        if (raw.AsSpan(0, 8).SequenceEqual(BuyDisc)) isBuy = true;
        else if (raw.AsSpan(0, 8).SequenceEqual(SellDisc)) isBuy = false;
        else return null;

        int o = 8;
        var ts = (long)R.U64(raw, ref o);
        var baseAmount = R.U64(raw, ref o);
        o += 8;        // max/min quote bound
        o += 8 * 4;    // user/pool reserves
        var quoteAmount = R.U64(raw, ref o);
        o += 8 * 6;    // fee fields
        var pool = R.Key(raw, ref o);
        var user = R.Key(raw, ref o);

        return new AmmSwapEvent(isBuy, ts, baseAmount, quoteAmount, pool, user);
    }
}

public static class EventDecoder
{
    private const string Prefix = "Program data: ";

    public static PumpEvent? Decode(byte[] raw)
    {
        return (PumpEvent?)TradeEvent.TryParse(raw)
            ?? (PumpEvent?)MigrationEvent.TryParse(raw)
            ?? (PumpEvent?)AmmSwapEvent.TryParse(raw);
    }

    public static PumpEvent? DecodeLogLine(string line)
    {
        if (line == null || !line.StartsWith(Prefix))
            return null;
        try
        {
            return Decode(Convert.FromBase64String(line[Prefix.Length..].Trim()));
        }
        catch
        {
            return null;
        }
    }
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
