using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 生命吸取重铸：全接管为「水蛭通道」。锁定至多 3 个视线内目标拉血丝持续抽血；
    /// 热量即血压，白热带附 +2HP/s 真实回复（原版只加再生）；
    /// 右键泄压「血爆」在光标处引爆积存血量；过载血涌反噬 -10HP 进锁，贪吸有价
    /// </summary>
    internal class GsSoulDrain : GsHeatScheme
    {
        public override int TargetItemID => ItemID.SoulDrain;

        protected override string GsDescFallback =>
            "Reforged: a leech conduit that tethers up to three foes and drains them together; white heat grants true life recovery\nRight click to detonate the stored blood at your cursor; overload backfires for 10 life";

        internal override float HeatPerShot => 0f;
        internal override float CoolRatePerTick => 1.0f;
        internal override Color MuzzleTheme => GsConduitVFX.BloodMain;

        /// <summary>过载反噬的死亡回执文案</summary>
        private LocalizedText bloodPriceDeath;

        public override void GsSetStaticDefaults()
            => bloodPriceDeath = this.GetLocalization("BloodPriceDeath", () => "{0} was drained by their own leech conduit");

        public override bool? GsCanUseItem(Item item, Player player) {
            if (base.GsCanUseItem(item, player) == false) {
                return false;
            }
            if (HeldAlive<GsDrainTetherProj>(player)) {
                return false;
            }
            return null;
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.whoAmI == Main.myPlayer && !HeldAlive<GsDrainTetherProj>(player)) {
                Projectile.NewProjectile(source, player.MountedCenter, GsAimUnit(player),
                    ModContent.ProjectileType<GsDrainTetherProj>(), damage, knockback, player.whoAmI);
            }
            return false;
        }

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            //血爆：光标处（限通道射程内）引爆积存血压，热满时 ×3 封顶
            Vector2 at = Main.MouseWorld;
            Vector2 offset = at - player.MountedCenter;
            if (offset.Length() > GsDrainTetherProj.DrainRadius) {
                at = player.MountedCenter + offset.SafeNormalize(Vector2.UnitX) * GsDrainTetherProj.DrainRadius;
            }
            float power = 1.0f + 2.0f * hp.Heat / GsHeatPlayer.HeatMax;
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * power));
            Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"), at, Vector2.Zero,
                ModContent.ProjectileType<GsConduitNovaProj>(), damage, 5f, player.whoAmI,
                150f + 2 * 1024f, 0f, 0f);
        }

        internal override void OnOverload(Player player, GsHeatPlayer hp) {
            base.OnOverload(player, hp);
            //血涌反噬：owner 端结算自伤（玩家生命客户端权威）
            player.Hurt(PlayerDeathReason.ByCustomReason(bloodPriceDeath.Format(player.name)), 10, 0);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 9; i++) {
                    PRTLoader.NewParticle<PRT_CrimsonBloodStain>(player.MountedCenter + Main.rand.NextVector2Circular(14f, 18f),
                        Main.rand.NextVector2Circular(2.5f, 2f), GsConduitVFX.BloodDeep, Main.rand.NextFloat(0.5f, 0.9f));
                }
            }
        }
    }
}
