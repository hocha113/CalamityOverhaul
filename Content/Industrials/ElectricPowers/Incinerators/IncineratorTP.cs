using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Incinerators
{
    /// <summary>
    /// 焚烧炉TP实体
    /// </summary>
    internal class IncineratorTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<IncineratorTile>();
        public override int TargetItem => ModContent.ItemType<Incinerator>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500;

        internal IncineratorData IncData => MachineData as IncineratorData;
        internal int frame;
        private int frameTimer;
        private int particleTimer;

        /// <summary>
        /// 焚烧配方表：输入物品类型 -> 输出物品类型
        /// </summary>
        public static Dictionary<int, int> SmeltRecipes { get; private set; }

        public override MachineData GetGeneratorDataInds() {
            var data = new IncineratorData {
                MaxSmeltingProgress = 120,
                UEPerTick = 0.5f,
                MaxUE = MaxUEValue,
                MaxTemperature = 100
            };
            return data;
        }

        public override void SetBattery() {
            InitializeRecipes();
        }

        /// <summary>
        /// 初始化焚烧配方
        /// </summary>
        private static void InitializeRecipes() {
            if (SmeltRecipes != null) {
                return;
            }

            SmeltRecipes = new Dictionary<int, int> {
                //原版矿石
                { ItemID.CopperOre, ItemID.CopperBar },
                { ItemID.TinOre, ItemID.TinBar },
                { ItemID.IronOre, ItemID.IronBar },
                { ItemID.LeadOre, ItemID.LeadBar },
                { ItemID.SilverOre, ItemID.SilverBar },
                { ItemID.TungstenOre, ItemID.TungstenBar },
                { ItemID.GoldOre, ItemID.GoldBar },
                { ItemID.PlatinumOre, ItemID.PlatinumBar },
                { ItemID.CrimtaneOre, ItemID.CrimtaneBar },
                { ItemID.DemoniteOre, ItemID.DemoniteBar },
                { ItemID.Hellstone, ItemID.HellstoneBar },
                { ItemID.CobaltOre, ItemID.CobaltBar },
                { ItemID.PalladiumOre, ItemID.PalladiumBar },
                { ItemID.MythrilOre, ItemID.MythrilBar },
                { ItemID.OrichalcumOre, ItemID.OrichalcumBar },
                { ItemID.AdamantiteOre, ItemID.AdamantiteBar },
                { ItemID.TitaniumOre, ItemID.TitaniumBar },
                { ItemID.ChlorophyteOre, ItemID.ChlorophyteBar },
                { ItemID.LunarOre, ItemID.LunarBar },
                //其他可焚烧物
                { ItemID.SandBlock, ItemID.Glass },
                { ItemID.Wood, ItemID.Coal },
                { ItemID.Ebonwood, ItemID.Coal },
                { ItemID.Shadewood, ItemID.Coal },
                { ItemID.RichMahogany, ItemID.Coal },
                { ItemID.BorealWood, ItemID.Coal },
                { ItemID.PalmWood, ItemID.Coal },
                { ItemID.Pearlwood, ItemID.Coal },
                { ItemID.ClayBlock, ItemID.RedBrick },
                { ItemID.MudBlock, ItemID.DirtBlock },
            };
        }

        /// <summary>
        /// 检查物品是否可以被焚烧
        /// </summary>
        public static bool CanSmelt(Item item) {
            if (item == null || item.IsAir) {
                return false;
            }
            InitializeRecipes();
            return SmeltRecipes.ContainsKey(item.type);
        }

        /// <summary>
        /// 获取焚烧后的输出物品类型
        /// </summary>
        public static int GetSmeltResult(int inputType) {
            InitializeRecipes();
            return SmeltRecipes.TryGetValue(inputType, out int result) ? result : ItemID.None;
        }

        public override void UpdateMachine() {
            //更新温度视觉效果
            if (IncData.SmeltingProgress > 0 && IncData.UEvalue >= IncData.UEPerTick) {
                IncData.Temperature = MathHelper.Lerp(IncData.Temperature, IncData.MaxTemperature, 0.05f);
            }
            else {
                IncData.Temperature = MathHelper.Lerp(IncData.Temperature, 0, 0.02f);
            }

            //没有电量时停止工作
            if (IncData.UEvalue < IncData.UEPerTick) {
                UpdateIdleAnimation();
                return;
            }

            //检查是否可以开始焚烧
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

        /// <summary>
        /// 检查是否可以开始焚烧
        /// </summary>
        private bool CanStartSmelting() {
            if (IncData.InputItem == null || IncData.InputItem.IsAir) {
                return false;
            }
            if (!CanSmelt(IncData.InputItem)) {
                return false;
            }

            int resultType = GetSmeltResult(IncData.InputItem.type);
            if (resultType == ItemID.None) {
                return false;
            }

            //检查输出槽是否有空间
            if (IncData.OutputItem != null && !IncData.OutputItem.IsAir) {
                if (IncData.OutputItem.type != resultType) {
                    return false;
                }
                if (IncData.OutputItem.stack >= IncData.OutputItem.maxStack) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 开始焚烧
        /// </summary>
        private void StartSmelting() {
            IncData.SmeltingProgress = 1;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = -0.2f }, CenterInWorld);
            }
        }

        /// <summary>
        /// 处理焚烧过程
        /// </summary>
        private void ProcessSmelting() {
            //消耗电量
            IncData.UEvalue -= IncData.UEPerTick;

            //更新动画帧(在工作帧0和1之间切换)
            if (++frameTimer >= 6) {
                frame = frame == 0 ? 1 : 0;
                frameTimer = 0;
            }

            //生成粒子效果
            if (!VaultUtils.isServer) {
                SpawnWorkingParticles();
            }

            //增加进度
            IncData.SmeltingProgress++;

            //焚烧完成
            if (IncData.SmeltingProgress >= IncData.MaxSmeltingProgress) {
                CompleteSmelting();
            }
        }

        /// <summary>
        /// 完成焚烧
        /// </summary>
        private void CompleteSmelting() {
            int resultType = GetSmeltResult(IncData.InputItem.type);
            if (resultType == ItemID.None) {
                IncData.SmeltingProgress = 0;
                return;
            }

            //减少输入物品
            IncData.InputItem.stack--;
            if (IncData.InputItem.stack <= 0) {
                IncData.InputItem.TurnToAir();
            }

            //增加输出物品
            if (IncData.OutputItem == null || IncData.OutputItem.IsAir) {
                IncData.OutputItem = new Item(resultType, 1);
            }
            else {
                IncData.OutputItem.stack++;
            }

            //重置进度
            IncData.SmeltingProgress = 0;

            SendData();
        }

        /// <summary>
        /// 更新空闲动画
        /// </summary>
        private void UpdateIdleAnimation() {
            frame = 2; //熄灭帧
            frameTimer = 0;
        }

        /// <summary>
        /// 生成工作时的粒子效果
        /// </summary>
        private void SpawnWorkingParticles() {
            if (++particleTimer < 4) {
                return;
            }
            particleTimer = 0;

            Vector2 particlePos = CenterInWorld + new Vector2(Main.rand.NextFloat(-20f, 20f), -30f);
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(particlePos, 4, 4, DustID.Smoke, 0, -2f, 100, default, 1.2f);
            }
            if (Main.rand.NextBool(2)) {
                Dust.NewDust(particlePos, 4, 4, DustID.Torch, Main.rand.NextFloat(-1f, 1f), -3f, 0, default, 1.5f);
            }
        }

        /// <summary>
        /// 处理物品交互
        /// </summary>
        internal void HandleInputItem() {
            Item mouseItem = Main.mouseItem;

            //如果手持物品可以焚烧，放入输入槽
            if (CanSmelt(mouseItem)) {
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

        /// <summary>
        /// 处理输出物品交互
        /// </summary>
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

        /// <summary>
        /// 右键点击物块时的处理
        /// </summary>
        public void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();

            //Shift点击快速放入
            if (Main.keyState.PressingShift()) {
                if (CanSmelt(item)) {
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

                //Shift点击取出所有物品
                if (IncData.InputItem != null && !IncData.InputItem.IsAir) {
                    Main.LocalPlayer.QuickSpawnItem(new EntitySource_WorldEvent(), IncData.InputItem, IncData.InputItem.stack);
                    IncData.InputItem.TurnToAir();
                }
                if (IncData.OutputItem != null && !IncData.OutputItem.IsAir) {
                    Main.LocalPlayer.QuickSpawnItem(new EntitySource_WorldEvent(), IncData.OutputItem, IncData.OutputItem.stack);
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
            }

            IncData.InputItem?.TurnToAir();
            IncData.OutputItem?.TurnToAir();

            //关闭UI
            var ui = UIHandleLoader.GetUIHandleOfType<IncineratorUI>();
            if (ui != null && ui.CurrentTP == this) {
                ui.IsActive = false;
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
