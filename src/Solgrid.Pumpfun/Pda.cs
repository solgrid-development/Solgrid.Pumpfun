using System.Text;
using Solnet.Programs;
using Solnet.Wallet;

namespace Solgrid.Pumpfun;

public static class Pda
{
    private static PublicKey Find(IEnumerable<byte[]> seeds, PublicKey program)
    {
        PublicKey.TryFindProgramAddress(seeds.ToList(), program, out PublicKey addr, out _);
        return addr;
    }

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    // ---- pump program ----

    public static PublicKey Global() => Find(new[] { Utf8("global") }, Addresses.Pump);

    public static PublicKey BondingCurve(PublicKey mint) =>
        Find(new[] { Utf8("bonding-curve"), mint.KeyBytes }, Addresses.Pump);

    public static PublicKey AssociatedBondingCurve(PublicKey curve, PublicKey tokenProgram, PublicKey mint) =>
        Find(new[] { curve.KeyBytes, tokenProgram.KeyBytes, mint.KeyBytes }, AssociatedTokenAccountProgram.ProgramIdKey);

    public static PublicKey CreatorVault(PublicKey creator) =>
        Find(new[] { Utf8("creator-vault"), creator.KeyBytes }, Addresses.Pump);

    public static PublicKey SharingConfig(PublicKey mint) =>
        Find(new[] { Utf8("sharing-config"), mint.KeyBytes }, Addresses.Pump);

    public static PublicKey GlobalVolumeAccumulator(PublicKey program) =>
        Find(new[] { Utf8("global_volume_accumulator") }, program);

    public static PublicKey UserVolumeAccumulator(PublicKey program, PublicKey user) =>
        Find(new[] { Utf8("user_volume_accumulator"), user.KeyBytes }, program);

    // fee_config seed carries the fee program pubkey as a constant second seed
    public static PublicKey FeeConfig(PublicKey program) =>
        Find(new[] { Utf8("fee_config"), Addresses.PumpFees.KeyBytes }, program);

    public static PublicKey EventAuthority(PublicKey program) =>
        Find(new[] { Utf8("__event_authority") }, program);

    // ---- generic ata, works for spl-token and token-2022 ----

    public static PublicKey Ata(PublicKey owner, PublicKey tokenProgram, PublicKey mint) =>
        Find(new[] { owner.KeyBytes, tokenProgram.KeyBytes, mint.KeyBytes }, AssociatedTokenAccountProgram.ProgramIdKey);

    // ---- pumpswap ----

    public static PublicKey AmmGlobalConfig() =>
        Find(new[] { Utf8("global_config") }, Addresses.PumpAmm);

    public static PublicKey AmmPool(ushort index, PublicKey creator, PublicKey baseMint, PublicKey quoteMint) =>
        Find(new[] { Utf8("pool"), new[] { (byte)(index & 0xff), (byte)(index >> 8) }, creator.KeyBytes, baseMint.KeyBytes, quoteMint.KeyBytes }, Addresses.PumpAmm);

    public static PublicKey AmmPoolLpMint(PublicKey pool) =>
        Find(new[] { Utf8("pool_lp_mint"), pool.KeyBytes }, Addresses.PumpAmm);

    // same seed string as the pump creator vault, but under the amm program
    public static PublicKey AmmCoinCreatorVaultAuthority(PublicKey coinCreator) =>
        Find(new[] { Utf8("creator-vault"), coinCreator.KeyBytes }, Addresses.PumpAmm);
}
