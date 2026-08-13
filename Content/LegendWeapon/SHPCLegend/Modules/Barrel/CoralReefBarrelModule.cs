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
                //浪涌白沫环
                if (Main.netMode != NetmodeID.Server) {
                    PRTLoader.NewParticle<PRT_DWave>(proj.Center, Vector2.Zero,
                        new Color(220, 245, 240), 0.06f).Configure(new Vector2(1f, 0.75f), 0f, 0.5f, 20);
                }
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
        private float age;
        //兄弟锚点缓存，DrawPrimitives 复用
        private readonly List<Vector2> linkedAnchors = new();
        private readonly List<Trail> reefTrails = new();
        private readonly List<Vector2[]> reefSegments = new();

        //出生生长包络，22f 长成
        private float GrowEase => 1f - MathF.Pow(1f - MathHelper.Clamp(age / 22f, 0f, 1f), 3f);
        //濒死钙化包络，末36f 褪成骨白
        private float FadeOutT => MathHelper.Clamp(Projectile.timeLeft / 36f, 0f, 1f);

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
            age++;
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % LinkScanInterval == 0) {
                CollectLinks();
            }
            if (Main.netMode != NetmodeID.Server) {
                //息肉孢子，24帧节流
                if (Main.GameUpdateCount % 24 == 0) {
                    PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f), new Vector2(0f, Main.rand.NextFloat(-0.6f, 0.2f)), new Color(255, 130, 170), Main.rand.NextFloat(0.35f, 0.7f)).Configure(new Color(120, 220, 200), Main.rand.Next(20, 40), Main.rand.NextFloat(-0.15f, 0.15f), 0.7f);
                }
                //水泡，锚点或礁线上升起，whoAmI 错峰
                if (Main.GameUpdateCount % 14 == (uint)(Projectile.whoAmI % 14)) {
                    Vector2 pos;
                    if (linkedAnchors.Count > 0 && Main.rand.NextBool()) {
                        Vector2 other = linkedAnchors[Main.rand.Next(linkedAnchors.Count)];
                        pos = Vector2.Lerp(Projectile.Center, other, Main.rand.NextFloat(0.15f, 0.85f));
                    }
                    else {
                        pos = Projectile.Center + Main.rand.NextVector2Circular(16f, 12f);
                    }
                    PRTLoader.NewParticle<PRT_SHPCCoralBubble>(pos, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.1f)),
                        new Color(190, 240, 230), Main.rand.NextFloat(0.04f, 0.08f)).Configure(Main.rand.Next(26, 44));
                }
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.35f, 0.45f) * 0.6f);
        }

        public override void OnKill(int timeLeft) {
            //钙质碎裂余韵，骨白碎屑+气泡逸散
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.4f }, Projectile.Center);
            for (int i = 0; i < 7; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.6f, 2.2f) + new Vector2(0f, 0.6f);
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f), vel,
                    Color.Lerp(new Color(235, 226, 212), new Color(255, 130, 160), Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.3f, 0.5f))
                    .Configure(new Color(150, 120, 110), Main.rand.Next(16, 28), Main.rand.NextFloat(-0.2f, 0.2f), 0.55f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_SHPCCoralBubble>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.6f)),
                    new Color(190, 240, 230), Main.rand.NextFloat(0.04f, 0.07f)).Configure(Main.rand.Next(20, 34));
            }
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
            return 4f + taper * 8f;
        }

        private Color ReefColor(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (linkedAnchors.Count == 0) return;
            //专属水流材质，缺 fxc 回退共享电弧
            Effect flowFx = EffectLoader.SHPCModCoralFlow?.Value;
            Effect shader = flowFx ?? EffectLoader.CyberDataArc?.Value;
            if (shader == null) return;

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
            //礁线随锚出生淡入、濒死淡出
            shader.Parameters["fadeAlpha"]?.SetValue(GrowEase * FadeOutT);
            shader.Parameters["coreColor"]?.SetValue(PinkVec);
            shader.Parameters["glowColor"]?.SetValue(TealVec);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            if (flowFx != null) {
                Texture2D noise = CWRAsset.PerlinNoise?.Value;
                if (noise == null) return;
                //s1 显式绑定，shader 内 register(s1)
                device.Textures[1] = noise;
                device.SamplerStates[1] = SamplerState.LinearWrap;
                //水带走预乘 AlphaBlend，边缘深色可压暗
                device.BlendState = BlendState.AlphaBlend;
                shader.Parameters["coreColor"]?.SetValue(new Color(150, 230, 215).ToVector3());
                shader.Parameters["glowColor"]?.SetValue(new Color(15, 80, 85).ToVector3());
            }
            else {
                Texture2D noise = CWRAsset.ThunderTrail?.Value ?? CWRAsset.Extra_193?.Value;
                if (noise == null) return;
                shader.Parameters["uNoiseTex"]?.SetValue(noise);
                device.BlendState = BlendState.Additive;
            }
            for (int i = 0; i < linkedAnchors.Count; i++) {
                reefTrails[i].DrawTrail(shader);
            }
            device.BlendState = BlendState.AlphaBlend;
        }

        //LightShot 段绘，A 起左缘中点、沿 ang 伸 len，箭头尖端朝外
        private static void DrawBranchSeg(Texture2D shot, Vector2 a, float ang, float len, float thick, Color col) {
            Vector2 origin = new(0f, shot.Height * 0.5f);
            Main.spriteBatch.Draw(shot, a, null, col, ang, origin, new Vector2(len / shot.Width, thick), SpriteEffects.None, 0f);
        }

        //逐枝确定性 0-1 哈希
        private float BranchHash(int i) {
            float v = MathF.Sin(seedAngle * 3.7f + i * 13.71f) * 43758.5453f;
            return v - MathF.Floor(v);
        }

        public override bool PreDraw(ref Color lightColor) {
            //钙质骨架枝，根骨白梢息肉彩，出生长出、濒死钙化褪色
            Texture2D shot = CWRAsset.LightShot?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (shot == null) return false;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float calcify = 1f - FadeOutT;
            //末14f 整体透明淡出，防一帧消失
            float aMix = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            float t = (float)Main.timeForVisualEffects;
            Color boneWhite = new(235, 226, 212, 0);
            for (int i = 0; i < 4; i++) {
                float h = BranchHash(i);
                float gi = MathHelper.Clamp((age - i * 2.5f) / 20f, 0f, 1f);
                gi = 1f - MathF.Pow(1f - gi, 3f);
                if (gi <= 0f) continue;
                //梢部水流微摆，替换掉逐帧随机抖角
                float sway = MathF.Sin(t * 1.35f + i * 1.9f) * 0.05f * gi;
                float ang = seedAngle + i * MathHelper.PiOver2 + sway;
                float mainLen = (16f + (i % 2) * 8f + 8f * h) * gi + 5f;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 root = baseScreen + dir * 2f;
                Color tipColor = Color.Lerp(new Color(255, 130, 160, 0), new Color(120, 220, 200, 0), i / 4f);
                Color rootColor = Color.Lerp(tipColor, boneWhite, 0.6f + calcify * 0.4f);
                Color polypColor = Color.Lerp(tipColor, boneWhite, calcify * 0.85f);
                //主干粗段
                DrawBranchSeg(shot, root, ang, mainLen * 0.66f, 0.30f, rootColor * (0.8f * aMix));
                //梢段细，摆幅加倍
                Vector2 branchPoint = root + dir * mainLen * 0.55f;
                float tipAng = ang + sway * 1.8f;
                DrawBranchSeg(shot, branchPoint, tipAng, mainLen * 0.5f, 0.15f, polypColor * (0.9f * aMix));
                //子叉，奇偶换边
                float forkAng = ang + (0.55f + 0.3f * h) * ((i % 2 == 0) ? 1f : -1f);
                DrawBranchSeg(shot, branchPoint, forkAng, mainLen * 0.45f, 0.13f, polypColor * (0.7f * aMix));
                //息肉端点微光
                if (glow != null) {
                    Vector2 gOrigin = glow.Size() * 0.5f;
                    Vector2 tipEnd = branchPoint + tipAng.ToRotationVector2() * mainLen * 0.5f;
                    Vector2 forkEnd = branchPoint + forkAng.ToRotationVector2() * mainLen * 0.45f;
                    Color polypGlow = new Color(polypColor.R, polypColor.G, polypColor.B, 0) * (0.75f * gi * aMix);
                    Main.spriteBatch.Draw(glow, tipEnd, null, polypGlow, 0f, gOrigin, 0.10f, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(glow, forkEnd, null, polypGlow * 0.7f, 0f, gOrigin, 0.075f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            //真加色批，A 必须随强度走，A=0 整层不显示
            float fadeMix = GrowEase * MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            Color inner = new Color(255, 150, 180) * 0.5f * fadeMix;
            Color outer = new Color(80, 220, 190) * 0.22f * fadeMix;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, 0.55f, 0f, 3);
        }
    }
}
