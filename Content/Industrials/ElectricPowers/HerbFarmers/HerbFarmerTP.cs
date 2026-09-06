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
    /// <summary>草药农场机作业状态:权威端推算,随全量包下发,面板状态灯与机身状态灯共用</summary>
    internal enum HerbFarmState : byte
    {
        /// <summary>手动停用</summary>
        Off,
        /// <summary>缺电</summary>
        NoPower,
        /// <summary>近期有播种或收割动作</summary>
        Working,
        /// <summary>田里有成熟株,等它进入花期</summary>
        WaitingBloom,
        /// <summary>田里只有幼苗,等它长成</summary>
        Growing,
        /// <summary>有开花株却装不进产出仓</summary>
        ProduceFull,
        /// <summary>种子仓空,田里也没有草药</summary>
        NoSeeds,
        /// <summary>有种子,但范围内没有这些种子能种的土壤</summary>
        NoSoil,
        /// <summary>有种子有土,本轮却没种成(落点被液体占等),下轮再试</summary>
        Idle,
    }

    /// <summary>
    /// 草药农场机TP:消耗种子在范围内的合法土壤上播种,收割开花期草药进产出仓。<br/>
    /// 开花判定沿用原版 <see cref="WorldGen.IsHarvestableHerbWithSeed"/>:1.4 里多数草药开花并不改物块类型,
    /// 成熟株(83)按时段与天气实时判花,只有闪耀根与寒颤棘会真正翻成开花块(84);
    /// 所以缓存要收全部成熟株,收割时再逐株判花,只盯 84 的话五种草药永远收不到。<br/>
    /// 落点扫描与物块改动仅权威端执行(主线程经 Defer),动作演出经修订号搭同一份全量包广播给客户端
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
        //每种草药各自保留的最近落点数:机器脚下一种土壤再多,也挤不掉别的种子的落点
        private const int SpotsPerStyle = 40;
        //成熟株缓存上限
        private const int MaxHerbSpots = 200;
        //一次动作后"耕作中"状态的保持时长
        private const int WorkingHold = 150;
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

        /// <summary>某种土壤能种哪些样式的草药,按位对应 <see cref="HerbTable"/> 索引;镜像原版 PlaceAlch 的土壤表</summary>
        internal static int SoilStyleMask(ushort soil) => soil switch {
            TileID.ClayPot or TileID.PlanterBox => 0b111_1111,
            TileID.Grass or TileID.HallowedGrass or TileID.GolfGrass or TileID.GolfGrassHallowed => 1 << 0,
            TileID.JungleGrass => 1 << 1,
            TileID.Dirt or TileID.Mud => 1 << 2,
            TileID.CorruptGrass or TileID.Ebonstone or TileID.CrimsonGrass or TileID.Crimstone
                or TileID.CorruptJungleGrass or TileID.CrimsonJungleGrass => 1 << 3,
            TileID.Sand or TileID.Pearlsand => 1 << 4,
            TileID.Ash or TileID.AshGrass => 1 << 5,
            TileID.SnowBlock or TileID.IceBlock or TileID.CorruptIce or TileID.HallowedIce or TileID.FleshIce => 1 << 6,
            _ => 0,
        };

        #endregion

        #region 字段

        internal Item[] Seeds = new Item[SeedSlotCount];
        internal Item[] Produce = new Item[ProduceSlotCount];
        internal bool Enabled = true;

        /// <summary>作业状态:权威端每帧推算,变化时随全量包下发</summary>
        internal HerbFarmState State { get; private set; } = HerbFarmState.Idle;
        internal bool IsWorking => State == HerbFarmState.Working;
        internal float GlowIntensity;

        private int plantTimer;
        private int harvestTimer;
        //首帧就扫一遍,别让新放的机器先空转五秒
        private int scanTimer = ScanInterval;
        private int textIdleTime;
        private int workingTimer;
        private int seedRoundRobin;

        /// <summary>落点:空位及其下方土壤能种的样式位</summary>
        private readonly record struct PlantSpot(Point16 Pos, int StyleMask);

        //权威端落点缓存与扫描/挑选用的复用缓冲
        private readonly List<PlantSpot> plantSpots = [];
        private readonly List<PlantSpot> scanBuffer = [];
        private readonly List<int> matchBuffer = [];
        //权威端成熟株缓存(83/84),开花与否收割时逐株实判
        private readonly List<Point16> herbSpots = [];
        //扫描统计:幼苗数、全部落点能种的样式位并集
        private int immatureCount;
        private int plantableMask;
        //上轮收割有开花株因产出仓装不下而搁置
        private bool harvestBlocked;

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
            data.Write((byte)State);
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
            //权威端收到客户端面板改动时会被旧状态覆盖一帧,下帧推算即纠回
            State = (HerbFarmState)reader.ReadByte();
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
                    seedTags.Add(CWRSaveData.SaveItemTag(Seeds[i] ?? new Item()));
                }
                tag["_Seeds"] = seedTags;
                List<TagCompound> produceTags = [];
                for (int i = 0; i < ProduceSlotCount; i++) {
                    produceTags.Add(CWRSaveData.SaveItemTag(Produce[i] ?? new Item()));
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
                if (tag.TrySafeGet("_Enabled", out bool enabled)) {
                    Enabled = enabled;
                }
                if (tag.TrySafeGet("_Seeds", out List<TagCompound> seedTags)) {
                    for (int i = 0; i < SeedSlotCount && i < seedTags.Count; i++) {
                        Seeds[i] = CWRSaveData.LoadItemTag(seedTags[i], $"{nameof(HerbFarmerTP)}:_Seeds");
                    }
                }
                if (tag.TrySafeGet("_Produce", out List<TagCompound> produceTags)) {
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

        /// <summary>刷新落点、成熟株与幼苗统计;只读物块,并行阶段安全</summary>
        private void ScanWorkArea() {
            herbSpots.Clear();
            scanBuffer.Clear();
            immatureCount = 0;

            Vector2 center = CenterInWorld;
            Point centerTile = center.ToTileCoordinates();
            int radiusTiles = (int)(WorkRadius / 16f);
            float radiusSQ = WorkRadius * WorkRadius;

            for (int x = centerTile.X - radiusTiles; x <= centerTile.X + radiusTiles; x++) {
                for (int y = centerTile.Y - radiusTiles; y <= centerTile.Y + radiusTiles; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    if (Vector2.DistanceSquared(center, new Vector2(x * 16 + 8, y * 16 + 8)) > radiusSQ) {
                        continue;
                    }

                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile) {
                        if (tile.TileType == TileID.MatureHerbs || tile.TileType == TileID.BloomingHerbs) {
                            if (herbSpots.Count < MaxHerbSpots) {
                                herbSpots.Add(new Point16(x, y));
                            }
                        }
                        else if (tile.TileType == TileID.ImmatureHerbs) {
                            immatureCount++;
                        }
                        continue;
                    }

                    //空位:下方是草药土壤才值得记;液体等易变条件留给 PlaceTile 复核
                    Tile below = Main.tile[x, y + 1];
                    if (!below.HasTile || below.IsHalfBlock || below.Slope != 0) {
                        continue;
                    }
                    int mask = SoilStyleMask(below.TileType);
                    if (mask != 0) {
                        scanBuffer.Add(new PlantSpot(new Point16(x, y), mask));
                    }
                }
            }

            //按距离排序后逐样式限量收录
            scanBuffer.Sort((a, b) => Vector2.DistanceSquared(center, a.Pos.ToWorldCoordinates())
                .CompareTo(Vector2.DistanceSquared(center, b.Pos.ToWorldCoordinates())));
            plantSpots.Clear();
            plantableMask = 0;
            int[] perStyle = new int[HerbTable.Length];
            foreach (PlantSpot spot in scanBuffer) {
                bool wanted = false;
                for (int s = 0; s < HerbTable.Length; s++) {
                    if ((spot.StyleMask & (1 << s)) != 0 && perStyle[s] < SpotsPerStyle) {
                        wanted = true;
                        break;
                    }
                }
                if (!wanted) {
                    continue;
                }
                for (int s = 0; s < HerbTable.Length; s++) {
                    if ((spot.StyleMask & (1 << s)) != 0) {
                        perStyle[s]++;
                    }
                }
                plantSpots.Add(spot);
                plantableMask |= spot.StyleMask;
            }
            scanBuffer.Clear();
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
            if (workingTimer > 0) {
                workingTimer--;
            }

            //State 入包同步,MP 客户端的机身辉光也能跟上
            GlowIntensity = IsWorking
                ? Math.Min(1f, GlowIntensity + 0.04f)
                : Math.Max(0f, GlowIntensity - 0.02f);

            if (VaultUtils.isClient) {
                return;
            }

            if (!Enabled) {
                SetState(HerbFarmState.Off);
                return;
            }

            //定期刷新工作区缓存
            if (++scanTimer >= ScanInterval) {
                scanTimer = 0;
                ScanWorkArea();
            }

            //收割:开花株结算进产出仓
            if (++harvestTimer >= HarvestInterval) {
                harvestTimer = 0;
                TryHarvest();
            }

            //播种:轮询种子槽找落点
            if (++plantTimer >= PlantInterval) {
                plantTimer = 0;
                TryPlantSeed();
            }

            SetState(ComputeState());
        }

        private void SetState(HerbFarmState state) {
            if (State == state) {
                return;
            }
            State = state;
            netDirty = true;
        }

        /// <summary>按优先级推算作业状态:先讲阻塞原因,再讲等待原因</summary>
        private HerbFarmState ComputeState() {
            if (MachineData.UEvalue < PlantCost) {
                return HerbFarmState.NoPower;
            }
            if (workingTimer > 0) {
                return HerbFarmState.Working;
            }
            if (harvestBlocked) {
                return HerbFarmState.ProduceFull;
            }
            if (herbSpots.Count > 0) {
                return HerbFarmState.WaitingBloom;
            }
            if (immatureCount > 0) {
                return HerbFarmState.Growing;
            }
            int seedMask = LoadedSeedMask();
            if (seedMask == 0) {
                return HerbFarmState.NoSeeds;
            }
            if ((seedMask & plantableMask) == 0) {
                return HerbFarmState.NoSoil;
            }
            return HerbFarmState.Idle;
        }

        private int LoadedSeedMask() {
            int mask = 0;
            foreach (Item seed in Seeds) {
                if (IsHerbSeed(seed)) {
                    mask |= 1 << GetStyleForSeed(seed.type);
                }
            }
            return mask;
        }

        /// <summary>本轮要种的种子槽:从轮询位起找第一格有可种落点的种子,既不只烧第一格,也不让没土的种子卡住别的</summary>
        private int PickSeedSlot() {
            for (int step = 0; step < SeedSlotCount; step++) {
                int index = (seedRoundRobin + step) % SeedSlotCount;
                int style = IsHerbSeed(Seeds[index]) ? GetStyleForSeed(Seeds[index].type) : -1;
                if (style < 0 || (plantableMask & (1 << style)) == 0) {
                    continue;
                }
                seedRoundRobin = (index + 1) % SeedSlotCount;
                return index;
            }
            return -1;
        }

        private void TryPlantSeed() {
            int slot = PickSeedSlot();
            if (slot < 0) {
                return;
            }

            if (MachineData.UEvalue < PlantCost) {
                PromptNoEnergy();
                return;
            }

            int style = GetStyleForSeed(Seeds[slot].type);

            //物块写入与种子消耗都在主线程做;只在这种种子能种的落点里随机试,种上为止
            Defer(() => {
                if (MachineData.UEvalue < PlantCost || !IsHerbSeed(Seeds[slot]) || GetStyleForSeed(Seeds[slot].type) != style) {
                    return;
                }

                matchBuffer.Clear();
                for (int i = 0; i < plantSpots.Count; i++) {
                    if ((plantSpots[i].StyleMask & (1 << style)) != 0) {
                        matchBuffer.Add(i);
                    }
                }

                int tries = Math.Min(MaxPlantTries, matchBuffer.Count);
                for (int i = 0; i < tries; i++) {
                    int pick = Main.rand.Next(matchBuffer.Count);
                    int spotIndex = matchBuffer[pick];
                    matchBuffer.RemoveAt(pick);
                    Point16 spot = plantSpots[spotIndex].Pos;

                    //已被占(自己刚种的/玩家放的),等下次扫描剔除
                    if (Main.tile[spot.X, spot.Y].HasTile) {
                        continue;
                    }

                    //原版 PlaceTile 自带该样式的土壤与液体校验,失败无副作用
                    if (!WorldGen.PlaceTile(spot.X, spot.Y, TileID.ImmatureHerbs, true, false, -1, style)) {
                        continue;
                    }

                    Seeds[slot].stack--;
                    if (Seeds[slot].stack <= 0) {
                        Seeds[slot].TurnToAir();
                    }
                    MachineData.UEvalue -= PlantCost;
                    plantSpots.RemoveAt(spotIndex);
                    immatureCount++;

                    if (VaultUtils.isServer) {
                        NetMessage.SendTileSquare(-1, spot.X, spot.Y, 1);
                    }
                    CommitAction(1, spot);
                    return;
                }
            });
        }

        private void TryHarvest() {
            if (herbSpots.Count == 0) {
                harvestBlocked = false;
                return;
            }

            if (MachineData.UEvalue < HarvestCost) {
                PromptNoEnergy();
                return;
            }

            //物块读写与产出结算都在主线程做
            Defer(() => {
                if (MachineData.UEvalue < HarvestCost) {
                    return;
                }

                bool blocked = false;
                for (int i = 0; i < herbSpots.Count; i++) {
                    Point16 spot = herbSpots[i];
                    Tile tile = Main.tile[spot.X, spot.Y];
                    int style = tile.TileFrameX / 18;

                    //株已不在(玩家收了/被踩掉),剔出缓存
                    if (!tile.HasTile
                        || (tile.TileType != TileID.MatureHerbs && tile.TileType != TileID.BloomingHerbs)
                        || style < 0 || style >= HerbTable.Length) {
                        herbSpots.RemoveAt(i--);
                        continue;
                    }

                    //成熟但没到花期,留在地里等;判定与原版收割掉种子的条件完全一致
                    if (!WorldGen.IsHarvestableHerbWithSeed(tile.TileType, style)) {
                        continue;
                    }

                    (int seedType, int herbType) = HerbTable[style];

                    //产出仓装不下这种草药就先不收,留在地里
                    if (!CanInsert(Produce, herbType)) {
                        blocked = true;
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
                    herbSpots.RemoveAt(i);
                    harvestBlocked = false;
                    CommitAction(2, spot);
                    return;
                }

                harvestBlocked = blocked;
                if (blocked) {
                    Prompt(HerbFarmer.FullText.Value);
                }
            });
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

        private void PromptNoEnergy() => Prompt(HerbFarmer.NoEnergyText.Value);

        private void Prompt(string text) {
            if (textIdleTime > 0) {
                return;
            }
            textIdleTime = 300;
            //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
            Defer(() => CombatText.NewText(HitBox, HerbFarmer.Tint, text));
        }

        /// <summary>登记一次动作:本端立即播演出,修订号随全量包带给客户端补播</summary>
        private void CommitAction(byte kind, Point16 pos) {
            actionKind = kind;
            actionPos = pos;
            actionRevision++;
            netDirty = true;
            workingTimer = WorkingHold;
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

        /// <summary>机身状态灯:与缺电压暗互补,把"为什么不动"编码在灯色上;落在底座右侧的金属横带上</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            FarmLampState lamp;
            if (Disabled || State == HerbFarmState.Off) {
                lamp = FarmLampState.Off;
            }
            else {
                lamp = State switch {
                    HerbFarmState.NoPower => FarmLampState.NoPower,
                    HerbFarmState.Working => FarmLampState.Working,
                    HerbFarmState.ProduceFull or HerbFarmState.NoSeeds or HerbFarmState.NoSoil => FarmLampState.MissingResource,
                    _ => FarmLampState.Idle,
                };
            }
            FarmStatusLamp.Draw(spriteBatch, PosInWorld + new Vector2(Width - 9f, Height - 12f), lamp, HerbFarmer.Tint, WhoAmI);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }

        #endregion
    }
}
