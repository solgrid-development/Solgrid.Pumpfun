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
            VirtualTokenReserves = ByteRW.U64(data, ref o),
            VirtualQuoteReserves = ByteRW.U64(data, ref o),
            RealTokenReserves = ByteRW.U64(data, ref o),
            RealQuoteReserves = ByteRW.U64(data, ref o),
            TokenTotalSupply = ByteRW.U64(data, ref o),
            Complete = ByteRW.Bool(data, ref o),
            Creator = ByteRW.Key(data, ref o),
            IsMayhemMode = ByteRW.Bool(data, ref o),
            IsCashbackCoin = ByteRW.Bool(data, ref o),
            QuoteMint = ByteRW.Key(data, ref o),
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
        ByteRW.Bool(data, ref o);              // initialized
        ByteRW.Key(data, ref o);               // authority
        var feeRecipient = ByteRW.Key(data, ref o);
        ByteRW.U64(data, ref o);               // initial_virtual_token_reserves
        ByteRW.U64(data, ref o);               // initial_virtual_sol_reserves
        ByteRW.U64(data, ref o);               // initial_real_token_reserves
        ByteRW.U64(data, ref o);               // token_total_supply
        var feeBps = ByteRW.U64(data, ref o);
        ByteRW.Key(data, ref o);               // withdraw_authority
        ByteRW.Bool(data, ref o);              // enable_migrate
        ByteRW.U64(data, ref o);               // pool_migration_fee
        var creatorFeeBps = ByteRW.U64(data, ref o);
        ByteRW.Keys(data, ref o, 7);           // fee_recipients
        ByteRW.Key(data, ref o);               // set_creator_authority
        ByteRW.Key(data, ref o);               // admin_set_creator_authority
        ByteRW.Bool(data, ref o);              // create_v2_enabled
        ByteRW.Key(data, ref o);               // whitelist_pda
        ByteRW.Key(data, ref o);               // reserved_fee_recipient
        ByteRW.Bool(data, ref o);              // mayhem_mode_enabled
        ByteRW.Keys(data, ref o, 7);           // reserved_fee_recipients
        ByteRW.Bool(data, ref o);              // is_cashback_enabled
        ByteRW.Keys(data, ref o, 8);           // buyback_fee_recipients
        ByteRW.U64(data, ref o);               // buyback_basis_points
        ByteRW.U64(data, ref o);               // initial_virtual_quote_reserves
        var quoteMints = ByteRW.Keys(data, ref o, 1);

        return new PumpGlobal
        {
            FeeRecipient = feeRecipient,
            FeeBasisPoints = feeBps,
            CreatorFeeBasisPoints = creatorFeeBps,
            WhitelistedQuoteMints = quoteMints,
        };
    }
}
