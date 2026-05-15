using Solnet.Wallet;

namespace Solgrid.Pumpfun;

public static class Addresses
{
    // bonding curve program, v1 and v2 instructions live here
    public static readonly PublicKey Pump = new("6EF8rrecthR5Dkzon8Nwu78hRvfCKubJ14M5uBEwF6P");

    // pumpswap (pAMM), where migrated coins trade
    public static readonly PublicKey PumpAmm = new("pAMMBay6oceH9fJKBRHGP5D4bD4sWpmSwMn52FMfXEA");

    // pump-fees, cpi'd from both programs for fee config
    public static readonly PublicKey PumpFees = new("pfeeUxB6jkeY1Hxd7CsFCAjcbHA9rWtchMGdZ6VojVZ");
}
