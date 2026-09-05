using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 涡流射手重铸：经典不毁，速射加周期火箭的招牌原样可辨。<br/>
    /// [涡流扫射]：原版节奏，周期火箭获得微追踪
    /// </summary>
    internal class GsVortexBeater : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.VortexBeater;

        public override string GsFamily => "Guns";

        protected override string GsDescFallback =>
            "Reforged: keeps the classic storm, rockets gently home in";
        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeSweep", EnName = "Vortex Sweep",
            },
        ];

        //涡流后坐：原版级轻挫
        protected override float RecoilShift => 3.2f;
        protected override float RecoilKick => 0.05f;

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //周期火箭：微追踪（各端同源找最近目标，漂移由弹幕同步纠正）
            if (proj.type != ProjectileID.VortexBeaterRocket) {
                return;
            }
            NPC target = FindRocketTarget(proj);
            if (target != null) {
                float speed = Math.Max(6f, proj.velocity.Length());
                Vector2 want = (target.Center - proj.Center).SafeNormalize(Vector2.UnitX) * speed;
                proj.velocity = Vector2.Lerp(proj.velocity, want, 0.045f);
            }
        }

        private static NPC FindRocketTarget(Projectile proj) {
            NPC best = null;
            float bestDist = 340f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(proj)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, proj.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }
    }
}
