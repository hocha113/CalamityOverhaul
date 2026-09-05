using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 烈焰鞭重铸：引导提速 25%
    /// </summary>
    internal class GsFlamelash : GsMorphScheme
    {
        public override int TargetItemID => ItemID.Flamelash;

        protected override string GsDescFallback =>
            "Reforged: guided flight steers 25% faster.\nrelease a serpent of flame whose coils trail your guidance and set foes ablaze";
        protected override float BaseDamageMult => 1.10f;

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            Player owner = Main.player[proj.owner];
            if (proj.owner == Main.myPlayer && owner.channel) {
                //引导提速：原版每帧重置速度基准，恒定放大不会累乘
                proj.velocity *= 1.25f;
            }
        }
    }
}
