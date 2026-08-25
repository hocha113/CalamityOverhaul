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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.TeleportStations
{
    /// <summary>
    /// 传送站面板:列出世界上全部站点(按距离排序),点击即传送;
    /// 本站可改名,名字编辑走客户端权威推送。
    /// 笔刷与材质在 <see cref="IndustrialTerminalRenderer"/>
    /// </summary>
    internal class TeleportStationUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI.TeleportStation";

        #region 布局与状态
        private const float PanelWidth = 396f;
        private const float PanelHeight = 344f;
        private const int RowHeight = 42;
        private const int VisibleRows = 5;

        private static Color TextMain => IndustrialTerminalRenderer.TextMain;
        private static Color TextDim => IndustrialTerminalRenderer.TextDim;
        private static Color WarnRed => IndustrialTerminalRenderer.WarnRed;
        private static Color OkGreen => IndustrialTerminalRenderer.OkGreen;
        private static Color Accent => TeleportStation.Tint;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        internal TeleportStationTP Station;
        internal bool IsActive;

        private float uiFadeAlpha;
        public override bool Active => IsActive || uiFadeAlpha > 0.01f;

        private float latchHover;
        private float animTimer;

        //拖拽
        private bool isDragging;
        private Vector2 dragOffset;
        private bool positionInitialized;

        //站名编辑
        private bool renaming;
        private string nameBuffer = "";
        private int textBlinker;

        //列表
        private readonly List<TeleportStationTP> stations = [];
        private int scrollOffset;

        //布局矩形
        private Rectangle panelRect;
        private Rectangle closeRect;
        private Rectangle nameRowRect;
        private Rectangle renameBtn;
        private Rectangle energyBarRect;
        private Rectangle listRect;

        private bool hoveringRename;
        private int hoveringRow = -1;
        #endregion

        #region 本地化
        protected static LocalizedText TitleText;
        protected static LocalizedText LocalLabel;
        protected static LocalizedText RenameText;
        protected static LocalizedText RenameHint;
        protected static LocalizedText EmptyListText;
        protected static LocalizedText RowInfoLine;
        protected static LocalizedText StatusReady;
        protected static LocalizedText StatusNoPower;
        protected static LocalizedText StatusTargetDead;
        protected static LocalizedText EnergyLabel;
        protected static LocalizedText PowerUnit;

        public override Texture2D Texture => VaultAsset.placeholder2.Value;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "传送站网络");
            LocalLabel = this.GetLocalization(nameof(LocalLabel), () => "本站");
            RenameText = this.GetLocalization(nameof(RenameText), () => "改名");
            RenameHint = this.GetLocalization(nameof(RenameHint), () => "回车确认,Esc 取消");
            EmptyListText = this.GetLocalization(nameof(EmptyListText), () => "世界上没有其他传送站");
            RowInfoLine = this.GetLocalization(nameof(RowInfoLine), () => "{0} 格 · {1} UE");
            StatusReady = this.GetLocalization(nameof(StatusReady), () => "可传送");
            StatusNoPower = this.GetLocalization(nameof(StatusNoPower), () => "本站缺电");
            StatusTargetDead = this.GetLocalization(nameof(StatusTargetDead), () => "对端缺电");
            EnergyLabel = this.GetLocalization(nameof(EnergyLabel), () => "电力");
            PowerUnit = this.GetLocalization(nameof(PowerUnit), () => "UE");
        }
        #endregion

        public void Interactive(TeleportStationTP tp) {
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
            scrollOffset = 0;
            CancelRename();
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.3f, Pitch = -0.5f });
        }

        private void CancelRename() {
            renaming = false;
            nameBuffer = "";
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
            textBlinker++;

            float targetAlpha = IsActive ? 1f : 0f;
            uiFadeAlpha = MathHelper.Lerp(uiFadeAlpha, targetAlpha, 0.15f);
            if (uiFadeAlpha < 0.01f && !IsActive) {
                return;
            }

            if (Station == null || !Station.Active) {
                IsActive = false;
                CancelRename();
                return;
            }

            if (Main.LocalPlayer.DistanceSQ(Station.CenterInWorld) > 40000) {
                IsActive = false;
                CancelRename();
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
                return;
            }

            ComputeLayout();
            RefreshStations();
            HandleRenameInput();

            Point mouse = MousePosition.ToPoint();
            hoveringRename = renameBtn.Contains(mouse) && !isDragging;
            hoverInMainPage = panelRect.Contains(mouse);
            latchHover = MathHelper.Lerp(latchHover, closeRect.Contains(mouse) ? 1f : 0f, 0.2f);
            UpdateHoveredRow(mouse);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();

                //列表滚动
                int wheel = PlayerInput.ScrollWheelDeltaForUI;
                if (wheel != 0 && listRect.Contains(mouse)) {
                    int maxOffset = Math.Max(0, stations.Count - VisibleRows);
                    scrollOffset = Math.Clamp(scrollOffset - Math.Sign(wheel), 0, maxOffset);
                }
            }

            //闩钮关闭
            if (closeRect.Contains(mouse) && keyLeftPressState == KeyPressState.Pressed) {
                IsActive = false;
                CancelRename();
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f, Volume = 0.5f });
                return;
            }

            HandleClicks(mouse);

            //背景区拖拽,避开控件与列表
            bool overControl = hoveringRename || hoveringRow >= 0
                || closeRect.Contains(mouse) || listRect.Contains(mouse);
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
            nameRowRect = new Rectangle(panelRect.X + 26, panelRect.Y + 52, 250, 26);
            renameBtn = new Rectangle(panelRect.X + 288, panelRect.Y + 52, 60, 26);
            energyBarRect = new Rectangle(panelRect.X + 26, panelRect.Y + 92, 250, 10);
            listRect = new Rectangle(panelRect.X + 16, panelRect.Y + 116, (int)PanelWidth - 32, RowHeight * VisibleRows);
        }

        /// <summary>收集除本站外的全部站点,按与本站的距离升序</summary>
        private void RefreshStations() {
            TeleportStationTP.CollectStations(stations);
            stations.Remove(Station);
            Vector2 origin = Station.CenterInWorld;
            stations.Sort((a, b) => a.CenterInWorld.DistanceSQ(origin).CompareTo(b.CenterInWorld.DistanceSQ(origin)));
            int maxOffset = Math.Max(0, stations.Count - VisibleRows);
            scrollOffset = Math.Clamp(scrollOffset, 0, maxOffset);
        }

        private void UpdateHoveredRow(Point mouse) {
            hoveringRow = -1;
            if (isDragging || renaming || !listRect.Contains(mouse)) {
                return;
            }
            int rowIndex = (mouse.Y - listRect.Y) / RowHeight;
            int listIndex = scrollOffset + rowIndex;
            if (rowIndex >= 0 && rowIndex < VisibleRows && listIndex < stations.Count) {
                hoveringRow = listIndex;
            }
        }

        /// <summary>站名编辑:原版 IME 文本通道,回车提交推送,Esc 取消</summary>
        private void HandleRenameInput() {
            if (!renaming) {
                return;
            }

            PlayerInput.WritingText = true;
            Main.instance.HandleIME();
            string input = Main.GetInputText(nameBuffer);
            if (input.Length > TeleportStationTP.MaxNameLength) {
                input = input[..TeleportStationTP.MaxNameLength];
            }
            nameBuffer = input;

            if (Main.inputTextEnter) {
                Station.StationName = nameBuffer.Trim();
                Station.SendData();//客户端权威编辑推送
                CancelRename();
                SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.5f });
            }
            else if (Main.inputTextEscape) {
                CancelRename();
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
            }
        }

        private void HandleClicks(Point mouse) {
            if (keyLeftPressState != KeyPressState.Pressed || Station == null) {
                return;
            }

            if (hoveringRename && !renaming) {
                renaming = true;
                nameBuffer = Station.StationName ?? "";
                Main.clrInput();
                SoundEngine.PlaySound(SoundID.MenuTick);
                return;
            }

            if (hoveringRow >= 0 && hoveringRow < stations.Count) {
                TeleportStationTP target = stations[hoveringRow];
                if (Station.TryTeleportLocalPlayer(target)) {
                    IsActive = false;
                    CancelRename();
                }
            }
        }

        public override void OnEnterWorld() {
            IsActive = false;
            CancelRename();
        }

        public override void SaveUIData(TagCompound tag) {
            tag["TeleportStationUI_DrawPos_X"] = DrawPosition.X;
            tag["TeleportStationUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("TeleportStationUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            if (tag.TryGet("TeleportStationUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f || Station == null) {
                return;
            }

            float alpha = uiFadeAlpha;

            //钢壳 + 铆钉 + 铭牌
            IndustrialTerminalRenderer.ShaderPanel(spriteBatch, panelRect, alpha);
            int inset = IndustrialTerminalRenderer.Chamfer + 2;
            IndustrialTerminalRenderer.DrawRivet(spriteBatch, new Vector2(panelRect.X + inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(spriteBatch, new Vector2(panelRect.Right - inset, panelRect.Y + inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(spriteBatch, new Vector2(panelRect.X + inset, panelRect.Bottom - inset), alpha);
            IndustrialTerminalRenderer.DrawRivet(spriteBatch, new Vector2(panelRect.Right - inset, panelRect.Bottom - inset), alpha);

            string title = TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.86f;
            Rectangle plate = new(panelRect.X + 22, panelRect.Y + 9, (int)titleSize.X + 30, 27);
            IndustrialTerminalRenderer.DrawNameplate(spriteBatch, plate, alpha);
            IndustrialTerminalRenderer.DrawPlateTitle(spriteBatch, plate, title, alpha, 0.86f);

            IndustrialTerminalRenderer.DrawEtchedLine(spriteBatch, panelRect.X + 14, panelRect.Width - 28, panelRect.Y + 44, alpha, 0.8f);
            IndustrialTerminalRenderer.DrawLatch(spriteBatch, closeRect.Center.ToVector2(), alpha, latchHover);

            DrawLocalRow(spriteBatch, alpha);
            DrawStationList(spriteBatch, alpha);
        }

        /// <summary>本站行:站名(或编辑框)+ 改名按钮 + 电量条</summary>
        private void DrawLocalRow(SpriteBatch sb, float alpha) {
            Utils.DrawBorderString(sb, LocalLabel.Value,
                new Vector2(nameRowRect.X, nameRowRect.Y + 4), TextDim * alpha, 0.62f);

            Vector2 namePos = new(nameRowRect.X + 44, nameRowRect.Y + 3);
            if (renaming) {
                //编辑框:凹槽底 + 输入文本 + 闪烁光标
                IndustrialTerminalRenderer.DrawRecess(sb,
                    new Rectangle(nameRowRect.X + 40, nameRowRect.Y - 2, nameRowRect.Width - 40, 26), alpha);
                string shown = nameBuffer;
                if (textBlinker % 40 < 20) {
                    shown += "|";
                }
                Utils.DrawBorderString(sb, shown, namePos, Color.Lerp(TextMain, Accent, 0.5f) * alpha, 0.72f);
                Utils.DrawBorderString(sb, RenameHint.Value,
                    new Vector2(nameRowRect.X, nameRowRect.Y + 28), TextDim * (alpha * 0.9f), 0.55f);
            }
            else {
                Utils.DrawBorderString(sb, Station.ShowName, namePos,
                    Color.Lerp(TextMain, Accent, 0.4f) * alpha, 0.72f);
            }

            IndustrialTerminalRenderer.DrawButton(sb, renameBtn, alpha, hoveringRename ? 1f : 0f,
                hoveringRename && keyLeftPressState == KeyPressState.Held, RenameText.Value);

            //电量行
            float ratio = MathHelper.Clamp(Station.MachineData.UEvalue / Station.MaxUEValue, 0f, 1f);
            Utils.DrawBorderString(sb, $"{EnergyLabel.Value} {(int)Station.MachineData.UEvalue}/{(int)Station.MaxUEValue} {PowerUnit.Value}",
                new Vector2(energyBarRect.X, energyBarRect.Y - 16), TextDim * alpha, 0.58f);
            IndustrialTerminalRenderer.DrawTickBar(sb, energyBarRect, ratio, Accent, alpha);
        }

        /// <summary>站点列表:名字 + 距离费用 + 状态灯,可传送行点击出发</summary>
        private void DrawStationList(SpriteBatch sb, float alpha) {
            IndustrialTerminalRenderer.DrawEtchedLine(sb, panelRect.X + 14, panelRect.Width - 28, listRect.Y - 4, alpha, 0.6f);

            if (stations.Count == 0) {
                Utils.DrawBorderString(sb, EmptyListText.Value,
                    new Vector2(listRect.X + 12, listRect.Y + 16), TextDim * alpha, 0.66f);
                return;
            }

            Texture2D px = VaultAsset.placeholder2.Value;
            int shown = Math.Min(VisibleRows, stations.Count - scrollOffset);
            for (int row = 0; row < shown; row++) {
                int listIndex = scrollOffset + row;
                TeleportStationTP target = stations[listIndex];
                Rectangle rowRect = new(listRect.X, listRect.Y + row * RowHeight, listRect.Width, RowHeight - 4);

                float cost = TeleportStationTP.TeleportCost(Station, target);
                bool canAfford = Station.MachineData.UEvalue >= cost;
                bool targetReady = target.MachineData.UEvalue >= TeleportStationTP.ArrivalReserveUE;
                bool ready = canAfford && targetReady;

                //行底:悬停亮起
                bool hovered = hoveringRow == listIndex;
                Color bed = hovered ? Color.Lerp(new Color(13, 11, 9), Accent, 0.18f) : new Color(13, 11, 9);
                sb.Draw(px, rowRect, bed * (alpha * (hovered ? 0.9f : 0.55f)));

                //状态灯
                Color lampColor = ready ? OkGreen : (!canAfford ? WarnRed : TextDim);
                float lampBright = ready ? MathF.Sin(animTimer * 2.6f) * 0.2f + 0.7f : 0.45f;
                IndustrialTerminalRenderer.DrawLamp(sb, new Vector2(rowRect.X + 14, rowRect.Y + rowRect.Height / 2), lampColor, alpha, lampBright);

                //站名
                Utils.DrawBorderString(sb, target.ShowName,
                    new Vector2(rowRect.X + 28, rowRect.Y + 4), TextMain * alpha, 0.7f);

                //距离与费用
                float tiles = Station.CenterInWorld.Distance(target.CenterInWorld) / 16f;
                string info = RowInfoLine.Format((int)tiles, (int)MathF.Ceiling(cost));
                Utils.DrawBorderString(sb, info,
                    new Vector2(rowRect.X + 28, rowRect.Y + 21), TextDim * alpha, 0.56f);

                //右侧状态字
                string status = ready ? StatusReady.Value : (!canAfford ? StatusNoPower.Value : StatusTargetDead.Value);
                Vector2 statusSize = FontAssets.MouseText.Value.MeasureString(status) * 0.56f;
                Utils.DrawBorderString(sb, status,
                    new Vector2(rowRect.Right - 12 - statusSize.X, rowRect.Y + 12),
                    Color.Lerp(TextMain, lampColor, 0.5f) * alpha, 0.56f);
            }

            //滚动指示
            if (stations.Count > VisibleRows) {
                float viewRatio = VisibleRows / (float)stations.Count;
                float posRatio = scrollOffset / (float)(stations.Count - VisibleRows);
                int barHeight = Math.Max(18, (int)(listRect.Height * viewRatio));
                int barY = listRect.Y + (int)((listRect.Height - barHeight) * posRatio);
                sb.Draw(px, new Rectangle(listRect.Right - 4, barY, 3, barHeight),
                    IndustrialTerminalRenderer.Brass * alpha);
            }
        }
        #endregion
    }
}
