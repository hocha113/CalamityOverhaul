using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines;
using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors
{
    /// <summary>
    /// 阿波利娅英雄单位面板UI：星流风格 左侧屏幕图标触发，展示HP/伤害/指令/立绘
    /// </summary>
    internal class ApolliaHeroPanelUI : UIHandle, ILocalizedModType
    {
        #region 常量与尺寸

        private const float PanelWidth = 370f;
        private const float PanelHeight = 500f;
        private const float IconSize = 46f;
        private const float IconMarginLeft = 12f;
        private const float IconMarginTop = 260f;
        private const float PortraitAreaHeight = 185f;
        private const float HPBarHeight = 16f;
        private const float CommandBtnHeight = 30f;
        private const float CommandBtnWidth = 76f;
        private const float SectionPadding = 12f;

        #endregion

        #region 状态

        public static ApolliaHeroPanelUI Instance => UIHandleLoader.GetUIHandleOfType<ApolliaHeroPanelUI>();
        public string LocalizationCategory => "UI";

        /// <summary>英雄面板是否已解锁（对话完成后启用）</summary>
        internal bool Unlocked;

        /// <summary>面板是否展开</summary>
        private bool panelOpen;

        /// <summary>面板淡入淡出</summary>
        private float panelAlpha;

        /// <summary>图标淡入淡出</summary>
        private float iconAlpha;

        /// <summary>图标呼吸计时器</summary>
        private float iconPulseTimer;

        /// <summary>英雄数据引用</summary>
        internal ApolliaHeroData HeroData = new();

        public override bool Active => Unlocked && MachineWorld.Active;

        #endregion

        #region 飞入动画

        /// <summary>飞入动画阶段：0=未开始, 1=飞行中, 2=已完成</summary>
        private int flyPhase;
        /// <summary>飞入起点（屏幕坐标）</summary>
        private Vector2 flyStartPos;
        /// <summary>飞入动画进度 0~1</summary>
        private float flyProgress;
        /// <summary>飞入动画持续帧数</summary>
        private const float FlyDuration = 55f;
        /// <summary>飞入闪光尾迹计时器</summary>
        private float flyTrailTimer;
        /// <summary>飞入过程中的位置记录（</summary>
        private readonly List<Vector2> flyTrailPositions = [];

        /// <summary>

        /// 启动图标飞入动画：从玩家头顶飞到左侧

        /// </summary>
        internal void StartFlyIn() {
            Player p = Main.LocalPlayer;
            if (p == null) return;
            //起点：玩家头顶屏幕坐标
            flyStartPos = p.Top - Main.screenPosition + new Vector2(-10, -40);
            flyProgress = 0f;
            flyPhase = 1;
            flyTrailPositions.Clear();
            flyTrailTimer = 0f;
        }

        #endregion

        #region 动画

        private float starFlowTimer;
        private float nebulaPulseTimer;
        private float shimmerTimer;
        private float dataStreamTimer;
        private float hexGridTimer;
        private float energyPulseTimer;
        private float hpFillAnim;
        private bool hpFillTriggered;

        #endregion

        #region 粒子

        private readonly List<HeroUnitPRT> orbitParticles = [];
        private int orbitSpawnTimer;
        private readonly List<StarDustPRT> panelDusts = [];
        private int dustSpawnTimer;

        #endregion

        #region 交互区域

        private Rectangle iconRect;
        private Rectangle panelRect;
        private Rectangle portraitRect;
        private Rectangle hpBarRect;
        private Rectangle statsRect;
        private Rectangle commandRect;
        private readonly Rectangle[] commandBtnRects = new Rectangle[4];

        private bool hoveringIcon;
        private bool hoveringPanel;
        private int hoveringCommandIndex = -1;

        //拖拽
        private bool isDragging;
        private Vector2 dragOffset;

        #endregion

        #region 本地化

        private static LocalizedText TitleText;
        private static LocalizedText HPLabel;
        private static LocalizedText DamageLabel;
        private static LocalizedText DefenseLabel;
        private static LocalizedText CommandLabel;
        private static LocalizedText CmdFollow;
        private static LocalizedText CmdHold;
        private static LocalizedText CmdAggressive;
        private static LocalizedText CmdDefensive;

        #endregion

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "阿波利娅");
            HPLabel = this.GetLocalization(nameof(HPLabel), () => "生命值");
            DamageLabel = this.GetLocalization(nameof(DamageLabel), () => "伤害");
            DefenseLabel = this.GetLocalization(nameof(DefenseLabel), () => "防御");
            CommandLabel = this.GetLocalization(nameof(CommandLabel), () => "作战指令");
            CmdFollow = this.GetLocalization(nameof(CmdFollow), () => "跟随");
            CmdHold = this.GetLocalization(nameof(CmdHold), () => "驻守");
            CmdAggressive = this.GetLocalization(nameof(CmdAggressive), () => "进攻");
            CmdDefensive = this.GetLocalization(nameof(CmdDefensive), () => "防御");
        }

        #region Update

        public override void Update() {
            //飞入动画
            if (flyPhase == 1) {
                flyProgress += 1f / FlyDuration;
                flyTrailTimer += 0.1f;
                if (flyProgress >= 1f) {
                    flyProgress = 1f;
                    flyPhase = 2;
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = 0.3f });
                }
                //记录轨迹
                Vector2 targetPos = new(IconMarginLeft + IconSize * 0.5f, IconMarginTop + IconSize * 0.5f);
                Vector2 currentFly = GetFlyPosition(targetPos);
                flyTrailPositions.Add(currentFly);
                if (flyTrailPositions.Count > 12) flyTrailPositions.RemoveAt(0);
            }

            //图标始终可见（解锁后且飞入完成或飞行中）
            iconPulseTimer += 0.03f;
            if (iconPulseTimer > MathHelper.TwoPi) iconPulseTimer -= MathHelper.TwoPi;

            bool showIcon = Unlocked && flyPhase >= 2;
            iconAlpha = showIcon ? Math.Min(1f, iconAlpha + 0.08f) : Math.Max(0f, iconAlpha - 0.08f);

            //面板淡入淡出
            if (panelOpen) {
                panelAlpha = Math.Min(1f, panelAlpha + 0.1f);
                if (!hpFillTriggered) {
                    hpFillAnim = 0f;
                    hpFillTriggered = true;
                }
                hpFillAnim = Math.Min(1f, hpFillAnim + 0.018f);
            }
            else {
                panelAlpha = Math.Max(0f, panelAlpha - 0.1f);
                if (panelAlpha <= 0f) hpFillTriggered = false;
            }

            //图标位置（左侧屏幕中部）
            iconRect = new Rectangle((int)IconMarginLeft, (int)IconMarginTop, (int)IconSize, (int)IconSize);

            //面板位置
            if (DrawPosition == Vector2.Zero) {
                DrawPosition = new Vector2(IconMarginLeft + IconSize + 8, IconMarginTop - 40);
            }

            //动画计时器
            starFlowTimer += 0.035f;
            nebulaPulseTimer += 0.02f;
            shimmerTimer += 0.028f;
            dataStreamTimer += 0.04f;
            hexGridTimer += 0.015f;
            energyPulseTimer += 0.045f;

            if (starFlowTimer > MathHelper.TwoPi) starFlowTimer -= MathHelper.TwoPi;
            if (nebulaPulseTimer > MathHelper.TwoPi) nebulaPulseTimer -= MathHelper.TwoPi;
            if (shimmerTimer > MathHelper.TwoPi) shimmerTimer -= MathHelper.TwoPi;
            if (dataStreamTimer > MathHelper.TwoPi) dataStreamTimer -= MathHelper.TwoPi;
            if (hexGridTimer > MathHelper.TwoPi) hexGridTimer -= MathHelper.TwoPi;
            if (energyPulseTimer > MathHelper.TwoPi) energyPulseTimer -= MathHelper.TwoPi;

            //计算子区域
            Vector2 topLeft = DrawPosition;
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);

            int curY = panelRect.Y + 44;
            portraitRect = new Rectangle(panelRect.X + (int)SectionPadding, curY, panelRect.Width - (int)(SectionPadding * 2), (int)PortraitAreaHeight);
            curY += (int)PortraitAreaHeight + 10;

            hpBarRect = new Rectangle(panelRect.X + (int)SectionPadding + 54, curY + 16, panelRect.Width - (int)(SectionPadding * 2) - 66, (int)HPBarHeight);
            curY += 44;

            statsRect = new Rectangle(panelRect.X + (int)SectionPadding, curY, panelRect.Width - (int)(SectionPadding * 2), 65);
            curY += 73;

            commandRect = new Rectangle(panelRect.X + (int)SectionPadding, curY, panelRect.Width - (int)(SectionPadding * 2), 84);

            //指令按钮布局 2x2
            int btnStartX = commandRect.X + 12;
            int btnStartY = commandRect.Y + 26;
            int btnGapX = (int)CommandBtnWidth + 10;
            int btnGapY = (int)CommandBtnHeight + 6;
            for (int i = 0; i < 4; i++) {
                int col = i % 2;
                int row = i / 2;
                commandBtnRects[i] = new Rectangle(
                    btnStartX + col * btnGapX,
                    btnStartY + row * btnGapY,
                    (int)CommandBtnWidth, (int)CommandBtnHeight);
            }

            //交互检测
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);
            hoveringIcon = iconRect.Contains(mousePos.ToPoint()) && flyPhase >= 2;

            if (panelAlpha > 0.01f) {
                hoveringPanel = panelRect.Contains(mousePos.ToPoint());
                hoveringCommandIndex = -1;
                if (!isDragging) {
                    for (int i = 0; i < 4; i++) {
                        if (commandBtnRects[i].Contains(mousePos.ToPoint())) {
                            hoveringCommandIndex = i;
                            break;
                        }
                    }
                }
            }
            else {
                hoveringPanel = false;
                hoveringCommandIndex = -1;
            }

            if (hoveringIcon || hoveringPanel && panelAlpha > 0.3f) {
                player.mouseInterface = true;
            }

            //图标点击
            if (hoveringIcon && UIHandleLoader.keyLeftPressState == KeyPressState.Pressed) {
                panelOpen = !panelOpen;
                SoundEngine.PlaySound(panelOpen ? SoundID.MenuOpen : SoundID.MenuClose);
            }

            //指令按钮点击
            if (panelAlpha > 0.5f && hoveringCommandIndex >= 0
                && UIHandleLoader.keyLeftPressState == KeyPressState.Pressed) {
                HeroData.CurrentCommand = (HeroCommand)hoveringCommandIndex;
                SoundEngine.PlaySound(SoundID.MenuTick);
            }

            //面板拖拽
            HandleDragging(mousePos);

            //限制面板位置
            if (panelOpen) {
                DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 10, Main.screenWidth - PanelWidth - 10);
                DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 10, Main.screenHeight - PanelHeight - 10);
            }

            //粒子更新
            UpdateParticles();
        }

        private void HandleDragging(Vector2 mousePos) {
            if (panelAlpha < 0.3f) {
                isDragging = false;
                return;
            }

            if (hoveringPanel && hoveringCommandIndex < 0
                && UIHandleLoader.keyLeftPressState == KeyPressState.Pressed
                && !isDragging && !hoveringIcon) {
                Rectangle titleDragArea = new(panelRect.X, panelRect.Y, panelRect.Width, 42);
                if (titleDragArea.Contains(mousePos.ToPoint())) {
                    isDragging = true;
                    dragOffset = DrawPosition - mousePos;
                }
            }

            if (isDragging) {
                DrawPosition = mousePos + dragOffset;
                if (UIHandleLoader.keyLeftPressState == KeyPressState.Released) {
                    isDragging = false;
                }
            }
        }

        private Vector2 GetFlyPosition(Vector2 target) {
            //三次缓出曲线
            float t = flyProgress;
            float eased = 1f - MathF.Pow(1f - t, 3f);
            //带弧线的飞行：中间偏上抛物
            Vector2 mid = (flyStartPos + target) * 0.5f + new Vector2(0, -80f);
            Vector2 p01 = Vector2.Lerp(flyStartPos, mid, eased);
            Vector2 p12 = Vector2.Lerp(mid, target, eased);
            return Vector2.Lerp(p01, p12, eased);
        }

        private void UpdateParticles() {
            if (panelAlpha < 0.2f) return;

            orbitSpawnTimer++;
            if (orbitSpawnTimer >= 35 && orbitParticles.Count < 8) {
                orbitSpawnTimer = 0;
                Vector2 center = new(portraitRect.Center.X, portraitRect.Center.Y);
                float radius = Main.rand.NextFloat(50f, 100f);
                orbitParticles.Add(new HeroUnitPRT(center, radius));
            }
            for (int i = orbitParticles.Count - 1; i >= 0; i--) {
                orbitParticles[i].Center = new Vector2(portraitRect.Center.X, portraitRect.Center.Y);
                if (orbitParticles[i].Update()) {
                    orbitParticles.RemoveAt(i);
                }
            }

            dustSpawnTimer++;
            if (dustSpawnTimer >= 40 && panelDusts.Count < 6) {
                dustSpawnTimer = 0;
                Vector2 start = new(
                    Main.rand.NextFloat(panelRect.X + 20, panelRect.Right - 20),
                    Main.rand.NextFloat(panelRect.Y + 60, panelRect.Bottom - 20));
                panelDusts.Add(new StarDustPRT(start));
            }
            Vector2 panelPos = new(panelRect.X, panelRect.Y);
            Vector2 panelSize = new(panelRect.Width, panelRect.Height);
            for (int i = panelDusts.Count - 1; i >= 0; i--) {
                if (panelDusts[i].Update(panelPos, panelSize)) {
                    panelDusts.RemoveAt(i);
                }
            }
        }

        #endregion

        #region Draw

        public override void Draw(SpriteBatch spriteBatch) {
            //飞入动画
            if (flyPhase == 1) {
                DrawFlyIn(spriteBatch);
            }

            //图标
            if (iconAlpha > 0.01f) {
                DrawIcon(spriteBatch);
            }

            //面板
            if (panelAlpha > 0.01f) {
                DrawPanel(spriteBatch);
            }
        }

        #region 飞入动画绘制

        private void DrawFlyIn(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 target = new(IconMarginLeft + IconSize * 0.5f, IconMarginTop + IconSize * 0.5f);
            Vector2 pos = GetFlyPosition(target);
            float alpha = MathHelper.Clamp(flyProgress * 2f, 0f, 1f);

            //拖尾光带
            for (int i = 0; i < flyTrailPositions.Count; i++) {
                float trailT = i / (float)flyTrailPositions.Count;
                float trailAlpha = trailT * 0.4f * alpha;
                float trailSize = 3f + trailT * 5f;
                Color trailColor = Color.Lerp(new Color(100, 160, 255), new Color(255, 210, 100), trailT) * trailAlpha;
                sb.Draw(px, flyTrailPositions[i], null, trailColor, 0f, new Vector2(0.5f),
                    new Vector2(trailSize, trailSize * 0.3f), SpriteEffects.None, 0f);
            }

            //核心光点
            float pulse = MathF.Sin(flyTrailTimer * 3f) * 0.3f + 0.7f;
            Color coreColor = new Color(255, 230, 140) * (alpha * pulse);
            sb.Draw(px, pos, null, coreColor, flyTrailTimer, new Vector2(0.5f),
                new Vector2(12f, 3f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, null, coreColor * 0.7f, flyTrailTimer + MathHelper.PiOver2, new Vector2(0.5f),
                new Vector2(12f, 3f), SpriteEffects.None, 0f);

            //图标预览
            Texture2D icon = ADVAsset.ApolliaIcon;
            if (icon != null && !icon.IsDisposed) {
                float iconScale = (IconSize - 8) / Math.Max(icon.Width, icon.Height) * alpha;
                sb.Draw(icon, pos, null, Color.White * (alpha * 0.8f), 0f,
                    new Vector2(icon.Width * 0.5f, icon.Height * 0.5f), iconScale, SpriteEffects.None, 0f);
            }

            //外圈光环
            if (CWRAsset.SoftGlow != null && !CWRAsset.SoftGlow.IsDisposed) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float glowScale = 40f / (glow.Width * 0.5f) * alpha;
                Color glowColor = new Color(120, 180, 255) * (alpha * 0.35f * pulse);
                sb.Draw(glow, pos, null, glowColor with { A = 0 }, 0f,
                    glow.Size() * 0.5f, glowScale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 图标绘制

        private void DrawIcon(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = iconAlpha;
            float pulse = MathF.Sin(iconPulseTimer * 1.8f) * 0.5f + 0.5f;

            //外层光晕底座
            if (CWRAsset.SoftGlow != null && !CWRAsset.SoftGlow.IsDisposed) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Vector2 center = iconRect.Center.ToVector2();
                float glowScale = (IconSize + 16) / (glow.Width * 0.5f);
                Color glowColor = Color.Lerp(new Color(60, 100, 200), new Color(200, 170, 80), pulse) * (alpha * 0.2f);
                sb.Draw(glow, center, null, glowColor with { A = 0 }, 0f,
                    glow.Size() * 0.5f, glowScale, SpriteEffects.None, 0f);
            }

            //深蓝底盘
            Color bgColor = Color.Lerp(new Color(6, 4, 16), new Color(12, 10, 28), pulse) * (alpha * 0.94f);
            sb.Draw(px, iconRect, new Rectangle(0, 0, 1, 1), bgColor);

            //绘制ApolliaIcon纹理
            Texture2D icon = ADVAsset.ApolliaIcon;
            if (icon != null && !icon.IsDisposed) {
                float iconScale = (IconSize - 10) / Math.Max(icon.Width, icon.Height);
                Vector2 iconCenter = iconRect.Center.ToVector2();
                Vector2 iconOrigin = new(icon.Width * 0.5f, icon.Height * 0.5f);
                sb.Draw(icon, iconCenter, null, Color.White * (alpha * 0.92f), 0f,
                    iconOrigin, iconScale, SpriteEffects.None, 0f);
            }

            //外框：双层金蓝渐变
            Color outerBorder = Color.Lerp(new Color(140, 120, 50), new Color(220, 190, 100), pulse) * (alpha * 0.9f);
            //外层 (2px)
            sb.Draw(px, new Rectangle(iconRect.X - 1, iconRect.Y - 1, iconRect.Width + 2, 2), new Rectangle(0, 0, 1, 1), outerBorder);
            sb.Draw(px, new Rectangle(iconRect.X - 1, iconRect.Bottom - 1, iconRect.Width + 2, 2), new Rectangle(0, 0, 1, 1), outerBorder * 0.7f);
            sb.Draw(px, new Rectangle(iconRect.X - 1, iconRect.Y - 1, 2, iconRect.Height + 2), new Rectangle(0, 0, 1, 1), outerBorder * 0.85f);
            sb.Draw(px, new Rectangle(iconRect.Right - 1, iconRect.Y - 1, 2, iconRect.Height + 2), new Rectangle(0, 0, 1, 1), outerBorder * 0.85f);

            //内层蓝光 (1px, 内缩3)
            Color innerBorder = new Color(100, 140, 220) * (alpha * 0.35f * (0.6f + pulse * 0.4f));
            Rectangle ib = iconRect;
            ib.Inflate(-3, -3);
            sb.Draw(px, new Rectangle(ib.X, ib.Y, ib.Width, 1), new Rectangle(0, 0, 1, 1), innerBorder);
            sb.Draw(px, new Rectangle(ib.X, ib.Bottom - 1, ib.Width, 1), new Rectangle(0, 0, 1, 1), innerBorder * 0.6f);
            sb.Draw(px, new Rectangle(ib.X, ib.Y, 1, ib.Height), new Rectangle(0, 0, 1, 1), innerBorder * 0.8f);
            sb.Draw(px, new Rectangle(ib.Right - 1, ib.Y, 1, ib.Height), new Rectangle(0, 0, 1, 1), innerBorder * 0.8f);

            //四角小星标
            float markSize = 3.5f;
            DrawHeroStarMark(sb, new Vector2(iconRect.X + 2, iconRect.Y + 2), alpha * 0.7f, markSize);
            DrawHeroStarMark(sb, new Vector2(iconRect.Right - 2, iconRect.Y + 2), alpha * 0.7f, markSize);
            DrawHeroStarMark(sb, new Vector2(iconRect.X + 2, iconRect.Bottom - 2), alpha * 0.5f, markSize);
            DrawHeroStarMark(sb, new Vector2(iconRect.Right - 2, iconRect.Bottom - 2), alpha * 0.5f, markSize);

            //顶部流光微线
            float flowT = shimmerTimer * 0.9f % 1f;
            int hlW = 20;
            int hlX = iconRect.X + (int)(flowT * (iconRect.Width - hlW));
            for (int dx = 0; dx < hlW; dx++) {
                float localT = dx / (float)hlW;
                float intensity = MathF.Sin(localT * MathHelper.Pi);
                Color hlC = new Color(200, 220, 255) * (alpha * 0.35f * intensity);
                sb.Draw(px, new Rectangle(hlX + dx, iconRect.Y - 1, 1, 2), new Rectangle(0, 0, 1, 1), hlC);
            }

            //悬停高亮
            if (hoveringIcon) {
                Color hover = new Color(255, 230, 140) * (alpha * 0.12f);
                sb.Draw(px, iconRect, new Rectangle(0, 0, 1, 1), hover);
                //悬停边框增强
                Color hoverBorder = new Color(255, 220, 100) * (alpha * 0.4f);
                sb.Draw(px, new Rectangle(iconRect.X, iconRect.Y, iconRect.Width, 1), new Rectangle(0, 0, 1, 1), hoverBorder);
                sb.Draw(px, new Rectangle(iconRect.X, iconRect.Bottom - 1, iconRect.Width, 1), new Rectangle(0, 0, 1, 1), hoverBorder * 0.6f);
            }
        }

        #endregion

        #region 面板绘制

        private void DrawPanel(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = panelAlpha;

            //多层阴影
            Rectangle shadow1 = panelRect;
            shadow1.Offset(6, 8);
            sb.Draw(px, shadow1, new Rectangle(0, 0, 1, 1), new Color(2, 0, 8) * (alpha * 0.4f));
            Rectangle shadow2 = panelRect;
            shadow2.Offset(3, 4);
            sb.Draw(px, shadow2, new Rectangle(0, 0, 1, 1), new Color(4, 2, 12) * (alpha * 0.3f));

            DrawPanelBackground(sb, alpha);
            DrawHexGrid(sb, panelRect, alpha * 0.5f);
            DrawDataStreams(sb, panelRect, alpha * 0.6f);
            DrawEnergyPulseLines(sb, panelRect, alpha * 0.5f);

            //内部微光
            float innerPulse = MathF.Sin(shimmerTimer * 0.9f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-4, -4);
            sb.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(150, 120, 50) * (alpha * 0.04f * innerPulse));

            DrawPanelFrame(sb, panelRect, alpha, innerPulse);

            //粒子层
            foreach (var dust in panelDusts) {
                dust.Draw(sb, alpha * 0.5f);
            }
            foreach (var orbit in orbitParticles) {
                orbit.Draw(sb, alpha * 0.6f);
            }

            DrawTitle(sb, alpha);
            DrawPortraitSection(sb, alpha);
            DrawHPBar(sb, alpha);
            DrawStatsSection(sb, alpha);
            DrawCommandSection(sb, alpha);
        }

        private void DrawPanelBackground(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int segs = 45;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color deep = new Color(4, 3, 14);
                Color mid = new Color(8, 7, 24);
                Color warm = new Color(16, 12, 36);

                float nebula = MathF.Sin(nebulaPulseTimer * 0.6f + t * 2.2f) * 0.5f + 0.5f;
                Color blended = Color.Lerp(deep, mid, nebula);
                Color c = Color.Lerp(blended, warm, t * t * 0.4f);
                c *= alpha * 0.95f;
                sb.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            //星云色斑
            float nebulaBreath = MathF.Sin(nebulaPulseTimer * 1.1f) * 0.5f + 0.5f;
            Color nebulaSpot = new Color(25, 8, 35) * (alpha * 0.18f * nebulaBreath);
            sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), nebulaSpot);
        }

        private void DrawHexGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int cols = 12;
            int rows = 16;
            float cellW = rect.Width / (float)cols;
            float cellH = rect.Height / (float)rows;

            for (int row = 0; row < rows; row++) {
                for (int col = 0; col < cols; col++) {
                    float phase = hexGridTimer + (row * 0.4f + col * 0.3f);
                    float brightness = MathF.Sin(phase) * 0.5f + 0.5f;
                    if (brightness < 0.3f) continue;

                    float x = rect.X + col * cellW + (row % 2 == 0 ? 0 : cellW * 0.5f);
                    float y = rect.Y + row * cellH;

                    Color dotColor = new Color(120, 100, 60) * (alpha * 0.04f * brightness);
                    sb.Draw(px, new Vector2(x, y), null, dotColor, 0f, new Vector2(0.5f),
                        new Vector2(2.2f), SpriteEffects.None, 0f);

                    //亮节点连接线
                    if (brightness > 0.7f && col + 1 < cols) {
                        float nextPhase = hexGridTimer + (row * 0.4f + (col + 1) * 0.3f);
                        float nextBright = MathF.Sin(nextPhase) * 0.5f + 0.5f;
                        if (nextBright > 0.7f) {
                            float nx = rect.X + (col + 1) * cellW + (row % 2 == 0 ? 0 : cellW * 0.5f);
                            Color lineC = new Color(100, 90, 50) * (alpha * 0.02f * brightness);
                            float len = nx - x;
                            float rot = 0f;
                            sb.Draw(px, new Vector2(x, y), new Rectangle(0, 0, 1, 1), lineC,
                                rot, new Vector2(0, 0.5f), new Vector2(len, 0.5f), SpriteEffects.None, 0f);
                        }
                    }
                }
            }
        }

        private void DrawDataStreams(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int lineCount = 4;

            for (int i = 0; i < lineCount; i++) {
                float t = (i + 1f) / (lineCount + 1f);
                float y = rect.Y + t * rect.Height;
                float flowOffset = dataStreamTimer * (0.4f + i * 0.18f) % 1f;
                int streamW = 50 + i * 10;
                int streamX = rect.X + (int)(flowOffset * (rect.Width - streamW));

                for (int dx = 0; dx < streamW; dx++) {
                    float localT = dx / (float)streamW;
                    float intensity = MathF.Sin(localT * MathHelper.Pi);
                    Color streamColor = Color.Lerp(
                        new Color(80, 120, 200),
                        new Color(220, 180, 80),
                        localT) * (alpha * 0.1f * intensity);
                    sb.Draw(px, new Rectangle(streamX + dx, (int)y, 1, 1), new Rectangle(0, 0, 1, 1), streamColor);
                }
            }
        }

        /// <summary>

        /// 能量脉冲线：竖向发光脉冲，从底部向上传播

        /// </summary>
        private void DrawEnergyPulseLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int pulseCount = 3;

            for (int i = 0; i < pulseCount; i++) {
                float xT = (i + 1f) / (pulseCount + 1f);
                float x = rect.X + xT * rect.Width;
                float pulseY = energyPulseTimer * (0.3f + i * 0.15f) % 1f;
                float yPos = rect.Bottom - pulseY * rect.Height;
                int pulseH = 30;

                for (int dy = 0; dy < pulseH; dy++) {
                    float localT = dy / (float)pulseH;
                    float intensity = MathF.Sin(localT * MathHelper.Pi);
                    Color pulseColor = Color.Lerp(
                        new Color(100, 80, 200),
                        new Color(200, 160, 80),
                        pulseY) * (alpha * 0.06f * intensity);
                    sb.Draw(px, new Rectangle((int)x, (int)yPos - dy, 1, 1), new Rectangle(0, 0, 1, 1), pulseColor);
                }
            }
        }

        private void DrawPanelFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //外框：蓝金混色
            Color outerEdge = Color.Lerp(
                new Color(100, 130, 180),
                new Color(220, 190, 100),
                pulse * 0.6f) * (alpha * 0.8f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), outerEdge * 0.65f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.85f);

            //第二层框 (内缩4px, 更亮的金色)
            Rectangle mid = rect;
            mid.Inflate(-4, -4);
            Color midC = new Color(180, 160, 80) * (alpha * 0.18f * pulse);
            sb.Draw(px, new Rectangle(mid.X, mid.Y, mid.Width, 1), new Rectangle(0, 0, 1, 1), midC);
            sb.Draw(px, new Rectangle(mid.X, mid.Bottom - 1, mid.Width, 1), new Rectangle(0, 0, 1, 1), midC * 0.55f);
            sb.Draw(px, new Rectangle(mid.X, mid.Y, 1, mid.Height), new Rectangle(0, 0, 1, 1), midC * 0.75f);
            sb.Draw(px, new Rectangle(mid.Right - 1, mid.Y, 1, mid.Height), new Rectangle(0, 0, 1, 1), midC * 0.75f);

            //内层蓝光框 (内缩8px)
            Rectangle innerF = rect;
            innerF.Inflate(-8, -8);
            Color innerC = new Color(80, 110, 180) * (alpha * 0.1f * (0.5f + pulse * 0.5f));
            sb.Draw(px, new Rectangle(innerF.X, innerF.Y, innerF.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(px, new Rectangle(innerF.X, innerF.Bottom - 1, innerF.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.5f);
            sb.Draw(px, new Rectangle(innerF.X, innerF.Y, 1, innerF.Height), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            sb.Draw(px, new Rectangle(innerF.Right - 1, innerF.Y, 1, innerF.Height), new Rectangle(0, 0, 1, 1), innerC * 0.7f);

            //角落星标
            DrawHeroStarMark(sb, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.85f, 5.5f);
            DrawHeroStarMark(sb, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.85f, 5.5f);
            DrawHeroStarMark(sb, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.6f, 5.5f);
            DrawHeroStarMark(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.6f, 5.5f);

            //顶部流光
            float flowT = shimmerTimer * 0.7f % 1f;
            int hlW = 70;
            int hlX = rect.X + (int)(flowT * (rect.Width - hlW));
            Color hlColor = new Color(180, 200, 255) * (alpha * 0.28f);
            for (int dx = 0; dx < hlW; dx++) {
                float localT = dx / (float)hlW;
                float intensity = MathF.Sin(localT * MathHelper.Pi);
                sb.Draw(px, new Rectangle(hlX + dx, rect.Y, 1, 2), new Rectangle(0, 0, 1, 1), hlColor * intensity);
            }

            //底部反向流光
            float flowB = (shimmerTimer * 0.5f + 0.5f) % 1f;
            int hlBX = rect.X + (int)((1f - flowB) * (rect.Width - hlW));
            Color hlBColor = new Color(200, 170, 80) * (alpha * 0.18f);
            for (int dx = 0; dx < hlW; dx++) {
                float localT = dx / (float)hlW;
                float intensity = MathF.Sin(localT * MathHelper.Pi);
                sb.Draw(px, new Rectangle(hlBX + dx, rect.Bottom - 2, 1, 2), new Rectangle(0, 0, 1, 1), hlBColor * intensity);
            }
        }

        private void DrawTitle(SpriteBatch sb, float alpha) {
            string title = $"\u2726 {TitleText.Value} \u2726";
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.9f;
            Vector2 titlePos = new(panelRect.X + panelRect.Width * 0.5f - titleSize.X * 0.5f, panelRect.Y + 12);

            //光晕
            Color glow = new Color(200, 180, 120) * (alpha * 0.5f);
            for (int i = 0; i < 5; i++) {
                float angle = MathHelper.TwoPi * i / 5f + shimmerTimer * 0.2f;
                Vector2 offset = angle.ToRotationVector2() * 2f;
                Utils.DrawBorderString(sb, title, titlePos + offset, glow, 0.9f);
            }
            Color titleColor = new Color(255, 245, 220) * alpha;
            Utils.DrawBorderString(sb, title, titlePos, titleColor, 0.9f);

            //双分割线
            float divY = panelRect.Y + 38;
            DrawGradientLine(sb,
                new Vector2(panelRect.X + SectionPadding, divY),
                new Vector2(panelRect.Right - SectionPadding, divY),
                new Color(180, 160, 80) * (alpha * 0.7f),
                new Color(80, 100, 160) * (alpha * 0.15f), 1.2f);
            DrawGradientLine(sb,
                new Vector2(panelRect.X + SectionPadding + 20, divY + 3),
                new Vector2(panelRect.Right - SectionPadding - 20, divY + 3),
                new Color(80, 100, 160) * (alpha * 0.2f),
                new Color(180, 160, 80) * (alpha * 0.08f), 0.8f);
        }

        #region 立绘区域

        private void DrawPortraitSection(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //立绘底框
            Color frameBg = new Color(4, 2, 10) * (alpha * 0.88f);
            sb.Draw(px, portraitRect, new Rectangle(0, 0, 1, 1), frameBg);

            //立绘框双层边线
            Color frameEdge = new Color(120, 140, 180) * (alpha * 0.45f);
            sb.Draw(px, new Rectangle(portraitRect.X, portraitRect.Y, portraitRect.Width, 1), new Rectangle(0, 0, 1, 1), frameEdge);
            sb.Draw(px, new Rectangle(portraitRect.X, portraitRect.Bottom - 1, portraitRect.Width, 1), new Rectangle(0, 0, 1, 1), frameEdge * 0.6f);
            sb.Draw(px, new Rectangle(portraitRect.X, portraitRect.Y, 1, portraitRect.Height), new Rectangle(0, 0, 1, 1), frameEdge * 0.8f);
            sb.Draw(px, new Rectangle(portraitRect.Right - 1, portraitRect.Y, 1, portraitRect.Height), new Rectangle(0, 0, 1, 1), frameEdge * 0.8f);
            //内层金色线
            Rectangle pInner = portraitRect;
            pInner.Inflate(-3, -3);
            Color pInnerC = new Color(180, 150, 60) * (alpha * 0.15f);
            sb.Draw(px, new Rectangle(pInner.X, pInner.Y, pInner.Width, 1), new Rectangle(0, 0, 1, 1), pInnerC);
            sb.Draw(px, new Rectangle(pInner.X, pInner.Bottom - 1, pInner.Width, 1), new Rectangle(0, 0, 1, 1), pInnerC * 0.5f);

            //绘制阿波利娅立绘
            Texture2D portrait = ADVAsset.Apollia;
            if (portrait != null && !portrait.IsDisposed) {
                float scaleToFit = Math.Min(
                    (float)portraitRect.Width / portrait.Width,
                    portraitRect.Height / (portrait.Height * 0.5f));
                scaleToFit = Math.Min(scaleToFit, 0.55f);

                int srcH = (int)(portraitRect.Height / scaleToFit);
                srcH = Math.Min(srcH, portrait.Height);
                Rectangle srcRect = new(0, 0, portrait.Width, srcH);

                Vector2 drawPos = new(
                    portraitRect.Center.X - portrait.Width * scaleToFit * 0.5f,
                    portraitRect.Y);

                sb.Draw(portrait, drawPos, srcRect, Color.White * (alpha * 0.92f),
                    0f, Vector2.Zero, scaleToFit, SpriteEffects.None, 0f);
            }

            //边缘暗角遮罩（四边渐变）
            int vignetteW = 25;
            for (int i = 0; i < vignetteW; i++) {
                float t = 1f - i / (float)vignetteW;
                Color mask = new Color(4, 2, 10) * (alpha * t * 0.7f);
                //左
                sb.Draw(px, new Rectangle(portraitRect.X + 1, portraitRect.Y + 1, 1, portraitRect.Height - 2), new Rectangle(0, 0, 1, 1), mask);
                //右
                sb.Draw(px, new Rectangle(portraitRect.Right - 2 - i, portraitRect.Y + 1, 1, portraitRect.Height - 2), new Rectangle(0, 0, 1, 1), mask);
            }

            //底部渐变遮罩
            int fadeH = 30;
            for (int i = 0; i < fadeH; i++) {
                float t = i / (float)fadeH;
                int y = portraitRect.Bottom - fadeH + i;
                Color mask = new Color(4, 2, 10) * (alpha * t * 0.95f);
                sb.Draw(px, new Rectangle(portraitRect.X + 1, y, portraitRect.Width - 2, 1), new Rectangle(0, 0, 1, 1), mask);
            }

            //立绘区角标
            DrawHeroStarMark(sb, new Vector2(portraitRect.X + 6, portraitRect.Y + 6), alpha * 0.5f, 3.5f);
            DrawHeroStarMark(sb, new Vector2(portraitRect.Right - 6, portraitRect.Y + 6), alpha * 0.5f, 3.5f);
        }

        #endregion

        #region HP条

        private void DrawHPBar(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float barRadius = 7f;

            //HP标签
            string hpLabel = HPLabel.Value;
            Vector2 labelPos = new(panelRect.X + SectionPadding, hpBarRect.Y - 1);
            Utils.DrawBorderString(sb, hpLabel, labelPos, new Color(200, 180, 120) * (alpha * 0.88f), 0.73f);

            //外层阴影
            Rectangle shadowRect = hpBarRect;
            shadowRect.Offset(2, 3);
            DrawRoundedRect(sb, shadowRect, new Color(2, 1, 8) * (alpha * 0.4f), barRadius + 1);

            //外层底座
            Rectangle barOuter = hpBarRect;
            barOuter.Inflate(2, 2);
            DrawRoundedRect(sb, barOuter, new Color(22, 18, 38) * (alpha * 0.85f), barRadius + 2);

            //内层底座
            DrawRoundedRect(sb, hpBarRect, new Color(6, 5, 14) * (alpha * 0.95f), barRadius);

            //HP填充（带动态填充动画）
            float ratio = HeroData.HPRatio;
            float easedFill = EaseOutCubic(hpFillAnim);
            float displayRatio = ratio * easedFill;
            int fillW = (int)((hpBarRect.Width - 4) * displayRatio);
            if (fillW > 2) {
                Rectangle fillRect = new(hpBarRect.X + 2, hpBarRect.Y + 2, fillW, hpBarRect.Height - 4);

                Color hpColor;
                if (ratio > 0.6f) {
                    hpColor = Color.Lerp(new Color(80, 200, 130), new Color(140, 210, 220), (ratio - 0.6f) / 0.4f);
                }
                else if (ratio > 0.3f) {
                    hpColor = Color.Lerp(new Color(220, 200, 60), new Color(80, 200, 130), (ratio - 0.3f) / 0.3f);
                }
                else {
                    hpColor = Color.Lerp(new Color(240, 60, 50), new Color(220, 200, 60), ratio / 0.3f);
                }
                hpColor *= alpha;
                DrawRoundedRect(sb, fillRect, hpColor, barRadius - 2);

                //顶部高光条（圆润内缩）
                float hlPulse = MathF.Sin(shimmerTimer * 1.5f) * 0.4f + 0.6f;
                Rectangle hlRect = new(fillRect.X + 2, fillRect.Y + 1, fillRect.Width - 4, 3);
                if (hlRect.Width > 2) {
                    DrawRoundedRect(sb, hlRect, Color.White * (alpha * 0.2f * hlPulse), 2f);
                }

                //底部暗影条
                Rectangle darkRect = new(fillRect.X + 2, fillRect.Bottom - 2, fillRect.Width - 4, 2);
                if (darkRect.Width > 2) {
                    DrawRoundedRect(sb, darkRect, new Color(0, 0, 0) * (alpha * 0.2f), 1f);
                }

                //发光层（SoftGlow在填充区域右端）
                if (hpFillAnim < 1f && CWRAsset.SoftGlow != null && !CWRAsset.SoftGlow.IsDisposed) {
                    Texture2D glow = CWRAsset.SoftGlow.Value;
                    Vector2 edgePos = new(fillRect.Right, fillRect.Center.Y);
                    float glowScale = (fillRect.Height + 8) / (glow.Height * 0.5f);
                    sb.Draw(glow, edgePos, null, hpColor with { A = 0 } * 0.5f, 0f,
                        glow.Size() * 0.5f, new Vector2(glowScale * 0.6f, glowScale), SpriteEffects.None, 0f);
                }

                //流光扫过
                if (hpFillAnim >= 1f) {
                    float scanT = shimmerTimer * 0.4f % 1f;
                    int scanPixel = fillRect.X + (int)(scanT * fillRect.Width);
                    int scanW = 18;
                    for (int dx = 0; dx < scanW && scanPixel + dx < fillRect.Right; dx++) {
                        float si = MathF.Sin(dx / (float)scanW * MathHelper.Pi);
                        sb.Draw(px, new Rectangle(scanPixel + dx, fillRect.Y + 1, 1, fillRect.Height - 2),
                            new Rectangle(0, 0, 1, 1), Color.White * (alpha * 0.1f * si));
                    }
                }
            }

            //圆润边框
            Color barBorder = Color.Lerp(new Color(100, 90, 55), new Color(140, 125, 70),
                MathF.Sin(shimmerTimer * 0.8f) * 0.5f + 0.5f) * (alpha * 0.55f);
            DrawRoundedRectBorder(sb, hpBarRect, barBorder, barRadius, 1);

            //HP数值文字（显示完整数字）
            string hpText = $"{(int)HeroData.HP}/{(int)HeroData.MaxHP}";
            Vector2 hpTextSize = FontAssets.MouseText.Value.MeasureString(hpText) * 0.6f;
            Vector2 hpTextPos = new(hpBarRect.Center.X - hpTextSize.X * 0.5f, hpBarRect.Y - 1);
            Utils.DrawBorderString(sb, hpText, hpTextPos + new Vector2(1, 1), new Color(0, 0, 0) * (alpha * 0.5f), 0.6f);
            Utils.DrawBorderString(sb, hpText, hpTextPos, new Color(255, 250, 230) * (alpha * 0.95f), 0.6f);
        }

        #endregion

        #region 属性面板

        private void DrawStatsSection(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float textScale = 0.76f;

            //伤害：带图标小标记
            Vector2 dmgPos = new(statsRect.X + 8, statsRect.Y + 6);
            //攻击力图标：小剑形
            Color iconC = new Color(255, 200, 80) * (alpha * 0.7f);
            sb.Draw(px, dmgPos + new Vector2(0, 5), null, iconC, MathHelper.PiOver4, new Vector2(0.5f),
                new Vector2(10f, 2f), SpriteEffects.None, 0f);
            sb.Draw(px, dmgPos + new Vector2(3, 2), null, iconC * 0.5f, -MathHelper.PiOver4, new Vector2(0.5f),
                new Vector2(5f, 1.5f), SpriteEffects.None, 0f);

            string dmgText = $"{DamageLabel.Value}: {(int)HeroData.BaseDamage}";
            Utils.DrawBorderString(sb, dmgText, dmgPos + new Vector2(16, 0), new Color(255, 215, 100) * (alpha * 0.92f), textScale);

            //防御：带盾形图标
            Vector2 defPos = new(statsRect.X + 8, statsRect.Y + 32);
            Color shieldC = new Color(120, 170, 220) * (alpha * 0.6f);
            sb.Draw(px, defPos + new Vector2(5, 5), null, shieldC, 0f, new Vector2(0.5f),
                new Vector2(8f, 10f), SpriteEffects.None, 0f);
            sb.Draw(px, defPos + new Vector2(5, 5), null, new Color(4, 2, 10) * (alpha * 0.5f), 0f, new Vector2(0.5f),
                new Vector2(5f, 7f), SpriteEffects.None, 0f);

            string defText = $"{DefenseLabel.Value}: {(int)HeroData.Defense}";
            Utils.DrawBorderString(sb, defText, defPos + new Vector2(16, 0), new Color(140, 185, 225) * (alpha * 0.92f), textScale);

            //分隔线
            DrawGradientLine(sb,
                new Vector2(statsRect.X, statsRect.Bottom),
                new Vector2(statsRect.Right, statsRect.Bottom),
                new Color(120, 100, 60) * (alpha * 0.45f),
                new Color(60, 80, 120) * (alpha * 0.12f), 1f);
        }

        #endregion

        #region 指令面板

        private void DrawCommandSection(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            string label = CommandLabel.Value;
            Vector2 labelPos = new(commandRect.X + 8, commandRect.Y + 4);
            Utils.DrawBorderString(sb, label, labelPos, new Color(200, 180, 120) * (alpha * 0.88f), 0.73f);

            string[] cmdNames = [CmdFollow.Value, CmdHold.Value, CmdAggressive.Value, CmdDefensive.Value];
            //每个指令的主题色
            Color[] cmdAccents = [
                new Color(100, 200, 160),
                new Color(180, 160, 80),
                new Color(220, 100, 80),
                new Color(100, 140, 220),
            ];

            for (int i = 0; i < 4; i++) {
                Rectangle btn = commandBtnRects[i];
                bool isActive = (int)HeroData.CurrentCommand == i;
                bool isHovering = hoveringCommandIndex == i;
                Color accent = cmdAccents[i];

                //按钮底色
                Color btnBg;
                if (isActive) {
                    float activePulse = MathF.Sin(shimmerTimer * 1.3f + i) * 0.3f + 0.7f;
                    btnBg = Color.Lerp(new Color(15, 12, 30), accent, 0.12f) * (alpha * 0.9f * activePulse);
                }
                else {
                    btnBg = new Color(10, 8, 20) * (alpha * 0.78f);
                }
                sb.Draw(px, btn, new Rectangle(0, 0, 1, 1), btnBg);

                //边框颜色
                Color btnBorder = isActive
                    ? accent * (alpha * 0.65f)
                    : new Color(70, 60, 45) * (alpha * 0.4f);
                if (isHovering && !isActive) {
                    btnBorder = accent * (alpha * 0.4f);
                }
                sb.Draw(px, new Rectangle(btn.X, btn.Y, btn.Width, 1), new Rectangle(0, 0, 1, 1), btnBorder);
                sb.Draw(px, new Rectangle(btn.X, btn.Bottom - 1, btn.Width, 1), new Rectangle(0, 0, 1, 1), btnBorder * 0.6f);
                sb.Draw(px, new Rectangle(btn.X, btn.Y, 1, btn.Height), new Rectangle(0, 0, 1, 1), btnBorder * 0.8f);
                sb.Draw(px, new Rectangle(btn.Right - 1, btn.Y, 1, btn.Height), new Rectangle(0, 0, 1, 1), btnBorder * 0.8f);

                //悬停高亮
                if (isHovering) {
                    sb.Draw(px, btn, new Rectangle(0, 0, 1, 1), accent * (alpha * 0.06f));
                }

                //活跃指示条（左侧竖条）
                if (isActive) {
                    float barPulse = MathF.Sin(shimmerTimer * 2f + i) * 0.3f + 0.7f;
                    sb.Draw(px, new Rectangle(btn.X + 1, btn.Y + 3, 3, btn.Height - 6),
                        new Rectangle(0, 0, 1, 1), accent * (alpha * 0.7f * barPulse));
                }

                //文字
                Color textColor = isActive
                    ? Color.Lerp(new Color(255, 250, 230), accent, 0.2f) * (alpha * 0.95f)
                    : new Color(150, 140, 105) * (alpha * 0.72f);
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(cmdNames[i]) * 0.7f;
                Vector2 textPos = new(btn.Center.X - textSize.X * 0.5f + (isActive ? 3 : 0), btn.Center.Y - textSize.Y * 0.5f);
                Utils.DrawBorderString(sb, cmdNames[i], textPos, textColor, 0.7f);
            }
        }

        #endregion

        #endregion

        #endregion

        #region 工具函数

        /// <summary>

        /// 六芒星标记

        /// </summary>
        private static void DrawHeroStarMark(SpriteBatch sb, Vector2 pos, float a, float size) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color c = new Color(180, 200, 255) * a;

            for (int i = 0; i < 3; i++) {
                float angle = MathHelper.TwoPi * i / 3f;
                sb.Draw(px, pos, null, c * (0.8f - i * 0.1f), angle, new Vector2(0.5f),
                    new Vector2(size * 1.2f, size * 0.18f), SpriteEffects.None, 0f);
            }

            Color bright = new Color(255, 245, 220) * (a * 0.6f);
            sb.Draw(px, pos, null, bright, 0f, new Vector2(0.5f),
                new Vector2(size * 0.3f), SpriteEffects.None, 0f);
        }

        private static void DrawGradientLine(SpriteBatch sb, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;

            edge.Normalize();
            float rotation = MathF.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 11f));

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                sb.Draw(px, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f),
                    new Vector2(segLength, thickness), SpriteEffects.None, 0f);
            }
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);

        private static void DrawRoundedRect(SpriteBatch sb, Rectangle rect, Color color, float radius) {
            Texture2D px = CWRAsset.Placeholder_White.Value;
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

            Rectangle center = new(rect.X + r, rect.Y, rect.Width - r * 2, rect.Height);
            sb.Draw(px, center, new Rectangle(0, 0, 1, 1), color);
            Rectangle left = new(rect.X, rect.Y + r, r, rect.Height - r * 2);
            Rectangle right = new(rect.Right - r, rect.Y + r, r, rect.Height - r * 2);
            sb.Draw(px, left, new Rectangle(0, 0, 1, 1), color);
            sb.Draw(px, right, new Rectangle(0, 0, 1, 1), color);

            for (int i = 0; i < r; i++) {
                float t = i / (float)r;
                int cw = (int)(r * MathF.Sqrt(1f - (1f - t) * (1f - t)));
                sb.Draw(px, new Rectangle(rect.X + r - cw, rect.Y + i, cw, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(px, new Rectangle(rect.Right - r, rect.Y + i, cw, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(px, new Rectangle(rect.X + r - cw, rect.Bottom - 1 - i, cw, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(px, new Rectangle(rect.Right - r, rect.Bottom - 1 - i, cw, 1), new Rectangle(0, 0, 1, 1), color);
            }
        }

        private static void DrawRoundedRectBorder(SpriteBatch sb, Rectangle rect, Color color, float radius, int thickness) {
            Texture2D px = CWRAsset.Placeholder_White.Value;
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

            sb.Draw(px, new Rectangle(rect.X + r, rect.Y, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(px, new Rectangle(rect.X + r, rect.Bottom - thickness, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(px, new Rectangle(rect.X, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(px, new Rectangle(rect.Right - thickness, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);

            for (int i = 0; i < r; i++) {
                float angle0 = MathHelper.PiOver2 * i / r;
                float angle1 = MathHelper.PiOver2 * (i + 1) / r;
                for (float a = angle0; a < angle1; a += 0.15f) {
                    float cx = MathF.Cos(a) * r;
                    float cy = MathF.Sin(a) * r;
                    sb.Draw(px, new Vector2(rect.Right - r + cx, rect.Y + r - cy), new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f), thickness, SpriteEffects.None, 0f);
                    sb.Draw(px, new Vector2(rect.X + r - cx, rect.Y + r - cy), new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f), thickness, SpriteEffects.None, 0f);
                    sb.Draw(px, new Vector2(rect.Right - r + cx, rect.Bottom - r + cy), new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f), thickness, SpriteEffects.None, 0f);
                    sb.Draw(px, new Vector2(rect.X + r - cx, rect.Bottom - r + cy), new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f), thickness, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion
    }
}
