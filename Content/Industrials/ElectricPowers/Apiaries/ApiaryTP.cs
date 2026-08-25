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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Apiaries
{
    /// <summary>
    /// 养蜂箱TP:消耗空玻璃瓶与电力,周期性灌装蜂蜜瓶。<br/>
    /// 邻近蜂蜜液体或身处丛林时蜂群更活跃,产率x1.5(环境检查节流缓存)。<br/>
    /// 结算仅权威端执行(主线程经 Defer),灌装演出经修订号搭全量包广播
    /// </summary>
    internal class ApiaryTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ApiaryTile>();
        public override int TargetItem => ModContent.ItemType<Apiary>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 300;
        /// <summary>全量包携带6格物品数据,放宽锚定节奏</summary>
        public override int NetAnchorIntervalTicks => 600;

        #region 常量

        internal const int BottleSlotCount = 2;
        internal const int ProduceSlotCount = 4;
        /// <summary>每瓶蜂蜜的电力开销</summary>
        internal const float BrewCost = 5f;
        /// <summary>基础灌装周期(tick),60秒一瓶</summary>
        internal const int CycleTicks = 3600;
        /// <summary>环境加成下的进度倍率</summary>
        internal const float EnvRateBonus = 1.5f;
        private const int EnvCheckInterval = 300;
        //蜂蜜液体的邻接探测外扩(格)
        private const int HoneyProbeExpand = 3;
        //丛林判定的采样半径(格)与达标数
        private const int JungleProbeRadius = 25;
        private const int JungleTileThreshold = 4;
        //账本合批同步节流
        private const int NetInterval = 30;

        #endregion

        #region 字段

        internal Item[] Bottles = new Item[BottleSlotCount];
        internal Item[] Produce = new Item[ProduceSlotCount];
        internal bool Enabled = true;

        /// <summary>灌装进度(tick 计),UI 进度条用</summary>
        internal float BrewProgress;
        /// <summary>环境加成生效中(邻蜜或丛林)</summary>
        internal bool EnvBonus;

        internal bool IsWorking { get; private set; }
        internal float GlowIntensity;

        private int envCheckTimer;
        private int textIdleTime;
        private byte brewRevision;
        private bool netDirty;
        private int netCooldown;

        #endregion

        #region 属性

        internal bool HasBottle {
            get {
                foreach (Item item in Bottles) {
                    if (item != null && !item.IsAir && item.type == ItemID.Bottle) {
                        return true;
                    }
                }
                return false;
            }
        }

        internal bool ProduceHasSpace {
            get {
                foreach (Item item in Produce) {
                    if (item == null || item.IsAir) {
                        return true;
                    }
                    if (item.type == ItemID.BottledHoney && item.stack < item.maxStack) {
                        return true;
                    }
                }
                return false;
            }
        }

        internal static bool IsEmptyBottle(Item item) => item != null && !item.IsAir && item.type == ItemID.Bottle;

        #endregion

        public override void SetBattery() {
            EnsureSlots();
        }

        public override void Initialize() {
            EnsureSlots();
        }

        private void EnsureSlots() {
            Bottles ??= new Item[BottleSlotCount];
            Produce ??= new Item[ProduceSlotCount];
            for (int i = 0; i < BottleSlotCount; i++) {
                Bottles[i] ??= new Item();
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                Produce[i] ??= new Item();
            }
        }

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(Enabled);
            data.Write(BrewProgress);
            data.Write(EnvBonus);
            data.Write(brewRevision);
            for (int i = 0; i < BottleSlotCount; i++) {
                ItemIO.Send(Bottles[i] ?? new Item(), data, true);
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                ItemIO.Send(Produce[i] ?? new Item(), data, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            EnsureSlots();
            Enabled = reader.ReadBoolean();
            BrewProgress = reader.ReadSingle();
            EnvBonus = reader.ReadBoolean();
            byte newRevision = reader.ReadByte();
            for (int i = 0; i < BottleSlotCount; i++) {
                Bottles[i] = ItemIO.Receive(reader, true);
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                Produce[i] = ItemIO.Receive(reader, true);
            }

            //修订号推进才播灌装演出,入世快照不播
            if (!TileProcessorNetWork.InitializeWorld && newRevision != brewRevision) {
                PlayBrewEffect();
            }
            brewRevision = newRevision;
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            try {
                tag["_Enabled"] = Enabled;
                tag["_BrewProgress"] = BrewProgress;
                List<TagCompound> bottleTags = [];
                for (int i = 0; i < BottleSlotCount; i++) {
                    bottleTags.Add(ItemIO.Save(Bottles[i] ?? new Item()));
                }
                tag["_Bottles"] = bottleTags;
                List<TagCompound> produceTags = [];
                for (int i = 0; i < ProduceSlotCount; i++) {
                    produceTags.Add(ItemIO.Save(Produce[i] ?? new Item()));
                }
                tag["_Produce"] = produceTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"ApiaryTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                EnsureSlots();
                if (tag.TryGet("_Enabled", out bool enabled)) {
                    Enabled = enabled;
                }
                if (tag.TryGet("_BrewProgress", out float progress)) {
                    BrewProgress = Math.Clamp(progress, 0f, CycleTicks);
                }
                if (tag.TryGet("_Bottles", out List<TagCompound> bottleTags)) {
                    for (int i = 0; i < BottleSlotCount && i < bottleTags.Count; i++) {
                        Bottles[i] = CWRSaveData.LoadItemTag(bottleTags[i], $"{nameof(ApiaryTP)}:_Bottles");
                    }
                }
                if (tag.TryGet("_Produce", out List<TagCompound> produceTags)) {
                    for (int i = 0; i < ProduceSlotCount && i < produceTags.Count; i++) {
                        Produce[i] = CWRSaveData.LoadItemTag(produceTags[i], $"{nameof(ApiaryTP)}:_Produce");
                    }
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"ApiaryTP.LoadData Error: {ex.Message}");
            }
        }

        /// <summary>槽位被UI/管道改动后调用:权威端下次节流推送合并纠偏</summary>
        internal void MarkDirty() => netDirty = true;

        #endregion

        #region 环境检查

        /// <summary>
        /// 邻蜜或丛林判定,只读物块,并行阶段安全。
        /// 蜂蜜:机身外扩数格内任一蜂蜜液体格;丛林:采样半径内丛林草达标
        /// </summary>
        private void CheckEnvironment() {
            int tileWidth = Width / 16;
            int tileHeight = Height / 16;

            //蜂蜜液体邻接
            for (int x = Position.X - HoneyProbeExpand; x <= Position.X + tileWidth + HoneyProbeExpand; x++) {
                for (int y = Position.Y - HoneyProbeExpand; y <= Position.Y + tileHeight + HoneyProbeExpand; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Honey) {
                        EnvBonus = true;
                        return;
                    }
                }
            }

            //丛林草采样
            int centerX = Position.X + tileWidth / 2;
            int centerY = Position.Y + tileHeight / 2;
            int jungleCount = 0;
            for (int x = centerX - JungleProbeRadius; x <= centerX + JungleProbeRadius; x += 2) {
                for (int y = centerY - JungleProbeRadius; y <= centerY + JungleProbeRadius; y += 2) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == TileID.JungleGrass) {
                        if (++jungleCount >= JungleTileThreshold) {
                            EnvBonus = true;
                            return;
                        }
                    }
                }
            }

            EnvBonus = false;
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

            //环境加成节流复查
            if (++envCheckTimer >= EnvCheckInterval) {
                envCheckTimer = 0;
                bool old = EnvBonus;
                CheckEnvironment();
                if (old != EnvBonus) {
                    netDirty = true;
                }
            }

            //原料齐备才推进酿造进度:缺瓶/缺电/满仓时蜂群歇工
            bool canWork = HasBottle && ProduceHasSpace && MachineData.UEvalue >= BrewCost;
            if (!canWork) {
                IsWorking = false;
                if (!HasBottle) {
                    Prompt(Apiary.NoBottleText.Value);
                }
                else if (!ProduceHasSpace) {
                    Prompt(Apiary.FullText.Value);
                }
                else {
                    Prompt(Apiary.NoEnergyText.Value);
                }
                return;
            }

            IsWorking = true;
            BrewProgress += EnvBonus ? EnvRateBonus : 1f;
            if (BrewProgress < CycleTicks) {
                return;
            }

            BrewProgress = 0f;
            netDirty = true;

            //结算在主线程做,与UI编辑无竞争
            Defer(() => {
                if (MachineData.UEvalue < BrewCost || !ProduceHasSpace) {
                    return;
                }

                //消耗一只空瓶
                bool consumed = false;
                for (int i = 0; i < BottleSlotCount; i++) {
                    Item bottle = Bottles[i];
                    if (!IsEmptyBottle(bottle)) {
                        continue;
                    }
                    bottle.stack--;
                    if (bottle.stack <= 0) {
                        bottle.TurnToAir();
                    }
                    consumed = true;
                    break;
                }
                if (!consumed) {
                    return;
                }

                int remain = InsertItem(Produce, ItemID.BottledHoney, 1);
                if (remain > 0) {
                    DropItem(new Item(ItemID.BottledHoney, remain));
                }

                MachineData.UEvalue -= BrewCost;
                brewRevision++;
                netDirty = true;

                PlayBrewEffect();
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
            Defer(() => CombatText.NewText(HitBox, Apiary.Tint, text));
        }

        /// <summary>灌装演出:蜂蜜色滴珠从箱口涌出;主线程调用,服务器跳过</summary>
        internal void PlayBrewEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 spout = CenterInWorld + new Vector2(0, -6);
            for (int i = 0; i < 14; i++) {
                Dust dust = Dust.NewDustPerfect(spout + Main.rand.NextVector2Circular(10f, 6f),
                    DustID.Honey, new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.5f, 2.4f)),
                    60, default, 1.1f);
                dust.noGravity = false;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = 0.3f }, spout);
        }

        #endregion

        #region 交互/销毁/绘制

        public void OpenUI() {
            var ui = UIHandleLoader.GetUIHandleOfType<ApiaryUI>();
            ui?.Interactive(this);
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            //倒出全部空瓶与产出
            for (int i = 0; i < BottleSlotCount; i++) {
                if (Bottles[i] != null && !Bottles[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Bottles[i]);
                    Bottles[i] = new Item();
                }
            }
            for (int i = 0; i < ProduceSlotCount; i++) {
                if (Produce[i] != null && !Produce[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Produce[i]);
                    Produce[i] = new Item();
                }
            }

            //掉落机器本身(带能量)
            Item apiaryItem = new Item(ModContent.ItemType<Apiary>());
            apiaryItem.CWR().UEValue = MachineData.UEvalue;
            int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, apiaryItem);
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
