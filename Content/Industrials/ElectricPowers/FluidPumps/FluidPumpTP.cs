using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids;
using Microsoft.Xna.Framework.Graphics;
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

        public override void UpdateMachine() {
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
