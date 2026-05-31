using Solnet.Wallet;

namespace Solgrid.Pumpfun.Accounts;

public class BondingCurve
{
    public ulong VirtualTokenReserves { get; init; }
    public ulong VirtualQuoteReserves { get; init; }
    public ulong RealTokenReserves { get; init; }
    public ulong RealQuoteReserves { get; init; }
    public ulong TokenTotalSupply { get; init; }
    public bool Complete { get; init; }
    public PublicKey Creator { get; init; } = new(new byte[32]);
    public bool IsMayhemMode { get; init; }
    public bool IsCashbackCoin { get; init; }
    public PublicKey QuoteMint { get; init; } = new(new byte[32]);

    public static BondingCurve? Deserialize(byte[] data)
    {
        if (data.Length < 8 + 40 + 1 + 32 + 2 + 32)
            return null;

        int o = 8;
        return new BondingCurve
        {
            VirtualTokenReserves = Read.U64(data, ref o),
            VirtualQuoteReserves = Read.U64(data, ref o),
            RealTokenReserves = Read.U64(data, ref o),
            RealQuoteReserves = Read.U64(data, ref o),
            TokenTotalSupply = Read.U64(data, ref o),
            Complete = Read.Bool(data, ref o),
            Creator = Read.Key(data, ref o),
            IsMayhemMode = Read.Bool(data, ref o),
            IsCashbackCoin = Read.Bool(data, ref o),
            QuoteMint = Read.Key(data, ref o),
        };
    }
}

public class PumpGlobal
{
    public PublicKey FeeRecipient { get; init; } = new(new byte[32]);
    public ulong FeeBasisPoints { get; init; }
    public ulong CreatorFeeBasisPoints { get; init; }
    public PublicKey[] WhitelistedQuoteMints { get; init; } = Array.Empty<PublicKey>();

    // partial parse, only what traders need; layout order follows the IDL
    public static PumpGlobal? Deserialize(byte[] data)
    {
        if (data.Length < 8 + 1 + 32 + 32 + 32)
            return null;

        int o = 8;
        Read.Bool(data, ref o);              // initialized
        Read.Key(data, ref o);               // authority
        var feeRecipient = Read.Key(data, ref o);
        Read.U64(data, ref o);               // initial_virtual_token_reserves
        Read.U64(data, ref o);               // initial_virtual_sol_reserves
        Read.U64(data, ref o);               // initial_real_token_reserves
        Read.U64(data, ref o);               // token_total_supply
        var feeBps = Read.U64(data, ref o);
        Read.Key(data, ref o);               // withdraw_authority
        Read.Bool(data, ref o);              // enable_migrate
        Read.U64(data, ref o);               // pool_migration_fee
        var creatorFeeBps = Read.U64(data, ref o);
        Read.Keys(data, ref o, 7);           // fee_recipients
        Read.Key(data, ref o);               // set_creator_authority
        Read.Key(data, ref o);               // admin_set_creator_authority
        Read.Bool(data, ref o);              // create_v2_enabled
        Read.Key(data, ref o);               // whitelist_pda
        Read.Key(data, ref o);               // reserved_fee_recipient
        Read.Bool(data, ref o);              // mayhem_mode_enabled
        Read.Keys(data, ref o, 7);           // reserved_fee_recipients
        Read.Bool(data, ref o);              // is_cashback_enabled
        Read.Keys(data, ref o, 8);           // buyback_fee_recipients
        Read.U64(data, ref o);               // buyback_basis_points
        Read.U64(data, ref o);               // initial_virtual_quote_reserves
        var quoteMints = Read.Keys(data, ref o, 1);

        return new PumpGlobal
        {
            FeeRecipient = feeRecipient,
            FeeBasisPoints = feeBps,
            CreatorFeeBasisPoints = creatorFeeBps,
            WhitelistedQuoteMints = quoteMints,
        };
    }
}

internal static class Read
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
        // low qword is enough for reserves, high stays 0 in practice
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
}
