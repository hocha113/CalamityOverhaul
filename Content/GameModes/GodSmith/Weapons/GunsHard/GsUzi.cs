using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 乌兹重铸：泼洒与点杀两态，高速弹身份两档全保留。<br/>
    /// [双持乱射]：+30% 射速、附加散布，弹壳左右交替抛出的双持演出。<br/>
    /// [高速点射]：3 连完全收束零散布，链间强制间歇；点射走真实 use，每发照常耗弹
    /// </summary>
    internal class GsUzi : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Uzi;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch grip\n" +
            "Akimbo sprays 30% faster with a loose pattern; Burst fires tight three round strings dead on target\n" +
            "High velocity rounds stay in both grips";

        /// <summary>抛壳左右交替记号；只在 owner 射击链读写</summary>
        private int casingSide = 1;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeAkimbo", EnName = "Akimbo",
                UseSpeed = 1.30f, DamageMul = 0.87f,
                ExtraSpread = MathHelper.ToRadians(6f),
            },
            new GsFireMode {
                Key = "ModeBurst", EnName = "Burst",
                UseSpeed = 1.15f, DamageMul = 1.35f, Converge = 1f,
                BurstCount = 3, BurstRest = 18,
            },
        ];

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (!VaultUtils.isServer) {
                //双持演出：弹壳左右交替甩出（乱射档），点射档规整右抛
                casingSide = mp.ModeIndex == 0 ? -casingSide : 1;
                Vector2 unit = velocity.SafeNormalize(Vector2.UnitX * player.direction);
                Vector2 side = unit.RotatedBy(MathHelper.PiOver2 * casingSide);
                PRTLoader.NewParticle<PRT_ProcChip>(position + unit * 8f,
                    side * Main.rand.NextFloat(1.5f, 2.5f) - Vector2.UnitY * Main.rand.NextFloat(1.5f, 3f),
                    new Color(206, 170, 96), Main.rand.NextFloat(0.4f, 0.55f))
                    ?.Configure(new Color(255, 226, 142), Main.rand.Next(18, 28));
            }
            return null;
        }

        internal override void GsGunHeldReset(Player player) => casingSide = 1;
    }
}
