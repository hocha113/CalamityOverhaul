using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters;
using InnoVault.Storages;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors
{
    /// <summary>收集器控制台(绑定/投放模式/过滤)</summary>
    internal class CollectorUI : UIHandle, ILocalizedModType
    {
        //面板尺寸
        private const float PanelWidth = 440f;
        private const float PanelHeight = 428f;
        //面板与收集器的最大交互距离(像素)
        private const float PanelKeepDistance = 300f;

        //动画变量
        private float scanLineTimer;
        private float pulseTimer;
        private float glowTimer;
        private float warningFlashTimer;

        public static CollectorUI Instance => UIHandleLoader.GetUIHandleOfType<CollectorUI>();

        //淡入淡出走基类 OpenProgress；Active默认(IsOpen||进度>0)
        private float uiFadeAlpha => OpenProgress.Current;

        //拖拽功能
        private bool isDragging;
        private Vector2 dragOffset;

        //面板区域
        private Rectangle panelRect;
        private Rectangle bindingsRect;
        private Rectangle filterRect;
        private Rectangle statusRect;

        //按钮区域
        private Rectangle modeButton;
        private Rectangle addBindingButton;
        private Rectangle clearFilterButton;
        private Rectangle editFilterButton;
        private readonly Rectangle[] bindingRowRects = new Rectangle[CollectorTP.MaxBindings];
        private readonly Rectangle[] bindingUpButtons = new Rectangle[CollectorTP.MaxBindings];
        private readonly Rectangle[] bindingRemoveButtons = new Rectangle[CollectorTP.MaxBindings];

        //悬停状态
        private bool hoveringPanel;
        private bool hoveringMode;
        private bool hoveringAdd;
        private bool hoveringClearFilter;
        private bool hoveringEditFilter;
        private int hoveringRow = -1;
        private int hoveringUp = -1;
        private int hoveringRemove = -1;

        //本地化文本
        internal static LocalizedText TitleText;
        internal static LocalizedText ModeLabel;
        internal static LocalizedText ModeAuto;
        internal static LocalizedText ModeBoundFirst;
        internal static LocalizedText ModeBoundOnly;
        internal static LocalizedText ModeAutoDesc;
        internal static LocalizedText ModeBoundFirstDesc;
        internal static LocalizedText ModeBoundOnlyDesc;
        internal static LocalizedText BindingsLabel;
        internal static LocalizedText AddBindingText;
        internal static LocalizedText PickingTitle;
        internal static LocalizedText PickingHint;
        internal static LocalizedText PickingExitHint;
        internal static LocalizedText BindingFullText;
        internal static LocalizedText InvalidText;
        internal static LocalizedText OutOfRangeText;
        internal static LocalizedText ChestDefaultName;
        internal static LocalizedText MagicStorageName;
        internal static LocalizedText NoBindingsHint;
        internal static LocalizedText RemoveHint;
        internal static LocalizedText MoveUpHint;
        internal static LocalizedText FilterLabel;
        internal static LocalizedText FilterNoneText;
        internal static LocalizedText ClearFilterText;
        internal static LocalizedText EditFilterText;
        internal static LocalizedText FilterHint;
        internal static LocalizedText StatusLabel;
        internal static LocalizedText ArmCountLabel;
        internal static LocalizedText EnergyLabel;
        internal static LocalizedText StatusWorking;
        internal static LocalizedText StatusNoEnergy;
        internal static LocalizedText StatusStarting;
        internal static LocalizedText StatusNoStorage;
        internal static LocalizedText CloseHint;

        internal CollectorTP Station;
        /// <summary>世界选取绑定中(面板收起)</summary>
        internal bool PickingStorage;

        public string LocalizationCategory => "UI";

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "收集器控制台");
            ModeLabel = this.GetLocalization(nameof(ModeLabel), () => "存储模式");
            ModeAuto = this.GetLocalization(nameof(ModeAuto), () => "就近存储");
            ModeBoundFirst = this.GetLocalization(nameof(ModeBoundFirst), () => "绑定优先");
            ModeBoundOnly = this.GetLocalization(nameof(ModeBoundOnly), () => "仅限绑定");
            ModeAutoDesc = this.GetLocalization(nameof(ModeAutoDesc), () => "自动存入范围内最近的可用容器");
            ModeBoundFirstDesc = this.GetLocalization(nameof(ModeBoundFirstDesc), () => "优先存入绑定容器, 失败时就近存储");
            ModeBoundOnlyDesc = this.GetLocalization(nameof(ModeBoundOnlyDesc), () => "只存入绑定的容器");
            BindingsLabel = this.GetLocalization(nameof(BindingsLabel), () => "存储绑定");
            AddBindingText = this.GetLocalization(nameof(AddBindingText), () => "+ 绑定容器");
            PickingTitle = this.GetLocalization(nameof(PickingTitle), () => "选取存储容器");
            PickingHint = this.GetLocalization(nameof(PickingHint), () => "左键点击世界中的容器进行绑定");
            PickingExitHint = this.GetLocalization(nameof(PickingExitHint), () => "右键或[ESC]退出选取");
            BindingFullText = this.GetLocalization(nameof(BindingFullText), () => "绑定已满");
            InvalidText = this.GetLocalization(nameof(InvalidText), () => "失效");
            OutOfRangeText = this.GetLocalization(nameof(OutOfRangeText), () => "超出范围");
            ChestDefaultName = this.GetLocalization(nameof(ChestDefaultName), () => "箱子");
            MagicStorageName = this.GetLocalization(nameof(MagicStorageName), () => "存储核心");
            NoBindingsHint = this.GetLocalization(nameof(NoBindingsHint), () => "尚未绑定容器, 点击下方按钮开始");
            RemoveHint = this.GetLocalization(nameof(RemoveHint), () => "移除绑定");
            MoveUpHint = this.GetLocalization(nameof(MoveUpHint), () => "提升优先级");
            FilterLabel = this.GetLocalization(nameof(FilterLabel), () => "收集过滤");
            FilterNoneText = this.GetLocalization(nameof(FilterNoneText), () => "无过滤(收集所有物品)");
            ClearFilterText = this.GetLocalization(nameof(ClearFilterText), () => "清除");
            EditFilterText = this.GetLocalization(nameof(EditFilterText), () => "编辑");
            FilterHint = this.GetLocalization(nameof(FilterHint), () => "手持物品右键收集器可设定过滤目标");
            StatusLabel = this.GetLocalization(nameof(StatusLabel), () => "运行状态");
            ArmCountLabel = this.GetLocalization(nameof(ArmCountLabel), () => "机械臂");
            EnergyLabel = this.GetLocalization(nameof(EnergyLabel), () => "能量");
            StatusWorking = this.GetLocalization(nameof(StatusWorking), () => "运行中");
            StatusNoEnergy = this.GetLocalization(nameof(StatusNoEnergy), () => "能量不足");
            StatusStarting = this.GetLocalization(nameof(StatusStarting), () => "启动中");
            StatusNoStorage = this.GetLocalization(nameof(StatusNoStorage), () => "无可用存储");
            CloseHint = this.GetLocalization(nameof(CloseHint), () => "[ESC]或空手再次右键可关闭");
        }

        public void Initialize(CollectorTP collectorTP) {
            if (Station != collectorTP) {
                Station = collectorTP;
                DrawPosition = new Vector2(Main.screenWidth / 2, Main.screenHeight / 2);
                PickingStorage = false;
                Open();
            }
            else {
                PickingStorage = false;
                Toggle();
            }
        }

        #region 更新

        public override void Update() {
            if (uiFadeAlpha < 0.01f) {
                return;
            }

            //选取时放宽距离
            float keepDistance = PickingStorage ? CollectorTP.MaxBindDistance + 400f : PanelKeepDistance;
            if (IsOpen && (Station == null || !Station.Active
                || Station.PosInWorld.To(player.Center).Length() > keepDistance)) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f, Volume = 0.5f });
                PickingStorage = false;
                Close();
                return;
            }

            //更新动画
            scanLineTimer += 0.035f;
            pulseTimer += 0.025f;
            glowTimer += 0.04f;
            warningFlashTimer += 0.08f;
            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            if (glowTimer > MathHelper.TwoPi) glowTimer -= MathHelper.TwoPi;
            if (warningFlashTimer > MathHelper.TwoPi) warningFlashTimer -= MathHelper.TwoPi;

            if (PickingStorage) {
                UpdatePickingMode();
                return;
            }

            //拖拽
            HandleDragging();

            //限制面板位置
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, PanelWidth / 2 + 10, Main.screenWidth - PanelWidth / 2 - 10);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, PanelHeight / 2 + 10, Main.screenHeight - PanelHeight / 2 - 10);

            //计算面板区域
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);

            //模式行
            modeButton = new Rectangle(panelRect.X + 110, panelRect.Y + 42, 96, 24);

            //绑定区
            bindingsRect = new Rectangle(panelRect.X + 15, panelRect.Y + 92, panelRect.Width - 30, 196);
            int rowHeight = 26;
            for (int i = 0; i < CollectorTP.MaxBindings; i++) {
                int rowY = bindingsRect.Y + 24 + i * rowHeight;
                bindingRowRects[i] = new Rectangle(bindingsRect.X + 6, rowY, bindingsRect.Width - 12, rowHeight - 3);
                bindingUpButtons[i] = new Rectangle(bindingsRect.Right - 58, rowY + 2, 20, 18);
                bindingRemoveButtons[i] = new Rectangle(bindingsRect.Right - 32, rowY + 2, 20, 18);
            }
            addBindingButton = new Rectangle(bindingsRect.X + 6, bindingsRect.Bottom - 26, 130, 22);

            //过滤区
            filterRect = new Rectangle(panelRect.X + 15, panelRect.Y + 296, panelRect.Width - 30, 54);
            clearFilterButton = new Rectangle(filterRect.Right - 66, filterRect.Y + 18, 56, 22);
            editFilterButton = new Rectangle(filterRect.Right - 130, filterRect.Y + 18, 56, 22);

            //状态区
            statusRect = new Rectangle(panelRect.X + 15, panelRect.Y + 358, panelRect.Width - 30, 58);

            //鼠标交互检测
            Point mousePoint = new Point(Main.mouseX, Main.mouseY);
            hoveringPanel = panelRect.Contains(mousePoint);
            hoveringMode = modeButton.Contains(mousePoint) && !isDragging;
            hoveringAdd = addBindingButton.Contains(mousePoint) && !isDragging;
            hoveringClearFilter = clearFilterButton.Contains(mousePoint) && !isDragging
                && Station.TagItemSign > ItemID.None;
            hoveringEditFilter = editFilterButton.Contains(mousePoint) && !isDragging
                && Station.FilterInstalled;
            hoveringRow = -1;
            hoveringUp = -1;
            hoveringRemove = -1;
            int bindingCount = Station.BoundStorages.Count;
            for (int i = 0; i < bindingCount && i < CollectorTP.MaxBindings; i++) {
                if (bindingUpButtons[i].Contains(mousePoint) && !isDragging) {
                    hoveringUp = i;
                }
                else if (bindingRemoveButtons[i].Contains(mousePoint) && !isDragging) {
                    hoveringRemove = i;
                }
                else if (bindingRowRects[i].Contains(mousePoint) && !isDragging) {
                    hoveringRow = i;
                }
            }

            //编辑器悬停时不抢点击
            if (ItemFilterEditorUI.Instance?.hoverInMainPage == true) {
                hoveringPanel = hoveringMode = hoveringAdd = false;
                hoveringClearFilter = hoveringEditFilter = false;
                hoveringRow = hoveringUp = hoveringRemove = -1;
            }

            hoverInMainPage = hoveringPanel;
            if (hoveringPanel) {
                player.mouseInterface = true;
            }

            HandleButtonClicks();

            //右键空白处或ESC关闭
            bool anyButtonHover = hoveringMode || hoveringAdd || hoveringClearFilter
                || hoveringEditFilter || hoveringUp >= 0 || hoveringRemove >= 0;
            if (hoveringPanel && keyRightPressState == KeyPressState.Pressed && !anyButtonHover) {
                Close();
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.7f });
                return;
            }
            if (EscapeJustPressed()) {
                Close();
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.7f });
            }
        }

        private static bool EscapeJustPressed() {
            return Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)
                && !Main.oldKeyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape);
        }

        private void UpdatePickingMode() {
            //选取模式下屏蔽物品使用，专注于世界点击
            player.mouseInterface = true;

            if (keyLeftPressState == KeyPressState.Pressed) {
                TryPickStorageAtMouse();
            }

            if (keyRightPressState == KeyPressState.Pressed || EscapeJustPressed()) {
                PickingStorage = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.6f });
            }
        }

        private void TryPickStorageAtMouse() {
            Point16 mouseTile = Main.MouseWorld.ToTileCoordinates16();
            if (!VaultUtils.SafeGetTopLeft(mouseTile.X, mouseTile.Y, out Point16 topLeft)
                || !StorageLoader.TryGetStorageTargetByPoint(topLeft, out IStorageProvider provider)) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f, Pitch = -0.4f });
                return;
            }

            if (Station.TryAddBinding(provider.Position)) {
                Station.SendData();
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = 0.2f });
                //绑定满了自动退出选取
                if (Station.BoundStorages.Count >= CollectorTP.MaxBindings) {
                    PickingStorage = false;
                }
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.6f, Pitch = -0.3f });
            }
        }

        private void HandleDragging() {
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);
            bool anyInteractive = hoveringMode || hoveringAdd || hoveringClearFilter
                || hoveringEditFilter || hoveringUp >= 0 || hoveringRemove >= 0 || hoveringRow >= 0;

            if (hoveringPanel && !anyInteractive
                && keyLeftPressState == KeyPressState.Pressed && !isDragging) {
                isDragging = true;
                dragOffset = DrawPosition - mousePos;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
            }

            if (isDragging) {
                DrawPosition = mousePos + dragOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    isDragging = false;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f });
                }
            }
        }

        private void HandleButtonClicks() {
            if (Station == null || keyLeftPressState != KeyPressState.Pressed) {
                return;
            }

            if (hoveringMode) {
                Station.StorageMode = (CollectorStorageMode)(((byte)Station.StorageMode + 1) % 3);
                Station.InvalidateStorageCache();
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringAdd) {
                if (Station.BoundStorages.Count < CollectorTP.MaxBindings) {
                    PickingStorage = true;
                    SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.7f, Pitch = 0.2f });
                }
                else {
                    SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.6f, Pitch = -0.3f });
                }
            }
            else if (hoveringClearFilter) {
                if (Station.FilterInstalled) {
                    Station.UninstallFilter();
                }
                else {
                    Station.TagItemSign = ItemID.None;
                    Station.SendData();
                }
                SoundEngine.PlaySound(CWRSound.Select with { Pitch = 0.2f });
            }
            else if (hoveringEditFilter) {
                //就地编辑机器自己的名单，不再需要拿过滤卡来回倒腾
                ItemFilterEditorUI.Instance?.OpenFor(Station);
                SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.6f, Pitch = 0.15f });
            }
            else if (hoveringUp >= 0) {
                Station.MoveBindingUp(hoveringUp);
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (hoveringRemove >= 0) {
                Station.RemoveBindingAt(hoveringRemove);
                Station.SendData();
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.6f });
            }
        }

        #endregion

        #region 显示信息

        private string GetModeName() {
            return Station.StorageMode switch {
                CollectorStorageMode.BoundFirst => ModeBoundFirst.Value,
                CollectorStorageMode.BoundOnly => ModeBoundOnly.Value,
                _ => ModeAuto.Value
            };
        }

        private string GetModeDesc() {
            return Station.StorageMode switch {
                CollectorStorageMode.BoundFirst => ModeBoundFirstDesc.Value,
                CollectorStorageMode.BoundOnly => ModeBoundOnlyDesc.Value,
                _ => ModeAutoDesc.Value
            };
        }

        private static string GetStorageDisplayName(IStorageProvider provider) {
            if (provider is ChestStorageProvider chestProvider) {
                string name = chestProvider.Chest?.name;
                return string.IsNullOrEmpty(name) ? ChestDefaultName.Value : name;
            }
            if (provider != null && provider.Identifier == "MagicStorage.StorageHeart") {
                return MagicStorageName.Value;
            }
            return provider?.Identifier ?? ChestDefaultName.Value;
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f || Station == null) {
                return;
            }

            if (PickingStorage) {
                DrawPickingOverlay(spriteBatch);
                return;
            }

            DrawMainPanel(spriteBatch);
            DrawBindings(spriteBatch);
            DrawFilterSection(spriteBatch);
            DrawStatusSection(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        private void DrawPickingOverlay(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);

            //顶部标题条
            string title = $"{PickingTitle.Value}  ({Station.BoundStorages.Count}/{CollectorTP.MaxBindings})";
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.9f;
            Vector2 titlePos = new Vector2(Main.screenWidth / 2 - titleSize.X / 2, 80);
            Rectangle titleBg = new Rectangle((int)titlePos.X - 14, (int)titlePos.Y - 8, (int)titleSize.X + 28, (int)titleSize.Y + 14);
            sb.Draw(px, titleBg, src, new Color(12, 8, 6) * (alpha * 0.9f));
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            Color edge = Color.Lerp(new Color(130, 65, 35), new Color(210, 120, 60), pulse) * alpha;
            sb.Draw(px, new Rectangle(titleBg.X, titleBg.Y, titleBg.Width, 2), src, edge);
            sb.Draw(px, new Rectangle(titleBg.X, titleBg.Bottom - 2, titleBg.Width, 2), src, edge * 0.6f);
            Utils.DrawBorderString(sb, title, titlePos, new Color(255, 200, 150) * alpha, 0.9f);

            //提示文本
            string hint1 = PickingHint.Value;
            string hint2 = PickingExitHint.Value;
            Vector2 hint1Size = FontAssets.MouseText.Value.MeasureString(hint1) * 0.7f;
            Vector2 hint2Size = FontAssets.MouseText.Value.MeasureString(hint2) * 0.7f;
            Utils.DrawBorderString(sb, hint1, new Vector2(Main.screenWidth / 2 - hint1Size.X / 2, titleBg.Bottom + 8)
                , new Color(230, 200, 170) * alpha, 0.7f);
            Utils.DrawBorderString(sb, hint2, new Vector2(Main.screenWidth / 2 - hint2Size.X / 2, titleBg.Bottom + 30)
                , new Color(180, 150, 120) * alpha, 0.7f);

            //鼠标准星
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);
            float crossPulse = 1f + (float)Math.Sin(glowTimer * 2.5f) * 0.25f;
            Color crossColor = new Color(255, 190, 100) * alpha;
            float armLen = 10f * crossPulse;
            const float gap = 5f;
            sb.Draw(px, mousePos + new Vector2(gap, 0), src, crossColor, 0f, new Vector2(0f, 0.5f), new Vector2(armLen, 2f), SpriteEffects.None, 0f);
            sb.Draw(px, mousePos - new Vector2(gap + armLen, 0), src, crossColor, 0f, new Vector2(0f, 0.5f), new Vector2(armLen, 2f), SpriteEffects.None, 0f);
            sb.Draw(px, mousePos + new Vector2(0, gap), src, crossColor, 0f, new Vector2(0.5f, 0f), new Vector2(2f, armLen), SpriteEffects.None, 0f);
            sb.Draw(px, mousePos - new Vector2(0, gap + armLen), src, crossColor, 0f, new Vector2(0.5f, 0f), new Vector2(2f, armLen), SpriteEffects.None, 0f);
        }

        private void DrawMainPanel(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);
            float alpha = uiFadeAlpha;

            //主背景，废土深色调渐变
            int segments = 46;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color darkBase = new Color(8, 6, 6);
                Color rustMid = new Color(22, 14, 10);
                Color warmEdge = new Color(35, 20, 14);

                float pulse = (float)Math.Sin(pulseTimer * 0.8f + t * 2.5f) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(darkBase, rustMid, pulse * 0.6f);
                Color finalColor = Color.Lerp(baseColor, warmEdge, t * 0.3f);
                finalColor *= alpha * 0.92f;

                sb.Draw(px, r, src, finalColor);
            }

            //扫描线
            float scanY = panelRect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * panelRect.Height + panelRect.Height * 0.5f;
            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < panelRect.Y || offsetY > panelRect.Bottom) continue;
                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = new Color(200, 100, 60) * (alpha * 0.08f * intensity);
                sb.Draw(px, new Rectangle(panelRect.X + 12, (int)offsetY, panelRect.Width - 24, i == 0 ? 2 : 1), src, scanColor);
            }

            //边框
            float framePulse = (float)Math.Sin(pulseTimer * 1.5f) * 0.5f + 0.5f;
            Color rustEdge = Color.Lerp(new Color(130, 65, 35), new Color(190, 100, 55), framePulse) * (alpha * 0.8f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 3), src, rustEdge);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 3, panelRect.Width, 3), src, rustEdge * 0.65f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 3, panelRect.Height), src, rustEdge * 0.8f);
            sb.Draw(px, new Rectangle(panelRect.Right - 3, panelRect.Y, 3, panelRect.Height), src, rustEdge * 0.8f);

            //标题
            string title = TitleText.Value;
            Vector2 titlePos = new Vector2(panelRect.Center.X, panelRect.Y + 22);
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.85f;
            Color glowColor = new Color(255, 150, 90) * (alpha * 0.55f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 2f;
                Utils.DrawBorderString(sb, title, titlePos - titleSize / 2 + offset, glowColor, 0.85f);
            }
            Utils.DrawBorderString(sb, title, titlePos - titleSize / 2, new Color(230, 200, 170) * alpha, 0.85f);

            //模式行
            Utils.DrawBorderString(sb, ModeLabel.Value, new Vector2(panelRect.X + 20, modeButton.Y + 4)
                , new Color(200, 160, 130) * alpha, 0.6f);
            DrawButton(sb, modeButton, GetModeName(), hoveringMode, alpha
                , Station.StorageMode == CollectorStorageMode.Auto
                    ? new Color(180, 140, 100)
                    : new Color(120, 200, 255));
            Utils.DrawBorderString(sb, GetModeDesc(), new Vector2(modeButton.Right + 12, modeButton.Y + 5)
                , new Color(150, 125, 105) * alpha, 0.52f);
        }

        private void DrawBindings(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);

            DrawSectionBox(sb, bindingsRect, alpha);
            Utils.DrawBorderString(sb, $"{BindingsLabel.Value}  {Station.BoundStorages.Count}/{CollectorTP.MaxBindings}"
                , new Vector2(bindingsRect.X + 8, bindingsRect.Y + 5), new Color(200, 160, 130) * alpha, 0.6f);

            int bindingCount = Station.BoundStorages.Count;
            if (bindingCount == 0) {
                string hint = NoBindingsHint.Value;
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.62f;
                float hintPulse = 0.5f + (float)Math.Sin(glowTimer) * 0.2f;
                Utils.DrawBorderString(sb, hint
                    , new Vector2(bindingsRect.Center.X - hintSize.X / 2, bindingsRect.Center.Y - 16)
                    , new Color(170, 140, 115) * (alpha * hintPulse + 0.3f * alpha), 0.62f);
            }

            for (int i = 0; i < bindingCount && i < CollectorTP.MaxBindings; i++) {
                Point16 pos = Station.BoundStorages[i];
                IStorageProvider provider = StorageLoader.GetStorageTargetByPoint(pos);
                bool inRange = Station.BindingInRange(pos);
                bool valid = provider != null && provider.IsValid && inRange;

                Rectangle rowRect = bindingRowRects[i];

                //行背景
                Color rowBg = hoveringRow == i ? new Color(32, 22, 16) : new Color(18, 12, 10);
                sb.Draw(px, rowRect, src, rowBg * (alpha * 0.9f));

                //有效性LED
                Vector2 ledPos = new Vector2(rowRect.X + 10, rowRect.Y + rowRect.Height / 2);
                Color ledColor;
                if (valid) {
                    ledColor = new Color(100, 255, 120) * (0.7f + (float)Math.Sin(glowTimer * 2f + i) * 0.3f);
                }
                else {
                    float flash = (float)Math.Sin(warningFlashTimer * 3f) * 0.5f + 0.5f;
                    ledColor = new Color(255, 70, 50) * (0.4f + flash * 0.6f);
                }
                sb.Draw(px, ledPos, src, ledColor * alpha, 0f, new Vector2(0.5f), 5f, SpriteEffects.None, 0f);

                //名称与坐标
                string name = $"{i + 1}. {GetStorageDisplayName(provider)} ({pos.X}, {pos.Y})";
                if (!valid) {
                    name += $"  [{(inRange ? InvalidText.Value : OutOfRangeText.Value)}]";
                }
                Color nameColor = valid ? new Color(230, 200, 170) : new Color(200, 110, 95);
                Utils.DrawBorderString(sb, name, new Vector2(rowRect.X + 22, rowRect.Y + 4), nameColor * alpha, 0.58f);

                //距离(格)
                int tileDist = (int)(Station.CenterInWorld.Distance(pos.ToWorldCoordinates()) / 16f);
                string distText = $"{tileDist}";
                Vector2 distSize = FontAssets.MouseText.Value.MeasureString(distText) * 0.55f;
                Utils.DrawBorderString(sb, distText
                    , new Vector2(bindingUpButtons[i].X - 12 - distSize.X, rowRect.Y + 5)
                    , new Color(170, 145, 120) * alpha, 0.55f);

                //上移/移除
                if (i > 0) {
                    DrawButton(sb, bindingUpButtons[i], "^", hoveringUp == i, alpha, new Color(180, 140, 100));
                }
                DrawButton(sb, bindingRemoveButtons[i], "x", hoveringRemove == i, alpha, new Color(220, 100, 70));
            }

            //添加绑定按钮
            bool full = Station.BoundStorages.Count >= CollectorTP.MaxBindings;
            DrawButton(sb, addBindingButton, full ? BindingFullText.Value : AddBindingText.Value
                , hoveringAdd && !full, alpha, full ? new Color(120, 90, 70) : new Color(120, 200, 130));
        }

        private void DrawFilterSection(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            DrawSectionBox(sb, filterRect, alpha);
            Utils.DrawBorderString(sb, FilterLabel.Value, new Vector2(filterRect.X + 8, filterRect.Y + 5)
                , new Color(200, 160, 130) * alpha, 0.6f);

            int tagItem = Station.TagItemSign;
            if (tagItem <= ItemID.None) {
                Utils.DrawBorderString(sb, FilterNoneText.Value, new Vector2(filterRect.X + 12, filterRect.Y + 26)
                    , new Color(150, 125, 105) * alpha, 0.55f);

                //空过滤时显示引导提示
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(FilterHint.Value) * 0.5f;
                Utils.DrawBorderString(sb, FilterHint.Value
                    , new Vector2(filterRect.Right - hintSize.X - 12, filterRect.Y + 6)
                    , new Color(130, 110, 95) * alpha, 0.5f);
            }
            else if (Station.FilterInstalled) {
                //过滤名单前几项
                IReadOnlyList<int> filterItems = Station.Filter.OrderedItems;
                int shown = Math.Min(filterItems.Count, 7);
                for (int i = 0; i < shown; i++) {
                    VaultUtils.SafeLoadItem(filterItems[i]);
                    VaultUtils.SimpleDrawItem(sb, filterItems[i]
                        , new Vector2(filterRect.X + 22 + i * 30, filterRect.Y + 34)
                        , itemWidth: 24, 0, 0, Color.White * alpha);
                }
                if (filterItems.Count > shown) {
                    Utils.DrawBorderString(sb, $"+{filterItems.Count - shown}"
                        , new Vector2(filterRect.X + 22 + shown * 30, filterRect.Y + 26)
                        , new Color(200, 170, 140) * alpha, 0.6f);
                }
                DrawButton(sb, editFilterButton, EditFilterText.Value, hoveringEditFilter, alpha, new Color(120, 200, 255));
                DrawButton(sb, clearFilterButton, ClearFilterText.Value, hoveringClearFilter, alpha, new Color(220, 100, 70));
            }
            else {
                //单一物品过滤
                VaultUtils.SafeLoadItem(tagItem);
                VaultUtils.SimpleDrawItem(sb, tagItem, new Vector2(filterRect.X + 24, filterRect.Y + 34)
                    , itemWidth: 26, 0, 0, Color.White * alpha);
                string itemName = Lang.GetItemNameValue(tagItem);
                Utils.DrawBorderString(sb, itemName, new Vector2(filterRect.X + 44, filterRect.Y + 26)
                    , new Color(230, 200, 170) * alpha, 0.58f);
                DrawButton(sb, clearFilterButton, ClearFilterText.Value, hoveringClearFilter, alpha, new Color(220, 100, 70));
            }
        }

        private void DrawStatusSection(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);

            DrawSectionBox(sb, statusRect, alpha);

            //状态判定
            Color ledColor;
            string statusText;
            if (!Station.workState) {
                ledColor = new Color(255, 200, 80);
                statusText = StatusStarting.Value;
            }
            else if (Station.MachineData.UEvalue < CollectorTP.consumeUE) {
                float flash = (float)Math.Sin(warningFlashTimer * 3f) * 0.5f + 0.5f;
                ledColor = new Color(255, 80, 50) * flash;
                statusText = StatusNoEnergy.Value;
            }
            else if (Station.GetStorageCandidates().Count == 0) {
                float flash = (float)Math.Sin(warningFlashTimer * 2f) * 0.5f + 0.5f;
                ledColor = new Color(255, 160, 60) * (0.5f + flash * 0.5f);
                statusText = StatusNoStorage.Value;
            }
            else {
                float pulse = (float)Math.Sin(glowTimer * 2f) * 0.3f + 0.7f;
                ledColor = new Color(100, 255, 100) * pulse;
                statusText = StatusWorking.Value;
            }

            //LED灯与状态文字
            Vector2 ledPos = new Vector2(statusRect.X + 16, statusRect.Y + 16);
            sb.Draw(px, ledPos, src, ledColor * alpha, 0f, new Vector2(0.5f), 8f, SpriteEffects.None, 0f);
            sb.Draw(px, ledPos, src, Color.White * (alpha * 0.3f), 0f, new Vector2(0.5f), 4f, SpriteEffects.None, 0f);
            Utils.DrawBorderString(sb, $"{StatusLabel.Value}: {statusText}"
                , new Vector2(statusRect.X + 30, statusRect.Y + 8), new Color(200, 170, 140) * alpha, 0.6f);

            //机械臂计数
            string armText = $"{ArmCountLabel.Value}: {Station.CountOwnedArms()}/3";
            Vector2 armSize = FontAssets.MouseText.Value.MeasureString(armText) * 0.6f;
            Utils.DrawBorderString(sb, armText, new Vector2(statusRect.Right - armSize.X - 14, statusRect.Y + 8)
                , new Color(180, 150, 120) * alpha, 0.6f);

            //能量条
            Rectangle barBg = new Rectangle(statusRect.X + 15, statusRect.Y + 32, statusRect.Width - 160, 16);
            sb.Draw(px, barBg, src, new Color(15, 10, 8) * (alpha * 0.9f));
            float ratio = MathHelper.Clamp(Station.MachineData.UEvalue / Station.MaxUEValue, 0f, 1f);
            int fillWidth = (int)((barBg.Width - 4) * ratio);
            if (fillWidth > 0) {
                Color fillColor = Color.Lerp(new Color(200, 80, 40), new Color(255, 200, 100), ratio);
                sb.Draw(px, new Rectangle(barBg.X + 2, barBg.Y + 2, fillWidth, barBg.Height - 4), src, fillColor * (alpha * 0.85f));
            }
            Color borderColor = new Color(100, 60, 40) * (alpha * 0.7f);
            sb.Draw(px, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1), src, borderColor);
            sb.Draw(px, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1), src, borderColor * 0.6f);
            sb.Draw(px, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height), src, borderColor * 0.8f);
            sb.Draw(px, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height), src, borderColor * 0.8f);

            string energyText = $"{EnergyLabel.Value}: {(int)Station.MachineData.UEvalue}/{(int)Station.MaxUEValue}UE";
            Utils.DrawBorderString(sb, energyText, new Vector2(barBg.Right + 10, barBg.Y), new Color(180, 150, 120) * alpha, 0.5f);
        }

        private void DrawHoverTips(SpriteBatch sb) {
            string tip = null;
            if (hoveringUp >= 0 && hoveringUp > 0) {
                tip = MoveUpHint.Value;
            }
            else if (hoveringRemove >= 0) {
                tip = RemoveHint.Value;
            }

            if (tip != null) {
                ShowTooltip(sb, tip);
            }

            //右上角关闭提示
            if (hoveringPanel && uiFadeAlpha > 0.9f) {
                string closeHint = CloseHint.Value;
                Vector2 closeSize = FontAssets.MouseText.Value.MeasureString(closeHint) * 0.62f;
                Utils.DrawBorderString(sb, closeHint
                    , new Vector2(panelRect.Right - closeSize.X - 8, panelRect.Y - 22)
                    , new Color(200, 170, 140) * (uiFadeAlpha * 0.75f), 0.62f);
            }
        }

        private void ShowTooltip(SpriteBatch sb, string text) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.7f;
            Vector2 textPos = mousePos + new Vector2(16, 16);

            Rectangle tooltipBg = new Rectangle((int)textPos.X - 8, (int)textPos.Y - 4, (int)textSize.X + 16, (int)textSize.Y + 8);
            sb.Draw(px, tooltipBg, src, new Color(12, 8, 6) * 0.95f);
            sb.Draw(px, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 2), src, new Color(160, 90, 50) * 0.8f);
            sb.Draw(px, new Rectangle(tooltipBg.X, tooltipBg.Y, 2, tooltipBg.Height), src, new Color(160, 90, 50) * 0.8f);

            Utils.DrawBorderString(sb, text, textPos, new Color(240, 210, 170), 0.7f);
        }

        private void DrawSectionBox(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);

            sb.Draw(px, rect, src, new Color(12, 8, 7) * (alpha * 0.85f));

            Color borderColor = new Color(100, 55, 35) * (alpha * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), src, borderColor);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), src, borderColor * 0.6f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), src, borderColor * 0.8f);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), src, borderColor * 0.8f);
        }

        private static void DrawButton(SpriteBatch sb, Rectangle rect, string text, bool hovering, float alpha, Color accent) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);

            Color bgColor = hovering ? new Color(50, 30, 20) : new Color(25, 16, 12);
            Color borderColor = hovering ? Color.Lerp(accent, Color.White, 0.3f) : accent * 0.7f;

            sb.Draw(px, rect, src, bgColor * (alpha * 0.9f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), src, borderColor * (alpha * 0.8f));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), src, borderColor * (alpha * 0.5f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), src, borderColor * (alpha * 0.7f));
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), src, borderColor * (alpha * 0.7f));

            Color textColor = hovering ? new Color(255, 220, 180) : new Color(210, 175, 140);
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.55f;
            Utils.DrawBorderString(sb, text, rect.Center.ToVector2() - textSize / 2, textColor * alpha, 0.55f);
        }

        #endregion
    }
}
