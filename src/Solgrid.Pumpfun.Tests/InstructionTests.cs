using Solgrid.Pumpfun.Instructions;
using Solgrid.Pumpfun.Math;
using Solnet.Wallet;

namespace Solgrid.Pumpfun.Tests;

public class InstructionTests
{
    private static PumpTradeAccounts Accounts() => new(
        Mint: new PublicKey(Enumerable.Repeat((byte)1, 32).ToArray()),
        QuoteMint: new PublicKey(new byte[32]),
        Curve: new PublicKey(Enumerable.Repeat((byte)2, 32).ToArray()),
        Creator: new PublicKey(Enumerable.Repeat((byte)3, 32).ToArray()),
        User: new PublicKey(Enumerable.Repeat((byte)4, 32).ToArray()),
        BaseTokenProgram: Solnet.Programs.TokenProgram.ProgramIdKey,
        QuoteTokenProgram: Solnet.Programs.TokenProgram.ProgramIdKey,
        FeeRecipient: new PublicKey(Enumerable.Repeat((byte)5, 32).ToArray()),
        BuybackFeeRecipient: new PublicKey(Enumerable.Repeat((byte)6, 32).ToArray()));

    [Fact]
    public void BuyV1_DataLayout()
    {
        var ix = PumpInstructions.Buy(Accounts(), 1234, 5678, new Solnet.Wallet.PublicKey(new byte[32]), trackVolume: false);

        Assert.Equal(25, ix.Data.Length);
        Assert.Equal(102, ix.Data[0]);
        Assert.Equal(6, ix.Data[1]);
        Assert.Equal(1234UL, BitConverter.ToUInt64(ix.Data, 8));
        Assert.Equal(5678UL, BitConverter.ToUInt64(ix.Data, 16));
        Assert.Equal(0, ix.Data[24]);
        Assert.Equal(18, ix.Keys.Count);
    }

    [Fact]
    public void SellV1_DataLayout()
    {
        var ix = PumpInstructions.Sell(Accounts(), 999, 111, new Solnet.Wallet.PublicKey(new byte[32]));

        Assert.Equal(24, ix.Data.Length);
        Assert.Equal(51, ix.Data[0]);
        Assert.Equal(999UL, BitConverter.ToUInt64(ix.Data, 8));
        Assert.Equal(16, ix.Keys.Count);
    }

    [Fact]
    public void BuyV2_HasQuoteAccounts_AndGlobalVolume()
    {
        var ix = PumpInstructionsV2.Buy(Accounts(), 10, 20);

        // v2 list is v1 + quote mint side + sharing + accumulators
        Assert.Equal(27, ix.Keys.Count);
        Assert.Equal(184, ix.Data[0]);
    }

    [Fact]
    public void SellV2_NoGlobalVolume()
    {
        var ix = PumpInstructionsV2.Sell(Accounts(), 10, 20);
        Assert.Equal(26, ix.Keys.Count);
        Assert.Equal(93, ix.Data[0]);
    }

    [Fact]
    public void BuyExactQuoteInV2_SameAccountsAsBuy()
    {
        var a = Accounts();
        Assert.Equal(
            PumpInstructionsV2.Buy(a, 1, 2).Keys.Count,
            PumpInstructionsV2.BuyExactQuoteIn(a, 1, 2).Keys.Count);
        Assert.Equal(194, PumpInstructionsV2.BuyExactQuoteIn(a, 1, 2).Data[0]);
    }
}

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
