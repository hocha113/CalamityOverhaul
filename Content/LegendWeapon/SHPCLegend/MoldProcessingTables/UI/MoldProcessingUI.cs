using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables.UI
{
    /// <summary>
    /// 模具加工台主 UI：右键加工台物块后由 <see cref="MoldProcessingTableTile.RightClick"/> 调用 <see cref="Open"/>
    /// 整体布局：顶部 Header + 左侧 Sidebar（6 类别） + 右侧 Tab 切换的 Workbench / Codex
    /// 视觉与配色完全复用 <see cref="SHPCRenderer"/> / <see cref="SHPCTheme"/> / <see cref="EffectLoader.SHPCModPanel"/>
    /// </summary>
    internal class MoldProcessingUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";
        public static MoldProcessingUI Instance => UIHandleLoader.GetUIHandleOfType<MoldProcessingUI>();

        //最大与加工台距离的平方（约 30 格）
        private const float MaxInteractionDistSq = 30f * 16f * 30f * 16f;

        #region 本地化

        public static LocalizedText Title { get; private set; }
        public static LocalizedText Subtitle { get; private set; }
        public static LocalizedText TabWorkbench { get; private set; }
        public static LocalizedText TabCodex { get; private set; }
        public static LocalizedText Decompose { get; private set; }
        public static LocalizedText Reforge { get; private set; }
        public static LocalizedText ClearPin { get; private set; }
        public static LocalizedText RandomMode { get; private set; }
        public static LocalizedText PinnedMode { get; private set; }
        public static LocalizedText CostLine { get; private set; }
        public static LocalizedText HaveLine { get; private set; }
        public static LocalizedText UnknownName { get; private set; }
        public static LocalizedText ProgressFormat { get; private set; }
        public static LocalizedText ShardSuffix { get; private set; }
        public static LocalizedText EmptyCandidates { get; private set; }
        public static LocalizedText CloseHint { get; private set; }
        public static LocalizedText DecomposeHint { get; private set; }
        public static LocalizedText CodexHint { get; private set; }
        public static LocalizedText Discovered { get; private set; }
        public static LocalizedText PinnedTag { get; private set; }

        public override void SetStaticDefaults() {
            Title = this.GetLocalization(nameof(Title), () => "MOLD PROCESSING");
            Subtitle = this.GetLocalization(nameof(Subtitle), () => "模具加工台 · 分解 / 重铸");
            TabWorkbench = this.GetLocalization(nameof(TabWorkbench), () => "WORKBENCH");
            TabCodex = this.GetLocalization(nameof(TabCodex), () => "CODEX");
            Decompose = this.GetLocalization(nameof(Decompose), () => "DECOMPOSE");
            Reforge = this.GetLocalization(nameof(Reforge), () => "REFORGE");
            ClearPin = this.GetLocalization(nameof(ClearPin), () => "CLEAR PIN");
            RandomMode = this.GetLocalization(nameof(RandomMode), () => "RANDOM · UNDISCOVERED FIRST");
            PinnedMode = this.GetLocalization(nameof(PinnedMode), () => "PINNED FROM CODEX");
            CostLine = this.GetLocalization(nameof(CostLine), () => "COST: {0}");
            HaveLine = this.GetLocalization(nameof(HaveLine), () => "HAVE: {0}");
            UnknownName = this.GetLocalization(nameof(UnknownName), () => "??????");
            ProgressFormat = this.GetLocalization(nameof(ProgressFormat), () => "{0}/{1}");
            ShardSuffix = this.GetLocalization(nameof(ShardSuffix), () => "SHARDS");
            EmptyCandidates = this.GetLocalization(nameof(EmptyCandidates), () => "// no compatible modules in inventory");
            CloseHint = this.GetLocalization(nameof(CloseHint), () => "ESC TO CLOSE");
            DecomposeHint = this.GetLocalization(nameof(DecomposeHint), () => "Click a module to decompose");
            CodexHint = this.GetLocalization(nameof(CodexHint), () => "Click a discovered mold to pin it as reforge target");
            Discovered = this.GetLocalization(nameof(Discovered), () => "DISCOVERED");
            PinnedTag = this.GetLocalization(nameof(PinnedTag), () => "PINNED");
        }

        #endregion

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        //是否处于打开状态
        private bool visible;
        public override bool Active => visible && Main.LocalPlayer != null && Main.LocalPlayer.active && !Main.LocalPlayer.dead;

        //当前面板的滑入进度，0..1
        private float openProgress;
        //当前绑定的加工台格子位置（用于距离检查）
        private Point16 boundTile;
        private bool hasBoundTile;
        //当前选中的类别
        public SHPCSlotCategory SelectedCategory { get; set; } = SHPCSlotCategory.Barrel;
        //当前 Tab：false=Workbench, true=Codex
        public bool CodexMode { get; private set; }

        //缓存的布局（每帧由 Update 重建）
        private MoldLayout cachedLayout;
        //缓存命中类型
        private enum TopHit { None, Close, TabWorkbench, TabCodex }
        private TopHit topHover;

        /// <summary>
        /// 由 <see cref="MoldProcessingTableTile.RightClick"/> 调用。如果 UI 已经开着则切换为关闭；不同位置则切换绑定
        /// </summary>
        public void Open(Point16 tilePos) {
            if (visible && hasBoundTile && boundTile == tilePos) {
                Close();
                return;
            }
            visible = true;
            openProgress = 0f;
            boundTile = tilePos;
            hasBoundTile = true;
            SelectedCategory = SHPCSlotCategory.Barrel;
            CodexMode = false;
            MoldCodexPanel.ScrollReset();
            MoldProcessingPanel.ScrollReset();
            SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.5f });
        }

        public new void Close() {
            if (!visible) {
                return;
            }
            visible = false;
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
        }

        public override void Update() {
            openProgress = MathHelper.Lerp(openProgress, 1f, 0.22f);
            if (MathF.Abs(openProgress - 1f) < 0.005f) {
                openProgress = 1f;
            }

            //距离与持有合法性检查
            if (visible && hasBoundTile) {
                Vector2 tileCenterWorld = new(boundTile.X * 16f + MoldProcessingTableTile.Width * 8f,
                    boundTile.Y * 16f + MoldProcessingTableTile.Height * 8f);
                if (Main.LocalPlayer.DistanceSQ(tileCenterWorld) > MaxInteractionDistSq) {
                    Close();
                    return;
                }
                Tile tile = Framing.GetTileSafely(boundTile.X, boundTile.Y);
                if (!tile.HasTile || tile.TileType != ModContent.TileType<MoldProcessingTableTile>()) {
                    Close();
                    return;
                }
            }

            //ESC 关闭
            if (visible && Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) {
                Close();
                Main.LocalPlayer.releaseInventory = false;
                return;
            }

            Vector2 center = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            cachedLayout = MoldLayout.Compute(center, openProgress);
            UIHitBox = cachedLayout.Panel;

            //刷新 workbench 候选项 / codex 列表（每帧）
            MoldProcessingPanel.RefreshCandidates(player, SelectedCategory);

            //命中检测
            topHover = TopHit.None;
            if (cachedLayout.CloseBtn.Contains((int)MousePosition.X, (int)MousePosition.Y)) {
                topHover = TopHit.Close;
            }
            else if (cachedLayout.TabWorkbench.Contains((int)MousePosition.X, (int)MousePosition.Y)) {
                topHover = TopHit.TabWorkbench;
            }
            else if (cachedLayout.TabCodex.Contains((int)MousePosition.X, (int)MousePosition.Y)) {
                topHover = TopHit.TabCodex;
            }

            MoldCategorySidebar.UpdateHover(cachedLayout, MousePosition, this);
            if (CodexMode) {
                MoldCodexPanel.UpdateHover(cachedLayout, MousePosition, this);
            }
            else {
                MoldProcessingPanel.UpdateHover(cachedLayout, MousePosition, this);
            }

            //鼠标交互占用
            if (cachedLayout.Panel.Contains((int)MousePosition.X, (int)MousePosition.Y)) {
                player.mouseInterface = true;
                player.CWR().DontSwitchWeaponTime = 2;
            }

            //滚轮：按当前 tab 分发
            if (CodexMode) {
                if (cachedLayout.Content.Contains((int)MousePosition.X, (int)MousePosition.Y)) {
                    MoldCodexPanel.HandleScroll();
                }
            }
            else {
                if (cachedLayout.Content.Contains((int)MousePosition.X, (int)MousePosition.Y)) {
                    MoldProcessingPanel.HandleScroll();
                }
            }

            //左键
            if (keyLeftPressState == KeyPressState.Pressed) {
                if (topHover == TopHit.Close) {
                    Close();
                    return;
                }
                if (topHover == TopHit.TabWorkbench) {
                    if (CodexMode) {
                        CodexMode = false;
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                    return;
                }
                if (topHover == TopHit.TabCodex) {
                    if (!CodexMode) {
                        CodexMode = true;
                        MoldCodexPanel.ScrollReset();
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                    return;
                }
                if (MoldCategorySidebar.HandleClick(this)) {
                    return;
                }
                if (CodexMode) {
                    MoldCodexPanel.HandleClick(this, player);
                }
                else {
                    MoldProcessingPanel.HandleClick(this, player);
                }
            }
        }

        public override void Draw(SpriteBatch sb) {
            if (!visible) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2.Value;
            if (px == null) {
                return;
            }

            float a = openProgress;
            Rectangle rect = cachedLayout.Panel;

            //投影
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(rect.X + 3, rect.Y + 4, rect.Width, rect.Height),
                new Color(0, 0, 0) * (0.55f * a));

            //着色器背景（焦点放在工作台中央偏右，让重铸预览区获得更多发光）
            Vector2 focus = new(cachedLayout.Content.X + cachedLayout.Content.Width * 0.72f,
                cachedLayout.Content.Y + cachedLayout.Content.Height * 0.5f);
            int side = 24;
            DrawShaderBackground(sb, px, new Rectangle(rect.X - side, rect.Y - side, rect.Width + side * 2, rect.Height + side * 2), focus, openProgress);

            //外框 + 四角
            SHPCRenderer.DrawRectStroke(sb, px, rect, 1.2f, SHPCTheme.Border * (0.9f * a));
            SHPCRenderer.DrawCornerBrackets(sb, px, rect, 10f, 1.5f, SHPCTheme.BorderHi * (0.9f * a));

            //顶部色带
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(rect.X, rect.Y, rect.Width, 3),
                SHPCTheme.Cyan * (0.85f * a));

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //标题 / 副标题（标题最多占据 close 按钮左侧到 SYS 码之间的空间）
            float headerLeft = rect.X + 14f;
            float headerRight = cachedLayout.CloseBtn.X - 90f;   //预留右上 SYS#xxxx 与间距
            float titleScale = MoldFont.TitleBase * MoldFont.FontScale;
            float subtitleScale = MoldFont.SubtitleBase * MoldFont.FontScale;
            string titleStr = MoldFont.TruncateForWidth(font, Title.Value, headerRight - headerLeft, titleScale);
            string subtitleStr = MoldFont.TruncateForWidth(font, Subtitle.Value, headerRight - headerLeft, subtitleScale);
            Utils.DrawBorderString(sb, titleStr,
                new Vector2(headerLeft, rect.Y + 7f), SHPCTheme.Text * a, titleScale);
            Utils.DrawBorderString(sb, subtitleStr,
                new Vector2(headerLeft, rect.Y + 26f), SHPCTheme.TextDim * a, subtitleScale);

            //右上 SYS ID + 关闭按钮
            float time = (float)Main.GameUpdateCount / 60f;
            string idCode = $"SYS#{(int)(time * 13f) % 9999:D4}";
            float idScale = MoldFont.SysIdBase * MoldFont.FontScale;
            Vector2 idSz = font.MeasureString(idCode) * idScale;
            Utils.DrawBorderString(sb, idCode,
                new Vector2(cachedLayout.CloseBtn.X - 8f - idSz.X, rect.Y + 12f),
                SHPCTheme.Cyan * (0.7f * a), idScale);

            DrawCloseButton(sb, px, font, cachedLayout.CloseBtn, topHover == TopHit.Close, a);

            //左侧侧栏
            MoldCategorySidebar.Draw(sb, px, font, cachedLayout, this, a);

            //Tab 栏
            DrawTabBar(sb, px, font, a);

            //内容区分发
            if (CodexMode) {
                MoldCodexPanel.Draw(sb, px, font, cachedLayout, this, a);
            }
            else {
                MoldProcessingPanel.Draw(sb, px, font, cachedLayout, this, a);
            }
        }

        private void DrawTabBar(SpriteBatch sb, Texture2D px, DynamicSpriteFont font, float a) {
            DrawTabBtn(sb, px, font, cachedLayout.TabWorkbench, TabWorkbench.Value,
                !CodexMode, topHover == TopHit.TabWorkbench, a);
            DrawTabBtn(sb, px, font, cachedLayout.TabCodex, TabCodex.Value,
                CodexMode, topHover == TopHit.TabCodex, a);
        }

        private static void DrawTabBtn(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            Rectangle r, string label, bool isActive, bool isHover, float a) {
            //投影
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(r.X + 2, r.Y + 2, r.Width, r.Height),
                new Color(0, 0, 0) * (0.4f * a));

            Color bg = isActive ? new Color(10, 45, 62) * (0.95f * a)
                : isHover ? new Color(8, 30, 44) * (0.9f * a)
                : new Color(4, 14, 22) * (0.85f * a);
            SHPCRenderer.DrawFilledRect(sb, px, r, bg);

            if (isActive) {
                SHPCRenderer.DrawFilledRect(sb, px,
                    new Rectangle(r.X, r.Y, r.Width, 2),
                    SHPCTheme.Cyan * (0.95f * a));
            }

            Color border = isActive ? SHPCTheme.CyanHi * (0.9f * a)
                : isHover ? SHPCTheme.Border * (0.85f * a)
                : SHPCTheme.Border * (0.55f * a);
            SHPCRenderer.DrawRectStroke(sb, px, r, 1.1f, border);

            if (isHover && !isActive) {
                SHPCRenderer.DrawCornerBrackets(sb, px, r, 3f, 1.1f, SHPCTheme.CyanHi * a);
            }

            //Tab 文本：当中文/英文较长时按宽度截断，并随之缩小一档
            float maxLabelW = r.Width - 12f;
            float scale = MoldFont.TabLabelBase * MoldFont.FontScale;
            string drawLabel = MoldFont.TruncateForWidth(font, label, maxLabelW, scale);
            Vector2 sz = font.MeasureString(drawLabel) * scale;
            Color textCol = isActive ? SHPCTheme.CyanHi * a
                : SHPCTheme.TextDim * (0.9f * a);
            Utils.DrawBorderString(sb, drawLabel,
                new Vector2(r.X + (r.Width - sz.X) * 0.5f, r.Y + (r.Height - sz.Y) * 0.5f),
                textCol, scale);
        }

        private static void DrawCloseButton(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            Rectangle r, bool isHover, float a) {
            Color bg = isHover ? new Color(60, 20, 20) * (0.9f * a) : new Color(20, 8, 12) * (0.85f * a);
            SHPCRenderer.DrawFilledRect(sb, px, r, bg);
            Color border = isHover ? new Color(255, 120, 110) * (0.95f * a) : SHPCTheme.Border * (0.7f * a);
            SHPCRenderer.DrawRectStroke(sb, px, r, 1.1f, border);

            float scale = MoldFont.CloseBtnBase * MoldFont.FontScale;
            Vector2 sz = font.MeasureString("X") * scale;
            Color textCol = isHover ? new Color(255, 200, 200) * a : SHPCTheme.TextDim * a;
            Utils.DrawBorderString(sb, "X",
                new Vector2(r.X + (r.Width - sz.X) * 0.5f, r.Y + (r.Height - sz.Y) * 0.5f - 1f),
                textCol, scale);
        }

        /// <summary>
        /// 直接复用 <see cref="UI.SHPCModPanel"/> 的着色器入口，只把焦点从枪体中心改为工作台预览中心
        /// </summary>
        private static void DrawShaderBackground(SpriteBatch sb, Texture2D px,
            Rectangle rect, Vector2 focus, float openProgress) {
            float a = openProgress;
            if (EffectLoader.SHPCModPanel?.Value == null) {
                SHPCRenderer.DrawFilledRect(sb, px, rect, new Color(4, 14, 22) * (0.96f * a));
                return;
            }
            Effect effect = EffectLoader.SHPCModPanel.Value;
            float time = (float)Main.GameUpdateCount / 60f;
            Vector2 focusRel = new(focus.X - rect.X, focus.Y - rect.Y);
            float focusRadius = 80f;

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(a * 0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uEdgePad"]?.SetValue(MoldLayout.EdgePad);
            effect.Parameters["uGunCenter"]?.SetValue(focusRel);
            effect.Parameters["uGunRadius"]?.SetValue(focusRadius);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, rect, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
