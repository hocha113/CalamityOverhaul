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
    /// <summary>珊瑚枪管，命中长锚连礁线，右键浪涌</summary>
    internal sealed class CoralReefBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(255, 115, 150);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.15f;
            ctx.BeamLifeMul += 0.1f;
            ctx.OrbExplosionRadiusMul += 0.08f;
            ctx.ManaCostMul += 0.36f;
        }

        //同主锚点上限
        private const int MaxConcurrentAnchors = 8;
        //同点90px内已有则跳过
        private const float MinSpacing = 90f;

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            int anchorType = ModContent.ProjectileType<SHPCCoralAnchorProj>();
            if (SHPCNaturalFx.CountOwned(beam.Projectile.owner, anchorType) >= MaxConcurrentAnchors) return;
            if (SHPCNaturalFx.HasOwnedNear(beam.Projectile.owner, anchorType, target.Center, MinSpacing)) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                target.Center, Vector2.Zero,
                anchorType, Math.Max(damageDone / 3, 1), 0f, beam.Projectile.owner);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            int detonated = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != orb.Projectile.owner) continue;
                if (proj.type != ModContent.ProjectileType<SHPCCoralAnchorProj>()) continue;
                if (Vector2.DistanceSquared(proj.Center, orb.Projectile.Center) > 900f * 900f) continue;
                //半径160px，ai2 走生成包同步
                Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                    proj.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    Math.Max(orb.Projectile.damage / 3, 1), 0f, orb.Projectile.owner,
                    ai0: 0.55f, ai1: 0f, ai2: 160f);
                detonated++;
            }
            if (detonated > 0) {
                SHPCNaturalFx.Shake(MathF.Min(1f * detonated, 6f));
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item81 with { Volume = 0.55f, Pitch = -0.2f }, orb.Projectile.Center);
                }
            }
        }
    }

    /// <summary>珊瑚锚，枝程序绘+礁线 Trail</summary>
    internal sealed class SHPCCoralAnchorProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Vector3 PinkVec = new Color(255, 110, 140).ToVector3();
        private static readonly Vector3 TealVec = new Color(80, 220, 190).ToVector3();

        private float seedAngle;
        //兄弟锚点缓存，DrawPrimitives 复用
        private readonly List<Vector2> linkedAnchors = new();
        private readonly List<Trail> reefTrails = new();
        private readonly List<Vector2[]> reefSegments = new();

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 90;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        //链接扫描，每4帧错峰
        private const int LinkScanInterval = 4;

        public override void AI() {
            if (seedAngle == 0f) seedAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % LinkScanInterval == 0) {
                CollectLinks();
            }
            //孢子，24帧节流
            if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 24 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f), new Vector2(0f, Main.rand.NextFloat(-0.6f, 0.2f)), new Color(255, 130, 170), Main.rand.NextFloat(0.35f, 0.7f)).Configure(new Color(120, 220, 200), Main.rand.Next(20, 40), Main.rand.NextFloat(-0.15f, 0.15f), 0.7f);
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.35f, 0.45f) * 0.6f);
        }

        private void CollectLinks() {
            linkedAnchors.Clear();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner || other.whoAmI == Projectile.whoAmI) continue;
                if (other.type != Projectile.type) continue;
                if (Vector2.DistanceSquared(other.Center, Projectile.Center) > 360f * 360f) continue;
                //whoAmI 较小端记链，防双画
                if (other.whoAmI > Projectile.whoAmI) continue;
                linkedAnchors.Add(other.Center);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            bool hit = false;
            float point = 0f;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner || other.whoAmI == Projectile.whoAmI) continue;
                if (other.type != Projectile.type || other.whoAmI > Projectile.whoAmI) continue;
                if (Vector2.DistanceSquared(other.Center, Projectile.Center) > 360f * 360f) continue;
                hit |= Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, other.Center, 12f, ref point);
            }
            return hit;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.NPCHit36 with { Volume = 0.4f, Pitch = 0.2f }, target.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Bloomlight>(target.Center + Main.rand.NextVector2Circular(10f, 10f), Main.rand.NextVector2Circular(2f, 2f), Color.Lerp(new Color(255, 110, 140), new Color(80, 220, 190), Main.rand.NextFloat()), Main.rand.NextFloat(0.3f, 0.6f)).Configure(22);
            }
        }

        private float ReefWidth(float progress) {
            //远端变细
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            return 4f + taper * 7f;
        }

        private Color ReefColor(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (linkedAnchors.Count == 0) return;
            Effect shader = EffectLoader.CyberDataArc?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.ThunderTrail?.Value ?? CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            //礁线8段曲线+正弦摆
            while (reefSegments.Count < linkedAnchors.Count) {
                reefSegments.Add(new Vector2[8]);
                reefTrails.Add(new Trail(reefSegments[reefSegments.Count - 1], ReefWidth, ReefColor));
            }
            float drift = (float)Main.timeForVisualEffects * 0.04f;
            for (int i = 0; i < linkedAnchors.Count; i++) {
                Vector2[] pts = reefSegments[i];
                Vector2 start = Projectile.Center;
                Vector2 end = linkedAnchors[i];
                Vector2 dir = end - start;
                Vector2 perp = dir.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                float length = dir.Length();
                float amp = MathF.Min(length * 0.08f, 16f);
                for (int s = 0; s < pts.Length; s++) {
                    float t = s / (float)(pts.Length - 1);
                    float taper = MathF.Sin(t * MathHelper.Pi);
                    float wave = MathF.Sin(drift + t * 5.5f + i) * taper * amp;
                    pts[s] = Vector2.Lerp(start, end, t) + perp * wave;
                }
                reefTrails[i].TrailPositions = pts;
            }

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue(drift);
            shader.Parameters["fadeAlpha"]?.SetValue(1f);
            shader.Parameters["coreColor"]?.SetValue(PinkVec);
            shader.Parameters["glowColor"]?.SetValue(TealVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            for (int i = 0; i < linkedAnchors.Count; i++) {
                reefTrails[i].DrawTrail(shader);
            }
            device.BlendState = BlendState.AlphaBlend;
        }

        public override bool PreDraw(ref Color lightColor) {
            //4 段珊瑚枝
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (shot == null) return false;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            for (int i = 0; i < 4; i++) {
                float a = seedAngle + i * MathHelper.PiOver2 + Main.rand.NextFloat(-0.06f, 0.06f);
                float length = 14f + (i % 2) * 8f + 4f;
                Vector2 dir = a.ToRotationVector2();
                Vector2 tip = baseScreen + dir * length;
                Color inner = Color.Lerp(new Color(255, 130, 160, 0), new Color(120, 220, 200, 0), i / 4f) * 0.85f;
                Color outer = inner * 0.4f;
                Vector2 origin = new(shot.Width, shot.Height * 0.5f);
                Vector2 scale = new(length / shot.Width, 0.18f);
                Main.spriteBatch.Draw(shot, tip, null, inner, a + MathHelper.Pi, origin, scale, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(shot, tip, null, outer, a + MathHelper.Pi, origin, scale * new Vector2(1f, 2f), SpriteEffects.None, 0f);
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            Color inner = new Color(255, 150, 180, 0) * 0.7f;
            Color outer = new Color(80, 220, 190, 0) * 0.3f;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, 0.55f, 0f, 3);
        }
    }
}
