using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoFishers
{
    /// <summary>
    /// 自动钓鱼机TP:探测机身下方的水面,消耗鱼饵与电力周期性收获渔获。<br/>
    /// 垂钓判定与库存变更仅权威端执行(主线程经 Defer),
    /// 收获演出经修订号搭全量包广播;钓竿与钓线为纯客户端表现
    /// </summary>
    internal class AutoFisherTP : BaseBattery
    {
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/AutoFisherRod")]
        internal static Asset<Texture2D> rodAsset = null;
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/AutoFisherHook")]
        internal static Asset<Texture2D> hookAsset = null;

        public override int TargetTileID => ModContent.TileType<AutoFisherTile>();
        public override int TargetItem => ModContent.ItemType<AutoFisher>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 800;
        /// <summary>全量包携带16格物品数据,放宽锚定节奏</summary>
        public override int NetAnchorIntervalTicks => 600;

        #region 常量

        internal const int BaitSlotCount = 4;
        internal const int CatchSlotCount = 12;
        /// <summary>每次收获消耗的电力</summary>
        internal const float CastCost = 15f;
        /// <summary>机器基础钓力</summary>
        internal const int BasePower = 20;
        /// <summary>联通水体最小规模,不足没法下竿</summary>
        internal const int MinLakeSize = 30;
        /// <summary>满效水体规模,不足按比例折减钓力</summary>
        internal const int FullLakeSize = 75;
        //水面探测的横向半幅与向下深度(格)
        private const int ProbeHalfWidth = 6;
        private const int ProbeDepth = 24;
        //水体规模统计上限
        private const int LakeMeasureCap = 400;
        private const int ScanInterval = 300;
        //收获后的歇竿时间
        private const int RestInterval = 90;
        //账本合批同步节流
        private const int NetInterval = 30;

        #endregion

        #region 字段

        internal Item[] Baits = new Item[BaitSlotCount];
        internal Item[] Catches = new Item[CatchSlotCount];

        internal bool Enabled = true;
        /// <summary>单次垂钓等待时长(帧),UI 可调</summary>
        internal int FishInterval = 600;

        /// <summary>0=收竿待机 1=浮标在水上等待</summary>
        internal byte FishState;
        /// <summary>浮标所在的水面物块</summary>
        internal Point16 WaterPoint;
        /// <summary>联通水体规模</summary>
        internal int LakeSize;
        /// <summary>是否蜂蜜水</summary>
        internal bool HoneyWater;
        /// <summary>最近一次结算的最终钓力,UI 展示</summary>
        internal int CurrentPower;
        /// <summary>最近一次渔获类型,演出用</summary>
        internal int LastCatchType;

        internal float GlowIntensity;

        private int waitTimer;
        private int restTimer;
        private int scanTimer;
        private int waterCheckTimer;
        private int textIdleTime;
        private byte catchRevision;
        private bool netDirty;
        private int netCooldown;

        //---- 客户端表现字段:钓竿摆角与浮标 ----
        internal float ArmAngle = -0.5f;
        internal float CatchFlash;
        private float bobPhase;

        //---- 钓竿装配锚点(相对物块左上角,由组合预览图反算) ----
        //竿底插销枢轴:竿绕它做小幅摆动
        private static readonly Vector2 RodPivotOffset = new(12f, 18f);
        //插销在竿贴图内的本地坐标(底部尖头中心)
        private static readonly Vector2 RodPivotLocal = new(5f, 26f);
        //出线点在竿贴图内的本地坐标(竿顶红球)
        private static readonly Vector2 RodTipLocal = new(6.5f, 1.5f);

        #endregion

        #region 属性

        internal bool HasBait {
            get {
                foreach (Item item in Baits) {
                    if (item != null && !item.IsAir && item.bait > 0) {
                        return true;
                    }
                }
                return false;
            }
        }

        internal bool WaterOK => WaterPoint != Point16.Zero && LakeSize >= MinLakeSize;

        internal bool CatchHasSpace {
            get {
                foreach (Item item in Catches) {
                    if (item == null || item.IsAir) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>竿底枢轴世界坐标(机身桅杆插座)</summary>
        internal Vector2 RodPivotPos => PosInWorld + RodPivotOffset;

        /// <summary>竿的当前摆角:表现包络 ArmAngle 折算成小幅倾斜</summary>
        internal float RodRotation => ArmAngle * 0.16f;

        /// <summary>竿顶出线点世界坐标(随竿摆动旋转)</summary>
        internal Vector2 RodTipPos => RodPivotPos + (RodTipLocal - RodPivotLocal).RotatedBy(RodRotation);

        /// <summary>浮标世界坐标,含沉浮摆动</summary>
        internal Vector2 BobberPos {
            get {
                Vector2 basePos = WaterPoint.ToWorldCoordinates(8, 6);
                return basePos + new Vector2(0, MathF.Sin(bobPhase) * 2.5f);
            }
        }

        #endregion

        public override void SetBattery() {
            EnsureSlots();
        }

        public override void Initialize() {
            EnsureSlots();
        }

        private void EnsureSlots() {
            Baits ??= new Item[BaitSlotCount];
            Catches ??= new Item[CatchSlotCount];
            for (int i = 0; i < BaitSlotCount; i++) {
                Baits[i] ??= new Item();
            }
            for (int i = 0; i < CatchSlotCount; i++) {
                Catches[i] ??= new Item();
            }
        }

        internal static bool IsBait(Item item) => item != null && !item.IsAir && item.bait > 0;

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(Enabled);
            data.Write(FishInterval);
            data.Write(FishState);
            data.Write(WaterPoint.X);
            data.Write(WaterPoint.Y);
            data.Write(LakeSize);
            data.Write(HoneyWater);
            data.Write(CurrentPower);
            data.Write(catchRevision);
            data.Write(LastCatchType);
            for (int i = 0; i < BaitSlotCount; i++) {
                ItemIO.Send(Baits[i] ?? new Item(), data, true);
            }
            for (int i = 0; i < CatchSlotCount; i++) {
                ItemIO.Send(Catches[i] ?? new Item(), data, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            EnsureSlots();
            Enabled = reader.ReadBoolean();
            FishInterval = reader.ReadInt32();
            FishState = reader.ReadByte();
            WaterPoint = new Point16(reader.ReadInt16(), reader.ReadInt16());
            LakeSize = reader.ReadInt32();
            HoneyWater = reader.ReadBoolean();
            CurrentPower = reader.ReadInt32();
            byte newRevision = reader.ReadByte();
            LastCatchType = reader.ReadInt32();
            for (int i = 0; i < BaitSlotCount; i++) {
                Baits[i] = ItemIO.Receive(reader, true);
            }
            for (int i = 0; i < CatchSlotCount; i++) {
                Catches[i] = ItemIO.Receive(reader, true);
            }

            //修订号推进才播收获演出,入世快照不播
            if (!TileProcessorNetWork.InitializeWorld && newRevision != catchRevision) {
                PlayCatchEffect();
            }
            catchRevision = newRevision;
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            try {
                tag["_Enabled"] = Enabled;
                tag["_FishInterval"] = FishInterval;
                List<TagCompound> baitTags = [];
                for (int i = 0; i < BaitSlotCount; i++) {
                    baitTags.Add(ItemIO.Save(Baits[i] ?? new Item()));
                }
                tag["_Baits"] = baitTags;
                List<TagCompound> catchTags = [];
                for (int i = 0; i < CatchSlotCount; i++) {
                    catchTags.Add(ItemIO.Save(Catches[i] ?? new Item()));
                }
                tag["_Catches"] = catchTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"AutoFisherTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                EnsureSlots();
                if (tag.TryGet("_Enabled", out bool enabled)) {
                    Enabled = enabled;
                }
                if (tag.TryGet("_FishInterval", out int interval)) {
                    FishInterval = Math.Clamp(interval, 300, 1200);
                }
                if (tag.TryGet("_Baits", out List<TagCompound> baitTags)) {
                    for (int i = 0; i < BaitSlotCount && i < baitTags.Count; i++) {
                        Baits[i] = CWRSaveData.LoadItemTag(baitTags[i], $"{nameof(AutoFisherTP)}:_Baits");
                    }
                }
                if (tag.TryGet("_Catches", out List<TagCompound> catchTags)) {
                    for (int i = 0; i < CatchSlotCount && i < catchTags.Count; i++) {
                        Catches[i] = CWRSaveData.LoadItemTag(catchTags[i], $"{nameof(AutoFisherTP)}:_Catches");
                    }
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"AutoFisherTP.LoadData Error: {ex.Message}");
            }
        }

        /// <summary>槽位被UI/管道改动后调用:权威端下次节流推送合并纠偏</summary>
        internal void MarkDirty() => netDirty = true;

        #endregion

        #region 水面探测

        /// <summary>
        /// 机身下前方找一处开阔水面(顶格液体且上方无实心),再数联通水体规模;
        /// 只读物块,并行阶段安全。熔岩不认
        /// </summary>
        private void ScanWater() {
            int centerX = Position.X + 1;
            int startY = Position.Y + Height / 16;

            Point16 found = Point16.Zero;
            bool honey = false;

            for (int y = startY; y < startY + ProbeDepth && found == Point16.Zero; y++) {
                for (int dx = 0; dx <= ProbeHalfWidth && found == Point16.Zero; dx++) {
                    //从中线向两侧扩展,取离机身最近的水面
                    for (int side = 0; side < 2; side++) {
                        int x = side == 0 ? centerX + dx : centerX - dx;
                        if (side == 1 && dx == 0) {
                            continue;
                        }
                        if (!WorldGen.InWorld(x, y, 5)) {
                            continue;
                        }

                        Tile tile = Main.tile[x, y];
                        if (tile.LiquidAmount <= 32 || tile.LiquidType == LiquidID.Lava || tile.LiquidType == LiquidID.Shimmer) {
                            continue;
                        }
                        if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                            continue;
                        }
                        //要求是水面:上方无液体也无实心块
                        Tile above = Main.tile[x, y - 1];
                        if (above.LiquidAmount > 0 || (above.HasTile && Main.tileSolid[above.TileType])) {
                            continue;
                        }

                        found = new Point16(x, y);
                        honey = tile.LiquidType == LiquidID.Honey;
                        break;
                    }
                }
            }

            if (found == Point16.Zero) {
                if (WaterPoint != Point16.Zero) {
                    WaterPoint = Point16.Zero;
                    LakeSize = 0;
                    netDirty = true;
                }
                return;
            }

            int size = MeasureLakeSize(found);
            if (found != WaterPoint || size != LakeSize || honey != HoneyWater) {
                WaterPoint = found;
                LakeSize = size;
                HoneyWater = honey;
                netDirty = true;
            }
        }

        /// <summary>四向泛洪数联通液体格,封顶 <see cref="LakeMeasureCap"/></summary>
        private static int MeasureLakeSize(Point16 start) {
            HashSet<Point16> visited = [];
            Queue<Point16> queue = new();
            queue.Enqueue(start);
            visited.Add(start);
            int count = 0;

            while (queue.Count > 0 && count < LakeMeasureCap) {
                Point16 point = queue.Dequeue();
                Tile tile = Main.tile[point.X, point.Y];
                if (tile.LiquidAmount <= 0) {
                    continue;
                }
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    continue;
                }
                count++;

                Span<Point16> neighbors = [
                    new Point16(point.X + 1, point.Y),
                    new Point16(point.X - 1, point.Y),
                    new Point16(point.X, point.Y + 1),
                    new Point16(point.X, point.Y - 1),
                ];
                foreach (Point16 next in neighbors) {
                    if (!WorldGen.InWorld(next.X, next.Y, 5) || visited.Contains(next)) {
                        continue;
                    }
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return count;
        }

        #endregion

        #region 更新逻辑

        public override void UpdateMachine() {
            //权威端节流刷新
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
            if (restTimer > 0) {
                restTimer--;
            }

            //客户端表现推进
            UpdateVisual();

            bool authority = !VaultUtils.isClient;
            if (!authority) {
                return;
            }

            //定期重探水面
            if (++scanTimer >= ScanInterval) {
                scanTimer = 0;
                ScanWater();
            }

            if (!Enabled) {
                if (FishState != 0) {
                    FishState = 0;
                    netDirty = true;
                }
                return;
            }

            if (FishState == 0) {
                if (restTimer > 0) {
                    return;
                }
                TryStartFishing();
            }
            else {
                UpdateWaiting();
            }
        }

        private void TryStartFishing() {
            if (!WaterOK) {
                Prompt(AutoFisher.NoWaterText.Value);
                return;
            }
            if (!HasBait) {
                Prompt(AutoFisher.NoBaitText.Value);
                return;
            }
            if (!CatchHasSpace) {
                Prompt(AutoFisher.FullText.Value);
                return;
            }
            if (MachineData.UEvalue < CastCost) {
                Prompt(AutoFisher.NoEnergyText.Value);
                return;
            }

            FishState = 1;
            //等待时长带两成浮动,Rand线程安全
            int jitter = FishInterval / 5;
            waitTimer = FishInterval + Rand.Next(-jitter, jitter + 1);
            netDirty = true;

            if (!VaultUtils.isServer) {
                Defer(() => SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.2f, Volume = 0.5f }, RodPivotPos));
            }
        }

        private void UpdateWaiting() {
            //水面丢了就收竿
            if (++waterCheckTimer >= 60) {
                waterCheckTimer = 0;
                Tile tile = Main.tile[WaterPoint.X, WaterPoint.Y];
                if (tile.LiquidAmount <= 32 || tile.LiquidType == LiquidID.Lava || tile.LiquidType == LiquidID.Shimmer) {
                    FishState = 0;
                    ScanWater();
                    netDirty = true;
                    return;
                }
            }

            if (--waitTimer > 0) {
                return;
            }

            PerformCatch();
        }

        /// <summary>结算一次渔获:消耗鱼饵与电力,掷骰入仓;整段在主线程执行保证与UI编辑无竞争</summary>
        private void PerformCatch() {
            //先把状态收回,结算细节交给主线程闭包
            FishState = 0;
            restTimer = RestInterval;

            Defer(() => {
                //主线程复核,乱序到达时放弃本次结算
                if (MachineData.UEvalue < CastCost || !CatchHasSpace) {
                    netDirty = true;
                    return;
                }

                //消耗一份鱼饵
                int baitPower = 0;
                for (int i = 0; i < BaitSlotCount; i++) {
                    Item bait = Baits[i];
                    if (!IsBait(bait)) {
                        continue;
                    }
                    baitPower = bait.bait;
                    bait.stack--;
                    if (bait.stack <= 0) {
                        bait.TurnToAir();
                    }
                    break;
                }
                if (baitPower <= 0) {
                    netDirty = true;
                    return;
                }

                //最终钓力:基础+饵力,再按水体规模折减
                float lakeFactor = MathHelper.Clamp(LakeSize / (float)FullLakeSize, 0.5f, 1f);
                int power = (int)((BasePower + baitPower) * lakeFactor);
                CurrentPower = power;

                FishEnvironment env = FishEnvironment.Capture(WaterPoint, HoneyWater);
                int itemType = AutoFisherLootTable.Roll(power, env, Main.rand);

                int remain = InsertItem(Catches, itemType, 1);
                if (remain > 0) {
                    DropItem(new Item(itemType, remain));
                }

                MachineData.UEvalue -= CastCost;
                LastCatchType = itemType;
                catchRevision++;
                netDirty = true;

                PlayCatchEffect();
            });
        }

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

        private void Prompt(string text) {
            if (textIdleTime > 0) {
                return;
            }
            textIdleTime = 300;
            //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
            Defer(() => CombatText.NewText(HitBox, AutoFisher.Tint, text));
        }

        /// <summary>收获演出:浮标处水花+提竿闪动+音效;主线程调用,服务器跳过</summary>
        internal void PlayCatchEffect() {
            CatchFlash = 1f;
            if (VaultUtils.isServer || WaterPoint == Point16.Zero) {
                return;
            }

            Vector2 splashPos = WaterPoint.ToWorldCoordinates(8, 4);
            int dustType = HoneyWater ? DustID.Honey : DustID.Water;
            for (int i = 0; i < 16; i++) {
                Dust dust = Dust.NewDustPerfect(splashPos,
                    dustType, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1.5f, 4.5f)), 60, default, 1.2f);
                dust.noGravity = false;
            }
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.7f, Pitch = 0.1f }, splashPos);
        }

        /// <summary>钓竿摆角与浮标沉浮的客户端包络</summary>
        private void UpdateVisual() {
            if (VaultUtils.isServer) {
                return;
            }

            bobPhase += 0.06f;
            if (bobPhase > MathHelper.TwoPi) {
                bobPhase -= MathHelper.TwoPi;
            }

            if (CatchFlash > 0f) {
                CatchFlash = Math.Max(0f, CatchFlash - 0.04f);
            }

            //挥竿姿态:等待时前倾,收竿时回撤,提竿瞬间猛拉
            float target = FishState == 1 ? 0.45f : -0.5f;
            target -= CatchFlash * 0.9f;
            ArmAngle = MathHelper.Lerp(ArmAngle, target, 0.12f);

            bool working = FishState == 1;
            GlowIntensity = working
                ? Math.Min(1f, GlowIntensity + 0.04f)
                : Math.Max(0f, GlowIntensity - 0.02f);
        }

        #endregion

        #region 交互/销毁/绘制

        public void OpenUI() {
            var ui = UIHandleLoader.GetUIHandleOfType<AutoFisherUI>();
            ui?.Interactive(this);
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            for (int i = 0; i < BaitSlotCount; i++) {
                if (Baits[i] != null && !Baits[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Baits[i]);
                    Baits[i] = new Item();
                }
            }
            for (int i = 0; i < CatchSlotCount; i++) {
                if (Catches[i] != null && !Catches[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Catches[i]);
                    Catches[i] = new Item();
                }
            }

            //掉落机器本身(带能量)
            Item fisherItem = new Item(ModContent.ItemType<AutoFisher>());
            fisherItem.CWR().UEValue = MachineData.UEvalue;
            int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, fisherItem);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
            DrawRodAndLine(spriteBatch);
        }

        /// <summary>钓竿贴图立在机身桅杆上,提竿/收竿经竿身小幅摆动传达;鱼线仍为程序化贝塞尔</summary>
        private void DrawRodAndLine(SpriteBatch spriteBatch) {
            if (rodAsset == null || hookAsset == null) {
                return;
            }

            Texture2D rodTex = rodAsset.Value;
            Vector2 pivotScreen = RodPivotPos - Main.screenPosition;
            Color lightColor = Lighting.GetColor(RodPivotPos.ToTileCoordinates());

            //竿:底部插销锚在桅杆插座,绕它随包络小幅摆动
            spriteBatch.Draw(rodTex, pivotScreen, null, lightColor, RodRotation,
                RodPivotLocal, 1f, SpriteEffects.None, 0f);

            //钓线与浮标:只在浮标在水上时画
            if (FishState != 1 || WaterPoint == Point16.Zero) {
                return;
            }

            Vector2 bobberWorld = BobberPos;
            //线从竿顶红球出,挂到浮标顶端
            DrawFishingLine(spriteBatch, RodTipPos, bobberWorld - new Vector2(0f, 4f), lightColor);

            //浮标贴图:锚点取贴图中上,浮在水面线上
            Texture2D hookTex = hookAsset.Value;
            spriteBatch.Draw(hookTex, bobberWorld - Main.screenPosition, null,
                Lighting.GetColor(bobberWorld.ToTileCoordinates()), 0f,
                new Vector2(hookTex.Width * 0.5f, 4f), 1f, SpriteEffects.None, 0f);
        }

        /// <summary>竿尖到浮标的下垂钓线,二次贝塞尔逐段画</summary>
        private static void DrawFishingLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color lightColor) {
            Texture2D px = VaultAsset.placeholder2.Value;
            const int segments = 16;
            float sag = MathHelper.Clamp(from.Distance(to) * 0.18f, 8f, 60f);
            Vector2 control = (from + to) * 0.5f + new Vector2(0, sag);
            Color lineColor = Color.Lerp(new Color(40, 40, 46), lightColor, 0.3f) * 0.85f;

            Vector2 prev = from;
            for (int i = 1; i <= segments; i++) {
                float t = i / (float)segments;
                Vector2 point = Vector2.Lerp(Vector2.Lerp(from, control, t), Vector2.Lerp(control, to, t), t);
                Vector2 delta = point - prev;
                float length = delta.Length();
                if (length > 0.01f) {
                    spriteBatch.Draw(px, prev - Main.screenPosition, new Rectangle(0, 0, 1, 1), lineColor,
                        delta.ToRotation(), new Vector2(0f, 0.5f), new Vector2(length, 1.2f), SpriteEffects.None, 0f);
                }
                prev = point;
            }
        }

        #endregion
    }
}
