using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 魔法导弹重铸：保留原版按住引导并提速 25%
    /// </summary>
    internal class GsMagicMissile : GsMorphScheme
    {
        public override int TargetItemID => ItemID.MagicMissile;

        protected override string GsDescFallback =>
            "Reforged: guided flight steers 25% faster.\nrelease a triple volley that escorts your cursor in formation";
        protected override float BaseDamageMult => 1.10f;

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            Player owner = Main.player[proj.owner];
            //引导修正只在 owner 端进行（目标点读的是本地鼠标），远端靠原版 netUpdate 同步速度
            if (proj.owner == Main.myPlayer && owner.channel && proj.velocity.Length() > 0.01f) {
                //原版每帧重置速度基准，这里恒定放大 25% 不会逐帧累乘
                proj.velocity *= 1.25f;
            }
        }
    }
}
