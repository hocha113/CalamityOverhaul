using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
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

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 蜂巢枪管：左键铺设信息素，右键引爆时派出赛博蜂群分头俯冲标记目标。
    /// </summary>
    internal sealed class HiveBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(255, 205, 70);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.08f;
            ctx.DamageMul += -0.14f;
            ctx.HomingMul += 0.12f;
            ctx.ManaCostMul += 0.20f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyPheromone(360, beam.Projectile.owner);
            }
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            List<NPC> targets = SHPCNPCEffects.CollectPheromoneTargets(orb.Projectile.owner, orb.Projectile.Center, 900f, 6);
            if (targets.Count == 0) return;
            int droneCount = targets.Count * 2;
            for (int i = 0; i < droneCount; i++) {
                NPC target = targets[i % targets.Count];
                Vector2 spawn = orb.Projectile.Center + Main.rand.NextVector2Circular(70f, 70f);
                Vector2 vel = (target.Center - spawn).SafeNormalize(Main.rand.NextVector2CircularEdge(1f, 1f)) * 9f;
                Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                    spawn, vel,
                    ModContent.ProjectileType<SHPCHiveDroneProj>(),
                    Math.Max(orb.Projectile.damage / 5, 1), 0f, orb.Projectile.owner, ai0: target.whoAmI);
            }
            //召唤瞬间：信息素扩散环 + 屏幕震动 + 音效
            SHPCNaturalFx.Shake(2f);
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.NPCHit44 with { Volume = 0.5f, Pitch = 0.3f }, orb.Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath28 with { Volume = 0.4f, Pitch = -0.1f }, orb.Projectile.Center);
                for (int i = 0; i < 12; i++) {
                    float ang = MathHelper.TwoPi * i / 12f;
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 4f);
                    PRTLoader.AddParticle(new PRT_GammaIonize(
                        orb.Projectile.Center, vel,
                        new Color(255, 215, 90), Main.rand.NextFloat(0.6f, 1f),
                        Main.rand.Next(20, 36), Main.rand.NextFloat()));
                }
                PRTLoader.AddParticle(new PRT_StarPulseRing(
                    orb.Projectile.Center, Vector2.Zero,
                    new Color(255, 215, 90, 0), 0.05f, 0.7f, 22));
            }
        }
    }

    /// <summary>
    /// 赛博蜂群个体：保留 14 帧位置历史 + Trail（CyberTraceBeam shader），头部 SoftGlow + LightShotAlt 锥光
    /// </summary>
    internal sealed class SHPCHiveDroneProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int TrailLen = 14;
        private static readonly Vector3 CoreVec = new Color(255, 240, 160).ToVector3();
        private static readonly Vector3 GlowVec = new Color(255, 200, 60).ToVector3();
        private static readonly Vector3 AuraVec = new Color(120, 90, 25).ToVector3();

        private Vector2[] trailPoints;
        private Trail trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 110;
            Projectile.extraUpdates = 1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            NPC target = null;
            int targetIndex = (int)Projectile.ai[0];
            if (targetIndex >= 0 && targetIndex < Main.maxNPCs && Main.npc[targetIndex].active) {
                target = Main.npc[targetIndex];
            }
            target ??= Projectile.Center.FindClosestNPC(460f, false, true);
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity.SafeNormalize(Vector2.UnitX)) * 13f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.08f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.45f, 0.15f));
            //偶发蜜糖火星
            if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 3 == 0) {
                Vector2 vel = -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.6f, 1.4f);
                PRTLoader.AddParticle(new PRT_Sparkle(
                    Projectile.Center + Main.rand.NextVector2Circular(2f, 2f), vel,
                    new Color(255, 210, 80), new Color(140, 90, 25),
                    Main.rand.NextFloat(0.3f, 0.55f), Main.rand.Next(8, 14),
                    Main.rand.NextFloat(-0.3f, 0.3f), 0.7f));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.NPCDeath28 with { Volume = 0.35f, Pitch = 0.2f }, target.Center);
            //蜂窝六边形效果：HeavenStar
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(-0.1f, 0.1f);
                PRTLoader.AddParticle(new PRT_HeavenStar(
                    target.Center, Vector2.Zero,
                    new Color(255, 220, 100), new Color(120, 80, 25), angle,
                    new Vector2(0.2f, 0.05f), new Vector2(0.45f, 0.12f), 26,
                    Main.rand.NextFloat(-0.05f, 0.05f), 0.85f));
            }
            PRTLoader.AddParticle(new PRT_StarPulseRing(
                target.Center, Vector2.Zero,
                new Color(255, 215, 90, 0), 0.05f, 0.35f, 18));
        }

        private float WidthFunction(float progress) {
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            return MathHelper.Lerp(2f, 9f, taper);
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos == null || Projectile.oldPos.Length < 2) return;

            Effect shader = EffectLoader.CyberTraceBeam?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.ThunderTrail?.Value ?? CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trailPoints ??= new Vector2[TrailLen];
            Vector2 head = Projectile.Center;
            for (int i = 0; i < TrailLen; i++) {
                Vector2 raw = i < Projectile.oldPos.Length ? Projectile.oldPos[i] : Vector2.Zero;
                trailPoints[i] = raw == Vector2.Zero ? head : raw + Projectile.Size * 0.5f;
            }

            trail ??= new Trail(trailPoints, WidthFunction, ColorFunction);
            trail.TrailPositions = trailPoints;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.06f);
            shader.Parameters["fadeAlpha"]?.SetValue(1f);
            shader.Parameters["coreColor"]?.SetValue(CoreVec);
            shader.Parameters["glowColor"]?.SetValue(GlowVec);
            shader.Parameters["auraColor"]?.SetValue(AuraVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);
            shader.Parameters["overdriveAmount"]?.SetValue(0f);
            shader.Parameters["glitchBurst"]?.SetValue(0f);
            shader.Parameters["odCoreColor"]?.SetValue(CoreVec);
            shader.Parameters["odGlowColor"]?.SetValue(GlowVec);
            shader.Parameters["odAuraColor"]?.SetValue(AuraVec);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color inner = new Color(255, 230, 130, 0) * 0.85f;
                Color outer = new Color(120, 90, 25, 0) * 0.45f;
                SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, 0.5f, 0f, 2);
            }
            Texture2D shot = CWRAsset.LightShotAlt?.Value;
            if (shot != null) {
                Vector2 origin = new(shot.Width, shot.Height * 0.5f);
                spriteBatch.Draw(shot, baseScreen, null, new Color(255, 200, 80, 0) * 0.55f,
                    Projectile.rotation, origin, new Vector2(0.35f, 0.18f), SpriteEffects.None, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
