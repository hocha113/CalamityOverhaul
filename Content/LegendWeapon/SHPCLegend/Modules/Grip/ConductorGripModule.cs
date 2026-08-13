using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>导体握把，+链跳/范围，命中过载电弧</summary>
    internal sealed class ConductorGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //导体电蓝
        public override Color TintColor => new(80, 200, 255);

        //沿用既有命中色板
        private static readonly Color ArcBright = new(180, 240, 255);
        private static readonly Color ArcEdge = new(80, 180, 255);

        /// <summary>弧闪节流帧距，同帧多束齐中只放一簇</summary>
        private const int ArcIcdFrames = 4;
        private uint lastArcTick = uint.MaxValue;

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamChainCount += 1;
            ctx.BeamChainRange = MathF.Max(ctx.BeamChainRange, 240f) + 60f;
            ctx.DamageMul += 0.04f;
            ctx.ManaCostMul += 0.48f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            //方屑作二级碎电介质，弧才是本体
            for (int i = 0; i < 3; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center, vel, ArcBright,
                    Main.rand.NextFloat(0.9f, 1.8f))?.Configure(ArcEdge, Main.rand.Next(8, 16));
            }
            if (Main.GameUpdateCount - lastArcTick < ArcIcdFrames) return;
            //屏外不放弧，PRT_TeslaArc全局仅24枚，防挤占在屏电弧
            if (!VaultUtils.IsPointOnScreen(target.Center - Main.screenPosition, 200)) return;
            lastArcTick = Main.GameUpdateCount;
            SpawnDischargeArcs(target);
        }

        /// <summary>命中导体炸开触须电弧，不等长不对称，快起快灭</summary>
        private static void SpawnDischargeArcs(NPC target) {
            int arcCount = Main.rand.Next(2, 4);
            for (int a = 0; a < arcCount; a++) {
                Vector2 dir = Main.rand.NextVector2Unit();
                float len = Main.rand.NextFloat(56f, 110f) + MathF.Min(target.width, 90f) * 0.2f;
                Vector2 side = dir.RotatedBy(MathHelper.PiOver2);
                Vector2 from = target.Center + dir * Main.rand.NextFloat(0f, 10f);
                const int pointCount = 4;
                Vector2[] path = new Vector2[pointCount];
                for (int i = 0; i < pointCount; i++) {
                    float t = i / (float)(pointCount - 1);
                    //两端钉死，中段侧摆
                    float sway = MathF.Sin(t * MathHelper.Pi) * Main.rand.NextFloat(-14f, 14f);
                    path[i] = from + dir * (len * t) + side * sway;
                }
                PRTLoader.NewParticle<PRT_TeslaArc>(path[pointCount / 2], Vector2.Zero, ArcBright, 1f)
                    ?.Configure(path, Main.rand.Next(9, 14), Main.rand.NextFloat(5f, 8f), (0f, 7f), 4f);
            }
            //一瞬过曝闪心，加色批全alpha
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero,
                new Color(200, 240, 255), 0.04f)?.Configure(0.04f, 0.26f, 10);
        }
    }
}
