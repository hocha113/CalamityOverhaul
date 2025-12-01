using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses.OceanRaidersUIs;
using CalamityOverhaul.Content.Industrials;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
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
        internal const int fishingTime = 12;
        internal const int maxStorageSlots = 340;

        //机器状态
        internal int frame;
        internal int fishingTimer;
        internal int particleTimer;
        internal int textTimer;
        internal bool isWorking;
        internal bool hasWater;
        internal float glowIntensity;
        internal Vector2 intakeCenter;

        //存储物品列表
        internal List<Item> storedItems = new();

        //吸入效果粒子
        internal List<FishingParticle> fishingParticles = new();

        //视觉效果管理器
        private OceanRaidersVortexEffect vortexEffect;

        //音效系统
        private SlotId vortexSoundSlot;
        private SoundStyle vortexSoundStyle = new SoundStyle(CWRConstant.Asset + "Sounds/RollingMERoer") {
            IsLooped = true,
            MaxInstances = 8,
            Volume = 0.6f
        };

        public override void SetBattery() {
            IdleDistance = 4000;
            storedItems = new List<Item>();
            fishingParticles = new List<FishingParticle>();
            vortexEffect = new OceanRaidersVortexEffect(this);
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(isWorking);
            data.Write(hasWater);
            data.Write(fishingTimer);

            //发送存储物品数据
            data.Write(storedItems.Count);
            foreach (var item in storedItems) {
                if (item == null) {
                    ItemIO.Send(new Item(), data, true);
                }
                else {
                    ItemIO.Send(item, data, true);
                }
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
                Item item = ItemIO.Receive(reader, true);
                storedItems.Add(item);
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            try {
                //保存存储的物品
                List<TagCompound> itemTags = new();
                foreach (var item in storedItems) {
                    if (item == null) {
                        itemTags.Add(ItemIO.Save(new Item()));
                    }
                    else {
                        itemTags.Add(ItemIO.Save(item));
                    }
                }
                tag["itemTags"] = itemTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"OceanRaidersTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                //加载存储的物品
                if (!tag.TryGet("itemTags", out List<TagCompound> itemTags)) {
                    return;
                }

                storedItems.Clear();
                foreach (var itemTag in itemTags) {
                    storedItems.Add(ItemIO.Load(itemTag));
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"OceanRaidersTP.LoadData Error: {ex.Message}");
            }
        }

        private bool CheckWaterBelow() {
            //检查机器下方是否有水
            int checkDistance = 32;
            Point startPoint = (Position + new Point16(3, 6)).ToPoint();

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
            int power = 50 + Main.rand.Next(30);

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

            catches.Add(ModContent.ItemType<Oceanfragments>());

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

            //添加新物品（检查是否超过最大格数）
            if (storedItems.Count < maxStorageSlots && stack > 0) {
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

            //生成新粒子（增加生成频率）
            if (isWorking && particleTimer++ % 8 == 0 && fishingParticles.Count < 20) {
                SpawnFishingParticle();
            }
        }

        private void SpawnFishingParticle() {
            //从水下随机位置生成粒子
            Vector2 waterSurfacePos = FindWaterSurface();
            if (waterSurfacePos == Vector2.Zero) return;

            //在水域深处随机位置生成
            Vector2 spawnPos = waterSurfacePos + new Vector2(
                Main.rand.NextFloat(-70f, 70f),
                Main.rand.NextFloat(40f, 120f)
            );

            //确保生成位置在水中
            Tile tile = Framing.GetTileSafely(spawnPos);
            if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water) {
                FishingParticle particle = new FishingParticle {
                    Position = spawnPos,
                    Type = (FishingParticleType)Main.rand.Next(3),
                    Scale = Main.rand.NextFloat(0.6f, 1.2f),
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    Life = Main.rand.Next(260, 300)
                };

                fishingParticles.Add(particle);
            }
        }

        //查找水面位置
        private Vector2 FindWaterSurface() {
            Point startPoint = (Position + new Point16(3, 6)).ToPoint();

            for (int y = 0; y < 32; y++) {
                for (int x = -2; x <= 2; x++) {
                    Tile tile = Framing.GetTileSafely(startPoint.X + x, startPoint.Y + y);
                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water) {
                        return new Vector2(
                            (startPoint.X + x) * 16 + 8,
                            (startPoint.Y + y) * 16 + 8
                        );
                    }
                }
            }

            return Vector2.Zero;
        }

        //循环音效更新回调
        private bool LoopingSoundUpdate(ActiveSound soundInstance) {
            //根据工作强度调整音调和音量
            float workIntensity = isWorking ? glowIntensity : 0f;
            soundInstance.Pitch = (-0.3f + workIntensity * 0.6f) * 1.8f;
            soundInstance.Position = intakeCenter;
            soundInstance.Volume = workIntensity * 0.8f;
            return Active && hasWater;
        }

        //更新音效系统
        private void UpdateSoundEffects() {
            if (VaultUtils.isServer) return;

            if (isWorking && hasWater) {
                //播放循环的水龙卷音效
                if (!SoundEngine.TryGetActiveSound(vortexSoundSlot, out var activeSound)) {
                    vortexSoundSlot = SoundEngine.PlaySound(vortexSoundStyle, intakeCenter, LoopingSoundUpdate);
                }
            }
        }

        public override void UpdateMachine() {
            //更新吸入口位置
            intakeCenter = CenterInWorld + new Vector2(0, 32);

            //更新动画
            frame = 0;
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
                vortexEffect?.Update();
                UpdateSoundEffects();
                return;
            }

            //机器正常工作
            isWorking = true;
            glowIntensity = Math.Min(1f, glowIntensity + 0.05f);

            //钓鱼计时
            if (++fishingTimer >= fishingTime) {
                fishingTimer = 0;
                MachineData.UEvalue -= consumeUE;

                PerformFishing();

                if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                    //播放钓到鱼的音效
                    SoundEngine.PlaySound(SoundID.Splash with {
                        Pitch = -0.2f,
                        Volume = 0.5f
                    }, intakeCenter);

                    //随机播放水泡音效
                    if (Main.rand.NextBool(3)) {
                        SoundEngine.PlaySound(SoundID.Item21 with {
                            Pitch = Main.rand.NextFloat(-0.3f, 0.1f),
                            Volume = 0.3f
                        }, intakeCenter);
                    }
                }
            }

            //自动转移物品到箱子
            if (storedItems.Count > 0 && Main.GameUpdateCount % 120 == 0) {
                TransferItemsToChest();
            }

            //更新粒子效果
            UpdateParticles();

            //更新水龙卷效果
            vortexEffect?.Update();

            //更新音效系统
            UpdateSoundEffects();
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            //绘制水龙卷效果
            vortexEffect?.DrawVortex(spriteBatch);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //绘制粒子
            if (VaultUtils.isServer) return;

            foreach (var particle in fishingParticles) {
                particle.Draw(spriteBatch);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            //右键打开专属箱子UI
            if (player.whoAmI == Main.myPlayer) {
                OceanRaidersUI.Instance.Interactive(this);
            }
            return true;
        }
    }
}