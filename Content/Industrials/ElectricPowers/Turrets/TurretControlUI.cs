using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MachineModules;
using CalamityOverhaul.Content.Industrials.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets
{
    /// <summary>
    /// 防御塔族控制面板:钢壳 + 铭牌 + 电量表 + 电源拨杆 + 模块插座行 + 射程/节拍/耗电读数。<br/>
    /// 五座塔(火焰/冰冻/激光/护盾/治疗)共用这一块面板,读数由 <see cref="BaseTurretTP"/> 的
    /// Panel* 虚属性供给;拨杆走 <see cref="BaseTurretTP.RightEvent"/>(与电线同一条路径),
    /// 模块编辑走"本地改 + SendData 推送"的既有客户端权威模型
    /// </summary>
    internal class TurretControlUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static TurretControlUI Instance => UIHandleLoader.GetUIHandleOfType<TurretControlUI>();

        #region 布局与调色
        private const float PanelWidth = 352f;
        private const float PanelHeight = 272f;
        private const int SlotSize = 40;
        /// <summary>走出这个距离面板自动收摊;距离量到塔的包围盒最近点,不是左上角</summary>
        private const float MaxUseDistance = 420f;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color Amber => IndustrialTerminalRenderer.Amber;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color OkGreen => IndustrialTerminalRenderer.OkGreen;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        #endregion

        #region 状态
        private BaseTurretTP turret;
        private bool pendingCenter;

        //模块插座行(点击/校验/红闪/绘制全在共享件里)
        private readonly ModuleSocketStrip socketStrip = new();

        //电量指针的欠阻尼弹簧
        private float energyDisplay;
        private float energyVel;
        //交互动效
        private float latchHover;
        private float switchHover;
        private int switchPressTimer;

        private Rectangle panelRect;
        private Rectangle titleRect;
        private Rectangle closeRect;
        private Rectangle switchRect;
        private Vector2 energyGaugeCenter;
        private Rectangle energyGaugeRect;

        private float uiAlpha => OpenProgress.Current;
        #endregion

        #region 基类接入
        public override bool AutoUpdateHitBox => true;
        public override bool BlockMouseWhenHovered => true;
        public override bool CanDrag => true;
        public override MouseButtonType DragMouseButton => MouseButtonType.Left;
        //把手挖掉右上闩钮,不然关闭钮只会拖面板
        public override Rectangle? DragHandleRect
            => new Rectangle(titleRect.X, titleRect.Y, Math.Max(titleRect.Width - 48, 0), titleRect.Height);
        #endregion

        #region 本地化
        internal static LocalizedText TitleText;
        internal static LocalizedText ChargeLabel;
        internal static LocalizedText PowerOnText;
        internal static LocalizedText PowerOffText;
        internal static LocalizedText StateWorking;
        internal static LocalizedText StateStandby;
        internal static LocalizedText StateNoPower;
        internal static LocalizedText RangeLine;
        internal static LocalizedText RhythmLine;
        internal static LocalizedText RhythmSustained;
        internal static LocalizedText EnergyPerShotLine;
        internal static LocalizedText EnergyPerTickLine;
        internal static LocalizedText ChargeReadLine;
        internal static LocalizedText SwitchHint;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "Fire Control");
            ChargeLabel = this.GetLocalization(nameof(ChargeLabel), () => "Charge");
            PowerOnText = this.GetLocalization(nameof(PowerOnText), () => "POWERED");
            PowerOffText = this.GetLocalization(nameof(PowerOffText), () => "SHUT OFF");
            StateWorking = this.GetLocalization(nameof(StateWorking), () => "Operating");
            StateStandby = this.GetLocalization(nameof(StateStandby), () => "Switched Off");
            StateNoPower = this.GetLocalization(nameof(StateNoPower), () => "No Power");
            RangeLine = this.GetLocalization(nameof(RangeLine), () => "Range: {0} tiles");
            RhythmLine = this.GetLocalization(nameof(RhythmLine), () => "Cadence: {0}/s");
            RhythmSustained = this.GetLocalization(nameof(RhythmSustained), () => "Cadence: sustained");
            EnergyPerShotLine = this.GetLocalization(nameof(EnergyPerShotLine), () => "Draw: {0} UE per shot");
            EnergyPerTickLine = this.GetLocalization(nameof(EnergyPerTickLine), () => "Draw: {0} UE per tick");
            ChargeReadLine = this.GetLocalization(nameof(ChargeReadLine), () => "{0}/{1} UE");
            SwitchHint = this.GetLocalization(nameof(SwitchHint), () => "Toggle the turret");
        }
        #endregion

        /// <summary>右键塔身时进入:同一座塔切换开合,换一座塔切换绑定</summary>
        public void Initialize(BaseTurretTP target) {
            if (turret != target) {
                turret = target;
                if (!IsOpen) {
                    Open();
                }
                pendingCenter = true;
            }
            else {
                Toggle();
            }

            if (IsOpen) {
                //指针从零起摆,开板有仪器上电的感觉
                energyDisplay = 0f;
                energyVel = 0f;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.4f, Pitch = -0.4f });
            }
        }

        public override void OnEnterWorld() {
            turret = null;
            Close();
        }

        public override void Update() {
            Size = new Vector2(PanelWidth, PanelHeight);

            if (pendingCenter) {
                pendingCenter = false;
                if (DrawPosition == Vector2.Zero) {
                    DrawPosition = new Vector2((UIScreenW - PanelWidth) * 0.5f, UIScreenH * 0.3f);
                }
            }

            //绑定失效或走远时收摊。塔是 3x5 的立柱,玩家站在塔脚,距离一律量包围盒最近点
            if (IsOpen && (turret == null || !turret.Active || !NearEnough())) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f, Volume = 0.6f });
                Close();
                return;
            }

            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 8f, UIScreenW - PanelWidth - 8f);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 8f, UIScreenH - PanelHeight - 8f);

            ComputeLayout();

            if (hoverInMainPage) {
                UIInputGuard.SuppressWeaponSwitch();
                PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/TurretControl");
            }

            socketStrip.Update();
            if (switchPressTimer > 0) {
                switchPressTimer--;
            }

            if (uiAlpha < 0.01f || turret == null) {
                return;
            }

            UpdateNeedle();
            latchHover = MathHelper.Lerp(latchHover, closeRect.Contains(MousePoint) ? 1f : 0f, 0.2f);
            switchHover = MathHelper.Lerp(switchHover, switchRect.Contains(MousePoint) ? 1f : 0f, 0.2f);

            HandleClicks();
        }

        /// <summary>玩家中心夹进塔的包围盒再量距离</summary>
        private bool NearEnough() {
            Rectangle hit = turret.HitBox;
            Vector2 center = player.Center;
            Vector2 nearest = new(
                MathHelper.Clamp(center.X, hit.Left, hit.Right),
                MathHelper.Clamp(center.Y, hit.Top, hit.Bottom));
            return nearest.Distance(center) <= MaxUseDistance;
        }

        private void UpdateNeedle() {
            float target = turret.MachineData != null
                ? MathHelper.Clamp(turret.MachineData.UEvalue / turret.MaxUEValue, 0f, 1f) : 0f;
            energyVel = energyVel * 0.80f + (target - energyDisplay) * 0.05f;
            energyDisplay += energyVel;
        }

        private void ComputeLayout() {
            panelRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)PanelWidth, (int)PanelHeight);
            titleRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 42);
            closeRect = new Rectangle(panelRect.Right - 40, panelRect.Y + 8, 26, 26);

            energyGaugeCenter = new Vector2(panelRect.X + 66, panelRect.Y + 96);
            energyGaugeRect = new Rectangle((int)energyGaugeCenter.X - 30, (int)energyGaugeCenter.Y - 30, 60, 60);

            switchRect = new Rectangle(panelRect.X + 128, panelRect.Y + 56, 116, 34);

            socketStrip.Layout(panelRect.X + 24, panelRect.Y + 180,
                turret?.ModuleSlotCount ?? 0, SlotSize, 10);
        }

        private void HandleClicks() {
            if (IsDragging || keyLeftPressState != KeyPressState.Pressed) {
                return;
            }
            Point mouse = MousePoint;

            if (closeRect.Contains(mouse)) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f, Volume = 0.6f });
                Close();
                return;
            }

            //电源拨杆:与电线信号同一条翻转路径,音效与飘字由塔自己的表现钩子给
            if (switchRect.Contains(mouse)) {
                switchPressTimer = 8;
                turret.RightEvent();
                return;
            }

            //模块插座行:本地改动后推送,塔下一帧自会按新乘数跑
            socketStrip.HandleClick(mouse, turret.ModuleRack, turret.ModuleSlotCount, player,
                () => turret.SendData());
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiAlpha < 0.01f || turret == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawGaugeAndSwitch(spriteBatch);
            DrawReadouts(spriteBatch);
            DrawSlots(spriteBatch);
            DrawStatus(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        /// <summary>钢壳 + 铆钉 + 黄铜铭牌 + 闩钮</summary>
        private void DrawShell(SpriteBatch sb) {
            float alpha = uiAlpha;

            IndustrialTerminalRenderer.ShaderPanel(sb, panelRect, alpha);

            int inset = IndustrialTerminalRenderer.Chamfer + 2;
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Bottom - inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Bottom - inset), alpha);

            string title = TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.8f;
            Rectangle plate = new(panelRect.X + 20, panelRect.Y + 8, (int)titleSize.X + 28, 26);
            IndustrialTerminalRenderer.DrawNameplate(sb, plate, alpha);
            IndustrialTerminalRenderer.DrawPlateTitle(sb, plate, title, alpha, 0.8f);

            //塔的本名挂在铭牌右侧
            string name = Lang.GetItemNameValue(turret.TargetItem);
            Utils.DrawBorderString(sb, name, new Vector2(plate.Right + 12, plate.Y + 6), TextDim * alpha, 0.66f);

            IndustrialTerminalRenderer.DrawEtchedLine(sb, panelRect.X + 14, panelRect.Width - 28, titleRect.Bottom - 3, alpha, 0.8f);
            IndustrialTerminalRenderer.DrawLatch(sb, closeRect.Center.ToVector2(), alpha, latchHover);
        }

        /// <summary>电量表 + 电源拨杆</summary>
        private void DrawGaugeAndSwitch(SpriteBatch sb) {
            float alpha = uiAlpha;
            float ratio = turret.MachineData != null
                ? MathHelper.Clamp(turret.MachineData.UEvalue / turret.MaxUEValue, 0f, 1f) : 0f;

            //电量见底那一段标红:塔要停摆的预告
            IndustrialTerminalRenderer.DrawGauge(sb, energyGaugeCenter, 30f, energyDisplay,
                Amber, alpha, ChargeLabel.Value, $"{(int)(ratio * 100f)}%", 0f == ratio ? -1f : 0.12f);

            bool on = turret.AttackPattern;
            IndustrialTerminalRenderer.DrawButton(sb, switchRect, alpha, switchHover,
                switchPressTimer > 0, on ? PowerOnText.Value : PowerOffText.Value);

            //拨杆左侧一枚随开关变色的小灯,不看字也知道当前挡位
            IndustrialTerminalRenderer.DrawLamp(sb, new Vector2(switchRect.X - 12, switchRect.Center.Y),
                on ? OkGreen : TextDim, alpha, on ? 0.85f : 0.25f);
        }

        /// <summary>射程/节拍/耗电三行读数:模块装上去后这里立刻变</summary>
        private void DrawReadouts(SpriteBatch sb) {
            float alpha = uiAlpha;
            int x = panelRect.X + 128;
            int y = panelRect.Y + 100;

            string range = RangeLine.Format((turret.PanelRange / 16f).ToString("0"));
            Utils.DrawBorderString(sb, range, new Vector2(x, y), TextMain * alpha, 0.62f);

            int rhythm = turret.PanelRhythm;
            string cadence = rhythm > 0
                ? RhythmLine.Format((60f / rhythm).ToString("0.0"))
                : RhythmSustained.Value;
            Utils.DrawBorderString(sb, cadence, new Vector2(x, y + 20), TextMain * alpha, 0.62f);

            string draw = turret.PanelEnergyPerTick
                ? EnergyPerTickLine.Format(turret.PanelEnergy.ToString("0.##"))
                : EnergyPerShotLine.Format(turret.PanelEnergy.ToString("0.##"));
            Utils.DrawBorderString(sb, draw, new Vector2(x, y + 40), TextMain * alpha, 0.62f);
        }

        /// <summary>模块槽标签 + 插座行</summary>
        private void DrawSlots(SpriteBatch sb) {
            if (turret.ModuleSlotCount <= 0) {
                return;
            }
            float alpha = uiAlpha;
            Utils.DrawBorderString(sb, MachineModuleText.SlotLabel.Value,
                new Vector2(panelRect.X + 24, panelRect.Y + 158), TextDim * alpha, 0.62f);
            socketStrip.Draw(sb, turret.ModuleRack, turret.ModuleSlotCount, alpha, MousePoint);
        }

        /// <summary>补充读数行 + 底部状态灯与精确电量</summary>
        private void DrawStatus(SpriteBatch sb) {
            float alpha = uiAlpha;

            string note = turret.PanelNote;
            if (!string.IsNullOrEmpty(note)) {
                Utils.DrawBorderString(sb, note, new Vector2(panelRect.X + 24, panelRect.Y + 226), TextDim * alpha, 0.58f);
            }

            IndustrialTerminalRenderer.DrawEtchedLine(sb, panelRect.X + 14, panelRect.Width - 28, panelRect.Y + 244, alpha, 0.7f);

            bool hasPower = (turret.MachineData?.UEvalue ?? 0f) >= turret.PanelEnergy;
            bool working = turret.PanelWorking;
            Color lampColor = working ? OkGreen : hasPower ? TextDim : WarnRed;
            float bright = working
                ? MathF.Sin(GlobalTimer * 0.04f) * 0.2f + 0.7f
                : hasPower ? 0.3f : MathF.Sin(GlobalTimer * 0.09f) * 0.3f + 0.55f;
            float lampY = panelRect.Bottom - 18;
            IndustrialTerminalRenderer.DrawLamp(sb, new Vector2(panelRect.X + 30, lampY), lampColor, alpha, bright);

            string state = working ? StateWorking.Value : hasPower ? StateStandby.Value : StateNoPower.Value;
            Utils.DrawBorderString(sb, state, new Vector2(panelRect.X + 44, lampY - 8),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.62f);

            if (turret.MachineData != null) {
                string charge = ChargeReadLine.Format((int)turret.MachineData.UEvalue, (int)turret.MaxUEValue);
                Vector2 size = FontAssets.MouseText.Value.MeasureString(charge) * 0.6f;
                Utils.DrawBorderString(sb, charge, new Vector2(panelRect.Right - 20 - size.X, lampY - 7),
                    TextMain * alpha, 0.6f);
            }
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (IsDragging) {
                return;
            }
            Point mouse = MousePoint;
            if (turret.ModuleSlotCount > 0 && socketStrip.DrawHoverTip(sb, turret.ModuleRack,
                turret.ModuleSlotCount, mouse, (text, color) => ShowTip(sb, text, color))) {
                return;
            }
            if (switchRect.Contains(mouse)) {
                ShowTip(sb, SwitchHint.Value, TextMain);
            }
        }

        private static void ShowTip(SpriteBatch sb, string text, Color color) {
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
            Vector2 pos = new Vector2(Main.mouseX, Main.mouseY) + new Vector2(18, 18);
            if (pos.X + textSize.X + 20 > UIScreenW) {
                pos.X = Main.mouseX - textSize.X - 24;
            }
            if (pos.Y + textSize.Y + 12 > UIScreenH) {
                pos.Y = Main.mouseY - textSize.Y - 18;
            }

            Rectangle bg = new((int)pos.X - 9, (int)pos.Y - 5, (int)textSize.X + 18, (int)textSize.Y + 10);
            IndustrialTerminalRenderer.DrawTooltipPlate(sb, bg, 1f);
            Utils.DrawBorderString(sb, text, pos, color, 0.75f);
        }
        #endregion

        public override void SaveUIData(TagCompound tag) {
            tag["TurretControlUI_DrawPos_X"] = DrawPosition.X;
            tag["TurretControlUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TrySafeGet("TurretControlUI_DrawPos_X", out float x) && float.IsFinite(x)) {
                DrawPosition.X = x;
            }
            if (tag.TrySafeGet("TurretControlUI_DrawPos_Y", out float y) && float.IsFinite(y)) {
                DrawPosition.Y = y;
            }
        }
    }
}
