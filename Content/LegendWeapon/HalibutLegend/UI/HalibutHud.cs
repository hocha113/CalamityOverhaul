using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas;
using CalamityOverhaul.Content.UIs;
using CalamityOverhaul.Content.UIs.HudStack;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 比目鱼常驻 HUD（左下，手持时显示）
    /// 核心 HalibutHudEye.fx、跟踪/眨眼/虹膜/红化/冷却翳/就绪闪
    /// 复苏条 HalibutHudGauge.fx、液面/气泡/刻线/临界
    /// 外围、徽章/层数弧/冷却环/研究弧
    /// </summary>
    [VaultLoaden(CWRConstant.UI + "FishSkill")]
    internal class HalibutHud : UIHandle, ILocalizedModType, IBottomLeftHud
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
            resPhases[4] = Language.GetOrRegister(LegacyResKey + "Phase5", () => "深渊临界，随时可能失控");
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

        #region 左下角 HUD 队列接入
        //主武器HUD、左下队列底锚（次序最小，独占不顶高）
        bool IBottomLeftHud.HudStackActive => Active;
        int IBottomLeftHud.HudStackOrder => 0;
        Vector2 IBottomLeftHud.HudStackAnchor => NaturalAnchor;
        //上下覆盖卫星环/层弧/压力柱与徽章命中盒
        float IBottomLeftHud.HudStackTopExtent => 60f;
        float IBottomLeftHud.HudStackBottomExtent => 70f;
        #endregion

        #region 几何常量
        //眼睛着色器quad边长与可视半径
        private const int EyeQuadSize = 96;
        private const float EyeR = 44f;
        //技能徽章
        private static Vector2 BadgeOffset => new(0, 42f);
        private const float BadgeR = 13.5f;
        //层数菱标弧
        private const float PipArcR = 56f;
        //流体压力柱
        private const int GaugeW = 30;
        private const int GaugeH = 106;
        #endregion

        //出现进度
        private float appear;
        //复苏显示
        private float displayRatio;
        private float gaugeShake;
        private float gaugeFlash;
        //眼睛状态
        private float blinkPhase = 1f;     //>=1表示不在眨眼过程中
        private int nextBlinkTimer = 180;
        private float eyeOpen = 1f;        //平滑睁眼量
        private Vector2 pupilOffset;       //平滑注视偏移
        private float dilate;              //平滑扩张量
        private float readyFlash;          //冷却结束闪光
        private float prevCooldownRatio;
        private float badgeFlash;          //切换技能时徽章闪光
        //卫星冷却
        private readonly Dictionary<int, int> maxCooldown = [];
        private readonly float[] satelliteShake = new float[3];
        //粒子
        private readonly HalibutUIParticlePool particles = new(70);
        //悬停
        private bool hoverCore;
        private bool hoverGauge;
        private int hoverSatellite = -1;

        /// <summary>
        /// HUD自然锚点（深渊之眼圆心）、UI空间屏尺寸，不受缩放语境
        /// <br/>未参与左下队列避让时的原始位置
        /// </summary>
        public static Vector2 NaturalAnchor => new(HalibutTheme.HudAnchorOffset.X,
            HalibutTheme.UIScreenH + HalibutTheme.HudAnchorOffset.Y);

        /// <summary>
        /// HUD锚点（深渊之眼圆心）、经左下队列避让后的最终位
        /// <br/>队列底成员，独占时与 <see cref="NaturalAnchor"/> 一致；绘制/命中统一用本属性
        /// </summary>
        public static Vector2 Anchor {
            get {
                HalibutHud inst = Instance;
                return inst == null ? NaturalAnchor : BottomLeftHudStack.ResolveAnchor(inst);
            }
        }

        /// <summary>
        /// 复苏压力柱中心
        /// </summary>
        public static Vector2 GaugeCenter => Anchor + new Vector2(64f, -4f);

        /// <summary>
        /// 研究完成等、复苏强化，光粒飞向压力柱
        /// </summary>
        public void TriggerGaugeImprove(Vector2 from, int count) {
            count = Math.Clamp(count, 1, 20);
            gaugeFlash = 1.2f;
            for (int i = 0; i < count; i++) {
                particles.SpawnFlyingMote(from + Main.rand.NextVector2Circular(20f, 20f), GaugeCenter, null, i * 4f);
            }
            particles.SpawnRingPulse(GaugeCenter, HalibutTheme.Glow, 60f, 4f);
        }

        /// <summary>
        /// 技能切换、眨眼+点亮徽章
        /// </summary>
        public void NotifySkillSwitched() {
            if (blinkPhase >= 1f) {
                blinkPhase = 0f;
            }
            badgeFlash = 1f;
        }

        public override void Update() {
            appear = MathHelper.Clamp(appear + 0.08f, 0f, 1f);
            particles.Update();

            if (!player.TryGetOverride<HalibutPlayer>(out var hp)) {
                return;
            }
            var save = player.GetModPlayer<HalibutSave>();

            //复苏显示值平滑与抖动
            float ratio = hp.ResurrectionSystem?.Ratio ?? 0f;
            displayRatio = MathHelper.Lerp(displayRatio, ratio, 0.12f);
            float targetShake = ratio >= 0.9f ? 2.6f : ratio >= 0.7f ? 1.1f : 0f;
            gaugeShake = MathHelper.Lerp(gaugeShake, targetShake, 0.15f);
            if (gaugeFlash > 0f) {
                gaugeFlash = MathF.Max(gaugeFlash - 0.035f, 0f);
            }

            UpdateEyeState(save, hp, ratio);
            UpdateSatellites(hp);

            //命中区域
            Vector2 anchor = Anchor;
            Size = new Vector2(170f, 150f);
            DrawPosition = anchor - new Vector2(EyeR + 14f, 84f);
            UIHitBox = DrawPosition.GetRectangle(Size);

            //自家图鉴沿用 0.4 缓冲；异域全屏（任务书等）当帧断电，免得眼睛点击在册子底下开图鉴
            float atlasOpen = HalibutAtlas.Instance?.OpenProgress ?? 0f;
            if (atlasOpen > 0.4f
                || FullScreenUIHub.AnyForeignOpen(FullScreenUIDomain.Halibut)) {
                hoverCore = hoverGauge = false;
                hoverSatellite = -1;
                return;
            }

            Vector2 mouse = MousePosition;
            hoverCore = Vector2.Distance(mouse, anchor) < EyeR
                || Vector2.Distance(mouse, anchor + BadgeOffset) < BadgeR + 3f;
            Vector2 gaugeCenter = GaugeCenter;
            Rectangle gaugeRect = new((int)(gaugeCenter.X - GaugeW * 0.5f - 4f),
                (int)(gaugeCenter.Y - GaugeH * 0.5f - 5f), GaugeW + 8, GaugeH + 10);
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

            //点击眼睛打开图鉴
            if (hoverCore && keyLeftPressState == KeyPressState.Pressed) {
                SoundEngine.PlaySound(CWRSound.ButtonZero);
                HalibutAtlas.Instance?.Open();
            }
        }

        private void UpdateEyeState(HalibutSave save, HalibutPlayer hp, float ratio) {
            //眨眼调度、随机间隔，blinkPhase 0→1
            if (blinkPhase >= 1f) {
                nextBlinkTimer--;
                if (nextBlinkTimer <= 0) {
                    blinkPhase = 0f;
                    nextBlinkTimer = Main.rand.Next(240, 560);
                    //偶尔连眨两次
                    if (Main.rand.NextBool(4)) {
                        nextBlinkTimer = 26;
                    }
                }
            }
            else {
                blinkPhase = MathF.Min(blinkPhase + 0.085f, 1f);
            }
            //睁眼量、闭开三角波
            float blinkOpen = blinkPhase >= 1f ? 1f : MathF.Abs(blinkPhase * 2f - 1f);
            //复苏临界时眼睛圆睁
            float openTarget = MathF.Min(blinkOpen + ratio * 0.1f, 1.05f);
            eyeOpen = MathHelper.Lerp(eyeOpen, openTarget, 0.5f);

            //注视、光标微偏
            Vector2 toMouse = MousePosition - Anchor;
            float len = toMouse.Length();
            Vector2 gazeTarget = len > 4f ? toMouse / len * MathF.Min(len * 0.06f, 4.5f) : Vector2.Zero;
            pupilOffset = Vector2.Lerp(pupilOffset, gazeTarget, 0.12f);

            //扩张、呼吸+躁动+悬停+就绪
            FishSkill skill = save.FishSkill;
            float cd = skill?.CooldownRatio ?? 0f;
            if (prevCooldownRatio > 0.02f && cd <= 0.001f) {
                readyFlash = 1f;//冷却结束、闪光+瞳孔骤张
            }
            prevCooldownRatio = cd;
            if (readyFlash > 0f) {
                readyFlash = MathF.Max(readyFlash - 0.045f, 0f);
            }
            if (badgeFlash > 0f) {
                badgeFlash = MathF.Max(badgeFlash - 0.05f, 0f);
            }
            float breath = HalibutTheme.Breath(Main.GlobalTimeWrappedHourly, 0.7f, 1.6f);
            float dilateTarget = 0.12f + breath * 0.10f + ratio * 0.42f
                + (hoverCore ? 0.14f : 0f) + readyFlash * 0.5f;
            dilate = MathHelper.Lerp(dilate, MathHelper.Clamp(dilateTarget, 0f, 1f), 0.15f);
        }

        private void UpdateSatellites(HalibutPlayer hp) {
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
        }

        private static Vector2 SatellitePos(int index) {
            return Anchor + new Vector2(-16f + index * (HalibutTheme.HudSatelliteR * 2f + 7f), -70f);
        }

        public override void Draw(SpriteBatch sb) {
            //自家图鉴或异域全屏铺开都让位
            float cover = MathF.Max(HalibutAtlas.Instance?.OpenProgress ?? 0f,
                FullScreenUIHub.ForeignOcclusion01(FullScreenUIDomain.Halibut));
            float a = appear * (1f - MathHelper.Clamp(cover * 1.6f, 0f, 1f));
            if (a < 0.01f) {
                return;
            }
            if (!player.TryGetOverride<HalibutPlayer>(out var hp)) {
                return;
            }
            var save = player.GetModPlayer<HalibutSave>();
            float time = Main.GlobalTimeWrappedHourly;
            Vector2 anchor = Anchor;
            int crashLevel = hp.CrashesLevel();
            int activeLayers = save.ActiveEyeCount;

            DrawAbyssalEye(sb, anchor, save, hp, activeLayers, crashLevel, a);
            DrawSkillBadge(sb, anchor, save, a, time);
            DrawDomainPips(sb, anchor, activeLayers, crashLevel, a, time);
            DrawGauge(sb, hp, a, time);
            DrawSatellites(sb, hp, a, time);
            DrawStudyArc(sb, anchor, save, a);
            particles.Draw(sb, a);

            //悬浮信息
            if (hoverCore) {
                string atlasKey = CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value);
                string wheelKey = CWRKeySystem.RadialWheel_Key.ToTooltipString(CWRKeySystem.Notbound.Value);
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

        #region 深渊之眼
        private void DrawAbyssalEye(SpriteBatch sb, Vector2 anchor, HalibutSave save,
            HalibutPlayer hp, int activeLayers, int crashLevel, float a) {
            FishSkill skill = save.FishSkill;
            float cd = skill?.CooldownRatio ?? 0f;
            Effect effect = EffectLoader.HalibutHudEye?.Value;
            if (effect == null) {
                DrawEyeFallback(sb, anchor, skill, cd, a);
                return;
            }
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(a);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(EyeQuadSize, EyeQuadSize));
            effect.Parameters["uPupilOffset"]?.SetValue(pupilOffset);
            effect.Parameters["uDilate"]?.SetValue(dilate);
            effect.Parameters["uBlink"]?.SetValue(MathHelper.Clamp(eyeOpen, 0f, 1f));
            effect.Parameters["uLayers"]?.SetValue(activeLayers / 10f);
            effect.Parameters["uCrash"]?.SetValue(MathHelper.Clamp(crashLevel / 10f, 0f, 1f));
            effect.Parameters["uCooldown"]?.SetValue(MathHelper.Clamp(cd, 0f, 1f));
            effect.Parameters["uAgitation"]?.SetValue(MathHelper.Clamp(displayRatio, 0f, 1f));
            effect.Parameters["uReadyFlash"]?.SetValue(readyFlash);
            Rectangle dest = new((int)(anchor.X - EyeQuadSize * 0.5f), (int)(anchor.Y - EyeQuadSize * 0.5f),
                EyeQuadSize, EyeQuadSize);
            HalibutRenderer.DrawEffectQuad(sb, effect, dest);
        }

        /// <summary>
        /// 着色器缺失CPU回退、环+图标
        /// </summary>
        private void DrawEyeFallback(SpriteBatch sb, Vector2 anchor, FishSkill skill, float cd, float a) {
            float time = Main.GlobalTimeWrappedHourly;
            float breath = HalibutTheme.Breath(time, 0.7f);
            HalibutRenderer.DrawDisc(sb, anchor, HalibutTheme.HudCoreR, 5f, HalibutTheme.Deep * (0.92f * a));
            HalibutRenderer.DrawRing(sb, anchor, HalibutTheme.HudCoreRingR, 1.6f,
                HalibutTheme.Glow * ((0.55f + breath * 0.25f) * a));
            if (skill?.Icon != null) {
                float scale = 36f / MathF.Max(skill.Icon.Width, skill.Icon.Height);
                sb.Draw(skill.Icon, anchor, null, Color.White * a, 0f,
                    skill.Icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                HalibutRenderer.DrawCooldownSweep(sb, anchor, HalibutTheme.HudCoreR - 1f, cd, a);
            }
        }

        /// <summary>
        /// 技能徽章、眼右下小环图标
        /// </summary>
        private void DrawSkillBadge(SpriteBatch sb, Vector2 anchor, HalibutSave save, float a, float time) {
            FishSkill skill = save.FishSkill;
            if (skill?.Icon == null) {
                return;
            }
            Vector2 pos = anchor + BadgeOffset;
            float flash = badgeFlash;
            HalibutRenderer.DrawDisc(sb, pos, BadgeR - 1f, 3f, HalibutTheme.Deep * (0.94f * a));
            Color ringCol = Color.Lerp(HalibutTheme.Glow, HalibutTheme.Caustic, flash);
            HalibutRenderer.DrawRing(sb, pos, BadgeR, 1.3f, ringCol * ((0.7f + flash * 0.3f) * a));
            //切换闪光环
            if (flash > 0.02f) {
                HalibutRenderer.DrawRing(sb, pos, BadgeR + (1f - flash) * 10f, 1.2f,
                    HalibutTheme.Caustic * (flash * 0.8f * a));
            }
            float scale = (BadgeR * 2f - 7f) / MathF.Max(skill.Icon.Width, skill.Icon.Height);
            sb.Draw(skill.Icon, pos, null, Color.White * a, 0f,
                skill.Icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            if (skill.CooldownRatio > 0.01f) {
                HalibutRenderer.DrawCooldownSweep(sb, pos, BadgeR - 2f, skill.CooldownRatio, a);
                int seconds = (int)MathF.Ceiling(skill.Cooldown / 60f);
                HalibutRenderer.DrawGlowTextCentered(sb, seconds.ToString(), pos + new Vector2(0f, BadgeR + 9f),
                    HalibutTheme.Text * (0.9f * a), HalibutTheme.Deep * (0.5f * a), 0.66f);
            }
        }
        #endregion

        /// <summary>
        /// 领域层数菱标弧（左上象限），死机层闪红
        /// </summary>
        private static void DrawDomainPips(SpriteBatch sb, Vector2 anchor, int active,
            int crashLevel, float a, float time) {
            const int total = 10;
            float aStart = MathHelper.Pi * 0.84f;
            float aEnd = MathHelper.Pi * 1.46f;
            //引导弧线
            HalibutRenderer.DrawArcStroke(sb, anchor, PipArcR, aStart, aEnd, 0.8f,
                HalibutTheme.Teal * (0.30f * a));
            for (int i = 0; i < total; i++) {
                float t = i / (float)(total - 1);
                Vector2 pos = anchor + HalibutRenderer.AngleDir(MathHelper.Lerp(aStart, aEnd, t)) * PipArcR;
                bool lit = i < active;
                bool crashed = lit && (i + 1) <= crashLevel;
                if (!lit) {
                    HalibutRenderer.DrawDiamond(sb, pos, 2.4f, HalibutTheme.Disabled * (0.45f * a));
                    continue;
                }
                if (crashed) {
                    float flick = HalibutTheme.Breath(time, i, 5f);
                    HalibutRenderer.DrawDiamond(sb, pos, 3.8f,
                        Color.Lerp(HalibutTheme.DangerDim, HalibutTheme.Danger, flick) * a);
                }
                else {
                    float breath = HalibutTheme.Breath(time, i * 0.6f, 2.5f);
                    HalibutRenderer.DrawDiamond(sb, pos, 4.6f, HalibutTheme.Glow * (0.30f * a));
                    HalibutRenderer.DrawDiamond(sb, pos, 3f,
                        Color.Lerp(HalibutTheme.Glow, HalibutTheme.Caustic, breath) * a);
                }
            }
        }

        #region 复苏压力柱
        private void DrawGauge(SpriteBatch sb, HalibutPlayer hp, float a, float time) {
            Vector2 center = GaugeCenter;
            if (gaugeShake > 0.05f) {
                center += new Vector2(MathF.Sin(time * 38f) * gaugeShake, MathF.Cos(time * 29f) * gaugeShake * 0.5f);
            }
            float ratio = MathHelper.Clamp(displayRatio, 0f, 1f);
            float danger = MathHelper.Clamp((ratio - 0.5f) / 0.45f, 0f, 1f);
            float rate = MathHelper.Clamp((hp.ResurrectionSystem?.ResurrectionRate ?? 0f) / 0.09f, 0f, 1f);

            Effect effect = EffectLoader.HalibutHudGauge?.Value;
            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(a);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(GaugeW, GaugeH));
            effect.Parameters["uFill"]?.SetValue(ratio);
            effect.Parameters["uDanger"]?.SetValue(danger);
            effect.Parameters["uRate"]?.SetValue(rate);
            effect.Parameters["uFlash"]?.SetValue(gaugeFlash);
            Rectangle dest = new((int)(center.X - GaugeW * 0.5f), (int)(center.Y - GaugeH * 0.5f), GaugeW, GaugeH);
            HalibutRenderer.DrawEffectQuad(sb, effect, dest);

            //临界警告、外侧脉动红环
            if (ratio >= 0.9f) {
                float pulse = HalibutTheme.Breath(time, 3f, 6f);
                HalibutRenderer.DrawRing(sb, center, 22f + pulse * 6f, 1.3f,
                    HalibutTheme.Danger * ((0.45f - pulse * 0.25f) * a));
            }
            //强化反馈闪光环
            if (gaugeFlash > 0.02f) {
                HalibutRenderer.DrawSoftGlow(sb, center, 26f + gaugeFlash * 10f,
                    HalibutTheme.Glow * (gaugeFlash * 0.4f * a));
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
        #endregion

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
            float aStart = MathHelper.Pi * 0.10f;
            //背景弧
            HalibutRenderer.DrawArcStroke(sb, anchor, PipArcR, aStart, aStart + MathHelper.PiOver2,
                1.4f, HalibutTheme.Teal * (0.45f * a));
            //进度弧 + 前端光点
            float aEnd = aStart + MathHelper.PiOver2 * progress;
            HalibutRenderer.DrawArcStroke(sb, anchor, PipArcR, aStart, aEnd,
                1.9f, HalibutTheme.Accent * (0.9f * a));
            HalibutRenderer.DrawDisc(sb, anchor + HalibutRenderer.AngleDir(aEnd) * PipArcR, 1.8f, 1.4f,
                HalibutTheme.Caustic * (0.9f * a));
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
