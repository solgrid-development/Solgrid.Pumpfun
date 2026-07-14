using Solgrid.Pumpfun.Events;
using Solnet.Wallet;

namespace Solgrid.Pumpfun.Tests;

public class EventTests
{
    private static byte[] U64(ulong v)
    {
        var b = new byte[8];
        for (int i = 0; i < 8; i++) b[i] = (byte)(v >> (8 * i));
        return b;
    }

    private static byte[] LegacyTrade(bool isBuy)
    {
        var data = new List<byte>();
        data.AddRange(TradeEvent.Disc);
        data.AddRange(new byte[32]);              // mint
        data.AddRange(U64(1_000_000_000));        // sol
        data.AddRange(U64(2_000_000));            // tokens
        data.Add((byte)(isBuy ? 1 : 0));
        data.AddRange(Enumerable.Repeat((byte)9, 32)); // user
        data.AddRange(U64(1700000000));           // ts
        data.AddRange(U64(30_000_000_000));       // vsol
        data.AddRange(U64(1_073_000_000_000_000)); // vtok
        return data.ToArray();
    }

    [Fact]
    public void LegacyTrade_Parses()
    {
        var e = TradeEvent.TryParse(LegacyTrade(true));

        Assert.NotNull(e);
        Assert.True(e.IsBuy);
        Assert.Equal(1_000_000_000UL, e.SolAmount);
        Assert.Equal(2_000_000UL, e.TokenAmount);
        Assert.Equal("", e.IxName);
        Assert.Equal(0UL, e.CreatorFee);
    }

    [Fact]
    public void ModernTrade_ParsesExtendedFields()
    {
        var data = new List<byte>(LegacyTrade(false));
        data.AddRange(U64(11));                    // real sol
        data.AddRange(U64(22));                    // real tok
        data.AddRange(Enumerable.Repeat((byte)5, 32)); // fee recipient
        data.AddRange(U64(100));                   // fee bps
        data.AddRange(U64(33));                    // fee
        data.AddRange(Enumerable.Repeat((byte)6, 32)); // creator
        data.AddRange(U64(50));                    // creator bps
        data.AddRange(U64(44));                    // creator fee
        data.Add(1);                               // track volume
        data.AddRange(U64(0));                     // unclaimed
        data.AddRange(U64(0));                     // claimed
        data.AddRange(U64(55));                    // current sol volume
        data.AddRange(U64(0));                     // last update ts
        var name = "buy_v2";
        data.AddRange(BitConverter.GetBytes((uint)name.Length));
        data.AddRange(System.Text.Encoding.UTF8.GetBytes(name));
        data.Add(0);                               // mayhem
        data.AddRange(U64(0));                     // cashback bps
        data.AddRange(U64(0));                     // cashback
        data.AddRange(U64(0));                     // buyback bps
        data.AddRange(U64(0));                     // buyback
        data.AddRange(BitConverter.GetBytes((uint)0)); // shareholders vec
        data.AddRange(Enumerable.Repeat((byte)7, 32)); // quote mint
        data.AddRange(U64(66));                    // quote amount
        data.AddRange(U64(0));                     // vqr
        data.AddRange(U64(0));                     // rqr

        var e = TradeEvent.TryParse(data.ToArray());

        Assert.NotNull(e);
        Assert.False(e.IsBuy);
        Assert.Equal("buy_v2", e.IxName);
        Assert.Equal(44UL, e.CreatorFee);
        Assert.True(e.TrackVolume);
        Assert.Equal(66UL, e.QuoteAmount);
        Assert.Equal(new PublicKey(Enumerable.Repeat((byte)7, 32).ToArray()).Key, e.QuoteMint.Key);
    }

    [Fact]
    public void Migration_Parses()
    {
        var data = new List<byte>();
        data.AddRange(MigrationEvent.Disc);
        data.AddRange(Enumerable.Repeat((byte)1, 32)); // user
        data.AddRange(Enumerable.Repeat((byte)2, 32)); // mint
        data.AddRange(U64(793_100_000_000_000));        // mint amount
        data.AddRange(U64(85_000_000_000));             // sol
        data.AddRange(U64(10_000_000));                 // migration fee
        data.AddRange(Enumerable.Repeat((byte)3, 32)); // curve
        data.AddRange(U64(1700000000));                 // ts
        data.AddRange(Enumerable.Repeat((byte)4, 32)); // pool
        data.AddRange(new byte[32]);                    // quote mint

        var e = MigrationEvent.TryParse(data.ToArray());

        Assert.NotNull(e);
        Assert.Equal(new PublicKey(Enumerable.Repeat((byte)4, 32).ToArray()).Key, e.Pool.Key);
        Assert.Equal(85_000_000_000UL, e.SolAmount);
    }

    [Fact]
    public void Decoder_RoutesByDisc()
    {
        Assert.IsType<TradeEvent>(EventDecoder.Decode(LegacyTrade(true)));
        var mig = new List<byte> { };
        mig.AddRange(MigrationEvent.Disc);
        mig.AddRange(new byte[200]);
        Assert.IsType<MigrationEvent>(EventDecoder.Decode(mig.ToArray()));
        Assert.Null(EventDecoder.Decode(new byte[200]));
    }

    [Fact]
    public void Decoder_LogLine()
    {
        var line = "Program data: " + Convert.ToBase64String(LegacyTrade(true));
        Assert.NotNull(EventDecoder.DecodeLogLine(line));
        Assert.Null(EventDecoder.DecodeLogLine("Program log: Instruction: Buy"));
    }

    [Fact]
    public void AmmSwapEvent_ParsesBuy()
    {
        var data = new List<byte>();
        data.AddRange(AmmSwapEvent.BuyDisc);
        data.AddRange(U64(1700000000));          // ts
        data.AddRange(U64(5_000_000));           // base out
        data.AddRange(U64(999));                 // max quote in
        for (int i = 0; i < 4; i++) data.AddRange(U64(1));   // reserves
        data.AddRange(U64(120_000_000));         // quote in
        for (int i = 0; i < 6; i++) data.AddRange(U64(2));   // fee fields
        data.AddRange(Enumerable.Repeat((byte)8, 32));      // pool
        data.AddRange(Enumerable.Repeat((byte)9, 32));      // user
        data.AddRange(new byte[64]);             // tail

        var e = AmmSwapEvent.TryParse(data.ToArray());

        Assert.NotNull(e);
        Assert.True(e.IsBuy);
        Assert.Equal(5_000_000UL, e.BaseAmount);
        Assert.Equal(120_000_000UL, e.QuoteAmount);
        Assert.Equal(new PublicKey(Enumerable.Repeat((byte)9, 32).ToArray()).Key, e.User.Key);
        Assert.IsType<AmmSwapEvent>(EventDecoder.Decode(data.ToArray()));
    }
}
