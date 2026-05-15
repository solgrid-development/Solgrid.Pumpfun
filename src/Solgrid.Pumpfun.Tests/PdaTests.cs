using Solgrid.Pumpfun;
using Solgrid.Pumpfun.Accounts;
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

public class AccountsTests
{
    private static byte[] U64(ulong v)
    {
        var b = new byte[8];
        for (int i = 0; i < 8; i++) b[i] = (byte)(v >> (8 * i));
        return b;
    }

    [Fact]
    public void BondingCurve_FullLayout()
    {
        var creator = Enumerable.Repeat((byte)9, 32).ToArray();
        var quote = Enumerable.Repeat((byte)4, 32).ToArray();
        var data = new List<byte>();
        data.AddRange(new byte[8]);            // disc
        data.AddRange(U64(1_000_000));         // vtr
        data.AddRange(U64(2_000_000));         // vqr
        data.AddRange(U64(3_000_000));         // rtr
        data.AddRange(U64(4_000_000));         // rqr
        data.AddRange(U64(5_000_000));         // supply
        data.Add(1);                           // complete
        data.AddRange(creator);
        data.Add(0);                           // mayhem
        data.Add(1);                           // cashback
        data.AddRange(quote);

        var c = BondingCurve.Deserialize(data.ToArray());

        Assert.NotNull(c);
        Assert.Equal(1_000_000UL, c.VirtualTokenReserves);
        Assert.Equal(2_000_000UL, c.VirtualQuoteReserves);
        Assert.True(c.Complete);
        Assert.False(c.IsMayhemMode);
        Assert.True(c.IsCashbackCoin);
        Assert.Equal(new PublicKey(creator).Key, c.Creator.Key);
        Assert.Equal(new PublicKey(quote).Key, c.QuoteMint.Key);
    }

    [Fact]
    public void BondingCurve_ShortData_Null()
    {
        Assert.Null(BondingCurve.Deserialize(new byte[20]));
    }
}
