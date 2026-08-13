using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>相位机匣，约 30 帧传送到最近敌附近；断连残影→相位链→重连爆点三段表现</summary>
    internal sealed class PhantomFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //相位紫
        public override Color TintColor => new(180, 80, 255);

        private const int TeleportInterval = 30;
        private readonly System.Collections.Generic.Dictionary<int, int> phantomTimers = [];

        //相位色板
        private static readonly Color PhaseMain = new(200, 100, 255);
        private static readonly Color PhaseEdge = new(100, 40, 200);
        private static readonly Color PhaseGhost = new(150, 70, 230);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += 0.24f;
            ctx.DamageMul += 0.08f;
            ctx.ManaCostMul += 0.36f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer
                || beam.Projectile.numUpdates != -1) return;
            int id = beam.Projectile.whoAmI;
            int timer = phantomTimers.TryGetValue(id, out int value) ? value + 1 : 1;
            if (timer < TeleportInterval) {
                phantomTimers[id] = timer;
                return;
            }
            phantomTimers[id] = 0;
            NPC target = beam.Projectile.Center.FindClosestNPC(300f, false, true);
            if (target == null) return;
            Vector2 from = beam.Projectile.Center;
            Vector2 oldDir = beam.FlightDirection;
            Vector2 dir = (target.Center - from).SafeNormalize(Vector2.UnitX);
            beam.Projectile.Center = target.Center - dir * 80f;
            float speed = beam.Projectile.velocity.Length();
            beam.Projectile.velocity = dir * speed;
            beam.Projectile.netUpdate = true;
            BlinkVisuals(from, beam.Projectile.Center, oldDir, dir);
        }

        /// <summary>断连残影(出发)→虚线相位链→重连爆点(到达)，拥有者端</summary>
        private static void BlinkVisuals(Vector2 from, Vector2 to, Vector2 oldDir, Vector2 newDir) {
            if (Main.netMode == NetmodeID.Server) return;
            bool fromOn = VaultUtils.IsPointOnScreen(from - Main.screenPosition, 200);
            bool toOn = VaultUtils.IsPointOnScreen(to - Main.screenPosition, 200);
            if (!fromOn && !toOn) return;

            float oldAngle = oldDir.ToRotation();
            float linkAngle = (to - from).ToRotation();

            //出发点，断连残影沿旧向速衰+空间闭合内收
            if (fromOn) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_SHPCGlitchShard>(
                        from + oldDir * Main.rand.NextFloat(-14f, 4f) + Main.rand.NextVector2Circular(4f, 4f),
                        oldDir * Main.rand.NextFloat(1f, 2.5f),
                        PhaseGhost, Main.rand.NextFloat(0.7f, 1.1f))
                        .Configure(PhaseEdge, Main.rand.Next(9, 15), oldAngle);
                }
                for (int i = 0; i < 3; i++) {
                    Vector2 spawnPos = from + Main.rand.NextVector2CircularEdge(34f, 34f);
                    PRTLoader.NewParticle<PRT_CyberConverge>(spawnPos, Vector2.Zero,
                        PhaseMain, Main.rand.NextFloat(0.4f, 0.7f))
                        .Configure(from, PhaseEdge, Main.rand.Next(8, 14));
                }
            }

            //相位链，出发→到达虚线碎条，寿命顺程递增读作数据行进
            float dist = Vector2.Distance(from, to);
            int dashes = Math.Clamp((int)(dist / 46f), 3, 8);
            for (int i = 0; i < dashes; i++) {
                float t = (i + 0.5f) / dashes;
                Vector2 pos = Vector2.Lerp(from, to, t) + Main.rand.NextVector2Circular(5f, 5f);
                if (!VaultUtils.IsPointOnScreen(pos - Main.screenPosition, 150)) continue;
                PRTLoader.NewParticle<PRT_SHPCGlitchShard>(pos, Vector2.Zero,
                    PhaseMain, Main.rand.NextFloat(0.45f, 0.75f))
                    .Configure(PhaseEdge, 6 + i * 2, linkAngle);
            }

            //到达点，重连爆发沿新向锥形+横向两粒
            if (toOn) {
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = newDir.RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f)) * Main.rand.NextFloat(2f, 5f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(to, vel,
                        PhaseMain, Main.rand.NextFloat(0.8f, 1.6f)).Configure(PhaseEdge, Main.rand.Next(15, 28));
                }
                Vector2 perp = newDir.RotatedBy(MathHelper.PiOver2);
                for (int s = -1; s <= 1; s += 2) {
                    PRTLoader.NewParticle<PRT_SHPCGlitchShard>(to + perp * s * 6f,
                        perp * s * Main.rand.NextFloat(1.5f, 3f),
                        PhaseGhost, Main.rand.NextFloat(0.6f, 0.9f))
                        .Configure(PhaseEdge, Main.rand.Next(10, 16), linkAngle + MathHelper.PiOver2);
                }
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.22f, Pitch = 0.6f }, to);
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            phantomTimers.Remove(beam.Projectile.whoAmI);
        }
    }
}
