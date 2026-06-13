using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas
{
    /// <summary>
    /// 领域之眼图鉴视图：九眼轨道 + 中心核，按激活序计层
    /// 第十眼需九眼全开且满足时代唯一条件
    /// 文案沿用旧 DomainUI 本地化键
    /// </summary>
    internal class AtlasDomainEyes
    {
        private const string LegacyKey = "Mods.CalamityOverhaul.Legend.HalibutText.DomainUI.";
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText ExtraEyeTitleText { get; private set; }
        public static LocalizedText CrashedLabelText { get; private set; }
        public static LocalizedText LayerTitleFormat { get; private set; }
        private static readonly LocalizedText[] eyeLayerDescriptions = new LocalizedText[11];

        /// <summary>
        /// 注册本地化（由 <see cref="HalibutAtlas.SetStaticDefaults"/> 调用一次）
        /// </summary>
        public static void RegisterLocalization() {
            TitleText = Language.GetOrRegister(LegacyKey + "TitleText", () => "海洋领域");
            ExtraEyeTitleText = Language.GetOrRegister(LegacyKey + "ExtraEyeTitleText", () => "第 十 层");
            CrashedLabelText = Language.GetOrRegister(LegacyKey + "CrashedLabelText", () => "已死机");
            LayerTitleFormat = Language.GetOrRegister(LegacyKey + "LayerTitleFormat", () => "第 {0} 层");
            eyeLayerDescriptions[1] = Language.GetOrRegister(LegacyKey + "EyeDesc1", () => "初启领域之眼，微弱的潮汐感开始共鸣");
            eyeLayerDescriptions[2] = Language.GetOrRegister(LegacyKey + "EyeDesc2", () => "双目同开，水压在周遭缓慢聚集，力量渐显");
            eyeLayerDescriptions[3] = Language.GetOrRegister(LegacyKey + "EyeDesc3", () => "三重视界锁定海流，领域开始稳定成型");
            eyeLayerDescriptions[4] = Language.GetOrRegister(LegacyKey + "EyeDesc4", () => "第四层共鸣放大，涌动的寒意悄然扩散");
            eyeLayerDescriptions[5] = Language.GetOrRegister(LegacyKey + "EyeDesc5", () => "五层交织，环形水旋于脚下成形，给予守护");
            eyeLayerDescriptions[6] = Language.GetOrRegister(LegacyKey + "EyeDesc6", () => "第六层脉冲涌现，能量脉络变得清晰可辨");
            eyeLayerDescriptions[7] = Language.GetOrRegister(LegacyKey + "EyeDesc7", () => "七眼同辉，潮域对外界的侵蚀性显著增强");
            eyeLayerDescriptions[8] = Language.GetOrRegister(LegacyKey + "EyeDesc8", () => "第八层使水压几近凝实，力量几乎到达巅峰");
            eyeLayerDescriptions[9] = Language.GetOrRegister(LegacyKey + "EyeDesc9", () => "九层极境，海渊之形完全显现，伟力贯通");
            eyeLayerDescriptions[10] = Language.GetOrRegister(LegacyKey + "EyeDesc10", () => "十层无限叠加，神之境界");
        }

        //几何
        private const float EyeOrbitRadius = 132f;
        private const float EyeSize = 17f;
        private const float ExtraEyeSize = 23f;
        private const float HalibutSize = 56f;

        //每只外圈眼的视图状态
        private readonly float[] eyeHover = new float[HalibutSave.MaxEyes];
        private readonly float[] eyeOpenAmount = new float[HalibutSave.MaxEyes];
        private readonly float[] eyeBlink = new float[HalibutSave.MaxEyes];
        private float extraHover;
        private float extraOpenAmount;
        //中心鱼旋转
        private float halibutRotation;
        //锁定表现
        private float lockOverlay;
        private float lockShake;
        private int lastLockSecond = -1;
        private float countdownScale = 1f;
        //粒子
        private readonly HalibutUIParticlePool particles = new(80);
        //悬停目标缓存（绘制tooltip用）
        private int hoveredEyeIndex = -1;
        private bool hoveredExtra;

        public void Update(Rectangle contentArea, HalibutSave save, float alpha, bool inputAvailable) {
            particles.Update();
            halibutRotation += 0.005f;
            if (halibutRotation > MathHelper.TwoPi) {
                halibutRotation -= MathHelper.TwoPi;
            }

            Vector2 center = contentArea.Center.ToVector2();
            Player plr = Main.LocalPlayer;
            plr.TryGetOverride<HalibutPlayer>(out var hp);
            bool locked = hp != null && hp.IsInteractionLockedTime > 0;
            lockOverlay = MathHelper.Lerp(lockOverlay, locked ? 1f : 0f, locked ? 0.1f : 0.14f);
            if (lockShake > 0.02f) {
                lockShake *= 0.86f;
            }

            //锁定倒计时秒变动画
            if (locked) {
                int second = (int)MathF.Ceiling(hp.IsInteractionLockedTime / 60f);
                if (second != lastLockSecond && second > 0) {
                    lastLockSecond = second;
                    countdownScale = 1.5f;
                    if (second <= 3) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = 0.3f });
                    }
                }
                countdownScale = MathHelper.Lerp(countdownScale, 1f, 0.15f);
            }
            else {
                lastLockSecond = -1;
                countdownScale = 1f;
            }

            hoveredEyeIndex = -1;
            hoveredExtra = false;
            Vector2 mouse = Main.MouseScreen;

            //外圈九眼
            for (int i = 0; i < save.eyes.Count && i < HalibutSave.MaxEyes; i++) {
                SeaEyeState eye = save.eyes[i];
                Vector2 pos = EyePos(center, eye);
                bool hovered = inputAvailable && Vector2.Distance(mouse, pos) < EyeSize + 6f;
                if (hovered) {
                    hoveredEyeIndex = i;
                }
                eyeHover[i] = MathHelper.Lerp(eyeHover[i], hovered ? 1f : 0f, 0.18f);
                float openTarget = eye.IsActive ? 1f : 0.12f + eyeHover[i] * 0.25f;
                if (eyeBlink[i] > 0f) {
                    eyeBlink[i] -= 1f;
                    openTarget *= eyeBlink[i] % 10 < 5 ? 0.1f : 1f;
                }
                eyeOpenAmount[i] = MathHelper.Lerp(eyeOpenAmount[i], openTarget, 0.2f);

                if (hovered && Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    if (locked) {
                        TriggerLockedFeedback(mouse);
                    }
                    else {
                        ToggleEye(save, eye, pos, center);
                    }
                }
            }

            //第十眼
            bool canShowExtra = save.activationSequence.Count >= 9 && HalibutPlayer.TheOnlyBornOfAnEra();
            extraOpenAmount = MathHelper.Lerp(extraOpenAmount,
                !canShowExtra ? 0f : save.ExtraEyeActive ? 1f : 0.15f + extraHover * 0.25f, 0.18f);
            if (canShowExtra) {
                bool hovered = inputAvailable && Vector2.Distance(mouse, center) < ExtraEyeSize + 7f;
                hoveredExtra = hovered;
                extraHover = MathHelper.Lerp(extraHover, hovered ? 1f : 0f, 0.18f);
                if (hovered && Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    if (locked) {
                        TriggerLockedFeedback(mouse);
                    }
                    else {
                        save.ToggleExtraEye();
                        PlayToggleSound(save.ExtraEyeActive, false);
                        if (save.ExtraEyeActive) {
                            particles.SpawnRingPulse(center, HalibutTheme.GlowHi, 90f, 4f);
                            particles.SpawnBurst(center, HalibutTheme.GlowHi, 14, 3.4f);
                        }
                    }
                }
            }
            else {
                extraHover = MathHelper.Lerp(extraHover, 0f, 0.2f);
            }
        }

        private void ToggleEye(HalibutSave save, SeaEyeState eye, Vector2 eyePos, Vector2 center) {
            bool wasActive = eye.IsActive;
            save.ToggleEye(eye);
            eyeBlink[eye.Index] = 15f;
            bool crashed = eye.IsCrashedState(Main.LocalPlayer);
            PlayToggleSound(!wasActive, crashed);
            Color burst = crashed ? HalibutTheme.Danger
                : !wasActive ? HalibutTheme.Glow : HalibutTheme.Disabled;
            particles.SpawnBurst(eyePos, burst, 12, 3.2f);
            if (!wasActive) {
                //激活：光粒飞向中心 + 抵达脉冲
                particles.SpawnFlyingMote(eyePos, center,
                    () => particles.SpawnRingPulse(center, HalibutTheme.Glow, 70f, 3f));
            }
        }

        private static void PlayToggleSound(bool activating, bool crashed) {
            SoundEngine.PlaySound(SoundID.MenuTick);
            if (activating) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.45f, Pitch = crashed ? -0.4f : -0.1f });
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = crashed ? -0.3f : 0.1f });
            }
            else {
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.3f, Pitch = 0.1f });
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.25f, Pitch = -0.4f });
            }
        }

        private void TriggerLockedFeedback(Vector2 mouse) {
            lockShake = 1f;
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f, Pitch = -0.5f });
            particles.SpawnBurst(mouse, HalibutTheme.Danger, 8, 3f);
        }

        private static Vector2 EyePos(Vector2 center, SeaEyeState eye) {
            float wave = MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + eye.Index * 0.3f) * 3f;
            return center + HalibutRenderer.AngleDir(eye.Angle) * (EyeOrbitRadius + wave);
        }

        public void Draw(SpriteBatch sb, Rectangle contentArea, HalibutSave save, float alpha) {
            float time = Main.GlobalTimeWrappedHourly;
            Vector2 center = contentArea.Center.ToVector2();
            if (lockShake > 0.02f) {
                center += new Vector2(MathF.Sin(time * 40f) * lockShake * 5f, 0f);
            }
            Player plr = Main.LocalPlayer;
            plr.TryGetOverride<HalibutPlayer>(out var hp);
            int crashLevel = hp?.CrashesLevel() ?? 0;
            int activeCount = save.ActiveEyeCount;

            //标题
            HalibutRenderer.DrawGlowTextCentered(sb, TitleText.Value,
                new Vector2(center.X, contentArea.Y + 26f),
                HalibutTheme.Text * alpha, HalibutTheme.Glow * (0.4f * alpha), 1f);

            //层数同心环（每层一圈，带波动的旋转圆）
            for (int i = 0; i < activeCount; i++) {
                float radius = 40f + i * 11f;
                float rot = time * (0.12f + i * 0.02f);
                Color ringCol = Color.Lerp(HalibutTheme.Glow, HalibutTheme.GlowHi, i / 9f);
                DrawWavyRing(sb, center, radius, rot, ringCol * (0.34f * alpha), i);
            }

            //连接丝线：中心到每只激活眼
            foreach (SeaEyeState eye in save.activationSequence) {
                if (!eye.IsActive) {
                    continue;
                }
                Vector2 eyePos = EyePos(center, eye);
                bool crashed = (eye.LayerNumber ?? 1) <= crashLevel;
                float wave = MathF.Sin(time * 3f + eye.Index * 0.5f) * 0.5f + 0.5f;
                Color lineCol = crashed
                    ? Color.Lerp(HalibutTheme.DangerDim, HalibutTheme.Danger, wave)
                    : Color.Lerp(HalibutTheme.Glow, HalibutTheme.GlowHi, wave);
                HalibutRenderer.DrawGlowLine(sb, center, eyePos, 1f + wave * 0.8f, lineCol * (0.35f * alpha));
            }

            //中心核
            DrawHalibutCore(sb, center, activeCount, alpha, time);

            //外圈九眼
            for (int i = 0; i < save.eyes.Count && i < HalibutSave.MaxEyes; i++) {
                SeaEyeState eye = save.eyes[i];
                Vector2 pos = EyePos(center, eye);
                bool crashed = eye.IsActive
                    ? (eye.LayerNumber ?? 1) <= crashLevel
                    : NextLayerNumber(save) <= crashLevel;
                Color iris = crashed ? HalibutTheme.Danger : HalibutTheme.Glow;
                float scale = 1f + eyeHover[i] * 0.2f;
                //激活辉光
                if (eye.IsActive) {
                    HalibutRenderer.DrawSoftGlow(sb, pos, 26f, iris * (0.4f * alpha));
                }
                HalibutRenderer.DrawEye(sb, pos, EyeSize * scale, eyeOpenAmount[i], iris,
                    alpha * (eye.IsActive ? 1f : 0.62f), time + i);
                //层数标记
                if (eye.IsActive && eye.LayerNumber.HasValue) {
                    HalibutRenderer.DrawGlowTextCentered(sb, eye.LayerNumber.Value.ToString(),
                        pos + new Vector2(0f, EyeSize + 11f),
                        (crashed ? HalibutTheme.Danger : HalibutTheme.Text) * (0.85f * alpha),
                        HalibutTheme.Deep * (0.4f * alpha), 0.72f);
                }
            }

            //第十眼（中心上方浮现）
            bool canShowExtra = save.activationSequence.Count >= 9 && HalibutPlayer.TheOnlyBornOfAnEra();
            if (canShowExtra || extraOpenAmount > 0.05f) {
                bool extraCrashed = 10 <= crashLevel;
                Color extraIris = extraCrashed ? HalibutTheme.Danger : HalibutTheme.GlowHi;
                if (save.ExtraEyeActive) {
                    float pulse = HalibutTheme.Breath(time, 9f, 4f);
                    HalibutRenderer.DrawSoftGlow(sb, center, 40f + pulse * 8f, extraIris * (0.5f * alpha));
                }
                HalibutRenderer.DrawEye(sb, center, ExtraEyeSize * (1f + extraHover * 0.18f),
                    extraOpenAmount, extraIris, alpha, time * 1.3f);
            }

            particles.Draw(sb, alpha);

            //锁定覆盖层
            if (lockOverlay > 0.02f && hp != null) {
                DrawLockOverlay(sb, contentArea, center, hp, alpha * lockOverlay, time);
            }

            //眼睛悬浮说明
            if (hoveredEyeIndex >= 0 && hoveredEyeIndex < save.eyes.Count) {
                SeaEyeState eye = save.eyes[hoveredEyeIndex];
                int displayLayer = eye.LayerNumber ?? NextLayerNumber(save);
                displayLayer = Math.Clamp(displayLayer, 1, 10);
                bool crashed = displayLayer <= crashLevel;
                string title = string.Format(LayerTitleFormat.Value, GetLayerNumeralText(displayLayer));
                HalibutRenderer.DrawCursorPanel(sb, Main.MouseScreen, title,
                    crashed ? HalibutTheme.Danger : HalibutTheme.GlowHi, GetDescription(displayLayer), alpha,
                    crashed ? CrashedLabelText.Value : null, HalibutTheme.Danger);
            }
            else if (hoveredExtra) {
                bool crashed = 10 <= crashLevel;
                HalibutRenderer.DrawCursorPanel(sb, Main.MouseScreen, ExtraEyeTitleText.Value,
                    crashed ? HalibutTheme.Danger : HalibutTheme.Accent, GetDescription(10), alpha,
                    crashed ? CrashedLabelText.Value : null, HalibutTheme.Danger);
            }
        }

        private static int NextLayerNumber(HalibutSave save) {
            int count = 0;
            foreach (var eye in save.activationSequence) {
                if (eye.IsActive) {
                    count++;
                }
            }
            return count + 1;
        }

        private void DrawHalibutCore(SpriteBatch sb, Vector2 center, int activeCount, float alpha, float time) {
            Main.instance.LoadItem(HalibutOverride.ID);
            Texture2D tex = TextureAssets.Item[HalibutOverride.ID].Value;
            float pulse = MathF.Sin(time * 2f) * 0.1f + 0.9f;
            float scale = HalibutSize / tex.Width * pulse;
            //双层辉光
            for (int i = 0; i < 2; i++) {
                Color glow = Color.Lerp(HalibutTheme.Glow, HalibutTheme.Teal, i / 2f)
                    * ((0.3f - i * 0.1f) * alpha);
                sb.Draw(tex, center, null, glow with { A = 0 }, halibutRotation + i * 0.1f,
                    tex.Size() * 0.5f, scale * (1.2f + i * 0.15f), SpriteEffects.None, 0f);
            }
            sb.Draw(tex, center, null, Color.White * alpha, halibutRotation,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            //层数
            if (activeCount > 0) {
                HalibutRenderer.DrawGlowTextCentered(sb, activeCount.ToString(),
                    center + new Vector2(0f, HalibutSize * 0.62f),
                    HalibutTheme.Text * alpha, HalibutTheme.Accent * (0.5f * alpha), 1f);
            }
        }

        private static void DrawWavyRing(SpriteBatch sb, Vector2 center, float radius, float rotation,
            Color color, int layerIndex) {
            int segments = 44;
            float step = MathHelper.TwoPi / segments;
            float time = Main.GlobalTimeWrappedHourly;
            Vector2 prev = Vector2.Zero;
            for (int i = 0; i <= segments; i++) {
                float ang = i * step + rotation;
                float wave = MathF.Sin(time * 2f + ang * 2f + layerIndex) * 2f;
                Vector2 cur = center + HalibutRenderer.AngleDir(ang) * (radius + wave);
                if (i > 0) {
                    float bright = 0.6f + MathF.Sin(time * 3f + ang * 3f) * 0.3f;
                    HalibutRenderer.DrawLine(sb, prev, cur, 1.4f, color * bright);
                }
                prev = cur;
            }
        }

        private void DrawLockOverlay(SpriteBatch sb, Rectangle area, Vector2 center,
            HalibutPlayer hp, float a, float time) {
            Texture2D px = HalibutRenderer.Pixel;
            //红色脉动罩
            float pulse = MathF.Sin(time * 3f) * 0.15f + 0.35f;
            sb.Draw(px, area, new Rectangle(0, 0, 1, 1), new Color(180, 60, 60) * (a * pulse * 0.5f));
            //扫描线
            for (int i = 0; i < 7; i++) {
                float yOffset = (time * 2f + i * 0.31f) % 1f * area.Height;
                int y = area.Y + (int)yOffset;
                sb.Draw(px, new Rectangle(area.X, y, area.Width, 1), new Rectangle(0, 0, 1, 1),
                    new Color(220, 100, 100) * (a * 0.3f));
            }
            //流动虚线警告框
            HalibutRenderer.DrawDashedRectBorder(sb, area, HalibutTheme.Danger * (a * 0.85f),
                2f, 10f, 6f, time * 60f);

            //中心锁形图标
            Vector2 lockPos = center;
            float iconPulse = MathF.Sin(time * 4f) * 0.2f + 1f;
            DrawLockIcon(sb, lockPos, 30f * iconPulse, new Color(255, 120, 120) * a);

            //倒计时环 + 秒数
            int remain = hp.IsInteractionLockedTime;
            if (remain > 0) {
                float remainSeconds = remain / 60f;
                float progress = MathHelper.Clamp(remainSeconds / 10f, 0f, 1f);
                Vector2 ringCenter = lockPos + new Vector2(0f, 44f);
                HalibutRenderer.DrawRing(sb, ringCenter, 19f, 2f, new Color(80, 40, 40) * (a * 0.6f));
                float aStart = -MathHelper.PiOver2;
                HalibutRenderer.DrawArcStroke(sb, ringCenter, 19f, aStart,
                    aStart + MathHelper.TwoPi * progress, 2.4f, HalibutTheme.Danger * a);
                string secText = ((int)MathF.Ceiling(remainSeconds)).ToString();
                HalibutRenderer.DrawGlowTextCentered(sb, secText, ringCenter,
                    Color.White * a, HalibutTheme.Danger * (a * 0.6f), countdownScale);
                //剩余很短时的警告波
                if (remainSeconds <= 3f) {
                    float warnPulse = MathF.Sin(time * 10f) * 0.5f + 0.5f;
                    HalibutRenderer.DrawRing(sb, ringCenter, 25f + warnPulse * 9f, 1.3f,
                        HalibutTheme.Danger * (a * (0.45f - warnPulse * 0.2f)));
                }
            }
        }

        private static void DrawLockIcon(SpriteBatch sb, Vector2 center, float size, Color color) {
            Texture2D px = HalibutRenderer.Pixel;
            //锁身
            Rectangle body = new(
                (int)(center.X - size * 0.25f), (int)(center.Y - size * 0.05f),
                (int)(size * 0.5f), (int)(size * 0.42f));
            sb.Draw(px, body, new Rectangle(0, 0, 1, 1), color);
            //锁孔
            Rectangle hole = new(
                (int)(center.X - size * 0.07f), (int)(center.Y + size * 0.07f),
                (int)(size * 0.14f), (int)(size * 0.18f));
            sb.Draw(px, hole, new Rectangle(0, 0, 1, 1), HalibutTheme.Void * (color.A / 255f));
            //锁环
            HalibutRenderer.DrawArcStroke(sb, center + new Vector2(0f, -size * 0.05f),
                size * 0.27f, MathHelper.Pi, MathHelper.TwoPi, size * 0.1f, color);
        }

        public static string GetDescription(int layer) {
            if (layer < 1 || layer >= eyeLayerDescriptions.Length || eyeLayerDescriptions[layer] == null) {
                return string.Empty;
            }
            string value = eyeLayerDescriptions[layer].Value;
            value = value.Replace("[Halibut_Domain]", CWRKeySystem.Legend_Domain.ToTooltipString(CWRKeySystem.Notbound.Value));
            value = value.Replace("[Halibut_Restart]", CWRKeySystem.Legend_Restart.ToTooltipString(CWRKeySystem.Notbound.Value));
            value = value.Replace("[Halibut_Clone]", CWRKeySystem.Halibut_Clone.ToTooltipString(CWRKeySystem.Notbound.Value));
            value = value.Replace("[Halibut_Superposition]", CWRKeySystem.Halibut_Superposition.ToTooltipString(CWRKeySystem.Notbound.Value));
            value = value.Replace("[Halibut_Teleport]", CWRKeySystem.Legend_Teleport.ToTooltipString(CWRKeySystem.Notbound.Value));
            value = value.Replace("[Line]", "______________");
            return value;
        }

        private static string GetLayerNumeralText(int i) {
            if (Language.ActiveCulture.LegacyId != (int)GameCulture.CultureName.Chinese) {
                return i switch {
                    1 => "I",
                    2 => "II",
                    3 => "III",
                    4 => "IV",
                    5 => "V",
                    6 => "VI",
                    7 => "VII",
                    8 => "VIII",
                    9 => "IX",
                    10 => "X",
                    _ => i.ToString()
                };
            }
            return i switch {
                1 => "一",
                2 => "二",
                3 => "三",
                4 => "四",
                5 => "五",
                6 => "六",
                7 => "七",
                8 => "八",
                9 => "九",
                10 => "十",
                _ => i.ToString()
            };
        }
    }
}
