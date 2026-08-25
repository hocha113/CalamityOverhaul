using CalamityOverhaul.Content.Industrials.ElectricPowers.GridSwitches;
using CalamityOverhaul.Content.Industrials.ElectricPowers.WireInterfaces;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
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
            //判定与发信仅权威端;客户端状态经包同步,只推眼灯表现
            if (VaultUtils.isClient) {
                UpdateEyeVisual();
                return;
            }

            if (Mode == SensorMode.Off) {
                if (ConditionActive) {
                    //关闭时静默复位,不发离开脉冲:改配置不该误触发机关
                    ConditionActive = false;
                    SendData();
                }
                UpdateEyeVisual();
                return;
            }

            if (MachineData.UEvalue < StandbyCost) {
                //缺电:判定暂停,状态冻结,不产生假边沿
                UpdateEyeVisual();
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

            UpdateEyeVisual();
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

        private void UpdateEyeVisual() {
            float target = !Powered ? 0f : (ConditionActive ? 1f : 0.35f);
            EyeGlow = MathHelper.Lerp(EyeGlow, target, 0.12f);
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
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color light = Lighting.GetColor(Position.ToPoint());
            Rectangle src = new(0, 0, 1, 1);
            int x = (int)drawPos.X;
            int y = (int)drawPos.Y;

            //基脚与立杆
            spriteBatch.Draw(px, new Rectangle(x + 1, y + 28, 14, 4), src, new Color(38, 37, 42).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x + 5, y + 12, 6, 16), src, new Color(52, 50, 56).MultiplyRGB(light));

            //感应头外壳与眼窝
            spriteBatch.Draw(px, new Rectangle(x + 2, y + 1, 12, 12), src, new Color(58, 56, 63).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x + 4, y + 3, 8, 8), src, new Color(22, 22, 26).MultiplyRGB(light));

            //电子眼:模式色,条件成立满亮,缺电熄灭;轻微呼吸
            float flicker = 0.85f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f + Position.Y * 0.9f) * 0.15f;
            Color eye = ModeColor() * ((0.18f + EyeGlow * 0.82f) * flicker);
            eye.A = 255;
            spriteBatch.Draw(px, new Rectangle(x + 5, y + 4, 6, 6), src, eye);
            //高光点
            if (EyeGlow > 0.5f) {
                spriteBatch.Draw(px, new Rectangle(x + 6, y + 5, 2, 2), src, Color.White * (EyeGlow * 0.8f));
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
        #endregion
    }
}
