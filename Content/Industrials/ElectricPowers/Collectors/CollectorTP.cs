using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.OtherMods.MagicStorage;
using Microsoft.Xna.Framework.Graphics;
using System;
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
        internal int ArmIndex0 = -1;
        internal int ArmIndex1 = -1;
        internal int ArmIndex2 = -1;
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
            data.Write(ArmIndex0);
            data.Write(ArmIndex1);
            data.Write(ArmIndex2);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            ItemFilter = ItemIO.Receive(reader);
            TagItemSign = reader.ReadInt32();
            BatteryPrompt = reader.ReadBoolean();
            workState = reader.ReadBoolean();
            ArmIndex0 = reader.ReadInt32();
            ArmIndex1 = reader.ReadInt32();
            ArmIndex2 = reader.ReadInt32();
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

        internal static bool IsArmValid(int armIndex) {
            if (armIndex < 0) return false;
            Projectile projectile = Main.projectile.FindByIdentity(armIndex);
            return projectile.Alives() && projectile.type == ModContent.ProjectileType<CollectorArm>();
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

        internal Chest FindChest(Item item) {
            Chest chest = Position.FindClosestChest(maxFindChestMode, true, (c) => c.CanItemBeAddedToChest(item));

            //只在服务器端显示提示
            if (chest == null && textIdleTime <= 0 && !VaultUtils.isClient) {
                CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text2.Value);
                textIdleTime = 300;

                //生成视觉提示粒子（客户端也会同步看到）
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 220; i++) {
                        Vector2 spwanPos = PosInWorld + VaultUtils.RandVr(maxFindChestMode, maxFindChestMode + 1);
                        int dust = Dust.NewDust(spwanPos, 2, 2, DustID.OrangeTorch, 0, 0);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }
            return chest;
        }

        /// <summary>
        /// 尝试查找Magic Storage存储核心
        /// </summary>
        internal object FindMagicStorage(Item item) {
            if (!ModLoader.HasMod("MagicStorage")) {
                return null;
            }

            try {
                return MSRef.FindMagicStorage(item, Position, maxFindChestMode);
            } catch {
                return null;
            }
        }

        /// <summary>
        /// 查找存储目标（箱子或Magic Storage）
        /// </summary>
        internal object FindStorageTarget(Item item) {
            //优先尝试查找箱子
            Chest chest = Position.FindClosestChest(maxFindChestMode, true, (c) => c.CanItemBeAddedToChest(item));

            if (chest != null) {
                return chest;
            }

            //如果没有箱子，尝试查找Magic Storage
            object magicStorage = FindMagicStorage(item);
            if (magicStorage != null) {
                return magicStorage;
            }

            //都找不到，显示提示
            if (textIdleTime <= 0 && !VaultUtils.isClient) {
                CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text2.Value);
                textIdleTime = 300;

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 220; i++) {
                        Vector2 spwanPos = PosInWorld + VaultUtils.RandVr(maxFindChestMode, maxFindChestMode + 1);
                        int dust = Dust.NewDust(spwanPos, 2, 2, DustID.OrangeTorch, 0, 0);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 检查并生成机械臂（仅服务器端）
        /// </summary>
        private void SpawnArmsIfNeeded() {
            if (VaultUtils.isClient) return;
            if (ArmPos.FindClosestPlayer(killerArmDistance) == null) return;
            if (dontSpawnArmTime > 0) return;

            bool needsSync = false;
            int armType = ModContent.ProjectileType<CollectorArm>();

            //检查并生成三个机械臂
            if (!IsArmValid(ArmIndex0)) {
                ArmIndex0 = Projectile.NewProjectileDirect(
                    this.FromObjectGetParent(), ArmPos, Vector2.Zero,
                    armType, 0, 0, -1, ai0: 0, ai1: 0
                ).identity;
                needsSync = true;
            }

            if (!IsArmValid(ArmIndex1)) {
                ArmIndex1 = Projectile.NewProjectileDirect(
                    this.FromObjectGetParent(), ArmPos, Vector2.Zero,
                    armType, 0, 0, -1, ai0: 0, ai1: 1
                ).identity;
                needsSync = true;
            }

            if (!IsArmValid(ArmIndex2)) {
                ArmIndex2 = Projectile.NewProjectileDirect(
                    this.FromObjectGetParent(), ArmPos, Vector2.Zero,
                    armType, 0, 0, -1, ai0: 0, ai1: 2
                ).identity;
                needsSync = true;
            }

            if (needsSync) {
                SendData();
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
            if (VaultUtils.CountProjectilesOfID<CollectorArm>() > 300) {
                if (textIdleTime <= 0) {
                    CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text1.Value);
                    textIdleTime = 300;
                }
                return;
            }

            //生成机械臂
            SpawnArmsIfNeeded();

            //检查能量状态
            BatteryPrompt = MachineData.UEvalue < consumeUE;
            if (BatteryPrompt && textIdleTime <= 0) {
                CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text3.Value);
                textIdleTime = 300;
            }
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            //只绘制属于当前收集器的机械臂
            int armType = ModContent.ProjectileType<CollectorArm>();

            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != armType) continue;

                int armSlot = (int)proj.ai[1];
                bool belongsToThis = armSlot == 0 && ArmIndex0 == proj.identity
                    || armSlot == 1 && ArmIndex1 == proj.identity
                    || armSlot == 2 && ArmIndex2 == proj.identity;

                if (belongsToThis) {
                    ((CollectorArm)proj.ModProjectile).DoDraw(Lighting.GetColor(proj.Center.ToTileCoordinates()));
                }
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
