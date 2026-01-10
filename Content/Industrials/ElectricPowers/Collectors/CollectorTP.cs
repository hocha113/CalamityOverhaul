using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Industrials.Storage;
using InnoVault.Actors;
using InnoVault.Storages;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors
{
    internal class CollectorTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<CollectorTile>();
        public override int TargetItem => ModContent.ItemType<Collector>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;
        internal const int maxFindChestMode = 600;
        internal const int killerArmDistance = 2400;
        public Vector2 ArmPos => CenterInWorld + new Vector2(0, 14);
        private int textIdleTime;
        internal int frame;
        internal bool workState;
        internal bool BatteryPrompt;
        internal Item ItemFilter;
        internal int TagItemSign;
        internal int dontSpawnArmTime;
        internal int consumeUE = 8;
        internal List<int> ArmActorIndices = new List<int>();
        internal float hoverSengs;

        public override void SetBattery() {
            ItemFilter = new Item();
            DrawExtendMode = 2200;
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            ItemIO.Send(ItemFilter, data);
            data.Write(TagItemSign);
            data.Write(BatteryPrompt);
            data.Write(workState);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            ItemFilter = ItemIO.Receive(reader);
            TagItemSign = reader.ReadInt32();
            BatteryPrompt = reader.ReadBoolean();
            workState = reader.ReadBoolean();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);

            ItemFilter ??= new Item();
            tag["_ItemFilter"] = ItemIO.Save(ItemFilter);

            string result = TagItemSign < ItemID.Count
                ? TagItemSign.ToString()
                : ItemLoader.GetItem(TagItemSign).FullName;
            tag["_TagItemFullName"] = result;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);

            if (tag.TryGet<TagCompound>("_ItemFilter", out var value)) {
                ItemFilter = ItemIO.Load(value);
            }
            else {
                ItemFilter = new Item();
            }

            if (tag.TryGet("_TagItemFullName", out string fullName)) {
                TagItemSign = VaultUtils.GetItemTypeFromFullName(fullName);
            }
            else {
                TagItemSign = ItemID.None;
            }
        }

        private void FindFrame() {
            int maxFrame = workState ? 7 : 24;
            if (!workState && frame == 23) {
                frame = 0;
                workState = true;
                if (!VaultUtils.isClient) {
                    SendData();
                }
                SoundEngine.PlaySound(CWRSound.CollectorStart, PosInWorld);
            }
            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        internal static bool IsArmActorValid(int actorIndex) {
            if (actorIndex < 0 || actorIndex >= ActorLoader.MaxActorCount) return false;
            Actor actor = ActorLoader.Actors[actorIndex];
            return actor != null && actor.Active && actor is CollectorArm;
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            Item item = player.GetItem();
            bool changed = false;

            if (!item.Alives()) {
                if (TagItemSign != ItemID.None) {
                    TagItemSign = ItemID.None;
                    changed = true;
                }
            }
            else if (TagItemSign > ItemID.None && TagItemSign == item.type) {
                TagItemSign = ItemID.None;
                changed = true;
            }
            else {
                TagItemSign = item.type;
                changed = true;

                if (TagItemSign == ModContent.ItemType<ItemFilter>()) {
                    ItemFilter = item.Clone();
                    //深拷贝过滤数据
                    var sourceData = item.GetGlobalItem<ItemFilterData>();
                    var targetData = ItemFilter.GetGlobalItem<ItemFilterData>();
                    targetData.SetItems(sourceData.Items);
                }
            }

            //播放音效（所有客户端）
            SoundEngine.PlaySound(CWRSound.Select with {
                Pitch = changed && TagItemSign > ItemID.None ? -0.2f : 0.2f
            });

            if (changed) {
                SendData();
            }
            return false;
        }

        /// <summary>
        /// 查找可用的存储目标
        /// 使用抽象存储系统自动支持箱子和Magic Storage等扩展
        /// </summary>
        internal IStorageProvider FindStorageTarget(Item item) {
            var storage = StorageLoader.FindStorageTarget(Position, maxFindChestMode, item);

            //找不到存储目标时显示提示
            if (storage == null && textIdleTime <= 0 && !VaultUtils.isClient) {
                CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text2.Value);
                textIdleTime = 300;

                //生成视觉提示粒子
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 220; i++) {
                        Vector2 spwanPos = PosInWorld + VaultUtils.RandVr(maxFindChestMode, maxFindChestMode + 1);
                        int dust = Dust.NewDust(spwanPos, 2, 2, DustID.OrangeTorch, 0, 0);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            return storage;
        }

        ///<summary>
        ///检查并生成机械臂(仅服务器端)
        ///</summary>
        private void SpawnArmsIfNeeded() {
            if (VaultUtils.isClient) return;
            if (dontSpawnArmTime > 0) return;

            //清理失效的索引
            ArmActorIndices.RemoveAll(index => !IsArmActorValid(index));

            if (ArmActorIndices.Count < 3) {
                int armSlot = ArmActorIndices.Count;
                int actorIndex = ActorLoader.NewActor<CollectorArm>(ArmPos, Vector2.Zero);
                ArmActorIndices.Add(actorIndex);
                ActorLoader.Actors[actorIndex].OnSpawn(Position, armSlot);
            }
        }

        public override void UpdateMachine() {
            FindFrame();
            consumeUE = 8;

            if (!workState) {
                return;
            }

            hoverSengs = HoverTP
                ? Math.Min(hoverSengs + 0.1f, 1f)
                : Math.Max(hoverSengs - 0.1f, 0f);

            if (textIdleTime > 0) {
                textIdleTime--;
            }
            if (dontSpawnArmTime > 0) {
                dontSpawnArmTime--;
            }

            //检查机械臂总数限制
            int totalArms = ActorLoader.GetActiveActors<CollectorArm>().Count;
            if (totalArms > 300) {
                if (textIdleTime <= 0) {
                    CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text1.Value);
                    textIdleTime = 300;
                }
                return;
            }

            SpawnArmsIfNeeded();

            //检查能量状态
            BatteryPrompt = MachineData.UEvalue < consumeUE;
            if (BatteryPrompt && textIdleTime <= 0) {
                CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text3.Value);
                textIdleTime = 300;
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            if (TagItemSign > ItemID.None) {
                VaultUtils.SimpleDrawItem(Main.spriteBatch, TagItemSign
                    , CenterInWorld - Main.screenPosition + new Vector2(0, 32)
                    , itemWidth: 32, 0, 0, Lighting.GetColor(Position.ToPoint()));
            }

            if (TagItemSign == ModContent.ItemType<ItemFilter>() && hoverSengs > 0.01f) {
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

            DrawChargeBar();
        }
    }
}
