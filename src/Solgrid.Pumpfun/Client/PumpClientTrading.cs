using Solnet.Programs;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Solgrid.Pumpfun.Accounts;
using Solgrid.Pumpfun.Instructions;
using Solgrid.Pumpfun.Math;

namespace Solgrid.Pumpfun.Client;

public partial class PumpClient
{
    public const ulong DefaultCuLimit = 150_000;

    // builds the signed buy tx without sending; sim tools and tests use this
    public async Task<(byte[]? Tx, string? Error)> BuildBuyAsync(string mint, ulong quoteIn, int slippageBps, ulong cuPrice, ulong cuLimit = DefaultCuLimit)
    {
        if (Trader == null)
            return (null, "no trader account");

        var state = await GetCoinStateAsync(mint);
        if (state?.Curve == null)
            return (null, "coin not found");
        if (state.Migrated && state.Pool == null)
            return (null, "migrated but pool not found");

        var tokensOut = await QuoteBuyTokensAsync(state, quoteIn);
        if (tokensOut == 0)
            return (null, "zero quote");
        var maxQuote = quoteIn + quoteIn * (ulong)slippageBps / 10000;

        var g = await GlobalAsync();
        if (g == null)
            return (null, "global not readable");

        var builder = BaseBuilder(cuLimit, cuPrice);

        if (!state.Migrated)
        {
            var accounts = new PumpTradeAccounts(
                state.Mint, state.QuoteMint, Pda.BondingCurve(state.Mint), state.Curve.Creator,
                Trader.PublicKey, state.BaseTokenProgram, state.QuoteTokenProgram,
                PickFeeRecipient(g, state.Curve.IsMayhemMode), PickBuyback(g));

            var userAta = Pda.Ata(Trader.PublicKey, state.BaseTokenProgram, state.Mint);
            await AddAtaIfMissing(builder, userAta, Trader.PublicKey, state.Mint, state.BaseTokenProgram);

            var ix = state.NeedsV2
                ? PumpInstructionsV2.Buy(accounts, tokensOut, maxQuote)
                : PumpInstructions.Buy(accounts, tokensOut, maxQuote, accounts.BuybackFeeRecipient);
            builder.AddInstruction(ix);
        }
        else
        {
            var pool = state.Pool!;
            var reserves = await PoolReservesAsync(pool);
            var cfg = await AmmConfigAsync();
            if (cfg == null)
                return (null, "amm config not readable");

            var baseOut = Quotes.BuyTokensOnPool(reserves.Base, reserves.Quote, cfg, quoteIn);

            var swap = new AmmSwapAccounts(pool, Trader.PublicKey, Pda.AmmGlobalConfig(),
                cfg.ProtocolFeeRecipients.Length > 0 ? cfg.ProtocolFeeRecipients[0] : new PublicKey(new byte[32]),
                state.BaseTokenProgram, state.QuoteTokenProgram);

            var userBase = Pda.Ata(Trader.PublicKey, state.BaseTokenProgram, pool.BaseMint);
            var userQuote = Pda.Ata(Trader.PublicKey, state.QuoteTokenProgram, pool.QuoteMint);
            await AddAtaIfMissing(builder, userBase, Trader.PublicKey, pool.BaseMint, state.BaseTokenProgram);
            await AddAtaIfMissing(builder, userQuote, Trader.PublicKey, pool.QuoteMint, state.QuoteTokenProgram);

            builder.AddInstruction(AmmInstructions.Buy(swap, baseOut, maxQuote));
        }

        return await SignAsync(builder);
    }

    public async Task<(bool Ok, string? Error, string? Signature)> BuyAsync(string mint, ulong quoteIn, int slippageBps, ulong cuPrice, ulong cuLimit = DefaultCuLimit)
    {
        var (tx, err) = await BuildBuyAsync(mint, quoteIn, slippageBps, cuPrice, cuLimit);
        if (tx == null)
            return (false, err, null);
        return await SendRawAsync(tx, $"buy {mint[..6]}");
    }

