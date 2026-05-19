using Solgrid.Pumpfun.Math;

namespace Solgrid.Pumpfun.Tests;

public class BondingMathTests
{
    private const ulong VQuote = 30_000_000_000;
    private const ulong VToken = 1_073_000_000_000_000;

    [Fact]
    public void FreshCurve_OneSol()
    {
        var tokens = BondingMath.QuoteBuyTokens(VQuote, VToken, 1_000_000_000);
        Assert.InRange(tokens, 34_000_000_000_000UL, 34_600_000_000_000UL);
    }

    [Fact]
    public void RoundTrip_LosesFees()
    {
        var tokens = BondingMath.QuoteBuyTokens(VQuote, VToken, 1_000_000_000);
        var (q2, t2) = BondingMath.AfterBuy(VQuote, VToken, 1_000_000_000, tokens);
        var back = BondingMath.QuoteSellQuote(q2, t2, tokens);
        Assert.True(back < 1_000_000_000);
        Assert.True(back > 970_000_000);
    }

    [Fact]
    public void CustomFeeBps_ScalesFee()
    {
        var defaultFee = BondingMath.QuoteBuyTokens(VQuote, VToken, 1_000_000_000, 100);
        var doubleFee = BondingMath.QuoteBuyTokens(VQuote, VToken, 1_000_000_000, 200);
        Assert.True(doubleFee < defaultFee);
    }
}
