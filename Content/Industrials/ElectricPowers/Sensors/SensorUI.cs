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
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Sensors
{
    /// <summary>
    /// 传感器设置面板:条件单选列表+阈值/半径调节+输出方式+状态灯+电力表盘。<br/>
    /// 全部编辑为客户端权威:本地改 TP 字段后 SendData 推送(§2.3 UI 契约);
    /// 文本注册在 <see cref="Sensor"/> 物品下,本类只引用
    /// </summary>
    internal class SensorUI : UIHandle
    {
        #region 布局与状态
        private const float PanelWidth = 410f;
        private const float PanelHeight = 330f;
        private const int ModeRowHeight = 26;
        private const int ModeCount = 8;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color Accent => Sensor.Tint;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        public static SensorUI Instance => UIHandleLoader.GetUIHandleOfType<SensorUI>();

        internal SensorTP Station;
        internal bool IsActive;

        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        private float powerDisplay;
        private float powerVel;
        private float latchHover;
        private float animTimer;

        private bool isDragging;
        private Vector2 dragOffset;
        private bool positionInitialized;

        private Rectangle panelRect;
        private Rectangle closeRect;
        private readonly Rectangle[] modeRects = new Rectangle[ModeCount];
        private Rectangle minusBtn;
        private Rectangle plusBtn;
        private Rectangle outputBtn;
        private Vector2 lampPos;
        private Vector2 gaugeCenter;
        private Rectangle gaugeRect;

        private int hoveringMode = -1;
        private bool hoveringMinus;
        private bool hoveringPlus;
        private bool hoveringOutput;
        private bool hoveringGauge;
        #endregion

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public void Interactive(SensorTP tp) {
            if (tp == null) {
                return;
            }

            if (Station == tp) {
                IsActive = !IsActive;
            }
            else {
                IsActive = true;
            }

            Station = tp;
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

            if (Station == null || !Station.Active) {
                IsActive = false;
                return;
            }

            if (Main.LocalPlayer.DistanceSQ(Station.CenterInWorld) > 40000) {
                IsActive = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
                return;
            }

            ComputeLayout();

            float powerTarget = MathHelper.Clamp(Station.MachineData.UEvalue / Station.MaxUEValue, 0f, 1f);
            powerVel = powerVel * 0.80f + (powerTarget - powerDisplay) * 0.05f;
            powerDisplay += powerVel;

            Point mouse = MousePosition.ToPoint();
            hoveringMode = -1;
            for (int i = 0; i < ModeCount; i++) {
                if (modeRects[i].Contains(mouse) && !isDragging) {
                    hoveringMode = i;
                    break;
                }
            }
            bool hasParam = HasParamRow();
            hoveringMinus = hasParam && minusBtn.Contains(mouse) && !isDragging;
            hoveringPlus = hasParam && plusBtn.Contains(mouse) && !isDragging;
            hoveringOutput = outputBtn.Contains(mouse) && !isDragging;
            hoveringGauge = gaugeRect.Contains(mouse) && !isDragging;
            hoverInMainPage = panelRect.Contains(mouse);
            latchHover = MathHelper.Lerp(latchHover, closeRect.Contains(mouse) ? 1f : 0f, 0.2f);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
            }

            if (closeRect.Contains(mouse) && keyLeftPressState == KeyPressState.Pressed) {
                IsActive = false;
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f, Volume = 0.5f });
                return;
            }

            if (keyLeftPressState == KeyPressState.Pressed) {
                if (hoveringMode >= 0) {
                    SelectMode((SensorMode)hoveringMode);
                }
                else if (hoveringMinus) {
                    AdjustParam(-1);
                }
                else if (hoveringPlus) {
                    AdjustParam(+1);
                }
                else if (hoveringOutput) {
                    Station.LevelOutput = !Station.LevelOutput;
                    Station.SendData();
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }

            //背景区拖拽,避开控件
            bool overControl = hoveringMode >= 0 || hoveringMinus || hoveringPlus
                || hoveringOutput || closeRect.Contains(mouse);
            if (keyLeftPressState == KeyPressState.Pressed && hoverInMainPage && !overControl && !isDragging) {
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
            closeRect = new Rectangle(panelRect.Right - 38, panelRect.Y + 9, 26, 26);

            for (int i = 0; i < ModeCount; i++) {
                modeRects[i] = new Rectangle(panelRect.X + 22, panelRect.Y + 78 + i * ModeRowHeight, 190, ModeRowHeight - 2);
            }

            minusBtn = new Rectangle(panelRect.X + 240, panelRect.Y + 100, 24, 24);
            plusBtn = new Rectangle(panelRect.X + 356, panelRect.Y + 100, 24, 24);
            outputBtn = new Rectangle(panelRect.X + 240, panelRect.Y + 168, 140, 28);
            lampPos = new Vector2(panelRect.X + 248, panelRect.Y + 218);
            gaugeCenter = new Vector2(panelRect.X + 310, panelRect.Y + 276);
            gaugeRect = new Rectangle((int)gaugeCenter.X - 30, (int)gaugeCenter.Y - 30, 60, 60);
        }

        /// <summary>当前模式是否有可调参数行</summary>
        private bool HasParamRow()
            => Station != null && Station.Mode is SensorMode.ChargeAbove or SensorMode.ChargeBelow or SensorMode.Enemy;

        private void SelectMode(SensorMode mode) {
            if (Station.Mode == mode) {
                return;
            }
            Station.Mode = mode;
            //换条件后旧判定作废:静默复位,权威端下一刻按新条件重新evaluate
            Station.ConditionActive = false;
            Station.SendData();
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private void AdjustParam(int dir) {
            if (Station.Mode is SensorMode.ChargeAbove or SensorMode.ChargeBelow) {
                int pct = Math.Clamp(Station.ThresholdPct + dir * 5, 5, 95);
                if (pct == Station.ThresholdPct) {
                    return;
                }
                Station.ThresholdPct = (byte)pct;
            }
            else if (Station.Mode == SensorMode.Enemy) {
                short[] steps = SensorTP.RangeSteps;
                int index = Array.IndexOf(steps, Station.EnemyRange);
                if (index < 0) {
                    index = 1;
                }
                int next = Math.Clamp(index + dir, 0, steps.Length - 1);
                if (steps[next] == Station.EnemyRange) {
                    return;
                }
                Station.EnemyRange = steps[next];
            }
            else {
                return;
            }
            Station.SendData();
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["SensorUI_DrawPos_X"] = DrawPosition.X;
            tag["SensorUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("SensorUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            if (tag.TryGet("SensorUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f || Station == null) {
                return;
            }

            DrawShell(spriteBatch);
            DrawModeList(spriteBatch);
            DrawParamColumn(spriteBatch);
            DrawHoverTips(spriteBatch);
        }

        private void DrawShell(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            IndustrialTerminalRenderer.ShaderPanel(sb, panelRect, alpha);

            int inset = IndustrialTerminalRenderer.Chamfer + 2;
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.X + inset, panelRect.Bottom - inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(sb, new Vector2(panelRect.Right - inset, panelRect.Bottom - inset), alpha);

            string title = Sensor.TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.86f;
            Rectangle plate = new(panelRect.X + 22, panelRect.Y + 9, (int)titleSize.X + 30, 27);
            IndustrialTerminalRenderer.DrawNameplate(sb, plate, alpha);
            IndustrialTerminalRenderer.DrawPlateTitle(sb, plate, title, alpha, 0.86f);

            IndustrialTerminalRenderer.DrawEtchedLine(sb, panelRect.X + 14, panelRect.Width - 28, panelRect.Y + 44, alpha, 0.8f);
            IndustrialTerminalRenderer.DrawLatch(sb, closeRect.Center.ToVector2(), alpha, latchHover);
        }

        private void DrawModeList(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            Utils.DrawBorderString(sb, Sensor.ConditionLabelText.Value,
                new Vector2(panelRect.X + 22, panelRect.Y + 56), TextDim * alpha, 0.62f);

            for (int i = 0; i < ModeCount; i++) {
                SensorMode mode = (SensorMode)i;
                Rectangle row = modeRects[i];
                bool selected = Station.Mode == mode;
                bool hovered = hoveringMode == i;

                //行底:选中亮、悬停微亮
                Texture2D px = VaultAsset.placeholder2.Value;
                Rectangle src = new(0, 0, 1, 1);
                if (selected) {
                    sb.Draw(px, row, src, ModeRowColor(mode) * (alpha * 0.22f));
                }
                else if (hovered) {
                    sb.Draw(px, row, src, Color.White * (alpha * 0.06f));
                }

                //单选灯 + 模式名
                Color lamp = selected ? ModeRowColor(mode) : TextDim;
                float bright = selected ? MathF.Sin(animTimer * 2.4f) * 0.15f + 0.75f : 0.22f;
                IndustrialTerminalRenderer.DrawLamp(sb, new Vector2(row.X + 12, row.Y + row.Height / 2), lamp, alpha, bright);
                Utils.DrawBorderString(sb, ModeName(mode), new Vector2(row.X + 26, row.Y + 4),
                    (selected ? Color.Lerp(TextMain, lamp, 0.4f) : TextDim) * alpha, 0.7f);
            }
        }

        private void DrawParamColumn(SpriteBatch sb) {
            float alpha = uiFadeAlpha;

            //参数行:阈值 / 半径 / 事件说明
            if (Station.Mode is SensorMode.ChargeAbove or SensorMode.ChargeBelow) {
                Utils.DrawBorderString(sb, Sensor.ThresholdLabelText.Value,
                    new Vector2(panelRect.X + 240, panelRect.Y + 78), TextDim * alpha, 0.62f);
                IndustrialTerminalRenderer.DrawButton(sb, minusBtn, alpha, hoveringMinus ? 1f : 0f,
                    hoveringMinus && keyLeftPressState == KeyPressState.Held, "-");
                IndustrialTerminalRenderer.DrawButton(sb, plusBtn, alpha, hoveringPlus ? 1f : 0f,
                    hoveringPlus && keyLeftPressState == KeyPressState.Held, "+");
                string value = $"{Station.ThresholdPct}%";
                Vector2 size = FontAssets.MouseText.Value.MeasureString(value) * 0.85f;
                Utils.DrawBorderString(sb, value,
                    new Vector2((minusBtn.Right + plusBtn.X) / 2f - size.X / 2f, minusBtn.Y + 3), TextMain * alpha, 0.85f);
            }
            else if (Station.Mode == SensorMode.Enemy) {
                Utils.DrawBorderString(sb, Sensor.RangeLabelText.Value,
                    new Vector2(panelRect.X + 240, panelRect.Y + 78), TextDim * alpha, 0.62f);
                IndustrialTerminalRenderer.DrawButton(sb, minusBtn, alpha, hoveringMinus ? 1f : 0f,
                    hoveringMinus && keyLeftPressState == KeyPressState.Held, "-");
                IndustrialTerminalRenderer.DrawButton(sb, plusBtn, alpha, hoveringPlus ? 1f : 0f,
                    hoveringPlus && keyLeftPressState == KeyPressState.Held, "+");
                string value = $"{Station.EnemyRange}px";
                Vector2 size = FontAssets.MouseText.Value.MeasureString(value) * 0.85f;
                Utils.DrawBorderString(sb, value,
                    new Vector2((minusBtn.Right + plusBtn.X) / 2f - size.X / 2f, minusBtn.Y + 3), TextMain * alpha, 0.85f);
            }
            else if (Station.Mode != SensorMode.Off) {
                Utils.DrawBorderString(sb, Sensor.EventHintText.Value,
                    new Vector2(panelRect.X + 240, panelRect.Y + 96), TextDim * alpha, 0.6f);
            }

            //输出方式
            Utils.DrawBorderString(sb, Sensor.OutputLabelText.Value,
                new Vector2(panelRect.X + 240, panelRect.Y + 148), TextDim * alpha, 0.62f);
            string outputLabel = Station.LevelOutput ? Sensor.OutputLevelText.Value : Sensor.OutputPulseText.Value;
            IndustrialTerminalRenderer.DrawButton(sb, outputBtn, alpha, hoveringOutput ? 1f : 0f,
                hoveringOutput && keyLeftPressState == KeyPressState.Held, outputLabel);

            //状态灯
            string state;
            Color lampColor;
            float lampBright;
            if (Station.Mode == SensorMode.Off) {
                state = Sensor.StatusOffText.Value;
                lampColor = TextDim;
                lampBright = 0.2f;
            }
            else if (!Station.Powered) {
                state = Sensor.StatusNoPowerText.Value;
                lampColor = WarnRed;
                lampBright = MathF.Sin(animTimer * 5f) * 0.35f + 0.55f;
            }
            else if (Station.ConditionActive) {
                state = Sensor.StatusActiveText.Value;
                lampColor = Station.ModeColor();
                lampBright = MathF.Sin(animTimer * 2.6f) * 0.2f + 0.75f;
            }
            else {
                state = Sensor.StatusIdleText.Value;
                lampColor = TextDim;
                lampBright = 0.32f;
            }
            IndustrialTerminalRenderer.DrawLamp(sb, lampPos, lampColor, alpha, lampBright);
            Utils.DrawBorderString(sb, state, lampPos + new Vector2(14, -8),
                Color.Lerp(TextMain, lampColor, 0.35f) * alpha, 0.66f);

            //电力表盘
            float ratio = MathHelper.Clamp(Station.MachineData.UEvalue / Station.MaxUEValue, 0f, 1f);
            IndustrialTerminalRenderer.DrawGauge(sb, gaugeCenter, 28f, powerDisplay,
                Accent, alpha, Sensor.EnergyLabelText.Value, $"{(int)(ratio * 100f)}%");
        }

        private void DrawHoverTips(SpriteBatch sb) {
            if (isDragging) {
                return;
            }
            if (hoveringGauge) {
                string text = $"{(int)Station.MachineData.UEvalue}/{(int)Station.MaxUEValue} UE";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
                Vector2 pos = new Vector2(Main.mouseX, Main.mouseY) + new Vector2(18, 18);
                Rectangle bg = new((int)pos.X - 9, (int)pos.Y - 5, (int)textSize.X + 18, (int)textSize.Y + 10);
                IndustrialTerminalRenderer.DrawTooltipPlate(sb, bg, 1f);
                Utils.DrawBorderString(sb, text, pos, TextMain, 0.75f);
            }
        }

        private static string ModeName(SensorMode mode) => mode switch {
            SensorMode.ChargeAbove => Sensor.ModeChargeAboveText.Value,
            SensorMode.ChargeBelow => Sensor.ModeChargeBelowText.Value,
            SensorMode.Enemy => Sensor.ModeEnemyText.Value,
            SensorMode.BloodMoon => Sensor.ModeBloodMoonText.Value,
            SensorMode.Eclipse => Sensor.ModeEclipseText.Value,
            SensorMode.SlimeRain => Sensor.ModeSlimeRainText.Value,
            SensorMode.Invasion => Sensor.ModeInvasionText.Value,
            _ => Sensor.ModeOffText.Value,
        };

        private static Color ModeRowColor(SensorMode mode) => mode switch {
            SensorMode.ChargeAbove => new Color(110, 220, 130),
            SensorMode.ChargeBelow => new Color(240, 150, 70),
            SensorMode.Enemy => new Color(235, 84, 74),
            SensorMode.BloodMoon => new Color(200, 46, 66),
            SensorMode.Eclipse => new Color(235, 190, 82),
            SensorMode.SlimeRain => new Color(92, 176, 230),
            SensorMode.Invasion => new Color(186, 108, 228),
            _ => new Color(150, 150, 158),
        };
        #endregion
    }
}
