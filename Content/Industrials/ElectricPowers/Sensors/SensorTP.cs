using CalamityOverhaul.Content.Industrials.ElectricPowers.ControlVisuals;
using CalamityOverhaul.Content.Industrials.ElectricPowers.GridSwitches;
using CalamityOverhaul.Content.Industrials.ElectricPowers.WireInterfaces;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Sensors
{
    /// <summary>传感器触发条件</summary>
    internal enum SensorMode : byte
    {
        /// <summary>关闭,不判定不耗电</summary>
        Off,
        /// <summary>邻接电网充能率高于阈值</summary>
        ChargeAbove,
        /// <summary>邻接电网充能率低于阈值</summary>
        ChargeBelow,
        /// <summary>警戒半径内出现敌怪</summary>
        Enemy,
        /// <summary>血月</summary>
        BloodMoon,
        /// <summary>日食</summary>
        Eclipse,
        /// <summary>史莱姆雨</summary>
        SlimeRain,
        /// <summary>入侵事件进行中</summary>
        Invasion,
    }

    /// <summary>
    /// 多模式传感器TP:UI 选定条件,权威端每刻判定,条件跨越边沿时经 Wiring.TripWire
    /// 沿自身接线发机关脉冲;阈值/敌情判定自带迟滞,不重发。<br/>
    /// 输出方式:电平跟随=进入与离开各发一次脉冲,原版翻转器件(灯/门)表现为状态跟随;
    /// 单次脉冲=仅进入时发一次。<br/>
    /// 条件配置为客户端编辑+推送(§2.3 UI 契约),判定与发信仅权威端;
    /// TripWire 在多人客户端内部为空操作,天然只属于服务器世界机制
    /// </summary>
    internal class SensorTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<SensorTile>();
        public override int TargetItem => ModContent.ItemType<Sensor>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 200;

        /// <summary>待机耗电(UE/刻),仅条件模式开启时消耗</summary>
        internal const float StandbyCost = 0.1f;
        /// <summary>警戒半径档位(像素)</summary>
        internal static readonly short[] RangeSteps = [300, 500, 800, 1200];
        /// <summary>阈值迟滞带宽:激活后须回撤 5% 才退出</summary>
        private const float ThresholdBand = 0.05f;
        /// <summary>敌情退出去抖(刻):连续无敌这么久才算警报解除</summary>
        private const int EnemyOffDebounce = 60;

        internal SensorMode Mode;
        /// <summary>电量阈值(百分比 5~95,步进 5)</summary>
        internal byte ThresholdPct = 50;
        /// <summary>警戒半径(像素),取值限于 <see cref="RangeSteps"/></summary>
        internal short EnemyRange = 500;
        /// <summary>true=电平跟随,false=单次脉冲</summary>
        internal bool LevelOutput = true;
        /// <summary>条件当前判定;包内同步供眼灯显示,序列化防止重载重发边沿</summary>
        internal bool ConditionActive;

        /// <summary>眼灯亮度包络,纯表现</summary>
        internal float EyeGlow { get; private set; }
        /// <summary>是否有电可判定(模式开启且电量足够)</summary>
        internal bool Powered => Mode != SensorMode.Off && MachineData != null && MachineData.UEvalue >= StandbyCost;

        private int enemyOffTicks;
        private readonly HashSet<MachineTP> neighborCache = new();

        public override void SetBattery() {
            //控制件不参与基类导电均衡:电量只经管道单向灌入
            Efficiency = 0;
        }

        #region 序列化:MachineData+待机位(基类) → 条件配置 → 判定状态
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write((byte)Mode);
            data.Write(ThresholdPct);
            data.Write(EnemyRange);
            data.Write(LevelOutput);
            data.Write(ConditionActive);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            Mode = (SensorMode)reader.ReadByte();
            ThresholdPct = reader.ReadByte();
            EnemyRange = reader.ReadInt16();
            LevelOutput = reader.ReadBoolean();
            ConditionActive = reader.ReadBoolean();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["_Mode"] = (byte)Mode;
            tag["_ThresholdPct"] = ThresholdPct;
            tag["_EnemyRange"] = EnemyRange;
            tag["_LevelOutput"] = LevelOutput;
            tag["_ConditionActive"] = ConditionActive;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (tag.TryGet("_Mode", out byte mode) && mode <= (byte)SensorMode.Invasion) {
                Mode = (SensorMode)mode;
            }
            if (tag.TryGet("_ThresholdPct", out byte pct)) {
                ThresholdPct = Math.Clamp(pct, (byte)5, (byte)95);
            }
            if (tag.TryGet("_EnemyRange", out short range)) {
                EnemyRange = range;
            }
            if (tag.TryGet("_LevelOutput", out bool level)) {
                LevelOutput = level;
            }
            if (tag.TryGet("_ConditionActive", out bool active)) {
                ConditionActive = active;
            }
        }
        #endregion

        #region 判定
        public override void UpdateMachine() {
            //判定与发信仅权威端;客户端状态经包同步,表现全在 Draw 的绘制帧推进
            if (VaultUtils.isClient) {
                return;
            }

            if (Mode == SensorMode.Off) {
                if (ConditionActive) {
                    //关闭时静默复位,不发离开脉冲:改配置不该误触发机关
                    ConditionActive = false;
                    SendData();
                }
                return;
            }

            if (MachineData.UEvalue < StandbyCost) {
                //缺电:判定暂停,状态冻结,不产生假边沿
                return;
            }
            MachineData.UEvalue -= StandbyCost;

            bool raw = EvaluateCondition();
            if (raw != ConditionActive) {
                ConditionActive = raw;
                //上升沿必发;下降沿仅在电平跟随模式补发,使翻转器件回到原态
                if (raw || LevelOutput) {
                    EmitPulse();
                }
                SendData();
            }
        }

        private bool EvaluateCondition() {
            switch (Mode) {
                case SensorMode.ChargeAbove: {
                    if (!TryReadGridCharge(out float ratio)) {
                        return false;
                    }
                    float pct = ThresholdPct / 100f;
                    //迟滞:激活后须回撤出带宽才退出
                    return ConditionActive ? ratio >= pct - ThresholdBand : ratio >= pct;
                }
                case SensorMode.ChargeBelow: {
                    if (!TryReadGridCharge(out float ratio)) {
                        return false;
                    }
                    float pct = ThresholdPct / 100f;
                    return ConditionActive ? ratio <= pct + ThresholdBand : ratio <= pct;
                }
                case SensorMode.Enemy: {
                    //索敌口径与防御塔同源(CanBeChasedBy 过滤,雷达默认无视遮挡)
                    bool found = CenterInWorld.FindClosestNPC(EnemyRange) != null;
                    if (found) {
                        enemyOffTicks = 0;
                        return true;
                    }
                    if (ConditionActive && ++enemyOffTicks < EnemyOffDebounce) {
                        return true;//退出去抖:敌怪走位闪烁不抖动信号
                    }
                    return false;
                }
                case SensorMode.BloodMoon:
                    return Main.bloodMoon;
                case SensorMode.Eclipse:
                    return Main.eclipse;
                case SensorMode.SlimeRain:
                    return Main.slimeRain;
                case SensorMode.Invasion:
                    return Main.invasionType > 0 && Main.invasionSize > 0;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 聚合外缘邻接 TP 的充能率,跨格大机器 TopLeft 归一去重。
        /// 管道计入:管道充盈度即电网压力的近似,贴管道测网压、贴电池测储能;
        /// 控制件(接口器/总闸/传感器)缓冲无网格意义,剔除。
        /// 同岛并行分组(基类 CollectGroupLinks 声明外圈)保证邻接读取安全
        /// </summary>
        private bool TryReadGridCharge(out float ratio) {
            neighborCache.Clear();

            int tileWidth = Width / 16;
            int tileHeight = Height / 16;
            for (int i = Position.X; i < Position.X + tileWidth; i++) {
                CollectNeighbor(new Point16(i, Position.Y - 1));
                CollectNeighbor(new Point16(i, Position.Y + tileHeight));
            }
            for (int j = Position.Y; j < Position.Y + tileHeight; j++) {
                CollectNeighbor(new Point16(Position.X - 1, j));
                CollectNeighbor(new Point16(Position.X + tileWidth, j));
            }

            float ue = 0f;
            float max = 0f;
            foreach (var machine in neighborCache) {
                ue += machine.MachineData.UEvalue;
                max += machine.MaxUEValue;
            }
            ratio = max > 0f ? ue / max : 0f;
            return max > 0f;
        }

        private void CollectNeighbor(Point16 point) {
            if (!Framing.GetTileSafely(point).HasTile) {
                return;
            }
            if (!VaultUtils.SafeGetTopLeft(point, out var topLeft)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(topLeft, out var tp)) {
                return;
            }
            if (tp is not MachineTP machine || !machine.Active || machine.MachineData == null) {
                return;
            }
            if (machine is WireInterfaceTP or GridSwitchTP or SensorTP) {
                return;
            }
            neighborCache.Add(machine);
        }

        private void EmitPulse() {
            Point16 pos = Position;
            //TripWire 操作全局布线状态,并行阶段延后主线程执行(串行阶段立即执行);
            //多人客户端内它直接返回,权威门已在 UpdateMachine 开头
            Defer(() => Wiring.TripWire(pos.X, pos.Y, 1, 2));
        }
        #endregion

        #region 纯客户端表现:绘制帧推进,不入包不存档
        /// <summary>上一绘制帧所见条件判定,null=尚未见过</summary>
        private bool? lastConditionVisual;
        /// <summary>上一绘制帧所见条件模式:UI 换条件会静默复位判定,同帧模式变化不算真边沿</summary>
        private SensorMode lastModeVisual;
        /// <summary>上一次绘制帧号,用于陈旧检测</summary>
        private uint lastVisualFrame;
        /// <summary>触发爆闪包络</summary>
        private float eyeFlash;
        /// <summary>警戒扫描线方位角</summary>
        private float sweepAngle;

        private Vector2 EyeCenterWorld => PosInWorld + new Vector2(8f, 7f);

        /// <summary>
        /// 绘制帧表现推进。边沿源:包内 ConditionActive 字段(权威端判定后 SendData,
        /// 本地单人当帧检出,远端收包后下一绘制帧检出)——上升沿爆闪+火花+沿线流光;
        /// 下降沿仅电平跟随模式补一记回落闪(那才真的又发了脉冲)。
        /// Mode 同帧变化=改配置的静默复位而非真脉冲,抑制反馈;
        /// 屏外错过的变化按陈旧检测静默重同步
        /// </summary>
        private void UpdateEyeEnvelope() {
            uint frame = MachineStandbyFX.DrawFrame;
            bool fresh = lastConditionVisual != null && frame - lastVisualFrame <= MachineStandbyFX.StaleFrameGap
                && Mode == lastModeVisual;
            if (fresh && lastConditionVisual != ConditionActive) {
                bool rising = ConditionActive;
                if (rising) {
                    eyeFlash = 1f;
                    Vector2 eye = EyeCenterWorld;
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.38f, Pitch = 0.2f }, eye);
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(eye, VaultUtils.RandVr(2.4f), ModeColor(),
                            Main.rand.NextFloat(0.28f, 0.4f))?.Configure(true, Main.rand.Next(14, 22));
                    }
                }
                else {
                    eyeFlash = MathF.Max(eyeFlash, 0.45f);
                }
                if (rising || LevelOutput) {
                    CtrlWireFX.EmitWirePulse(this, ModeColor());
                }
            }
            lastConditionVisual = ConditionActive;
            lastModeVisual = Mode;
            lastVisualFrame = frame;

            eyeFlash *= 0.88f;

            //眼灯包络:电平跟随成立=常亮 / 单次脉冲成立=中亮慢脉冲(已发过,等退出重臂)
            //待命=微亮 / 缺电、关闭、待机=熄灭
            float target;
            if (!Powered || Disabled) {
                target = 0f;
            }
            else if (ConditionActive) {
                target = LevelOutput ? 1f : 0.55f + 0.10f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
            }
            else {
                target = 0.35f;
            }
            EyeGlow = MathHelper.Lerp(EyeGlow, target, 0.12f);

            //警戒扫描角推进:有敌加速;同时把绘制剔除边距扩到警戒半径,杆体出屏时扫描线仍可见
            if (Powered && !Disabled && Mode == SensorMode.Enemy) {
                sweepAngle += ConditionActive ? 0.052f : 0.020f;
                if (sweepAngle > MathHelper.TwoPi) {
                    sweepAngle -= MathHelper.TwoPi;
                }
                DrawExtendMode = EnemyRange + 140;
            }
            else {
                DrawExtendMode = 160;
            }
        }
        #endregion

        #region 交互与绘制
        public void OpenUI() {
            var ui = UIHandleLoader.GetUIHandleOfType<SensorUI>();
            ui?.Interactive(this);
        }

        internal Color ModeColor() => Mode switch {
            SensorMode.ChargeAbove => new Color(110, 220, 130),
            SensorMode.ChargeBelow => new Color(240, 150, 70),
            SensorMode.Enemy => new Color(235, 84, 74),
            SensorMode.BloodMoon => new Color(200, 46, 66),
            SensorMode.Eclipse => new Color(235, 190, 82),
            SensorMode.SlimeRain => new Color(92, 176, 230),
            SensorMode.Invasion => new Color(186, 108, 228),
            _ => new Color(150, 150, 158),
        };

        internal string ModeText() => Mode switch {
            SensorMode.ChargeAbove => Sensor.ModeChargeAboveText.Value,
            SensorMode.ChargeBelow => Sensor.ModeChargeBelowText.Value,
            SensorMode.Enemy => Sensor.ModeEnemyText.Value,
            SensorMode.BloodMoon => Sensor.ModeBloodMoonText.Value,
            SensorMode.Eclipse => Sensor.ModeEclipseText.Value,
            SensorMode.SlimeRain => Sensor.ModeSlimeRainText.Value,
            SensorMode.Invasion => Sensor.ModeInvasionText.Value,
            _ => Sensor.ModeOffText.Value,
        };

        /// <summary>程序化本体:落地感应杆+顶端电子眼;贴图后补,加载零资产</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            UpdateEyeEnvelope();

            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color light = Lighting.GetColor(Position.ToPoint());
            Rectangle src = new(0, 0, 1, 1);
            int x = (int)drawPos.X;
            int y = (int)drawPos.Y;

            //基脚与立杆
            spriteBatch.Draw(px, new Rectangle(x + 1, y + 28, 14, 4), src, new Color(38, 37, 42).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x + 1, y + 28, 14, 1), src, new Color(52, 50, 58).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x + 5, y + 12, 6, 16), src, new Color(52, 50, 56).MultiplyRGB(light));
            //立杆受光棱线与检修螺栓
            spriteBatch.Draw(px, new Rectangle(x + 5, y + 12, 1, 16), src, new Color(66, 64, 72).MultiplyRGB(light));
            Color bolt = new Color(72, 70, 80).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(x + 6, y + 16, 4, 1), src, bolt);
            spriteBatch.Draw(px, new Rectangle(x + 6, y + 23, 4, 1), src, bolt);

            //感应头外壳与眼窝
            spriteBatch.Draw(px, new Rectangle(x + 2, y + 1, 12, 12), src, new Color(58, 56, 63).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x + 2, y + 1, 12, 1), src, new Color(76, 74, 84).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x + 4, y + 3, 8, 8), src, new Color(22, 22, 26).MultiplyRGB(light));
            //壳角铆钉
            Color rivet = new Color(84, 82, 92).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(x + 3, y + 2, 1, 1), src, rivet);
            spriteBatch.Draw(px, new Rectangle(x + 12, y + 2, 1, 1), src, rivet);

            //电子眼:模式色;缺电/待机熄灭只剩暗镜面,通电待命微亮,条件成立按输出方式常亮/脉冲
            float flicker = 0.85f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f + Position.Y * 0.9f) * 0.15f;
            float glowFloor = Powered && !Disabled ? 0.18f : 0.05f;
            Color eye = ModeColor() * ((glowFloor + EyeGlow * (1f - glowFloor)) * flicker + eyeFlash * 0.9f);
            eye.A = 255;
            spriteBatch.Draw(px, new Rectangle(x + 5, y + 4, 6, 6), src, eye);
            //高光点
            if (EyeGlow > 0.5f || eyeFlash > 0.35f) {
                spriteBatch.Draw(px, new Rectangle(x + 6, y + 5, 2, 2), src,
                    Color.White * MathHelper.Clamp(EyeGlow * 0.8f + eyeFlash * 0.6f, 0f, 1f));
            }
            //触发爆闪的加色光晕
            if (eyeFlash > 0.08f && CWRAsset.SoftGlow?.Value is Texture2D glowTex) {
                Color c = ModeColor();
                spriteBatch.Draw(glowTex, drawPos + new Vector2(8f, 7f), null,
                    new Color(c.R, c.G, c.B, 0) * (eyeFlash * 0.85f), 0f,
                    glowTex.Size() * 0.5f, 0.3f + eyeFlash * 0.5f, SpriteEffects.None, 0f);
            }

            //警戒模式:通电才有雷达扫描线;半径示意环只要悬停就给(布放规划要用,不吃电)
            if (Mode == SensorMode.Enemy) {
                if (Powered && !Disabled) {
                    DrawScanSweep(spriteBatch);
                }
                if (HoverTP) {
                    DrawRangeRing(spriteBatch);
                }
            }
        }

        /// <summary>警戒扫描:主针+两道残针的雷达扫掠,半径即真实警戒半径;低调加色,屏外段剔除</summary>
        private void DrawScanSweep(SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 eyeScreen = EyeCenterWorld - Main.screenPosition;
            Color c = ModeColor();
            float baseAlpha = ConditionActive ? 0.20f : 0.10f;
            const int Segments = 10;
            float segLen = EnemyRange / (float)Segments;
            Rectangle screen = new(-80, -80, Main.screenWidth + 160, Main.screenHeight + 160);

            for (int ghost = 0; ghost < 3; ghost++) {
                float angle = sweepAngle - ghost * 0.075f;
                float ghostAlpha = baseAlpha * (ghost == 0 ? 1f : ghost == 1 ? 0.45f : 0.2f);
                Vector2 dir = angle.ToRotationVector2();
                for (int i = 0; i < Segments; i++) {
                    Vector2 start = eyeScreen + dir * (segLen * i + 6f);
                    Vector2 mid = start + dir * segLen * 0.5f;
                    if (!screen.Contains((int)mid.X, (int)mid.Y)) {
                        continue;
                    }
                    float fall = 1f - i / (float)Segments * 0.85f;
                    spriteBatch.Draw(px, start, src, new Color(c.R, c.G, c.B, 0) * (ghostAlpha * fall),
                        angle, new Vector2(0f, 0.5f), new Vector2(segLen + 1f, 1.2f), SpriteEffects.None, 0f);
                }
            }

            //扫描针端点亮标
            Vector2 tip = eyeScreen + sweepAngle.ToRotationVector2() * EnemyRange;
            if (screen.Contains((int)tip.X, (int)tip.Y)) {
                spriteBatch.Draw(px, tip, src, new Color(c.R, c.G, c.B, 0) * (baseAlpha * 1.6f),
                    sweepAngle, new Vector2(0.5f, 0.5f), new Vector2(3f, 3f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>悬停时的警戒半径虚线环</summary>
        private void DrawRangeRing(SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 eyeScreen = EyeCenterWorld - Main.screenPosition;
            Color c = ModeColor();
            Rectangle screen = new(-40, -40, Main.screenWidth + 80, Main.screenHeight + 80);
            const int Dashes = 40;
            float spin = Main.GlobalTimeWrappedHourly * 0.15f;
            for (int i = 0; i < Dashes; i++) {
                float angle = MathHelper.TwoPi * i / Dashes + spin;
                Vector2 pos = eyeScreen + angle.ToRotationVector2() * EnemyRange;
                if (!screen.Contains((int)pos.X, (int)pos.Y)) {
                    continue;
                }
                spriteBatch.Draw(px, pos, src, new Color(c.R, c.G, c.B, 0) * 0.24f,
                    angle + MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(1.2f, 7f), SpriteEffects.None, 0f);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
        #endregion
    }
}
