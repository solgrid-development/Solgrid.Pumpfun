using Solgrid.Pumpfun.Accounts;
using Solgrid.Pumpfun.Instructions;
using Solgrid.Pumpfun.Math;
using Solnet.Wallet;

namespace Solgrid.Pumpfun.Tests;

public class PoolMathTests
{
    private const ulong BaseR = 500_000_000_000;
    private const ulong QuoteR = 60_000_000_000;
    private const ulong Lp = 20;
    private const ulong Proto = 5;
    private const ulong Creator = 0;

    [Fact]
    public void BuyQuoteIn_IncludesFees()
    {
        var baseOut = 1_000_000_000UL;
        var total = PoolMath.BuyQuoteIn(BaseR, QuoteR, baseOut, Lp, Proto, Creator);

        var num = (System.Numerics.BigInteger)QuoteR * baseOut;
        var cp = (ulong)((num + (BaseR - baseOut) - 1) / (BaseR - baseOut));
        Assert.True(total > cp);
        Assert.Equal(cp + cp * Lp / 10000 + cp * Proto / 10000, total);
    }

    [Fact]
    public void BuyBaseOut_StripsFees()
    {
        var quoteIn = PoolMath.BuyQuoteIn(BaseR, QuoteR, 1_000_000_000, Lp, Proto, Creator);
        var baseOut = PoolMath.BuyBaseOut(BaseR, QuoteR, quoteIn, Lp, Proto, Creator);

        // rounding loses a few units, not percent
        Assert.InRange(baseOut, 999_000_000UL, 1_000_000_000UL);
    }

    [Fact]
    public void SellRoundTrip_LosesFees()
    {
        var baseIn = 1_000_000_000UL;
        var quoteOut = PoolMath.SellQuoteOut(BaseR, QuoteR, baseIn, Lp, Proto, Creator);
        var baseBack = PoolMath.SellBaseIn(BaseR, QuoteR, quoteOut, Lp, Proto, Creator);

        Assert.True(quoteOut > 0);
        Assert.InRange(baseBack, baseIn - baseIn / 100, baseIn);
    }
}

public class AmmInstructionTests
{
    private static AmmSwapAccounts Accounts() => new(
        Pool: new AmmPool
        {
            Index = 0,
            Creator = new PublicKey(Enumerable.Repeat((byte)1, 32).ToArray()),
            BaseMint = new PublicKey(Enumerable.Repeat((byte)2, 32).ToArray()),
            QuoteMint = new PublicKey(new byte[32]),
            CoinCreator = new PublicKey(Enumerable.Repeat((byte)3, 32).ToArray()),
            PoolBaseTokenAccount = new PublicKey(Enumerable.Repeat((byte)4, 32).ToArray()),
            PoolQuoteTokenAccount = new PublicKey(Enumerable.Repeat((byte)5, 32).ToArray()),
        },
        User: new PublicKey(Enumerable.Repeat((byte)6, 32).ToArray()),
        GlobalConfig: Pda.AmmGlobalConfig(),
        ProtocolFeeRecipient: new PublicKey(Enumerable.Repeat((byte)7, 32).ToArray()),
        BaseTokenProgram: Solnet.Programs.TokenProgram.ProgramIdKey,
        QuoteTokenProgram: Solnet.Programs.TokenProgram.ProgramIdKey);

    [Fact]
    public void Buy_HasVolumeAccounts()
    {
        var ix = AmmInstructions.Buy(Accounts(), 100, 200);
        Assert.Equal(23, ix.Keys.Count);
        Assert.Equal(102, ix.Data[0]);
        Assert.Equal(25, ix.Data.Length);
    }

    [Fact]
    public void Sell_NoVolumeAccounts()
    {
        var ix = AmmInstructions.Sell(Accounts(), 100, 50);
        Assert.Equal(21, ix.Keys.Count);
        Assert.Equal(51, ix.Data[0]);
    }
}
