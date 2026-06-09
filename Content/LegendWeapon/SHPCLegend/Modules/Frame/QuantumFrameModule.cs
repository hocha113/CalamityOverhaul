using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>
    /// 量子机匣（神明吞噬者）：命中会在战场留下量子回响节点，节点化作小型炮台向附近敌人折跃射击；
    /// 右键引爆时，爆心附近的节点全部坍缩，向爆心倾泻一轮集中切割。
    /// 节点为独立弹幕、坍缩由 OnOrbDetonation 触发，不改核心追踪逻辑。
    /// </summary>
    internal sealed class QuantumFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //量子超紫
        public override Color TintColor => new(140, 80, 255);

        private const int MaxNodes = 4;
        private const float NodeSpacing = 150f;
        private const float CollapseRange = 620f;

        public override void Apply(ref ShootContext ctx) {
            ctx.HomingMul += 0.2f;
            ctx.OrbSpeedMul += 0.32f;
            ctx.ManaCostMul += 0.2f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if (!Main.rand.NextBool(2)) return;
            int nodeType = ModContent.ProjectileType<SHPCQuantumNodeProj>();
            if (SHPCNaturalFx.CountOwned(beam.Projectile.owner, nodeType) >= MaxNodes) return;
            if (SHPCNaturalFx.HasOwnedNear(beam.Projectile.owner, nodeType, target.Center, NodeSpacing)) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                target.Center, Vector2.Zero, nodeType,
                Math.Max(beam.Projectile.damage / 2, 1), 0f, beam.Projectile.owner);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            int nodeType = ModContent.ProjectileType<SHPCQuantumNodeProj>();
            float r2 = CollapseRange * CollapseRange;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != orb.Projectile.owner || p.type != nodeType) continue;
                if (Vector2.DistanceSquared(p.Center, orb.Projectile.Center) > r2) continue;
                //坍缩：节点朝爆心方向喷出三道集中切割束后湮灭
                Vector2 toCenter = (orb.Projectile.Center - p.Center).SafeNormalize(Vector2.UnitX);
                for (int s = -1; s <= 1; s++) {
                    Vector2 vel = toCenter.RotatedBy(s * 0.22f) * 15f;
                    SHPCNaturalFx.SpawnDerivedBeam(p, p.Center, vel, Math.Max(p.damage, 1), 2.2f, 0.5f);
                }
                if (Main.netMode != NetmodeID.Server) {
                    PRTLoader.NewParticle<PRT_StarPulseRing>(p.Center, Vector2.Zero, new Color(170, 90, 255, 0), 0.05f).Configure(0.05f, 0.32f, 16);
                }
                p.Kill();
            }
        }
    }

    /// <summary>
    /// 量子回响节点：静止的小型炮台，周期性向最近敌人折跃出一道强追踪派生束。
    /// </summary>
    internal sealed class SHPCQuantumNodeProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int Lifetime = 360;
        private const int FireInterval = 34;
        private const float FireRange = 700f;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.rotation += 0.05f;
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.3f, 1f) * 0.6f);

            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % FireInterval == 0 && Projectile.owner == Main.myPlayer) {
                NPC target = Projectile.Center.FindClosestNPC(FireRange, false, true);
                if (target != null) {
                    Vector2 vel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 14f;
                    SHPCNaturalFx.SpawnDerivedBeam(Projectile, Projectile.Center, vel, Math.Max(Projectile.damage, 1), 2f, 0.5f);
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.3f, Pitch = 0.6f }, Projectile.Center);
                    }
                }
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(6)) {
                Vector2 off = Main.rand.NextVector2Circular(16f, 16f);
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + off, Main.rand.NextVector2Circular(1f, 1f), new Color(170, 90, 255), Main.rand.NextFloat(0.4f, 0.9f)).Configure(new Color(90, 30, 200), Main.rand.Next(10, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float life = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f) * MathHelper.Clamp((Lifetime - Projectile.timeLeft) / 12f, 0f, 1f);
            float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.2f);
            Vector2 screen = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, new Color(170, 90, 255, 0) * life * pulse, new Color(60, 20, 140, 0) * life * 0.4f, 0.5f, 0f, 3);
            }
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                spriteBatch.Draw(star, screen, null, new Color(210, 160, 255, 0) * life * pulse, Projectile.rotation, star.Size() * 0.5f, 0.16f, SpriteEffects.None, 0f);
            }
        }
    }
}
