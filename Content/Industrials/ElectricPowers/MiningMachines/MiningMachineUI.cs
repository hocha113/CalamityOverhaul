using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MiningMachines
{
    /// <summary>
    /// 矿机勘探终端:地层剖面 + 勘探报告 + 模块槽。<br/>
    /// 报告数据与产出掷骰同源(<see cref="MiningMachineSystem.BuildReport"/>),
    /// 展示的份额即真实概率;模块槽编辑走"本地改 + SendData 推送"
    /// </summary>
    internal class MiningMachineUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static MiningMachineUI Instance => UIHandleLoader.GetUIHandleOfType<MiningMachineUI>();

        #region 布局与调色
        private const float PanelWidth = 540f;
        private const float PanelHeight = 420f;
        private const int RowHeight = 24;
        private const int SlotSize = 44;

        //工业域锈铁/余烬暗色系,勘探终端用收敛的琥珀作唯一亮色
        private static readonly Color BgDark = new(14, 11, 9);
        private static readonly Color BgMid = new(26, 19, 14);
        private static readonly Color FrameRust = new(140, 82, 44);
        private static readonly Color FrameGlow = new(200, 120, 60);
        private static readonly Color TextMain = new(232, 210, 180);
        private static readonly Color TextDim = new(150, 132, 112);
        private static readonly Color Amber = new(235, 170, 90);
        private static readonly Color WarnRed = new(255, 100, 80);
        private static readonly Color OkGreen = new(150, 220, 120);

        //地层剖面配色:天穹/地表土层/洞穴岩层/地狱
        private static readonly Color StrataSky = new(20, 24, 34);
        private static readonly Color StrataSoil = new(52, 38, 26);
        private static readonly Color StrataRock = new(36, 30, 28);
        private static readonly Color StrataHell = new(58, 22, 14);

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        #endregion

        #region 状态
        private BaseMiningMachineTP machine;
        private List<OreReportEntry> report = [];
        private int reportRefreshTimer;
        /// <summary>勘探扫掠动画进度 0..1,报告行随扫掠揭示</summary>
        private float scanProgress = 1f;
        private float scrollOffset;
        private float scrollTarget;
        private bool pendingCenter;

        //槽位拒绝反馈
        private int denyFlashSlot = -1;
        private int denyFlashTimer;
        private LocalizedText denyReason;

        //布局矩形,每帧在 Update 里重算
        private Rectangle panelRect;
        private Rectangle titleRect;
        private Rectangle closeRect;
        private Rectangle strataRect;
        private Rectangle reportRect;
        private Rectangle rescanRect;
        private Rectangle energyRect;
        private readonly List<Rectangle> slotRects = [];

        private float uiAlpha => OpenProgress.Current;
        #endregion

        #region 基类接入
        public override bool AutoUpdateHitBox => true;
        public override bool BlockMouseWhenHovered => true;
        public override bool CanDrag => true;
        public override MouseButtonType DragMouseButton => MouseButtonType.Left;
        public override Rectangle? DragHandleRect => titleRect;
        #endregion

        #region 本地化
        internal static LocalizedText TitleText;
        internal static LocalizedText ReportTitle;
        internal static LocalizedText RescanText;
        internal static LocalizedText ModuleSlotLabel;
        internal static LocalizedText EmptySlotHint;
        internal static LocalizedText StatusLabel;
        internal static LocalizedText StateWorking;
        internal static LocalizedText StateNoPower;
        internal static LocalizedText StateNoFooting;
        internal static LocalizedText PickPowerLine;
        internal static LocalizedText YieldRateLine;
        internal static LocalizedText EnergyLabel;
        internal static LocalizedText LayerLabel;
        internal static LocalizedText BiomeLabel;
        internal static LocalizedText VeinLabel;
        internal static LocalizedText LayerSurface;
        internal static LocalizedText LayerUnderground;
        internal static LocalizedText LayerCavern;
        internal static LocalizedText LayerUnderworld;
        internal static LocalizedText BiomeJungle;
        internal static LocalizedText BiomeSnow;
        internal static LocalizedText BiomeDesert;
        internal static LocalizedText BiomeCorrupt;
        internal static LocalizedText BiomeCrimson;
        internal static LocalizedText BiomeHallow;
        internal static LocalizedText BiomeNone;
        internal static LocalizedText GateNeedPick;
        internal static LocalizedText GateNeedDrill;
        internal static LocalizedText GateNotInWorld;
        internal static LocalizedText VeinTilesFormat;
        internal static LocalizedText ModuleOnly;
        internal static LocalizedText ModuleDuplicate;
        internal static LocalizedText ModuleTagText;
        internal static LocalizedText ModuleHowToText;
        internal static LocalizedText DrillTargetText;
        internal static LocalizedText DrillEffectText;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "Prospecting Terminal");
            ReportTitle = this.GetLocalization(nameof(ReportTitle), () => "Survey Report");
            RescanText = this.GetLocalization(nameof(RescanText), () => "Rescan");
            ModuleSlotLabel = this.GetLocalization(nameof(ModuleSlotLabel), () => "Modules");
            EmptySlotHint = this.GetLocalization(nameof(EmptySlotHint), () => "Insert an upgrade module");
            StatusLabel = this.GetLocalization(nameof(StatusLabel), () => "Status");
            StateWorking = this.GetLocalization(nameof(StateWorking), () => "Operating");
            StateNoPower = this.GetLocalization(nameof(StateNoPower), () => "No Power");
            StateNoFooting = this.GetLocalization(nameof(StateNoFooting), () => "Unstable Footing");
            PickPowerLine = this.GetLocalization(nameof(PickPowerLine), () => "Pick Power: {0}%");
            YieldRateLine = this.GetLocalization(nameof(YieldRateLine), () => "Yield: ~{0}/min");
            EnergyLabel = this.GetLocalization(nameof(EnergyLabel), () => "Energy");
            LayerLabel = this.GetLocalization(nameof(LayerLabel), () => "Stratum");
            BiomeLabel = this.GetLocalization(nameof(BiomeLabel), () => "Biome");
            VeinLabel = this.GetLocalization(nameof(VeinLabel), () => "Veins");
            LayerSurface = this.GetLocalization(nameof(LayerSurface), () => "Surface");
            LayerUnderground = this.GetLocalization(nameof(LayerUnderground), () => "Underground");
            LayerCavern = this.GetLocalization(nameof(LayerCavern), () => "Cavern");
            LayerUnderworld = this.GetLocalization(nameof(LayerUnderworld), () => "Underworld");
            BiomeJungle = this.GetLocalization(nameof(BiomeJungle), () => "Jungle");
            BiomeSnow = this.GetLocalization(nameof(BiomeSnow), () => "Snow");
            BiomeDesert = this.GetLocalization(nameof(BiomeDesert), () => "Desert");
            BiomeCorrupt = this.GetLocalization(nameof(BiomeCorrupt), () => "Corruption");
            BiomeCrimson = this.GetLocalization(nameof(BiomeCrimson), () => "Crimson");
            BiomeHallow = this.GetLocalization(nameof(BiomeHallow), () => "Hallow");
            BiomeNone = this.GetLocalization(nameof(BiomeNone), () => "Barren Rock");
            GateNeedPick = this.GetLocalization(nameof(GateNeedPick), () => "Requires {0}% pick power");
            GateNeedDrill = this.GetLocalization(nameof(GateNeedDrill), () => "Requires a dedicated drill module");
            GateNotInWorld = this.GetLocalization(nameof(GateNotInWorld), () => "Ore source not yet surveyed");
            VeinTilesFormat = this.GetLocalization(nameof(VeinTilesFormat), () => "{0} vein tiles detected");
            ModuleOnly = this.GetLocalization(nameof(ModuleOnly), () => "Only mining machine modules fit here!");
            ModuleDuplicate = this.GetLocalization(nameof(ModuleDuplicate), () => "A module of this type is already installed!");
            ModuleTagText = this.GetLocalization(nameof(ModuleTagText), () => "Mining Machine Module");
            ModuleHowToText = this.GetLocalization(nameof(ModuleHowToText), () => "Right-click a mining machine and slot this into its terminal");
            DrillTargetText = this.GetLocalization(nameof(DrillTargetText), () => "Targets: {0}");
            DrillEffectText = this.GetLocalization(nameof(DrillEffectText), () => "Grants extraction rights for its targets and quadruples their yield weight");
        }
        #endregion

        /// <summary>右键矿机时进入:同一台切换开合,不同台切换绑定</summary>
        public void Initialize(BaseMiningMachineTP target) {
            if (machine != target) {
                machine = target;
                if (!IsOpen) {
                    Open();
                }
                pendingCenter = true;
            }
            else {
                Toggle();
            }

            if (IsOpen) {
                StartScanSweep();
            }
        }

        private void StartScanSweep() {
            scanProgress = 0f;
            scrollTarget = 0f;
            scrollOffset = 0f;
            RebuildReport();
        }

        private void RebuildReport() {
            if (machine == null) {
                report.Clear();
                return;
            }
            MiningContext ctx = machine.BuildContext();
            report = MiningMachineSystem.BuildReport(in ctx);
        }

        public override void Update() {
            Size = new Vector2(PanelWidth, PanelHeight);

            if (pendingCenter) {
                pendingCenter = false;
                DrawPosition = new Vector2((UIScreenW - PanelWidth) * 0.5f, UIScreenH * 0.16f);
            }

            //绑定失效或走远时收摊
            if (IsOpen && (machine == null || !machine.Active
                || machine.CenterInWorld.To(player.Center).Length() > 420)) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f, Volume = 0.6f });
                Close();
                return;
            }

            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 8f, UIScreenW - PanelWidth - 8f);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 8f, UIScreenH - PanelHeight - 8f);

            ComputeLayout();

            if (scanProgress < 1f) {
                scanProgress = MathF.Min(1f, scanProgress + 0.025f);
            }
            if (denyFlashTimer > 0) {
                denyFlashTimer--;
            }

            if (uiAlpha < 0.01f || machine == null) {
                return;
            }

            //报告周期刷新,拿到的是与掷骰同源的数据
            if (++reportRefreshTimer >= 15) {
                reportRefreshTimer = 0;
                RebuildReport();
            }

            UpdateScroll();
            HandleClicks();
        }

        private void ComputeLayout() {
            Vector2 topLeft = DrawPosition;
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);
            titleRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 44);
            closeRect = new Rectangle(panelRect.Right - 34, panelRect.Y + 11, 22, 22);

            strataRect = new Rectangle(panelRect.X + 18, panelRect.Y + 60, 64, 232);
            reportRect = new Rectangle(panelRect.X + 98, panelRect.Y + 60, panelRect.Width - 98 - 18, 24 + RowHeight * 8);
            rescanRect = new Rectangle(reportRect.Right - 96, reportRect.Y + 1, 96, 20);

            //槽行独占底行,六槽也放得下;状态三项横排在报告下方,能量条横贯其下
            slotRects.Clear();
            if (machine != null) {
                int slotCount = machine.ModuleSlotCount;
                int slotsY = panelRect.Y + 354;
                for (int i = 0; i < slotCount; i++) {
                    slotRects.Add(new Rectangle(panelRect.X + 98 + i * (SlotSize + 9), slotsY, SlotSize, SlotSize));
                }
            }

            energyRect = new Rectangle(panelRect.X + 150, panelRect.Y + 322, (int)PanelWidth - 150 - 18, 10);
        }

        private void UpdateScroll() {
            int rowCount = report.Count;
            int visibleRows = (reportRect.Height - 24) / RowHeight;
            float maxScroll = Math.Max(0, rowCount - visibleRows) * RowHeight;

            if (reportRect.Contains(MousePosition.ToPoint())) {
                int delta = PlayerInput.ScrollWheelDeltaForUI;
                if (delta != 0) {
                    scrollTarget -= delta * 0.4f;
                    PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/MiningMachine");
                }
            }
            scrollTarget = MathHelper.Clamp(scrollTarget, 0, maxScroll);
            scrollOffset = MathHelper.Lerp(scrollOffset, scrollTarget, 0.25f);
        }

        private void HandleClicks() {
            if (IsDragging || UIHandleLoader.keyLeftPressState != KeyPressState.Pressed) {
                return;
            }
            Point mouse = MousePosition.ToPoint();

            if (closeRect.Contains(mouse)) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f, Volume = 0.6f });
                Close();
                return;
            }

            if (rescanRect.Contains(mouse)) {
                machine.RescanNow();
                StartScanSweep();
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f });
                return;
            }

            for (int i = 0; i < slotRects.Count; i++) {
                if (slotRects[i].Contains(mouse)) {
                    HandleSlotClick(i);
                    return;
                }
            }
        }

        /// <summary>模块槽点击:本地改动后 SendData 推送,同类模块每台限一枚</summary>
        private void HandleSlotClick(int index) {
            Item[] modules = machine.EnsureModules();
            if (index >= modules.Length) {
                return;
            }
            Item slot = modules[index];
            Item mouse = Main.mouseItem;

            if (mouse.IsAir && slot.IsAir) {
                return;
            }

            if (!mouse.IsAir) {
                if (mouse.ModItem is not IMiningModule) {
                    Deny(index, ModuleOnly);
                    return;
                }
                if (machine.HasModuleType(mouse.type, ignoreSlot: index)) {
                    Deny(index, ModuleDuplicate);
                    return;
                }
                //放入/交换
                Item swap = slot.IsAir ? new Item() : slot.Clone();
                modules[index] = mouse.Clone();
                modules[index].stack = 1;
                Main.mouseItem = swap;
            }
            else {
                //取出:Shift 直接回背包,MP下地面掉落会被队友截走
                if (Main.keyState.PressingShift()) {
                    player.GiveItem(new EntitySource_WorldEvent(), slot.Clone());
                }
                else {
                    Main.mouseItem = slot.Clone();
                }
                modules[index] = new Item();
            }

            SoundEngine.PlaySound(SoundID.Grab);
            machine.MarkModulesDirty();
            machine.SendData();
            //勘探阵列这类改扫描尺寸的模块要立刻重扫才能反映在报告里
            machine.RescanNow();
            RebuildReport();
        }

        private void Deny(int slotIndex, LocalizedText reason) {
            denyFlashSlot = slotIndex;
            denyFlashTimer = 40;
            denyReason = reason;
            SoundEngine.PlaySound(SoundID.MenuClose);
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiAlpha < 0.01f || machine == null) {
                return;
            }

            DrawPanel(spriteBatch);
            DrawStrata(spriteBatch);
            DrawReadouts(spriteBatch);
            DrawReport(spriteBatch);
            DrawSlots(spriteBatch);
            DrawStatus(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        private void DrawPanel(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiAlpha;
            Rectangle src = new(0, 0, 1, 1);

            //底色:自上而下的锈土渐层,分段着色避免大平面
            int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)((i + 1) / (float)segments * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));
                float pulse = MathF.Sin(GlobalTimer * 0.8f + t * 2.2f) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(BgDark, BgMid, pulse * 0.6f + t * 0.3f);
                sb.Draw(px, r, src, baseColor * (alpha * 0.92f));
            }

            //横向岩纹细线,给面板一点地质档案的质感
            int lines = 9;
            for (int i = 1; i < lines; i++) {
                float t = i / (float)lines;
                int y = panelRect.Y + 48 + (int)(t * (panelRect.Height - 60));
                float bright = MathF.Sin(GlobalTimer * 0.5f + t * MathHelper.Pi) * 0.5f + 0.5f;
                sb.Draw(px, new Rectangle(panelRect.X + 14, y, panelRect.Width - 28, 1), src,
                    new Color(80, 48, 30) * (alpha * 0.06f * bright));
            }

            //边框:外圈锈铁,顶缘受光
            float framePulse = MathF.Sin(GlobalTimer * 1.4f) * 0.5f + 0.5f;
            Color edge = Color.Lerp(FrameRust, FrameGlow, framePulse * 0.4f) * (alpha * 0.8f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 3), src, edge);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 3, panelRect.Width, 3), src, edge * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 3, panelRect.Height), src, edge * 0.85f);
            sb.Draw(px, new Rectangle(panelRect.Right - 3, panelRect.Y, 3, panelRect.Height), src, edge * 0.85f);

            //标题栏分隔线
            sb.Draw(px, new Rectangle(panelRect.X + 10, titleRect.Bottom - 2, panelRect.Width - 20, 1), src,
                FrameGlow * (alpha * 0.35f));

            //标题
            string title = TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.95f;
            Vector2 titlePos = new(panelRect.X + 18, titleRect.Center.Y - titleSize.Y * 0.5f + 2);
            Utils.DrawBorderString(sb, title, titlePos + new Vector2(1.2f, 1.2f), FrameGlow * (alpha * 0.4f), 0.95f);
            Utils.DrawBorderString(sb, title, titlePos, TextMain * alpha, 0.95f);

            //机器名挂在标题右侧
            string name = Lang.GetItemNameValue(machine.TargetItem);
            Utils.DrawBorderString(sb, name, titlePos + new Vector2(titleSize.X + 14, 4), TextDim * alpha, 0.7f);

            //关闭钮:两道交叉短杠
            bool closeHover = closeRect.Contains(MousePosition.ToPoint());
            Color closeColor = (closeHover ? WarnRed : TextDim) * alpha;
            Vector2 closeCenter = closeRect.Center.ToVector2();
            sb.Draw(px, closeCenter, src, closeColor, MathHelper.PiOver4, new Vector2(0.5f),
                new Vector2(14f, 2f), SpriteEffects.None, 0f);
            sb.Draw(px, closeCenter, src, closeColor, -MathHelper.PiOver4, new Vector2(0.5f),
                new Vector2(14f, 2f), SpriteEffects.None, 0f);
        }

        private void DrawStrata(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiAlpha;
            Rectangle src = new(0, 0, 1, 1);
            MiningSurvey survey = machine.Survey;

            //把世界纵深映射进剖面柱
            float worldToCol(float worldY) =>
                strataRect.Y + MathHelper.Clamp(worldY / Main.maxTilesY, 0f, 1f) * strataRect.Height;

            float surfaceY = worldToCol((float)Main.worldSurface);
            float rockY = worldToCol((float)Main.rockLayer);
            float hellY = worldToCol(Main.maxTilesY - 204);

            //四段地层
            void band(float top, float bottom, Color color) {
                int y0 = (int)top;
                int h = Math.Max(1, (int)bottom - y0);
                sb.Draw(px, new Rectangle(strataRect.X, y0, strataRect.Width, h), src, color * (alpha * 0.9f));
            }
            band(strataRect.Y, surfaceY, StrataSky);
            band(surfaceY, rockY, StrataSoil);
            band(rockY, hellY, StrataRock);
            band(hellY, strataRect.Bottom, StrataHell);

            //层界细线
            Span<float> boundaries = [surfaceY, rockY, hellY];
            foreach (float y in boundaries) {
                sb.Draw(px, new Rectangle(strataRect.X, (int)y, strataRect.Width, 1), src,
                    new Color(90, 60, 40) * (alpha * 0.5f));
            }

            //扫描窗:机器实际"看见"的柱段
            if (survey != null) {
                float sliceTop = worldToCol(survey.Anchor.Y);
                float sliceBottom = worldToCol(survey.Anchor.Y + survey.Depth);
                int st = (int)sliceTop;
                int sh = Math.Max(3, (int)sliceBottom - st);
                Rectangle slice = new(strataRect.X + 3, st, strataRect.Width - 6, sh);
                float reveal = scanProgress;
                sb.Draw(px, new Rectangle(slice.X, slice.Y, slice.Width, (int)(slice.Height * reveal)), src,
                    Amber * (alpha * 0.12f));
                sb.Draw(px, new Rectangle(slice.X, slice.Y, slice.Width, 1), src, Amber * (alpha * 0.7f));
                int sweepY = slice.Y + (int)(slice.Height * reveal);
                if (reveal < 1f) {
                    sb.Draw(px, new Rectangle(slice.X, sweepY, slice.Width, 2), src, Amber * (alpha * 0.9f));
                }
                else {
                    sb.Draw(px, new Rectangle(slice.X, slice.Bottom, slice.Width, 1), src, Amber * (alpha * 0.45f));
                }

                //矿脉亮点:按报告里探明的矿,伪随机撒进扫描窗
                int glintBudget = 0;
                foreach (OreReportEntry entry in report) {
                    if (entry.VeinTiles <= 0 || glintBudget >= 18) {
                        continue;
                    }
                    int dots = Math.Min(1 + entry.VeinTiles / 12, 4);
                    for (int k = 0; k < dots && glintBudget < 18; k++, glintBudget++) {
                        //确定性散布:同一台机器每帧稳定,不闪跳
                        int hash = entry.ItemID * 73856093 ^ (k + 1) * 19349663 ^ machine.WhoAmI * 83492791;
                        float fx = (hash & 0xFFFF) / 65535f;
                        float fy = ((hash >> 12) & 0xFFFF) / 65535f;
                        if (fy > reveal) {
                            continue;
                        }
                        Vector2 dot = new(slice.X + 4 + fx * (slice.Width - 8), slice.Y + 3 + fy * (slice.Height - 6));
                        float tw = MathF.Sin(GlobalTimer * 2.2f + hash % 7) * 0.5f + 0.5f;
                        sb.Draw(px, dot, src, Color.Lerp(Amber, Color.White, 0.4f) * (alpha * (0.35f + tw * 0.45f)),
                            MathHelper.PiOver4, new Vector2(0.5f), new Vector2(2.4f), SpriteEffects.None, 0f);
                    }
                }

                //机器位置标记:一枚指向剖面的琥珀楔
                float markY = worldToCol(survey.Anchor.Y);
                Vector2 mark = new(strataRect.X - 5, markY);
                sb.Draw(px, mark, src, Amber * alpha, MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(6f, 6f), SpriteEffects.None, 0f);
            }

            //剖面柱包边
            Color edge = FrameRust * (alpha * 0.7f);
            sb.Draw(px, new Rectangle(strataRect.X - 1, strataRect.Y - 1, strataRect.Width + 2, 1), src, edge);
            sb.Draw(px, new Rectangle(strataRect.X - 1, strataRect.Bottom, strataRect.Width + 2, 1), src, edge);
            sb.Draw(px, new Rectangle(strataRect.X - 1, strataRect.Y, 1, strataRect.Height), src, edge);
            sb.Draw(px, new Rectangle(strataRect.Right, strataRect.Y, 1, strataRect.Height), src, edge);
        }

        private string LayerName(MiningSurvey survey) {
            if (survey == null) {
                return "-";
            }
            if (survey.IsUnderworld) {
                return LayerUnderworld.Value;
            }
            if (survey.IsCavern) {
                return LayerCavern.Value;
            }
            if (survey.IsUnderground) {
                return LayerUnderground.Value;
            }
            return LayerSurface.Value;
        }

        private string BiomeNames(MiningSurvey survey) {
            if (survey == null) {
                return "-";
            }
            StringBuilder sb = new();
            void append(string name) {
                if (sb.Length > 0) {
                    sb.Append('·');
                }
                sb.Append(name);
            }
            if (survey.IsJungle) append(BiomeJungle.Value);
            if (survey.IsSnow) append(BiomeSnow.Value);
            if (survey.IsDesert) append(BiomeDesert.Value);
            if (survey.IsCorrupt) append(BiomeCorrupt.Value);
            if (survey.IsCrimson) append(BiomeCrimson.Value);
            if (survey.IsHallow) append(BiomeHallow.Value);
            return sb.Length > 0 ? sb.ToString() : BiomeNone.Value;
        }

        private void DrawReadouts(SpriteBatch sb) {
            float alpha = uiAlpha;
            MiningSurvey survey = machine.Survey;
            float y = strataRect.Bottom + 8;
            float x = strataRect.X;

            void line(string label, string value, Color valueColor) {
                Utils.DrawBorderString(sb, label, new Vector2(x, y), TextDim * alpha, 0.62f);
                Utils.DrawBorderString(sb, value, new Vector2(x, y + 13), valueColor * alpha, 0.66f);
                y += 28;
            }

            line(LayerLabel.Value, LayerName(survey), TextMain);
            line(BiomeLabel.Value, BiomeNames(survey), TextMain);
            line(VeinLabel.Value, survey != null ? survey.TotalOreTiles.ToString() : "-",
                survey != null && survey.TotalOreTiles > 0 ? Amber : TextDim);
        }

        private void DrawReport(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiAlpha;
            Rectangle src = new(0, 0, 1, 1);

            //报告头
            Utils.DrawBorderString(sb, ReportTitle.Value, new Vector2(reportRect.X, reportRect.Y), TextMain * alpha, 0.78f);

            //重新勘探按钮
            bool rescanHover = rescanRect.Contains(MousePosition.ToPoint());
            float hoverGlow = rescanHover ? 0.5f : 0f;
            sb.Draw(px, rescanRect, src, BgMid * (alpha * 0.9f));
            Color btnEdge = Color.Lerp(FrameRust, FrameGlow, hoverGlow) * (alpha * 0.9f);
            sb.Draw(px, new Rectangle(rescanRect.X, rescanRect.Y, rescanRect.Width, 1), src, btnEdge);
            sb.Draw(px, new Rectangle(rescanRect.X, rescanRect.Bottom - 1, rescanRect.Width, 1), src, btnEdge);
            sb.Draw(px, new Rectangle(rescanRect.X, rescanRect.Y, 1, rescanRect.Height), src, btnEdge);
            sb.Draw(px, new Rectangle(rescanRect.Right - 1, rescanRect.Y, 1, rescanRect.Height), src, btnEdge);
            string rescan = RescanText.Value;
            Vector2 rescanSize = FontAssets.MouseText.Value.MeasureString(rescan) * 0.62f;
            Utils.DrawBorderString(sb, rescan,
                new Vector2(rescanRect.Center.X - rescanSize.X * 0.5f, rescanRect.Center.Y - rescanSize.Y * 0.5f),
                Color.Lerp(TextMain, Amber, hoverGlow) * alpha, 0.62f);

            //行区
            Rectangle rowsRect = new(reportRect.X, reportRect.Y + 24, reportRect.Width, reportRect.Height - 24);
            int visibleRows = rowsRect.Height / RowHeight;
            float sweepReveal = scanProgress * report.Count;

            for (int i = 0; i < report.Count; i++) {
                float rowY = rowsRect.Y + i * RowHeight - scrollOffset;
                if (rowY < rowsRect.Y - RowHeight || rowY > rowsRect.Bottom) {
                    continue;
                }
                //越出视口上下缘时淡出,行区不开裁剪
                float edgeFade = 1f;
                if (rowY < rowsRect.Y) {
                    edgeFade = 1f - (rowsRect.Y - rowY) / RowHeight;
                }
                else if (rowY > rowsRect.Bottom - RowHeight) {
                    edgeFade = 1f - (rowY - (rowsRect.Bottom - RowHeight)) / RowHeight;
                }
                //扫掠揭示
                float reveal = MathHelper.Clamp(sweepReveal - i, 0f, 1f);
                if (reveal <= 0f) {
                    continue;
                }
                DrawReportRow(sb, report[i], new Rectangle(rowsRect.X, (int)rowY, rowsRect.Width, RowHeight),
                    alpha * edgeFade * reveal, i);
            }

            //扫掠亮线
            if (scanProgress < 1f) {
                float sweepY = rowsRect.Y + MathHelper.Clamp(sweepReveal * RowHeight - scrollOffset, 0, rowsRect.Height);
                sb.Draw(px, new Rectangle(rowsRect.X, (int)sweepY, rowsRect.Width, 2), src, Amber * (alpha * 0.8f));
            }

            //溢出指示:右缘细琥珀迹
            int rowCount = report.Count;
            if (rowCount > visibleRows) {
                float viewRatio = visibleRows / (float)rowCount;
                float posRatio = scrollOffset / (rowCount * RowHeight);
                int trackH = rowsRect.Height - 4;
                int barH = Math.Max(12, (int)(trackH * viewRatio));
                int barY = rowsRect.Y + 2 + (int)(posRatio * trackH);
                sb.Draw(px, new Rectangle(rowsRect.Right + 3, rowsRect.Y + 2, 1, trackH), src, FrameRust * (alpha * 0.35f));
                sb.Draw(px, new Rectangle(rowsRect.Right + 2, barY, 3, barH), src, Amber * (alpha * 0.6f));
            }
        }

        private void DrawReportRow(SpriteBatch sb, OreReportEntry entry, Rectangle row, float alpha, int index) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            bool open = entry.Gate == OreGate.Open;
            bool hover = row.Contains(MousePosition.ToPoint());

            //行底:开采行随权重着一点琥珀,锁定行沉灰
            Color rowBg = open ? Color.Lerp(BgMid, new Color(52, 32, 18), Math.Min(entry.Share * 3f, 1f))
                : new Color(20, 16, 13);
            sb.Draw(px, row, src, rowBg * (alpha * (hover ? 0.95f : 0.8f)));
            if (hover) {
                sb.Draw(px, new Rectangle(row.X, row.Y, 2, row.Height), src, Amber * (alpha * 0.8f));
            }

            //矿物图标
            VaultUtils.SimpleDrawItem(sb, entry.ItemID, new Vector2(row.X + 14, row.Center.Y), 20, 1f, 0,
                Color.White * (open ? alpha : alpha * 0.45f));

            //名称
            string name = Lang.GetItemNameValue(entry.ItemID);
            Color nameColor = open ? TextMain : TextDim;
            Utils.DrawBorderString(sb, name, new Vector2(row.X + 30, row.Y + 4), nameColor * alpha, 0.68f);

            if (open) {
                //份额条 + 百分比:与掷骰同源
                int barX = row.X + 176;
                int barW = row.Width - 176 - 58;
                sb.Draw(px, new Rectangle(barX, row.Center.Y - 3, barW, 6), src, new Color(30, 22, 16) * alpha);
                int fillW = (int)(barW * MathHelper.Clamp(entry.Share, 0f, 1f));
                if (fillW > 0) {
                    float pulse = MathF.Sin(GlobalTimer * 1.8f + index * 0.7f) * 0.15f + 0.85f;
                    sb.Draw(px, new Rectangle(barX, row.Center.Y - 3, fillW, 6), src, Amber * (alpha * pulse));
                }
                string share = (entry.Share * 100f).ToString(entry.Share >= 0.095f ? "0" : "0.0") + "%";
                Vector2 shareSize = FontAssets.MouseText.Value.MeasureString(share) * 0.66f;
                Utils.DrawBorderString(sb, share, new Vector2(row.Right - 8 - shareSize.X, row.Y + 4),
                    TextMain * alpha, 0.66f);

                //有真实矿脉的行,在名称后点一枚亮钉
                if (entry.VeinTiles > 0) {
                    Vector2 pin = new(row.X + 166, row.Center.Y);
                    sb.Draw(px, pin, src, Color.Lerp(Amber, Color.White, 0.4f) * (alpha * 0.9f),
                        MathHelper.PiOver4, new Vector2(0.5f), new Vector2(3f), SpriteEffects.None, 0f);
                }
            }
            else {
                //门控原因
                string reason = entry.Gate switch {
                    OreGate.NeedPick => GateNeedPick.Format((int)entry.RequiredPick),
                    OreGate.NeedDrill => GateNeedDrill.Value,
                    OreGate.NotInWorld => GateNotInWorld.Value,
                    _ => string.Empty,
                };
                Color reasonColor = entry.Gate switch {
                    OreGate.NeedPick => Color.Lerp(WarnRed, TextDim, 0.35f),
                    OreGate.NeedDrill => Color.Lerp(OkGreen, TextDim, 0.4f),
                    _ => new Color(120, 128, 150),
                };
                Vector2 reasonSize = FontAssets.MouseText.Value.MeasureString(reason) * 0.62f;
                Utils.DrawBorderString(sb, reason, new Vector2(row.Right - 8 - reasonSize.X, row.Y + 5),
                    reasonColor * alpha, 0.62f);
            }
        }

        private void DrawSlots(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiAlpha;
            Rectangle src = new(0, 0, 1, 1);
            Item[] modules = machine.EnsureModules();

            Utils.DrawBorderString(sb, ModuleSlotLabel.Value,
                new Vector2(slotRects.Count > 0 ? slotRects[0].X : panelRect.X + 98, panelRect.Y + 338),
                TextDim * alpha, 0.62f);

            for (int i = 0; i < slotRects.Count; i++) {
                Rectangle rect = slotRects[i];
                bool hover = rect.Contains(MousePosition.ToPoint());
                bool denied = denyFlashTimer > 0 && denyFlashSlot == i;

                sb.Draw(px, rect, src, new Color(18, 13, 10) * (alpha * 0.92f));

                float glow = hover ? 0.4f : 0f;
                Color edge = Color.Lerp(FrameRust, FrameGlow, MathF.Sin(GlobalTimer * 1.3f) * 0.25f + 0.25f + glow);
                if (denied) {
                    float flash = denyFlashTimer / 40f;
                    edge = Color.Lerp(edge, WarnRed, flash);
                }
                edge *= alpha * 0.85f;
                sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), src, edge);
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), src, edge);
                sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), src, edge);
                sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), src, edge);

                Item item = i < modules.Length ? modules[i] : null;
                if (item != null && !item.IsAir) {
                    if (item.ModItem is BaseMiningModule module) {
                        module.DrawIcon(sb, rect.Center.ToVector2(), 15f, alpha);
                    }
                    else {
                        VaultUtils.SimpleDrawItem(sb, item.type, rect.Center.ToVector2(), 32, 1f, 0, Color.White * alpha);
                    }
                }
                else {
                    //空槽:一枚暗刻的钻齿纹占位
                    Vector2 c = rect.Center.ToVector2();
                    sb.Draw(px, c, src, TextDim * (alpha * 0.18f), MathHelper.PiOver4, new Vector2(0.5f),
                        new Vector2(10f, 2f), SpriteEffects.None, 0f);
                    sb.Draw(px, c, src, TextDim * (alpha * 0.18f), -MathHelper.PiOver4, new Vector2(0.5f),
                        new Vector2(10f, 2f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawStatus(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiAlpha;
            Rectangle src = new(0, 0, 1, 1);
            float x = panelRect.X + 98;
            float y = panelRect.Y + 302;

            //状态行
            string state;
            Color stateColor;
            if (!machine.Powered) {
                state = StateNoPower.Value;
                stateColor = WarnRed;
            }
            else if (!machine.FootingOk) {
                state = StateNoFooting.Value;
                stateColor = Color.Lerp(WarnRed, Amber, 0.5f);
            }
            else {
                state = StateWorking.Value;
                stateColor = OkGreen;
                float blink = MathF.Sin(GlobalTimer * 4f) * 0.2f + 0.8f;
                stateColor *= blink;
            }
            //状态/镐力/产率横排一行,挤在报告与能量条之间
            Utils.DrawBorderString(sb, StatusLabel.Value, new Vector2(x, y), TextDim * alpha, 0.62f);
            float labelW = FontAssets.MouseText.Value.MeasureString(StatusLabel.Value).X * 0.62f;
            Utils.DrawBorderString(sb, state, new Vector2(x + labelW + 8, y), stateColor * alpha, 0.66f);

            machine.RefreshModifiers();
            Utils.DrawBorderString(sb, PickPowerLine.Format((int)machine.EffectivePickPower),
                new Vector2(panelRect.X + 278, y), TextMain * alpha, 0.62f);
            Utils.DrawBorderString(sb, YieldRateLine.Format(machine.EstimateYieldPerMinute().ToString("0.0")),
                new Vector2(panelRect.X + 398, y), TextMain * alpha, 0.62f);

            //能量条(横贯),标签内联在左
            Utils.DrawBorderString(sb, EnergyLabel.Value,
                new Vector2(panelRect.X + 98, energyRect.Y - 3), TextDim * alpha, 0.58f);
            sb.Draw(px, energyRect, src, new Color(16, 12, 10) * (alpha * 0.95f));
            float ratio = MathHelper.Clamp(machine.MachineData.UEvalue / machine.MaxUEValue, 0f, 1f);
            int fillW = (int)((energyRect.Width - 4) * ratio);
            if (fillW > 0) {
                int fillSegs = Math.Max(1, fillW / 8);
                for (int i = 0; i < fillSegs; i++) {
                    float t = i / (float)fillSegs;
                    int x1 = energyRect.X + 2 + (int)(t * fillW);
                    int x2 = energyRect.X + 2 + (int)((i + 1) / (float)fillSegs * fillW);
                    float pulse = MathF.Sin(GlobalTimer * 2.5f - t * 4f) * 0.18f + 0.82f;
                    sb.Draw(px, new Rectangle(x1, energyRect.Y + 2, Math.Max(1, x2 - x1), energyRect.Height - 4), src,
                        Color.Lerp(new Color(120, 62, 30), Amber, t) * (alpha * pulse));
                }
            }
            Color barEdge = FrameRust * (alpha * 0.8f);
            sb.Draw(px, new Rectangle(energyRect.X, energyRect.Y, energyRect.Width, 1), src, barEdge);
            sb.Draw(px, new Rectangle(energyRect.X, energyRect.Bottom - 1, energyRect.Width, 1), src, barEdge);
            sb.Draw(px, new Rectangle(energyRect.X, energyRect.Y, 1, energyRect.Height), src, barEdge);
            sb.Draw(px, new Rectangle(energyRect.Right - 1, energyRect.Y, 1, energyRect.Height), src, barEdge);
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (IsDragging) {
                return;
            }
            Point mouse = MousePosition.ToPoint();

            //模块槽悬停
            Item[] modules = machine.EnsureModules();
            for (int i = 0; i < slotRects.Count; i++) {
                if (!slotRects[i].Contains(mouse)) {
                    continue;
                }
                if (denyFlashTimer > 0 && denyFlashSlot == i && denyReason != null) {
                    ShowTooltip(sb, denyReason.Value, WarnRed);
                }
                else if (i < modules.Length && !modules[i].IsAir) {
                    Main.HoverItem = modules[i].Clone();
                    Main.hoverItemName = modules[i].Name;
                }
                else {
                    ShowTooltip(sb, EmptySlotHint.Value, TextMain);
                }
                return;
            }

            //能量条悬停
            if (energyRect.Contains(mouse)) {
                ShowTooltip(sb, $"{(int)machine.MachineData.UEvalue}/{(int)machine.MaxUEValue} UE", TextMain);
                return;
            }

            //报告行悬停:矿脉探明详情
            Rectangle rowsRect = new(reportRect.X, reportRect.Y + 24, reportRect.Width, reportRect.Height - 24);
            if (rowsRect.Contains(mouse)) {
                int index = (int)((mouse.Y - rowsRect.Y + scrollOffset) / RowHeight);
                if (index >= 0 && index < report.Count) {
                    OreReportEntry entry = report[index];
                    if (entry.VeinTiles > 0) {
                        ShowTooltip(sb, VeinTilesFormat.Format(entry.VeinTiles), Amber);
                    }
                }
            }
        }

        private void ShowTooltip(SpriteBatch sb, string text, Color color) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
            Vector2 pos = MousePosition + new Vector2(18, 18);
            //贴屏缘时翻转与钳制
            if (pos.X + textSize.X + 20 > UIScreenW) {
                pos.X = MousePosition.X - textSize.X - 24;
            }
            if (pos.Y + textSize.Y + 12 > UIScreenH) {
                pos.Y = MousePosition.Y - textSize.Y - 18;
            }

            Rectangle bg = new((int)pos.X - 8, (int)pos.Y - 5, (int)textSize.X + 16, (int)textSize.Y + 10);
            sb.Draw(px, bg, src, new Color(14, 10, 8) * 0.95f);
            sb.Draw(px, new Rectangle(bg.X, bg.Y, bg.Width, 2), src, FrameRust * 0.85f);
            sb.Draw(px, new Rectangle(bg.X, bg.Y, 2, bg.Height), src, FrameRust * 0.85f);
            Utils.DrawBorderString(sb, text, pos, color, 0.75f);
        }
        #endregion
    }
}
