using CalamityOverhaul.Content.MainMenus.Overs;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MainMenus.Characters
{
    /// <summary>主菜单角色码头，底部居中一排角色芯片，点击展开立绘查看<br/>
    /// 走 Mod_MenuLoad 层，原版菜单由 InnoVault 驱动，自定义菜单经 DriveMenuOverlays 驱动，菜单侧零代码</summary>
    internal class CharacterDockUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static CharacterDockUI Instance => UIHandleLoader.GetUIHandleOfType<CharacterDockUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override bool Active => VaultLoad.LoadenContent && Main.gameMenu && MenuCharacterRegistry.AnyUnlocked;

        //缩放档位，像素图整数倍
        internal const int MinZoomStep = 1;
        internal const int MaxZoomStep = 3;
        internal const int DefaultZoomStep = 2;

        //芯片布局
        private const float ChipPitch = 56f;
        private const float ChipBottomMargin = 40f;
        private const int ChipPad = 4;//框到半身像的留边
        private const float ChipCut = 5f;//切角px

        //立绘
        private const float PortraitAnchorXRatio = 0.14f;
        private const float PortraitBottomMargin = 90f;
        private const float MinVisiblePx = 80f;
        private const float OpenStep = 1f / 24f;
        private const float CloseStep = 1f / 16f;
        private const int MaxPips = 6;//超过退化为箭头+计数

        private const int AutoSaveDelay = 300;//5s防抖

        public static LocalizedText OpenHintLabel { get; private set; }
        public static LocalizedText DragHintLabel { get; private set; }

        /// <summary>芯片运行态，按角色 Key 常驻</summary>
        private sealed class ChipRuntime
        {
            public MenuCharacter Def;
            public Vector2 Center;
            public Vector2 Size;
            public float Hover;
            public bool HoverTarget;
            public bool PrevHoverTarget;

            public Rectangle HitBox => new(
                (int)(Center.X - Size.X / 2f) - ChipPad,
                (int)(Center.Y - Size.Y / 2f) - ChipPad,
                (int)Size.X + ChipPad * 2,
                (int)Size.Y + ChipPad * 2);
        }

        private readonly Dictionary<string, ChipRuntime> chipRuntime = [];
        private readonly List<ChipRuntime> visibleChips = [];

        private float dockAlpha;
        private string activeKey;//展示中的角色，含淡出期
        private bool portraitOpen;
        private float portraitProgress;
        private bool draggingPortrait;
        private Vector2 dragStartMouse;
        private Vector2 dragStartOffset;
        private Rectangle lastPortraitRect;//本帧立绘矩形缓存，粒子与占用查询共用
        private float lastPortraitAlpha;
        private int autoSaveTimer;
        private bool savePending;
        private bool savedStateApplied;

        #region 生命周期
        public override void SetStaticDefaults() {
            OpenHintLabel = this.GetLocalization(nameof(OpenHintLabel), () => "点击查看立绘");
            DragHintLabel = this.GetLocalization(nameof(DragHintLabel), () => "拖拽移动 · 滚轮缩放 · 右键复位");
            foreach (MenuCharacter def in MenuCharacterRegistry.All) {
                def.DisplayName = this.GetLocalization($"{def.Key}.Name", () => def.FallbackName);
            }
            ResetRuntime();
        }

        public override void UnLoad() {
            if (savePending) {
                MenuSave.SaveNow();
            }
            ResetRuntime();
        }

        private void ResetRuntime() {
            chipRuntime.Clear();
            visibleChips.Clear();
            dockAlpha = 0f;
            activeKey = null;
            portraitOpen = false;
            portraitProgress = 0f;
            draggingPortrait = false;
            lastPortraitRect = Rectangle.Empty;
            lastPortraitAlpha = 0f;
            autoSaveTimer = 0;
            savePending = false;
            savedStateApplied = false;
        }
        #endregion

        #region 状态与几何
        private static bool CanInteract() {
            return Main.menuMode == 0
                && !OverhaulSettingsUI.OnActive()
                && !FeedbackUI.Instance.OnActive()
                && !AcknowledgmentUI.OnActive();
        }

        private void MarkStateDirty() {
            savePending = true;
            autoSaveTimer = 0;
        }

        private bool TryGetPortraitContext(out MenuCharacter def, out CharacterMenuState state, out Texture2D tex, out int step) {
            def = null;
            state = null;
            tex = null;
            step = DefaultZoomStep;
            if (activeKey == null || !MenuCharacterRegistry.TryGet(activeKey, out def) || !def.HasPortrait) {
                return false;
            }
            state = MenuSave.GetState(def.Key);
            IList<Texture2D> list = def.Expressions;
            int expr = Math.Clamp(state.Expression, 0, list.Count - 1);
            tex = list[expr] ?? list[0];
            if (tex == null || tex.IsDisposed) {
                return false;
            }
            step = Math.Clamp(state.ZoomStep, MinZoomStep, MaxZoomStep);
            return true;
        }

        private static Vector2 PortraitBasePos(Texture2D tex, int step) {
            Vector2 size = tex.Size() * step;
            return new Vector2(
                Main.screenWidth * PortraitAnchorXRatio - size.X / 2f,
                Main.screenHeight - size.Y - PortraitBottomMargin);
        }

        private static Rectangle PortraitRect(CharacterMenuState state, Texture2D tex, int step) {
            Vector2 topLeft = PortraitBasePos(tex, step) + state.Offset;
            Vector2 size = tex.Size() * step;
            return new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)size.X, (int)size.Y);
        }

        /// <summary>钳偏移保证立绘每轴至少 80px 在屏内</summary>
        private static void ClampOffset(CharacterMenuState state, Texture2D tex, int step) {
            if (Main.screenWidth <= 0 || Main.screenHeight <= 0) {
                return;
            }
            Vector2 basePos = PortraitBasePos(tex, step);
            Vector2 size = tex.Size() * step;
            state.Offset = new Vector2(
                Math.Clamp(state.Offset.X, MinVisiblePx - basePos.X - size.X, Main.screenWidth - MinVisiblePx - basePos.X),
                Math.Clamp(state.Offset.Y, MinVisiblePx - basePos.Y - size.Y, Main.screenHeight - MinVisiblePx - basePos.Y));
        }

        //表情切换行几何，绘制与命中同一份
        private static float SwitchRowY(Rectangle rect) => rect.Bottom + 14f;

        private static Rectangle PipHit(Rectangle rect, int count, int i) {
            float pitch = 16f;
            float startX = rect.Center.X - (count - 1) * pitch / 2f;
            return new Rectangle((int)(startX + i * pitch) - 6, (int)SwitchRowY(rect) - 6, 12, 12);
        }

        private static Rectangle ArrowHit(Rectangle rect, int dir) =>
            new((int)(rect.Center.X + dir * 44f) - 8, (int)SwitchRowY(rect) - 8, 16, 16);

        /// <summary>菜单接管方的输入占用查询</summary>
        internal bool CapturesMenuInput(Point point) {
            if (!Active || Main.menuMode != 0 || dockAlpha <= 0.01f) {
                return false;
            }
            if (draggingPortrait) {
                return true;
            }
            foreach (ChipRuntime chip in visibleChips) {
                if (chip.HitBox.Contains(point)) {
                    return true;
                }
            }
            if (portraitOpen && portraitProgress > 0.9f && lastPortraitRect != Rectangle.Empty) {
                if (lastPortraitRect.Contains(point)) {
                    return true;
                }
                Rectangle switchBand = new(lastPortraitRect.X, lastPortraitRect.Bottom + 2, lastPortraitRect.Width, 26);
                if (switchBand.Contains(point)) {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region 更新
        private void LayoutChips() {
            visibleChips.Clear();
            foreach (MenuCharacter def in MenuCharacterRegistry.All) {
                if (!def.Unlocked || !def.ChipReady) {
                    continue;
                }
                if (!chipRuntime.TryGetValue(def.Key, out ChipRuntime chip)) {
                    chip = new ChipRuntime { Def = def };
                    chipRuntime[def.Key] = chip;
                }
                chip.Size = def.ChipFrames[0].Size() * def.ChipScale;
                visibleChips.Add(chip);
            }

            int count = visibleChips.Count;
            if (count == 0) {
                return;
            }
            float centerX = Main.screenWidth / 2f;
            float baseline = Main.screenHeight - ChipBottomMargin;
            for (int i = 0; i < count; i++) {
                ChipRuntime chip = visibleChips[i];
                chip.Center = new Vector2(
                    centerX + (i - (count - 1) / 2f) * ChipPitch,
                    baseline - chip.Size.Y / 2f);
            }
        }

        /// <summary>启动后一次性恢复上次展开的角色</summary>
        private void ApplySavedState() {
            savedStateApplied = true;
            foreach (MenuCharacter def in MenuCharacterRegistry.All) {
                if (!def.Unlocked || !def.HasPortrait) {
                    continue;
                }
                if (MenuSave.GetState(def.Key).Show) {
                    activeKey = def.Key;
                    portraitOpen = true;
                    return;
                }
            }
        }

        public override void MenuLogicUpdate() {
            LayoutChips();

            bool showDock = Main.menuMode == 0 && visibleChips.Count > 0;
            dockAlpha = showDock ? Math.Min(dockAlpha + 0.04f, 1f) : Math.Max(dockAlpha - 0.1f, 0f);

            foreach (ChipRuntime chip in visibleChips) {
                float target = chip.HoverTarget ? 1f : 0f;
                chip.Hover += Math.Clamp(target - chip.Hover, -0.12f, 0.12f);
            }

            if (portraitOpen) {
                portraitProgress = Math.Min(portraitProgress + OpenStep, 1f);
            }
            else {
                portraitProgress = Math.Max(portraitProgress - CloseStep, 0f);
                if (portraitProgress <= 0f) {
                    activeKey = null;
                }
            }

            foreach (ChipRuntime chip in visibleChips) {
                chip.Def.UpdateAmbient(BuildScene(chip));
            }

            if (savePending && ++autoSaveTimer >= AutoSaveDelay) {
                savePending = false;
                autoSaveTimer = 0;
                MenuSave.SaveNow();
            }
        }

        public override void Update() {
            LayoutChips();
            if (!savedStateApplied) {
                ApplySavedState();
            }
            UpdateInteraction();
            RefreshPortraitCache();
        }

        private void RefreshPortraitCache() {
            lastPortraitAlpha = MathF.Pow(portraitProgress, 1.2f) * dockAlpha;
            if (activeKey != null && TryGetPortraitContext(out _, out CharacterMenuState state, out Texture2D tex, out int step)) {
                //稳态每帧钳一次，分辨率变化后立绘不会丢在屏外
                if (portraitOpen && !draggingPortrait) {
                    ClampOffset(state, tex, step);
                }
                lastPortraitRect = PortraitRect(state, tex, step);
            }
            else {
                lastPortraitRect = Rectangle.Empty;
            }
        }

        private MenuCharacterScene BuildScene(ChipRuntime chip) {
            bool mine = activeKey == chip.Def.Key;
            return new MenuCharacterScene {
                ChipCenter = chip.Center,
                ChipSize = chip.Size,
                ChipAlpha = dockAlpha,
                PortraitRect = mine ? lastPortraitRect : Rectangle.Empty,
                PortraitAlpha = mine ? lastPortraitAlpha : 0f,
                PortraitVisible = mine && portraitOpen && portraitProgress > 0.5f
            };
        }

        private void UpdateInteraction() {
            bool canInteract = CanInteract() && dockAlpha > 0.4f;
            Point mouse = MousePosition.ToPoint();

            //芯片悬停，进入沿播一次tick
            ChipRuntime hoveredChip = null;
            foreach (ChipRuntime chip in visibleChips) {
                chip.PrevHoverTarget = chip.HoverTarget;
                chip.HoverTarget = canInteract && chip.HitBox.Contains(mouse);
                if (chip.HoverTarget) {
                    hoveredChip = chip;
                    if (!chip.PrevHoverTarget) {
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                }
            }

            //拖拽期独占输入
            if (draggingPortrait) {
                if (!canInteract || !portraitOpen
                    || !TryGetPortraitContext(out _, out CharacterMenuState dragState, out Texture2D dragTex, out int dragStep)) {
                    EndDrag();
                    return;
                }
                dragState.Offset = dragStartOffset + (MousePosition - dragStartMouse);
                ClampOffset(dragState, dragTex, dragStep);
                if (keyLeftPressState is KeyPressState.Released or KeyPressState.None) {
                    EndDrag();
                }
                return;
            }

            //芯片点击优先于立绘
            if (hoveredChip != null) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    ToggleCharacter(hoveredChip.Def);
                }
                return;
            }

            //立绘交互，稳态才响应
            if (!canInteract || !portraitOpen || portraitProgress < 0.9f) {
                return;
            }
            if (!TryGetPortraitContext(out MenuCharacter def, out CharacterMenuState state, out Texture2D tex, out int step)) {
                return;
            }
            Rectangle rect = PortraitRect(state, tex, step);

            int exprCount = def.Expressions.Count;
            if (exprCount > 1 && HandleExpressionInput(rect, state, exprCount, mouse)) {
                return;
            }

            if (!rect.Contains(mouse)) {
                return;
            }

            //滚轮换档
            int wheel = MouseScrollDelta;
            if (wheel != 0) {
                int next = Math.Clamp(state.ZoomStep + Math.Sign(wheel), MinZoomStep, MaxZoomStep);
                if (next != state.ZoomStep) {
                    state.ZoomStep = next;
                    ClampOffset(state, tex, next);
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    MarkStateDirty();
                }
            }

            //右键复位到默认锚点与档位
            if (keyRightPressState == KeyPressState.Pressed) {
                state.Offset = Vector2.Zero;
                state.ZoomStep = DefaultZoomStep;
                SoundEngine.PlaySound(SoundID.MenuTick);
                MarkStateDirty();
                return;
            }

            if (keyLeftPressState == KeyPressState.Pressed) {
                draggingPortrait = true;
                dragStartMouse = MousePosition;
                dragStartOffset = state.Offset;
            }
        }

        private void EndDrag() {
            draggingPortrait = false;
            MarkStateDirty();
        }

        private void ToggleCharacter(MenuCharacter def) {
            CharacterMenuState state = MenuSave.GetState(def.Key);
            if (activeKey == def.Key && portraitOpen) {
                portraitOpen = false;
                state.Show = false;
                SoundEngine.PlaySound(SoundID.MenuClose);
                //关闭立即落盘
                savePending = false;
                autoSaveTimer = 0;
                MenuSave.SaveNow();
                return;
            }
            if (!def.HasPortrait) {
                //暂无立绘的角色，点击仅回声
                SoundEngine.PlaySound(SoundID.MenuTick);
                return;
            }
            //单开语义，其余角色的展开位记为收起
            foreach (MenuCharacter other in MenuCharacterRegistry.All) {
                if (other != def) {
                    MenuSave.GetState(other.Key).Show = false;
                }
            }
            if (activeKey != def.Key) {
                portraitProgress = 0f;//换角色重新浮现
            }
            activeKey = def.Key;
            portraitOpen = true;
            state.Show = true;
            SoundEngine.PlaySound(SoundID.MenuOpen);
            MarkStateDirty();
        }

        private bool HandleExpressionInput(Rectangle rect, CharacterMenuState state, int count, Point mouse) {
            if (keyLeftPressState != KeyPressState.Pressed) {
                return false;
            }
            int current = Math.Clamp(state.Expression, 0, count - 1);
            if (count <= MaxPips) {
                for (int i = 0; i < count; i++) {
                    if (!PipHit(rect, count, i).Contains(mouse)) {
                        continue;
                    }
                    if (i != current) {
                        state.Expression = i;
                        SoundEngine.PlaySound(SoundID.MenuTick);
                        MarkStateDirty();
                    }
                    return true;
                }
                return false;
            }
            int dir = ArrowHit(rect, -1).Contains(mouse) ? -1 : ArrowHit(rect, 1).Contains(mouse) ? 1 : 0;
            if (dir == 0) {
                return false;
            }
            state.Expression = (current + dir + count) % count;
            SoundEngine.PlaySound(SoundID.MenuTick);
            MarkStateDirty();
            return true;
        }
        #endregion

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (dockAlpha <= 0.01f && portraitProgress <= 0.01f) {
                return;
            }

            //像素画段: PointClamp 保像素锐利
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            foreach (ChipRuntime chip in visibleChips) {
                chip.Def.DrawAmbient(spriteBatch, BuildScene(chip));
            }

            DrawPortraitLayer(spriteBatch);

            foreach (ChipRuntime chip in visibleChips) {
                DrawChip(spriteBatch, chip);
            }

            //文字与切换件段: 恢复层驱动的 LinearClamp 批次
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            DrawOverlayText(spriteBatch);
        }

        private void DrawPortraitLayer(SpriteBatch sb) {
            if (activeKey == null || portraitProgress <= 0.01f) {
                return;
            }
            if (!TryGetPortraitContext(out MenuCharacter def, out CharacterMenuState state, out Texture2D tex, out int step)) {
                return;
            }

            Rectangle rect = PortraitRect(state, tex, step);
            float ease = CharacterDockRenderer.EaseOutCubic(portraitProgress);
            float alpha = MathF.Pow(portraitProgress, 1.2f) * dockAlpha;

            //自芯片位置浮现
            Vector2 targetCenter = rect.Center.ToVector2();
            Vector2 fromCenter = chipRuntime.TryGetValue(def.Key, out ChipRuntime chip) ? chip.Center : targetCenter;
            Vector2 drawCenter = Vector2.Lerp(fromCenter, targetCenter, ease);
            float scale = step * (0.35f + 0.65f * ease);
            Vector2 topLeft = drawCenter - tex.Size() * scale / 2f;

            float pulse = MathF.Sin(GlobalTimer * 1.5f) * 0.5f + 0.5f;
            CharacterDockRenderer.DrawPortrait(sb, tex, topLeft, scale, alpha, def.AccentBright, pulse, GlobalTimer);
        }

        private void DrawChip(SpriteBatch sb, ChipRuntime chip) {
            MenuCharacter def = chip.Def;
            float lift = chip.Hover * 2f;
            Vector2 center = chip.Center - new Vector2(0f, lift);
            float alpha = dockAlpha;
            Rectangle rect = new(
                (int)(center.X - chip.Size.X / 2f) - ChipPad,
                (int)(center.Y - chip.Size.Y / 2f) - ChipPad,
                (int)chip.Size.X + ChipPad * 2,
                (int)chip.Size.Y + ChipPad * 2);

            float pulse = MathF.Sin(GlobalTimer * 1.8f + rect.X * 0.05f) * 0.5f + 0.5f;
            bool selected = activeKey == def.Key && portraitOpen;

            CharacterDockRenderer.DrawCutFill(sb, rect, (int)ChipCut, def.BaseShade * (alpha * 0.88f));

            IList<Texture2D> frames = def.ChipFrames;
            int frameIdx = Math.Clamp(def.GetChipFrame(GlobalTimer), 0, frames.Count - 1);
            Texture2D bust = frames[frameIdx] ?? frames[0];
            sb.Draw(bust, center, null, Color.White * alpha, 0f, bust.Size() / 2f, def.ChipScale, SpriteEffects.None, 0f);
            if (chip.Hover > 0.01f) {
                //悬停提亮，叠一层身份色薄光
                sb.Draw(bust, center, null, def.AccentBright * (alpha * 0.12f * chip.Hover),
                    0f, bust.Size() / 2f, def.ChipScale, SpriteEffects.None, 0f);
            }

            //双线切角框
            Color edge = Color.Lerp(def.AccentDark, def.AccentBright, selected ? 0.8f : pulse * 0.5f)
                * (alpha * (0.55f + 0.35f * chip.Hover + (selected ? 0.1f : 0f)));
            CharacterDockRenderer.DrawCutFrame(sb, rect, ChipCut, edge);
            Rectangle inner = rect;
            inner.Inflate(-3, -3);
            CharacterDockRenderer.DrawCutFrame(sb, inner, ChipCut - 2f, def.AccentBright * (alpha * 0.15f * pulse));

            CharacterDockRenderer.DrawBottomGlow(sb, rect, def.AccentBright, alpha * (0.45f + 0.4f * chip.Hover));

            if (chip.Hover > 0.01f) {
                //悬停外环脉冲
                Rectangle ring = rect;
                int expand = (int)(2f + pulse * 2f);
                ring.Inflate(expand, expand);
                CharacterDockRenderer.DrawCutFrame(sb, ring, ChipCut + expand * 0.6f, def.AccentBright * (alpha * 0.35f * chip.Hover));
            }
            else if (selected) {
                //选中常亮外环
                Rectangle ring = rect;
                ring.Inflate(3, 3);
                CharacterDockRenderer.DrawCutFrame(sb, ring, ChipCut + 2f, def.AccentBright * (alpha * 0.4f));
            }
        }

        private void DrawOverlayText(SpriteBatch sb) {
            //表情切换件
            if (activeKey != null && portraitOpen && portraitProgress > 0.9f
                && TryGetPortraitContext(out MenuCharacter pDef, out CharacterMenuState pState, out Texture2D pTex, out int pStep)
                && pDef.Expressions.Count > 1) {
                DrawExpressionSwitch(sb, pDef, pState, PortraitRect(pState, pTex, pStep), lastPortraitAlpha);
            }

            //悬停名牌与提示
            if (!CanInteract() || dockAlpha <= 0.4f) {
                return;
            }
            foreach (ChipRuntime chip in visibleChips) {
                if (!chip.HoverTarget) {
                    continue;
                }
                MenuCharacter def = chip.Def;
                string name = def.DisplayName?.Value ?? def.FallbackName;
                float topY = chip.HitBox.Y;
                Utils.DrawBorderString(sb, name, new Vector2(chip.Center.X, topY - 44f),
                    Color.White * dockAlpha, 0.9f, 0.5f);
                if (def.HasPortrait) {
                    string hint = activeKey == def.Key && portraitOpen ? DragHintLabel.Value : OpenHintLabel.Value;
                    Utils.DrawBorderString(sb, hint, new Vector2(chip.Center.X, topY - 22f),
                        new Color(200, 200, 200) * (dockAlpha * 0.85f), 0.75f, 0.5f);
                }
            }
        }

        private void DrawExpressionSwitch(SpriteBatch sb, MenuCharacter def, CharacterMenuState state, Rectangle rect, float alpha) {
            int count = def.Expressions.Count;
            int current = Math.Clamp(state.Expression, 0, count - 1);
            Point mouse = MousePosition.ToPoint();

            if (count <= MaxPips) {
                for (int i = 0; i < count; i++) {
                    Rectangle hit = PipHit(rect, count, i);
                    Vector2 c = hit.Center.ToVector2();
                    bool cur = i == current;
                    bool hover = hit.Contains(mouse);
                    if (cur) {
                        CharacterDockRenderer.DrawDiamond(sb, c, 13f, def.AccentBright * (alpha * 0.25f));
                    }
                    float size = cur ? 9f : hover ? 8f : 6f;
                    Color color = cur ? def.AccentBright * alpha : def.AccentDark * (alpha * (hover ? 0.9f : 0.5f));
                    CharacterDockRenderer.DrawDiamond(sb, c, size, color);
                }
                return;
            }

            //箭头+计数
            for (int dir = -1; dir <= 1; dir += 2) {
                Rectangle hit = ArrowHit(rect, dir);
                bool hover = hit.Contains(mouse);
                CharacterDockRenderer.DrawDiamond(sb, hit.Center.ToVector2(), hover ? 12f : 10f,
                    (hover ? def.AccentBright : def.AccentDark) * alpha);
            }
            Utils.DrawBorderString(sb, $"{current + 1}/{count}",
                new Vector2(rect.Center.X, SwitchRowY(rect) - 10f), Color.White * alpha, 0.75f, 0.5f);
        }
        #endregion
    }
}
