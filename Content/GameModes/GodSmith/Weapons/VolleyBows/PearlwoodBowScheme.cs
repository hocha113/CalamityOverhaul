using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 珍珠木弓（公认垫底位，整体重铸 135%）：每 5 发攒满，第 6 发成
    /// 彩虹微光五箭横列齐射，齐射箭曳珍珠虹尾。入门位不设标记与处决。
    /// 期望：+4×0.5/6 ≈ +33%
    /// </summary>
    internal class GsPearlwoodBow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.PearlwoodBow;

        protected override string GsDescFallback =>
            "Reforged: every 5 shots charge a 5-arrow pearlescent line volley, one ammo per volley\nRight click releases the volley early at 60%+ charge";

        protected override int VolleyCount => 5;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Line;
        protected override float SpreadPx => 14f;
        protected override float ChargePerShot => 20f;
        protected override float SideArrowMul => 0.5f;
        protected override int MarksPerVolleyHit => 0;
        protected override int PursuitEvery => 0;
        protected override Color TrailColor => RainbowNow(0f);

        /// <summary>珍珠虹相位色（确定性时间输入，绘制路径零随机）</summary>
        internal static Color RainbowNow(float shift) {
            float hue = (Main.GlobalTimeWrappedHourly * 0.35f + shift) % 1f;
            return Main.hslToRgb(hue, 0.75f, 0.72f);
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide) {
                //珍珠虹重影：相位随编队索引错开
                DrawSpeedGhost(proj, RainbowNow(router.MarkData2 * 0.13f + proj.identity * 0.021f), 0.4f);
                return null;
            }
            return base.GsProjPreDraw(proj, ref lightColor, router);
        }
    }
}
