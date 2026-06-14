using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.AcheronProtocols
{
    /// <summary>银河危机剧情演出渲染器（主控）</summary>
    internal partial class GalacticCrisisRender : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public override bool Active => active || fadeProgress > 0.01f;
        public override float RenderPriority => 0.88f;

        #region 本地化文本

        //面板标题
        public static LocalizedText HeaderStellarNavigation { get; private set; }
        public static LocalizedText HeaderKortoSystemOverview { get; private set; }
        public static LocalizedText HeaderGalacticStrategicMap { get; private set; }

        //银河系标记
        public static LocalizedText MarkerTerra { get; private set; }

        //灭绝令
        public static LocalizedText ExtinctionProtocolWarning { get; private set; }

        //战术人形档案
        public static LocalizedText HeaderAndroidProfile { get; private set; }
        public static LocalizedText AndroidArtisName { get; private set; }
        public static LocalizedText AndroidApolaName { get; private set; }
        public static LocalizedText AndroidCodename { get; private set; }
        public static LocalizedText AndroidClassLabel { get; private set; }
        public static LocalizedText AndroidStatusLabel { get; private set; }
        public static LocalizedText AndroidStatusLost { get; private set; }

        //科尔托标记
        public static LocalizedText KortoSystemLabel { get; private set; }
        public static LocalizedText KortoTargetLocked { get; private set; }
        public static LocalizedText KortoStarLabel { get; private set; }
        public static LocalizedText KortoIIILabel { get; private set; }
        public static LocalizedText KortoPrimaryObjective { get; private set; }
        public static LocalizedText KortoClassTerrestrial { get; private set; }
        public static LocalizedText KortoThreatCritical { get; private set; }
        public static LocalizedText KortoPlanetCountInfo { get; private set; }
        public static LocalizedText KortoStatusCompromised { get; private set; }

        #endregion

        public override void SetStaticDefaults() {
            HeaderStellarNavigation = this.GetLocalization(nameof(HeaderStellarNavigation), () => "◢ STELLAR NAVIGATION ◣");
            HeaderKortoSystemOverview = this.GetLocalization(nameof(HeaderKortoSystemOverview), () => "◢ KORTO SYSTEM OVERVIEW ◣");
            HeaderGalacticStrategicMap = this.GetLocalization(nameof(HeaderGalacticStrategicMap), () => "◢ GALACTIC STRATEGIC MAP ◣");

            MarkerTerra = this.GetLocalization(nameof(MarkerTerra), () => "TERRA");

            HeaderAndroidProfile = this.GetLocalization(nameof(HeaderAndroidProfile), () => "◢ TACTICAL ANDROID DOSSIER ◣");
            AndroidArtisName = this.GetLocalization(nameof(AndroidArtisName), () => "ARTIS");
            AndroidApolaName = this.GetLocalization(nameof(AndroidApolaName), () => "APOLA");
            AndroidCodename = this.GetLocalization(nameof(AndroidCodename), () => "CODENAME");
            AndroidClassLabel = this.GetLocalization(nameof(AndroidClassLabel), () => "CLASS: TACTICAL ANDROID");
            AndroidStatusLabel = this.GetLocalization(nameof(AndroidStatusLabel), () => "STATUS");
            AndroidStatusLost = this.GetLocalization(nameof(AndroidStatusLost), () => "SIGNAL LOST");

            ExtinctionProtocolWarning = this.GetLocalization(nameof(ExtinctionProtocolWarning), () => "◢ EXTINCTION PROTOCOL ACTIVE ◣");

            KortoSystemLabel = this.GetLocalization(nameof(KortoSystemLabel), () => "KORTO SYSTEM");
            KortoTargetLocked = this.GetLocalization(nameof(KortoTargetLocked), () => "◢ TARGET LOCKED ◣");
            KortoStarLabel = this.GetLocalization(nameof(KortoStarLabel), () => "KORTO");
            KortoIIILabel = this.GetLocalization(nameof(KortoIIILabel), () => "KORTO-III");
            KortoPrimaryObjective = this.GetLocalization(nameof(KortoPrimaryObjective), () => "◢ PRIMARY OBJECTIVE ◣");
            KortoClassTerrestrial = this.GetLocalization(nameof(KortoClassTerrestrial), () => "CLASS: TERRESTRIAL");
            KortoThreatCritical = this.GetLocalization(nameof(KortoThreatCritical), () => "THREAT: CRITICAL");
            KortoPlanetCountInfo = this.GetLocalization(nameof(KortoPlanetCountInfo), () => "KORTO SYSTEM \u2014 6 PLANETS");
            KortoStatusCompromised = this.GetLocalization(nameof(KortoStatusCompromised), () => "STATUS: COMPROMISED");
        }

        #region 动画阶段定义

        internal enum AnimPhase
        {
            None = 0,
            GalaxyReveal,
            SwarmApproach,
            ExtinctionProtocol,
            Idle,
            KortoZoom,
            KortoPlanetView,
            AndroidProfile,
            FadeOut,
        }

        #endregion

        #region 全局状态

        private static bool active;
        private static float fadeProgress;
        private static AnimPhase currentPhase = AnimPhase.None;
        private static float phaseTimer;
        private static float phaseProgress;

        //全息投影通用效果
        private static float hologramFlicker;
        private static float scanLineProgress;
        private static float globalTimer;

        //面板参数
        private const float BasePanelWidth = 620f;
        private const float BasePanelHeight = 580f;
        private const float ExpandedPanelWidth = 860f;
        private const float ExpandedPanelHeight = 740f;
        //科尔托行星视图的超宽面板尺寸
        private const float KortoPanelWidth = 1100f;
        private const float KortoPanelHeight = 480f;
        //战术人形信息展示面板尺寸
        private const float AndroidPanelWidth = 900f;
        private const float AndroidPanelHeight = 620f;
        private const int BorderThickness = 3;
        private static float panelExpandProgress;
        private static float kortoPanelExpandProgress;
        private static float androidPanelExpandProgress;

        //信号干扰
        private static float glitchIntensity;
        private static float glitchTimer;
        private static int glitchFrameSkip;

        #endregion

        #region 生命周期

        internal static void Activate() {
            active = true;
            fadeProgress = 0f;
            currentPhase = AnimPhase.None;
            phaseTimer = 0f;
            phaseProgress = 0f;
            hologramFlicker = 0f;
            scanLineProgress = 0f;
            globalTimer = 0f;
            glitchIntensity = 0f;
            glitchTimer = 0f;
            glitchFrameSkip = 0;
            panelExpandProgress = 0f;
            kortoPanelExpandProgress = 0f;
            androidPanelExpandProgress = 0f;

            InitGalaxy();
            InitSwarm();
            InitExtinction();
            InitKorto();
            InitAndroid();
        }

        internal static void SetPhase(AnimPhase phase) {
            if (currentPhase == phase) return;
            currentPhase = phase;
            phaseTimer = 0f;
            phaseProgress = 0f;

            switch (phase) {
                case AnimPhase.GalaxyReveal:
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalSpawnEnemy with {
                        Volume = 0.5f,
                        Pitch = 0.4f,
                        MaxInstances = 1
                    });
                    break;
                case AnimPhase.SwarmApproach:
                    SoundEngine.PlaySound(SoundID.Zombie105 with {
                        Volume = 0.3f,
                        Pitch = -0.6f,
                        MaxInstances = 1
                    });
                    break;
                case AnimPhase.ExtinctionProtocol:
                    SoundEngine.PlaySound(SoundID.Item117 with {
                        Volume = 0.6f,
                        Pitch = -0.3f,
                        MaxInstances = 1
                    });
                    break;
                case AnimPhase.KortoZoom:
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch with {
                        Volume = 0.4f,
                        Pitch = 0.3f,
                        MaxInstances = 1
                    });
                    break;
                case AnimPhase.KortoPlanetView:
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch with {
                        Volume = 0.3f,
                        Pitch = 0.5f,
                        MaxInstances = 1
                    });
                    break;
                case AnimPhase.AndroidProfile:
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch with {
                        Volume = 0.4f,
                        Pitch = 0.1f,
                        MaxInstances = 1
                    });
                    break;
            }
        }

        internal static void Deactivate() {
            active = false;
            currentPhase = AnimPhase.FadeOut;
            phaseTimer = 0f;
        }

        internal static void ForceCleanup() {
            active = false;
            fadeProgress = 0f;
            currentPhase = AnimPhase.None;
            CleanupGalaxy();
            CleanupSwarm();
            CleanupKorto();
            CleanupAndroid();
        }

        #endregion

        #region 逻辑更新

        public override void LogicUpdate() {
            if (!active && fadeProgress <= 0.01f) return;

            globalTimer += 0.016f;
            hologramFlicker += 0.06f;
            scanLineProgress += 0.025f;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;
            if (scanLineProgress > 1f) scanLineProgress -= 1f;

            phaseTimer += 1f;
            UpdatePhase();
            UpdateGlitch();

            //面板在虫群出现后或科尔托阶段平滑扩大
            float expandTarget = (currentPhase >= AnimPhase.SwarmApproach && currentPhase != AnimPhase.FadeOut) ? 1f : 0f;
            panelExpandProgress += (expandTarget - panelExpandProgress) * 0.04f;
            panelExpandProgress = MathHelper.Clamp(panelExpandProgress, 0f, 1f);

            //科尔托行星视图阶段面板进一步变宽变矮
            float kortoExpandTarget = (currentPhase == AnimPhase.KortoPlanetView && currentPhase != AnimPhase.FadeOut) ? 1f : 0f;
            kortoPanelExpandProgress += (kortoExpandTarget - kortoPanelExpandProgress) * 0.035f;
            kortoPanelExpandProgress = MathHelper.Clamp(kortoPanelExpandProgress, 0f, 1f);

            //战术人形档案阶段面板尺寸过渡
            float androidExpandTarget = (currentPhase == AnimPhase.AndroidProfile && currentPhase != AnimPhase.FadeOut) ? 1f : 0f;
            androidPanelExpandProgress += (androidExpandTarget - androidPanelExpandProgress) * 0.04f;
            androidPanelExpandProgress = MathHelper.Clamp(androidPanelExpandProgress, 0f, 1f);

            UpdateGalaxyLogic();
            UpdateSwarmLogic();
            UpdateExtinctionLogic();
            UpdateAndroidLogic();

            //整体淡入淡出
            if (active && currentPhase != AnimPhase.FadeOut) {
                fadeProgress = MathF.Min(fadeProgress + 0.03f, 1f);
            }
            else if (currentPhase == AnimPhase.FadeOut) {
                fadeProgress = MathF.Max(fadeProgress - 0.04f, 0f);
                if (fadeProgress <= 0.01f) {
                    ForceCleanup();
                }
            }

            if (!DraedonEffect.IsActive && active) {
                Deactivate();
            }
        }

        private static void UpdatePhase() {
            switch (currentPhase) {
                case AnimPhase.GalaxyReveal:
                    UpdateGalaxyRevealPhase();
                    break;
                case AnimPhase.SwarmApproach:
                    UpdateSwarmApproachPhase();
                    break;
                case AnimPhase.ExtinctionProtocol:
                    UpdateExtinctionPhase();
                    break;
                case AnimPhase.Idle:
                    UpdateIdlePhase();
                    break;
                case AnimPhase.KortoZoom:
                    UpdateKortoZoomPhase();
                    break;
                case AnimPhase.KortoPlanetView:
                    UpdateKortoPlanetViewPhase();
                    break;
                case AnimPhase.AndroidProfile:
                    UpdateAndroidProfilePhase();
                    break;
            }
        }

        private static void UpdateGlitch() {
            glitchTimer += 0.1f;
            if (Main.rand.NextFloat() < glitchIntensity * 0.3f) {
                glitchFrameSkip = Main.rand.Next(1, 4);
            }
            if (glitchFrameSkip > 0) {
                glitchFrameSkip--;
            }
        }

        #endregion

        #region 绘制调度

        private static Rectangle GetPanelRect() {
            float ease = VaultUtils.EaseOutCubic(panelExpandProgress);
            float w = MathHelper.Lerp(BasePanelWidth, ExpandedPanelWidth, ease);
            float h = MathHelper.Lerp(BasePanelHeight, ExpandedPanelHeight, ease);

            //科尔托行星视图：从扩展尺寸进一步变宽变矮
            if (kortoPanelExpandProgress > 0.01f) {
                float kortoEase = VaultUtils.EaseInOutCubic(kortoPanelExpandProgress);
                w = MathHelper.Lerp(w, KortoPanelWidth, kortoEase);
                h = MathHelper.Lerp(h, KortoPanelHeight, kortoEase);
            }

            //战术人形档案：独立面板尺寸
            if (androidPanelExpandProgress > 0.01f) {
                float androidEase = VaultUtils.EaseInOutCubic(androidPanelExpandProgress);
                w = MathHelper.Lerp(w, AndroidPanelWidth, androidEase);
                h = MathHelper.Lerp(h, AndroidPanelHeight, androidEase);
            }

            int x = (int)(Main.screenWidth * 0.5f - w * 0.5f);
            //顶部紧贴屏幕上方，留8px边距
            int y = 8;
            return new Rectangle(x, y, (int)w, (int)h);
        }

        private static Vector2 GetMapCenter() {
            Rectangle panel = GetPanelRect();
            return new Vector2(panel.X + panel.Width * 0.5f, panel.Y + panel.Height * 0.5f);
        }

        public override void Draw(SpriteBatch sb) {
            if (fadeProgress <= 0.01f) return;
            //不整帧跳过，改由glitchIntensity影响噪声强度而非可见性

            float alpha = fadeProgress;
            float flicker = MathF.Sin(hologramFlicker * 1.5f) * 0.04f + 0.96f;
            alpha *= flicker;

            Vector2 center = GetMapCenter();
            Rectangle panelRect = GetPanelRect();

            DrawPanelBackground(sb, panelRect, alpha);
            DrawPanelBorder(sb, panelRect, alpha);

            //开启剪裁，防止虫群/银河系等内容溢出面板边界
            int clipMargin = 4;
            Rectangle clipRect = new(panelRect.X + clipMargin, panelRect.Y + clipMargin,
                panelRect.Width - clipMargin * 2, panelRect.Height - clipMargin * 2);
            Rectangle origScissorRect = sb.GraphicsDevice.ScissorRectangle;
            RasterizerState scissorRaster = new() { ScissorTestEnable = true };

            sb.End();
            Rectangle safeClipRect = Rectangle.Intersect(clipRect, sb.GraphicsDevice.Viewport.Bounds);
            sb.GraphicsDevice.ScissorRectangle = VaultUtils.GetClippingRectangle(sb, safeClipRect);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, scissorRaster, null, Main.UIScaleMatrix);

            //战术人形档案阶段
            if (currentPhase == AnimPhase.AndroidProfile) {
                DrawAndroidProfile(sb, center, panelRect, alpha);
            }
            //科尔托行星视图阶段：完全替换银河系绘制
            else if (currentPhase == AnimPhase.KortoPlanetView && kortoPlanetTransition > 0.99f) {
                DrawKortoPlanetView(sb, center, alpha);
            }
            //科尔托缩放阶段：使用缩放版银河系绘制
            else if (currentPhase == AnimPhase.KortoZoom || (currentPhase == AnimPhase.KortoPlanetView && kortoPlanetTransition <= 0.99f)) {
                //银河系淡出（行星视图过渡中）
                float galaxyFade = currentPhase == AnimPhase.KortoPlanetView ? 1f - kortoPlanetTransition : 1f;
                if (galaxyFade > 0.01f) {
                    DrawKortoZoomGalaxy(sb, center, alpha * galaxyFade);
                }
                //行星视图淡入
                if (currentPhase == AnimPhase.KortoPlanetView && kortoPlanetTransition > 0.01f) {
                    DrawKortoPlanetView(sb, center, alpha);
                }
            }
            //正常银河系绘制
            else {
                if (galaxyRevealProgress > 0.01f) {
                    DrawGalaxy(sb, center, alpha);
                }

                if (swarmApproachProgress > 0.01f || currentPhase == AnimPhase.Idle) {
                    DrawSwarm(sb, center, alpha);
                }

                if (extinctionProgress > 0.01f) {
                    DrawExtinctionOverlay(sb, center, alpha);
                }

                if (galaxyRevealProgress > 0.5f) {
                    DrawTerraMarker(sb, center, alpha);
                }
            }

            //结束剪裁，恢复原始状态
            sb.End();
            sb.GraphicsDevice.ScissorRectangle = origScissorRect;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            DrawScanLineEffect(sb, panelRect, alpha);

            if (glitchIntensity > 0.02f) {
                DrawGlitchNoise(sb, panelRect, alpha);
            }

            DrawPanelHeader(sb, panelRect, alpha);
        }

        #endregion

        #region 面板UI绘制

        private static void DrawPanelBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //多层扩散阴影让面板有悬浮感
            for (int d = 9; d >= 1; d--) {
                Rectangle s = rect;
                s.Inflate(d, d);
                s.Offset(5, 6);
                sb.Draw(pixel, s, new Rectangle(0, 0, 1, 1),
                    Color.Black * (alpha * 0.05f * (9f - d) / 9f));
            }

            //纵向渐变背景（分段绘制，带脉冲呼吸）
            int segs = 24;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                float pulse = MathF.Sin(hologramFlicker * 0.5f + t * 1.8f) * 0.5f + 0.5f;
                Color c = Color.Lerp(new Color(4, 8, 18), new Color(9, 16, 30), pulse) * (alpha * 0.95f);
                sb.Draw(pixel, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    new Rectangle(0, 0, 1, 1), c);
            }

            //对角线底纹（45°极细线，替代空洞的纯色背景）
            float diagPhase = scanLineProgress * 20f;
            int diagSpacing = 22;
            for (int col = -(rect.Height / diagSpacing) - 1; col < (rect.Width / diagSpacing) + 2; col++) {
                int ox = (int)(col * diagSpacing + diagPhase % diagSpacing);
                for (int row = 0; row < rect.Height; row += 2) {
                    int px = rect.X + ox - row;
                    if (px < rect.X || px >= rect.Right) continue;
                    sb.Draw(pixel, new Rectangle(px, rect.Y + row, 1, 1),
                        new Rectangle(0, 0, 1, 1), new Color(20, 60, 90) * (alpha * 0.026f));
                }
            }

            //底部内发光（增加面板景深感）
            int glowH = rect.Height / 4;
            for (int i = 0; i < glowH; i++) {
                float fade = (1f - (float)i / glowH);
                fade *= fade;
                sb.Draw(pixel, new Rectangle(rect.X + 4, rect.Bottom - glowH + i, rect.Width - 8, 1),
                    new Rectangle(0, 0, 1, 1), new Color(8, 25, 48) * (alpha * 0.28f * fade));
            }

            //全息叠层随闪烁轻微呼吸
            float flicker = MathF.Sin(hologramFlicker * 1.5f) * 0.5f + 0.5f;
            sb.Draw(pixel, rect, new Rectangle(0, 0, 1, 1),
                new Color(8, 18, 32) * (alpha * 0.18f * flicker));
        }

        private static void DrawPanelBorder(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color techColor = new Color(60, 160, 220);
            float pulse = MathF.Sin(hologramFlicker * 2f) * 0.15f + 0.85f;

            //顶部主强调线（亮线加暗线双层，增加厚重感）
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3),
                new Rectangle(0, 0, 1, 1), techColor * (alpha * 0.95f * pulse));
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y + 3, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), techColor * (alpha * 0.38f));

            //底部细线
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2),
                new Rectangle(0, 0, 1, 1), techColor * (alpha * 0.55f * pulse));

            //左右边线
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height),
                new Rectangle(0, 0, 1, 1), techColor * (alpha * 0.72f));
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height),
                new Rectangle(0, 0, 1, 1), techColor * (alpha * 0.72f));

            //内层辅助线（略透明，制造双层边框视觉）
            Color borderDim = techColor * (alpha * 0.2f);
            sb.Draw(pixel, new Rectangle(rect.X + 5, rect.Y + 5, rect.Width - 10, 1), new Rectangle(0, 0, 1, 1), borderDim);
            sb.Draw(pixel, new Rectangle(rect.X + 5, rect.Bottom - 6, rect.Width - 10, 1), new Rectangle(0, 0, 1, 1), borderDim * 0.7f);
            sb.Draw(pixel, new Rectangle(rect.X + 5, rect.Y + 5, 1, rect.Height - 10), new Rectangle(0, 0, 1, 1), borderDim * 0.8f);
            sb.Draw(pixel, new Rectangle(rect.Right - 6, rect.Y + 5, 1, rect.Height - 10), new Rectangle(0, 0, 1, 1), borderDim * 0.8f);

            //四角L形括号（比十字更有科技感，方向各异）
            Color cornerColor = new Color(80, 200, 255) * (alpha * 0.82f * pulse);
            float cornerLen = 22f;
            DrawLBracket(sb, pixel, new Vector2(rect.X + 2, rect.Y + 2), cornerColor, cornerLen, false, false);
            DrawLBracket(sb, pixel, new Vector2(rect.Right - 2, rect.Y + 2), cornerColor, cornerLen, true, false);
            DrawLBracket(sb, pixel, new Vector2(rect.X + 2, rect.Bottom - 2), cornerColor, cornerLen, false, true);
            DrawLBracket(sb, pixel, new Vector2(rect.Right - 2, rect.Bottom - 2), cornerColor, cornerLen, true, true);

            //顶部刻痕（靠左，机械感点缀）
            Color notchColor = techColor * (alpha * 0.62f);
            sb.Draw(pixel, new Rectangle(rect.X + 6, rect.Y, 1, 9), new Rectangle(0, 0, 1, 1), notchColor);
            sb.Draw(pixel, new Rectangle(rect.X + 22, rect.Y, 1, 6), new Rectangle(0, 0, 1, 1), notchColor * 0.65f);
            sb.Draw(pixel, new Rectangle(rect.X + 38, rect.Y, 1, 4), new Rectangle(0, 0, 1, 1), notchColor * 0.38f);

            //右侧刻度尺（替代单调的空边）
            DrawSideTicks(sb, pixel, rect, alpha * 0.45f);
        }

        //L形括号角标（方向由flipX/flipY控制）
        private static void DrawLBracket(SpriteBatch sb, Texture2D pixel, Vector2 pos, Color color, float len, bool flipX, bool flipY) {
            int rx = flipX ? -1 : 1;
            int ry = flipY ? -1 : 1;
            //横线
            int lx = flipX ? (int)(pos.X - len + 2) : (int)pos.X;
            sb.Draw(pixel, new Rectangle(lx, (int)pos.Y, (int)len, 2), new Rectangle(0, 0, 1, 1), color);
            //竖线
            int ly = flipY ? (int)(pos.Y - len + 2) : (int)pos.Y;
            sb.Draw(pixel, new Rectangle((int)pos.X, ly, 2, (int)len), new Rectangle(0, 0, 1, 1), color * 0.92f);
            //竖线末端内侧短横（加强视觉收口感）
            int notchX = flipX ? (int)(pos.X - len + 2) : (int)(pos.X + len - 6);
            sb.Draw(pixel, new Rectangle(notchX, (int)pos.Y + 4 * ry, 5, 1), new Rectangle(0, 0, 1, 1), color * 0.48f);
        }

        //右侧刻度尺（流光向下循环，与扫描线周期一致）
        private static void DrawSideTicks(SpriteBatch sb, Texture2D pixel, Rectangle rect, float alpha) {
            int rx = rect.Right - 7;
            Color tickColor = new Color(60, 160, 220);
            int spacing = 12;
            int marks = (rect.Height - 50) / spacing;
            float flow = scanLineProgress * 0.5f;
            for (int i = 0; i < marks; i++) {
                float t = (float)i / marks;
                float bright = MathF.Sin((t + flow) * MathHelper.TwoPi) * 0.3f + 0.48f;
                int mLen = (i % 4 == 0) ? 7 : 4;
                sb.Draw(pixel, new Rectangle(rx - mLen, rect.Y + 35 + i * spacing, mLen, 1),
                    new Rectangle(0, 0, 1, 1), tickColor * (alpha * bright));
            }
        }

        private static void DrawPanelHeader(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color techColor = new Color(60, 160, 220);
            float pulse = MathF.Sin(hologramFlicker * 1.8f) * 0.12f + 0.88f;

            //标题栏背景（横向渐变，区别于主背景的单色矩形）
            Rectangle headerRect = new(rect.X + 3, rect.Y + 3, rect.Width - 6, 30);
            int hSegs = 20;
            for (int i = 0; i < hSegs; i++) {
                float t = i / (float)hSegs;
                int segX = headerRect.X + (int)(t * headerRect.Width);
                int segW = Math.Max(1, headerRect.Width / hSegs + 1);
                float brightness = (1f - t * 0.55f) * 0.55f + 0.28f;
                sb.Draw(pixel, new Rectangle(segX, headerRect.Y, segW, headerRect.Height),
                    new Rectangle(0, 0, 1, 1), new Color(8, 18, 34) * (alpha * brightness));
            }

            //标题栏下方分隔线（主亮线+辅助暗线）
            sb.Draw(pixel, new Rectangle(headerRect.X, headerRect.Bottom, headerRect.Width, 2),
                new Rectangle(0, 0, 1, 1), techColor * (alpha * 0.68f * pulse));
            sb.Draw(pixel, new Rectangle(headerRect.X, headerRect.Bottom + 3, headerRect.Width, 1),
                new Rectangle(0, 0, 1, 1), techColor * (alpha * 0.18f));

            //标题文字
            string title = currentPhase switch {
                AnimPhase.KortoZoom => HeaderStellarNavigation.Value,
                AnimPhase.KortoPlanetView => HeaderKortoSystemOverview.Value,
                AnimPhase.AndroidProfile => HeaderAndroidProfile.Value,
                _ => HeaderGalacticStrategicMap.Value
            };
            var font = FontAssets.MouseText.Value;
            Vector2 titleSize = font.MeasureString(title) * 0.45f;
            Vector2 titlePos = new(
                headerRect.X + (headerRect.Width - titleSize.X) * 0.5f,
                headerRect.Y + (headerRect.Height - titleSize.Y) * 0.5f
            );

            //辉光阴影
            for (int i = 0; i < 4; i++) {
                float a = MathHelper.TwoPi * i / 4f;
                Vector2 off = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 1.2f;
                Utils.DrawBorderString(sb, title, titlePos + off, techColor * (alpha * 0.42f), 0.45f);
            }
            Utils.DrawBorderString(sb, title, titlePos, Color.White * alpha, 0.45f);

            //左侧状态指示灯（3个颜色各异的小方块，交替闪烁）
            Color[] dotColors = [new Color(0, 200, 100), techColor, new Color(255, 165, 30)];
            float[] dotOffsets = [0f, 0.33f, 0.66f];
            for (int i = 0; i < 3; i++) {
                float blink = MathF.Sin(hologramFlicker * 3f + dotOffsets[i] * MathHelper.TwoPi) * 0.4f + 0.6f;
                sb.Draw(pixel, new Rectangle(headerRect.X + 8 + i * 9, headerRect.Y + headerRect.Height / 2 - 2, 5, 5),
                    new Rectangle(0, 0, 1, 1), dotColors[i] * (alpha * blink));
            }

            //右侧伪时间戳读出（让面板像真实终端设备）
            int fakeFrame = (int)(globalTimer * 30f) % 9999;
            string frameStr = $"T:{fakeFrame:D4}";
            float frameScale = 0.35f;
            Vector2 frameSize = font.MeasureString(frameStr) * frameScale;
            Utils.DrawBorderString(sb, frameStr,
                new Vector2(headerRect.Right - frameSize.X - 10, headerRect.Y + (headerRect.Height - frameSize.Y) * 0.5f),
                techColor * (alpha * 0.55f), frameScale);
        }

        //保留以兼容旧调用，当前DrawPanelBorder已改用DrawLBracket
        private static void DrawCornerDecor(SpriteBatch sb, Vector2 pos, Color color, float size, float rotation) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, rotation,
                new Vector2(0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.7f, rotation + MathHelper.PiOver2,
                new Vector2(0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
        }

        private static void DrawScanLineEffect(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            int contentTop = panelRect.Y + 38;
            int contentH = panelRect.Height - 42;

            //主扫描线（3层轻拖影，透明度降低避免喧宾夺主）
            float scanY = contentTop + scanLineProgress * contentH;
            for (int i = 0; i <= 2; i++) {
                float iy = scanY + i * 2f;
                if (iy < contentTop || iy > contentTop + contentH) continue;
                float fade = 1f - i * 0.38f;
                Color scanColor = new Color(60, 180, 255) * (alpha * 0.10f * fade);
                sb.Draw(pixel, new Vector2(panelRect.X + 6, iy), new Rectangle(0, 0, 1, 1),
                    scanColor, 0f, Vector2.Zero,
                    new Vector2(panelRect.Width - 12, i == 0 ? 2f : 1f), SpriteEffects.None, 0f);
            }

            //第二条辅助扫描线（青绿，极淡）
            float scan2Progress = (scanLineProgress + 0.5f) % 1f;
            float scan2Y = contentTop + scan2Progress * contentH;
            sb.Draw(pixel, new Vector2(panelRect.X + 6, scan2Y), new Rectangle(0, 0, 1, 1),
                new Color(0, 200, 180) * (alpha * 0.045f), 0f, Vector2.Zero,
                new Vector2(panelRect.Width - 12, 1f), SpriteEffects.None, 0f);

            //内容区水平网格线（间距50px，透明度进一步降低）
            int gridSpacing = 50;
            int gridCount = contentH / gridSpacing;
            for (int i = 0; i <= gridCount; i++) {
                int gy = contentTop + i * gridSpacing;
                sb.Draw(pixel, new Rectangle(panelRect.X + 6, gy, panelRect.Width - 12, 1),
                    new Rectangle(0, 0, 1, 1), new Color(28, 75, 115) * (alpha * 0.032f));
            }
        }

        private static void DrawGlitchNoise(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            int noiseCount = (int)(glitchIntensity * 50f);
            for (int i = 0; i < noiseCount; i++) {
                float x = panelRect.X + Main.rand.NextFloat(panelRect.Width);
                float y = panelRect.Y + Main.rand.NextFloat(panelRect.Height);
                float w = Main.rand.NextFloat(5f, 50f);
                float h = Main.rand.NextFloat(1f, 3f);

                Color noiseColor = Main.rand.NextBool()
                    ? new Color(80, 200, 255) * (alpha * 0.15f)
                    : new Color(200, 50, 50) * (alpha * 0.1f);

                sb.Draw(pixel, new Vector2(x, y), new Rectangle(0, 0, 1, 1),
                    noiseColor, 0f, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0f);
            }
        }

        #endregion
    }
}
