using System.Numerics;

namespace Solgrid.Pumpfun.Math;

// constant product on virtual reserves, fee taken from the input side.
// BigInteger because real reserve products overflow ulong badly.
public static class BondingMath
{
    public const ulong DefaultFeeBps = 100;

    public static ulong QuoteBuyTokens(ulong virtualQuote, ulong virtualToken, ulong quoteIn, ulong feeBps = DefaultFeeBps)
    {
        if (virtualQuote == 0 || virtualToken == 0 || quoteIn == 0)
            return 0;
        ulong eff = quoteIn - CeilFee(quoteIn, feeBps);
        return (ulong)(new BigInteger(virtualToken) * eff / (virtualQuote + eff));
    }

    public static ulong QuoteSellQuote(ulong virtualQuote, ulong virtualToken, ulong tokensIn, ulong feeBps = DefaultFeeBps)
    {
        if (virtualQuote == 0 || virtualToken == 0 || tokensIn == 0)
            return 0;
        ulong eff = tokensIn - CeilFee(tokensIn, feeBps);
        return (ulong)(new BigInteger(virtualQuote) * eff / (virtualToken + eff));
    }

    // program rounds fees up, matching ceilDiv in the official sdk
    public static ulong CeilFee(ulong amount, ulong feeBps)
        => (amount * feeBps + 9999) / 10000;

    public static (ulong VirtualQuote, ulong VirtualToken) AfterBuy(ulong virtualQuote, ulong virtualToken, ulong quoteIn, ulong tokensOut, ulong feeBps = DefaultFeeBps)
    {
        ulong eff = quoteIn - CeilFee(quoteIn, feeBps);
        return (virtualQuote + eff, virtualToken - tokensOut);
    }

    public static (ulong VirtualQuote, ulong VirtualToken) AfterSell(ulong virtualQuote, ulong virtualToken, ulong quoteOut, ulong tokensIn, ulong feeBps = DefaultFeeBps)
    {
        ulong eff = tokensIn - CeilFee(tokensIn, feeBps);
        return (virtualQuote - quoteOut, virtualToken + eff);
    }

    // spot price in quote units per whole token (tokens are 6 dp)
    public static decimal SpotPrice(ulong virtualQuote, ulong virtualToken)
    {
        if (virtualToken == 0) return 0m;
        return virtualQuote / (decimal)virtualToken * 1_000_000m;
    }
}
