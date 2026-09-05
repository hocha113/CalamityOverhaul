using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
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
    /// 过载血涌反噬 -10HP 进锁，贪吸有价
    /// </summary>
    internal class GsSoulDrain : GsHeatScheme
    {
        public override int TargetItemID => ItemID.SoulDrain;

        protected override string GsDescFallback =>
            "Reforged: a leech conduit that tethers up to three foes and drains them together; white heat grants true life recovery\noverload backfires for 10 life";
        internal override float HeatPerShot => 0f;
        internal override float CoolRatePerTick => 1.0f;

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

        internal override void OnOverload(Player player, GsHeatPlayer hp) {
            base.OnOverload(player, hp);
            //血涌反噬：owner 端结算自伤（玩家生命客户端权威）
            player.Hurt(PlayerDeathReason.ByCustomReason(bloodPriceDeath.Format(player.name)), 10, 0);
        }
    }
}
