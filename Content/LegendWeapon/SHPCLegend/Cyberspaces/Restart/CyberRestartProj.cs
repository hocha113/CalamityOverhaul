using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Restart
{
    /// <summary>赛博重启演出弹幕，贴附主人领域中心；按 owner 取 CyberspacePlayer</summary>
    internal class CyberRestartProj : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //黑墙裂缝
        private const int CrackCount = 7;
        //每条裂缝的角度
        private float[] crackAngles;
        //每条裂缝的折线相位种子
        private float[] crackSeeds;
        //每条裂缝出现的延迟帧
        private int[] crackDelays;

        //收缩粒带
        private const int StreamCount = 18;
        private float[] streamAngles;
        private float[] streamLag;

        //炸裂尘埃
        private const int DebrisCount = 22;
        private float[] debrisAngles;
        private float[] debrisDist;
        private float[] debrisLag;

        //奇点核心抖动相位
        private float coreSeed;

        //寿命与 CyberRestart.TotalFrames 对齐
        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4096;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = CyberRestart.TotalFrames + 4;
        }

        public override void AI() {
            //每帧贴附主人领域中心
            CyberspacePlayer cp = Cyberspace.For(Projectile.owner);
            if (cp != null) {
                Projectile.Center = cp.DomainCenter;
            }
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Init();
            }
        }

        /// <summary>取主人 CyberspacePlayer，离线时 null</summary>
        private CyberspacePlayer OwnerCp() => Cyberspace.For(Projectile.owner);

        private void Init() {
            crackAngles = new float[CrackCount];
            crackSeeds = new float[CrackCount];
            crackDelays = new int[CrackCount];
            float baseAng = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < CrackCount; i++) {
                crackAngles[i] = baseAng + i * MathHelper.TwoPi / CrackCount
                    + Main.rand.NextFloat(-0.18f, 0.18f);
                crackSeeds[i] = Main.rand.NextFloat();
                //错开出现，营造"逐道撕裂"的连锁感
                crackDelays[i] = (int)MathHelper.Lerp(0f, CyberRestart.PhaseTearEnd * 0.55f,
                    i / (float)CrackCount);
            }

            streamAngles = new float[StreamCount];
            streamLag = new float[StreamCount];
            for (int i = 0; i < StreamCount; i++) {
                streamAngles[i] = Main.rand.NextFloat(MathHelper.TwoPi);
                streamLag[i] = Main.rand.NextFloat(0f, 0.35f);
            }

            debrisAngles = new float[DebrisCount];
            debrisDist = new float[DebrisCount];
            debrisLag = new float[DebrisCount];
            for (int i = 0; i < DebrisCount; i++) {
                debrisAngles[i] = Main.rand.NextFloat(MathHelper.TwoPi);
                debrisDist[i] = Main.rand.NextFloat(120f, 360f);
                debrisLag[i] = Main.rand.NextFloat(0f, 0.25f);
            }

            coreSeed = Main.rand.NextFloat();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (pixel == null || glow == null || crackAngles == null) {
                return false;
            }

            int t = CyberRestart.TotalFrames - Math.Max(0, Projectile.timeLeft - 4) + 1;
            //尾帧不画
            if (t > CyberRestart.TotalFrames + 1) {
                return false;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();

            //领域着色器四阶段，自管 Immediate
            DrawFieldShader(drawPos, t);

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            DrawCracks(pixel, drawPos, t);
            DrawCollapseStreams(pixel, drawPos, t);
            DrawSingularityCore(pixel, glow, drawPos, t);
            DrawBurstFlash(pixel, glow, drawPos, t);
            DrawBurstDebris(pixel, drawPos, t);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        //领域中心锚点，2R 方 quad 加法
        //四阶段权重 0..1
        private void DrawFieldShader(Vector2 drawPos, int t) {
            Effect shader = EffectLoader.CyberRestartField?.Value;
            if (shader == null) {
                return;
            }
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (canvas == null || noise == null) {
                return;
            }

            float tearK = MathHelper.Clamp(t / (float)CyberRestart.PhaseTearEnd, 0f, 1f);
            float collapseK;
            if (t <= CyberRestart.PhaseTearEnd) {
                collapseK = 0f;
            }
            else if (t >= CyberRestart.PhaseSingularityEnd) {
                collapseK = 1f;
            }
            else {
                collapseK = (t - CyberRestart.PhaseTearEnd) /
                    (float)(CyberRestart.PhaseSingularityEnd - CyberRestart.PhaseTearEnd);
            }
            float singularityK;
            if (t < CyberRestart.PhaseCollapseEnd - 6) {
                singularityK = 0f;
            }
            else if (t <= CyberRestart.PhaseSingularityEnd) {
                singularityK = MathHelper.Clamp((t - (CyberRestart.PhaseCollapseEnd - 6)) /
                    (float)(CyberRestart.PhaseSingularityEnd - (CyberRestart.PhaseCollapseEnd - 6)), 0f, 1f);
            }
            else {
                float k = (t - CyberRestart.PhaseSingularityEnd) / 8f;
                singularityK = MathHelper.Clamp(1f - k, 0f, 1f);
            }
            float burstK;
            if (t < CyberRestart.PhaseSingularityEnd) {
                burstK = 0f;
            }
            else {
                burstK = MathHelper.Clamp((t - CyberRestart.PhaseSingularityEnd) /
                    (float)(CyberRestart.PhaseBurstEnd - CyberRestart.PhaseSingularityEnd), 0f, 1f);
            }

            //前4软入，末6软出
            float globalAlpha = 1f;
            if (t < 4) {
                globalAlpha = t / 4f;
            }
            else if (t > CyberRestart.PhaseBurstEnd - 6) {
                globalAlpha = MathHelper.Clamp((CyberRestart.TotalFrames - t) / 6f, 0f, 1f);
            }

            CyberspacePlayer ownerCp = OwnerCp();
            float ownerTime = ownerCp?.EffectTime ?? Cyberspace.EffectTime;
            shader.Parameters["uTime"]?.SetValue(ownerTime * 1.4f);
            shader.Parameters["tearK"]?.SetValue(tearK);
            shader.Parameters["collapseK"]?.SetValue(collapseK);
            shader.Parameters["singularityK"]?.SetValue(singularityK);
            shader.Parameters["burstK"]?.SetValue(burstK);
            shader.Parameters["crackSeed"]?.SetValue(coreSeed);
            shader.Parameters["globalAlpha"]?.SetValue(globalAlpha);

            //基准半径=主人峰值，避 RestartCollapse
            float ownerRadius = ownerCp?.Radius ?? Cyberspace.BaseRadius;
            float baseRadius = MathF.Max(ownerRadius, Cyberspace.BaseRadius);
            float drawDiameter = baseRadius * 2.3f;

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            Main.spriteBatch.End();
        }

        //黑墙裂缝，撕裂外扩收缩吸回
        private void DrawCracks(Texture2D pixel, Vector2 drawPos, int t) {
            if (t > CyberRestart.PhaseSingularityEnd) return;

            Vector2 origin = pixel.Size() * 0.5f;
            //外径取主人 EffectiveOuterRadius
            CyberspacePlayer ownerCp = OwnerCp();
            float ownerEffR = ownerCp?.EffectiveOuterRadius ?? Cyberspace.BaseRadius;
            float baseLen = MathHelper.Clamp(ownerEffR * 0.85f, 240f, 720f);
            //收缩前用原半径
            if (t <= CyberRestart.PhaseTearEnd) {
                float rawR = ownerCp?.Radius ?? Cyberspace.BaseRadius;
                if (rawR > baseLen) baseLen = rawR * 0.85f;
            }

            for (int i = 0; i < CrackCount; i++) {
                int local = t - crackDelays[i];
                if (local <= 0) continue;

                //生长，撕裂末满长
                float growSpan = MathF.Max(1f, CyberRestart.PhaseTearEnd - crackDelays[i]);
                float grow = MathHelper.Clamp(local / growSpan, 0f, 1f);
                grow = 1f - MathF.Pow(1f - grow, 2.6f);

                //收缩吸回
                float retract = 1f;
                if (t > CyberRestart.PhaseTearEnd) {
                    float k = MathHelper.Clamp(
                        (t - CyberRestart.PhaseTearEnd) / (float)(CyberRestart.PhaseSingularityEnd - CyberRestart.PhaseTearEnd),
                        0f, 1f);
                    retract = 1f - k;
                    retract *= retract;
                }

                float length = baseLen * grow * retract;
                if (length < 4f) continue;

                float ang = crackAngles[i];
                float seed = crackSeeds[i];

                //折线裂痕
                const int Segs = 9;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 perp = new(-dir.Y, dir.X);

                for (int s = 0; s < Segs; s++) {
                    float k0 = s / (float)Segs;
                    float k1 = (s + 1) / (float)Segs;
                    float r0 = length * k0;
                    float r1 = length * k1;
                    //折线中段最大
                    float band0 = MathF.Sin(k0 * MathF.PI) * 12f;
                    float band1 = MathF.Sin(k1 * MathF.PI) * 12f;
                    float jitter0 = MathF.Sin((seed * 31f + k0 * 17f + t * 0.18f)) * band0;
                    float jitter1 = MathF.Sin((seed * 29f + k1 * 19f + t * 0.18f + 1.3f)) * band1;
                    Vector2 p0 = drawPos + dir * r0 + perp * jitter0;
                    Vector2 p1 = drawPos + dir * r1 + perp * jitter1;

                    Vector2 mid = (p0 + p1) * 0.5f;
                    Vector2 d = p1 - p0;
                    float segLen = d.Length();
                    if (segLen < 0.5f) continue;
                    float rot = MathF.Atan2(d.Y, d.X);

                    //暗红芯+深边
                    float fade = retract * (0.55f + 0.45f * grow);
                    Color core = new Color(0.85f, 0.10f, 0.12f) * fade * 0.7f;
                    Color edge = new Color(0.18f, 0.02f, 0.04f) * fade * 0.85f;

                    //黑剃刀边
                    Main.spriteBatch.Draw(pixel, mid, null, edge, rot, origin,
                        new Vector2(segLen + 1.5f, 3.6f), SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(pixel, mid, null, core, rot, origin,
                        new Vector2(segLen + 1f, 1.6f), SpriteEffects.None, 0f);
                }

                //末端亮点
                Vector2 tip = drawPos + dir * length;
                float tipPulse = 0.5f + 0.5f * MathF.Sin(t * 0.6f + seed * 19f);
                Color tipCol = new Color(1f, 0.55f, 0.4f) * retract * (0.45f + 0.55f * tipPulse);
                Main.spriteBatch.Draw(pixel, tip, null, tipCol, 0f, origin,
                    new Vector2(6f, 6f), SpriteEffects.None, 0f);
            }
        }

        //收缩粒带，向心收束
        private void DrawCollapseStreams(Texture2D pixel, Vector2 drawPos, int t) {
            if (t <= CyberRestart.PhaseTearEnd || t > CyberRestart.PhaseSingularityEnd) return;

            float k = (float)(t - CyberRestart.PhaseTearEnd)
                / MathF.Max(1f, CyberRestart.PhaseSingularityEnd - CyberRestart.PhaseTearEnd);

            //初始外径=主人峰值
            CyberspacePlayer ownerCp = OwnerCp();
            float ownerRadius = ownerCp?.Radius ?? Cyberspace.BaseRadius;
            float startR = MathHelper.Clamp(ownerRadius, 320f, 760f);
            Vector2 origin = pixel.Size() * 0.5f;

            for (int i = 0; i < StreamCount; i++) {
                float lag = streamLag[i];
                float span = 1f - lag;
                if (span <= 0f) continue;
                float local = MathHelper.Clamp((k - lag) / span, 0f, 1f);
                if (local <= 0f) continue;

                float ease = 1f - MathF.Pow(1f - local, 2.4f);
                float radius = MathHelper.Lerp(startR, 0f, ease);

                float ang = streamAngles[i] + local * 1.6f;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 pos = drawPos + dir * radius;
                float rot = ang;
                float length = MathHelper.Lerp(40f, 120f, local);
                float width = MathHelper.Lerp(2.5f, 1.0f, local);

                float alpha = 0.45f + local * 0.55f;
                Color core = new Color(1f, 0.45f, 0.35f) * alpha * 0.7f;
                Color edge = new Color(0.55f, 0.05f, 0.08f) * alpha * 0.85f;

                Main.spriteBatch.Draw(pixel, pos, null, edge, rot, origin,
                    new Vector2(length * 1.2f, width * 2.2f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(pixel, pos, null, core, rot, origin,
                    new Vector2(length, width), SpriteEffects.None, 0f);
            }

            //外环径向缩
            float ringR = MathHelper.Lerp(startR, 0f, MathF.Pow(k, 1.6f));
            DrawHollowRing(pixel, drawPos, ringR, k, origin);
        }

        //空心环，细线段拼
        private void DrawHollowRing(Texture2D pixel, Vector2 drawPos, float r, float k, Vector2 origin) {
            if (r < 6f) return;
            const int Segs = 36;
            float fade = MathHelper.Clamp(0.3f + k * 0.85f, 0f, 1f);
            Color edge = new Color(0.55f, 0.08f, 0.10f) * fade * 0.6f;
            for (int i = 0; i < Segs; i++) {
                float a0 = i * MathHelper.TwoPi / Segs;
                float a1 = (i + 1) * MathHelper.TwoPi / Segs;
                Vector2 p0 = drawPos + a0.ToRotationVector2() * r;
                Vector2 p1 = drawPos + a1.ToRotationVector2() * r;
                Vector2 mid = (p0 + p1) * 0.5f;
                Vector2 d = p1 - p0;
                float segLen = d.Length();
                if (segLen < 0.1f) continue;
                float rot = MathF.Atan2(d.Y, d.X);
                Main.spriteBatch.Draw(pixel, mid, null, edge, rot, origin,
                    new Vector2(segLen + 1f, 2.8f), SpriteEffects.None, 0f);
            }
        }

        //奇点红黑核心
        //光晕→黑底→辐条→吸积带→竖缝→暖芯
        //约两倍旧尺，暖红避纯白
        private void DrawSingularityCore(Texture2D pixel, Texture2D glow, Vector2 drawPos, int t) {
            if (t < CyberRestart.PhaseCollapseEnd - 6 || t > CyberRestart.PhaseBurstEnd) return;

            //强度，收缩末浮现，炸裂断
            float fade;
            if (t < CyberRestart.PhaseCollapseEnd) {
                fade = (t - (CyberRestart.PhaseCollapseEnd - 6)) / 6f;
            }
            else if (t <= CyberRestart.PhaseSingularityEnd) {
                fade = 1f;
            }
            else {
                //炸裂前6帧消
                float k = (t - CyberRestart.PhaseSingularityEnd) / 6f;
                fade = MathHelper.Clamp(1f - k, 0f, 1f);
            }
            fade = MathHelper.Clamp(fade, 0f, 1f);
            if (fade <= 0f) return;

            Vector2 origin = pixel.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            //脉动+呼吸
            float pulse = 0.75f + 0.25f * MathF.Sin(t * 0.9f + coreSeed * 13f);
            float breath = 0.92f + 0.08f * MathF.Sin(t * 0.22f + coreSeed * 5f);
            float scale = pulse * breath;

            //外层红黑光晕
            Color outerHaloDeep = new Color(0.55f, 0.06f, 0.08f) * fade * 0.6f;
            Main.spriteBatch.Draw(glow, drawPos, null, outerHaloDeep, 0f, glowOrigin,
                new Vector2(7.2f * scale), SpriteEffects.None, 0f);
            Color outerHaloWarm = new Color(0.85f, 0.20f, 0.15f) * fade * 0.45f;
            Main.spriteBatch.Draw(glow, drawPos, null, outerHaloWarm, 0f, glowOrigin,
                new Vector2(4.8f * scale), SpriteEffects.None, 0f);

            //黑椭圆底
            Color baseBlack = new Color(0.03f, 0.0f, 0.0f) * fade * 0.95f;
            Main.spriteBatch.Draw(pixel, drawPos, null, baseBlack, 0f, origin,
                new Vector2(144f * scale, 420f * scale), SpriteEffects.None, 0f);

            //辐条 12+8
            int spokeMain = 12;
            float spokeSpin = t * 0.020f + coreSeed * 0.7f;
            for (int i = 0; i < spokeMain; i++) {
                float ang = MathHelper.TwoPi * i / spokeMain + spokeSpin;
                float seed = MathF.Sin(i * 12.9898f + coreSeed * 78.233f);
                seed = seed - MathF.Floor(seed);
                float jitter = 0.65f + 0.35f * MathF.Sin(t * 0.7f + i * 1.3f + coreSeed * 4f);
                float spokeLen = (340f + 70f * seed) * scale * jitter;
                float spokeWidth = 3.2f + 1.2f * MathF.Sin(t * 0.5f + i * 2.1f);
                Color spokeCore = new Color(1f, 0.45f, 0.30f) * fade * 0.7f * jitter;
                Color spokeEdge = new Color(0.65f, 0.10f, 0.10f) * fade * 0.55f * jitter;
                Vector2 mid = drawPos;
                Main.spriteBatch.Draw(pixel, mid, null, spokeEdge, ang, origin,
                    new Vector2(spokeLen * 1.05f, spokeWidth + 2.4f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(pixel, mid, null, spokeCore, ang, origin,
                    new Vector2(spokeLen, spokeWidth), SpriteEffects.None, 0f);
            }
            int spokeAlt = 8;
            for (int i = 0; i < spokeAlt; i++) {
                float ang = MathHelper.TwoPi * i / spokeAlt + spokeSpin * -1.4f
                    + MathHelper.Pi / spokeAlt;
                float seed = MathF.Sin(i * 23.451f - coreSeed * 47.7f);
                seed = seed - MathF.Floor(seed);
                float jitter = 0.6f + 0.4f * MathF.Sin(t * 0.9f + i * 0.8f);
                float spokeLen = (220f + 100f * seed) * scale * jitter;
                Color spokeCore = new Color(1f, 0.55f, 0.25f) * fade * 0.55f * jitter;
                Main.spriteBatch.Draw(pixel, drawPos, null, spokeCore, ang, origin,
                    new Vector2(spokeLen, 2.0f), SpriteEffects.None, 0f);
            }

            //水平吸积带
            float diskPulse = 0.7f + 0.3f * MathF.Sin(t * 0.5f + coreSeed * 9f);
            float diskLen = 640f * scale * diskPulse;
            Color diskGlow = new Color(0.55f, 0.10f, 0.10f) * fade * 0.8f;
            Color diskCore = new Color(1f, 0.45f, 0.25f) * fade * 0.9f;
            Color diskHot = new Color(1f, 0.75f, 0.45f) * fade * diskPulse;
            Main.spriteBatch.Draw(glow, drawPos, null, diskGlow, 0f, glowOrigin,
                new Vector2(diskLen / glow.Width * 1.4f, 1.7f * scale), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, drawPos, null, diskCore, 0f, origin,
                new Vector2(diskLen, 8.4f * scale), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, drawPos, null, diskHot, 0f, origin,
                new Vector2(diskLen * 0.85f, 2.8f * scale), SpriteEffects.None, 0f);

            //红芯竖缝+内芯
            Color slitDeep = new Color(0.95f, 0.10f, 0.18f) * fade * 0.9f;
            Color slitHot = new Color(1f, 0.55f, 0.45f) * fade * pulse;
            Color slitInner = new Color(1f, 0.85f, 0.65f) * fade * pulse * 0.85f;
            Main.spriteBatch.Draw(pixel, drawPos, null, slitDeep, 0f, origin,
                new Vector2(32f * scale, 360f * scale), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, drawPos, null, slitHot, 0f, origin,
                new Vector2(10f * scale, 320f * scale), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, drawPos, null, slitInner, 0f, origin,
                new Vector2(3.6f, 300f * scale), SpriteEffects.None, 0f);

            //横向短闪
            float jitter2 = MathF.Sin(t * 1.7f + coreSeed * 23f);
            if (jitter2 > 0.5f) {
                float intensity = jitter2 - 0.4f;
                Color cross = new Color(1f, 0.55f, 0.35f) * fade * intensity;
                Main.spriteBatch.Draw(pixel, drawPos, null, cross, 0f, origin,
                    new Vector2(240f * scale, 4.0f), SpriteEffects.None, 0f);
            }

            //中心暖高光
            Color centerHot = new Color(1f, 0.82f, 0.55f) * fade * pulse * 0.85f;
            Main.spriteBatch.Draw(glow, drawPos, null, centerHot, 0f, glowOrigin,
                new Vector2(1.7f * scale), SpriteEffects.None, 0f);
        }

        //炸裂十字闪
        private void DrawBurstFlash(Texture2D pixel, Texture2D glow, Vector2 drawPos, int t) {
            int burstStart = CyberRestart.PhaseSingularityEnd;
            int burstWindowEnd = burstStart + 18;
            if (t < burstStart || t > burstWindowEnd) return;

            float k = (t - burstStart) / (float)(burstWindowEnd - burstStart);
            float pulse;
            if (k < 0.25f) {
                pulse = k / 0.25f;
            }
            else {
                pulse = 1f - (k - 0.25f) / 0.75f;
            }
            pulse = MathHelper.Clamp(pulse, 0f, 1f);
            if (pulse <= 0f) return;

            Vector2 origin = pixel.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;

            Color halo = new Color(1f, 0.7f, 0.5f) * pulse * 0.85f;
            Main.spriteBatch.Draw(glow, drawPos, null, halo, 0f, glowOrigin,
                new Vector2(MathHelper.Lerp(0.6f, 3.2f, pulse)), SpriteEffects.None, 0f);

            float length = MathHelper.Lerp(160f, 460f, pulse);
            float wMain = MathHelper.Lerp(2f, 22f, pulse);
            float wThin = MathHelper.Lerp(1f, 7f, pulse);

            Color hot = new Color(1f, 0.95f, 0.78f) * pulse;
            Color warm = new Color(1f, 0.45f, 0.22f) * pulse * 0.85f;

            Main.spriteBatch.Draw(pixel, drawPos, null, warm, 0f, origin,
                new Vector2(length, wMain * 1.4f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, drawPos, null, warm, MathHelper.PiOver2, origin,
                new Vector2(length * 0.85f, wMain * 1.2f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, drawPos, null, hot, 0f, origin,
                new Vector2(length, wThin), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, drawPos, null, hot, MathHelper.PiOver2, origin,
                new Vector2(length * 0.85f, wThin * 0.85f), SpriteEffects.None, 0f);
        }

        //炸裂红黑残片
        private void DrawBurstDebris(Texture2D pixel, Vector2 drawPos, int t) {
            int start = CyberRestart.PhaseSingularityEnd;
            if (t < start || t > CyberRestart.PhaseBurstEnd) return;

            float k = (t - start) / (float)(CyberRestart.PhaseBurstEnd - start);
            Vector2 origin = pixel.Size() * 0.5f;

            for (int i = 0; i < DebrisCount; i++) {
                float lag = debrisLag[i];
                float span = 1f - lag;
                if (span <= 0f) continue;
                float local = MathHelper.Clamp((k - lag) / span, 0f, 1f);
                if (local <= 0f) continue;

                float ease = 1f - MathF.Pow(1f - local, 2.0f);
                float dist = MathHelper.Lerp(0f, debrisDist[i], ease);
                float ang = debrisAngles[i];
                Vector2 dir = ang.ToRotationVector2();
                Vector2 pos = drawPos + dir * dist;

                float fade = 1f - local;
                fade *= fade;
                if (fade <= 0.02f) continue;

                float rot = ang;
                float length = MathHelper.Lerp(18f, 6f, local);
                Color core = new Color(1f, 0.55f, 0.35f) * fade * 0.85f;
                Color edge = new Color(0.45f, 0.05f, 0.08f) * fade * 1.0f;

                Main.spriteBatch.Draw(pixel, pos, null, edge, rot, origin,
                    new Vector2(length * 1.3f, 3.5f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(pixel, pos, null, core, rot, origin,
                    new Vector2(length, 1.2f), SpriteEffects.None, 0f);
            }
        }

        public override bool ShouldUpdatePosition() => false;

        //屏幕扭曲，奇点向心/炸裂外放
        public void Warp() {
            if (crackAngles == null) return;//Init 尚未触发
            int t = CyberRestart.TotalFrames - Math.Max(0, Projectile.timeLeft - 4) + 1;
            if (t < CyberRestart.PhaseCollapseEnd - 6 || t > CyberRestart.PhaseBurstEnd + 4) return;

            Texture2D warpTex = CWRAsset.DiffusionCircle?.Value;
            if (warpTex == null) return;

            Vector2 origin = warpTex.Size() * 0.5f;
            //扭曲锚主人中心，勿读 Local
            CyberspacePlayer ownerCp = OwnerCp();
            Vector2 ownerCenter = ownerCp?.DomainCenter ?? Projectile.Center;
            Vector2 drawPos = ownerCenter - Main.screenPosition;

            //奇点向心扭曲
            float singWarp;
            if (t < CyberRestart.PhaseCollapseEnd - 6) {
                singWarp = 0f;
            }
            else if (t <= CyberRestart.PhaseSingularityEnd) {
                singWarp = MathHelper.Clamp((t - (CyberRestart.PhaseCollapseEnd - 6)) /
                    (float)(CyberRestart.PhaseSingularityEnd - (CyberRestart.PhaseCollapseEnd - 6)), 0f, 1f);
                singWarp = MathF.Pow(singWarp, 0.7f);
            }
            else {
                singWarp = 0f;
            }

            if (singWarp > 0f) {
                //DiffusionCircle 向心
                Color warpColor = new Color(40, 12, 60) * singWarp;
                float scale = MathHelper.Lerp(1.4f, 0.55f, singWarp);
                float ownerWarpTime = ownerCp?.EffectTime ?? Cyberspace.EffectTime;
                for (int i = 0; i < 3; i++) {
                    float rot = i * MathHelper.PiOver2 * 0.5f + ownerWarpTime * (0.4f + 0.2f * i);
                    Main.spriteBatch.Draw(warpTex, drawPos, null, warpColor, rot,
                        origin, scale, SpriteEffects.None, 0f);
                }
            }

            //炸裂冲击波，前1/3峰
            float burstK;
            if (t < CyberRestart.PhaseSingularityEnd) {
                burstK = 0f;
            }
            else {
                burstK = MathHelper.Clamp((t - CyberRestart.PhaseSingularityEnd) /
                    (float)(CyberRestart.PhaseBurstEnd - CyberRestart.PhaseSingularityEnd), 0f, 1f);
            }
            if (burstK > 0f) {
                float burstAmp;
                if (burstK < 0.32f) {
                    burstAmp = burstK / 0.32f;
                }
                else {
                    burstAmp = 1f - (burstK - 0.32f) / 0.68f;
                }
                burstAmp = MathHelper.Clamp(burstAmp, 0f, 1f);
                if (burstAmp > 0.01f) {
                    //外扩透镜暖红
                    Color shockColor = new Color(80, 20, 30) * burstAmp;
                    float scale = MathHelper.Lerp(0.6f, 2.6f, burstK);
                    float ownerBurstTime = ownerCp?.EffectTime ?? Cyberspace.EffectTime;
                    for (int i = 0; i < 4; i++) {
                        float rot = i * MathHelper.PiOver2 + ownerBurstTime * 0.7f;
                        Main.spriteBatch.Draw(warpTex, drawPos, null, shockColor, rot,
                            origin, scale, SpriteEffects.None, 0f);
                    }
                }
            }
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}
