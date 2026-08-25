using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MachineModules;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Recyclers
{
    /// <summary>回收机槽位与进度</summary>
    internal class RecyclerData : MachineData
    {
        /// <summary>装备槽</summary>
        internal Item InputItem = new Item();
        /// <summary>锭料槽</summary>
        internal Item OutputItem = new Item();
        /// <summary>进度0..Max</summary>
        internal int RecycleProgress;
        /// <summary>完成所需进度</summary>
        internal int MaxRecycleProgress = 120;
        /// <summary>单tick耗电(单次作业共5UE均摊)</summary>
        internal float UEPerTick = 5f / 120f;
        /// <summary>电量上限</summary>
        internal float MaxUE = 500;
        internal bool IsWorking => RecycleProgress > 0 && UEvalue >= UEPerTick;

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(RecycleProgress);
            ItemIO.Send(InputItem ?? new Item(), data, true, true);
            ItemIO.Send(OutputItem ?? new Item(), data, true, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            RecycleProgress = reader.ReadInt32();
            InputItem = ItemIO.Receive(reader, true, true);
            OutputItem = ItemIO.Receive(reader, true, true);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["Recycler_RecycleProgress"] = RecycleProgress;
            if (InputItem != null && !InputItem.IsAir) {
                tag["Recycler_InputItem"] = ItemIO.Save(InputItem);
            }
            if (OutputItem != null && !OutputItem.IsAir) {
                tag["Recycler_OutputItem"] = ItemIO.Save(OutputItem);
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (!tag.TryGet("Recycler_RecycleProgress", out RecycleProgress)) {
                RecycleProgress = 0;
            }
            InputItem = CWRSaveData.LoadItemFromTag(tag, "Recycler_InputItem", nameof(RecyclerData));
            OutputItem = CWRSaveData.LoadItemFromTag(tag, "Recycler_OutputItem", nameof(RecyclerData));
        }
    }

    /// <summary>回收机TP:装备按稀有度拆解成锭,数量权威端掷骰</summary>
    internal class RecyclerTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<RecyclerTile>();
        public override int TargetItem => ModContent.ItemType<Recycler>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500 * ModuleRack.StorageMult;

        /// <summary>模块槽数</summary>
        internal const int ModuleSlotCount = 3;
        internal readonly MachineModuleRack ModuleRack = new(MachineModuleTarget.Recycler);

        internal RecyclerData RecData => MachineData as RecyclerData;
        /// <summary>拆解臂相位,瓦片绘制消费</summary>
        internal float armPhase;
        private int sparkTimer;
        /// <summary>自动进料节拍</summary>
        private int autoFeedTimer;

        public override MachineData GetGeneratorDataInds() => new RecyclerData {
            MaxUE = MaxUEValue,
        };

        public override void UpdateMachine() {
            ModuleRack.EnsureSlots(ModuleSlotCount);
            ModuleRack.Refresh();
            //储能扩容模块动上限,数据侧字段每帧对齐
            RecData.MaxUE = MaxUEValue;

            //自动进料斗:输入槽空了就从近旁存储抽可拆装备(权威端,主线程经 Defer)
            if (!VaultUtils.isClient && ModuleRack.AutoFeed && ++autoFeedTimer >= 30) {
                autoFeedTimer = 0;
                if (RecData.InputItem == null || RecData.InputItem.IsAir) {
                    Defer(() => {
                        if (RecData.InputItem != null && !RecData.InputItem.IsAir) {
                            return;
                        }
                        Item got = MachineLogistics.TryWithdraw(Position,
                            stored => RecyclerTables.CanRecycle(stored), 1);
                        if (!got.IsAir) {
                            RecData.InputItem = got;
                            SendData();
                        }
                    });
                }
            }

            //没有电量时停止工作
            if (RecData.UEvalue < RecData.UEPerTick) {
                armPhase = 0f;
                return;
            }

            //可否开始拆解
            if (RecData.RecycleProgress == 0 && CanStartRecycling()) {
                StartRecycling();
            }

            //执行拆解
            if (RecData.RecycleProgress > 0) {
                ProcessRecycling();
            }
            else {
                armPhase = 0f;
            }
        }

        private bool CanStartRecycling() {
            if (!RecyclerTables.CanRecycle(RecData.InputItem)) {
                return false;
            }

            //输出槽须为空或与预估锭种一致且留有余量(掷骰上浮 +1)
            if (RecData.OutputItem != null && !RecData.OutputItem.IsAir) {
                (int barType, int baseCount) = RecyclerTables.ResolveByRarity(RecData.InputItem.rare);
                if (RecData.OutputItem.type != barType) {
                    return false;
                }
                if (RecData.OutputItem.stack + baseCount + 1 > RecData.OutputItem.maxStack) {
                    return false;
                }
            }
            return true;
        }

        private void StartRecycling() {
            RecData.RecycleProgress = 1;
            if (!VaultUtils.isServer) {
                //并行阶段音效播放延迟到主线程执行(串行阶段立即执行)
                Defer(() => SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.5f, Pitch = -0.1f }, CenterInWorld));
            }
        }

        private void ProcessRecycling() {
            RecData.UEvalue -= RecData.UEPerTick;
            armPhase += 0.12f;

            //拆解火花
            if (!Main.dedServ && ++sparkTimer >= 8) {
                sparkTimer = 0;
                Vector2 sparkPos = CenterInWorld + new Vector2(Rand.NextFloat(-12f, 12f), -4f);
                float velX = Rand.NextFloat(-1.2f, 1.2f);
                //并行阶段Dust生成延迟到主线程执行(串行阶段立即执行)
                Defer(() => Dust.NewDust(sparkPos, 4, 4, DustID.Electric, velX, -1f, 100, default, 0.8f));
            }

            RecData.RecycleProgress++;
            if (RecData.RecycleProgress >= RecData.MaxRecycleProgress) {
                CompleteRecycling();
            }
        }

        private void CompleteRecycling() {
            //物品结算是权威端专属:客户端把进度停在满格等服务器的完成包
            if (VaultUtils.isClient) {
                RecData.RecycleProgress = RecData.MaxRecycleProgress;
                return;
            }

            if (!RecyclerTables.CanRecycle(RecData.InputItem)) {
                RecData.RecycleProgress = 0;
                return;
            }

            //权威掷骰:锭种确定性解析,数量 ±1 抖动 + 价值护栏
            RecyclerTables.RollOutput(RecData.InputItem, Rand, out int barType, out int count);

            //消耗一件装备
            RecData.InputItem.stack -= 1;
            if (RecData.InputItem.stack <= 0) {
                RecData.InputItem.TurnToAir();
            }

            //产出并入锭料槽;锭种不符(价值护栏降级)或溢出的部分落地,不吞物品
            int overflow = 0;
            if (RecData.OutputItem == null || RecData.OutputItem.IsAir) {
                RecData.OutputItem = new Item(barType, count);
            }
            else if (RecData.OutputItem.type == barType) {
                int space = RecData.OutputItem.maxStack - RecData.OutputItem.stack;
                int put = System.Math.Min(space, count);
                RecData.OutputItem.stack += put;
                overflow = count - put;
            }
            else {
                overflow = count;
            }
            if (overflow > 0) {
                DropItem(new Item(barType, overflow));
            }

            RecData.RecycleProgress = 0;

            //自动出料口:产物直接送进近旁存储(主线程经 Defer,原版箱走快照广播)
            if (ModuleRack.AutoEject) {
                Defer(() => {
                    if (RecData.OutputItem == null || RecData.OutputItem.IsAir) {
                        return;
                    }
                    Item toStore = RecData.OutputItem.Clone();
                    if (MachineLogistics.TryDeposit(Position, toStore)) {
                        RecData.OutputItem.TurnToAir();
                        SendData();
                    }
                });
            }

            SendData();
        }

        internal void HandleInputItem() {
            Item mouseItem = Main.mouseItem;

            //手持可拆装备,放入装备槽(装备均不可堆叠,直接交换)
            if (RecyclerTables.CanRecycle(mouseItem)) {
                if (RecData.InputItem == null || RecData.InputItem.IsAir) {
                    RecData.InputItem = mouseItem.Clone();
                    mouseItem.TurnToAir();
                }
                else {
                    Item temp = RecData.InputItem.Clone();
                    RecData.InputItem = mouseItem.Clone();
                    Main.mouseItem = temp;
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
                return;
            }

            //手为空,取出装备槽物品
            if (mouseItem.IsAir && RecData.InputItem != null && !RecData.InputItem.IsAir) {
                Main.mouseItem = RecData.InputItem.Clone();
                RecData.InputItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        internal void HandleOutputItem() {
            Item mouseItem = Main.mouseItem;

            if (RecData.OutputItem == null || RecData.OutputItem.IsAir) {
                return;
            }

            if (mouseItem.IsAir) {
                Main.mouseItem = RecData.OutputItem.Clone();
                RecData.OutputItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
            else if (mouseItem.type == RecData.OutputItem.type) {
                int space = mouseItem.maxStack - mouseItem.stack;
                int transfer = System.Math.Min(space, RecData.OutputItem.stack);
                mouseItem.stack += transfer;
                RecData.OutputItem.stack -= transfer;
                if (RecData.OutputItem.stack <= 0) {
                    RecData.OutputItem.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        public void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();

            //Shift点击快速放入/取出
            if (Main.keyState.PressingShift()) {
                if (RecyclerTables.CanRecycle(item) && (RecData.InputItem == null || RecData.InputItem.IsAir)) {
                    RecData.InputItem = item.Clone();
                    item.TurnToAir();
                    SendData();
                    SoundEngine.PlaySound(SoundID.Grab);
                    return;
                }

                //Shift点击取出所有物品(直接入背包,MP下地面掉落会被队友截走)
                if (RecData.InputItem != null && !RecData.InputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), RecData.InputItem.Clone());
                    RecData.InputItem.TurnToAir();
                }
                if (RecData.OutputItem != null && !RecData.OutputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), RecData.OutputItem.Clone());
                    RecData.OutputItem.TurnToAir();
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            //打开UI
            var ui = UIHandleLoader.GetUIHandleOfType<RecyclerUI>();
            if (ui != null) {
                ui.Interactive(this, newTP);
            }
        }

        public override void MachineKill() {
            //掉落槽内物品与模块
            if (!VaultUtils.isClient) {
                if (RecData.InputItem != null && !RecData.InputItem.IsAir) {
                    DropItem(RecData.InputItem.Clone());
                }
                if (RecData.OutputItem != null && !RecData.OutputItem.IsAir) {
                    DropItem(RecData.OutputItem.Clone());
                }
                ModuleRack.EnsureSlots(ModuleSlotCount);
                ModuleRack.DropAll(item => DropItem(item));
            }

            RecData.InputItem?.TurnToAir();
            RecData.OutputItem?.TurnToAir();

            //关闭UI
            var ui = UIHandleLoader.GetUIHandleOfType<RecyclerUI>();
            if (ui != null && ui.CurrentTP == this) {
                ui.IsActive = false;
            }
        }

        #region 存档与同步:模块架追加在既有字段之后,槽数固定故两端对称
        public override void SendData(ModPacket data) {
            base.SendData(data);
            ModuleRack.Send(data, ModuleSlotCount);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            ModuleRack.Receive(reader, ModuleSlotCount);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            ModuleRack.Save(tag, ModuleSlotCount);
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            ModuleRack.Load(tag, ModuleSlotCount, GetType().Name);
        }
        #endregion

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
