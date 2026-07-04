using Solnet.Programs;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Solgrid.Pumpfun.Instructions;
using Solgrid.Pumpfun.Math;

namespace Solgrid.Pumpfun.Client;

public partial class PumpClient
{
    public const ulong DefaultCuLimit = 150_000;

    // quote in (usually lamports of sol or usdc) -> tokens out, signed and sent
    public async Task<(bool Ok, string? Error, string? Signature)> BuyAsync(string mint, ulong quoteIn, int slippageBps, ulong cuPrice, ulong cuLimit = DefaultCuLimit)
    {
        if (Trader == null)
            return (false, "no trader account", null);

        var state = await GetCoinStateAsync(mint);
        if (state?.Curve == null)
            return (false, "coin not found", null);
        if (state.Migrated && state.Pool == null)
            return (false, "migrated but pool not found", null);

        var tokensOut = await QuoteBuyTokensAsync(state, quoteIn);
        if (tokensOut == 0)
            return (false, "zero quote", null);
        var maxQuote = quoteIn + quoteIn * (ulong)slippageBps / 10000;

        var g = await GlobalAsync();
        if (g == null)
            return (false, "global not readable", null);

        var builder = BaseBuilder(cuLimit, cuPrice);

        if (!state.Migrated)
        {
            var accounts = new PumpTradeAccounts(
                state.Mint, state.QuoteMint, Pda.BondingCurve(state.Mint), state.Curve.Creator,
                Trader.PublicKey, TokenProgram.ProgramIdKey, TokenProgram.ProgramIdKey,
                g.FeeRecipient, g.FeeRecipient);

            var userAta = Pda.Ata(Trader.PublicKey, TokenProgram.ProgramIdKey, state.Mint);
            await AddAtaIfMissing(builder, userAta, Trader.PublicKey, state.Mint);

            var ix = state.NeedsV2
                ? PumpInstructionsV2.Buy(accounts, tokensOut, maxQuote)
                : PumpInstructions.Buy(accounts, tokensOut, maxQuote);
            builder.AddInstruction(ix);
        }
        else
        {
            var pool = state.Pool!;
            var cfg = await AmmConfigAsync();
            if (cfg == null)
                return (false, "amm config not readable", null);

            var reserves = await PoolReservesAsync(pool);
            var baseOut = Quotes.BuyTokensOnPool(reserves.Base, reserves.Quote, cfg, quoteIn);

            var swap = new AmmSwapAccounts(pool, Trader.PublicKey, Pda.AmmGlobalConfig(),
                cfg.ProtocolFeeRecipients.Length > 0 ? cfg.ProtocolFeeRecipients[0] : new PublicKey(new byte[32]),
                TokenProgram.ProgramIdKey, TokenProgram.ProgramIdKey);

            var userBase = Pda.Ata(Trader.PublicKey, TokenProgram.ProgramIdKey, pool.BaseMint);
            var userQuote = Pda.Ata(Trader.PublicKey, TokenProgram.ProgramIdKey, pool.QuoteMint);
            await AddAtaIfMissing(builder, userBase, Trader.PublicKey, pool.BaseMint);
            await AddAtaIfMissing(builder, userQuote, Trader.PublicKey, pool.QuoteMint);

            builder.AddInstruction(AmmInstructions.Buy(swap, baseOut, maxQuote));
        }

        return await SendAsync(builder, $"buy {mint[..6]}");
    }

    private TransactionBuilder BaseBuilder(ulong cuLimit, ulong cuPrice)
        => new TransactionBuilder()
            .SetFeePayer(Trader!)
            .AddInstruction(ComputeBudgetProgram.SetComputeUnitLimit((uint)cuLimit))
            .AddInstruction(ComputeBudgetProgram.SetComputeUnitPrice((ulong)cuPrice));

    private async Task AddAtaIfMissing(TransactionBuilder builder, PublicKey ata, PublicKey owner, PublicKey mint)
    {
        var info = await _rpc.GetAccountInfoAsync(ata.Key, Commitment.Confirmed);
        if (info.WasSuccessful && info.Result?.Value == null)
            builder.AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(Trader!, owner, mint));
    }

    private async Task<(bool Ok, string? Error, string? Signature)> SendAsync(TransactionBuilder builder, string what)
    {
        var bh = await _rpc.GetLatestBlockHashAsync(Commitment.Confirmed);
        if (!bh.WasSuccessful || bh.Result?.Value == null)
            return (false, "no blockhash", null);

        byte[] tx;
        try
        {
            tx = builder.SetRecentBlockHash(bh.Result.Value.Blockhash).Build(Trader!);
        }
        catch (Exception ex)
        {
            return (false, $"build: {ex.Message}", null);
        }

        var res = await _rpc.SendTransactionAsync(tx, true, Commitment.Confirmed);
        if (!res.WasSuccessful || string.IsNullOrEmpty(res.Result))
            return (false, res.Reason ?? "send failed", null);

        return (true, null, res.Result);
    }
}
