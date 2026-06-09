using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 高压核心（星神使）：命中给敌人叠加高压标记，满层引发"过压击穿"——
    /// 向周围敌人爆发独立电弧并在节点引爆，优先连接其它带电目标。
    /// 旧版的"聚束伤害"为死属性，已彻底移除，改为标记 + 链式电弧机制。
    /// </summary>
    internal sealed class HighVoltageCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //高压电蓝
        public override Color TintColor => new(80, 180, 255);

        private const int MarkThreshold = 4;
        private const int MarkTime = 240;
        private const int MaxArcs = 4;
        private const float ArcRange = 560f;
        private const float PrimeRange = 360f;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.06f;
            ctx.ManaCostMul += 0.5f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            Mark(beam.Projectile, target);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            Mark(laser.Projectile, target);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            //能量球引爆视作一次大范围充能脉冲，对范围内敌人各加一层
            float r2 = PrimeRange * PrimeRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, orb.Projectile.Center) > r2) continue;
                if (!npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) continue;
                int s = eff.ApplyHighVoltage(MarkTime, orb.Projectile.owner);
                if (s >= MarkThreshold) {
                    eff.ResetHighVoltage();
                    Burst(orb.Projectile, npc, Math.Max(orb.Projectile.damage / 2, 1));
                }
            }
        }

        private static void Mark(Projectile src, NPC target) {
            if (src.owner != Main.myPlayer) return;
            if (!target.TryGetGlobalNPC(out SHPCNPCEffects eff)) return;
            int stacks = eff.ApplyHighVoltage(MarkTime, src.owner);
            if (stacks >= MarkThreshold) {
                eff.ResetHighVoltage();
                Burst(src, target, Math.Max(src.damage, 1));
            }
        }

        private static void Burst(Projectile src, NPC origin, int baseDmg) {
            if (src.owner != Main.myPlayer) return;
            //中央击穿闪：复用 CyberDetonationProj，90px 半径
            int cIdx = Projectile.NewProjectile(src.GetSource_FromThis(),
                origin.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                baseDmg, 0f, src.owner, ai0: 0.4f);
            if (cIdx >= 0 && cIdx < Main.maxProjectiles) {
                Main.projectile[cIdx].localAI[2] = 90f;
            }

            List<NPC> exclude = new() { origin };
            for (int i = 0; i < MaxArcs; i++) {
                NPC next = origin.Center.FindClosestNPC(ArcRange, false, true, exclude);
                if (next == null) break;
                exclude.Add(next);
                SpawnArc(src, origin.Center, next.Center);
                int aIdx = Projectile.NewProjectile(src.GetSource_FromThis(),
                    next.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    Math.Max((int)(baseDmg * 0.7f), 1), 0f, src.owner, ai0: 0.3f);
                if (aIdx >= 0 && aIdx < Main.maxProjectiles) {
                    Main.projectile[aIdx].localAI[2] = 70f;
                }
                //把电压传导给被击中目标，营造"连锁带电"质感（不立即引爆，避免雪崩）
                if (next.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                    eff.ApplyHighVoltage(MarkTime, src.owner);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.55f, Pitch = 0.3f }, origin.Center);
                for (int i = 0; i < 16; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(origin.Center, vel, new Color(180, 230, 255), Main.rand.NextFloat(0.8f, 1.8f)).Configure(new Color(60, 140, 255), Main.rand.Next(12, 24));
                }
            }
            SHPCNaturalFx.Shake(3f);
        }

        private static void SpawnArc(Projectile src, Vector2 start, Vector2 end) {
            int idx = Projectile.NewProjectile(src.GetSource_FromThis(),
                start, Vector2.Zero,
                ModContent.ProjectileType<SHPCVoltArcProj>(),
                0, 0f, src.owner, ai0: end.X, ai1: end.Y);
            _ = idx;
        }
    }

    /// <summary>
    /// 高压电弧：连接两点的折线闪电，纯视觉装饰，复用 CyberDataArc 着色器，短寿命。
    /// 起点为 Projectile.Center，终点由 ai0/ai1 传入。
    /// </summary>
    internal sealed class SHPCVoltArcProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxLife = 12;
        private static readonly Vector3 CoreVec = new Color(210, 240, 255).ToVector3();
        private static readonly Vector3 GlowVec = new Color(70, 150, 255).ToVector3();

        private Vector2[] points;
        private Trail trail;

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (points != null) return;
            Vector2 end = new(Projectile.ai[0], Projectile.ai[1]);
            Vector2 dir = (end - Projectile.Center);
            float len = dir.Length();
            dir = dir.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            int seg = 8;
            points = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++) {
                float t = i / (float)seg;
                float taper = MathF.Sin(t * MathHelper.Pi);
                float jitter = (i == 0 || i == seg) ? 0f : Main.rand.NextFloat(-1f, 1f) * 18f * taper;
                points[i] = Projectile.Center + dir * (len * t) + perp * jitter;
            }
            if (Main.netMode != NetmodeID.Server) {
                Lighting.AddLight(Projectile.Center, GlowVec * 0.6f);
                Lighting.AddLight(end, GlowVec * 0.6f);
            }
        }

        private float WidthFunction(float progress) {
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            float life = Projectile.timeLeft / (float)MaxLife;
            return taper * 7f * life + 1f;
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (points == null) return;
            Effect shader = EffectLoader.CyberDataArc?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.ThunderTrail?.Value ?? CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trail ??= new Trail(points, WidthFunction, ColorFunction);
            trail.TrailPositions = points;

            float life = Projectile.timeLeft / (float)MaxLife;
            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.08f);
            shader.Parameters["fadeAlpha"]?.SetValue(life);
            shader.Parameters["coreColor"]?.SetValue(CoreVec);
            shader.Parameters["glowColor"]?.SetValue(GlowVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
