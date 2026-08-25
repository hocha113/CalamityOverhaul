using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.BottlingMachines
{
    /// <summary>瓶装机转换表:装瓶(空容器+储液→成品)与倒空(满容器→储液+空容器)</summary>
    internal static class BottlingRecipes
    {
        /// <summary>装瓶行:该液型每件消耗 Units 单位,产出 ResultType</summary>
        internal readonly record struct FillRecipe(int LiquidType, int Units, int ResultType);
        /// <summary>倒空行:每件回收 Units 单位该液型,返还 ReturnType</summary>
        internal readonly record struct DrainRecipe(int LiquidType, int Units, int ReturnType);

        /// <summary>空容器可装的液型候选;微光无对应物品,按设计不可瓶装</summary>
        internal static readonly Dictionary<int, FillRecipe[]> FillTable = new() {
            [ItemID.Bottle] = [
                new FillRecipe(LiquidID.Water, 25, ItemID.BottledWater),
                new FillRecipe(LiquidID.Honey, 25, ItemID.BottledHoney),
            ],
            [ItemID.EmptyBucket] = [
                new FillRecipe(LiquidID.Water, FluidHelper.UnitsPerTile, ItemID.WaterBucket),
                new FillRecipe(LiquidID.Lava, FluidHelper.UnitsPerTile, ItemID.LavaBucket),
                new FillRecipe(LiquidID.Honey, FluidHelper.UnitsPerTile, ItemID.HoneyBucket),
            ],
        };

        internal static readonly Dictionary<int, DrainRecipe> DrainTable = new() {
            [ItemID.BottledWater] = new DrainRecipe(LiquidID.Water, 25, ItemID.Bottle),
            [ItemID.BottledHoney] = new DrainRecipe(LiquidID.Honey, 25, ItemID.Bottle),
            [ItemID.WaterBucket] = new DrainRecipe(LiquidID.Water, FluidHelper.UnitsPerTile, ItemID.EmptyBucket),
            [ItemID.LavaBucket] = new DrainRecipe(LiquidID.Lava, FluidHelper.UnitsPerTile, ItemID.EmptyBucket),
            [ItemID.HoneyBucket] = new DrainRecipe(LiquidID.Honey, FluidHelper.UnitsPerTile, ItemID.EmptyBucket),
        };

        internal static bool CanProcess(Item item) {
            if (item == null || item.IsAir) {
                return false;
            }
            return FillTable.ContainsKey(item.type) || DrainTable.ContainsKey(item.type);
        }
    }

    /// <summary>
    /// 瓶装机TP:输入槽放空瓶/空桶时抽储液装满,放整瓶/整桶时倒空进储液,成品入输出槽。
    /// 作业只在权威端推进并结算,槽位变化以事件包推给客户端;
    /// 输入输出槽经 StorageProvider 对接物品管道
    /// </summary>
    internal class BottlingMachineTP : BaseBattery, IFluidContainer
    {
        public override int TargetTileID => ModContent.TileType<BottlingMachineTile>();
        public override int TargetItem => ModContent.ItemType<BottlingMachine>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500;

        #region 液体容器契约
        public int FluidType { get; set; }
        public int FluidAmount { get; set; }
        public int FluidCapacity => 4 * FluidHelper.UnitsPerTile;
        public FluidNetRole FluidRole => FluidNetRole.Consumer;
        public bool CanAcceptFluid(int liquidId) => FluidHelper.DefaultCanAccept(this, liquidId);
        #endregion

        /// <summary>单次作业电费</summary>
        internal const float JobCostUE = 5f;
        /// <summary>作业节拍(帧)</summary>
        internal const int BeatTicks = 60;

        /// <summary>待处理容器槽</summary>
        internal Item InputItem = new Item();
        /// <summary>成品槽</summary>
        internal Item OutputItem = new Item();

        private int jobTimer;

        public override void UpdateMachine() {
            //作业仅权威端推进,客户端槽位与液量等事件包
            if (VaultUtils.isClient) {
                return;
            }
            if (++jobTimer < BeatTicks) {
                return;
            }
            jobTimer = 0;

            if (MachineData.UEvalue < JobCostUE || InputItem == null || InputItem.IsAir) {
                return;
            }

            if (BottlingRecipes.DrainTable.TryGetValue(InputItem.type, out var drain)) {
                TryDrainJob(drain);
            }
            else if (BottlingRecipes.FillTable.TryGetValue(InputItem.type, out var fills)) {
                TryFillJob(fills);
            }
        }

        /// <summary>输出槽可否再收一件该物品</summary>
        private bool OutputCanTake(int itemType) {
            if (OutputItem == null || OutputItem.IsAir) {
                return true;
            }
            return OutputItem.type == itemType && OutputItem.stack < OutputItem.maxStack;
        }

        private void PushOutput(int itemType) {
            if (OutputItem == null || OutputItem.IsAir) {
                OutputItem = new Item(itemType);
            }
            else {
                OutputItem.stack++;
            }
        }

        private void ConsumeOneInput() {
            InputItem.stack--;
            if (InputItem.stack <= 0) {
                InputItem.TurnToAir();
            }
        }

        /// <summary>倒空:满容器的液体回收进储液,返还空容器</summary>
        private void TryDrainJob(BottlingRecipes.DrainRecipe drain) {
            if (!CanAcceptFluid(drain.LiquidType)) {
                return;
            }
            if (FluidCapacity - FluidAmount < drain.Units) {
                return;
            }
            if (!OutputCanTake(drain.ReturnType)) {
                return;
            }

            if (FluidAmount <= 0) {
                FluidType = drain.LiquidType;
            }
            FluidAmount += drain.Units;
            MachineData.UEvalue -= JobCostUE;
            ConsumeOneInput();
            PushOutput(drain.ReturnType);
            SendData();
        }

        /// <summary>装瓶:按储液类型匹配候选行,装满一件空容器</summary>
        private void TryFillJob(BottlingRecipes.FillRecipe[] fills) {
            if (FluidAmount <= 0) {
                return;
            }
            foreach (var fill in fills) {
                if (fill.LiquidType != FluidType || FluidAmount < fill.Units) {
                    continue;
                }
                if (!OutputCanTake(fill.ResultType)) {
                    continue;
                }

                FluidAmount -= fill.Units;
                MachineData.UEvalue -= JobCostUE;
                ConsumeOneInput();
                PushOutput(fill.ResultType);
                SendData();
                return;
            }
        }

        /// <summary>右键交互(交互客户端执行):放入可处理容器/空手取成品/Shift 全取</summary>
        public void RightClickByTile() {
            Item item = Main.LocalPlayer.GetItem();

            if (Main.keyState.PressingShift()) {
                //Shift 全部取出,直接入背包(MP 下地面掉落会被队友截走)
                if (InputItem != null && !InputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), InputItem.Clone());
                    InputItem.TurnToAir();
                }
                if (OutputItem != null && !OutputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), OutputItem.Clone());
                    OutputItem.TurnToAir();
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            //手持可处理容器:放入输入槽(空槽/同型堆叠/异型不动)
            if (BottlingRecipes.CanProcess(item)) {
                if (InputItem == null || InputItem.IsAir) {
                    InputItem = item.Clone();
                    item.TurnToAir();
                }
                else if (InputItem.type == item.type) {
                    int space = InputItem.maxStack - InputItem.stack;
                    int transfer = System.Math.Min(space, item.stack);
                    InputItem.stack += transfer;
                    item.stack -= transfer;
                    if (item.stack <= 0) {
                        item.TurnToAir();
                    }
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            //空手:取出成品
            if (item.IsAir && OutputItem != null && !OutputItem.IsAir) {
                Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), OutputItem.Clone());
                OutputItem.TurnToAir();
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        public override void MachineKill() {
            //槽内物品随拆机倒出(权威端)
            if (!VaultUtils.isClient) {
                if (InputItem != null && !InputItem.IsAir) {
                    DropItem(InputItem.Clone());
                }
                if (OutputItem != null && !OutputItem.IsAir) {
                    DropItem(OutputItem.Clone());
                }
            }
            InputItem?.TurnToAir();
            OutputItem?.TurnToAir();
        }

        #region 存档与同步:液体与槽位追加在基类之后
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write((byte)FluidType);
            data.Write(FluidAmount);
            ItemIO.Send(InputItem ?? new Item(), data, true, true);
            ItemIO.Send(OutputItem ?? new Item(), data, true, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            FluidType = reader.ReadByte();
            FluidAmount = reader.ReadInt32();
            InputItem = ItemIO.Receive(reader, true, true);
            OutputItem = ItemIO.Receive(reader, true, true);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["FluidType"] = FluidType;
            tag["FluidAmount"] = FluidAmount;
            if (InputItem != null && !InputItem.IsAir) {
                tag["Bottling_InputItem"] = ItemIO.Save(InputItem);
            }
            if (OutputItem != null && !OutputItem.IsAir) {
                tag["Bottling_OutputItem"] = ItemIO.Save(OutputItem);
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            FluidType = tag.TryGet("FluidType", out int type) ? type : LiquidID.Water;
            FluidAmount = tag.TryGet("FluidAmount", out int amount) ? amount : 0;
            InputItem = CWRSaveData.LoadItemFromTag(tag, "Bottling_InputItem", nameof(BottlingMachineTP));
            OutputItem = CWRSaveData.LoadItemFromTag(tag, "Bottling_OutputItem", nameof(BottlingMachineTP));
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
