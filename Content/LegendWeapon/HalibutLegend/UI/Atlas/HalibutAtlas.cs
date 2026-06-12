using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas
{
    /// <summary>
    /// 深渊图鉴：比目鱼的全量技能管理界面（近全屏沉浸式）
    /// 左侧导航在「技能海域」与「领域之眼」两个场景间切换，
    /// 背景由 HalibutAtlasBg.fx 程序生成，随下潜深度与复苏躁动变化
    /// </summary>
    internal class HalibutAtlas : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText";
        public static HalibutAtlas Instance => UIHandleLoader.GetUIHandleOfType<HalibutAtlas>();

        #region 本地化
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText NavSea { get; private set; }
        public static LocalizedText NavEyes { get; private set; }
        private static LocalizedText[] tierNames = new LocalizedText[HalibutTheme.AtlasTierCount];
        public static LocalizedText DockLabel { get; private set; }
        public static LocalizedText LockedNodeName { get; private set; }
        public static LocalizedText LockedNodeHint { get; private set; }
        public static LocalizedText EquipBtn { get; private set; }
        public static LocalizedText UnequipBtn { get; private set; }
        public static LocalizedText SelectBtn { get; private set; }
        public static LocalizedText SelectedTag { get; private set; }
        public static LocalizedText LoadoutFullHint { get; private set; }
        public static LocalizedText UnlockFishLine { get; private set; }
        public static LocalizedText AltarTitle { get; private set; }
        public static LocalizedText AltarHint { get; private set; }
        public static LocalizedText DragHint { get; private set; }
        public static LocalizedText LayerStateFormat { get; private set; }
        public static LocalizedText UnlockCountFormat { get; private set; }

        public static string TierName(int tier) {
            tier = Math.Clamp(tier, 0, tierNames.Length - 1);
            return tierNames[tier]?.Value ?? string.Empty;
        }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "深 渊 图 鉴");
            NavSea = this.GetLocalization(nameof(NavSea), () => "技能海域");
            NavEyes = this.GetLocalization(nameof(NavEyes), () => "领域之眼");
            tierNames[0] = this.GetLocalization("TierName0", () => "浅滩 · 浮光层");
            tierNames[1] = this.GetLocalization("TierName1", () => "远洋 · 微光层");
            tierNames[2] = this.GetLocalization("TierName2", () => "深海 · 弱光层");
            tierNames[3] = this.GetLocalization("TierName3", () => "深渊 · 无光层");
            DockLabel = this.GetLocalization(nameof(DockLabel), () => "装备栏 {0} / {1} · 点击选用");
            LockedNodeName = this.GetLocalization(nameof(LockedNodeName), () => "未知的鱼影");
            LockedNodeHint = this.GetLocalization(nameof(LockedNodeHint), () => "在研究祭坛中研究 {0} 后点亮此技能");
            EquipBtn = this.GetLocalization(nameof(EquipBtn), () => "装 备");
            UnequipBtn = this.GetLocalization(nameof(UnequipBtn), () => "卸 下");
            SelectBtn = this.GetLocalization(nameof(SelectBtn), () => "选 用");
            SelectedTag = this.GetLocalization(nameof(SelectedTag), () => "当 前");
            LoadoutFullHint = this.GetLocalization(nameof(LoadoutFullHint), () => "装备栏已满");
            UnlockFishLine = this.GetLocalization(nameof(UnlockFishLine), () => "研究来源：{0}");
            AltarTitle = this.GetLocalization(nameof(AltarTitle), () => "研究祭坛");
            AltarHint = this.GetLocalization(nameof(AltarHint), () => "手持一条可研究的鱼点击祭坛放入，研究完成后对应技能将在海域中点亮");
            DragHint = this.GetLocalization(nameof(DragHint), () => "长按拖拽技能到装备栏 · 滚轮下潜");
            LayerStateFormat = this.GetLocalization(nameof(LayerStateFormat), () => "领域 {0} 层");
            UnlockCountFormat = this.GetLocalization(nameof(UnlockCountFormat), () => "已点亮 {0} / {1}");

            AtlasDomainEyes.RegisterLocalization();
            //研究完成的全局演出入口
            HalibutSave.StudyCompleted += OnStudyCompletedGlobal;
        }
        #endregion

        private enum AtlasView
        {
            Sea,
            Eyes,
        }

        private AtlasView view = AtlasView.Sea;
        public readonly AtlasSkillSea Sea = new();
        public readonly AtlasDomainEyes Eyes = new();
        private float headerSlide;
        //导航与关闭按钮命中区
        private Rectangle navSeaRect;
        private Rectangle navEyesRect;
        private readonly Rectangle[] tierChipRects = new Rectangle[HalibutTheme.AtlasTierCount];
        private Rectangle closeRect;

        public override bool Active => IsOpen || OpenProgress > 0.01f;

        public override bool CloseOnEscape => true;

        public override Terraria.Audio.SoundStyle? OpenSound => SoundID.MenuOpen with { Pitch = -0.35f, Volume = 0.6f };

        public override Terraria.Audio.SoundStyle? CloseSound => SoundID.MenuClose with { Pitch = -0.2f, Volume = 0.5f };

        protected override void OnOpen() {
            Main.playerInventory = false;
            view = AtlasView.Sea;
            headerSlide = 0f;
            Sea.Rebuild(player.GetModPlayer<HalibutSave>());
        }

        private static void OnStudyCompletedGlobal(FishSkill skill) {
            HalibutAtlas atlas = Instance;
            if (atlas == null) {
                return;
            }
            atlas.Sea.OnStudyCompleted(skill, atlas.IsOpen && atlas.view == AtlasView.Sea);
        }

        /// <summary>
        /// 图鉴内容区（页眉之下的全部区域）
        /// </summary>
        private static Rectangle ContentArea => new(0, 64, Main.screenWidth, Main.screenHeight - 64);

        public override void Update() {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            var save = player.GetModPlayer<HalibutSave>();

            if (IsOpen) {
                //打开期间整屏占用鼠标并阻止武器切换
                player.mouseInterface = true;
                player.CWR().DontSwitchWeaponTime = 2;
                //异常自动关闭
                if (!player.active || player.dead
                    || !player.TryGetOverride<HalibutPlayer>(out var hp) || !hp.HasHalubut) {
                    Close();
                }
            }

            headerSlide = MathHelper.Lerp(headerSlide, IsOpen ? 1f : 0f, 0.14f);

            //页眉布局
            int navY = 76;
            navSeaRect = new Rectangle(18, navY, 118, 30);
            navEyesRect = new Rectangle(18, navY + 38, 118, 30);
            closeRect = new Rectangle(Main.screenWidth - 46, 14, 32, 32);
            for (int t = 0; t < HalibutTheme.AtlasTierCount; t++) {
                tierChipRects[t] = new Rectangle(18, navY + 100 + t * 32, 118, 26);
            }

            bool inputAvailable = IsOpen && a > 0.9f;
            Vector2 mouse = MousePosition;

            if (inputAvailable && keyLeftPressState == KeyPressState.Pressed) {
                if (closeRect.Contains(mouse.ToPoint())) {
                    Close();
                    return;
                }
                if (navSeaRect.Contains(mouse.ToPoint()) && view != AtlasView.Sea) {
                    view = AtlasView.Sea;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f });
                }
                else if (navEyesRect.Contains(mouse.ToPoint()) && view != AtlasView.Eyes) {
                    view = AtlasView.Eyes;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.1f });
                }
                else if (view == AtlasView.Sea) {
                    for (int t = 0; t < HalibutTheme.AtlasTierCount; t++) {
                        if (tierChipRects[t].Contains(mouse.ToPoint())) {
                            Sea.JumpToTier(t);
                            break;
                        }
                    }
                }
            }

            //左侧导航区域上的指针视为被UI占用，避免点击穿透到场景
            bool overChrome = navSeaRect.Contains(mouse.ToPoint()) || navEyesRect.Contains(mouse.ToPoint())
                || closeRect.Contains(mouse.ToPoint());
            if (view == AtlasView.Sea) {
                for (int t = 0; t < HalibutTheme.AtlasTierCount; t++) {
                    overChrome |= tierChipRects[t].Contains(mouse.ToPoint());
                }
            }

            Rectangle content = ContentArea;
            if (view == AtlasView.Sea) {
                Sea.Update(content, save, a, inputAvailable && !overChrome);
            }
            else {
                Eyes.Update(content, save, a, inputAvailable && !overChrome);
            }
        }

        public override void Draw(SpriteBatch sb) {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            var save = player.GetModPlayer<HalibutSave>();
            player.TryGetOverride<HalibutPlayer>(out var hp);
            float agitation = hp?.ResurrectionSystem?.Ratio ?? 0f;
            float time = Main.GlobalTimeWrappedHourly;

            //1 海域背景（全屏着色器）
            float depth = view == AtlasView.Sea ? Sea.Depth : 0.86f;
            float scrollPx = view == AtlasView.Sea ? Sea.ScrollPx : 0f;
            HalibutRenderer.DrawAtlasBackground(sb,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), a * 0.97f,
                depth, agitation, scrollPx);

            Rectangle content = ContentArea;

            //2 场景内容
            if (view == AtlasView.Sea) {
                Sea.Draw(sb, content, save, a);
            }
            else {
                Eyes.Draw(sb, content, save, a);
            }

            //3 页眉
            DrawHeader(sb, save, hp, a, time);

            //4 左侧导航
            DrawNav(sb, a);

            //5 关闭按钮
            DrawCloseButton(sb, a, time);
        }

        private void DrawHeader(SpriteBatch sb, HalibutSave save, HalibutPlayer hp, float a, float time) {
            float slide = CWRUtils.EaseOutCubic(headerSlide);
            float y = MathHelper.Lerp(-40f, 16f, slide);
            //标题
            HalibutRenderer.DrawGlowText(sb, TitleText.Value, new Vector2(24f, y),
                HalibutTheme.Text * a, HalibutTheme.Glow * (0.45f * a), 1.12f, 1.6f);
            //页眉分割线
            Vector2 lineL = new(16f, y + 40f);
            Vector2 lineR = new(Main.screenWidth - 16f, y + 40f);
            HalibutRenderer.DrawGradientLine(sb, lineL, lineR,
                HalibutTheme.Glow * (0.55f * a), HalibutTheme.Glow * (0.04f * a), 1.3f);

            //右侧状态：解锁计数 / 领域层数 / 复苏
            int unlockedCount = save.unlocked.Count;
            int total = FishSkill.Instances?.Count ?? 0;
            string state = string.Format(UnlockCountFormat.Value, unlockedCount, total)
                + "    " + string.Format(LayerStateFormat.Value, save.ActiveEyeCount);
            if (hp?.ResurrectionSystem != null) {
                state += $"    {hp.ResurrectionSystem.Ratio * 100f:F0}%";
            }
            var font = Terraria.GameContent.FontAssets.MouseText.Value;
            float w = font.MeasureString(state).X * 0.8f;
            Color stateCol = agitationColor(hp);
            HalibutRenderer.DrawGlowText(sb, state, new Vector2(Main.screenWidth - w - 64f, y + 8f),
                stateCol * a, HalibutTheme.Deep * (0.5f * a), 0.8f);

            //操作提示（海域视图）
            if (view == AtlasView.Sea) {
                HalibutRenderer.DrawGlowTextCentered(sb, DragHint.Value,
                    new Vector2(Main.screenWidth * 0.5f, y + 30f),
                    HalibutTheme.TextDim * (0.75f * a), HalibutTheme.Deep * (0.4f * a), 0.7f);
            }
        }

        private static Color agitationColor(HalibutPlayer hp) {
            float ratio = hp?.ResurrectionSystem?.Ratio ?? 0f;
            if (ratio >= 0.9f) {
                return HalibutTheme.Danger;
            }
            if (ratio >= 0.7f) {
                return HalibutTheme.Accent;
            }
            return HalibutTheme.TextDim;
        }

        private void DrawNav(SpriteBatch sb, float a) {
            Vector2 mouse = MousePosition;
            DrawNavButton(sb, navSeaRect, NavSea.Value, view == AtlasView.Sea,
                navSeaRect.Contains(mouse.ToPoint()), a);
            DrawNavButton(sb, navEyesRect, NavEyes.Value, view == AtlasView.Eyes,
                navEyesRect.Contains(mouse.ToPoint()), a);

            if (view == AtlasView.Sea) {
                for (int t = 0; t < HalibutTheme.AtlasTierCount; t++) {
                    Rectangle rect = tierChipRects[t];
                    bool hovered = rect.Contains(mouse.ToPoint());
                    Color tierCol = HalibutTheme.TierColor(t);
                    float litT = MathHelper.Clamp(1f - MathF.Abs(Sea.Depth * (HalibutTheme.AtlasTierCount - 1) - t), 0f, 1f);
                    Texture2D px = HalibutRenderer.Pixel;
                    sb.Draw(px, rect, new Rectangle(0, 0, 1, 1),
                        Color.Lerp(HalibutTheme.Deep, HalibutTheme.Mid, litT * 0.6f + (hovered ? 0.3f : 0f)) * (0.82f * a));
                    HalibutRenderer.DrawLine(sb, new Vector2(rect.X, rect.Y), new Vector2(rect.X, rect.Bottom),
                        2f, tierCol * ((0.5f + litT * 0.5f) * a));
                    HalibutRenderer.DrawGlowText(sb, TierName(t), new Vector2(rect.X + 9f, rect.Y + 3f),
                        Color.Lerp(HalibutTheme.TextDim, tierCol, 0.4f + litT * 0.6f) * a,
                        tierCol * (0.25f * a), 0.68f);
                }
            }
        }

        private static void DrawNavButton(SpriteBatch sb, Rectangle rect, string text,
            bool selected, bool hovered, float a) {
            Texture2D px = HalibutRenderer.Pixel;
            float hi = selected ? 1f : hovered ? 0.55f : 0f;
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1),
                Color.Lerp(HalibutTheme.Deep, HalibutTheme.Mid, hi) * (0.88f * a));
            Color edge = Color.Lerp(HalibutTheme.Teal, HalibutTheme.GlowHi, hi);
            HalibutRenderer.DrawLine(sb, new Vector2(rect.X, rect.Y), new Vector2(rect.Right, rect.Y), 1.1f, edge * (0.8f * a));
            HalibutRenderer.DrawLine(sb, new Vector2(rect.X, rect.Bottom), new Vector2(rect.Right, rect.Bottom), 1.1f, edge * (0.55f * a));
            HalibutRenderer.DrawLine(sb, new Vector2(rect.X, rect.Y), new Vector2(rect.X, rect.Bottom), 2f, edge * a);
            HalibutRenderer.DrawGlowText(sb, text, new Vector2(rect.X + 12f, rect.Y + 4f),
                Color.Lerp(HalibutTheme.TextDim, HalibutTheme.Text, 0.4f + hi * 0.6f) * a,
                HalibutTheme.Glow * (hi * 0.35f * a), 0.8f);
        }

        private void DrawCloseButton(SpriteBatch sb, float a, float time) {
            Vector2 center = closeRect.Center.ToVector2();
            bool hovered = closeRect.Contains(MousePosition.ToPoint());
            float hi = hovered ? 1f : 0f;
            HalibutRenderer.DrawRing(sb, center, 14f, 1.2f,
                Color.Lerp(HalibutTheme.Teal, HalibutTheme.Danger, hi) * ((0.6f + hi * 0.4f) * a));
            Color xCol = Color.Lerp(HalibutTheme.TextDim, HalibutTheme.Danger, hi) * a;
            HalibutRenderer.DrawLine(sb, center + new Vector2(-5f, -5f), center + new Vector2(5f, 5f), 1.6f, xCol);
            HalibutRenderer.DrawLine(sb, center + new Vector2(5f, -5f), center + new Vector2(-5f, 5f), 1.6f, xCol);
        }
    }
}
