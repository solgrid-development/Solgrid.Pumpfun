using System.Numerics;

namespace Solgrid.Pumpfun.Math;

// pumpswap (pAMM) constant product on vault balances.
// fees are charged on top of the cp quote for buys and deducted for sells:
//   buy:  user pays cp + lp + protocol + coin creator
//   sell: user gets cp - lp - protocol - coin creator
// quote side uses effective reserves = vault balance + Pool.virtual_quote_reserves
public static class PoolMath
{
    public static ulong EffectiveQuoteReserves(ulong vaultBalance, long virtualQuoteReserves)
    {
        var eff = (BigInteger)vaultBalance + virtualQuoteReserves;
        return eff < 0 ? 0 : (ulong)eff;
    }

    // base out wanted -> total quote the user must pay
    public static ulong BuyQuoteIn(ulong baseR, ulong quoteR, ulong baseOut, ulong lpBps, ulong protocolBps, ulong creatorBps)
    {
        if (baseOut == 0 || baseOut >= baseR)
            return 0;
        var cp = CpQuoteRoundUp(quoteR, baseR, baseOut);
        return cp + cp * lpBps / 10000 + cp * protocolBps / 10000 + cp * creatorBps / 10000;
    }

    // quote in offered -> base the user receives
    public static ulong BuyBaseOut(ulong baseR, ulong quoteR, ulong quoteIn, ulong lpBps, ulong protocolBps, ulong creatorBps)
    {
        var cp = StripFees(quoteIn, lpBps, protocolBps, creatorBps);
        if (cp == 0)
            return 0;
        return (ulong)(new BigInteger(baseR) * cp / (quoteR + cp));
    }

    // base in sold -> quote the user receives
    public static ulong SellQuoteOut(ulong baseR, ulong quoteR, ulong baseIn, ulong lpBps, ulong protocolBps, ulong creatorBps)
    {
        if (baseIn == 0)
            return 0;
        var cp = (ulong)(new BigInteger(quoteR) * baseIn / (baseR + baseIn));
        var fees = cp * lpBps / 10000 + cp * protocolBps / 10000 + cp * creatorBps / 10000;
        return cp > fees ? cp - fees : 0;
    }

    // quote out wanted -> base the user must sell
    public static ulong SellBaseIn(ulong baseR, ulong quoteR, ulong quoteOut, ulong lpBps, ulong protocolBps, ulong creatorBps)
    {
        var cp = AddFees(quoteOut, lpBps, protocolBps, creatorBps);
        if (cp == 0 || cp >= quoteR)
            return 0;
        // base_in = baseR * cp / (quoteR - cp), round up
        var num = new BigInteger(baseR) * cp;
        var den = (ulong)(quoteR - cp);
        return (ulong)((num + den - 1) / den);
    }

    private static ulong CpQuoteRoundUp(ulong quoteR, ulong baseR, ulong baseOut)
    {
        var num = new BigInteger(quoteR) * baseOut;
        var den = (ulong)(baseR - baseOut);
        return (ulong)((num + den - 1) / den);
    }

    // total -> cp part, fees proportional to cp
    private static ulong StripFees(ulong total, ulong lpBps, ulong protocolBps, ulong creatorBps)
    {
        ulong denom = 10000 + lpBps + protocolBps + creatorBps;
        return (ulong)(new BigInteger(total) * 10000 / denom);
    }

    private static ulong AddFees(ulong cp, ulong lpBps, ulong protocolBps, ulong creatorBps)
    {
        return cp + cp * lpBps / 10000 + cp * protocolBps / 10000 + cp * creatorBps / 10000;
    }
}
