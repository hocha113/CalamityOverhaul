using CalamityOverhaul.Content.Industrials;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses
{
    internal class OceanRaidersTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<OceanRaidersTile>();
        public override int TargetItem => ModContent.ItemType<OceanRaiders>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 1200;

        //机器常量
        internal const int consumeUE = 8;
        internal const int fishingTime = 180; //3秒钓一次鱼
        internal const int maxStorageItems = 40; //最多存储40组物品

        //机器状态
        internal int frame;
        internal int fishingTimer;
        internal int particleTimer;
        internal int textTimer;
        internal bool isWorking;
        internal bool hasWater;
        internal float glowIntensity;
        internal Vector2 intakeCenter; //吸入口中心位置

        //存储物品列表
        internal List<Item> storedItems = new();

        //吸入效果粒子
        internal List<FishingParticle> fishingParticles = new();

        public override void SetBattery() {
            IdleDistance = 4000;
            storedItems = new List<Item>();
            fishingParticles = new List<FishingParticle>();
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(isWorking);
            data.Write(hasWater);
            data.Write(fishingTimer);

            //发送存储物品数据
            data.Write(storedItems.Count);
            foreach (var item in storedItems) {
                data.Write(item.type);
                data.Write(item.stack);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            isWorking = reader.ReadBoolean();
            hasWater = reader.ReadBoolean();
            fishingTimer = reader.ReadInt32();

            //接收存储物品数据
            int count = reader.ReadInt32();
            storedItems.Clear();
            for (int i = 0; i < count; i++) {
                int type = reader.ReadInt32();
                int stack = reader.ReadInt32();
                Item item = new Item();
                item.SetDefaults(type);
                item.stack = stack;
                storedItems.Add(item);
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            //保存存储的物品
            tag["itemCount"] = storedItems.Count;
            for (int i = 0; i < storedItems.Count; i++) {
                tag[$"item{i}_type"] = storedItems[i].type;
                tag[$"item{i}_stack"] = storedItems[i].stack;
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            //加载存储的物品
            storedItems.Clear();
            int count = tag.GetInt("itemCount");
            for (int i = 0; i < count; i++) {
                if (tag.ContainsKey($"item{i}_type")) {
                    Item item = new Item();
                    item.SetDefaults(tag.GetInt($"item{i}_type"));
                    item.stack = tag.GetInt($"item{i}_stack");
                    storedItems.Add(item);
                }
            }
        }

        private bool CheckWaterBelow() {
            //检查机器下方是否有水
            int checkDistance = 32; //检查32格深度
            Point startPoint = (Position + new Point16(3, 6)).ToPoint(); //从底部中心开始检查

            for (int y = 0; y < checkDistance; y++) {
                for (int x = -2; x <= 2; x++) {
                    Tile tile = Framing.GetTileSafely(startPoint.X + x, startPoint.Y + y);
                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water) {
                        return true;
                    }
                }
            }
            return false;
        }

        private void PerformFishing() {
            //模拟钓鱼，生成物品
            if (VaultUtils.isClient) return;

            //创建钓鱼尝试
            int power = 50 + Main.rand.Next(30); //钓鱼竿力量

            //获取可能的钓鱼物品
            List<int> possibleCatches = GetPossibleCatches(power);
            if (possibleCatches.Count == 0) return;

            int caughtItem = Main.rand.Next(possibleCatches);
            int stack = 1;

            //箱子类物品只给1个
            if (caughtItem >= ItemID.WoodenCrate && caughtItem <= ItemID.GoldenCrate) {
                stack = 1;
            }
            //鱼类物品给1-3个
            else {
                stack = Main.rand.Next(1, 4);
            }

            AddItemToStorage(caughtItem, stack);
        }

        private List<int> GetPossibleCatches(int fishingPower) {
            List<int> catches = new();

            //基础鱼类
            catches.Add(ItemID.Bass);
            catches.Add(ItemID.Trout);
            catches.Add(ItemID.AtlanticCod);
            catches.Add(ItemID.RedSnapper);
            catches.Add(ItemID.Tuna);

            if (fishingPower > 30) {
                catches.Add(ItemID.NeonTetra);
                catches.Add(ItemID.ArmoredCavefish);
                catches.Add(ItemID.DoubleCod);
            }

            if (fishingPower > 50) {
                catches.Add(ItemID.WoodenCrate);
                catches.Add(ItemID.IronCrate);
            }

            if (fishingPower > 70) {
                catches.Add(ItemID.GoldenCrate);
                catches.Add(ItemID.Swordfish);
                catches.Add(ItemID.Sextant);
            }

            //添加灾厄物品支持
            if (CWRMod.Instance.calamity != null) {
                catches.Add(ModContent.ItemType<Oceanfragments>());
            }

            return catches;
        }

        private void AddItemToStorage(int itemType, int stack) {
            //尝试堆叠到现有物品
            foreach (var item in storedItems) {
                if (item.type == itemType && item.stack < item.maxStack) {
                    int addAmount = Math.Min(stack, item.maxStack - item.stack);
                    item.stack += addAmount;
                    stack -= addAmount;
                    if (stack <= 0) {
                        SendData();
                        return;
                    }
                }
            }

            //添加新物品
            if (storedItems.Count < maxStorageItems && stack > 0) {
                Item newItem = new Item();
                newItem.SetDefaults(itemType);
                newItem.stack = stack;
                storedItems.Add(newItem);
                SendData();
            }
        }

        private void TransferItemsToChest() {
            if (storedItems.Count == 0) return;

            Chest chest = Position.FindClosestChest(800, false);
            if (chest != null) {
                for (int i = storedItems.Count - 1; i >= 0; i--) {
                    Item item = storedItems[i];
                    chest.AddItem(item, true);
                    if (item.stack == 0) {
                        storedItems.RemoveAt(i);
                    }
                }
                if (storedItems.Count == 0) {
                    SendData();
                }
            }
        }

        private void UpdateParticles() {
            if (VaultUtils.isServer) return;

            //更新现有粒子
            for (int i = fishingParticles.Count - 1; i >= 0; i--) {
                fishingParticles[i].Update(intakeCenter);
                if (fishingParticles[i].ShouldRemove()) {
                    fishingParticles.RemoveAt(i);
                }
            }

            //生成新粒子
            if (isWorking && particleTimer++ % 3 == 0) {
                SpawnFishingParticle();
            }
        }

        private void SpawnFishingParticle() {
            //在吸入口周围生成粒子
            Vector2 spawnPos = intakeCenter + Main.rand.NextVector2Circular(80, 40);
            spawnPos.Y += Main.rand.NextFloat(20, 60);

            FishingParticle particle = new FishingParticle {
                Position = spawnPos,
                Type = (FishingParticleType)Main.rand.Next(3),
                Scale = Main.rand.NextFloat(0.6f, 1.2f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                Life = Main.rand.Next(60, 120)
            };

            fishingParticles.Add(particle);
        }

        public override void UpdateMachine() {
            //更新吸入口位置
            intakeCenter = CenterInWorld + new Vector2(0, 32);

            //更新动画
            frame = 0;//固定为0帧，因为目前纹理只有一帧动画，等以后做了多帧后再改这里
            //检查水源
            hasWater = CheckWaterBelow();

            //检查能量和水源
            if (MachineData.UEvalue < consumeUE || !hasWater) {
                isWorking = false;
                glowIntensity = Math.Max(0, glowIntensity - 0.05f);

                if (!VaultUtils.isServer && ++textTimer > 180) {
                    string text = !hasWater ? OceanRaiders.NoWaterText.Value : OceanRaiders.NoEnergyText.Value;
                    CombatText.NewText(HitBox, Color.Orange, text, false);
                    textTimer = 0;
                }

                UpdateParticles();
                return;
            }

            //机器正常工作
            isWorking = true;
            glowIntensity = Math.Min(1f, glowIntensity + 0.05f);

            //钓鱼计时
            if (++fishingTimer >= fishingTime) {
                PerformFishing();
                MachineData.UEvalue -= consumeUE;
                fishingTimer = 0;

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.2f, Volume = 0.5f }, intakeCenter);
                }
            }

            //自动转移物品到箱子
            if (storedItems.Count > 0 && Main.GameUpdateCount % 120 == 0) {
                TransferItemsToChest();
            }

            //更新粒子效果
            UpdateParticles();
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            //绘制吸入口的水龙卷效果
            if (!isWorking || VaultUtils.isServer) return;

            float vortexIntensity = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.3f + 0.7f;
            Color vortexColor = new Color(100, 200, 255) * (0.4f * vortexIntensity * glowIntensity);

            for (int i = 0; i < 8; i++) {
                float rotation = (Main.GlobalTimeWrappedHourly * 2f + i * MathHelper.PiOver4) % MathHelper.TwoPi;
                float radius = 40f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f + i) * 10f;
                Vector2 offset = rotation.ToRotationVector2() * radius;
                Vector2 drawPos = intakeCenter + offset - Main.screenPosition;

                Dust.NewDustPerfect(drawPos, DustID.Water, Vector2.Zero, 0, vortexColor, 1.5f);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //绘制粒子
            if (VaultUtils.isServer) return;

            foreach (var particle in fishingParticles) {
                particle.Draw(spriteBatch);
            }

            //绘制存储信息
            if (storedItems.Count > 0 && InScreen) {
                Vector2 textPos = CenterInWorld - new Vector2(0, 80) - Main.screenPosition;
                string text = $"Stored: {storedItems.Count}/{maxStorageItems}";
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text
                    , textPos.X, textPos.Y, Color.White, Color.Black, Vector2.Zero, 0.8f);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            //右键取出所有物品
            if (storedItems.Count > 0 && !VaultUtils.isClient) {
                foreach (var item in storedItems) {
                    player.QuickSpawnItem(player.FromObjectGetParent(), item);
                }
                storedItems.Clear();
                SendData();
                SoundEngine.PlaySound(SoundID.Grab, player.Center);
            }
            return true;
        }
    }
}
