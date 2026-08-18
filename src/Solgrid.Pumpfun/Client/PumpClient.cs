using Solnet.Rpc;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Messages;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Solgrid.Pumpfun.Accounts;
using Solgrid.Pumpfun.Math;

namespace Solgrid.Pumpfun.Client;

public class CoinState
{
    public PublicKey Mint { get; init; } = new(new byte[32]);
    public BondingCurve? Curve { get; init; }
    public AmmPool? Pool { get; init; }
    public bool Migrated => Curve?.Complete == true;
    public PublicKey QuoteMint { get; init; } = new(new byte[32]);
    public PublicKey BaseTokenProgram { get; init; } = Solnet.Programs.TokenProgram.ProgramIdKey;
    public PublicKey QuoteTokenProgram { get; init; } = Solnet.Programs.TokenProgram.ProgramIdKey;
    public bool NeedsV2 => QuoteMint.Key != new PublicKey(new byte[32]).Key;
}

public static class Quotes
{
    // pure quote helpers, kept separate so tests do not need an rpc
    public static ulong BuyTokensOnCurve(BondingCurve curve, ulong feeBps, ulong quoteIn)
        => BondingMath.QuoteBuyTokens(curve.VirtualQuoteReserves, curve.VirtualTokenReserves, quoteIn, feeBps);

    public static ulong SellQuoteOnCurve(BondingCurve curve, ulong feeBps, ulong tokensIn)
        => BondingMath.QuoteSellQuote(curve.VirtualQuoteReserves, curve.VirtualTokenReserves, tokensIn, feeBps);

    public static ulong BuyTokensOnPool(ulong baseR, ulong quoteR, AmmGlobalConfig cfg, ulong quoteIn)
        => PoolMath.BuyBaseOut(baseR, quoteR, quoteIn, cfg.LpFeeBasisPoints, cfg.ProtocolFeeBasisPoints, cfg.CoinCreatorFeeBasisPoints);

    public static ulong SellQuoteOnPool(ulong baseR, ulong quoteR, AmmGlobalConfig cfg, ulong baseIn)
        => PoolMath.SellQuoteOut(baseR, quoteR, baseIn, cfg.LpFeeBasisPoints, cfg.ProtocolFeeBasisPoints, cfg.CoinCreatorFeeBasisPoints);
}

public partial class PumpClient
{
    private readonly IRpcClient _rpc;
    private PumpGlobal? _global;
    private AmmGlobalConfig? _ammConfig;

    public Account? Trader { get; }
    public IRpcClient Rpc => _rpc;

    public PumpClient(IRpcClient rpc, Account? trader = null)
    {
        _rpc = rpc;
        Trader = trader;
    }

    public async Task<PumpGlobal?> GlobalAsync(bool refresh = false)
    {
        if (_global != null && !refresh) return _global;
        var acc = await _rpc.GetAccountInfoAsync(Pda.Global().Key, Commitment.Confirmed);
        _global = TryAccount(acc, PumpGlobal.Deserialize);
        return _global;
    }

    public async Task<AmmGlobalConfig?> AmmConfigAsync(bool refresh = false)
    {
        if (_ammConfig != null && !refresh) return _ammConfig;
        var acc = await _rpc.GetAccountInfoAsync(Pda.AmmGlobalConfig().Key, Commitment.Confirmed);
        _ammConfig = TryAccount(acc, AmmGlobalConfig.Deserialize);
        return _ammConfig;
    }

    private readonly Dictionary<string, PublicKey> _tokenPrograms = new();

    // spl-token or token-2022, from the mint account owner; pump coins mint
    // both ways and the ata create must match or the tx dies
    public async Task<PublicKey> TokenProgramOfAsync(PublicKey mint)
    {
        var key = mint.Key;
        if (_tokenPrograms.TryGetValue(key, out var cached))
            return cached;

        var info = await _rpc.GetAccountInfoAsync(key, Commitment.Confirmed);
        var owner = info.WasSuccessful && info.Result?.Value != null
            ? new PublicKey(info.Result.Value.Owner)
            : Solnet.Programs.TokenProgram.ProgramIdKey;
        _tokenPrograms[key] = owner;
        return owner;
    }

