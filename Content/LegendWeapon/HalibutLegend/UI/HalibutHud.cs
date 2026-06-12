using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 比目鱼常驻HUD状态簇（屏幕左下角，手持时显示）：
    /// 当前技能主环（冷却扫掠）+ 复苏深度压力计 + 领域层数点阵 +
    /// 领域技能冷却卫星环 + 研究进度弧
    /// 全程序化绘制，无任何面板纹理
    /// </summary>
    [VaultLoaden(CWRConstant.UI + "Halibut/FishSkill")]
    internal class HalibutHud : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText";
        public static HalibutHud Instance => UIHandleLoader.GetUIHandleOfType<HalibutHud>();

        //三个领域技能图标（资源名需与字段名一致，大小34*34）
        public static Texture2D RestartFish = null;
        public static Texture2D Superposition = null;
        public static Texture2D FishTeleport = null;

        #region 本地化（复苏文案沿用旧 ResurrectionUI 键，保留既有的七语言翻译）
        private const string LegacyResKey = "Mods.CalamityOverhaul.Legend.HalibutText.ResurrectionUI.";
        public static LocalizedText ResTitle { get; private set; }
        public static LocalizedText ResPercentFormat { get; private set; }
        public static LocalizedText ResValueFormat { get; private set; }
        public static LocalizedText ResRateFormat { get; private set; }
        public static LocalizedText ResHeaderSummary { get; private set; }
        private static readonly LocalizedText[] resRateLevels = new LocalizedText[5];
        private static readonly LocalizedText[] resPhases = new LocalizedText[5];
        private static readonly LocalizedText[] resTrends = new LocalizedText[5];
        public static LocalizedText ResSummaryFormat { get; private set; }
        //新增键
        public static LocalizedText OpenAtlasHint { get; private set; }
        public static LocalizedText DomainLayerFormat { get; private set; }
        public static LocalizedText StudyingLabel { get; private set; }

        public override void SetStaticDefaults() {
            ResTitle = Language.GetOrRegister(LegacyResKey + "TitleText", () => "深渊复苏状态");
            ResPercentFormat = Language.GetOrRegister(LegacyResKey + "LinePercentFormat", () => "百分比 : {0:F1}%");
            ResValueFormat = Language.GetOrRegister(LegacyResKey + "LineValueFormat", () => "复苏值 : {0:F1} / {1:F1}");
            ResRateFormat = Language.GetOrRegister(LegacyResKey + "LineRateFormat", () => "速度   : {0:F3}/秒");
            ResHeaderSummary = Language.GetOrRegister(LegacyResKey + "HeaderSummaryText", () => "当前态势评估");
            resRateLevels[0] = Language.GetOrRegister(LegacyResKey + "RateLevelVeryLow", () => "极低");
            resRateLevels[1] = Language.GetOrRegister(LegacyResKey + "RateLevelLow", () => "低");
            resRateLevels[2] = Language.GetOrRegister(LegacyResKey + "RateLevelMedium", () => "中");
            resRateLevels[3] = Language.GetOrRegister(LegacyResKey + "RateLevelHigh", () => "高");
            resRateLevels[4] = Language.GetOrRegister(LegacyResKey + "RateLevelDanger", () => "危险");
            resPhases[0] = Language.GetOrRegister(LegacyResKey + "Phase1", () => "复苏平稳，尚无明显异象");
            resPhases[1] = Language.GetOrRegister(LegacyResKey + "Phase2", () => "局势渐起波纹，能量仍可控");
            resPhases[2] = Language.GetOrRegister(LegacyResKey + "Phase3", () => "脉冲已具侵蚀感，需要留意");
            resPhases[3] = Language.GetOrRegister(LegacyResKey + "Phase4", () => "高压区形成，领域边缘不稳定");
            resPhases[4] = Language.GetOrRegister(LegacyResKey + "Phase5", () => "深渊临界——随时可能失控");
            resTrends[0] = Language.GetOrRegister(LegacyResKey + "Trend1", () => "几乎静止");
            resTrends[1] = Language.GetOrRegister(LegacyResKey + "Trend2", () => "缓慢上升");
            resTrends[2] = Language.GetOrRegister(LegacyResKey + "Trend3", () => "稳态攀升");
            resTrends[3] = Language.GetOrRegister(LegacyResKey + "Trend4", () => "快速累积");
            resTrends[4] = Language.GetOrRegister(LegacyResKey + "Trend5", () => "危险激增");
            ResSummaryFormat = Language.GetOrRegister(LegacyResKey + "SummaryFormat",
                () => "状态：{0}。当前增长趋势：{1}。请根据态势调整领域或研究策略");

            OpenAtlasHint = this.GetLocalization(nameof(OpenAtlasHint), () => "左键或 {0} 打开深渊图鉴，{1} 呼出技能轮盘");
            DomainLayerFormat = this.GetLocalization(nameof(DomainLayerFormat), () => "领域层数 {0} / 10");
            StudyingLabel = this.GetLocalization(nameof(StudyingLabel), () => "研究中");
        }
        #endregion

        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead || Main.dedServ) {
                    return false;
                }
                return p.TryGetOverride<HalibutPlayer>(out var hp) && hp.HeldHalibut;
            }
        }

        //出现进度
        private float appear;
        //复苏计的平滑显示值与抖动
        private float displayRatio;
        private float gaugeShake;
        private float gaugeFlash;
        //卫星冷却的最大值跟踪与抖动
        private readonly Dictionary<int, int> maxCooldown = [];
        private readonly float[] satelliteShake = new float[3];
        //粒子池（复苏提升的飞行光粒等）
        private readonly HalibutUIParticlePool particles = new(70);
        //悬停状态
        private bool hoverCore;
        private bool hoverGauge;
        private int hoverSatellite = -1;

        /// <summary>
        /// HUD锚点（主环圆心）
        /// </summary>
        public static Vector2 Anchor => new(HalibutTheme.HudAnchorOffset.X,
            Main.screenHeight + HalibutTheme.HudAnchorOffset.Y);

        /// <summary>
        /// 复苏深度计的中心位置
        /// </summary>
        public static Vector2 GaugeCenter => Anchor + new Vector2(52f, 0f);

        /// <summary>
        /// 研究完成等时刻触发的复苏计强化反馈：从指定位置放出若干光粒飞向深度计
        /// </summary>
        public void TriggerGaugeImprove(Vector2 from, int count) {
            count = Math.Clamp(count, 1, 20);
            gaugeFlash = 1.2f;
            for (int i = 0; i < count; i++) {
                particles.SpawnFlyingMote(from + Main.rand.NextVector2Circular(20f, 20f), GaugeCenter, null, i * 4f);
            }
            particles.SpawnRingPulse(GaugeCenter, HalibutTheme.Glow, 60f, 4f);
        }

        public override void Update() {
            appear = MathHelper.Clamp(appear + 0.08f, 0f, 1f);
            particles.Update();

            if (!player.TryGetOverride<HalibutPlayer>(out var hp)) {
                return;
            }

            //复苏显示值平滑
            float ratio = hp.ResurrectionSystem?.Ratio ?? 0f;
            displayRatio = MathHelper.Lerp(displayRatio, ratio, 0.12f);

            //危险抖动
            float targetShake = ratio >= 0.9f ? 2.6f : ratio >= 0.7f ? 1.2f : 0f;
            gaugeShake = MathHelper.Lerp(gaugeShake, targetShake, 0.15f);
            if (gaugeFlash > 0f) {
                gaugeFlash = MathF.Max(gaugeFlash - 0.04f, 0f);
            }

            //卫星按键反馈：冷却中尝试使用 → 抖动
            if (hp.RestartFishCooldown > 0 && CWRKeySystem.Legend_Restart.JustPressed) {
                satelliteShake[0] = 1f;
            }
            if (hp.SuperpositionCooldown > 0 && CWRKeySystem.Halibut_Superposition.JustPressed) {
                satelliteShake[1] = 1f;
            }
            if (hp.FishTeleportCooldown > 0 && CWRKeySystem.Legend_Teleport.JustPressed) {
                satelliteShake[2] = 1f;
            }
            for (int i = 0; i < satelliteShake.Length; i++) {
                satelliteShake[i] *= 0.84f;
                if (satelliteShake[i] < 0.02f) {
                    satelliteShake[i] = 0f;
                }
            }

            //命中区域
            Vector2 anchor = Anchor;
            Size = new Vector2(150f, 130f);
            DrawPosition = anchor - new Vector2(HalibutTheme.HudCoreRingR + 10f, 70f);
            UIHitBox = DrawPosition.GetRectangle(Size);

            float atlasOpen = HalibutAtlas.Instance?.OpenProgress ?? 0f;
            if (atlasOpen > 0.4f) {
                hoverCore = hoverGauge = false;
                hoverSatellite = -1;
                return;
            }

            Vector2 mouse = MousePosition;
            hoverCore = Vector2.Distance(mouse, anchor) < HalibutTheme.HudCoreRingR + 4f;
            Vector2 gaugeCenter = GaugeCenter;
            Rectangle gaugeRect = new((int)(gaugeCenter.X - 11f), (int)(gaugeCenter.Y - HalibutTheme.HudGaugeHeight * 0.5f - 6f),
                22, (int)HalibutTheme.HudGaugeHeight + 12);
            hoverGauge = gaugeRect.Contains(mouse.ToPoint());

            hoverSatellite = -1;
            for (int i = 0; i < 3; i++) {
                if (Vector2.Distance(mouse, SatellitePos(i)) < HalibutTheme.HudSatelliteR + 3f) {
                    hoverSatellite = i;
                }
            }

            if (hoverCore || hoverGauge || hoverSatellite >= 0) {
                player.mouseInterface = true;
            }

            //点击主环打开图鉴
            if (hoverCore && keyLeftPressState == KeyPressState.Pressed) {
                SoundEngine.PlaySound(CWRSound.ButtonZero);
                HalibutAtlas.Instance?.Open();
            }
        }

        private static Vector2 SatellitePos(int index) {
            return Anchor + new Vector2(-6f + index * (HalibutTheme.HudSatelliteR * 2f + 8f),
                -HalibutTheme.HudCoreRingR - 26f);
        }

        public override void Draw(SpriteBatch sb) {
            float atlasOpen = HalibutAtlas.Instance?.OpenProgress ?? 0f;
            float a = appear * (1f - MathHelper.Clamp(atlasOpen * 1.6f, 0f, 1f));
            if (a < 0.01f) {
                return;
            }
            var save = player.GetModPlayer<HalibutSave>();
            if (!player.TryGetOverride<HalibutPlayer>(out var hp)) {
                return;
            }
            float time = Main.GlobalTimeWrappedHourly;
            Vector2 anchor = Anchor;

            DrawCoreRing(sb, anchor, save, a, time);
            DrawDomainPips(sb, anchor, save, hp, a, time);
            DrawGauge(sb, hp, a, time);
            DrawSatellites(sb, hp, a, time);
            DrawStudyArc(sb, anchor, save, a);
            particles.Draw(sb, a);

            //悬浮信息
            if (hoverCore) {
                string atlasKey = CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value);
                string wheelKey = CWRKeySystem.Halibut_SkillWheel.ToTooltipString(CWRKeySystem.Notbound.Value);
                string title = save.FishSkill?.DisplayName?.Value ?? Lang.GetItemNameValue(HalibutOverride.ID);
                HalibutRenderer.DrawCursorPanel(sb, MousePosition, title, HalibutTheme.GlowHi,
                    string.Format(OpenAtlasHint.Value, atlasKey, wheelKey), a);
            }
            else if (hoverGauge) {
                DrawGaugeTooltip(sb, hp, a);
            }
            else if (hoverSatellite >= 0) {
                int cd = hoverSatellite switch {
                    0 => hp.RestartFishCooldown,
                    1 => hp.SuperpositionCooldown,
                    _ => hp.FishTeleportCooldown,
                };
                int seconds = (int)MathF.Ceiling(cd / 60f);
                HalibutRenderer.DrawCursorPanel(sb, MousePosition, $"{seconds}s", HalibutTheme.Text, null, a);
            }
        }

        private void DrawCoreRing(SpriteBatch sb, Vector2 anchor, HalibutSave save, float a, float time) {
            float breath = HalibutTheme.Breath(time, 0.7f);
            //底盘与外环
            HalibutRenderer.DrawDisc(sb, anchor, HalibutTheme.HudCoreR, 5f, HalibutTheme.Deep * (0.92f * a));
            Color ringCol = Color.Lerp(HalibutTheme.Glow, HalibutTheme.GlowHi, breath * 0.5f + (hoverCore ? 0.4f : 0f));
            HalibutRenderer.DrawRing(sb, anchor, HalibutTheme.HudCoreRingR, 1.6f,
                ringCol * ((0.55f + breath * 0.25f) * a));
            //缓转刻度
            float markRot = time * 0.35f;
            for (int i = 0; i < 4; i++) {
                float a0 = markRot + i * MathHelper.PiOver2;
                HalibutRenderer.DrawArcStroke(sb, anchor, HalibutTheme.HudCoreRingR + 4f,
                    a0, a0 + 0.4f, 1.1f, HalibutTheme.Teal * (0.6f * a));
            }

            //技能图标 + 冷却扫掠
            FishSkill skill = save.FishSkill;
            if (skill?.Icon != null) {
                float scale = 36f / MathF.Max(skill.Icon.Width, skill.Icon.Height);
                sb.Draw(skill.Icon, anchor, null, Color.White * a, 0f,
                    skill.Icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                HalibutRenderer.DrawCooldownSweep(sb, anchor, HalibutTheme.HudCoreR - 1f,
                    skill.CooldownRatio, a);
            }
            else {
                //未选定技能时显示武器本体
                Main.instance.LoadItem(HalibutOverride.ID);
                Texture2D weapon = Terraria.GameContent.TextureAssets.Item[HalibutOverride.ID].Value;
                float scale = 34f / MathF.Max(weapon.Width, weapon.Height);
                sb.Draw(weapon, anchor, null, Color.White * (0.85f * a), 0f,
                    weapon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawDomainPips(SpriteBatch sb, Vector2 anchor, HalibutSave save,
            HalibutPlayer hp, float a, float time) {
            int active = save.ActiveEyeCount;
            int crashLevel = hp.CrashesLevel();
            const int total = 10;
            //在主环左上方排出一道点阵弧
            float aStart = MathHelper.Pi * 0.78f;
            float aEnd = MathHelper.Pi * 1.46f;
            float radius = HalibutTheme.HudCoreRingR + 11f;
            for (int i = 0; i < total; i++) {
                float t = total <= 1 ? 0f : i / (float)(total - 1);
                Vector2 pos = anchor + HalibutRenderer.AngleDir(MathHelper.Lerp(aStart, aEnd, t)) * radius;
                bool lit = i < active;
                bool crashed = lit && (i + 1) <= crashLevel;
                Color col;
                float size;
                if (!lit) {
                    col = HalibutTheme.Disabled * (0.45f * a);
                    size = 1.6f;
                }
                else if (crashed) {
                    float flick = HalibutTheme.Breath(time, i, 5f);
                    col = Color.Lerp(HalibutTheme.DangerDim, HalibutTheme.Danger, flick) * a;
                    size = 2.3f;
                }
                else {
                    float breath = HalibutTheme.Breath(time, i * 0.6f, 2.5f);
                    col = Color.Lerp(HalibutTheme.Glow, HalibutTheme.GlowHi, breath) * a;
                    size = 2.3f;
                }
                HalibutRenderer.DrawDisc(sb, pos, size, 1.6f, col);
            }
        }

        private void DrawGauge(SpriteBatch sb, HalibutPlayer hp, float a, float time) {
            Vector2 center = GaugeCenter;
            //危险抖动
            if (gaugeShake > 0.05f) {
                center += new Vector2(MathF.Sin(time * 38f) * gaugeShake, MathF.Cos(time * 29f) * gaugeShake * 0.5f);
            }
            float h = HalibutTheme.HudGaugeHeight;
            float w = HalibutTheme.HudGaugeWidth;
            Vector2 top = center + new Vector2(0f, -h * 0.5f);
            Vector2 bottom = center + new Vector2(0f, h * 0.5f);

            //背槽
            HalibutRenderer.DrawLine(sb, top, bottom, w + 4f, HalibutTheme.Void * (0.7f * a));
            HalibutRenderer.DrawLine(sb, top, bottom, w + 2f, HalibutTheme.Deep * (0.9f * a));

            //填充（自下而上）
            float ratio = MathHelper.Clamp(displayRatio, 0f, 1f);
            if (ratio > 0.005f) {
                Vector2 fillTop = bottom + new Vector2(0f, -h * ratio);
                Color fillCol = GaugeColor(ratio);
                HalibutRenderer.DrawLine(sb, fillTop, bottom, w, fillCol * (0.92f * a));
                //液面亮线与辉光
                HalibutRenderer.DrawLine(sb, fillTop + new Vector2(-w * 0.8f, 0f), fillTop + new Vector2(w * 0.8f, 0f),
                    1.6f, HalibutTheme.Caustic * (0.85f * a));
                HalibutRenderer.DrawSoftGlow(sb, fillTop, 9f + gaugeFlash * 8f, fillCol * ((0.5f + gaugeFlash * 0.4f) * a));
            }

            //阈值刻度（70% / 90%）
            foreach (float th in stackalloc float[] { 0.7f, 0.9f }) {
                Vector2 tick = bottom + new Vector2(0f, -h * th);
                Color tickCol = th >= 0.9f ? HalibutTheme.Danger : HalibutTheme.Accent;
                HalibutRenderer.DrawLine(sb, tick + new Vector2(-w, 0f), tick + new Vector2(w + 2f, 0f),
                    1.1f, tickCol * (0.75f * a));
            }

            //外缘描边
            HalibutRenderer.DrawLine(sb, top + new Vector2(-w * 0.5f - 2f, 0f), bottom + new Vector2(-w * 0.5f - 2f, 0f),
                1f, HalibutTheme.Teal * (0.6f * a));
            HalibutRenderer.DrawLine(sb, top + new Vector2(w * 0.5f + 2f, 0f), bottom + new Vector2(w * 0.5f + 2f, 0f),
                1f, HalibutTheme.Teal * (0.6f * a));

            //临界警告：脉动红环
            if (ratio >= 0.9f) {
                float pulse = HalibutTheme.Breath(time, 3f, 6f);
                HalibutRenderer.DrawRing(sb, center, 16f + pulse * 5f, 1.3f,
                    HalibutTheme.Danger * ((0.5f - pulse * 0.3f) * a));
            }
        }

        private static Color GaugeColor(float ratio) {
            if (ratio >= 0.9f) {
                float flash = MathF.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.5f + 0.5f;
                return Color.Lerp(new Color(255, 50, 50), new Color(255, 150, 0), flash);
            }
            if (ratio >= 0.7f) {
                return Color.Lerp(new Color(255, 200, 50), new Color(255, 100, 50), (ratio - 0.7f) / 0.3f);
            }
            if (ratio >= 0.4f) {
                return Color.Lerp(new Color(100, 200, 255), new Color(255, 200, 50), (ratio - 0.4f) / 0.3f);
            }
            return Color.Lerp(new Color(50, 150, 255), new Color(100, 200, 255), ratio / 0.4f);
        }

        private void DrawSatellites(SpriteBatch sb, HalibutPlayer hp, float a, float time) {
            Span<int> cooldowns = [hp.RestartFishCooldown, hp.SuperpositionCooldown, hp.FishTeleportCooldown];
            Texture2D[] icons = [RestartFish, Superposition, FishTeleport];
            for (int i = 0; i < 3; i++) {
                if (cooldowns[i] <= 0 || icons[i] == null) {
                    maxCooldown.Remove(i);
                    continue;
                }
                if (!maxCooldown.TryGetValue(i, out int max) || cooldowns[i] > max) {
                    maxCooldown[i] = cooldowns[i];
                    max = cooldowns[i];
                }
                float remain = MathHelper.Clamp(cooldowns[i] / (float)max, 0f, 1f);
                Vector2 pos = SatellitePos(i);
                if (satelliteShake[i] > 0.02f) {
                    float amp = 3.6f * satelliteShake[i];
                    pos += new Vector2(MathF.Sin(time * 32f + i * 1.7f) * amp,
                        MathF.Cos(time * 25f + i * 1.7f) * amp * 0.5f);
                }
                bool hovered = hoverSatellite == i;
                HalibutRenderer.DrawDisc(sb, pos, HalibutTheme.HudSatelliteR, 3f, HalibutTheme.Deep * (0.9f * a));
                float scale = (HalibutTheme.HudSatelliteR * 2f - 6f) / MathF.Max(icons[i].Width, icons[i].Height);
                Color iconCol = Color.White * ((hovered ? 1f : 0.82f) * a);
                sb.Draw(icons[i], pos, null, iconCol, 0f, icons[i].Size() * 0.5f, scale, SpriteEffects.None, 0f);
                HalibutRenderer.DrawCooldownSweep(sb, pos, HalibutTheme.HudSatelliteR - 1f, remain, a);
                //剩余进度环
                float aStart = -MathHelper.PiOver2;
                HalibutRenderer.DrawArcStroke(sb, pos, HalibutTheme.HudSatelliteR + 1.5f,
                    aStart, aStart + MathHelper.TwoPi * (1f - remain), 1.3f,
                    (satelliteShake[i] > 0.1f ? HalibutTheme.Danger : HalibutTheme.Glow) * (0.85f * a));
            }
        }

        private static void DrawStudyArc(SpriteBatch sb, Vector2 anchor, HalibutSave save, float a) {
            if (!save.IsStudying) {
                return;
            }
            float progress = MathHelper.Clamp(save.StudyTimer / (float)save.StudyDuration, 0f, 1f);
            float aStart = MathHelper.Pi * 0.25f;
            float radius = HalibutTheme.HudCoreRingR + 11f;
            //背景弧
            HalibutRenderer.DrawArcStroke(sb, anchor, radius, aStart, aStart + MathHelper.PiOver2,
                1.6f, HalibutTheme.Teal * (0.5f * a));
            //进度弧
            HalibutRenderer.DrawArcStroke(sb, anchor, radius, aStart, aStart + MathHelper.PiOver2 * progress,
                2f, HalibutTheme.Accent * (0.9f * a));
        }

        private void DrawGaugeTooltip(SpriteBatch sb, HalibutPlayer hp, float a) {
            var res = hp.ResurrectionSystem;
            if (res == null) {
                return;
            }
            float ratio = res.Ratio;
            float rate = res.ResurrectionRate;
            string line1 = string.Format(ResPercentFormat.Value, ratio * 100f);
            string line2 = string.Format(ResValueFormat.Value, res.CurrentValue, res.MaxValue);
            string line3 = string.Format(ResRateFormat.Value, rate * 60f) + "  [" + GetRateLevel(rate) + "]";
            string summary = string.Format(ResSummaryFormat.Value, GetPhase(ratio), GetTrend(rate));
            string body = line1 + "\n" + line2 + "\n" + line3 + "\n" + ResHeaderSummary.Value + "\n" + summary;
            HalibutRenderer.DrawCursorPanel(sb, MousePosition, ResTitle.Value,
                GaugeColor(ratio), body, a, null, default, 250f, 430f);
        }

        private static string GetRateLevel(float rate) {
            if (rate < 0.01f) {
                return resRateLevels[0].Value;
            }
            if (rate < 0.025f) {
                return resRateLevels[1].Value;
            }
            if (rate < 0.05f) {
                return resRateLevels[2].Value;
            }
            if (rate < 0.09f) {
                return resRateLevels[3].Value;
            }
            return resRateLevels[4].Value;
        }

        private static string GetPhase(float ratio) {
            if (ratio < 0.25f) {
                return resPhases[0].Value;
            }
            if (ratio < 0.5f) {
                return resPhases[1].Value;
            }
            if (ratio < 0.7f) {
                return resPhases[2].Value;
            }
            if (ratio < 0.9f) {
                return resPhases[3].Value;
            }
            return resPhases[4].Value;
        }

        private static string GetTrend(float rate) {
            if (rate < 0.01f) {
                return resTrends[0].Value;
            }
            if (rate < 0.025f) {
                return resTrends[1].Value;
            }
            if (rate < 0.05f) {
                return resTrends[2].Value;
            }
            if (rate < 0.09f) {
                return resTrends[3].Value;
            }
            return resTrends[4].Value;
        }
    }
}
