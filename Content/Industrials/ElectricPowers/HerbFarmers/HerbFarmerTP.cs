using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.HerbFarmers
{
    /// <summary>
    /// 草药农场机TP:消耗种子在范围内的合法土壤上播种,收割开花期草药进产出仓。<br/>
    /// 落点扫描与物块改动仅权威端执行(主线程经 Defer),
    /// 动作演出经修订号搭同一份全量包广播给客户端
    /// </summary>
    internal class HerbFarmerTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<HerbFarmerTile>();
        public override int TargetItem => ModContent.ItemType<HerbFarmer>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 500;
        /// <summary>全量包携带12格物品数据,放宽锚定节奏</summary>
        public override int NetAnchorIntervalTicks => 600;

        #region 常量与草药表

        internal const int SeedSlotCount = 4;
        internal const int ProduceSlotCount = 8;
        /// <summary>作业半径(像素)</summary>
        internal const float WorkRadius = 800f;
        internal const float PlantCost = 3f;
        internal const float HarvestCost = 2f;
        private const int PlantInterval = 90;
        private const int HarvestInterval = 60;
        private const int ScanInterval = 300;
        //单轮播种尝试的落点数上限
        private const int MaxPlantTries = 24;
        //缓存落点上限
        private const int MaxCachedSpots = 80;
        //账本合批同步节流
        private const int NetInterval = 30;

        /// <summary>草药样式表,索引与原版草药瓦片 style 一致</summary>
        internal static readonly (int seedType, int herbType)[] HerbTable = [
            (ItemID.DaybloomSeeds, ItemID.Daybloom),
            (ItemID.MoonglowSeeds, ItemID.Moonglow),
            (ItemID.BlinkrootSeeds, ItemID.Blinkroot),
            (ItemID.DeathweedSeeds, ItemID.Deathweed),
            (ItemID.WaterleafSeeds, ItemID.Waterleaf),
            (ItemID.FireblossomSeeds, ItemID.Fireblossom),
            (ItemID.ShiverthornSeeds, ItemID.Shiverthorn),
        ];

        internal static bool IsHerbSeed(Item item) => item != null && !item.IsAir && GetStyleForSeed(item.type) >= 0;

        internal static int GetStyleForSeed(int itemType) {
            for (int i = 0; i < HerbTable.Length; i++) {
                if (HerbTable[i].seedType == itemType) {
                    return i;
                }
            }
            return -1;
        }

        #endregion

        #region 字段

        internal Item[] Seeds = new Item[SeedSlotCount];
        internal Item[] Produce = new Item[ProduceSlotCount];
        internal bool Enabled = true;

        internal bool IsWorking { get; private set; }
        internal float GlowIntensity;

        private int plantTimer;
        private int harvestTimer;
        private int scanTimer;
        private int textIdleTime;
        private int seedRoundRobin;

        //权威端落点缓存
        private readonly List<Point16> plantSpots = [];
        private readonly List<Point16> bloomSpots = [];

        //动作演出:修订号变化时客户端播放对应效果
        private Point16 actionPos;
        private byte actionRevision;
        private byte actionKind;

        private bool netDirty;
        private int netCooldown;

        #endregion

        public override void SetBattery() {
            EnsureSlots();
        }

        public override void Initialize() {
            EnsureSlots();
        }

        private void EnsureSlots() {
            Seeds ??= new Item[SeedSlotCount];
            Produce ??= new Item[ProduceSlotCount];
            for (int i = 0; i < SeedSlotCount; i++) {
                Seeds[i] ??= new Item();
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                Produce[i] ??= new Item();
            }
        }

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(Enabled);
            data.Write(actionRevision);
            data.Write(actionKind);
            data.Write(actionPos.X);
            data.Write(actionPos.Y);
            for (int i = 0; i < SeedSlotCount; i++) {
                ItemIO.Send(Seeds[i] ?? new Item(), data, true);
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                ItemIO.Send(Produce[i] ?? new Item(), data, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            EnsureSlots();
            Enabled = reader.ReadBoolean();
            byte newRevision = reader.ReadByte();
            byte newKind = reader.ReadByte();
            Point16 newPos = new(reader.ReadInt16(), reader.ReadInt16());
            for (int i = 0; i < SeedSlotCount; i++) {
                Seeds[i] = ItemIO.Receive(reader, true);
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                Produce[i] = ItemIO.Receive(reader, true);
            }

            //修订号推进才播演出,入世快照不播(防止加入时补播旧动作)
            if (!TileProcessorNetWork.InitializeWorld && newRevision != actionRevision && newPos != Point16.Zero) {
                PlayActionEffect(newKind, newPos);
            }
            actionRevision = newRevision;
            actionKind = newKind;
            actionPos = newPos;
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            try {
                tag["_Enabled"] = Enabled;
                List<TagCompound> seedTags = [];
                for (int i = 0; i < SeedSlotCount; i++) {
                    seedTags.Add(ItemIO.Save(Seeds[i] ?? new Item()));
                }
                tag["_Seeds"] = seedTags;
                List<TagCompound> produceTags = [];
                for (int i = 0; i < ProduceSlotCount; i++) {
                    produceTags.Add(ItemIO.Save(Produce[i] ?? new Item()));
                }
                tag["_Produce"] = produceTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"HerbFarmerTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                EnsureSlots();
                if (tag.TryGet("_Enabled", out bool enabled)) {
                    Enabled = enabled;
                }
                if (tag.TryGet("_Seeds", out List<TagCompound> seedTags)) {
                    for (int i = 0; i < SeedSlotCount && i < seedTags.Count; i++) {
                        Seeds[i] = CWRSaveData.LoadItemTag(seedTags[i], $"{nameof(HerbFarmerTP)}:_Seeds");
                    }
                }
                if (tag.TryGet("_Produce", out List<TagCompound> produceTags)) {
                    for (int i = 0; i < ProduceSlotCount && i < produceTags.Count; i++) {
                        Produce[i] = CWRSaveData.LoadItemTag(produceTags[i], $"{nameof(HerbFarmerTP)}:_Produce");
                    }
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"HerbFarmerTP.LoadData Error: {ex.Message}");
            }
        }

        /// <summary>槽位被UI/管道改动后调用:权威端下次节流推送合并纠偏</summary>
        internal void MarkDirty() => netDirty = true;

        #endregion

        #region 扫描

        /// <summary>刷新落点与开花缓存;只读物块,并行阶段安全</summary>
        private void ScanWorkArea() {
            plantSpots.Clear();
            bloomSpots.Clear();

            int radiusTiles = (int)(WorkRadius / 16f);
            int centerX = Position.X + 1;
            int centerY = Position.Y + 1;
            float radiusSQ = WorkRadius * WorkRadius;

            for (int x = centerX - radiusTiles; x <= centerX + radiusTiles; x++) {
                for (int y = centerY - radiusTiles; y <= centerY + radiusTiles; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    float distSQ = Vector2.DistanceSquared(CenterInWorld, new Vector2(x * 16 + 8, y * 16 + 8));
                    if (distSQ > radiusSQ) {
                        continue;
                    }

                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile) {
                        if (tile.TileType == TileID.BloomingHerbs) {
                            bloomSpots.Add(new Point16(x, y));
                        }
                        continue;
                    }

                    //空位:下方有实心块才值得试种,土质合法性交给 PlaceTile 复核
                    Tile below = Main.tile[x, y + 1];
                    if (below.HasTile && !below.IsHalfBlock && below.Slope == 0
                        && Main.tileSolid[below.TileType] && plantSpots.Count < MaxCachedSpots * 4) {
                        plantSpots.Add(new Point16(x, y));
                    }
                }
            }

            //落点太多时保留离机器最近的一批
            if (plantSpots.Count > MaxCachedSpots) {
                plantSpots.Sort((a, b) =>
                    Vector2.DistanceSquared(CenterInWorld, a.ToWorldCoordinates())
                    .CompareTo(Vector2.DistanceSquared(CenterInWorld, b.ToWorldCoordinates())));
                plantSpots.RemoveRange(MaxCachedSpots, plantSpots.Count - MaxCachedSpots);
            }
        }

        #endregion

        #region 更新逻辑

        public override void UpdateMachine() {
            //权威端节流刷新槽位与演出
            if (netCooldown > 0) {
                netCooldown--;
            }
            if (netDirty && netCooldown <= 0 && VaultUtils.isServer) {
                netDirty = false;
                netCooldown = NetInterval;
                SendData();
            }
            if (textIdleTime > 0) {
                textIdleTime--;
            }

            GlowIntensity = IsWorking
                ? Math.Min(1f, GlowIntensity + 0.04f)
                : Math.Max(0f, GlowIntensity - 0.02f);

            if (!Enabled) {
                IsWorking = false;
                return;
            }

            bool authority = !VaultUtils.isClient;
            if (!authority) {
                return;
            }

            //定期刷新工作区缓存
            if (++scanTimer >= ScanInterval) {
                scanTimer = 0;
                ScanWorkArea();
            }

            bool actedRecently = false;

            //收割:开花草药结算进产出仓
            if (++harvestTimer >= HarvestInterval) {
                harvestTimer = 0;
                if (TryHarvestBloom()) {
                    actedRecently = true;
                }
            }

            //播种:轮询种子槽找落点
            if (++plantTimer >= PlantInterval) {
                plantTimer = 0;
                if (TryPlantSeed()) {
                    actedRecently = true;
                }
            }

            if (actedRecently) {
                IsWorking = true;
            }
            else if (plantSpots.Count == 0 && bloomSpots.Count == 0) {
                IsWorking = false;
            }
        }

        /// <summary>本轮要种的种子槽,轮询防止只烧第一格</summary>
        private int PickSeedSlot() {
            for (int step = 0; step < SeedSlotCount; step++) {
                int index = (seedRoundRobin + step) % SeedSlotCount;
                if (IsHerbSeed(Seeds[index])) {
                    seedRoundRobin = (index + 1) % SeedSlotCount;
                    return index;
                }
            }
            return -1;
        }

        private bool TryPlantSeed() {
            int slot = PickSeedSlot();
            if (slot < 0 || plantSpots.Count == 0) {
                return false;
            }

            if (MachineData.UEvalue < PlantCost) {
                PromptNoEnergy();
                return false;
            }

            int style = GetStyleForSeed(Seeds[slot].type);
            if (style < 0) {
                return false;
            }

            //物块写入与种子消耗都在主线程做;落点从缓存随机试,种上为止
            Defer(() => {
                if (MachineData.UEvalue < PlantCost || !IsHerbSeed(Seeds[slot]) || GetStyleForSeed(Seeds[slot].type) != style) {
                    return;
                }

                int tries = Math.Min(MaxPlantTries, plantSpots.Count);
                for (int i = 0; i < tries; i++) {
                    int spotIndex = Main.rand.Next(plantSpots.Count);
                    Point16 spot = plantSpots[spotIndex];

                    Tile tile = Main.tile[spot.X, spot.Y];
                    if (tile.HasTile) {
                        plantSpots.RemoveAt(spotIndex);
                        if (plantSpots.Count == 0) {
                            return;
                        }
                        continue;
                    }

                    //原版 PlaceTile 自带该草药样式的土壤合法性校验,失败无副作用
                    if (!WorldGen.PlaceTile(spot.X, spot.Y, TileID.ImmatureHerbs, true, false, -1, style)) {
                        continue;
                    }

                    Seeds[slot].stack--;
                    if (Seeds[slot].stack <= 0) {
                        Seeds[slot].TurnToAir();
                    }
                    MachineData.UEvalue -= PlantCost;
                    plantSpots.RemoveAt(spotIndex);

                    if (VaultUtils.isServer) {
                        NetMessage.SendTileSquare(-1, spot.X, spot.Y, 1);
                    }
                    CommitAction(1, spot);
                    return;
                }
            });
            return true;
        }

        private bool TryHarvestBloom() {
            if (bloomSpots.Count == 0) {
                return false;
            }

            if (MachineData.UEvalue < HarvestCost) {
                PromptNoEnergy();
                return false;
            }

            //物块读写与掉落结算都在主线程做
            Defer(() => {
                if (MachineData.UEvalue < HarvestCost) {
                    return;
                }

                while (bloomSpots.Count > 0) {
                    Point16 spot = bloomSpots[0];
                    bloomSpots.RemoveAt(0);

                    Tile tile = Main.tile[spot.X, spot.Y];
                    if (!tile.HasTile || tile.TileType != TileID.BloomingHerbs) {
                        continue;
                    }

                    int style = tile.TileFrameX / 18;
                    if (style < 0 || style >= HerbTable.Length) {
                        continue;
                    }

                    (int seedType, int herbType) = HerbTable[style];

                    //产出仓装不下草药就先不收,留在地里
                    if (!CanInsert(Produce, herbType)) {
                        continue;
                    }

                    //镜像原版开花收割:草药x1 + 种子1~3
                    int seedCount = Main.rand.Next(1, 4);
                    InsertItem(Produce, herbType, 1);
                    //种子优先回填种子槽,自持运转;溢出进产出仓,再溢出落地
                    int remain = InsertItem(Seeds, seedType, seedCount);
                    if (remain > 0) {
                        remain = InsertItem(Produce, seedType, remain);
                    }
                    if (remain > 0) {
                        DropItem(new Item(seedType, remain));
                    }

                    MachineData.UEvalue -= HarvestCost;
                    WorldGen.KillTile(spot.X, spot.Y, false, false, true);
                    if (VaultUtils.isServer) {
                        NetMessage.SendTileSquare(-1, spot.X, spot.Y, 1);
                    }
                    CommitAction(2, spot);
                    return;
                }
            });
            return true;
        }

        private static bool CanInsert(Item[] slots, int itemType) {
            foreach (Item slot in slots) {
                if (slot == null || slot.IsAir) {
                    return true;
                }
                if (slot.type == itemType && slot.stack < slot.maxStack) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>往槽组塞物品,返回没塞下的数量</summary>
        private static int InsertItem(Item[] slots, int itemType, int count) {
            //先叠同类
            foreach (Item slot in slots) {
                if (count <= 0) {
                    return 0;
                }
                if (slot == null || slot.IsAir || slot.type != itemType || slot.stack >= slot.maxStack) {
                    continue;
                }
                int add = Math.Min(count, slot.maxStack - slot.stack);
                slot.stack += add;
                count -= add;
            }
            //再开新槽
            for (int i = 0; i < slots.Length && count > 0; i++) {
                if (slots[i] != null && !slots[i].IsAir) {
                    continue;
                }
                slots[i] = new Item(itemType, count);
                count = 0;
            }
            return count;
        }

        private void PromptNoEnergy() {
            if (textIdleTime > 0) {
                return;
            }
            textIdleTime = 300;
            //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
            Defer(() => CombatText.NewText(HitBox, HerbFarmer.Tint, HerbFarmer.NoEnergyText.Value));
        }

        /// <summary>登记一次动作:本端立即播演出,修订号随全量包带给客户端补播</summary>
        private void CommitAction(byte kind, Point16 pos) {
            actionKind = kind;
            actionPos = pos;
            actionRevision++;
            netDirty = true;
            IsWorking = true;
            PlayActionEffect(kind, pos);
        }

        /// <summary>动作演出:机器到落点的绿色粒子飞线 + 落点迸叶;主线程调用</summary>
        internal void PlayActionEffect(byte kind, Point16 pos) {
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 target = pos.ToWorldCoordinates(8, 8);
            Vector2 source = CenterInWorld + new Vector2(0, -10);
            float distance = source.Distance(target);
            int beamPoints = (int)MathHelper.Clamp(distance / 14f, 6f, 42f);

            for (int i = 0; i < beamPoints; i++) {
                float t = i / (float)beamPoints;
                Vector2 dustPos = Vector2.Lerp(source, target, t);
                //轻微下垂弧线,像被抛出的种子/收割光束
                dustPos.Y += MathF.Sin(t * MathHelper.Pi) * distance * 0.06f;
                Dust dust = Dust.NewDustPerfect(dustPos, DustID.GrassBlades,
                    Main.rand.NextVector2Circular(0.3f, 0.3f), 120, default, 0.9f);
                dust.noGravity = true;
            }

            int burstType = kind == 1 ? DustID.JungleGrass : DustID.GrassBlades;
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(target, burstType,
                    Main.rand.NextVector2Circular(1.6f, 1.6f) - new Vector2(0, 1f), 80, default, 1.1f);
                dust.noGravity = kind == 1;
            }

            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = kind == 1 ? 0.2f : -0.1f }, target);
        }

        #endregion

        #region 交互/销毁/绘制

        public void OpenUI() {
            var ui = UIHandleLoader.GetUIHandleOfType<HerbFarmerUI>();
            ui?.Interactive(this);
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            //倒出全部种子与产出
            for (int i = 0; i < SeedSlotCount; i++) {
                if (Seeds[i] != null && !Seeds[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Seeds[i]);
                    Seeds[i] = new Item();
                }
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                if (Produce[i] != null && !Produce[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Produce[i]);
                    Produce[i] = new Item();
                }
            }

            //掉落机器本身(带能量)
            Item farmerItem = new Item(ModContent.ItemType<HerbFarmer>());
            farmerItem.CWR().UEValue = MachineData.UEvalue;
            int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, farmerItem);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }

        #endregion
    }
}
