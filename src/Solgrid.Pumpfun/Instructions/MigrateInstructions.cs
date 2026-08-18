using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace Solgrid.Pumpfun.Instructions;

public record MigrateAccounts(
    PublicKey Mint,
    PublicKey QuoteMint,
    PublicKey User,
    PublicKey WithdrawAuthority,
    PublicKey BaseTokenProgram,
    PublicKey QuoteTokenProgram);

public static class MigrateInstructions
{
    public static readonly byte[] MigrateV2Disc = { 187, 203, 18, 31, 206, 237, 254, 41 };

    // graduation of a bonding curve into the canonical pumpswap pool;
    // permissionless, account order follows the IDL migrate_v2
    public static TransactionInstruction MigrateV2(MigrateAccounts a)
    {
        var curve = Pda.BondingCurve(a.Mint);
        var pool = Pda.CanonicalPool(a.Mint, a.QuoteMint);
        var poolAuthority = Pda.PumpPoolAuthority(a.Mint);
        var lpMint = Pda.AmmPoolLpMint(pool);

        var keys = new List<AccountMeta>
        {
            AccountMeta.ReadOnly(Pda.Global(), false),
            AccountMeta.Writable(a.WithdrawAuthority, false),
            AccountMeta.ReadOnly(a.Mint, false),
            AccountMeta.ReadOnly(a.QuoteMint, false),
            AccountMeta.Writable(curve, false),
            AccountMeta.Writable(Pda.AssociatedBondingCurve(curve, a.BaseTokenProgram, a.Mint), false),
            AccountMeta.Writable(Pda.Ata(curve, a.QuoteTokenProgram, a.QuoteMint), false),
            AccountMeta.ReadOnly(a.User, true),
            AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            AccountMeta.ReadOnly(Addresses.PumpAmm, false),
            AccountMeta.Writable(pool, false),
            AccountMeta.Writable(poolAuthority, false),
            AccountMeta.Writable(Pda.Ata(poolAuthority, a.BaseTokenProgram, a.Mint), false),
            AccountMeta.Writable(Pda.Ata(poolAuthority, a.QuoteTokenProgram, a.QuoteMint), false),
            AccountMeta.ReadOnly(Pda.AmmGlobalConfig(), false),
            AccountMeta.Writable(lpMint, false),
            AccountMeta.Writable(Pda.Ata(a.User, Addresses.Token2022, lpMint), false),
            AccountMeta.Writable(Pda.Ata(pool, a.BaseTokenProgram, a.Mint), false),
            AccountMeta.Writable(Pda.Ata(pool, a.QuoteTokenProgram, a.QuoteMint), false),
            AccountMeta.ReadOnly(a.BaseTokenProgram, false),
            AccountMeta.ReadOnly(a.QuoteTokenProgram, false),
            AccountMeta.ReadOnly(Addresses.Token2022, false),
            AccountMeta.ReadOnly(AssociatedTokenAccountProgram.ProgramIdKey, false),
            AccountMeta.ReadOnly(Pda.EventAuthority(Addresses.PumpAmm), false),
            AccountMeta.ReadOnly(SysVars.RentKey, false),
            AccountMeta.ReadOnly(Pda.EventAuthority(Addresses.Pump), false),
            AccountMeta.ReadOnly(Addresses.Pump, false),
        };

        return new TransactionInstruction
        {
            Keys = keys,
            ProgramId = Addresses.Pump,
            Data = MigrateV2Disc,
        };
    }
}
