using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.UIs;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MiningMachines
{
    /// <summary>
    /// 矿机勘探终端:野外地质仪器语言——钢壳(shader)、黄铜铭牌、岩芯样本管、
    /// 指针仪表、模块插座。报告数据与产出掷骰同源(<see cref="MiningMachineSystem.BuildReport"/>),
    /// 展示的份额即真实概率;模块槽编辑走"本地改 + SendData 推送"。<br/>
    /// 笔刷与材质在 <see cref="IndustrialTerminalRenderer"/>,本类只管布局、交互与编排
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

        //文字与点缀色引用渲染器主题,保证 CPU 前景与 shader 机壳同族
        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color Amber => IndustrialTerminalRenderer.Amber;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color OkGreen => IndustrialTerminalRenderer.OkGreen;
        private static Color BrassBright => IndustrialTerminalRenderer.BrassBright;

        //岩芯样本配色:天穹/地表土层/洞穴岩层/地狱
        private static readonly Color StrataSky = new(20, 24, 34);
        private static readonly Color StrataSoil = new(52, 38, 26);
        private static readonly Color StrataRock = new(36, 30, 28);
        private static readonly Color StrataHell = new(58, 22, 14);

        //铭牌底缘巡行亮笔的路径:一条横线
        private const string RunnerLinePath = "M -1 0 L 1 0";

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

        //模块插座行(点击/校验/红闪/绘制全在共享件里)
        private readonly ModuleSocketStrip socketStrip = new();

        //仪表指针的欠阻尼弹簧
        private float energyDisplay;
        private float energyVel;
        private float yieldDisplay;
        private float yieldVel;
        //交互动效
        private float latchHover;
        private float rescanHover;
        private int rescanPressTimer;

        //布局矩形,每帧在 Update 里重算
        private Rectangle panelRect;
        private Rectangle titleRect;
        private Rectangle closeRect;
        private Rectangle strataRect;
        private Rectangle reportRect;
        private Rectangle rescanRect;
        private Vector2 energyGaugeCenter;
        private Vector2 yieldGaugeCenter;
        private Rectangle energyGaugeRect;
        private Rectangle yieldGaugeRect;

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
        internal static LocalizedText StatusLabel;
        internal static LocalizedText StateWorking;
        internal static LocalizedText StateNoPower;
        internal static LocalizedText StateNoFooting;
        internal static LocalizedText PickPowerLine;
        internal static LocalizedText YieldRateLine;
        internal static LocalizedText EnergyLabel;
        internal static LocalizedText YieldLabel;
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
        internal static LocalizedText DrillTargetText;
        internal static LocalizedText DrillEffectText;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "Prospecting Terminal");
            ReportTitle = this.GetLocalization(nameof(ReportTitle), () => "Survey Report");
            RescanText = this.GetLocalization(nameof(RescanText), () => "Rescan");
            ModuleSlotLabel = this.GetLocalization(nameof(ModuleSlotLabel), () => "Modules");
            StatusLabel = this.GetLocalization(nameof(StatusLabel), () => "Status");
            StateWorking = this.GetLocalization(nameof(StateWorking), () => "Operating");
            StateNoPower = this.GetLocalization(nameof(StateNoPower), () => "No Power");
            StateNoFooting = this.GetLocalization(nameof(StateNoFooting), () => "Unstable Footing");
            PickPowerLine = this.GetLocalization(nameof(PickPowerLine), () => "Pick Power: {0}%");
            YieldRateLine = this.GetLocalization(nameof(YieldRateLine), () => "Yield: ~{0}/min");
            EnergyLabel = this.GetLocalization(nameof(EnergyLabel), () => "Energy");
            YieldLabel = this.GetLocalization(nameof(YieldLabel), () => "Yield");
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
                //指针从零起摆,开机有仪器上电的感觉
                energyDisplay = 0f;
                energyVel = 0f;
                yieldDisplay = 0f;
                yieldVel = 0f;
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

            //两把锁都要,且都必须每帧常驻(UIHandle.Update 跑在绘制阶段,
            //滚轮增量帧首已被 Player.Update 吃掉,等检测到 delta 再锁就晚一帧):
            //SuppressWeaponSwitch 是 tick 倒计时,拦 CanSwitchWeapon,管滚轮换武器;
            //LockVanillaMouseScroll 是单帧标志,管背包开启时的配方栏滚动
            if (hoverInMainPage) {
                UIInputGuard.SuppressWeaponSwitch();
                PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/MiningMachine");
            }

            if (scanProgress < 1f) {
                scanProgress = MathF.Min(1f, scanProgress + 0.025f);
            }
            socketStrip.Update();
            if (rescanPressTimer > 0) {
                rescanPressTimer--;
            }

            if (uiAlpha < 0.01f || machine == null) {
                return;
            }

            UpdateNeedles();
            latchHover = MathHelper.Lerp(latchHover, closeRect.Contains(MousePosition.ToPoint()) ? 1f : 0f, 0.2f);
            rescanHover = MathHelper.Lerp(rescanHover, rescanRect.Contains(MousePosition.ToPoint()) ? 1f : 0f, 0.2f);

            //报告周期刷新,拿到的是与掷骰同源的数据
            if (++reportRefreshTimer >= 15) {
                reportRefreshTimer = 0;
                RebuildReport();
            }

            UpdateScroll();
            HandleClicks();
        }

        /// <summary>仪表指针的欠阻尼弹簧:上电摆动、变化时过冲回稳</summary>
        private void UpdateNeedles() {
            float energyTarget = machine.MachineData != null
                ? MathHelper.Clamp(machine.MachineData.UEvalue / machine.MaxUEValue, 0f, 1f) : 0f;
            energyVel = energyVel * 0.80f + (energyTarget - energyDisplay) * 0.05f;
            energyDisplay += energyVel;

            //产率按 60/min 满档归一,超出顶格
            float yieldTarget = MathHelper.Clamp(machine.EstimateYieldPerMinute() / 60f, 0f, 1f);
            yieldVel = yieldVel * 0.80f + (yieldTarget - yieldDisplay) * 0.05f;
            yieldDisplay += yieldVel;
        }

        private void ComputeLayout() {
            Vector2 topLeft = DrawPosition;
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);
            titleRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 44);
            closeRect = new Rectangle(panelRect.Right - 40, panelRect.Y + 9, 26, 26);

            strataRect = new Rectangle(panelRect.X + 18, panelRect.Y + 64, 64, 224);
            reportRect = new Rectangle(panelRect.X + 98, panelRect.Y + 60, panelRect.Width - 98 - 18, 24 + RowHeight * 8);
            rescanRect = new Rectangle(reportRect.Right - 98, reportRect.Y - 2, 98, 22);

            //槽行独占底行;右侧留给双仪表
            socketStrip.Layout(panelRect.X + 98, panelRect.Y + 354,
                machine?.ModuleSlotCount ?? 0, SlotSize, 9);

            energyGaugeCenter = new Vector2(panelRect.X + 438, panelRect.Y + 330);
            yieldGaugeCenter = new Vector2(panelRect.X + 502, panelRect.Y + 330);
            energyGaugeRect = new Rectangle((int)energyGaugeCenter.X - 26, (int)energyGaugeCenter.Y - 26, 52, 52);
            yieldGaugeRect = new Rectangle((int)yieldGaugeCenter.X - 26, (int)yieldGaugeCenter.Y - 26, 52, 52);
        }

        private void UpdateScroll() {
            int rowCount = report.Count;
            int visibleRows = (reportRect.Height - 24) / RowHeight;
            float maxScroll = Math.Max(0, rowCount - visibleRows) * RowHeight;

            //滚轮锁在 Update 里每帧常驻,这里只消费增量
            if (reportRect.Contains(MousePosition.ToPoint())) {
                int delta = PlayerInput.ScrollWheelDeltaForUI;
                if (delta != 0) {
                    scrollTarget -= delta * 0.4f;
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
                rescanPressTimer = 8;
                machine.RescanNow();
                StartScanSweep();
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f });
                return;
            }

            //模块插座行:变更后本地改动 SendData 推送;
            //勘探阵列这类改扫描尺寸的模块要立刻重扫才能反映在报告里
            socketStrip.HandleClick(mouse, machine.ModuleRack, machine.ModuleSlotCount, player, () => {
                machine.MarkModulesDirty();
                machine.SendData();
                machine.RescanNow();
                RebuildReport();
            });
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiAlpha < 0.01f || machine == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawCoreSample(spriteBatch);
            DrawReadouts(spriteBatch);
            DrawReport(spriteBatch);
            DrawSlots(spriteBatch);
            DrawStatus(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        /// <summary>钢壳 + 铆钉 + 黄铜铭牌 + 闩钮</summary>
        private void DrawShell(SpriteBatch sb) {
            float alpha = uiAlpha;

            //机壳:一次 shader quad,拉丝钢/锈斑/磨亮棱线全在里面
            IndustrialTerminalRenderer.ShaderPanel(sb, panelRect, alpha);

            //四角铆钉,与模块钢牌同语汇
            int inset = IndustrialTerminalRenderer.Chamfer + 2;
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Bottom - inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Bottom - inset), alpha);

            //标题:黄铜铭牌 + 亮暖填漆字
            string title = TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.86f;
            Rectangle plate = new(panelRect.X + 22, panelRect.Y + 9, (int)titleSize.X + 30, 27);
            IndustrialTerminalRenderer.DrawNameplate(sb, plate, alpha);
            IndustrialTerminalRenderer.DrawPlateTitle(sb, plate, title, alpha, 0.86f);

            //铭牌底缘巡行亮笔:通着电的仪器,不是贴纸
            SvgPath runnerLine = SvgPathPen.Path(RunnerLinePath);
            SvgPathPen.StrokeRunner(sb, runnerLine,
                new Vector2(plate.Center.X, plate.Bottom + 1), plate.Width * 0.5f - 3f, 0f,
                BrassBright, 1f, alpha * 0.5f, GlobalTimer * 0.05f, 0.2f);

            //机器名挂在铭牌右侧
            string name = Lang.GetItemNameValue(machine.TargetItem);
            Utils.DrawBorderString(sb, name, new Vector2(plate.Right + 12, plate.Y + 6), TextDim * alpha, 0.68f);

            //标题栏下的蚀刻分隔
            IndustrialTerminalRenderer.DrawEtchedLine(sb, panelRect.X + 14, panelRect.Width - 28, titleRect.Bottom - 3, alpha, 0.8f);

            //闩钮:拧开面板
            IndustrialTerminalRenderer.DrawLatch(sb, closeRect.Center.ToVector2(), alpha, latchHover);
        }

        /// <summary>岩芯样本管:黄铜端盖玻璃管,管内是这台机器脚下的世界纵剖</summary>
        private void DrawCoreSample(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float alpha = uiAlpha;
            Rectangle src = new(0, 0, 1, 1);
            MiningSurvey survey = machine.Survey;

            //把世界纵深映射进样本管
            float worldToCol(float worldY) =>
                strataRect.Y + MathHelper.Clamp(worldY / Main.maxTilesY, 0f, 1f) * strataRect.Height;

            float surfaceY = worldToCol((float)Main.worldSurface);
            float rockY = worldToCol((float)Main.rockLayer);
            float hellY = worldToCol(Main.maxTilesY - 204);

            //管内芯体:四段地层
            Rectangle inner = new(strataRect.X + 1, strataRect.Y, strataRect.Width - 2, strataRect.Height);
            void band(float top, float bottom, Color color) {
                int y0 = (int)top;
                int h = Math.Max(1, (int)bottom - y0);
                sb.Draw(px, new Rectangle(inner.X, y0, inner.Width, h), src, color * (alpha * 0.92f));
            }
            band(strataRect.Y, surfaceY, StrataSky);
            band(surfaceY, rockY, StrataSoil);
            band(rockY, hellY, StrataRock);
            band(hellY, strataRect.Bottom, StrataHell);

            //岩屑噪斑:确定性撒点,让芯体读作压出来的岩样而不是色带
            for (int k = 0; k < 34; k++) {
                int hash = k * 40503 ^ machine.WhoAmI * 92821;
                float fx = (hash & 0x3FF) / 1023f;
                float fy = ((hash >> 10) & 0x3FF) / 1023f;
                float dy = strataRect.Y + fy * strataRect.Height;
                Color bandColor = dy < surfaceY ? StrataSky : dy < rockY ? StrataSoil : dy < hellY ? StrataRock : StrataHell;
                Color fleck = Color.Lerp(bandColor, Color.White, 0.16f + (hash >> 20 & 0x3) * 0.05f);
                sb.Draw(px, new Vector2(inner.X + 2 + fx * (inner.Width - 4), dy), src,
                    fleck * (alpha * 0.5f), 0f, new Vector2(0.5f), new Vector2(1.6f, 1f), SpriteEffects.None, 0f);
            }

            //层界细刻
            Span<float> boundaries = [surfaceY, rockY, hellY];
            foreach (float y in boundaries) {
                sb.Draw(px, new Rectangle(inner.X, (int)y, inner.Width, 1), src,
                    Color.Black * (alpha * 0.4f));
            }

            //扫描窗:机器实际"看见"的柱段
            if (survey != null) {
                float sliceTop = worldToCol(survey.Anchor.Y);
                float sliceBottom = worldToCol(survey.Anchor.Y + survey.Depth);
                int st = (int)sliceTop;
                int sh = Math.Max(3, (int)sliceBottom - st);
                Rectangle slice = new(inner.X + 2, st, inner.Width - 4, sh);
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
                        Vector2 dot = new(slice.X + 3 + fx * (slice.Width - 6), slice.Y + 3 + fy * (slice.Height - 6));
                        float tw = MathF.Sin(GlobalTimer * 2.2f + hash % 7) * 0.5f + 0.5f;
                        sb.Draw(px, dot, src, Color.Lerp(Amber, Color.White, 0.4f) * (alpha * (0.35f + tw * 0.45f)),
                            MathHelper.PiOver4, new Vector2(0.5f), new Vector2(2.4f), SpriteEffects.None, 0f);
                    }
                }

                //机器位置标记:一枚黄铜楔钉在管壁上
                float markY = worldToCol(survey.Anchor.Y);
                Vector2 mark = new(strataRect.X - 5, markY);
                sb.Draw(px, mark + new Vector2(0.7f), src, Color.Black * (alpha * 0.4f), MathHelper.PiOver4,
                    new Vector2(0.5f), new Vector2(6f), SpriteEffects.None, 0f);
                sb.Draw(px, mark, src, BrassBright * alpha, MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(6f), SpriteEffects.None, 0f);
            }

            //管壳画在芯体之上:端盖、管壁与玻璃高光
            IndustrialTerminalRenderer.DrawCoreTube(sb, strataRect, alpha, GlobalTimer);
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
            float y = strataRect.Bottom + 14;
            float x = strataRect.X - 3;

            void line(string label, string value, Color valueColor) {
                Utils.DrawBorderString(sb, label, new Vector2(x, y), TextDim * alpha, 0.6f);
                Utils.DrawBorderString(sb, value, new Vector2(x, y + 13), valueColor * alpha, 0.66f);
                y += 29;
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

            //报告头 + 机加工按钮
            Utils.DrawBorderString(sb, ReportTitle.Value, new Vector2(reportRect.X, reportRect.Y - 2), TextMain * alpha, 0.78f);
            IndustrialTerminalRenderer.DrawButton(sb, rescanRect, alpha, rescanHover, rescanPressTimer > 0, RescanText.Value);

            //行区:负空间 + 蚀刻分隔,不再画整行底盒
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

            //扫掠亮线 + 余辉:扫过的地方留几帧渐暗的残光
            if (scanProgress < 1f) {
                float sweepY = rowsRect.Y + MathHelper.Clamp(sweepReveal * RowHeight - scrollOffset, 0, rowsRect.Height);
                sb.Draw(px, new Rectangle(rowsRect.X, (int)sweepY, rowsRect.Width, 2), src, Amber * (alpha * 0.85f));
                for (int e = 1; e <= 3; e++) {
                    float echoY = sweepY - e * 5f;
                    if (echoY < rowsRect.Y) {
                        break;
                    }
                    sb.Draw(px, new Rectangle(rowsRect.X, (int)echoY, rowsRect.Width, 1), src,
                        Amber * (alpha * (0.32f - e * 0.09f)));
                }
            }

            //溢出指示:右缘蚀刻轨 + 琥珀游标
            int rowCount = report.Count;
            if (rowCount > visibleRows) {
                float viewRatio = visibleRows / (float)rowCount;
                float posRatio = scrollOffset / (rowCount * RowHeight);
                int trackH = rowsRect.Height - 4;
                int barH = Math.Max(12, (int)(trackH * viewRatio));
                int barY = rowsRect.Y + 2 + (int)(posRatio * trackH);
                sb.Draw(px, new Rectangle(rowsRect.Right + 3, rowsRect.Y + 2, 1, trackH), src, Color.Black * (alpha * 0.45f));
                sb.Draw(px, new Rectangle(rowsRect.Right + 4, rowsRect.Y + 2, 1, trackH), src, IndustrialTerminalRenderer.SteelLit * (alpha * 0.4f));
                sb.Draw(px, new Rectangle(rowsRect.Right + 2, barY, 3, barH), src, Amber * (alpha * 0.6f));
            }
        }

        private void DrawReportRow(SpriteBatch sb, OreReportEntry entry, Rectangle row, float alpha, int index) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            bool open = entry.Gate == OreGate.Open;
            bool hover = row.Contains(MousePosition.ToPoint());

            //悬停:左缘琥珀刻痕,不再整行填充
            if (hover) {
                sb.Draw(px, new Rectangle(row.X, row.Y + 3, 2, row.Height - 6), src, Amber * (alpha * 0.85f));
            }
            //行间蚀刻分隔
            IndustrialTerminalRenderer.DrawEtchedLine(sb, row.X + 2, row.Width - 8, row.Bottom - 1, alpha, 0.45f);

            //矿物图标;玩家没见过的矿贴图是懒加载的,不先 LoadItem 只会画出空气
            Main.instance.LoadItem(entry.ItemID);
            VaultUtils.SimpleDrawItem(sb, entry.ItemID, new Vector2(row.X + 14, row.Center.Y), 20, 1f, 0,
                Color.White * (open ? alpha : alpha * 0.45f));

            //名称
            string name = Lang.GetItemNameValue(entry.ItemID);
            Color nameColor = open ? (hover ? Color.Lerp(TextMain, Color.White, 0.25f) : TextMain) : TextDim;
            Utils.DrawBorderString(sb, name, new Vector2(row.X + 30, row.Y + 4), nameColor * alpha, 0.68f);

            if (open) {
                //份额刻度条 + 百分比:与掷骰同源
                int barX = row.X + 176;
                int barW = row.Width - 176 - 58;
                IndustrialTerminalRenderer.DrawTickBar(sb, new Rectangle(barX, row.Y + 5, barW, row.Height - 10),
                    entry.Share, Amber, alpha);
                string share = (entry.Share * 100f).ToString(entry.Share >= 0.095f ? "0" : "0.0") + "%";
                Vector2 shareSize = FontAssets.MouseText.Value.MeasureString(share) * 0.66f;
                Utils.DrawBorderString(sb, share, new Vector2(row.Right - 8 - shareSize.X, row.Y + 4),
                    TextMain * alpha, 0.66f);

                //有真实矿脉的行,在名称后钉一枚黄铜亮钉
                if (entry.VeinTiles > 0) {
                    Vector2 pin = new(row.X + 166, row.Center.Y);
                    sb.Draw(px, pin, src, Color.Lerp(BrassBright, Color.White, 0.3f) * (alpha * 0.9f),
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
            float alpha = uiAlpha;

            Utils.DrawBorderString(sb, ModuleSlotLabel.Value,
                new Vector2(socketStrip.Rects.Count > 0 ? socketStrip.Rects[0].X : panelRect.X + 98, panelRect.Y + 338),
                TextDim * alpha, 0.62f);

            socketStrip.Draw(sb, machine.ModuleRack, machine.ModuleSlotCount, alpha, MousePosition.ToPoint());
        }

        /// <summary>状态灯 + 镐力读数 + 双仪表(能量/产率)</summary>
        private void DrawStatus(SpriteBatch sb) {
            float alpha = uiAlpha;
            float x = panelRect.X + 98;
            float y = panelRect.Y + 302;

            //状态灯:唯一的裸辉光点缀
            string state;
            Color lampColor;
            float lampBright;
            if (!machine.Powered) {
                state = StateNoPower.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(GlobalTimer * 5f) * 0.35f + 0.55f;
            }
            else if (!machine.FootingOk) {
                state = StateNoFooting.Value;
                lampColor = Color.Lerp(WarnRed, Amber, 0.5f);
                lampBright = MathF.Sin(GlobalTimer * 3f) * 0.3f + 0.6f;
            }
            else {
                state = StateWorking.Value;
                lampColor = OkGreen;
                lampBright = MathF.Sin(GlobalTimer * 2.2f) * 0.2f + 0.72f;
            }
            IndustrialTerminalRenderer.DrawLamp(sb, new Vector2(x + 7, y + 9), lampColor, alpha, lampBright);
            Utils.DrawBorderString(sb, state, new Vector2(x + 21, y + 1),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.66f);

            //镐力读数:挂在槽位标签行,避开右侧表盘
            machine.RefreshModifiers();
            Utils.DrawBorderString(sb, PickPowerLine.Format((int)machine.EffectivePickPower),
                new Vector2(panelRect.X + 240, panelRect.Y + 338), TextMain * alpha, 0.62f);

            //双仪表:指针带欠阻尼摆动,作业时加一丝微颤
            float jitter = machine.IsWorking ? MathF.Sin(GlobalTimer * 34f) * 0.006f : 0f;
            float ratio = machine.MachineData != null
                ? MathHelper.Clamp(machine.MachineData.UEvalue / machine.MaxUEValue, 0f, 1f) : 0f;
            IndustrialTerminalRenderer.DrawGauge(sb, energyGaugeCenter, 26f, energyDisplay + jitter,
                Amber, alpha, EnergyLabel.Value, $"{(int)(ratio * 100f)}%");
            IndustrialTerminalRenderer.DrawGauge(sb, yieldGaugeCenter, 26f, yieldDisplay + jitter,
                Color.Lerp(Amber, OkGreen, 0.35f), alpha, YieldLabel.Value,
                machine.EstimateYieldPerMinute().ToString("0.0"));
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (IsDragging) {
                return;
            }
            Point mouse = MousePosition.ToPoint();

            //模块槽悬停
            if (socketStrip.DrawHoverTip(sb, machine.ModuleRack, machine.ModuleSlotCount, mouse,
                (text, color) => ShowTooltip(sb, text, color))) {
                return;
            }

            //仪表悬停:精确读数
            if (energyGaugeRect.Contains(mouse)) {
                ShowTooltip(sb, $"{(int)machine.MachineData.UEvalue}/{(int)machine.MaxUEValue} UE", TextMain);
                return;
            }
            if (yieldGaugeRect.Contains(mouse)) {
                ShowTooltip(sb, YieldRateLine.Format(machine.EstimateYieldPerMinute().ToString("0.0")), TextMain);
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
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
            Vector2 pos = MousePosition + new Vector2(18, 18);
            //贴屏缘时翻转与钳制
            if (pos.X + textSize.X + 20 > UIScreenW) {
                pos.X = MousePosition.X - textSize.X - 24;
            }
            if (pos.Y + textSize.Y + 12 > UIScreenH) {
                pos.Y = MousePosition.Y - textSize.Y - 18;
            }

            Rectangle bg = new((int)pos.X - 9, (int)pos.Y - 5, (int)textSize.X + 18, (int)textSize.Y + 10);
            IndustrialTerminalRenderer.DrawTooltipPlate(sb, bg, 1f);
            Utils.DrawBorderString(sb, text, pos, color, 0.75f);
        }
        #endregion
    }
}
