using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace Solgrid.Pumpfun.Instructions;

public static class AtaInstructions
{
    // solnet's helper hardcodes spl-token; token-2022 mints need their own
    // program in the account list or the create fails with IncorrectProgramId
    public static TransactionInstruction Create(PublicKey payer, PublicKey owner, PublicKey mint, PublicKey tokenProgram)
    {
        var ata = Pda.Ata(owner, tokenProgram, mint);
        var keys = new List<AccountMeta>
        {
            AccountMeta.Writable(payer, true),
            AccountMeta.Writable(ata, false),
            AccountMeta.ReadOnly(owner, false),
            AccountMeta.ReadOnly(mint, false),
            AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            AccountMeta.ReadOnly(tokenProgram, false),
        };
        return new TransactionInstruction
        {
            Keys = keys,
            ProgramId = AssociatedTokenAccountProgram.ProgramIdKey,
            Data = Array.Empty<byte>(),
        };
    }
}
