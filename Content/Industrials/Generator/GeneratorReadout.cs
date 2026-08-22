using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.Generator
{
    /// <summary>读数板的工况种类,决定标签与告警文案</summary>
    public enum GeneratorReadoutKind : byte
    {
        Wind,
        Water,
    }

    /// <summary>
    /// 无燃料发电机(风力/水力)向读数板暴露的实时工况。
    /// 由发电机 TP 实现,读数板纯只读消费
    /// </summary>
    public interface IGeneratorReadout
    {
        GeneratorReadoutKind ReadoutKind { get; }
        /// <summary>工况比 0..1(风速档位/水轮转速比)</summary>
        float ConditionRatio { get; }
        /// <summary>工况是否正常(无风微风/水轮离水为 false)</summary>
        bool ConditionOk { get; }
        /// <summary>当前输出功率(UE/s)</summary>
        float OutputPerSecond { get; }
    }

    /// <summary>
    /// 风力/水力发电机共用的仪表板:钢壳 + 铭牌 + 工况/储能双表盘 + 模块插座行 + 状态灯 + 输出读数。
    /// 右键开合、超距自动关、位置持久化,与热力面板同一套仪器语言
    /// </summary>
    internal class GeneratorReadoutUI : BaseGeneratorUI, ILocalizedModType
    {
        public string LocalizationCategory => "UI.Generator";

        #region 布局与状态
        private const float PanelWidth = 312f;
        private const float PanelHeight = 252f;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color Amber => IndustrialTerminalRenderer.Amber;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        //仪表指针的欠阻尼弹簧
        private float condDisplay;
        private float condVel;
        private float storeDisplay;
        private float storeVel;
        private float latchHover;

        //拖拽
        private bool isDragging;
        private Vector2 dragOffset;
        private bool positionInitialized;

        private Rectangle panelRect;
        private Rectangle closeRect;
        private Vector2 condGaugeCenter;
        private Vector2 storeGaugeCenter;
        private Rectangle condGaugeRect;
        private Rectangle storeGaugeRect;
        private bool hoveringSockets;

        //模块插座行(点击/校验/红闪/绘制在共享件里)
        private readonly ModuleSocketStrip socketStrip = new();

        private float animTimer;

        private IGeneratorReadout Readout => GeneratorTP as IGeneratorReadout;
        #endregion

        #region 本地化
        internal static LocalizedText WindLabel;
        internal static LocalizedText FlowLabel;
        internal static LocalizedText StorageLabel;
        internal static LocalizedText OutputLine;
        internal static LocalizedText RunningText;
        internal static LocalizedText NoWindText;
        internal static LocalizedText NoWaterText;
        internal static LocalizedText PowerUnitText;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            WindLabel = this.GetLocalization(nameof(WindLabel), () => "Wind");
            FlowLabel = this.GetLocalization(nameof(FlowLabel), () => "Flow");
            StorageLabel = this.GetLocalization(nameof(StorageLabel), () => "Charge");
            OutputLine = this.GetLocalization(nameof(OutputLine), () => "Output: {0} UE/s");
            RunningText = this.GetLocalization(nameof(RunningText), () => "Running");
            NoWindText = this.GetLocalization(nameof(NoWindText), () => "Weak Wind");
            NoWaterText = this.GetLocalization(nameof(NoWaterText), () => "Wheel Dry");
            PowerUnitText = this.GetLocalization(nameof(PowerUnitText), () => "UE");
        }
        #endregion

        public override void UpdateElement() {
            if (!positionInitialized && Main.screenWidth > 0) {
                positionInitialized = true;
                if (DrawPosition.X < PanelWidth / 2 + 10 && DrawPosition.Y < PanelHeight / 2 + 10) {
                    DrawPosition = new Vector2(UIScreenW * 0.5f, UIScreenH * 0.42f);
                }
            }

            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, PanelWidth / 2 + 10, UIScreenW - PanelWidth / 2 - 10);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, PanelHeight / 2 + 10, UIScreenH - PanelHeight / 2 - 10);

            animTimer += 1f / 60f;

            //绑定失效或走远时收摊。距离按结构包围盒最近点算,不能用左上角
            //MK2 风塔 3×27 格,塔顶左上距塔底玩家超 400px,按左上算刚右键开板就被这里关掉
            if (IsActive) {
                bool shutDown = GeneratorTP == null || !GeneratorTP.Active;
                if (!shutDown) {
                    Rectangle hit = GeneratorTP.HitBox;
                    Vector2 playerCenter = Main.LocalPlayer.Center;
                    Vector2 nearest = new(
                        MathHelper.Clamp(playerCenter.X, hit.Left, hit.Right),
                        MathHelper.Clamp(playerCenter.Y, hit.Top, hit.Bottom));
                    shutDown = nearest.Distance(playerCenter) > GeneratorTP.MaxFindMode;
                }
                if (shutDown) {
                    IsActive = false;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }

            float targetAlpha = IsActive ? 1f : 0f;
            uiFadeAlpha = MathHelper.Lerp(uiFadeAlpha, targetAlpha, 0.15f);
            if (uiFadeAlpha < 0.01f && !IsActive) {
                return;
            }

            ComputeLayout();
            UpdateNeedles();
            socketStrip.Update();

            Point mouse = MousePosition.ToPoint();
            hoverInMainPage = panelRect.Contains(mouse);
            hoveringSockets = socketStrip.Contains(mouse) && !isDragging;
            latchHover = MathHelper.Lerp(latchHover, closeRect.Contains(mouse) ? 1f : 0f, 0.2f);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                //纯读数板也别让滚轮翻快捷栏
                UIInputGuard.SuppressWeaponSwitch();
            }

            if (closeRect.Contains(mouse) && keyLeftPressState == KeyPressState.Pressed) {
                IsActive = false;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f, Volume = 0.5f });
                return;
            }

            //模块插座行点击(先于拖拽捕获)
            if (keyLeftPressState == KeyPressState.Pressed && hoveringSockets && GeneratorTP != null) {
                socketStrip.HandleClick(mouse, GeneratorTP.ModuleRack, GeneratorTP.ModuleSlotCount,
                    player, () => GeneratorTP.SendData());
            }

            //背景区拖拽,避开表盘/插座与闩钮
            if (keyLeftPressState == KeyPressState.Pressed && hoverInMainPage
                && !condGaugeRect.Contains(mouse) && !storeGaugeRect.Contains(mouse) && !hoveringSockets
                && !closeRect.Contains(mouse) && !isDragging) {
                isDragging = true;
                dragOffset = MousePosition - DrawPosition;
            }
            if (isDragging) {
                DrawPosition = MousePosition - dragOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    isDragging = false;
                }
            }
        }

        private void ComputeLayout() {
            Vector2 topLeft = DrawPosition - new Vector2(PanelWidth / 2, PanelHeight / 2);
            panelRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)PanelWidth, (int)PanelHeight);
            closeRect = new Rectangle(panelRect.Right - 38, panelRect.Y + 8, 26, 26);
            condGaugeCenter = new Vector2(panelRect.X + 88, panelRect.Y + 108);
            storeGaugeCenter = new Vector2(panelRect.X + 224, panelRect.Y + 108);
            condGaugeRect = new Rectangle((int)condGaugeCenter.X - 34, (int)condGaugeCenter.Y - 34, 68, 68);
            storeGaugeRect = new Rectangle((int)storeGaugeCenter.X - 34, (int)storeGaugeCenter.Y - 34, 68, 68);
            //插座行:表盘行与状态行之间(荒野结构槽数 0,行自然消失)
            socketStrip.Layout(panelRect.X + 26, panelRect.Y + 152,
                GeneratorTP?.ModuleSlotCount ?? 0, 40, 8);
        }

        private void UpdateNeedles() {
            IGeneratorReadout readout = Readout;
            float condTarget = readout != null ? MathHelper.Clamp(readout.ConditionRatio, 0f, 1f) : 0f;
            condVel = condVel * 0.80f + (condTarget - condDisplay) * 0.05f;
            condDisplay += condVel;

            float storeTarget = GeneratorTP?.MachineData != null
                ? MathHelper.Clamp(GeneratorTP.MachineData.UEvalue / GeneratorTP.MaxUEValue, 0f, 1f) : 0f;
            storeVel = storeVel * 0.80f + (storeTarget - storeDisplay) * 0.05f;
            storeDisplay += storeVel;
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["GeneratorReadoutUI_DrawPos_X"] = DrawPosition.X;
            tag["GeneratorReadoutUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("GeneratorReadoutUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            if (tag.TryGet("GeneratorReadoutUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
        }

        public override void RightClickByTile(bool newTP) {
            if (!newTP) {
                IsActive = !IsActive;
            }
            else {
                IsActive = true;
            }
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.3f, Pitch = -0.5f });
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f || GeneratorTP == null) {
                return;
            }
            IGeneratorReadout readout = Readout;
            if (readout == null) {
                return;
            }

            float alpha = uiFadeAlpha;

            //钢壳 + 铆钉
            IndustrialTerminalRenderer.ShaderPanel(spriteBatch, panelRect, alpha);
            int inset = IndustrialTerminalRenderer.Chamfer + 2;
            IndustrialTerminalRenderer.DrawRivet(spriteBatch, new Vector2(panelRect.X + inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(spriteBatch, new Vector2(panelRect.Right - inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(spriteBatch, new Vector2(panelRect.X + inset, panelRect.Bottom - inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(spriteBatch, new Vector2(panelRect.Right - inset, panelRect.Bottom - inset), alpha);

            //铭牌:显示这台机器的名字(亮暖填漆字)
            string title = Lang.GetItemNameValue(GeneratorTP.TargetItem);
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.78f;
            Rectangle plate = new(panelRect.X + 18, panelRect.Y + 8, (int)titleSize.X + 26, 25);
            IndustrialTerminalRenderer.DrawNameplate(spriteBatch, plate, alpha);
            IndustrialTerminalRenderer.DrawPlateTitle(spriteBatch, plate, title, alpha, 0.78f);

            IndustrialTerminalRenderer.DrawEtchedLine(spriteBatch, panelRect.X + 12, panelRect.Width - 24, panelRect.Y + 40, alpha, 0.8f);
            IndustrialTerminalRenderer.DrawLatch(spriteBatch, closeRect.Center.ToVector2(), alpha, latchHover);

            //双表盘:工况(转起来才有微颤)与储能
            bool ok = readout.ConditionOk;
            float jitter = ok ? MathF.Sin(animTimer * 30f) * 0.005f : 0f;
            string condLabel = readout.ReadoutKind == GeneratorReadoutKind.Wind ? WindLabel.Value : FlowLabel.Value;
            Color condAccent = ok ? Amber : Color.Lerp(Amber, WarnRed, 0.55f);
            IndustrialTerminalRenderer.DrawGauge(spriteBatch, condGaugeCenter, 32f, condDisplay + jitter,
                condAccent, alpha, condLabel, $"{(int)(MathHelper.Clamp(readout.ConditionRatio, 0f, 1f) * 100f)}%");

            float storeRatio = GeneratorTP.MachineData != null
                ? MathHelper.Clamp(GeneratorTP.MachineData.UEvalue / GeneratorTP.MaxUEValue, 0f, 1f) : 0f;
            IndustrialTerminalRenderer.DrawGauge(spriteBatch, storeGaugeCenter, 32f, storeDisplay,
                Amber, alpha, StorageLabel.Value, $"{(int)(storeRatio * 100f)}%");

            //模块插座行
            if (GeneratorTP.ModuleSlotCount > 0) {
                socketStrip.Draw(spriteBatch, GeneratorTP.ModuleRack, GeneratorTP.ModuleSlotCount,
                    alpha, MousePosition.ToPoint());
            }

            //状态灯 + 输出读数
            float lampY = panelRect.Bottom - 30;
            Color lampColor = ok ? IndustrialTerminalRenderer.OkGreen : WarnRed;
            float lampBright = ok
                ? MathF.Sin(animTimer * 2.4f) * 0.2f + 0.7f
                : MathF.Sin(animTimer * 4.5f) * 0.3f + 0.55f;
            IndustrialTerminalRenderer.DrawLamp(spriteBatch, new Vector2(panelRect.X + 34, lampY + 8), lampColor, alpha, lampBright);

            string state = ok ? RunningText.Value
                : readout.ReadoutKind == GeneratorReadoutKind.Wind ? NoWindText.Value : NoWaterText.Value;
            Utils.DrawBorderString(spriteBatch, state, new Vector2(panelRect.X + 48, lampY),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.64f);

            string output = OutputLine.Format(readout.OutputPerSecond.ToString("0.0"));
            Vector2 outputSize = FontAssets.MouseText.Value.MeasureString(output) * 0.62f;
            Utils.DrawBorderString(spriteBatch, output,
                new Vector2(panelRect.Right - 16 - outputSize.X, lampY + 1), TextMain * alpha, 0.62f);

            //插座悬停(先于表盘)
            if (!isDragging && GeneratorTP.ModuleSlotCount > 0
                && socketStrip.DrawHoverTip(spriteBatch, GeneratorTP.ModuleRack, GeneratorTP.ModuleSlotCount,
                    MousePosition.ToPoint(), (text, color) => ShowTip(spriteBatch, text, color))) {
                return;
            }

            //表盘悬停:精确储能数
            if (!isDragging && storeGaugeRect.Contains(MousePosition.ToPoint()) && GeneratorTP.MachineData != null) {
                ShowTip(spriteBatch, $"{(int)GeneratorTP.MachineData.UEvalue}/{(int)GeneratorTP.MaxUEValue} {PowerUnitText.Value}");
            }
        }

        private static void ShowTip(SpriteBatch sb, string text) => ShowTip(sb, text, TextMain);

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
    }
}
