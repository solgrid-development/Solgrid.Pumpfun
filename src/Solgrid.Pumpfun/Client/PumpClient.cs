using Solnet.Rpc;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Messages;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Solgrid.Pumpfun.Accounts;

namespace Solgrid.Pumpfun.Client;

public class CoinState
{
    public PublicKey Mint { get; init; } = new(new byte[32]);
    public BondingCurve? Curve { get; init; }
    public AmmPool? Pool { get; init; }
    public bool Migrated => Curve?.Complete == true;
    public PublicKey QuoteMint { get; init; } = new(new byte[32]);
    public bool NeedsV2 => QuoteMint.Key != new PublicKey(new byte[32]).Key;
}

public partial class PumpClient
{
    private readonly IRpcClient _rpc;
    private PumpGlobal? _global;
    private AmmGlobalConfig? _ammConfig;

    public Account? Trader { get; }

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

    public async Task<CoinState?> GetCoinStateAsync(string mint)
    {
        var mintKey = new PublicKey(mint);
        var curveAddr = Pda.BondingCurve(mintKey);
        var acc = await _rpc.GetAccountInfoAsync(curveAddr.Key, Commitment.Confirmed);
        var curve = TryAccount(acc, BondingCurve.Deserialize);
        if (curve == null)
            return null;
        return new CoinState { Mint = mintKey, Curve = curve, QuoteMint = curve.QuoteMint };
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
