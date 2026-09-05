using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 玉米糖来福枪重铸：糖衣炮弹。<br/>
    /// [糖雨]：原版速射加一点糖粒坠感的微弧
    /// </summary>
    internal class GsCandyCornRifle : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.CandyCornRifle;

        public override string GsFamily => "Guns";

        protected override string GsDescFallback =>
            "Reforged: the classic spray now droops on a sweet arc";
        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeCandyRain", EnName = "Candy Rain",
            },
        ];

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //糖粒微坠弧（各端确定性）
            proj.velocity.Y += 0.028f;
        }
    }
}
