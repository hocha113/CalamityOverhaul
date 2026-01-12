using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses.OceanRaidersUIs;
using CalamityOverhaul.Content.Industrials;
using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Projectiles.Others;
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

        internal Item ItemFilter;
        internal float hoverSengs;

        public override void SetBattery() {
            storedItems = new List<Item>();
            fishingParticles = new List<FishingParticle>();
            vortexEffect = new OceanRaidersVortexEffect(this);
            ItemFilter = new Item();
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }
            storedItems ??= [];
            foreach (var i in storedItems) {
                if (i.Alives()) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, i);
                }
            }
            storedItems.Clear();
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(isWorking);
            data.Write(hasWater);
            data.Write(fishingTimer);
            ItemIO.Send(ItemFilter, data);

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
            ItemFilter = ItemIO.Receive(reader);

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
                ItemFilter ??= new Item();
                tag["_ItemFilter"] = ItemIO.Save(ItemFilter);

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
                if (tag.TryGet<TagCompound>("_ItemFilter", out var value)) {
                    ItemFilter = ItemIO.Load(value);
                }
                else {
                    ItemFilter = new Item();
                }

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

        private bool IsItemFiltered(int itemType) {
            if (ItemFilter == null || ItemFilter.IsAir) return false;
            if (ItemFilter.TryGetGlobalItem<ItemFilterData>(out var data)) {
                return data.Items.Contains(itemType);
            }
            return false;
        }

        private void PerformFishing() {
            //模拟钓鱼，生成物品
            if (VaultUtils.isClient) return;

            //计算钓鱼力，基于能量充盈度和随机波动
            int power = 50 + Main.rand.Next(30);
            if (MachineData.UEvalue > MaxUEValue * 0.8f) {
                power += 20;
            }

            //获取钓鱼掉落
            int caughtItem = GetFishingLoot(power);
            if (caughtItem <= ItemID.None) return;

            //检查过滤器，如果物品在过滤列表中则不进行捕获
            if (IsItemFiltered(caughtItem)) return;

            int stack;

            //箱子类物品只给1个
            if (ItemID.Sets.IsFishingCrate[caughtItem]) {
                stack = 1;
            }
            //鱼类物品给1-3个
            else {
                stack = Main.rand.Next(1, 4);
            }

            //防止出现不合理的物品数量
            if (ContentSamples.ItemsByType.TryGetValue(caughtItem, out var item)) {
                stack = (int)MathHelper.Clamp(stack, 1, item.maxStack);
            }

            AddItemToStorage(caughtItem, stack);
        }

        private static int GetFishingLoot(int fishingPower) {
            //判定是否钓到箱子，基础概率10%，高渔力加成
            int crateChance = 10;
            if (fishingPower > 100) crateChance += 10;
            if (Main.rand.Next(100) < crateChance) {
                //根据渔力决定箱子品质
                if (fishingPower > 90 && Main.rand.NextBool(5)) return ItemID.GoldenCrate;
                if (fishingPower > 50 && Main.rand.NextBool(3)) return ItemID.IronCrate;
                return ItemID.WoodenCrate;
            }

            //判定稀有掉落(渔力 > 80)
            if (fishingPower > 80 && Main.rand.NextBool(25)) {
                int[] rares = {
                    ItemID.Swordfish,
                    ItemID.Sextant,
                    ItemID.ReaverShark,
                    ItemID.SawtoothShark,
                    ItemID.Rockfish,
                    ItemID.PurpleClubberfish
                };
                return rares[Main.rand.Next(rares.Length)];
            }

            //判定特殊掉落
            if (Main.rand.NextBool(12)) {
                return ModContent.ItemType<Oceanfragments>();
            }

            //判定普通掉落
            List<int> commons = new() {
                ItemID.Bass,
                ItemID.Trout,
                ItemID.AtlanticCod,
                ItemID.RedSnapper,
                ItemID.Tuna,
                ItemID.Shrimp,
                ItemID.Flounder
            };

            //高渔力解锁更多鱼类
            if (fishingPower > 30) {
                commons.Add(ItemID.NeonTetra);
                commons.Add(ItemID.ArmoredCavefish);
                commons.Add(ItemID.DoubleCod);
                commons.Add(ItemID.Damselfish);
                commons.Add(ItemID.FrostMinnow);
            }

            //极低概率钓到垃圾(渔力越低概率越高)
            if (fishingPower < 50 && Main.rand.NextBool(10)) {
                int[] junk = { ItemID.OldShoe, ItemID.TinCan, ItemID.FishingSeaweed };
                return junk[Main.rand.Next(junk.Length)];
            }

            return commons[Main.rand.Next(commons.Count)];
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
                //获取箱子的世界坐标
                Vector2 chestPos = new Vector2(chest.x * 16 + 16, chest.y * 16 + 16);

                for (int i = storedItems.Count - 1; i >= 0; i--) {
                    Item item = storedItems[i];
                    if (!chest.CanItemBeAddedToChest(item)) {
                        continue;
                    }

                    //生成飞向箱子的物品粒子
                    if (!VaultUtils.isClient) {
                        //生成一个临时的视觉物品弹幕飞向箱子
                        Projectile.NewProjectile(
                            this.FromObjectGetParent(),
                            intakeCenter,
                            Vector2.Zero,
                            ModContent.ProjectileType<TransferItemProj>(),
                            0, 0, -1, item.type, chestPos.X, chestPos.Y
                        );
                    }
                    chest.eatingAnimationTime = 30;
                    chest.AddItem(item, true);
                    storedItems.RemoveAt(i);
                    if (storedItems.Count == 0) {
                        break;
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

            hoverSengs = HoverTP
                ? Math.Min(hoverSengs + 0.1f, 1f)
                : Math.Max(hoverSengs - 0.1f, 0f);

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

            if (ItemFilter != null && !ItemFilter.IsAir && hoverSengs > 0.01f) {
                var filterItems = ItemFilter.GetGlobalItem<ItemFilterData>().Items;
                if (filterItems.Count > 0) {
                    const float maxRadius = 150f;
                    float currentRadius = maxRadius * hoverSengs;
                    float angleIncrement = MathHelper.TwoPi / filterItems.Count;

                    Vector2 drawCenter = CenterInWorld - Main.screenPosition + new Vector2(0, 32);

                    for (int i = 0; i < filterItems.Count; i++) {
                        int itemType = filterItems[i];
                        if (itemType <= ItemID.None) continue;

                        float currentAngle = angleIncrement * i - MathHelper.PiOver2;
                        Vector2 offset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * currentRadius;
                        Vector2 itemPos = drawCenter + offset;

                        Color drawColor = VaultUtils.MultiStepColorLerp(hoverSengs, Lighting.GetColor(Position.ToPoint()), Color.White);
                        float scale = hoverSengs * 1.25f;

                        VaultUtils.SafeLoadItem(itemType);
                        VaultUtils.SimpleDrawItem(Main.spriteBatch, itemType, itemPos, itemWidth: 32, scale, 0, drawColor);
                    }
                }
            }
            if (ItemFilter.Alives()) {
                VaultUtils.SimpleDrawItem(Main.spriteBatch, ItemFilter.type, CenterInWorld - Main.screenPosition, itemWidth: 32, 1f, 0, Color.White);
            }
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            Item item = player.GetItem();
            if (item.type == ModContent.ItemType<ItemFilter>()) {
                ItemFilter = item.Clone();
                //深拷贝过滤数据
                var sourceData = item.GetGlobalItem<ItemFilterData>();
                var targetData = ItemFilter.GetGlobalItem<ItemFilterData>();
                targetData.SetItems(sourceData.Items);

                SoundEngine.PlaySound(CWRSound.Select);

                if (Main.netMode != NetmodeID.SinglePlayer) {
                    SendData();
                }
                return true;
            }

            //右键打开专属箱子UI
            if (player.whoAmI == Main.myPlayer) {
                OceanRaidersUI.Instance.Interactive(this);
            }
            return true;
        }
    }
}