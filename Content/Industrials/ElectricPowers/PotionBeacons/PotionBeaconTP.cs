using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.PotionBeacons
{
    /// <summary>
    /// 药剂弥散信标TP:6个药水槽,开瓶后按"1瓶=原buff时长x2"的供给时间弥散,
    /// 周期性给半径内玩家续短时长buff(篝火模型:每端为它模拟的玩家施加)。<br/>
    /// 供给账本各端同跑,权威端经脏标记节流推送纠偏
    /// </summary>
    internal class PotionBeaconTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<PotionBeaconTile>();
        public override int TargetItem => ModContent.ItemType<PotionBeacon>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 800;
        /// <summary>全量包携带6格药水与账本,放宽锚定节奏</summary>
        public override int NetAnchorIntervalTicks => 600;

        #region 常量

        internal const int SlotCount = 6;
        /// <summary>弥散半径(像素)</summary>
        internal const float AuraRadius = 1200f;
        /// <summary>供给期间每帧耗电</summary>
        internal const float ConsumePerTick = 0.5f;
        //buff续杯节奏与单次时长:短时长反复续,离场即自然消退
        private const int ApplyInterval = 30;
        private const int BuffDuration = 120;
        //账本合批同步节流
        private const int NetInterval = 30;

        #endregion

        #region 字段

        /// <summary>药水槽</summary>
        internal Item[] Potions = new Item[SlotCount];
        /// <summary>各槽剩余供给帧数(开瓶后计)</summary>
        internal int[] SupplyLeft = new int[SlotCount];
        /// <summary>各槽供给中的buff类型</summary>
        internal int[] SupplyBuff = new int[SlotCount];
        /// <summary>各槽本瓶供给总帧数,UI进度用</summary>
        internal int[] SupplyTotal = new int[SlotCount];

        internal bool Enabled = true;
        internal bool WorkingActive { get; private set; }
        internal float GlowIntensity;

        private int applyTimer;
        private int textIdleTime;
        private int mistTimer;
        private bool netDirty;
        private int netCooldown;
        //本tick处于供给状态的buff,复用缓存避免逐帧分配
        private readonly List<int> activeBuffCache = new(SlotCount);

        #endregion

        public override void SetBattery() {
            EnsureSlots();
        }

        public override void Initialize() {
            EnsureSlots();
        }

        private void EnsureSlots() {
            Potions ??= new Item[SlotCount];
            for (int i = 0; i < SlotCount; i++) {
                Potions[i] ??= new Item();
            }
        }

        /// <summary>可入槽判定:有增益且可消耗的药水类物品</summary>
        internal static bool IsValidPotion(Item item) {
            return item != null && !item.IsAir && item.buffType > 0 && item.buffTime > 0 && item.consumable;
        }

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(Enabled);
            for (int i = 0; i < SlotCount; i++) {
                data.Write(SupplyLeft[i]);
                data.Write(SupplyBuff[i]);
                data.Write(SupplyTotal[i]);
                ItemIO.Send(Potions[i] ?? new Item(), data, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            EnsureSlots();
            Enabled = reader.ReadBoolean();
            for (int i = 0; i < SlotCount; i++) {
                SupplyLeft[i] = reader.ReadInt32();
                SupplyBuff[i] = reader.ReadInt32();
                SupplyTotal[i] = reader.ReadInt32();
                Potions[i] = ItemIO.Receive(reader, true);
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            try {
                tag["_Enabled"] = Enabled;
                tag["_SupplyLeft"] = SupplyLeft;
                tag["_SupplyBuff"] = SupplyBuff;
                tag["_SupplyTotal"] = SupplyTotal;
                List<TagCompound> itemTags = [];
                for (int i = 0; i < SlotCount; i++) {
                    itemTags.Add(ItemIO.Save(Potions[i] ?? new Item()));
                }
                tag["_Potions"] = itemTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"PotionBeaconTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                EnsureSlots();
                if (tag.TryGet("_Enabled", out bool enabled)) {
                    Enabled = enabled;
                }
                if (tag.TryGet("_SupplyLeft", out int[] left) && left.Length == SlotCount) {
                    SupplyLeft = left;
                }
                if (tag.TryGet("_SupplyBuff", out int[] buff) && buff.Length == SlotCount) {
                    SupplyBuff = buff;
                }
                if (tag.TryGet("_SupplyTotal", out int[] total) && total.Length == SlotCount) {
                    SupplyTotal = total;
                }
                if (tag.TryGet("_Potions", out List<TagCompound> itemTags)) {
                    for (int i = 0; i < SlotCount && i < itemTags.Count; i++) {
                        Potions[i] = CWRSaveData.LoadItemTag(itemTags[i], $"{nameof(PotionBeaconTP)}:_Potions");
                    }
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"PotionBeaconTP.LoadData Error: {ex.Message}");
            }
        }

        /// <summary>槽位被UI/管道改动后调用:本地即改,权威端下次节流推送合并纠偏</summary>
        internal void MarkDirty() => netDirty = true;

        #endregion

        #region 更新逻辑

        public override void UpdateMachine() {
            //权威端节流刷新账本与槽位
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

            activeBuffCache.Clear();
            bool anySupply = false;

            if (Enabled) {
                bool powered = MachineData.UEvalue >= ConsumePerTick;
                if (!powered) {
                    bool wantsWork = false;
                    for (int i = 0; i < SlotCount; i++) {
                        if (SupplyLeft[i] > 0 || IsValidPotion(Potions[i])) {
                            wantsWork = true;
                            break;
                        }
                    }
                    if (wantsWork && textIdleTime <= 0) {
                        //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
                        Defer(() => CombatText.NewText(HitBox, PotionBeacon.Tint, PotionBeacon.NoEnergyText.Value));
                        textIdleTime = 300;
                    }
                }
                else {
                    for (int i = 0; i < SlotCount; i++) {
                        //供给中:走完这瓶的账
                        if (SupplyLeft[i] > 0) {
                            SupplyLeft[i]--;
                            anySupply = true;
                            if (SupplyBuff[i] > 0 && !activeBuffCache.Contains(SupplyBuff[i])) {
                                activeBuffCache.Add(SupplyBuff[i]);
                            }
                            continue;
                        }

                        //开新瓶:账本各端同跑保持连续,权威端负责推送纠偏
                        Item potion = Potions[i];
                        if (!IsValidPotion(potion)) {
                            SupplyBuff[i] = 0;
                            SupplyTotal[i] = 0;
                            continue;
                        }

                        SupplyBuff[i] = potion.buffType;
                        SupplyTotal[i] = potion.buffTime * 2;
                        SupplyLeft[i] = SupplyTotal[i];
                        potion.stack--;
                        if (potion.stack <= 0) {
                            potion.TurnToAir();
                        }
                        netDirty = true;
                        anySupply = true;
                        if (!activeBuffCache.Contains(SupplyBuff[i])) {
                            activeBuffCache.Add(SupplyBuff[i]);
                        }
                        SpawnOpenEffect();
                    }

                    if (anySupply) {
                        MachineData.UEvalue -= ConsumePerTick;
                    }
                }
            }

            WorkingActive = anySupply;
            GlowIntensity = anySupply
                ? Math.Min(1f, GlowIntensity + 0.03f)
                : Math.Max(0f, GlowIntensity - 0.03f);

            if (anySupply) {
                ApplyAura();
                SpawnMistEffect();
            }
        }

        /// <summary>篝火模型:每个端为它模拟的所有玩家续buff,短时长反复续无需网络包</summary>
        private void ApplyAura() {
            if (++applyTimer < ApplyInterval) {
                return;
            }
            applyTimer = 0;

            float radiusSQ = AuraRadius * AuraRadius;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                if (player.Center.DistanceSQ(CenterInWorld) > radiusSQ) {
                    continue;
                }

                int whoAmI = player.whoAmI;
                //并行阶段buff写入延迟到主线程执行(串行阶段立即执行)
                foreach (int buffType in activeBuffCache) {
                    int type = buffType;
                    Defer(() => {
                        Player target = Main.player[whoAmI];
                        if (target.active && !target.dead) {
                            target.AddBuff(type, BuffDuration);
                        }
                    });
                }
            }
        }

        private void SpawnOpenEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            Defer(() => {
                for (int i = 0; i < 8; i++) {
                    Dust dust = Dust.NewDustDirect(PosInWorld, Width, 16, DustID.PurpleTorch,
                        Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 2.5f), 120, default, 1f);
                    dust.noGravity = true;
                }
            });
        }

        /// <summary>弥散氛围:塔顶紫雾缓慢上飘</summary>
        private void SpawnMistEffect() {
            if (VaultUtils.isServer || ++mistTimer < 14) {
                return;
            }
            mistTimer = 0;
            Vector2 spawnPos = PosInWorld + new Vector2(Rand.Next(Width), Rand.Next(16));
            Defer(() => {
                Dust dust = Dust.NewDustPerfect(spawnPos, DustID.PurpleTorch, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -0.8f), 150, default, 0.9f);
                dust.noGravity = true;
                dust.fadeIn = 0.5f;
            });
        }

        #endregion

        #region 交互/销毁/绘制

        public void OpenUI() {
            var ui = UIHandleLoader.GetUIHandleOfType<PotionBeaconUI>();
            ui?.Interactive(this);
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            //掉落存储的药水
            for (int i = 0; i < SlotCount; i++) {
                if (Potions[i] != null && !Potions[i].IsAir) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, Potions[i]);
                    Potions[i] = new Item();
                }
            }

            //掉落机器本身(带能量)
            Item beaconItem = new Item(ModContent.ItemType<PotionBeacon>());
            beaconItem.CWR().UEValue = MachineData.UEvalue;
            int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, beaconItem);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
            DrawActiveBuffIcons(spriteBatch);
        }

        /// <summary>塔顶横排画出当前弥散中的buff图标</summary>
        private void DrawActiveBuffIcons(SpriteBatch spriteBatch) {
            int count = 0;
            for (int i = 0; i < SlotCount; i++) {
                if (SupplyLeft[i] > 0 && SupplyBuff[i] > 0) {
                    count++;
                }
            }
            if (count == 0) {
                return;
            }

            const float iconSize = 22f;
            const float gap = 4f;
            float totalWidth = count * iconSize + (count - 1) * gap;
            Vector2 drawPos = CenterInWorld + new Vector2(-totalWidth / 2f, -Height / 2f - 34f) - Main.screenPosition;
            float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f) * 2f;

            for (int i = 0; i < SlotCount; i++) {
                if (SupplyLeft[i] <= 0 || SupplyBuff[i] <= 0) {
                    continue;
                }
                int buffType = SupplyBuff[i];
                Texture2D icon = TextureAssets.Buff[buffType].Value;
                float alpha = 0.55f + GlowIntensity * 0.35f;
                spriteBatch.Draw(icon, drawPos + new Vector2(0, bob),
                    null, Color.White * alpha, 0f, Vector2.Zero,
                    iconSize / icon.Width, SpriteEffects.None, 0f);
                drawPos.X += iconSize + gap;
            }
        }

        #endregion
    }
}
