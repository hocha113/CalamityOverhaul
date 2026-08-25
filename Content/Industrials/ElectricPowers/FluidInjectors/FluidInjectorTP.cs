using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.FluidInjectors
{
    /// <summary>
    /// 灌注机TP:反向泵,按节拍消耗储液向机身正下方的世界空间放液。
    /// 放液沿竖直探杆自上而下找第一个可放格,遇实心块或异种液体即停;
    /// 世界改动仅权威端(主线程经 Defer),放液走原版 sendWater 同步。
    /// 岩浆放置与原版岩浆桶同权,不引入额外许可系统
    /// </summary>
    internal class FluidInjectorTP : BaseBattery, IFluidContainer
    {
        public override int TargetTileID => ModContent.TileType<FluidInjectorTile>();
        public override int TargetItem => ModContent.ItemType<FluidInjector>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500;

        #region 液体容器契约
        public int FluidType { get; set; }
        public int FluidAmount { get; set; }
        public int FluidCapacity => 4 * FluidHelper.UnitsPerTile;
        public FluidNetRole FluidRole => FluidNetRole.Consumer;
        public bool CanAcceptFluid(int liquidId) => FluidHelper.DefaultCanAccept(this, liquidId);
        #endregion

        /// <summary>放置一整格液体的电费,补格按比例折算</summary>
        internal const float InjectCostPerTile = 2f;
        /// <summary>作业节拍(帧)</summary>
        internal const int BeatTicks = 30;
        /// <summary>放液探杆向下深度(格)</summary>
        private const int ScanDepth = 8;

        private int beatTimer;

        public override void UpdateMachine() {
            //作业与世界改动仅权威端
            if (VaultUtils.isClient) {
                return;
            }
            if (++beatTimer < BeatTicks) {
                return;
            }
            beatTimer = 0;

            if (MachineData.UEvalue < InjectCostPerTile || FluidAmount <= 0) {
                return;
            }

            //放液点扫描与世界写入都在主线程做(并行阶段经 Defer 延后,串行阶段立即执行)
            Defer(() => {
                if (MachineData.UEvalue < InjectCostPerTile || FluidAmount <= 0) {
                    return;
                }

                int tileWidth = Width / 16;
                int tileHeight = Height / 16;
                int top = Position.Y + tileHeight;
                int bottom = top + ScanDepth - 1;

                //每列一根探杆,被实心块或异种液体挡住的列不再向深处灌(不穿墙不混液)
                bool[] blocked = new bool[tileWidth];
                for (int y = top; y <= bottom; y++) {
                    for (int xi = 0; xi < tileWidth; xi++) {
                        if (blocked[xi]) {
                            continue;
                        }
                        int x = Position.X + xi;
                        if (!WorldGen.InWorld(x, y, 40)) {
                            blocked[xi] = true;
                            continue;
                        }
                        Tile tile = Framing.GetTileSafely(x, y);
                        if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                            blocked[xi] = true;
                            continue;
                        }
                        if (tile.LiquidAmount > 0 && tile.LiquidType != FluidType) {
                            blocked[xi] = true;
                            continue;
                        }
                        //已满同种格跳过,向更深处灌
                        if (tile.LiquidAmount >= byte.MaxValue) {
                            continue;
                        }

                        int need = byte.MaxValue - tile.LiquidAmount;
                        if (FluidAmount < need) {
                            //储液不足一格补量,等下个节拍
                            return;
                        }

                        FluidAmount -= need;
                        MachineData.UEvalue -= InjectCostPerTile * need / FluidHelper.UnitsPerTile;

                        tile.LiquidType = FluidType;
                        tile.LiquidAmount = byte.MaxValue;
                        WorldGen.SquareTileFrame(x, y);
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
