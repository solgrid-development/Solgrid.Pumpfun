using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace Solgrid.Pumpfun.Instructions;

public static class PumpInstructionsV2
{
    // v2 = quote-mint aware (usdc paired coins), creator + buyback fee atas,
    // sharing config and volume accumulators in every trade
    private static List<AccountMeta> V2Keys(PumpTradeAccounts a, bool withGlobalVolume)
    {
        var creatorVault = Pda.CreatorVault(a.Creator);
        var uva = Pda.UserVolumeAccumulator(Addresses.Pump, a.User);
        var ata = AssociatedTokenAccountProgram.ProgramIdKey;

        var keys = new List<AccountMeta>
        {
            AccountMeta.ReadOnly(Pda.Global(), false),
            AccountMeta.Writable(a.FeeRecipient, false),
            AccountMeta.ReadOnly(a.Mint, false),
            AccountMeta.ReadOnly(a.QuoteMint, false),
            AccountMeta.ReadOnly(a.BaseTokenProgram, false),
            AccountMeta.ReadOnly(a.QuoteTokenProgram, false),
            AccountMeta.ReadOnly(ata, false),
            AccountMeta.Writable(Pda.Ata(a.FeeRecipient, a.QuoteTokenProgram, a.QuoteMint), false),
            AccountMeta.Writable(a.BuybackFeeRecipient, false),
            AccountMeta.Writable(Pda.Ata(a.BuybackFeeRecipient, a.QuoteTokenProgram, a.QuoteMint), false),
            AccountMeta.Writable(a.Curve, false),
            AccountMeta.Writable(Pda.AssociatedBondingCurve(a.Curve, a.BaseTokenProgram, a.Mint), false),
            AccountMeta.Writable(Pda.Ata(a.Curve, a.QuoteTokenProgram, a.QuoteMint), false),
            AccountMeta.Writable(a.User, true),
            AccountMeta.Writable(Pda.Ata(a.User, a.BaseTokenProgram, a.Mint), false),
            AccountMeta.Writable(Pda.Ata(a.User, a.QuoteTokenProgram, a.QuoteMint), false),
            AccountMeta.Writable(creatorVault, false),
            AccountMeta.Writable(Pda.Ata(creatorVault, a.QuoteTokenProgram, a.QuoteMint), false),
            AccountMeta.ReadOnly(Pda.SharingConfig(a.Mint), false),
        };

        if (withGlobalVolume)
            keys.Add(AccountMeta.ReadOnly(Pda.GlobalVolumeAccumulator(Addresses.Pump), false));

        keys.Add(AccountMeta.Writable(uva, false));
        keys.Add(AccountMeta.Writable(Pda.Ata(uva, a.QuoteTokenProgram, a.QuoteMint), false));
        keys.Add(AccountMeta.ReadOnly(Pda.FeeConfig(Addresses.Pump), false));
        keys.Add(AccountMeta.ReadOnly(Addresses.PumpFees, false));
        keys.Add(AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false));
        keys.Add(AccountMeta.ReadOnly(Pda.EventAuthority(Addresses.Pump), false));
        keys.Add(AccountMeta.ReadOnly(Addresses.Pump, false));
        return keys;
    }

    private static byte[] Args(byte[] disc, ulong first, ulong second)
    {
        var data = new byte[24];
        disc.CopyTo(data, 0);
        PumpInstructions.WriteU64Public(data, 8, first);
        PumpInstructions.WriteU64Public(data, 16, second);
        return data;
    }

    public static TransactionInstruction Buy(PumpTradeAccounts a, ulong tokenAmountOut, ulong maxQuoteCost)
        => new() { Keys = V2Keys(a, true), ProgramId = Addresses.Pump, Data = Args(PumpInstructions.BuyV2Disc, tokenAmountOut, maxQuoteCost) };
}