    public async Task<CoinState?> GetCoinStateAsync(string mint)
    {
        var mintKey = new PublicKey(mint);
        var curveAddr = Pda.BondingCurve(mintKey);
        var acc = await _rpc.GetAccountInfoAsync(curveAddr.Key, Commitment.Confirmed);
        var curve = TryAccount(acc, BondingCurve.Deserialize);
        if (curve == null)
            return null;

        var baseTp = await TokenProgramOfAsync(mintKey);
        var quoteTp = curve.QuoteMint.Key == new PublicKey(new byte[32]).Key
            ? baseTp
            : await TokenProgramOfAsync(curve.QuoteMint);
        var quoteMint = curve.QuoteMint.Key == new PublicKey(new byte[32]).Key
            ? Addresses.NativeMint
            : curve.QuoteMint;

        var state = new CoinState
        {
            Mint = mintKey,
            Curve = curve,
            QuoteMint = quoteMint,
            BaseTokenProgram = baseTp,
            QuoteTokenProgram = quoteTp,
        };
        if (!curve.Complete)
            return state;

        // canonical pool derives directly; scan only as a last resort for
        // non-canonical pools
        var pool = await GetPoolAsync(Pda.CanonicalPool(mintKey, quoteMint))
            ?? await FindPoolAsync(mintKey);
        return new CoinState
        {
            Mint = mintKey,
            Curve = curve,
            Pool = pool,
            QuoteMint = pool?.QuoteMint ?? quoteMint,
            BaseTokenProgram = baseTp,
            QuoteTokenProgram = pool != null ? await TokenProgramOfAsync(pool.QuoteMint) : quoteTp,
        };
    }

    public async Task<AmmPool?> GetPoolAsync(PublicKey pool)
    {
        var acc = await _rpc.GetAccountInfoAsync(pool.Key, Commitment.Confirmed);
        return TryAccount(acc, AmmPool.Deserialize);
    }

    // pool pda needs index+creator we do not have, so filter program accounts
    // by base mint; happens once per migrated coin, cached by callers
    public async Task<AmmPool?> FindPoolAsync(PublicKey baseMint)
    {
        var filters = new List<MemCmp> { new() { Offset = 43, Bytes = baseMint.Key } };
        var res = await _rpc.GetProgramAccountsAsync(Addresses.PumpAmm.Key, Commitment.Confirmed, 178, filters);
        if (!res.WasSuccessful || res.Result == null)
            return null;
        foreach (var acc in res.Result)
        {
            var pool = AmmPool.Deserialize(Convert.FromBase64String(acc.Account.Data[0]));
            if (pool != null)
                return pool;
        }
        return null;
    }

    public async Task<ulong> QuoteBuyTokensAsync(CoinState state, ulong quoteIn)
    {
        if (!state.Migrated)
        {
            var g = await GlobalAsync();
            return Quotes.BuyTokensOnCurve(state.Curve!, g?.FeeBasisPoints ?? 100, quoteIn);
        }

        var (baseR, quoteR) = await PoolReservesAsync(state.Pool!);
        var cfg = await AmmConfigAsync();
        if (cfg == null) return 0;
        return Quotes.BuyTokensOnPool(baseR, quoteR, cfg, quoteIn);
    }

    public async Task<ulong> QuoteSellQuoteAsync(CoinState state, ulong tokensOrBaseIn)
    {
        if (!state.Migrated)
        {
            var g = await GlobalAsync();
            return Quotes.SellQuoteOnCurve(state.Curve!, g?.FeeBasisPoints ?? 100, tokensOrBaseIn);
        }

        var (baseR, quoteR) = await PoolReservesAsync(state.Pool!);
        var cfg = await AmmConfigAsync();
        if (cfg == null) return 0;
        return Quotes.SellQuoteOnPool(baseR, quoteR, cfg, tokensOrBaseIn);
    }

    public async Task<(ulong Base, ulong Quote)> PoolReservesAsync(AmmPool pool)
    {
        var baseAcc = await _rpc.GetAccountInfoAsync(pool.PoolBaseTokenAccount.Key, Commitment.Confirmed);
        var quoteAcc = await _rpc.GetAccountInfoAsync(pool.PoolQuoteTokenAccount.Key, Commitment.Confirmed);
        var baseR = TokenAmount(baseAcc);
        var quoteR = PoolMath.EffectiveQuoteReserves(TokenAmount(quoteAcc), pool.VirtualQuoteReserves);
        return (baseR, quoteR);
    }

    private static ulong TokenAmount(RequestResult<ResponseValue<AccountInfo>> res)
    {
        // spl token account: amount is u64 at offset 64
        var raw = TryData(res);
        if (raw == null || raw.Length < 72)
            return 0;
        ulong v = 0;
        for (int i = 7; i >= 0; i--)
            v = (v << 8) | raw[64 + i];
        return v;
    }

    private static T? TryAccount<T>(RequestResult<ResponseValue<AccountInfo>> res, Func<byte[], T?> parse) where T : class
        => parse(TryData(res) ?? Array.Empty<byte>());

    private static byte[]? TryData(RequestResult<ResponseValue<AccountInfo>> res)
    {
        if (!res.WasSuccessful || res.Result?.Value?.Data == null || res.Result.Value.Data.Count == 0)
            return null;
        try { return Convert.FromBase64String(res.Result.Value.Data[0]); }
        catch { return null; }
    }
}
