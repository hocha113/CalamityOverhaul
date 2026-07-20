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
            //演出已结束的尾帧不再绘制，避免多余闪烁
            if (t > CyberRestart.TotalFrames + 1) {
                return false;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();

            //领域级演出着色器：撕裂/收缩/奇点/炸裂四阶段一并渲染（自身 Immediate begin/end，结束后无 batch 打开）
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

        //领域级演出着色器：以领域中心为锚点绘制 2*领域半径方形 quad，加法混合
        //四阶段权重通过帧时间转换为 0..1 浮点，分别驱动撕裂/收缩/奇点/炸裂分支
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

            //整体淡入淡出：开场前 4 帧、结束前 6 帧分别做软入软出
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

            //演出基准半径：取主人玩家领域峰值半径 * 1.15，避免被 RestartCollapse 拽小
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

        //黑墙裂缝：撕裂段从中心向外扩散的折线状能量裂痕，整段演出全程可见但收缩段被"吸回"
        private void DrawCracks(Texture2D pixel, Vector2 drawPos, int t) {
            if (t > CyberRestart.PhaseSingularityEnd) return;

            Vector2 origin = pixel.Size() * 0.5f;
            //可视外径基于主人 EffectiveOuterRadius
            CyberspacePlayer ownerCp = OwnerCp();
            float ownerEffR = ownerCp?.EffectiveOuterRadius ?? Cyberspace.BaseRadius;
            float baseLen = MathHelper.Clamp(ownerEffR * 0.85f, 240f, 720f);
            //演出未触发收缩前用领域原半径，避免被 RestartCollapse 拽小
            if (t <= CyberRestart.PhaseTearEnd) {
                float rawR = ownerCp?.Radius ?? Cyberspace.BaseRadius;
                if (rawR > baseLen) baseLen = rawR * 0.85f;
            }

            for (int i = 0; i < CrackCount; i++) {
                int local = t - crackDelays[i];
                if (local <= 0) continue;

                //生长进度：从 0 起步，撕裂段末已达全长
                float growSpan = MathF.Max(1f, CyberRestart.PhaseTearEnd - crackDelays[i]);
                float grow = MathHelper.Clamp(local / growSpan, 0f, 1f);
                grow = 1f - MathF.Pow(1f - grow, 2.6f);

                //收缩段被拉回核心
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

                //裂缝由若干段折线组成，每段轻微偏离主轴，模拟黑墙故障撕裂
                const int Segs = 9;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 perp = new(-dir.Y, dir.X);

                for (int s = 0; s < Segs; s++) {
                    float k0 = s / (float)Segs;
                    float k1 = (s + 1) / (float)Segs;
                    float r0 = length * k0;
                    float r1 = length * k1;
                    //折线偏移：端点压平，中段最大
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

                    //核心暗红，外焰深红黑；与 shader 端裂缝互为细节，整体保持纤细
                    float fade = retract * (0.55f + 0.45f * grow);
                    Color core = new Color(0.85f, 0.10f, 0.12f) * fade * 0.7f;
                    Color edge = new Color(0.18f, 0.02f, 0.04f) * fade * 0.85f;

                    //黑色外框：仅作"剃刀边"使用，不再营造扇形带感
                    Main.spriteBatch.Draw(pixel, mid, null, edge, rot, origin,
                        new Vector2(segLen + 1.5f, 3.6f), SpriteEffects.None, 0f);
                    //红色内焰
                    Main.spriteBatch.Draw(pixel, mid, null, core, rot, origin,
                        new Vector2(segLen + 1f, 1.6f), SpriteEffects.None, 0f);
                }

                //裂缝末端的故障亮点
                Vector2 tip = drawPos + dir * length;
                float tipPulse = 0.5f + 0.5f * MathF.Sin(t * 0.6f + seed * 19f);
                Color tipCol = new Color(1f, 0.55f, 0.4f) * retract * (0.45f + 0.55f * tipPulse);
                Main.spriteBatch.Draw(pixel, tip, null, tipCol, 0f, origin,
                    new Vector2(6f, 6f), SpriteEffects.None, 0f);
            }
        }

        //收缩粒带：从领域边缘向心收束的细长光带，给出"领域被吸入"的直观运动
        private void DrawCollapseStreams(Texture2D pixel, Vector2 drawPos, int t) {
            if (t <= CyberRestart.PhaseTearEnd || t > CyberRestart.PhaseSingularityEnd) return;

            //当前阶段进度 (0..1)
            float k = (float)(t - CyberRestart.PhaseTearEnd)
                / MathF.Max(1f, CyberRestart.PhaseSingularityEnd - CyberRestart.PhaseTearEnd);

            //初始外径：取"主人玩家"领域峰值半径，避免被 RestartCollapse 拉小
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

            //外环收缩：粗描边沿径向缩进，给"领域圈被合拢"的整体动作
            float ringR = MathHelper.Lerp(startR, 0f, MathF.Pow(k, 1.6f));
            DrawHollowRing(pixel, drawPos, ringR, k, origin);
        }

        //空心圆环：用很多细线段拼出，做收缩段的"领域包围圈"
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

        //奇点核心：玩家压缩为红黑维度核心裂缝
        //结构：外层暗红光晕 → 黑椭圆基底 → 放射状辐条 → 水平吸积带 → 红芯竖缝 → 中心暖白
        //总尺寸约为旧版两倍，发光层全部使用暖红/橙红，避开纯白以防晃眼
        private void DrawSingularityCore(Texture2D pixel, Texture2D glow, Vector2 drawPos, int t) {
            if (t < CyberRestart.PhaseCollapseEnd - 6 || t > CyberRestart.PhaseBurstEnd) return;

            //单独的强度曲线：在收缩末段开始浮现，奇点段全亮，炸裂段瞬间断开
            float fade;
            if (t < CyberRestart.PhaseCollapseEnd) {
                fade = (t - (CyberRestart.PhaseCollapseEnd - 6)) / 6f;
            }
            else if (t <= CyberRestart.PhaseSingularityEnd) {
                fade = 1f;
            }
            else {
                //炸裂前 6 帧迅速消散
                float k = (t - CyberRestart.PhaseSingularityEnd) / 6f;
                fade = MathHelper.Clamp(1f - k, 0f, 1f);
            }
            fade = MathHelper.Clamp(fade, 0f, 1f);
            if (fade <= 0f) return;

            Vector2 origin = pixel.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            //高频脉动 + 低频呼吸，避免视觉静止
            float pulse = 0.75f + 0.25f * MathF.Sin(t * 0.9f + coreSeed * 13f);
            float breath = 0.92f + 0.08f * MathF.Sin(t * 0.22f + coreSeed * 5f);
            float scale = pulse * breath;

            //外层红黑光晕：再放大一倍体积，颜色压暖避免刺眼
            Color outerHaloDeep = new Color(0.55f, 0.06f, 0.08f) * fade * 0.6f;
            Main.spriteBatch.Draw(glow, drawPos, null, outerHaloDeep, 0f, glowOrigin,
                new Vector2(7.2f * scale), SpriteEffects.None, 0f);
            Color outerHaloWarm = new Color(0.85f, 0.20f, 0.15f) * fade * 0.45f;
            Main.spriteBatch.Draw(glow, drawPos, null, outerHaloWarm, 0f, glowOrigin,
                new Vector2(4.8f * scale), SpriteEffects.None, 0f);

            //黑椭圆基底：再放大一倍
            Color baseBlack = new Color(0.03f, 0.0f, 0.0f) * fade * 0.95f;
            Main.spriteBatch.Draw(pixel, drawPos, null, baseBlack, 0f, origin,
                new Vector2(144f * scale, 420f * scale), SpriteEffects.None, 0f);

            //放射状辐条：12+8 双层细线，模拟核心向外放出的光辐
            //主层 12 条均匀分布，时间相位让它们慢慢转动
            //辅层 8 条偏移半角度+不同长度，错落感
            int spokeMain = 12;
            float spokeSpin = t * 0.020f + coreSeed * 0.7f;
            for (int i = 0; i < spokeMain; i++) {
                float ang = MathHelper.TwoPi * i / spokeMain + spokeSpin;
                //每条辐条独立微相位，长度抖动
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

            //水平吸积带：仿黑洞吸积盘，比辐条更长更亮，常驻 + 节奏脉动
            float diskPulse = 0.7f + 0.3f * MathF.Sin(t * 0.5f + coreSeed * 9f);
            float diskLen = 640f * scale * diskPulse;
            Color diskGlow = new Color(0.55f, 0.10f, 0.10f) * fade * 0.8f;
            Color diskCore = new Color(1f, 0.45f, 0.25f) * fade * 0.9f;
            Color diskHot = new Color(1f, 0.75f, 0.45f) * fade * diskPulse;
            //柔光底
            Main.spriteBatch.Draw(glow, drawPos, null, diskGlow, 0f, glowOrigin,
                new Vector2(diskLen / glow.Width * 1.4f, 1.7f * scale), SpriteEffects.None, 0f);
            //中段红
            Main.spriteBatch.Draw(pixel, drawPos, null, diskCore, 0f, origin,
                new Vector2(diskLen, 8.4f * scale), SpriteEffects.None, 0f);
            //芯亮线
            Main.spriteBatch.Draw(pixel, drawPos, null, diskHot, 0f, origin,
                new Vector2(diskLen * 0.85f, 2.8f * scale), SpriteEffects.None, 0f);

            //红色核心竖缝：再放大一倍，并叠一层更细更亮的内芯
            Color slitDeep = new Color(0.95f, 0.10f, 0.18f) * fade * 0.9f;
            Color slitHot = new Color(1f, 0.55f, 0.45f) * fade * pulse;
            Color slitInner = new Color(1f, 0.85f, 0.65f) * fade * pulse * 0.85f;
            Main.spriteBatch.Draw(pixel, drawPos, null, slitDeep, 0f, origin,
                new Vector2(32f * scale, 360f * scale), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, drawPos, null, slitHot, 0f, origin,
                new Vector2(10f * scale, 320f * scale), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, drawPos, null, slitInner, 0f, origin,
                new Vector2(3.6f, 300f * scale), SpriteEffects.None, 0f);

            //横向抽搐短闪
            float jitter2 = MathF.Sin(t * 1.7f + coreSeed * 23f);
            if (jitter2 > 0.5f) {
                float intensity = jitter2 - 0.4f;
                Color cross = new Color(1f, 0.55f, 0.35f) * fade * intensity;
                Main.spriteBatch.Draw(pixel, drawPos, null, cross, 0f, origin,
                    new Vector2(240f * scale, 4.0f), SpriteEffects.None, 0f);
            }

            //中心暖白聚光：作为最高光，但用偏暖的 (1, .82, .55) 避开纯白
            Color centerHot = new Color(1f, 0.82f, 0.55f) * fade * pulse * 0.85f;
            Main.spriteBatch.Draw(glow, drawPos, null, centerHot, 0f, glowOrigin,
                new Vector2(1.7f * scale), SpriteEffects.None, 0f);
        }

        //炸裂闪带：奇点尾段一拍达峰的横+竖大十字白热闪
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

            //白热中心
            Color halo = new Color(1f, 0.7f, 0.5f) * pulse * 0.85f;
            Main.spriteBatch.Draw(glow, drawPos, null, halo, 0f, glowOrigin,
                new Vector2(MathHelper.Lerp(0.6f, 3.2f, pulse)), SpriteEffects.None, 0f);

            //横竖闪带
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

        //炸裂尘埃：奇点末→演出尾，向外扩散的红黑残片，作为"领域复原"的细节装饰
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

        //屏幕扭曲：奇点段强烈向心引力透镜、炸裂段外放冲击波，配合主着色器形成完整空间挤压感
        public void Warp() {
            if (crackAngles == null) return;//Init 尚未触发
            int t = CyberRestart.TotalFrames - Math.Max(0, Projectile.timeLeft - 4) + 1;
            if (t < CyberRestart.PhaseCollapseEnd - 6 || t > CyberRestart.PhaseBurstEnd + 4) return;

            Texture2D warpTex = CWRAsset.DiffusionCircle?.Value;
            if (warpTex == null) return;

            Vector2 origin = warpTex.Size() * 0.5f;
            //取"主人玩家"领域中心；远端客户端必须避免读 Local，否则扭曲透镜会跑到本地玩家身上
            CyberspacePlayer ownerCp = OwnerCp();
            Vector2 ownerCenter = ownerCp?.DomainCenter ?? Projectile.Center;
            Vector2 drawPos = ownerCenter - Main.screenPosition;

            //奇点向心扭曲：强度从收缩末段缓升、奇点段持续高位、炸裂瞬间归零
            float singWarp;
            if (t < CyberRestart.PhaseCollapseEnd - 6) {
                singWarp = 0f;
            }
            else if (t <= CyberRestart.PhaseSingularityEnd) {
                singWarp = MathHelper.Clamp((t - (CyberRestart.PhaseCollapseEnd - 6)) /
                    (float)(CyberRestart.PhaseSingularityEnd - (CyberRestart.PhaseCollapseEnd - 6)), 0f, 1f);
                //奇点段后半段加重
                singWarp = MathF.Pow(singWarp, 0.7f);
            }
            else {
                singWarp = 0f;
            }

            if (singWarp > 0f) {
                //深紫蓝偏向心：UV 通过 DiffusionCircle 整体收缩
                Color warpColor = new Color(40, 12, 60) * singWarp;
                float scale = MathHelper.Lerp(1.4f, 0.55f, singWarp);
                float ownerWarpTime = ownerCp?.EffectTime ?? Cyberspace.EffectTime;
                for (int i = 0; i < 3; i++) {
                    float rot = i * MathHelper.PiOver2 * 0.5f + ownerWarpTime * (0.4f + 0.2f * i);
                    Main.spriteBatch.Draw(warpTex, drawPos, null, warpColor, rot,
                        origin, scale, SpriteEffects.None, 0f);
                }
            }

            //炸裂冲击波：强度峰值在炸裂前 1/3，随后快速衰减
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
                    //外扩冲击波：透镜随阶段进度变大、染色色偏暖红
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
