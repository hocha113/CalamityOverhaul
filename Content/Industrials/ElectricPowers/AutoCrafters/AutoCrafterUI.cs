using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoCrafters
{
    /// <summary>
    /// 自动合成台面板:装配终端语言。左列样品口/成品口/电力表,
    /// 右区配方清单(按样品筛选,滚动,点击钉选/再点取消),底行状态灯与进度。<br/>
    /// 钉选编辑走"本地改 + SendData 推送"的客户端权威模型
    /// </summary>
    internal class AutoCrafterUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Items";

        #region 布局与状态
        private const float PanelWidth = 540f;
        private const float PanelHeight = 420f;
        private const int RowHeight = 30;
        private const int VisibleRows = 7;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color Amber => IndustrialTerminalRenderer.Amber;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color OkGreen => IndustrialTerminalRenderer.OkGreen;
        private static Color BrassBright => IndustrialTerminalRenderer.BrassBright;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        internal AutoCrafterTP CurrentTP;
        internal bool IsActive;

        //淡入淡出(Active 放宽到淡出结束,收摊有过程)
        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        //电力表指针弹簧
        private float powerDisplay;
        private float powerVel;
        private float latchHover;

        //拖拽
        private bool isDragging;
        private Vector2 dragOffset;
        private bool positionInitialized;

        //布局矩形
        private Rectangle panelRect;
        private Rectangle closeRect;
        private Rectangle sampleSlotRect;
        private Rectangle outputSlotRect;
        private Rectangle listRect;
        private Rectangle progressBarRect;
        private Vector2 powerGaugeCenter;
        private Rectangle powerGaugeRect;
        private bool hoveringSampleSlot;
        private bool hoveringOutputSlot;
        private bool hoveringPowerGauge;

        //配方清单缓存:样品 type 变化时重建
        private readonly List<Recipe> candidates = [];
        private int cachedSampleType = -1;
        private float scrollOffset;
        private float scrollTarget;

        private float animTimer;

        private AutoCrafterData CrafterData => CurrentTP?.CrafterData;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText SampleLabel;
        protected static LocalizedText OutputLabel;
        protected static LocalizedText ListLabel;
        protected static LocalizedText PowerLabel;
        protected static LocalizedText StatusIdle;
        protected static LocalizedText StatusNoPin;
        protected static LocalizedText StatusPinMissing;
        protected static LocalizedText StatusNoPower;
        protected static LocalizedText StatusNoStation;
        protected static LocalizedText StatusNoCondition;
        protected static LocalizedText StatusNoMaterial;
        protected static LocalizedText StatusOutputFull;
        protected static LocalizedText StatusWorking;
        protected static LocalizedText SampleHint;
        protected static LocalizedText ListEmptyText;
        protected static LocalizedText PinHint;
        protected static LocalizedText UnpinHint;
        protected static LocalizedText ConditionLockText;
        protected static LocalizedText PinnedTag;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "自动合成台");
            SampleLabel = this.GetLocalization(nameof(SampleLabel), () => "样品");
            OutputLabel = this.GetLocalization(nameof(OutputLabel), () => "成品");
            ListLabel = this.GetLocalization(nameof(ListLabel), () => "配方清单");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "电力");
            StatusIdle = this.GetLocalization(nameof(StatusIdle), () => "待机");
            StatusNoPin = this.GetLocalization(nameof(StatusNoPin), () => "未钉选配方");
            StatusPinMissing = this.GetLocalization(nameof(StatusPinMissing), () => "配方已失效,请重新钉选");
            StatusNoPower = this.GetLocalization(nameof(StatusNoPower), () => "缺电");
            StatusNoStation = this.GetLocalization(nameof(StatusNoStation), () => "附近缺少制作站");
            StatusNoCondition = this.GetLocalization(nameof(StatusNoCondition), () => "合成条件未满足");
            StatusNoMaterial = this.GetLocalization(nameof(StatusNoMaterial), () => "材料不足:");
            StatusOutputFull = this.GetLocalization(nameof(StatusOutputFull), () => "成品槽已满");
            StatusWorking = this.GetLocalization(nameof(StatusWorking), () => "装配中");
            SampleHint = this.GetLocalization(nameof(SampleHint), () => "放入样品物品,列出它的配方");
            ListEmptyText = this.GetLocalization(nameof(ListEmptyText), () => "样品没有对应配方");
            PinHint = this.GetLocalization(nameof(PinHint), () => "点击钉选这条配方");
            UnpinHint = this.GetLocalization(nameof(UnpinHint), () => "再次点击取消钉选");
            ConditionLockText = this.GetLocalization(nameof(ConditionLockText), () => "条件未满足");
            PinnedTag = this.GetLocalization(nameof(PinnedTag), () => "已钉选");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public void Interactive(AutoCrafterTP tp, bool newTP) {
            if (tp == null) {
                return;
            }

            if (CurrentTP == tp && !newTP) {
                IsActive = !IsActive;
            }
            else {
                IsActive = true;
            }

            CurrentTP = tp;
            cachedSampleType = -1;
            scrollOffset = scrollTarget = 0f;
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.3f, Pitch = -0.5f });
        }

        public override void Update() {
            if (!positionInitialized && Main.screenWidth > 0) {
                positionInitialized = true;
                if (DrawPosition.X < PanelWidth / 2 + 10 && DrawPosition.Y < PanelHeight / 2 + 10) {
                    DrawPosition = new Vector2(UIScreenW * 0.5f, UIScreenH * 0.5f);
                }
            }

            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, PanelWidth / 2 + 10, UIScreenW - PanelWidth / 2 - 10);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, PanelHeight / 2 + 10, UIScreenH - PanelHeight / 2 - 10);

            animTimer += 1f / 60f;

            float targetAlpha = IsActive ? 1f : 0f;
            uiFadeAlpha = MathHelper.Lerp(uiFadeAlpha, targetAlpha, 0.15f);
            if (uiFadeAlpha < 0.01f && !IsActive) {
                return;
            }

            //验证TP有效性
            if (CurrentTP == null || !CurrentTP.Active) {
                IsActive = false;
                return;
            }

            //检查距离
            if (Main.LocalPlayer.DistanceSQ(CurrentTP.CenterInWorld) > 40000) {
                IsActive = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
                return;
            }

            ComputeLayout();
            RebuildCandidates();
            UpdateEnvelopes();

            Point mouse = MousePosition.ToPoint();
            hoveringSampleSlot = sampleSlotRect.Contains(mouse) && !isDragging;
            hoveringOutputSlot = outputSlotRect.Contains(mouse) && !isDragging;
            hoveringPowerGauge = powerGaugeRect.Contains(mouse) && !isDragging;
            hoverInMainPage = panelRect.Contains(mouse);
            latchHover = MathHelper.Lerp(latchHover, closeRect.Contains(mouse) ? 1f : 0f, 0.2f);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                //两把锁都要:滚轮不换武器,背包开着时也不翻原版配方栏
                UIInputGuard.SuppressWeaponSwitch();
                PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/AutoCrafter");
            }

            //闩钮关闭
            if (closeRect.Contains(mouse) && keyLeftPressState == KeyPressState.Pressed) {
                IsActive = false;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f, Volume = 0.5f });
                return;
            }

            //清单滚动
            UpdateScroll(mouse);

            //背景区拖拽,避开槽口/清单/表盘/闩钮
            if (keyLeftPressState == KeyPressState.Pressed && hoverInMainPage
                && !hoveringSampleSlot && !hoveringOutputSlot && !hoveringPowerGauge
                && !listRect.Contains(mouse) && !closeRect.Contains(mouse) && !isDragging) {
                isDragging = true;
                dragOffset = MousePosition - DrawPosition;
            }
            if (isDragging) {
                DrawPosition = MousePosition - dragOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    isDragging = false;
                }
            }

            //样品口交互
            if (hoveringSampleSlot && CrafterData != null) {
                if (CrafterData.SampleItem != null && !CrafterData.SampleItem.IsAir) {
                    Main.HoverItem = CrafterData.SampleItem.Clone();
                    Main.hoverItemName = CrafterData.SampleItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    CurrentTP.HandleSampleItem();
                    cachedSampleType = -1;
                }
            }

            //成品口交互
            if (hoveringOutputSlot && CrafterData != null) {
                if (CrafterData.OutputItem != null && !CrafterData.OutputItem.IsAir) {
                    Main.HoverItem = CrafterData.OutputItem.Clone();
                    Main.hoverItemName = CrafterData.OutputItem.Name;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    CurrentTP.HandleOutputItem();
                }
            }

            //清单行点击:钉选/取消钉选
            if (keyLeftPressState == KeyPressState.Pressed && listRect.Contains(mouse) && !isDragging) {
                HandleListClick(mouse);
            }
        }

        private void ComputeLayout() {
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);
            closeRect = new Rectangle(panelRect.Right - 38, panelRect.Y + 9, 26, 26);
            sampleSlotRect = new Rectangle(panelRect.X + 30, panelRect.Y + 84, 64, 64);
            outputSlotRect = new Rectangle(panelRect.X + 30, panelRect.Y + 188, 64, 64);
            listRect = new Rectangle(panelRect.X + 126, panelRect.Y + 80, 388, 24 + RowHeight * VisibleRows);
            progressBarRect = new Rectangle(panelRect.X + 126, panelRect.Bottom - 66, 250, 8);
            powerGaugeCenter = new Vector2(panelRect.X + 62, panelRect.Y + 306);
            powerGaugeRect = new Rectangle((int)powerGaugeCenter.X - 32, (int)powerGaugeCenter.Y - 32, 64, 64);
        }

        /// <summary>样品变化时重建候选配方清单</summary>
        private void RebuildCandidates() {
            int sampleType = CrafterData?.SampleItem != null && !CrafterData.SampleItem.IsAir
                ? CrafterData.SampleItem.type : 0;
            if (sampleType == cachedSampleType) {
                return;
            }
            cachedSampleType = sampleType;
            candidates.Clear();
            if (sampleType > 0) {
                candidates.AddRange(AutoCrafterRecipeId.FindByResult(sampleType));
            }
            scrollOffset = scrollTarget = 0f;
        }

        private void UpdateEnvelopes() {
            AutoCrafterData data = CrafterData;
            float powerTarget = data != null ? MathHelper.Clamp(data.UEvalue / data.MaxUE, 0f, 1f) : 0f;
            powerVel = powerVel * 0.80f + (powerTarget - powerDisplay) * 0.05f;
            powerDisplay += powerVel;
        }

        private void UpdateScroll(Point mouse) {
            float maxScroll = Math.Max(0, candidates.Count - VisibleRows) * RowHeight;
            if (listRect.Contains(mouse)) {
                int delta = PlayerInput.ScrollWheelDeltaForUI;
                if (delta != 0) {
                    scrollTarget -= delta * 0.4f;
                }
            }
            scrollTarget = MathHelper.Clamp(scrollTarget, 0, maxScroll);
            scrollOffset = MathHelper.Lerp(scrollOffset, scrollTarget, 0.25f);
        }

        /// <summary>命中清单行:未钉选则钉选,已钉选则取消;条件锁死的行不可点</summary>
        private void HandleListClick(Point mouse) {
            int rowsTop = listRect.Y + 24;
            int index = (int)((mouse.Y - rowsTop + scrollOffset) / RowHeight);
            if (index < 0 || index >= candidates.Count) {
                return;
            }
            Recipe recipe = candidates[index];
            if (!RecipeConditionsMetLocal(recipe)) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
                return;
            }

            bool isPinned = IsPinnedRecipe(recipe);
            CurrentTP.PinRecipe(isPinned ? null : recipe);
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.4f, Pitch = isPinned ? -0.4f : 0.1f });
        }

        private bool IsPinnedRecipe(Recipe recipe) {
            if (CrafterData == null || CrafterData.PinnedResultType <= 0) {
                return false;
            }
            return recipe.createItem.type == CrafterData.PinnedResultType
                && recipe.createItem.stack == CrafterData.PinnedResultStack
                && AutoCrafterRecipeId.ComputeIngredientHash(recipe) == CrafterData.PinnedHash;
        }

        /// <summary>UI 端条件评估:异常按不满足;满足性以机器端(服务器)为准,这里只做灰显</summary>
        private static bool RecipeConditionsMetLocal(Recipe recipe) {
            foreach (var condition in recipe.Conditions) {
                bool met;
                try {
                    met = condition.IsMet();
                } catch {
                    met = false;
                }
                if (!met) {
                    return false;
                }
            }
            return true;
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["AutoCrafterUI_DrawPos_X"] = DrawPosition.X;
            tag["AutoCrafterUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("AutoCrafterUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            else {
                DrawPosition.X = Main.screenWidth / 2;
            }

            if (tag.TryGet("AutoCrafterUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
            else {
                DrawPosition.Y = Main.screenHeight / 2;
            }
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f) {
                return;
            }
            if (CrafterData == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawSlots(spriteBatch);
            DrawRecipeList(spriteBatch);
            DrawStatusRow(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        /// <summary>钢壳 + 铆钉 + 黄铜铭牌 + 闩钮</summary>
        private void DrawShell(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            IndustrialTerminalRenderer.ShaderPanel(sb, panelRect, alpha, mode: 0, heat: 0f);

            int inset = IndustrialTerminalRenderer.Chamfer + 2;
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Bottom - inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Bottom - inset), alpha);

            string title = TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.86f;
            Rectangle plate = new(panelRect.X + 22, panelRect.Y + 9, (int)titleSize.X + 30, 27);
            IndustrialTerminalRenderer.DrawNameplate(sb, plate, alpha);
            IndustrialTerminalRenderer.DrawPlateTitle(sb, plate, title, alpha, 0.86f);

            IndustrialTerminalRenderer.DrawEtchedLine(sb, panelRect.X + 14, panelRect.Width - 28, panelRect.Y + 44, alpha, 0.8f);
            IndustrialTerminalRenderer.DrawLatch(sb, closeRect.Center.ToVector2(), alpha, latchHover);
        }

        /// <summary>样品口与成品口 + 电力表</summary>
        private void DrawSlots(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            Vector2 inSize = FontAssets.MouseText.Value.MeasureString(SampleLabel.Value) * 0.62f;
            Utils.DrawBorderString(sb, SampleLabel.Value,
                new Vector2(sampleSlotRect.Center.X - inSize.X * 0.5f, sampleSlotRect.Y - 20), TextDim * alpha, 0.62f);
            Vector2 outSize = FontAssets.MouseText.Value.MeasureString(OutputLabel.Value) * 0.62f;
            Utils.DrawBorderString(sb, OutputLabel.Value,
                new Vector2(outputSlotRect.Center.X - outSize.X * 0.5f, outputSlotRect.Y - 20),
                Color.Lerp(TextDim, BrassBright, 0.4f) * alpha, 0.62f);

            IndustrialTerminalRenderer.DrawSocket(sb, sampleSlotRect, alpha, hoveringSampleSlot ? 1f : 0f, 0f);
            IndustrialTerminalRenderer.DrawSocket(sb, outputSlotRect, alpha, hoveringOutputSlot ? 1f : 0f, 0f);

            Crushers.CrusherUI.DrawSlotItem(sb, CrafterData.SampleItem, sampleSlotRect, alpha);
            Crushers.CrusherUI.DrawSlotItem(sb, CrafterData.OutputItem, outputSlotRect, alpha);

            //电力表盘
            float powerRatio = CrafterData.MaxUE > 0
                ? MathHelper.Clamp(CrafterData.UEvalue / CrafterData.MaxUE, 0f, 1f) : 0f;
            IndustrialTerminalRenderer.DrawGauge(sb, powerGaugeCenter, 30f, powerDisplay,
                Amber, alpha, PowerLabel.Value, $"{(int)(powerRatio * 100f)}%");
        }

        /// <summary>配方清单:凹槽底床 + 行(产物→原料),钉选行金框,条件不满足灰显</summary>
        private void DrawRecipeList(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);

            Utils.DrawBorderString(sb, ListLabel.Value,
                new Vector2(listRect.X + 2, listRect.Y - 20), TextDim * alpha, 0.62f);

            IndustrialTerminalRenderer.DrawRecess(sb, listRect, alpha, 0.8f);

            if (CrafterData.SampleItem == null || CrafterData.SampleItem.IsAir) {
                Utils.DrawBorderString(sb, SampleHint.Value,
                    new Vector2(listRect.X + 16, listRect.Y + 28), TextDim * (alpha * 0.9f), 0.7f);
                return;
            }
            if (candidates.Count == 0) {
                Utils.DrawBorderString(sb, ListEmptyText.Value,
                    new Vector2(listRect.X + 16, listRect.Y + 28), TextDim * (alpha * 0.9f), 0.7f);
                return;
            }

            int rowsTop = listRect.Y + 24;
            Rectangle rowsRect = new(listRect.X + 4, rowsTop, listRect.Width - 8, RowHeight * VisibleRows);
            Point mouse = MousePosition.ToPoint();

            for (int i = 0; i < candidates.Count; i++) {
                float rowY = rowsTop + i * RowHeight - scrollOffset;
                if (rowY < rowsTop - RowHeight || rowY > rowsTop + rowsRect.Height) {
                    continue;
                }
                Recipe recipe = candidates[i];
                bool pinned = IsPinnedRecipe(recipe);
                bool conditionOk = RecipeConditionsMetLocal(recipe);
                Rectangle rowRect = new(rowsRect.X, (int)rowY, rowsRect.Width, RowHeight - 2);
                bool hoverRow = rowRect.Contains(mouse) && rowsRect.Contains(mouse);

                //行底:钉选金,悬停微亮,条件锁死压暗
                Color rowBase = pinned
                    ? Color.Lerp(new Color(60, 48, 22), new Color(96, 74, 30), MathF.Sin(animTimer * 3f) * 0.5f + 0.5f)
                    : hoverRow ? new Color(48, 46, 42) : new Color(34, 33, 31);
                if (!conditionOk) {
                    rowBase = new Color(26, 25, 24);
                }
                sb.Draw(px, rowRect, src, rowBase * (alpha * 0.9f));
                if (pinned) {
                    //钉选描边
                    sb.Draw(px, new Rectangle(rowRect.X, rowRect.Y, rowRect.Width, 1), src, BrassBright * alpha);
                    sb.Draw(px, new Rectangle(rowRect.X, rowRect.Bottom - 1, rowRect.Width, 1), src, BrassBright * alpha);
                }

                float contentAlpha = conditionOk ? alpha : alpha * 0.35f;

                //产物 icon + 数量
                Main.instance.LoadItem(recipe.createItem.type);
                VaultUtils.SimpleDrawItem(sb, recipe.createItem.type,
                    new Vector2(rowRect.X + 18, rowRect.Center.Y), 22, 1f, 0, Color.White * contentAlpha);
                if (recipe.createItem.stack > 1) {
                    Utils.DrawBorderString(sb, recipe.createItem.stack.ToString(),
                        new Vector2(rowRect.X + 26, rowRect.Center.Y + 1), TextMain * contentAlpha, 0.55f);
                }

                //分隔箭头
                Utils.DrawBorderString(sb, "<",
                    new Vector2(rowRect.X + 44, rowRect.Center.Y - 8), TextDim * contentAlpha, 0.66f);

                //原料串:最多显示 7 个,余下省略号
                int shown = 0;
                float ix = rowRect.X + 62;
                foreach (Item required in recipe.requiredItem) {
                    if (required == null || required.IsAir) {
                        continue;
                    }
                    if (shown >= 7) {
                        Utils.DrawBorderString(sb, "...",
                            new Vector2(ix, rowRect.Center.Y - 6), TextDim * contentAlpha, 0.6f);
                        break;
                    }
                    Main.instance.LoadItem(required.type);
                    VaultUtils.SimpleDrawItem(sb, required.type,
                        new Vector2(ix + 10, rowRect.Center.Y), 20, 1f, 0, Color.White * contentAlpha);
                    if (required.stack > 1) {
                        Utils.DrawBorderString(sb, required.stack.ToString(),
                            new Vector2(ix + 16, rowRect.Center.Y + 2), TextMain * contentAlpha, 0.5f);
                    }
                    ix += 34;
                    shown++;
                }

                //行尾标签
                if (pinned) {
                    string tag = PinnedTag.Value;
                    Vector2 tagSize = FontAssets.MouseText.Value.MeasureString(tag) * 0.58f;
                    Utils.DrawBorderString(sb, tag,
                        new Vector2(rowRect.Right - tagSize.X - 8, rowRect.Center.Y - 8),
                        BrassBright * alpha, 0.58f);
                }
                else if (!conditionOk) {
                    string tag = ConditionLockText.Value;
                    Vector2 tagSize = FontAssets.MouseText.Value.MeasureString(tag) * 0.58f;
                    Utils.DrawBorderString(sb, tag,
                        new Vector2(rowRect.Right - tagSize.X - 8, rowRect.Center.Y - 8),
                        WarnRed * (alpha * 0.8f), 0.58f);
                }
            }

            //滚动指示:右缘细轨
            if (candidates.Count > VisibleRows) {
                float viewRatio = VisibleRows / (float)candidates.Count;
                float posRatio = scrollOffset / (candidates.Count * RowHeight);
                Rectangle track = new(listRect.Right - 7, rowsTop, 3, rowsRect.Height);
                sb.Draw(px, track, src, new Color(20, 20, 20) * alpha);
                Rectangle thumb = new(track.X, track.Y + (int)(posRatio * track.Height), 3,
                    Math.Max(12, (int)(viewRatio * track.Height)));
                sb.Draw(px, thumb, src, BrassBright * (alpha * 0.7f));
            }
        }

        /// <summary>状态灯 + 状态文本(含缺料 icon) + 进度刻度条</summary>
        private void DrawStatusRow(SpriteBatch sb) {
            float alpha = uiFadeAlpha;
            AutoCrafterData data = CrafterData;
            float x = panelRect.X + 126;
            float y = panelRect.Bottom - 46;

            //状态判定:UI 端本地评估,机器结算以权威端为准
            string state;
            Color lampColor;
            float lampBright = 0.6f;
            int missingIcon = 0;
            if (data.PinnedResultType <= 0) {
                state = StatusNoPin.Value;
                lampColor = TextDim;
                lampBright = 0.2f;
            }
            else if (CurrentTP.PinMissing) {
                state = StatusPinMissing.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else if (data.UEvalue < data.CraftCost) {
                state = StatusNoPower.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else if (!CurrentTP.StationOk) {
                state = StatusNoStation.Value;
                lampColor = Amber;
            }
            else if (!CurrentTP.ConditionsOk) {
                state = StatusNoCondition.Value;
                lampColor = Amber;
            }
            else if (!CurrentTP.MaterialsOk) {
                state = StatusNoMaterial.Value;
                lampColor = Amber;
                missingIcon = CurrentTP.MissingIngredientType;
            }
            else if (data.OutputItem != null && !data.OutputItem.IsAir
                && (data.OutputItem.type != data.PinnedResultType
                || data.OutputItem.stack + data.PinnedResultStack > data.OutputItem.maxStack)) {
                state = StatusOutputFull.Value;
                lampColor = Amber;
            }
            else if (data.CraftProgress > 0) {
                state = StatusWorking.Value;
                lampColor = OkGreen;
                lampBright = MathF.Sin(animTimer * 2.6f) * 0.2f + 0.72f;
            }
            else {
                state = StatusIdle.Value;
                lampColor = TextDim;
                lampBright = 0.25f;
            }

            IndustrialTerminalRenderer.DrawLamp(sb, new Vector2(x + 7, y + 9), lampColor, alpha, lampBright);
            Utils.DrawBorderString(sb, state, new Vector2(x + 21, y + 1),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.66f);
            if (missingIcon > 0) {
                Vector2 stateSize = FontAssets.MouseText.Value.MeasureString(state) * 0.66f;
                Main.instance.LoadItem(missingIcon);
                VaultUtils.SimpleDrawItem(sb, missingIcon,
                    new Vector2(x + 21 + stateSize.X + 14, y + 9), 20, 1f, 0, Color.White * alpha);
            }

            //进度刻度条
            float progress = data.MaxCraftProgress > 0
                ? MathHelper.Clamp(data.CraftProgress / (float)data.MaxCraftProgress, 0f, 1f) : 0f;
            IndustrialTerminalRenderer.DrawTickBar(sb, progressBarRect, progress, OkGreen, alpha);

            //操作提示
            string hint = string.Empty;
            if (hoveringSampleSlot) {
                hint = SampleHint.Value;
            }
            else if (listRect.Contains(MousePosition.ToPoint()) && candidates.Count > 0) {
                hint = data.PinnedResultType > 0 ? UnpinHint.Value : PinHint.Value;
            }
            if (!string.IsNullOrEmpty(hint)) {
                float blink = MathF.Sin(animTimer * 6f) * 0.3f + 0.7f;
                Utils.DrawBorderString(sb, hint,
                    new Vector2(panelRect.X + 126, panelRect.Bottom - 26),
                    Color.Lerp(TextDim, Amber, 0.5f) * (alpha * blink), 0.6f);
            }
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (isDragging) {
                return;
            }
            if (hoveringPowerGauge) {
                Crushers.CrusherUI.ShowTip(sb,
                    $"{(int)CrafterData.UEvalue}/{(int)CrafterData.MaxUE} {PowerUnit.Value}", TextMain);
            }
        }
        #endregion
    }
}
