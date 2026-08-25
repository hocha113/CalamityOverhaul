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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MushroomFarmers
{
    /// <summary>
    /// 蘑菇农场机TP:范围内的草地播普通蘑菇,蘑菇草播发光蘑菇,采收入仓。<br/>
    /// 蘑菇没有种子物品,播种输入换成地表类型扫描;蘑菇即种即收,产率由播种节拍限制。<br/>
    /// 物块改动与采收掷骰仅权威端执行(主线程经 Defer),动作演出经修订号搭全量包广播
    /// </summary>
    internal class MushroomFarmerTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<MushroomFarmerTile>();
        public override int TargetItem => ModContent.ItemType<MushroomFarmer>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 500;
        /// <summary>全量包携带8格物品数据,放宽锚定节奏</summary>
        public override int NetAnchorIntervalTicks => 600;

        #region 常量

        internal const int ProduceSlotCount = 8;
        /// <summary>作业半径(像素)</summary>
        internal const float WorkRadius = 800f;
        internal const float PlantCost = 3f;
        internal const float HarvestCost = 2f;
        /// <summary>蘑菇即种即收,节拍即产率:约10秒培育一株</summary>
        private const int PlantInterval = 600;
        private const int HarvestInterval = 300;
        private const int ScanInterval = 300;
        //单轮播种尝试的落点数上限
        private const int MaxPlantTries = 24;
        //缓存落点上限
        private const int MaxCachedSpots = 80;
        //账本合批同步节流
        private const int NetInterval = 30;
        /// <summary>普通蘑菇的植株帧(tile 3 frameX 144)</summary>
        private const short MushroomFrameX = 144;

        #endregion

        #region 字段

        internal Item[] Produce = new Item[ProduceSlotCount];
        internal bool Enabled = true;

        internal bool IsWorking { get; private set; }
        internal float GlowIntensity;

        private int plantTimer;
        private int harvestTimer;
        private int scanTimer;
        private int textIdleTime;

        //权威端落点缓存:草地位/蘑菇草位/可采收位
        private readonly List<Point16> grassSpots = [];
        private readonly List<Point16> mushGrassSpots = [];
        private readonly List<Point16> harvestSpots = [];

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
            Produce ??= new Item[ProduceSlotCount];
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
                List<TagCompound> produceTags = [];
                for (int i = 0; i < ProduceSlotCount; i++) {
                    produceTags.Add(ItemIO.Save(Produce[i] ?? new Item()));
                }
                tag["_Produce"] = produceTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"MushroomFarmerTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                EnsureSlots();
                if (tag.TryGet("_Enabled", out bool enabled)) {
                    Enabled = enabled;
                }
                if (tag.TryGet("_Produce", out List<TagCompound> produceTags)) {
                    for (int i = 0; i < ProduceSlotCount && i < produceTags.Count; i++) {
                        Produce[i] = CWRSaveData.LoadItemTag(produceTags[i], $"{nameof(MushroomFarmerTP)}:_Produce");
                    }
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"MushroomFarmerTP.LoadData Error: {ex.Message}");
            }
        }

        /// <summary>槽位被UI/管道改动后调用:权威端下次节流推送合并纠偏</summary>
        internal void MarkDirty() => netDirty = true;

        #endregion

        #region 扫描

        /// <summary>刷新落点与采收缓存;只读物块,并行阶段安全</summary>
        private void ScanWorkArea() {
            grassSpots.Clear();
            mushGrassSpots.Clear();
            harvestSpots.Clear();

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
                        //可采收:普通蘑菇(tile3 蘑菇帧)或发光蘑菇植株
                        if ((tile.TileType == TileID.Plants && tile.TileFrameX == MushroomFrameX)
                            || tile.TileType == TileID.MushroomPlants) {
                            harvestSpots.Add(new Point16(x, y));
                        }
                        continue;
                    }

                    //空位:按下方地表类型分类,合法性由 SquareTileFrame 的植株复核兜底
                    Tile below = Main.tile[x, y + 1];
                    if (!below.HasTile || below.IsHalfBlock || below.Slope != 0) {
                        continue;
                    }
                    if (below.TileType == TileID.Grass || below.TileType == TileID.GolfGrass) {
                        if (grassSpots.Count < MaxCachedSpots) {
                            grassSpots.Add(new Point16(x, y));
                        }
                    }
                    else if (below.TileType == TileID.MushroomGrass) {
                        if (mushGrassSpots.Count < MaxCachedSpots) {
                            mushGrassSpots.Add(new Point16(x, y));
                        }
                    }
                }
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

            //采收:成熟蘑菇结算进产出仓
            if (++harvestTimer >= HarvestInterval) {
                harvestTimer = 0;
                if (TryHarvest()) {
                    actedRecently = true;
                }
            }

            //播种:草地播普通蘑菇,蘑菇草播发光蘑菇
            if (++plantTimer >= PlantInterval) {
                plantTimer = 0;
                if (TryPlant()) {
                    actedRecently = true;
                }
            }

            if (actedRecently) {
                IsWorking = true;
            }
            else if (grassSpots.Count == 0 && mushGrassSpots.Count == 0 && harvestSpots.Count == 0) {
                IsWorking = false;
            }
        }

        private bool TryPlant() {
            if (grassSpots.Count == 0 && mushGrassSpots.Count == 0) {
                return false;
            }

            if (MachineData.UEvalue < PlantCost) {
                PromptNoEnergy();
                return false;
            }

            //物块写入在主线程做;两类落点轮换,谁有货种谁
            Defer(() => {
                if (MachineData.UEvalue < PlantCost) {
                    return;
                }

                int totalTries = Math.Min(MaxPlantTries, grassSpots.Count + mushGrassSpots.Count);
                for (int i = 0; i < totalTries; i++) {
                    //蘑菇草位优先(发光蘑菇更值钱),没有再种草地
                    bool useMush = mushGrassSpots.Count > 0
                        && (grassSpots.Count == 0 || Main.rand.NextBool());
                    List<Point16> spots = useMush ? mushGrassSpots : grassSpots;
                    if (spots.Count == 0) {
                        spots = useMush ? grassSpots : mushGrassSpots;
                        if (spots.Count == 0) {
                            return;
                        }
                        useMush = !useMush;
                    }

                    int spotIndex = Main.rand.Next(spots.Count);
                    Point16 spot = spots[spotIndex];
                    spots.RemoveAt(spotIndex);

                    Tile tile = Main.tile[spot.X, spot.Y];
                    if (tile.HasTile) {
                        continue;
                    }

                    //手写植株物块:PlaceTile 对 tile3 会随机撒草花,种不出蘑菇帧,
                    //SquareTileFrame 的 PlantCheck 自动复核地基,非法即杀,失败无副作用
                    tile.HasTile = true;
                    tile.IsHalfBlock = false;
                    tile.Slope = SlopeType.Solid;
                    if (useMush) {
                        tile.TileType = TileID.MushroomPlants;
                        tile.TileFrameX = (short)(Main.rand.Next(5) * 18);
                    }
                    else {
                        tile.TileType = TileID.Plants;
                        tile.TileFrameX = MushroomFrameX;
                    }
                    tile.TileFrameY = 0;
                    WorldGen.SquareTileFrame(spot.X, spot.Y);

                    Tile planted = Main.tile[spot.X, spot.Y];
                    if (!planted.HasTile
                        || (planted.TileType != TileID.Plants && planted.TileType != TileID.MushroomPlants)) {
                        continue;
                    }

                    MachineData.UEvalue -= PlantCost;
                    harvestSpots.Add(spot);

                    if (VaultUtils.isServer) {
                        NetMessage.SendTileSquare(-1, spot.X, spot.Y, 1);
                    }
                    CommitAction(1, spot);
                    return;
                }
            });
            return true;
        }

        private bool TryHarvest() {
            if (harvestSpots.Count == 0) {
                return false;
            }

            if (MachineData.UEvalue < HarvestCost) {
                PromptNoEnergy();
                return false;
            }

            if (!HasAnySpace()) {
                Prompt(MushroomFarmer.FullText.Value);
                return false;
            }

            //物块读写与掷骰都在主线程做
            Defer(() => {
                if (MachineData.UEvalue < HarvestCost || !HasAnySpace()) {
                    return;
                }

                while (harvestSpots.Count > 0) {
                    Point16 spot = harvestSpots[0];
                    harvestSpots.RemoveAt(0);

                    Tile tile = Main.tile[spot.X, spot.Y];
                    if (!tile.HasTile) {
                        continue;
                    }

                    int produceType;
                    if (tile.TileType == TileID.Plants && tile.TileFrameX == MushroomFrameX) {
                        //普通蘑菇固定掉一枚
                        produceType = ItemID.Mushroom;
                    }
                    else if (tile.TileType == TileID.MushroomPlants) {
                        //镜像原版发光蘑菇掷骰:1/40 蘑菇草种子,其余一半发光蘑菇,一半空手
                        if (Main.rand.NextBool(40)) {
                            produceType = ItemID.MushroomGrassSeeds;
                        }
                        else if (Main.rand.NextBool()) {
                            produceType = ItemID.GlowingMushroom;
                        }
                        else {
                            produceType = ItemID.None;
                        }
                    }
                    else {
                        continue;
                    }

                    //清株:空手也要清掉,否则采收位一直堵着;空手不扣电不演出
                    WorldGen.KillTile(spot.X, spot.Y, false, false, true);
                    if (VaultUtils.isServer) {
                        NetMessage.SendTileSquare(-1, spot.X, spot.Y, 1);
                    }

                    if (produceType == ItemID.None) {
                        netDirty = true;
                        continue;
                    }

                    int remain = InsertItem(Produce, produceType, 1);
                    if (remain > 0) {
                        DropItem(new Item(produceType, remain));
                    }

                    MachineData.UEvalue -= HarvestCost;
                    CommitAction(2, spot);
                    return;
                }
            });
            return true;
        }

        private bool HasAnySpace() {
            foreach (Item slot in Produce) {
                if (slot == null || slot.IsAir || slot.stack < slot.maxStack) {
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

        private void PromptNoEnergy() => Prompt(MushroomFarmer.NoEnergyText.Value);

        private void Prompt(string text) {
            if (textIdleTime > 0) {
                return;
            }
            textIdleTime = 300;
            //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
            Defer(() => CombatText.NewText(HitBox, MushroomFarmer.Tint, text));
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

        /// <summary>动作演出:机器到落点的孢子飞线 + 落点菌尘迸发;主线程调用</summary>
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
                //轻微下垂弧线,像被抛出的孢子束
                dustPos.Y += MathF.Sin(t * MathHelper.Pi) * distance * 0.06f;
                Dust dust = Dust.NewDustPerfect(dustPos, DustID.BlueFairy,
                    Main.rand.NextVector2Circular(0.3f, 0.3f), 120, default, 0.8f);
                dust.noGravity = true;
            }

            int burstType = kind == 1 ? DustID.BlueFairy : DustID.GlowingMushroom;
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(target, burstType,
                    Main.rand.NextVector2Circular(1.6f, 1.6f) - new Vector2(0, 1f), 80, default, 1.0f);
                dust.noGravity = kind == 1;
            }

            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = kind == 1 ? 0.3f : 0f }, target);
        }

        #endregion

        #region 交互/销毁/绘制

        public void OpenUI() {
            var ui = UIHandleLoader.GetUIHandleOfType<MushroomFarmerUI>();
            ui?.Interactive(this);
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            //倒出全部产出
            for (int i = 0; i < ProduceSlotCount; i++) {
                if (Produce[i] != null && !Produce[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Produce[i]);
                    Produce[i] = new Item();
                }
            }

            //掉落机器本身(带能量)
            Item farmerItem = new Item(ModContent.ItemType<MushroomFarmer>());
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
