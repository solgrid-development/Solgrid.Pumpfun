using Solgrid.Pumpfun;
using Solnet.Wallet;

namespace Solgrid.Pumpfun.Tests;

public class PdaTests
{
    [Fact]
    public void Global_MatchesKnownAddress()
    {
        Assert.Equal("4wTV1YmiEkRvAtNtsSGPtUrqRYQMe5SKy2uB4Jjaxnjf", Pda.Global().Key);
    }

    [Fact]
    public void EventAuthority_MatchesKnownAddress()
    {
        Assert.Equal("Ce6TQqeHC9p8KetsN6JsjHK7UTZk7nasjjnr7XxXp9F1", Pda.EventAuthority(Addresses.Pump).Key);
    }

    [Fact]
    public void BondingCurve_DerivationStable()
    {
        var mint = new PublicKey("A5ns9wBeGnaQHm4mRGbDmCLd3aBdYfW2Sy3YU8Ghpump");
        var a = Pda.BondingCurve(mint);
        var b = Pda.BondingCurve(mint);
        Assert.Equal(a.Key, b.Key);
        Assert.NotEqual(mint.Key, a.Key);
    }

    [Fact]
    public void CreatorVault_DiffersPerCreator()
    {
        var c1 = new PublicKey(new byte[32]);
        var c2 = new PublicKey(Enumerable.Repeat((byte)1, 32).ToArray());
        Assert.NotEqual(Pda.CreatorVault(c1).Key, Pda.CreatorVault(c2).Key);
    }
}
