using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.FluidPumps
{
    /// <summary>
    /// 抽液泵TP:通电后按节拍抽取机身下方区域的世界液体入内部缓冲,液管从中抽走。
    /// 世界液体的读改仅权威端执行(主线程经 Defer),改动后走原版 sendWater 同步;
    /// 缓冲一次只锁一种液体,排空后才改抽其他液体
    /// </summary>
    internal class FluidPumpTP : BaseBattery, IFluidContainer
    {
        public override int TargetTileID => ModContent.TileType<FluidPumpTile>();
        public override int TargetItem => ModContent.ItemType<FluidPump>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500;

        #region 液体容器契约
        public int FluidType { get; set; }
        public int FluidAmount { get; set; }
        public int FluidCapacity => 4 * FluidHelper.UnitsPerTile;
        public FluidNetRole FluidRole => FluidNetRole.Source;
        public bool CanAcceptFluid(int liquidId) => FluidHelper.DefaultCanAccept(this, liquidId);
        #endregion

        /// <summary>抽取一整格液体的电费,部分格按比例折算</summary>
        internal const float PumpCostPerTile = 2f;
        /// <summary>作业节拍(帧)</summary>
        internal const int BeatTicks = 30;
        /// <summary>扫描区向下探测深度(格)</summary>
        private const int ScanDepth = 8;

        private int beatTimer;

        #region 纯客户端表现状态(活塞/状态灯/入水口)
        /// <summary>运转程度 0..1:条件齐备缓升,缺一缓停(活塞减速停摆)</summary>
        private float runLevel;
        /// <summary>活塞相位(弧度),速度随 runLevel</summary>
        private float pistonPhase;
        /// <summary>抽液瞬间脉冲(活塞猛推一拍)</summary>
        private float gulpPulse;
        /// <summary>入水口(被抽液面)世界坐标,x<0=没找到</summary>
        private Vector2 intakePos = new(-1f, -1f);
        private int lastFluidAmountVis = -1;
        private int intakeScanTimer;
        #endregion

        public override void UpdateMachine() {
            if (!Main.dedServ) {
                UpdatePumpVisual();
            }

            //作业与世界改动仅权威端;客户端液量靠事件包与管网均衡自愈
            if (VaultUtils.isClient) {
                return;
            }
            if (++beatTimer < BeatTicks) {
                return;
            }
            beatTimer = 0;

            if (MachineData.UEvalue < PumpCostPerTile || FluidAmount >= FluidCapacity) {
                return;
            }

            //扫描与液体写入都在主线程做(并行阶段经 Defer 延后,串行阶段立即执行)
            Defer(() => {
                if (MachineData.UEvalue < PumpCostPerTile || FluidAmount >= FluidCapacity) {
                    return;
                }

                int tileWidth = Width / 16;
                int tileHeight = Height / 16;
                int left = Position.X - 1;
                int right = Position.X + tileWidth;
                int top = Position.Y;
                int bottom = Position.Y + tileHeight + ScanDepth - 1;

                //自上而下先抽液面
                for (int y = top; y <= bottom; y++) {
                    for (int x = left; x <= right; x++) {
                        if (!WorldGen.InWorld(x, y, 40)) {
                            continue;
                        }
                        Tile tile = Framing.GetTileSafely(x, y);
                        if (tile.LiquidAmount <= 0) {
                            continue;
                        }
                        //类型锁:缓冲非空只抽同型
                        if (FluidAmount > 0 && tile.LiquidType != FluidType) {
                            continue;
                        }

                        int room = FluidCapacity - FluidAmount;
                        int taken = System.Math.Min(tile.LiquidAmount, room);
                        if (taken <= 0) {
                            continue;
                        }

                        if (FluidAmount <= 0) {
                            FluidType = tile.LiquidType;
                        }
                        FluidAmount += taken;
                        MachineData.UEvalue -= PumpCostPerTile * taken / FluidHelper.UnitsPerTile;

                        tile.LiquidAmount -= (byte)taken;
                        if (tile.LiquidAmount == 0) {
                            //排空的格子液型复位为水,对齐原版空桶行为
                            tile.LiquidType = LiquidID.Water;
                        }
                        WorldGen.SquareTileFrame(x, y, false);
                        if (VaultUtils.isServer) {
                            NetMessage.sendWater(x, y);
                        }

                        //事件推送:客户端立刻拿到新液量
                        SendData();
                        return;
                    }
                }
            });
        }

        #region 表现推进(纯客户端,零网络)
        /// <summary>
        /// 运转状态由真实条件推导(电够+未满+下方有可抽液+未待机),
        /// 抽液瞬间由"观测到液量上升"触发猛推与入水口涟漪——事件包是各端的真实作业信号
        /// </summary>
        private void UpdatePumpVisual() {
            //入水口节流扫描(只读,并行安全):自上而下第一格可抽液体的液面
            if (--intakeScanTimer <= 0) {
                intakeScanTimer = 30;
                ScanIntakeSurface();
            }

            bool working = !Disabled && MachineData.UEvalue >= PumpCostPerTile
                && FluidAmount < FluidCapacity && intakePos.X > 0f;
            runLevel = MathHelper.Lerp(runLevel, working ? 1f : 0f, working ? 0.10f : 0.05f);

            //观测抽液:液量上升=一次入腹
            if (lastFluidAmountVis < 0) {
                lastFluidAmountVis = FluidAmount;
            }
            bool gulped = FluidAmount > lastFluidAmountVis;
            lastFluidAmountVis = FluidAmount;
            if (gulped) {
                gulpPulse = 1f;
            }
            gulpPulse *= 0.9f;

            //活塞:速度=运转程度,入腹瞬间猛推一拍
            pistonPhase += runLevel * 0.16f + gulpPulse * 0.22f;

            //入水口反馈:涟漪+被吸向机底的水珠
            if (gulped && intakePos.X > 0f && FluidVFX.NearLocalPlayer(CenterInWorld)) {
                FluidStyle style = FluidVFX.GetStyle(FluidType);
                Vector2 surface = intakePos;
                Vector2 inletDir = (new Vector2(CenterInWorld.X, PosInWorld.Y + Height) - surface).SafeNormalize(-Vector2.UnitY);
                Defer(() => {
                    PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(surface, Vector2.Zero,
                        style.Bright * 0.7f, 1f)?.Configure(0.04f, 0.14f, 18);
                    for (int i = 0; i < 3; i++) {
                        Vector2 vel = inletDir * Main.rand.NextFloat(1.6f, 2.6f)
                            + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), 0f);
                        PRTLoader.NewParticle<PRT_HeartcarverDroplet>(surface + new Vector2(Main.rand.NextFloat(-4f, 4f), -1f),
                            vel, Color.Lerp(style.Main, style.Bright, Main.rand.NextFloat(0.4f)), Main.rand.NextFloat(0.45f, 0.7f))
                            ?.Configure(Main.rand.Next(12, 18), 0.04f);
                    }
                });
            }
        }

        /// <summary>扫描区内自上而下找第一格可抽液体,记其液面为入水口</summary>
        private void ScanIntakeSurface() {
            int tileWidth = Width / 16;
            int tileHeight = Height / 16;
            int left = Position.X - 1;
            int right = Position.X + tileWidth;
            int top = Position.Y;
            int bottom = Position.Y + tileHeight + 8 - 1;

            for (int y = top; y <= bottom; y++) {
                for (int x = left; x <= right; x++) {
                    if (!WorldGen.InWorld(x, y, 40)) {
                        continue;
                    }
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.LiquidAmount <= 0) {
                        continue;
                    }
                    if (FluidAmount > 0 && tile.LiquidType != FluidType) {
                        continue;
                    }
                    intakePos = new Vector2(x * 16f + 8f, y * 16f + (16f - tile.LiquidAmount / 255f * 16f));
                    return;
                }
            }
            intakePos = new Vector2(-1f, -1f);
        }
        #endregion

        #region 机面覆层:储液量规+状态灯(机身为贴图,活塞感由呼吸灯与抽液粒子承担)
        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 basePos = PosInWorld - Main.screenPosition;
            Color lit = Lighting.GetColor(Position.ToPoint());
            FluidStyle style = FluidVFX.GetStyle(FluidType);
            int topY = (int)basePos.Y;

            //左缘储液量规:底暗条+按液色的充盈段,贴着桶身左壁
            float ratio = MathHelper.Clamp(FluidAmount / (float)FluidCapacity, 0f, 1f);
            spriteBatch.Draw(px, new Rectangle((int)basePos.X + 7, topY + 7, 2, 9), new Color(18, 20, 24).MultiplyRGB(lit));
            if (ratio > 0.01f) {
                int fillH = (int)(9 * ratio);
                spriteBatch.Draw(px, new Rectangle((int)basePos.X + 7, topY + 7 + 9 - fillH, 2, fillH),
                    style.Main.MultiplyRGB(lit));
                spriteBatch.Draw(px, new Rectangle((int)basePos.X + 7, topY + 7 + 9 - fillH, 2, 1),
                    FluidVFX.Glow(style.Bright, 0.35f));
            }

            //状态灯:运转=青绿呼吸,通电待机=琥珀,断电/待机=熄灭
            Color lamp;
            if (Disabled || MachineData.UEvalue < PumpCostPerTile) {
                lamp = new Color(30, 26, 24);
            }
            else if (runLevel > 0.3f) {
                float breath = 0.6f + 0.4f * MathF.Sin(pistonPhase * 0.5f);
                lamp = FluidVFX.Glow(new Color(90, 255, 170), 0.5f + 0.5f * breath);
            }
            else {
                lamp = FluidVFX.Glow(new Color(255, 180, 60), 0.55f);
            }
            spriteBatch.Draw(px, new Rectangle((int)(basePos.X + Width) - 7, topY + 6, 2, 2), lamp);
        }
        #endregion

        #region 存档与同步:液体字段追加在基类之后
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write((byte)FluidType);
            data.Write(FluidAmount);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            FluidType = reader.ReadByte();
            FluidAmount = reader.ReadInt32();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["FluidType"] = FluidType;
            tag["FluidAmount"] = FluidAmount;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            FluidType = tag.TryGet("FluidType", out int type) ? type : LiquidID.Water;
            FluidAmount = tag.TryGet("FluidAmount", out int amount) ? amount : 0;
        }
        #endregion

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
            if (HoverTP) {
                FluidHelper.DrawFluidBar(this, this);
            }
        }
    }
}
