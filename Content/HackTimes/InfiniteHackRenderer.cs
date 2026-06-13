using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    //无限骇入弹窗风暴渲染
    internal class InfiniteHackRenderer
    {
        #region 弹窗数据

        private class HackPopup
        {
            public QuickHackDef Hack;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public float Scale;
            public float Lifetime;
            public float MaxLifetime;
            public float UploadProgress;
            public float GlitchSeed;
            public float PopIn;
            public float Width;
        }

        #endregion

        #region 常量

        private const float BaseWidth = 300f;
        private const float BaseHeight = 52f;
        private const float SlashWidth = 10f;
        private const float BarHeight = 3f;
        //生命周期
        private const float PopupLifetimeBase = 0.7f;
        private const float PopupLifetimeRand = 0.4f;
        //上传填充速度(每秒)
        private const float UploadFillSpeed = 3.5f;
        //弹入动画速度(每秒)
        private const float PopInSpeed = 12f;
        //生成间隔
        private const float InitialSpawnInterval = 0.10f;
        private const float MinSpawnInterval = 0.022f;
        //加速时间(秒)
        private const float RampDuration = 3.5f;
        //最大弹窗数
        private const int MaxPopups = 45;
        //生成区域扩张时间(秒)
        private const float ZoneExpandDuration = 2.5f;
        //故障撕裂带数
        private const int GlitchTearCount = 5;
        //蓄力时长(秒)
        private const float ChargeDuration = 1.2f;

        #endregion

        #region 状态

        private enum StormPhase { Idle, Charging, Storming }

        private readonly List<HackPopup> popups = new();
        private float timer;
        private float spawnTimer;
        private float modeActiveTime;
        //蓄力/风暴状态机
        private StormPhase phase = StormPhase.Idle;
        private float chargeProgress;
        //故障撕裂带
        private readonly float[] tearY = new float[GlitchTearCount];
        private readonly float[] tearSeed = new float[GlitchTearCount];
        private readonly float[] tearLife = new float[GlitchTearCount];

        //当前是否正在蓄力或风暴中
        public bool IsActive => phase != StormPhase.Idle;

        #endregion

        #region 公共接口

        //点击协议后触发蓄力
        public void BeginCharge() {
            if (phase != StormPhase.Idle) return;
            phase = StormPhase.Charging;
            chargeProgress = 0f;
        }

        public void Update() {
            timer += 0.016f;

            bool modeOn = HackTime.InfiniteHack && HackTime.SelectedTargetIndex >= 0;

            //模式关闭时重置状态机
            if (!modeOn && phase != StormPhase.Idle) {
                phase = StormPhase.Idle;
                chargeProgress = 0f;
            }

            switch (phase) {
                case StormPhase.Charging:
                    chargeProgress += 0.016f / ChargeDuration;
                    if (chargeProgress >= 1f) {
                        chargeProgress = 1f;
                        phase = StormPhase.Storming;
                        modeActiveTime = 0f;
                    }
                    break;

                case StormPhase.Storming:
                    modeActiveTime += 0.016f;

                    //动态生成间隔(指数加速)
                    float ramp = Math.Min(modeActiveTime / RampDuration, 1f);
                    float interval = MathHelper.Lerp(InitialSpawnInterval, MinSpawnInterval, ramp * ramp);

                    spawnTimer -= 0.016f;
                    while (spawnTimer <= 0f && popups.Count < MaxPopups) {
                        SpawnPopup();
                        spawnTimer += interval;
                    }

                    //更新故障撕裂带
                    UpdateTears();
                    break;

                default:
                    //Idle时自然回收存量弹窗
                    if (popups.Count == 0 && modeActiveTime > 0f)
                        modeActiveTime = 0f;
                    spawnTimer = 0f;
                    //衰减残留撕裂带，避免冻结在屏幕上
                    for (int i = 0; i < GlitchTearCount; i++)
                        tearLife[i] = Math.Max(tearLife[i] - 0.016f, 0f);
                    break;
            }

            //更新所有弹窗
            for (int i = popups.Count - 1; i >= 0; i--) {
                var p = popups[i];
                p.Lifetime -= 0.016f;
                p.UploadProgress = Math.Min(p.UploadProgress + UploadFillSpeed * 0.016f, 1f);
                p.PopIn = Math.Min(p.PopIn + PopInSpeed * 0.016f, 1f);
                p.Position += p.Velocity * 0.016f;
                //轻微随机漂移
                p.Velocity *= 0.97f;

                if (p.Lifetime <= 0f)
                    popups.RemoveAt(i);
            }
        }

        public void Draw(SpriteBatch sb) {
            if (phase == StormPhase.Idle && popups.Count == 0) return;

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;
            float alpha = HackTime.Intensity;
            if (alpha < 0.01f) return;

            //蓄力阶段绘制
            if (phase == StormPhase.Charging) {
                DrawChargeBar(sb, px, alpha);
                return;
            }

            //风暴阶段
            //全局故障撕裂带(底层)
            DrawGlitchTears(sb, px, alpha);

            //标题
            DrawHeader(sb, px, alpha);

            //所有弹窗
            for (int i = 0; i < popups.Count; i++)
                DrawPopup(sb, px, alpha, popups[i]);

            //静电噪声叠加(顶层)
            DrawStaticNoise(sb, px, alpha);
        }

        public void Clear() {
            popups.Clear();
            modeActiveTime = 0f;
            spawnTimer = 0f;
            phase = StormPhase.Idle;
            chargeProgress = 0f;
            for (int i = 0; i < GlitchTearCount; i++)
                tearLife[i] = 0f;
        }

        #endregion

        #region 生成逻辑

        private void SpawnPopup() {
            var all = QuickHackDef.Instances;
            var hack = all[Main.rand.Next(all.Count)];

            //生成区域随时间从队列区扩散到整个左半屏
            float zoneExpand = Math.Min(modeActiveTime / ZoneExpandDuration, 1f);
            //初始区域：左侧36px、垂直居中±200px
            //最终区域：左半屏0~50%宽度、整个高度
            float maxX = MathHelper.Lerp(320f, Main.screenWidth * 0.50f, zoneExpand);
            float minX = 20f;
            float centerY = Main.screenHeight * 0.5f;
            float halfRangeY = MathHelper.Lerp(180f, Main.screenHeight * 0.46f, zoneExpand);

            float x = minX + Main.rand.NextFloat() * (maxX - minX);
            float y = centerY + (Main.rand.NextFloat() - 0.5f) * 2f * halfRangeY;
            y = Math.Clamp(y, 30f, Main.screenHeight - BaseHeight - 10f);

            float widthMult = 0.85f + Main.rand.NextFloat() * 0.35f;
            float life = PopupLifetimeBase + Main.rand.NextFloat() * PopupLifetimeRand;

            popups.Add(new HackPopup {
                Hack = hack,
                Position = new Vector2(x, y),
                Velocity = new Vector2(
                    (Main.rand.NextFloat() - 0.5f) * 60f,
                    (Main.rand.NextFloat() - 0.5f) * 30f),
                Rotation = (Main.rand.NextFloat() - 0.5f) * 0.10f,
                Scale = 0.70f + Main.rand.NextFloat() * 0.40f,
                Lifetime = life,
                MaxLifetime = life,
                UploadProgress = 0f,
                GlitchSeed = Main.rand.NextFloat() * 100f,
                PopIn = 0f,
                Width = BaseWidth * widthMult,
            });
        }

        #endregion

        #region 故障撕裂带

        private void UpdateTears() {
            for (int i = 0; i < GlitchTearCount; i++) {
                tearLife[i] -= 0.016f;
                if (tearLife[i] <= 0f && Main.rand.NextFloat() < 0.02f + modeActiveTime * 0.005f) {
                    tearY[i] = Main.rand.NextFloat() * Main.screenHeight;
                    tearSeed[i] = Main.rand.NextFloat() * 100f;
                    tearLife[i] = 0.05f + Main.rand.NextFloat() * 0.12f;
                }
            }
        }

        private void DrawGlitchTears(SpriteBatch sb, Texture2D px, float alpha) {
            for (int i = 0; i < GlitchTearCount; i++) {
                if (tearLife[i] <= 0f) continue;
                float intensity = Math.Min(tearLife[i] * 10f, 1f);
                float bandH = 2f + MathF.Sin(tearSeed[i] * 30f + timer * 50f) * 3f;
                float maxX = MathHelper.Lerp(320f, Main.screenWidth * 0.55f,
                    Math.Min(modeActiveTime / ZoneExpandDuration, 1f));

                //青色主带
                sb.Draw(px, new Rectangle(0, (int)tearY[i], (int)maxX, (int)Math.Max(bandH, 1f)),
                    new Rectangle(0, 0, 1, 1), HackTheme.Accent * (alpha * 0.18f * intensity));
                //红色偏移伪影
                sb.Draw(px, new Rectangle(3, (int)(tearY[i] + 1), (int)maxX, (int)Math.Max(bandH * 0.5f, 1f)),
                    new Rectangle(0, 0, 1, 1), HackTheme.Danger * (alpha * 0.12f * intensity));
            }
        }

        #endregion

        #region 弹窗绘制

        private void DrawPopup(SpriteBatch sb, Texture2D px, float globalAlpha, HackPopup p) {
            if (p.PopIn < 0.01f) return;

            //弹入缩放(过冲)
            float popScale = EaseOutBack(Math.Min(p.PopIn, 1f));
            float scale = p.Scale * popScale;
            //淡出阶段
            float lifeFrac = Math.Clamp(p.Lifetime / p.MaxLifetime, 0f, 1f);
            float fadeAlpha = lifeFrac < 0.2f ? lifeFrac / 0.2f : 1f;
            float alpha = globalAlpha * fadeAlpha;
            if (alpha < 0.01f) return;

            float w = p.Width * scale;
            float h = BaseHeight * scale;
            int ix = (int)p.Position.X;
            int iy = (int)p.Position.Y;
            Rectangle rect = new(ix, iy, (int)w, (int)h);

            //故障抖动(生命末尾加剧)
            float glitchIntensity = (1f - lifeFrac) * 8f;
            float glitchX = MathF.Sin(timer * 30f + p.GlitchSeed * 5f) * glitchIntensity;
            rect.X += (int)glitchX;

            //色散偏移
            float aberration = (1f - lifeFrac) * 2.5f + 0.5f;

            //=== 背景 ===
            float rPulse = MathF.Sin(timer * 16f + p.GlitchSeed * 4f) * 0.2f + 0.8f;
            Color bgColor = Color.Lerp(HackTheme.BgSlot, HackTheme.Danger, 0.10f * rPulse);
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), bgColor * (alpha * 0.90f));

            //CRT暗纹
            Color crtLine = HackTheme.BgDarkest * (alpha * 0.06f);
            for (int dy = 0; dy < rect.Height; dy += 3)
                sb.Draw(px, new Rectangle(rect.X, rect.Y + dy, rect.Width, 1),
                    new Rectangle(0, 0, 1, 1), crtLine);

            //=== 左侧色条(红色闪烁) ===
            float rFlicker = MathF.Sin(timer * 18f + p.GlitchSeed * 3f) * 0.3f + 0.7f;
            Color barColor = Color.Lerp(HackTheme.Danger, new Color(255, 60, 30), rFlicker * 0.4f);
            sb.Draw(px, new Rectangle(rect.X + 2, rect.Y + 2, 3, rect.Height - 4),
                new Rectangle(0, 0, 1, 1), barColor * (alpha * rFlicker));

            //=== 协议名称(带色散) ===
            float fontScale = 0.50f * scale;
            float nameX = rect.X + 10;
            float nameY = rect.Y + 4 * scale;
            Color nameColor = Color.Lerp(HackTheme.TextBright, HackTheme.Danger, 0.4f * rPulse);

            //色散副本
            if (aberration > 0.3f) {
                Color redGhost = new Color(220, 30, 30) * (alpha * 0.22f);
                Color blueGhost = new Color(30, 60, 220) * (alpha * 0.18f);
                Utils.DrawBorderString(sb, p.Hack.Name,
                    new Vector2(nameX - aberration, nameY), redGhost, fontScale);
                Utils.DrawBorderString(sb, p.Hack.Name,
                    new Vector2(nameX + aberration, nameY + 0.5f), blueGhost, fontScale);
            }
            Utils.DrawBorderString(sb, p.Hack.Name,
                new Vector2(nameX, nameY), nameColor * alpha, fontScale);

            //=== 进度条 ===
            int barY2 = rect.Bottom - (int)(BarHeight * scale) - 3;
            int barX2 = rect.X + 10;
            int barW = rect.Width - 14;
            if (barW > 4 && barY2 > rect.Y) {
                sb.Draw(px, new Rectangle(barX2, barY2, barW, (int)Math.Max(BarHeight * scale, 1f)),
                    new Rectangle(0, 0, 1, 1), HackTheme.ProgressBg * (alpha * 0.5f));
                int fillW = (int)(barW * p.UploadProgress);
                if (fillW > 0) {
                    Color fillColor = p.UploadProgress >= 1f ? HackTheme.Danger : HackTheme.Uploading;
                    sb.Draw(px, new Rectangle(barX2, barY2, fillW, (int)Math.Max(BarHeight * scale, 1f)),
                        new Rectangle(0, 0, 1, 1), fillColor * (alpha * 0.8f));
                }
            }

            //=== 百分比/完成 ===
            string statusText;
            Color statusColor;
            if (p.UploadProgress >= 1f) {
                float flash = MathF.Sin(timer * 14f + p.GlitchSeed) * 0.3f + 0.7f;
                statusText = HackTime.Breach.Value;
                statusColor = HackTheme.Danger * flash;
            }
            else {
                statusText = $"{(int)(p.UploadProgress * 100)}%";
                statusColor = HackTheme.Uploading;
            }
            float statusScale = 0.30f * scale;
            Vector2 stSize = FontAssets.MouseText.Value.MeasureString(statusText) * statusScale;
            Utils.DrawBorderString(sb, statusText,
                new Vector2(rect.Right - stSize.X - 6, rect.Y + 4 * scale),
                statusColor * alpha, statusScale);

            //=== 边框(红色闪烁) ===
            float rB = MathF.Sin(timer * 22f + p.GlitchSeed * 6f) * 0.25f + 0.75f;
            Color borderCol = Color.Lerp(HackTheme.Border, HackTheme.Danger, 0.5f * rB);
            Color bc = borderCol * (alpha * 0.45f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1), bc);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1), bc);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), bc);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), bc);

            //=== 完成时底部红色光带 ===
            if (p.UploadProgress >= 1f) {
                float completedPulse = MathF.Sin(timer * 10f + p.GlitchSeed * 2f) * 0.3f + 0.7f;
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2),
                    new Rectangle(0, 0, 1, 1), HackTheme.Danger * (alpha * 0.5f * completedPulse));
            }
        }

        #endregion

        #region 蓄力条

        private void DrawChargeBar(SpriteBatch sb, Texture2D px, float alpha) {
            float t = chargeProgress;
            float centerY = Main.screenHeight * 0.5f;
            float barX = 36f;
            float barW = 260f;
            float barH = 10f;

            //标题闪烁渐显
            float titleAlpha = Math.Min(t * 4f, 1f);
            float glitch = MathF.Sin(timer * 22f) * (1f + t * 3f);
            string title = HackTime.InitBreach.Value;
            Color titleColor = Color.Lerp(HackTheme.Accent, HackTheme.Danger, t) * (alpha * titleAlpha * 0.65f);
            Utils.DrawBorderString(sb, title,
                new Vector2(barX + glitch, centerY - 40f), titleColor, 0.36f);

            //进度条背景
            sb.Draw(px, new Rectangle((int)barX, (int)(centerY - barH * 0.5f), (int)barW, (int)barH),
                new Rectangle(0, 0, 1, 1), HackTheme.ProgressBg * (alpha * 0.7f));

            //进度条填充
            int fillW = (int)(barW * t);
            if (fillW > 0) {
                float pulse = MathF.Sin(timer * 12f) * 0.15f + 0.85f;
                Color fillColor = Color.Lerp(HackTheme.Accent, HackTheme.Danger, t * 0.8f) * pulse;
                sb.Draw(px, new Rectangle((int)barX, (int)(centerY - barH * 0.5f), fillW, (int)barH),
                    new Rectangle(0, 0, 1, 1), fillColor * (alpha * 0.9f));

                //进度前端辉光
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null && fillW > 4) {
                    Color tipGlow = Color.Lerp(HackTheme.Accent, HackTheme.Danger, t) * (alpha * 0.5f);
                    tipGlow.A = 0;
                    sb.Draw(glow, new Vector2(barX + fillW, centerY), null,
                        tipGlow, 0, glow.Size() / 2, new Vector2(0.15f, 0.05f), SpriteEffects.None, 0);
                }

                //高光线
                sb.Draw(px, new Rectangle((int)barX, (int)(centerY - barH * 0.5f), fillW, 1),
                    new Rectangle(0, 0, 1, 1), HackTheme.TextBright * (alpha * 0.15f));
            }

            //进度条边框
            Color borderCol = Color.Lerp(HackTheme.Border, HackTheme.Danger, t * 0.5f) * (alpha * 0.55f);
            sb.Draw(px, new Rectangle((int)barX, (int)(centerY - barH * 0.5f), (int)barW, 1), new Rectangle(0, 0, 1, 1), borderCol);
            sb.Draw(px, new Rectangle((int)barX, (int)(centerY + barH * 0.5f), (int)barW, 1), new Rectangle(0, 0, 1, 1), borderCol);
            sb.Draw(px, new Rectangle((int)barX, (int)(centerY - barH * 0.5f), 1, (int)barH), new Rectangle(0, 0, 1, 1), borderCol);
            sb.Draw(px, new Rectangle((int)(barX + barW) - 1, (int)(centerY - barH * 0.5f), 1, (int)barH), new Rectangle(0, 0, 1, 1), borderCol);

            //百分比
            string pctStr = $"{(int)(t * 100)}%";
            Vector2 pctSize = FontAssets.MouseText.Value.MeasureString(pctStr) * 0.34f;
            Utils.DrawBorderString(sb, pctStr,
                new Vector2(barX + barW + 10, centerY - pctSize.Y * 0.5f),
                Color.Lerp(HackTheme.Accent, HackTheme.Danger, t) * (alpha * 0.7f), 0.34f);

            //CRT扫描线覆盖进度条区域
            Color crtLine = HackTheme.BgDarkest * (alpha * 0.04f);
            for (int dy = 0; dy < (int)barH; dy += 2)
                sb.Draw(px, new Rectangle((int)barX, (int)(centerY - barH * 0.5f) + dy, (int)barW, 1),
                    new Rectangle(0, 0, 1, 1), crtLine);

            //临近完成时屏幕边缘闪烁
            if (t > 0.7f) {
                float edgePulse = MathF.Sin(timer * 16f) * 0.5f + 0.5f;
                float edgeAlpha = (t - 0.7f) / 0.3f * edgePulse;
                Color edgeColor = HackTheme.Danger * (alpha * edgeAlpha * 0.08f);
                //左侧竖条
                sb.Draw(px, new Rectangle(0, 0, 3, Main.screenHeight),
                    new Rectangle(0, 0, 1, 1), edgeColor);
            }
        }

        #endregion

        #region 全局效果

        private void DrawHeader(SpriteBatch sb, Texture2D px, float alpha) {
            if (modeActiveTime < 0.1f) return;
            float headerAlpha = Math.Min(modeActiveTime * 2f, 1f);
            float glitch = MathF.Sin(timer * 25f) * 2f;

            string header = HackTime.SystemBreach.Value;
            Color headerColor = HackTheme.Danger * (alpha * headerAlpha * 0.7f);

            //闪烁间歇可见
            float blink = MathF.Sin(timer * 8f + MathF.Sin(timer * 3f) * 2f);
            if (blink > -0.2f) {
                Utils.DrawBorderString(sb, header,
                    new Vector2(36f + glitch, 80f), headerColor, 0.40f);
            }

            //计数
            string countStr = $"[{popups.Count} ACTIVE]";
            float countBlink = MathF.Sin(timer * 6f) * 0.3f + 0.7f;
            Utils.DrawBorderString(sb, countStr,
                new Vector2(36f, 106f),
                HackTheme.Danger * (alpha * headerAlpha * 0.35f * countBlink), 0.28f);

            //警告横条
            if (modeActiveTime > 1.5f) {
                float warnPulse = MathF.Sin(timer * 4f) * 0.5f + 0.5f;
                float maxX = MathHelper.Lerp(320f, Main.screenWidth * 0.50f,
                    Math.Min(modeActiveTime / ZoneExpandDuration, 1f));
                sb.Draw(px,
                    new Rectangle(0, 74, (int)maxX, 1),
                    new Rectangle(0, 0, 1, 1), HackTheme.Danger * (alpha * 0.25f * warnPulse));
                sb.Draw(px,
                    new Rectangle(0, 120, (int)maxX, 1),
                    new Rectangle(0, 0, 1, 1), HackTheme.Danger * (alpha * 0.15f * warnPulse));
            }
        }

        private void DrawStaticNoise(SpriteBatch sb, Texture2D px, float alpha) {
            if (modeActiveTime < 0.5f) return;

            //越来越密集的静电噪点
            float density = Math.Min(modeActiveTime * 0.3f, 1f);
            int patchCount = (int)(12 * density);
            float noiseAlpha = alpha * 0.04f * density;

            for (int i = 0; i < patchCount; i++) {
                float seed = i * 73.37f + timer * 40f;
                float px2 = (MathF.Sin(seed) + 1f) * 0.5f * Main.screenWidth * 0.5f;
                float py = (MathF.Sin(seed * 1.7f + 29f) + 1f) * 0.5f * Main.screenHeight;
                float pw = 4f + MathF.Sin(seed * 2.3f) * 20f;
                float ph = 1f + MathF.Sin(seed * 3.1f) * 2f;

                if (pw > 0f && ph > 0f) {
                    sb.Draw(px, new Rectangle((int)px2, (int)py, (int)pw, (int)Math.Max(ph, 1f)),
                        new Rectangle(0, 0, 1, 1), HackTheme.Accent * noiseAlpha);
                }
            }
        }

        #endregion

        #region 工具

        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float inv = t - 1f;
            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }

        #endregion
    }
}
