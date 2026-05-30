using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace Solgrid.Pumpfun.Instructions;

public record PumpTradeAccounts(
    PublicKey Mint,
    PublicKey QuoteMint,
    PublicKey Curve,
    PublicKey Creator,
    PublicKey User,
    PublicKey BaseTokenProgram,
    PublicKey QuoteTokenProgram,
    PublicKey FeeRecipient,
    PublicKey BuybackFeeRecipient);

public static class PumpInstructions
{
    public static readonly byte[] BuyDisc = { 102, 6, 61, 18, 1, 218, 235, 234 };
    public static readonly byte[] SellDisc = { 51, 230, 133, 164, 1, 127, 131, 173 };
    public static readonly byte[] BuyV2Disc = { 184, 23, 238, 97, 103, 197, 211, 61 };
    public static readonly byte[] SellV2Disc = { 93, 246, 130, 60, 231, 233, 64, 178 };
    public static readonly byte[] BuyExactQuoteInV2Disc = { 194, 171, 28, 70, 104, 77, 91, 47 };

    // current v1 buy: creator vault, volume accumulators and the fee
    // program cpi are mandatory now, the old 12-account layout fails
    public static TransactionInstruction Buy(PumpTradeAccounts a, ulong tokenAmountOut, ulong maxQuoteCost, bool trackVolume = true)
    {
        var assocCurve = Pda.AssociatedBondingCurve(a.Curve, a.BaseTokenProgram, a.Mint);
        var userAta = Pda.Ata(a.User, a.BaseTokenProgram, a.Mint);
        var creatorVault = Pda.CreatorVault(a.Creator);

        var keys = new List<AccountMeta>
        {
            AccountMeta.ReadOnly(Pda.Global(), false),
            AccountMeta.Writable(a.FeeRecipient, false),
            AccountMeta.ReadOnly(a.Mint, false),
            AccountMeta.Writable(a.Curve, false),
            AccountMeta.Writable(assocCurve, false),
            AccountMeta.Writable(userAta, false),
            AccountMeta.Writable(a.User, true),
            AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            AccountMeta.ReadOnly(a.BaseTokenProgram, false),
            AccountMeta.Writable(creatorVault, false),
            AccountMeta.ReadOnly(Pda.EventAuthority(Addresses.Pump), false),
            AccountMeta.ReadOnly(Addresses.Pump, false),
            AccountMeta.ReadOnly(Pda.GlobalVolumeAccumulator(Addresses.Pump), false),
            AccountMeta.Writable(Pda.UserVolumeAccumulator(Addresses.Pump, a.User), false),
            AccountMeta.ReadOnly(Pda.FeeConfig(Addresses.Pump), false),
            AccountMeta.ReadOnly(Addresses.PumpFees, false),
        };

        var data = new byte[8 + 8 + 8 + 1];
        BuyDisc.CopyTo(data, 0);
        ByteRW.WriteU64(data, 8, tokenAmountOut);
        ByteRW.WriteU64(data, 16, maxQuoteCost);
        data[24] = (byte)(trackVolume ? 1 : 0);

        return new TransactionInstruction { Keys = keys, ProgramId = Addresses.Pump, Data = data };
    }

    public static TransactionInstruction Sell(PumpTradeAccounts a, ulong tokenAmountIn, ulong minQuoteOut)
    {
        var assocCurve = Pda.AssociatedBondingCurve(a.Curve, a.BaseTokenProgram, a.Mint);
        var userAta = Pda.Ata(a.User, a.BaseTokenProgram, a.Mint);
        var creatorVault = Pda.CreatorVault(a.Creator);

        var keys = new List<AccountMeta>
        {
            AccountMeta.ReadOnly(Pda.Global(), false),
            AccountMeta.Writable(a.FeeRecipient, false),
            AccountMeta.ReadOnly(a.Mint, false),
            AccountMeta.Writable(a.Curve, false),
            AccountMeta.Writable(assocCurve, false),
            AccountMeta.Writable(userAta, false),
            AccountMeta.Writable(a.User, true),
            AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            AccountMeta.Writable(creatorVault, false),
            AccountMeta.ReadOnly(a.BaseTokenProgram, false),
            AccountMeta.ReadOnly(Pda.EventAuthority(Addresses.Pump), false),
            AccountMeta.ReadOnly(Addresses.Pump, false),
            AccountMeta.ReadOnly(Pda.FeeConfig(Addresses.Pump), false),
            AccountMeta.ReadOnly(Addresses.PumpFees, false),
        };

        var data = new byte[24];
        SellDisc.CopyTo(data, 0);
        ByteRW.WriteU64(data, 8, tokenAmountIn);
        ByteRW.WriteU64(data, 16, minQuoteOut);

        return new TransactionInstruction { Keys = keys, ProgramId = Addresses.Pump, Data = data };
    }

    internal static void WriteU64Public(byte[] dst, int offset, ulong value) => ByteRW.WriteU64(dst, offset, value);
}
