using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>熵核，球飞行吸熵蓄能，引爆按累积释余波</summary>
    internal sealed class EntropyCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //熵核暗紫
        public override Color TintColor => new(170, 50, 220);

        private const float ScanRange = 380f;
        private const float MaxEntropy = 5f;
        private readonly Dictionary<int, float> _entropy = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbSpeedMul += -0.24f;
            ctx.ManaCostMul += 0.6f;
        }

        public override void OnOrbFlyingAI(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            //每 5 帧扫描一次
            int id = orb.Projectile.whoAmI;
            int frame = (int)Main.GameUpdateCount + id;
            if (frame % 5 != 0) return;
            float gain = 0f;
            float rangeSq = ScanRange * ScanRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, orb.Projectile.Center) > rangeSq) continue;
                gain += 0.06f;
                if (gain > 0.6f) break;
            }
            if (gain <= 0f) return;
            if (!_entropy.TryGetValue(id, out float e)) e = 0f;
            int prevStage = (int)e;
            e = MathF.Min(e + gain, MaxEntropy);
            _entropy[id] = e;

            if (Main.netMode == Terraria.ID.NetmodeID.Server) return;
            float ratio = e / MaxEntropy;
            Color wispMain = Color.Lerp(new Color(150, 60, 220), new Color(215, 150, 255), ratio);
            Color wispEdge = Color.Lerp(new Color(80, 20, 160), new Color(140, 70, 235), ratio);

            //熵丝向心被球吞入，锚点按球速前瞻，密度随累积
            Vector2 anchor = orb.Projectile.Center + orb.Projectile.velocity * 10f;
            int wisps = 1 + (int)(ratio * 2f) + (gain > 0.3f ? 1 : 0);
            for (int i = 0; i < wisps; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawnPos = anchor + ang.ToRotationVector2() * Main.rand.NextFloat(90f, 170f);
                PRTLoader.NewParticle<PRT_CyberConverge>(spawnPos, Vector2.Zero, wispMain,
                    Main.rand.NextFloat(0.4f, 0.8f + ratio * 0.4f))
                    .Configure(anchor, wispEdge, Main.rand.Next(16, 28), ratio);
            }

            //球周切向熵痕，色随累积加深增亮
            Vector2 orbitDir = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
            PRTLoader.NewParticle<PRT_CyberSquare>(
                orb.Projectile.Center + orbitDir * Main.rand.NextFloat(24f, 40f),
                orbitDir.RotatedBy(MathHelper.PiOver2) * (1.4f + ratio * 1.6f),
                wispMain, Main.rand.NextFloat(0.5f, 0.9f + ratio * 0.6f))
                .Configure(wispEdge, Main.rand.Next(14, 24));

            //跨整熵阶，细环+升调确认
            if ((int)e > prevStage) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(orb.Projectile.Center, Vector2.Zero,
                    wispMain, 0.04f).Configure(0.04f, 0.24f + ratio * 0.2f, 14);
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.3f, Pitch = -0.4f + ratio * 0.8f }, orb.Projectile.Center);
            }
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            int id = orb.Projectile.whoAmI;
            if (!_entropy.TryGetValue(id, out float e) || e <= 0.4f) return;

            //余波，伤与半径按熵比例
            if (orb.Projectile.owner == Main.myPlayer) {
                float ratio = MathHelper.Clamp(e / MaxEntropy, 0f, 1f);
                int dmg = Math.Max((int)(orb.Projectile.damage * (0.4f + ratio * 0.6f)), 1);
                //余波半径 120~640px，ai2 走生成包同步
                Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                    orb.Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    dmg, 0f, orb.Projectile.owner, ai0: 0.4f + ratio * 0.4f,
                    ai1: 0f, ai2: MathHelper.Lerp(120f, 640f, ratio));
            }
        }

        public override void OnOrbKill(CyberChargeOrbProj orb, int timeLeft) {
            _entropy.Remove(orb.Projectile.whoAmI);
        }
    }
}
