# Solgrid.Pumpfun

C# SDK for [pump.fun](https://pump.fun/) on Solana. Bonding-curve instructions (v1 and v2),
PumpSwap swap instructions, account types, event decoders, curve and pool
math, plus a client that routes a trade to wherever the coin currently
lives. Built from the official IDLs in pump-fun/pump-public-docs.

[Solnet.Pumpfun](https://github.com/Bifrost-Technologies/Solnet.Pumpfun/) is dead and predates creator fees, usdc-paired coins and
PumpSwap. This is the current program, in C#.

## Built with Solgrid.Pumpfun

Some projects built with this SDK:

- [PumpCopy](https://github.com/harutocodes/pumpfun-copytrade) - copytrade bot
  for SLP tokens on PumpFun and PumpSwap (.NET8, pre-compiled)

![PumpCopy](https://raw.githubusercontent.com/harutocodes/pumpfun-copytrade/main/docs/screen-main.png)

## Trade a coin

```csharp
var rpc = ClientFactory.GetClient("https://your.rpc");
var client = new PumpClient(rpc, Account.FromSecretKey(File.ReadAllText("key.txt").Trim()));

var state = await client.GetCoinStateAsync(mint);
var tokens = await client.QuoteBuyTokensAsync(state, 100_000_000); // 0.1 SOL

var (ok, err, sig) = await client.BuyAsync(mint, 100_000_000, slippageBps: 500, cuPrice: 500_000);
```

`BuyAsync` picks v1 or v2 by the coin's quote mint and routes migrated coins
through PumpSwap. `SellAsync` does the same in reverse and can close your
ATA on a full exit.

## Watch the whole tape

```csharp
var ws = ClientFactory.GetStreamingClient("wss://your.rpc");
var stream = new TradeStream(ws);
stream.Trade += t => Console.WriteLine($"{t.User} {(t.IsBuy ? "bought" : "sold")} {t.Mint}");
stream.Migration += m => Console.WriteLine($"{m.Mint} graduated to pool {m.Pool}");
await stream.StartAsync();
```

## Build your own instructions

All builders are pure: `PumpInstructions.Buy/Sell`, `PumpInstructionsV2.*`,
`AmmInstructions.Buy/Sell`. Account lists follow the current IDL exactly —
including creator vaults, volume accumulators and the fee-program CPI that
the old SDKs omit and that make old transactions fail today.

Math lives in `BondingMath` (curve, 1% protocol fee) and `PoolMath`
(pAMM constant product with lp/protocol/creator fees on effective quote
reserves).

## Notes

- fee recipient and fee bps are read from the on-chain Global account, not
  hardcoded; they rotate
- pool reserves come from the pool vault accounts plus
  `Pool.virtual_quote_reserves` (effective reserves, per the docs)
- keys never leave your process; signing is local, sending goes through
  whatever rpc you passed in

## Known issues

- TradeStream subscribes to the pump program only: you get curve trades and
  migrations, but not PumpSwap fills. The AmmSwapEvent decoder is there, the
  second subscription is not wired yet
- alchemy ws confirmed subscriptions but never delivered notifications for me
  on two different keys; a public wss endpoint worked for the same window.
  test your ws provider before blaming the sdk
