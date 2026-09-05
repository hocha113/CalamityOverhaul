using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 水晶蛇重铸：爆裂碎晶多一次弹跳（承签穿透 +1）
    /// </summary>
    internal class GsCrystalSerpent : GsMorphScheme
    {
        public override int TargetItemID => ItemID.CrystalSerpent;

        protected override string GsDescFallback =>
            "Reforged: burst shards bounce once more.\nrelease a great crystal python that slithers in a sine wave and bursts into a richer shard shower";
        protected override float BaseDamageMult => 1.08f;

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            if (proj.type != ProjectileID.CrystalPulse2) {
                return;
            }
            //碎晶多一次弹跳（弹跳消耗 penetrate，守卫防 -1 无限穿被写坏）
            if (proj.penetrate > 0) {
                proj.penetrate++;
            }
        }
    }
}
