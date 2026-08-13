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
    /// <summary>蜂巢枪管，左键铺信息素，右键派蜂俯冲</summary>
    internal sealed class HiveBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(255, 205, 70);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.06f;
            ctx.DamageMul += -0.06f;
            ctx.HomingMul += 0.1f;
            ctx.ManaCostMul += 0.24f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyPheromone(target, 360, beam.Projectile.owner);
                //信息素落标提示，金尘自目标上飘
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_Sparkle>(
                            target.Center + Main.rand.NextVector2Circular(target.width * 0.4f, target.height * 0.4f),
                            new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.8f, 1.6f)),
                            new Color(255, 215, 90), Main.rand.NextFloat(0.28f, 0.5f))
                            .Configure(new Color(150, 100, 30), Main.rand.Next(16, 26), 0f, 0.6f);
                    }
                }
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
            //召唤震+环+音
            SHPCNaturalFx.Shake(2f);
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.NPCHit44 with { Volume = 0.5f, Pitch = 0.3f }, orb.Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath28 with { Volume = 0.4f, Pitch = -0.1f }, orb.Projectile.Center);
                for (int i = 0; i < 12; i++) {
                    float ang = MathHelper.TwoPi * i / 12f;
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 4f);
                    PRTLoader.NewParticle<PRT_GammaIonize>(orb.Projectile.Center, vel, new Color(255, 215, 90), Main.rand.NextFloat(0.6f, 1f)).Configure(Main.rand.Next(20, 36), Main.rand.NextFloat());
                }
                //加色批禁 A=0
                PRTLoader.NewParticle<PRT_StarPulseRing>(orb.Projectile.Center, Vector2.Zero, new Color(255, 215, 90), 0.05f).Configure(0.05f, 0.7f, 22);
            }
        }
    }

    /// <summary>赛博蜂，Trail+Additive 头光</summary>
    internal sealed class SHPCHiveDroneProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
            //蜜糖火星
            if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 3 == 0) {
                Vector2 vel = -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.6f, 1.4f);
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(2f, 2f), vel, new Color(255, 210, 80), Main.rand.NextFloat(0.3f, 0.55f)).Configure(new Color(140, 90, 25), Main.rand.Next(8, 14), Main.rand.NextFloat(-0.3f, 0.3f), 0.7f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.NPCDeath28 with { Volume = 0.35f, Pitch = 0.2f }, target.Center);
            //蜂窝 HeavenStar
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(-0.1f, 0.1f);
                PRTLoader.NewParticle<PRT_HeavenStar>(target.Center, Vector2.Zero, new Color(255, 220, 100), 1f).Configure(new Color(120, 80, 25), angle, new Vector2(0.2f, 0.05f), new Vector2(0.45f, 0.12f), 26, Main.rand.NextFloat(-0.05f, 0.05f), 0.85f);
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, new Color(255, 215, 90), 0.05f).Configure(0.05f, 0.35f, 18);
        }

        public override void OnKill(int timeLeft) {
            //蜂消亡余韵，蜜滴坠散+一缕金尘，活得比弹幕久
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 3; i++) {
                Vector2 vel = Projectile.velocity * 0.2f
                    + new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.4f, 1.4f));
                PRTLoader.NewParticle<PRT_SHPCHoneyDrop>(Projectile.Center, vel,
                    new Color(255, 190, 60), Main.rand.NextFloat(0.5f, 0.9f)).Configure(Main.rand.Next(22, 34));
            }
            PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center,
                -Projectile.velocity.SafeNormalize(Vector2.Zero) * 0.6f,
                new Color(255, 220, 110), 0.4f).Configure(new Color(140, 90, 25), 12, 0f, 0.7f);
        }

        private float WidthFunction(float progress) {
            //收窄作能量尾流底层，蜂群感交给点串
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            return MathHelper.Lerp(1.4f, 6f, taper);
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
            shader.Parameters["fadeAlpha"]?.SetValue(0.7f);
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
            //真加色批，A 必须随强度走，A=0 整层不显示
            float t = (float)Main.timeForVisualEffects;
            float speed = Projectile.velocity.Length();
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = forward.RotatedBy(MathHelper.PiOver2);
            //嗡嗡微颤，仅表现不动弹道
            float buzz = MathF.Sin(t * 2.6f + Projectile.whoAmI * 1.7f);
            Vector2 bodyPos = Projectile.Center - Main.screenPosition + perp * buzz * 1.6f;

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Vector2 gOrigin = glow.Size() * 0.5f;
                //蜂群残影点串，离散个体感，逐点错相抖动
                if (Projectile.oldPos != null) {
                    for (int i = 2; i < TrailLen; i += 2) {
                        Vector2 raw = i < Projectile.oldPos.Length ? Projectile.oldPos[i] : Vector2.Zero;
                        if (raw == Vector2.Zero) continue;
                        float dt = i / (float)TrailLen;
                        Vector2 p = raw + Projectile.Size * 0.5f - Main.screenPosition;
                        p += perp * MathF.Sin(t * 3.1f + i * 2.3f + Projectile.whoAmI) * 2.2f * dt;
                        Color dot = Color.Lerp(new Color(255, 220, 110), new Color(150, 95, 30), dt) * (0.6f * (1f - dt) + 0.08f);
                        spriteBatch.Draw(glow, p, null, dot, 0f, gOrigin, MathHelper.Lerp(0.16f, 0.05f, dt), SpriteEffects.None, 0f);
                    }
                }
                //头部暖光双层
                spriteBatch.Draw(glow, bodyPos, null, new Color(255, 235, 150) * 0.7f, 0f, gOrigin, 0.28f, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, bodyPos, null, new Color(140, 95, 30) * 0.35f, 0f, gOrigin, 0.52f, SpriteEffects.None, 0f);
            }
            //蜂体，速度拉伸琥珀纺锤，26-44px 蜂的体量而非曳光弹
            Texture2D shot = CWRAsset.LightShotAlt?.Value;
            if (shot != null) {
                Vector2 origin = new(shot.Width, shot.Height * 0.5f);
                float stretch = 0.10f + speed * 0.005f;
                spriteBatch.Draw(shot, bodyPos, null, new Color(255, 200, 80) * 0.8f,
                    Projectile.rotation, origin, new Vector2(stretch, 0.11f), SpriteEffects.None, 0f);
            }
            //双翅高频交替闪，2f 换拍
            Texture2D wing = CWRAsset.LightShot?.Value;
            if (wing != null) {
                bool beat = ((Main.GameUpdateCount / 2) + (uint)Projectile.whoAmI) % 2 == 0;
                Vector2 wOrigin = new(wing.Width, wing.Height * 0.5f);
                Color wcA = new Color(230, 240, 200) * (beat ? 0.5f : 0.16f);
                Color wcB = new Color(230, 240, 200) * (beat ? 0.16f : 0.5f);
                Vector2 wingRoot = bodyPos - forward * 4f;
                spriteBatch.Draw(wing, wingRoot, null, wcA, Projectile.rotation - 1.05f, wOrigin, new Vector2(0.07f, 0.045f), SpriteEffects.None, 0f);
                spriteBatch.Draw(wing, wingRoot, null, wcB, Projectile.rotation + 1.05f, wOrigin, new Vector2(0.07f, 0.045f), SpriteEffects.None, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
