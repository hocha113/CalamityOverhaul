using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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

        #region 客户端视觉字段(不入存档不入网络包)

        //田间株位视觉缓存:物块各端已同步,客户端自行扫描,孢子云密度由株数驱动
        private readonly List<Point16> visualShroomSpots = [];
        private readonly List<Point16> visualGlowShroomSpots = [];
        private int visualEmptySpots;
        private int visualScanTimer;
        /// <summary>派生作业强度 0~1,由已同步字段(电量/田况)推出,MP 客户端也成立</summary>
        private float visualWork;
        /// <summary>视觉活跃半径:本地玩家超出此距离不扫描不发粒子</summary>
        private const float VisualRange = WorkRadius + 1000f;

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

            //MP 客户端上 IsWorking 不入包恒为 false,机身辉光改由派生的视觉作业强度驱动
            bool glowWorking = VaultUtils.isClient ? visualWork > 0.5f : IsWorking;
            GlowIntensity = glowWorking
                ? Math.Min(1f, GlowIntensity + 0.04f)
                : Math.Max(0f, GlowIntensity - 0.02f);

            //客户端视觉:田间扫描与孢子云,状态全部由已同步数据派生,零网络
            if (!VaultUtils.isServer) {
                UpdateClientVisual();
            }

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

                //记录本轮第一株空手清掉的位置:若整轮无产出,给它一拍清株反馈
                Point16 clearedEmpty = Point16.Zero;

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
                        if (clearedEmpty == Point16.Zero) {
                            clearedEmpty = spot;
                        }
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

                //整轮都掷了空手:植株不能无声消失,补一拍轻量清株演出
                if (clearedEmpty != Point16.Zero) {
                    CommitAction(3, clearedEmpty);
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

        /// <summary>动作演出:机器到落点的孢子飞线 + 落点反馈;kind 1=播种 2=采收 3=空手清株;主线程调用</summary>
        internal void PlayActionEffect(byte kind, Point16 pos) {
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 target = pos.ToWorldCoordinates(8, 8);
            Vector2 source = CenterInWorld + new Vector2(0, -10);
            float distance = source.Distance(target);
            if (distance < 1f) {
                return;
            }

            //株本体可能已被清掉(采收),读地基判菇种最可靠:蘑菇草=发光蘑菇的蓝辉
            Tile below = Framing.GetTileSafely(pos.X, pos.Y + 1);
            bool glowKind = below.HasTile && below.TileType == TileID.MushroomGrass;
            Color sporeColor = glowKind ? new Color(95, 160, 255) : new Color(226, 220, 205);

            //孢子飞线:沿下垂弧线洒一串孢子,初速取弧线切向;
            //寿命从机器端向落点递增,整条线读作"孢子被送达"
            int points = (int)MathHelper.Clamp(distance / 22f, 6f, 26f);
            Vector2 dir = (target - source) / distance;
            for (int i = 0; i < points; i++) {
                float t = (i + Main.rand.NextFloat(0.9f)) / points;
                Vector2 dropPos = Vector2.Lerp(source, target, t);
                dropPos.Y += MathF.Sin(t * MathHelper.Pi) * distance * 0.07f;
                Vector2 vel = dir * 2.2f;
                vel.Y += MathF.Cos(t * MathHelper.Pi) * 1.1f;
                PRTLoader.NewParticle<PRT_FarmSpore>(dropPos, vel.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.8f, 1.15f),
                    sporeColor, Main.rand.NextFloat(0.35f, 0.6f))
                    .Configure(16 + (int)(t * 26f), glowKind, 0.96f);
            }

            if (kind == 1) {
                //播种:落点拱起一小捧孢子
                for (int i = 0; i < 7; i++) {
                    PRTLoader.NewParticle<PRT_FarmSpore>(target + Main.rand.NextVector2Circular(6f, 3f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.4f, 1.1f)),
                        sporeColor, Main.rand.NextFloat(0.3f, 0.55f)).Configure(Main.rand.Next(40, 70), glowKind);
                }
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.3f }, target);
            }
            else if (kind == 3) {
                //空手清株:只有孢子散掉,没有菌伞,声音也更轻
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_FarmSpore>(target + Main.rand.NextVector2Circular(6f, 4f),
                        new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(0.2f, 0.7f)),
                        sporeColor, Main.rand.NextFloat(0.3f, 0.55f)).Configure(Main.rand.Next(40, 70), glowKind);
                }
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.35f, Pitch = -0.2f }, target);
            }
            else {
                //采收:菌伞碎块弧线飞散 + 孢子扑腾 + 原版菌尘打底
                int capItem = glowKind ? ItemID.GlowingMushroom : ItemID.Mushroom;
                int caps = Main.rand.Next(3, 5);
                for (int i = 0; i < caps; i++) {
                    PRTLoader.NewParticle<PRT_FarmMushroomCap>(target + Main.rand.NextVector2Circular(5f, 4f),
                        new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(2.2f, 4.2f)),
                        Color.White, Main.rand.NextFloat(0.8f, 1.15f)).Configure(capItem);
                }
                for (int i = 0; i < 9; i++) {
                    PRTLoader.NewParticle<PRT_FarmSpore>(target + Main.rand.NextVector2Circular(7f, 5f),
                        new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), -Main.rand.NextFloat(0.2f, 0.9f)),
                        sporeColor, Main.rand.NextFloat(0.3f, 0.6f)).Configure(Main.rand.Next(50, 90), glowKind);
                }
                for (int i = 0; i < 5; i++) {
                    Dust dust = Dust.NewDustPerfect(target, glowKind ? DustID.GlowingMushroom : DustID.Grass,
                        Main.rand.NextVector2Circular(1.4f, 1.2f) - new Vector2(0, 0.8f), 80, default, 1.0f);
                    dust.noGravity = false;
                }
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.55f }, target);
            }
        }

        #endregion

        #region 客户端视觉

        /// <summary>
        /// 客户端视觉总更新:节流扫描田间株位,派生作业强度,发射孢子云。
        /// 运行在并行更新阶段:只读物块,粒子生成经 Defer,随机数走 Rand
        /// </summary>
        private void UpdateClientVisual() {
            //本地玩家远离时不扫不发,省客户端开销
            if (Main.LocalPlayer.Center.DistanceSQ(CenterInWorld) > VisualRange * VisualRange) {
                visualWork = 0f;
                return;
            }

            if (--visualScanTimer <= 0) {
                visualScanTimer = 84 + WhoAmI % 13;
                VisualScanField();
            }

            bool able = Enabled && MachineData.UEvalue >= PlantCost;
            bool hasField = visualShroomSpots.Count + visualGlowShroomSpots.Count + visualEmptySpots > 0;
            visualWork = MathHelper.Lerp(visualWork, able && hasField ? 1f : 0f, 0.03f);

            if (visualWork < 0.2f) {
                return;
            }

            //田间常驻孢子云:密度自然随株数
            EmitSporesFrom(visualShroomSpots, false);
            EmitSporesFrom(visualGlowShroomSpots, true);

            //机身顶部微量孢子逸出,作业感的常驻锚点
            if (visualWork > 0.4f && InScreen && Rand.NextBool(26)) {
                Vector2 ventPos = PosInWorld + new Vector2(Rand.NextFloat(6f, Width - 6f), 2f);
                Defer(() => PRTLoader.NewParticle<PRT_FarmSpore>(ventPos, new Vector2(0f, -0.3f),
                    new Color(150, 165, 255), Rand.NextFloat(0.3f, 0.5f)).Configure(Rand.Next(70, 110), true));
            }
        }

        /// <summary>客户端株位扫描:分菇种缓存成熟株位并统计可播空位;只读物块,并行阶段安全</summary>
        private void VisualScanField() {
            visualShroomSpots.Clear();
            visualGlowShroomSpots.Clear();
            visualEmptySpots = 0;

            int radiusTiles = (int)(WorkRadius / 16f);
            int centerX = Position.X + 1;
            int centerY = Position.Y + 1;
            float radiusSQ = WorkRadius * WorkRadius;

            for (int x = centerX - radiusTiles; x <= centerX + radiusTiles; x++) {
                for (int y = centerY - radiusTiles; y <= centerY + radiusTiles; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    if (Vector2.DistanceSquared(CenterInWorld, new Vector2(x * 16 + 8, y * 16 + 8)) > radiusSQ) {
                        continue;
                    }

                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile) {
                        if (tile.TileType == TileID.Plants && tile.TileFrameX == MushroomFrameX) {
                            if (visualShroomSpots.Count < 60) {
                                visualShroomSpots.Add(new Point16(x, y));
                            }
                        }
                        else if (tile.TileType == TileID.MushroomPlants) {
                            if (visualGlowShroomSpots.Count < 60) {
                                visualGlowShroomSpots.Add(new Point16(x, y));
                            }
                        }
                        continue;
                    }

                    Tile below = Main.tile[x, y + 1];
                    if (!below.HasTile || below.IsHalfBlock || below.Slope != 0) {
                        continue;
                    }
                    if (below.TileType == TileID.Grass || below.TileType == TileID.GolfGrass
                        || below.TileType == TileID.MushroomGrass) {
                        visualEmptySpots++;
                    }
                }
            }
        }

        /// <summary>孢子云发射:每株低概率起尘,密度随株数;逐点屏内过滤,屏外株不发</summary>
        private void EmitSporesFrom(List<Point16> spots, bool glowMode) {
            if (spots.Count == 0) {
                return;
            }
            //期望发射率约 株数/8 次尝试 × 1/18,60 株时 ≈ 0.33 粒/tick,粒子池上限兜底
            int tries = Math.Min((spots.Count + 7) / 8, 6);
            for (int i = 0; i < tries; i++) {
                if (!Rand.NextBool(18)) {
                    continue;
                }
                Point16 spot = spots[Rand.Next(spots.Count)];
                Vector2 world = spot.ToWorldCoordinates(8, 4) + new Vector2(Rand.NextFloat(-8f, 8f), Rand.NextFloat(-10f, 4f));
                if (!VaultUtils.IsPointOnScreen(world - Main.screenPosition, 40)) {
                    continue;
                }
                Color color = glowMode ? new Color(95, 160, 255) : new Color(226, 220, 205);
                float scale = Rand.NextFloat(0.3f, glowMode ? 0.55f : 0.7f);
                Vector2 vel = new(Rand.NextFloat(-0.2f, 0.2f), Rand.NextFloat(-0.25f, 0.05f));
                int life = Rand.Next(100, 170);
                Defer(() => PRTLoader.NewParticle<PRT_FarmSpore>(world, vel, color, scale).Configure(life, glowMode));
            }
        }

        /// <summary>状态灯:与既有"缺电贴图变暗"互补的原因编码</summary>
        private void DrawStatusLamp(SpriteBatch spriteBatch) {
            FarmLampState state;
            if (Disabled || !Enabled) {
                state = FarmLampState.Off;
            }
            else if (MachineData.UEvalue < PlantCost) {
                state = FarmLampState.NoPower;
            }
            else if (!HasAnySpace()) {
                state = FarmLampState.MissingResource;
            }
            else if (visualWork > 0.35f) {
                state = FarmLampState.Working;
            }
            else {
                state = FarmLampState.Idle;
            }
            FarmStatusLamp.Draw(spriteBatch, PosInWorld + new Vector2(Width - 5f, 5f), state, MushroomFarmer.Tint, WhoAmI);
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

        public override void Draw(SpriteBatch spriteBatch) {
            DrawStatusLamp(spriteBatch);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }

        #endregion
    }
}