    public async Task<(bool Ok, string? Error, string? Signature)> SellAsync(string mint, ulong baseIn, int slippageBps, ulong cuPrice, bool closeAta = false, ulong cuLimit = DefaultCuLimit)
    {
        if (Trader == null)
            return (false, "no trader account", null);

        var state = await GetCoinStateAsync(mint);
        if (state?.Curve == null)
            return (false, "coin not found", null);

        var g = await GlobalAsync();
        if (g == null)
            return (false, "global not readable", null);

        var builder = BaseBuilder(cuLimit, cuPrice);

        if (!state.Migrated)
        {
            var minQuote = await QuoteSellQuoteAsync(state, baseIn);
            minQuote = minQuote > minQuote * (ulong)slippageBps / 10000
                ? minQuote - minQuote * (ulong)slippageBps / 10000
                : 0;

            var accounts = new PumpTradeAccounts(
                state.Mint, state.QuoteMint, Pda.BondingCurve(state.Mint), state.Curve.Creator,
                Trader.PublicKey, state.BaseTokenProgram, state.QuoteTokenProgram,
                PickFeeRecipient(g, state.Curve.IsMayhemMode), PickBuyback(g));

            var ix = state.NeedsV2
                ? PumpInstructionsV2.Sell(accounts, baseIn, minQuote)
                : PumpInstructions.Sell(accounts, baseIn, minQuote, accounts.BuybackFeeRecipient, state.Curve.IsCashbackCoin);
            builder.AddInstruction(ix);

            if (closeAta)
                builder.AddInstruction(TokenProgram.CloseAccount(
                    Pda.Ata(Trader.PublicKey, state.BaseTokenProgram, state.Mint),
                    Trader.PublicKey, Trader.PublicKey, state.BaseTokenProgram));
        }
        else
        {
            if (state.Pool == null)
                return (false, "migrated but pool not found", null);

            var reserves = await PoolReservesAsync(state.Pool);
            var cfg = await AmmConfigAsync();
            if (cfg == null)
                return (false, "amm config not readable", null);

            var quoteOut = Quotes.SellQuoteOnPool(reserves.Base, reserves.Quote, cfg, baseIn);
            var minOut = quoteOut > quoteOut * (ulong)slippageBps / 10000
                ? quoteOut - quoteOut * (ulong)slippageBps / 10000
                : 0;

            var swap = new AmmSwapAccounts(state.Pool, Trader.PublicKey, Pda.AmmGlobalConfig(),
                cfg.ProtocolFeeRecipients.Length > 0 ? cfg.ProtocolFeeRecipients[0] : new PublicKey(new byte[32]),
                state.BaseTokenProgram, state.QuoteTokenProgram);
            builder.AddInstruction(AmmInstructions.Sell(swap, baseIn, minOut));
        }

        return await SendAsync(builder, $"sell {mint[..6]}");
    }

    // the program accepts any authorized recipient; official sdk picks a
    // random one from the global lists, so do we
    private static PublicKey PickBuyback(PumpGlobal g)
    {
        var zero = new PublicKey(new byte[32]);
        var list = g.BuybackFeeRecipients.Where(k => k.Key != zero.Key).ToList();
        return list.Count == 0 ? zero : list[Random.Shared.Next(list.Count)];
    }

    private static PublicKey PickFeeRecipient(PumpGlobal g, bool mayhem)
    {
        var zero = new PublicKey(new byte[32]);
        var list = mayhem
            ? new List<PublicKey> { g.ReservedFeeRecipient }.Concat(g.ReservedFeeRecipients).ToList()
            : new List<PublicKey> { g.FeeRecipient }.Concat(g.FeeRecipients).ToList();
        list = list.Where(k => k.Key != zero.Key).ToList();
        return list.Count == 0 ? g.FeeRecipient : list[Random.Shared.Next(list.Count)];
    }

    private TransactionBuilder BaseBuilder(ulong cuLimit, ulong cuPrice)
        => new TransactionBuilder()
            .SetFeePayer(Trader!)
            .AddInstruction(ComputeBudgetProgram.SetComputeUnitLimit((uint)cuLimit))
            .AddInstruction(ComputeBudgetProgram.SetComputeUnitPrice((ulong)cuPrice));

    private async Task AddAtaIfMissing(TransactionBuilder builder, PublicKey ata, PublicKey owner, PublicKey mint, PublicKey tokenProgram)
    {
        var info = await _rpc.GetAccountInfoAsync(ata.Key, Commitment.Confirmed);
        if (info.WasSuccessful && info.Result?.Value == null)
            builder.AddInstruction(AtaInstructions.Create(Trader!, owner, mint, tokenProgram));
    }

    private async Task<(byte[]? Tx, string? Error)> SignAsync(TransactionBuilder builder)
    {
        var bh = await _rpc.GetLatestBlockHashAsync(Commitment.Confirmed);
        SdkLog.Trace($"blockhash={bh.Result?.Value?.Blockhash} ok={bh.WasSuccessful}");
        if (!bh.WasSuccessful || bh.Result?.Value == null)
            return (null, "no blockhash");

        try
        {
            var tx = builder.SetRecentBlockHash(bh.Result.Value.Blockhash).Build(Trader!);
        SdkLog.Trace($"signed tx len={tx.Length} feePayer={Trader.PublicKey.Key[..8]}");
        return (tx, null);
        }
        catch (Exception ex)
        {
            return (null, $"build: {ex.Message}");
        }
    }

    private async Task<(bool Ok, string? Error, string? Signature)> SendRawAsync(byte[] tx, string what)
    {
        SdkLog.Trace($"sendTransaction what={what} len={tx.Length}");
        var res = await _rpc.SendTransactionAsync(tx, true, Commitment.Confirmed);
        SdkLog.Trace($"send result ok={res.WasSuccessful} sig={res.Result} reason={res.Reason}");
        if (!res.WasSuccessful || string.IsNullOrEmpty(res.Result))
            return (false, res.Reason ?? "send failed", null);

        return (true, null, res.Result);
    }

    private async Task<(bool Ok, string? Error, string? Signature)> SendAsync(TransactionBuilder builder, string what)
    {
        var (tx, err) = await SignAsync(builder);
        if (tx == null)
            return (false, err, null);
        return await SendRawAsync(tx, what);
    }
}
