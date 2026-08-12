using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Incinerators
{
    /// <summary>焚烧炉TP</summary>
    internal class IncineratorTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<IncineratorTile>();
        public override int TargetItem => ModContent.ItemType<Incinerator>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500 * ModuleRack.StorageMult;

        /// <summary>模块槽数</summary>
        internal const int ModuleSlotCount = 3;
        internal readonly MachineModules.MachineModuleRack ModuleRack
            = new(MachineModules.MachineModuleTarget.Incinerator);

        internal IncineratorData IncData => MachineData as IncineratorData;
        internal int frame;
        private int frameTimer;
        private int particleTimer;
        /// <summary>熔速乘数下进度的小数累加器</summary>
        private float smeltAcc;
        /// <summary>自动进料节拍</summary>
        private int autoFeedTimer;

        /// <summary>当前每 tick 能耗(含节能模块)</summary>
        internal float EffectiveUEPerTick => IncData.UEPerTick * ModuleRack.IncEnergyMult;

        public override MachineData GetGeneratorDataInds() {
            var data = new IncineratorData {
                MaxSmeltingProgress = 120,
                UEPerTick = 0.5f,
                MaxUE = MaxUEValue,
                MaxTemperature = 100
            };
            return data;
        }

        public override void UpdateMachine() {
            ModuleRack.EnsureSlots(ModuleSlotCount);
            ModuleRack.Refresh();
            //储能扩容模块动上限,数据侧字段每帧对齐
            IncData.MaxUE = MaxUEValue;

            //更新温度视觉效果
            if (IncData.SmeltingProgress > 0 && IncData.UEvalue >= EffectiveUEPerTick) {
                IncData.Temperature = MathHelper.Lerp(IncData.Temperature, IncData.MaxTemperature, 0.05f);
            }
            else {
                IncData.Temperature = MathHelper.Lerp(IncData.Temperature, 0, 0.02f);
            }

            //自动进料斗:输入槽空了就从近旁存储抽可焚物(权威端,主线程经 Defer)
            if (!VaultUtils.isClient && ModuleRack.AutoFeed && ++autoFeedTimer >= 30) {
                autoFeedTimer = 0;
                if (IncData.InputItem == null || IncData.InputItem.IsAir) {
                    Defer(() => {
                        if (IncData.InputItem != null && !IncData.InputItem.IsAir) {
                            return;
                        }
                        Item got = MachineModules.MachineLogistics.TryWithdraw(Position,
                            stored => IncineratorRecipes.TryGetRecipe(stored.type, out _), 15);
                        if (!got.IsAir) {
                            IncData.InputItem = got;
                            SendData();
                        }
                    });
                }
            }

            //没有电量时停止工作
            if (IncData.UEvalue < EffectiveUEPerTick) {
                UpdateIdleAnimation();
                return;
            }

            //可否开烧
            if (IncData.SmeltingProgress == 0 && CanStartSmelting()) {
                StartSmelting();
            }

            //执行焚烧
            if (IncData.SmeltingProgress > 0) {
                ProcessSmelting();
            }
            else {
                UpdateIdleAnimation();
            }
        }

        private bool CanStartSmelting() {
            if (IncData.InputItem == null || IncData.InputItem.IsAir) {
                return false;
            }
            if (!IncineratorRecipes.TryGetRecipe(IncData.InputItem.type, out var recipe)) {
                return false;
            }

            //检查输入物品数量是否足够
            if (IncData.InputItem.stack < recipe.InputStack) {
                return false;
            }

            int resultType = recipe.OutputType;
            int outputStack = IncineratorRecipes.GetOutputStack(IncData.InputItem.type);

            //检查输出槽是否有空间
            if (IncData.OutputItem != null && !IncData.OutputItem.IsAir) {
                if (IncData.OutputItem.type != resultType) {
                    return false;
                }
                if (IncData.OutputItem.stack + outputStack > IncData.OutputItem.maxStack) {
                    return false;
                }
            }

            return true;
        }

        private void StartSmelting() {
            IncData.SmeltingProgress = 1;
            if (!VaultUtils.isServer) {
                //并行阶段音效播放延迟到主线程执行(串行阶段立即执行)
                Defer(() => SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = -0.2f }, CenterInWorld));
            }
        }

        private void ProcessSmelting() {
            //消耗电量(节能模块打折)
            IncData.UEvalue -= EffectiveUEPerTick;

            //更新动画帧(在工作帧0和1之间切换)
            if (++frameTimer >= 6) {
                if (++frame > 2) {
                    frame = 0;
                }
                frameTimer = 0;
            }

            //生成粒子效果
            if (!VaultUtils.isServer) {
                SpawnWorkingParticles();
            }

            //增加进度:熔速乘数走小数累加器,整数进度保持网络字段兼容
            smeltAcc += ModuleRack.IncSpeedMult;
            int steps = (int)smeltAcc;
            smeltAcc -= steps;
            IncData.SmeltingProgress += steps;

            //焚烧完成
            if (IncData.SmeltingProgress >= IncData.MaxSmeltingProgress) {
                CompleteSmelting();
            }
        }

        private void CompleteSmelting() {
            //物品结算是权威端专属：客户端把进度停在满格等服务器的完成包，
            //本地结算再推送会用漂移状态覆盖服务器的真实槽位
            if (VaultUtils.isClient) {
                IncData.SmeltingProgress = IncData.MaxSmeltingProgress;
                return;
            }

            if (!IncineratorRecipes.TryGetRecipe(IncData.InputItem.type, out var recipe)) {
                IncData.SmeltingProgress = 0;
                return;
            }

            int resultType = recipe.OutputType;
            int inputCost = recipe.InputStack;
            int outputAmount = IncineratorRecipes.GetOutputStack(IncData.InputItem.type);

            //双联坩埚:概率双倍产出(权威端判定;Rand 线程安全,并行阶段可用)
            if (ModuleRack.IncDoubleChance > 0f && Rand.NextFloat() < ModuleRack.IncDoubleChance) {
                outputAmount *= 2;
            }

            //减少输入物品(按配方需求数量)
            IncData.InputItem.stack -= inputCost;
            if (IncData.InputItem.stack <= 0) {
                IncData.InputItem.TurnToAir();
            }

            //增加输出物品(应用2倍产出)
            if (IncData.OutputItem == null || IncData.OutputItem.IsAir) {
                IncData.OutputItem = new Item(resultType, outputAmount);
            }
            else {
                IncData.OutputItem.stack += outputAmount;
                //不超过maxStack
                if (IncData.OutputItem.stack > IncData.OutputItem.maxStack) {
                    IncData.OutputItem.stack = IncData.OutputItem.maxStack;
                }
            }

            //重置进度
            IncData.SmeltingProgress = 0;

            //自动出料口:产物直接送进近旁存储(主线程经 Defer,原版箱走快照广播)
            if (ModuleRack.AutoEject) {
                Defer(() => {
                    if (IncData.OutputItem == null || IncData.OutputItem.IsAir) {
                        return;
                    }
                    Item toStore = IncData.OutputItem.Clone();
                    if (MachineModules.MachineLogistics.TryDeposit(Position, toStore)) {
                        IncData.OutputItem.TurnToAir();
                        SendData();
                    }
                });
            }

            SendData();
        }

        private void UpdateIdleAnimation() {
            frame = 3;//熄灭帧
            frameTimer = 0;
        }

        private void SpawnWorkingParticles() {
            if (++particleTimer < 4) {
                return;
            }
            particleTimer = 0;

            Vector2 particlePos = CenterInWorld + new Vector2(Rand.NextFloat(-20f, 20f), -30f);
            if (Rand.NextBool(3)) {
                //并行阶段Dust生成延迟到主线程执行(串行阶段立即执行)
                Defer(() => Dust.NewDust(particlePos, 4, 4, DustID.Smoke, 0, -2f, 100, default, 1.2f));
            }
            if (Rand.NextBool(2)) {
                float torchVelX = Rand.NextFloat(-1f, 1f);
                //并行阶段Dust生成延迟到主线程执行(串行阶段立即执行)
                Defer(() => Dust.NewDust(particlePos, 4, 4, DustID.Torch, torchVelX, -3f, 0, default, 1.5f));
            }
        }

        internal void HandleInputItem() {
            Item mouseItem = Main.mouseItem;

            //如果手持物品可以焚烧，放入输入槽
            if (IncineratorRecipes.CanSmelt(mouseItem)) {
                if (IncData.InputItem == null || IncData.InputItem.IsAir) {
                    IncData.InputItem = mouseItem.Clone();
                    mouseItem.TurnToAir();
                }
                else if (IncData.InputItem.type == mouseItem.type) {
                    int space = IncData.InputItem.maxStack - IncData.InputItem.stack;
                    int transfer = System.Math.Min(space, mouseItem.stack);
                    IncData.InputItem.stack += transfer;
                    mouseItem.stack -= transfer;
                    if (mouseItem.stack <= 0) {
                        mouseItem.TurnToAir();
                    }
                }
                else {
                    //交换物品
                    Item temp = IncData.InputItem.Clone();
                    IncData.InputItem = mouseItem.Clone();
                    Main.mouseItem = temp;
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
                return;
            }

            //如果手为空，取出输入槽物品
            if (mouseItem.IsAir && IncData.InputItem != null && !IncData.InputItem.IsAir) {
                Main.mouseItem = IncData.InputItem.Clone();
                IncData.InputItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        internal void HandleOutputItem() {
            Item mouseItem = Main.mouseItem;

            if (IncData.OutputItem == null || IncData.OutputItem.IsAir) {
                return;
            }

            if (mouseItem.IsAir) {
                Main.mouseItem = IncData.OutputItem.Clone();
                IncData.OutputItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
            else if (mouseItem.type == IncData.OutputItem.type) {
                int space = mouseItem.maxStack - mouseItem.stack;
                int transfer = System.Math.Min(space, IncData.OutputItem.stack);
                mouseItem.stack += transfer;
                IncData.OutputItem.stack -= transfer;
                if (IncData.OutputItem.stack <= 0) {
                    IncData.OutputItem.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
                SendData();
            }
        }

        public void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();

            //Shift点击快速放入
            if (Main.keyState.PressingShift()) {
                if (IncineratorRecipes.CanSmelt(item)) {
                    if (IncData.InputItem == null || IncData.InputItem.IsAir) {
                        IncData.InputItem = item.Clone();
                        item.TurnToAir();
                    }
                    else if (IncData.InputItem.type == item.type) {
                        int space = IncData.InputItem.maxStack - IncData.InputItem.stack;
                        int transfer = System.Math.Min(space, item.stack);
                        IncData.InputItem.stack += transfer;
                        item.stack -= transfer;
                        if (item.stack <= 0) {
                            item.TurnToAir();
                        }
                    }
                    SendData();
                    SoundEngine.PlaySound(SoundID.Grab);
                    return;
                }

                //Shift点击取出所有物品(直接入背包，MP下QuickSpawnItem是地面掉落会被队友截走)
                if (IncData.InputItem != null && !IncData.InputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), IncData.InputItem.Clone());
                    IncData.InputItem.TurnToAir();
                }
                if (IncData.OutputItem != null && !IncData.OutputItem.IsAir) {
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), IncData.OutputItem.Clone());
                    IncData.OutputItem.TurnToAir();
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            //打开UI
            var ui = UIHandleLoader.GetUIHandleOfType<IncineratorUI>();
            if (ui != null) {
                ui.Interactive(this, newTP);
            }
        }

        public override void MachineKill() {
            //掉落物品
            if (!VaultUtils.isClient) {
                if (IncData.InputItem != null && !IncData.InputItem.IsAir) {
                    DropItem(IncData.InputItem.Clone());
                }
                if (IncData.OutputItem != null && !IncData.OutputItem.IsAir) {
                    DropItem(IncData.OutputItem.Clone());
                }
                //模块随拆机倒出
                ModuleRack.EnsureSlots(ModuleSlotCount);
                ModuleRack.DropAll(item => DropItem(item));
            }

            IncData.InputItem?.TurnToAir();
            IncData.OutputItem?.TurnToAir();

            //关闭UI
            var ui = UIHandleLoader.GetUIHandleOfType<IncineratorUI>();
            if (ui != null && ui.CurrentTP == this) {
                ui.IsActive = false;
            }
        }

        #region 存档与同步:模块架追加在既有字段之后,槽数固定故两端对称
        public override void SendData(Terraria.ModLoader.ModPacket data) {
            base.SendData(data);
            ModuleRack.Send(data, ModuleSlotCount);
        }

        public override void ReceiveData(System.IO.BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            ModuleRack.Receive(reader, ModuleSlotCount);
        }

        public override void SaveData(Terraria.ModLoader.IO.TagCompound tag) {
            base.SaveData(tag);
            ModuleRack.Save(tag, ModuleSlotCount);
        }

        public override void LoadData(Terraria.ModLoader.IO.TagCompound tag) {
            base.LoadData(tag);
            ModuleRack.Load(tag, ModuleSlotCount, GetType().Name);
        }
        #endregion

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
