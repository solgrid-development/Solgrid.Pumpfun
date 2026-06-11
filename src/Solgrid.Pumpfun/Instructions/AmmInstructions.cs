using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace Solgrid.Pumpfun.Instructions;

public record AmmSwapAccounts(
    Accounts.AmmPool Pool,
    PublicKey User,
    PublicKey GlobalConfig,
    PublicKey ProtocolFeeRecipient,
    PublicKey BaseTokenProgram,
    PublicKey QuoteTokenProgram);

public static class AmmInstructions
{
    public static readonly byte[] BuyDisc = { 102, 6, 61, 18, 1, 218, 235, 234 };
    public static readonly byte[] SellDisc = { 51, 230, 133, 164, 1, 127, 131, 173 };

    private static List<AccountMeta> Keys(AmmSwapAccounts a, bool withVolume)
    {
        var pool = a.Pool;
        var poolAddr = Pda.AmmPool(pool.Index, pool.Creator, pool.BaseMint, pool.QuoteMint);
        var creatorAuthority = Pda.AmmCoinCreatorVaultAuthority(pool.CoinCreator);
        var ata = AssociatedTokenAccountProgram.ProgramIdKey;
        var uva = Pda.UserVolumeAccumulator(Addresses.PumpAmm, a.User);

        var keys = new List<AccountMeta>
        {
            AccountMeta.Writable(poolAddr, false),
            AccountMeta.Writable(a.User, true),
            AccountMeta.ReadOnly(a.GlobalConfig, false),
            AccountMeta.ReadOnly(pool.BaseMint, false),
            AccountMeta.ReadOnly(pool.QuoteMint, false),
            AccountMeta.Writable(Pda.Ata(a.User, a.BaseTokenProgram, pool.BaseMint), false),
            AccountMeta.Writable(Pda.Ata(a.User, a.QuoteTokenProgram, pool.QuoteMint), false),
            AccountMeta.Writable(pool.PoolBaseTokenAccount, false),
            AccountMeta.Writable(pool.PoolQuoteTokenAccount, false),
            AccountMeta.ReadOnly(a.ProtocolFeeRecipient, false),
            AccountMeta.Writable(Pda.Ata(a.ProtocolFeeRecipient, a.QuoteTokenProgram, pool.QuoteMint), false),
            AccountMeta.ReadOnly(a.BaseTokenProgram, false),
            AccountMeta.ReadOnly(a.QuoteTokenProgram, false),
            AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            AccountMeta.ReadOnly(ata, false),
            AccountMeta.ReadOnly(Pda.EventAuthority(Addresses.PumpAmm), false),
            AccountMeta.ReadOnly(Addresses.PumpAmm, false),
            AccountMeta.Writable(Pda.Ata(creatorAuthority, a.QuoteTokenProgram, pool.QuoteMint), false),
            AccountMeta.ReadOnly(creatorAuthority, false),
        };

        if (withVolume)
        {
            keys.Add(AccountMeta.ReadOnly(Pda.GlobalVolumeAccumulator(Addresses.PumpAmm), false));
            keys.Add(AccountMeta.Writable(uva, false));
        }

        keys.Add(AccountMeta.ReadOnly(Pda.FeeConfig(Addresses.PumpAmm), false));
        keys.Add(AccountMeta.ReadOnly(Addresses.PumpFees, false));
        return keys;
    }

    public static TransactionInstruction Buy(AmmSwapAccounts a, ulong baseAmountOut, ulong maxQuoteAmountIn, bool trackVolume = true)
    {
        var data = new byte[8 + 8 + 8 + 1];
        BuyDisc.CopyTo(data, 0);
        PumpInstructions.WriteU64Public(data, 8, baseAmountOut);
        PumpInstructions.WriteU64Public(data, 16, maxQuoteAmountIn);
        data[24] = (byte)(trackVolume ? 1 : 0);
        return new TransactionInstruction { Keys = Keys(a, true), ProgramId = Addresses.PumpAmm, Data = data };
    }

    public static TransactionInstruction Sell(AmmSwapAccounts a, ulong baseAmountIn, ulong minQuoteAmountOut)
    {
        var data = new byte[24];
        SellDisc.CopyTo(data, 0);
        PumpInstructions.WriteU64Public(data, 8, baseAmountIn);
        PumpInstructions.WriteU64Public(data, 16, minQuoteAmountOut);
        return new TransactionInstruction { Keys = Keys(a, false), ProgramId = Addresses.PumpAmm, Data = data };
    }
}
