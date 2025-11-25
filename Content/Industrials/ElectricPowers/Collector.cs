using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.OtherMods.MagicStorage;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class Collector : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Collector";
        internal static LocalizedText Text1;
        internal static LocalizedText Text2;
        internal static LocalizedText Text3;
        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "Excessive Quantity!");
            Text2 = this.GetLocalization(nameof(Text2), () => "There are no boxes around!");
            Text3 = this.GetLocalization(nameof(Text3), () => "Lack of Electricity!");
        }
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<CollectorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<DubiousPlating>(15).
                AddIngredient<MysteriousCircuitry>(20).
                AddRecipeGroup(CWRRecipes.TungstenBarGroup, 8).
                AddIngredient(ItemID.Hook, 3).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class CollectorTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/CollectorTile";
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorStartTile")]
        public static Asset<Texture2D> startAsset = null;
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorStartTileGlow")]
        public static Asset<Texture2D> startGlowAsset = null;
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorTileGlow")]
        public static Asset<Texture2D> tileGlowAsset = null;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<Collector>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(1, 3);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<Collector>());

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out CollectorTP collector)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += collector.frame * 18 * 5;
            Texture2D tex = collector.workState ? TextureAssets.Tile[Type].Value : startAsset.Value;
            Texture2D glow = collector.workState ? tileGlowAsset.Value : startGlowAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

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
            Chest chest = Position.FindClosestChest(maxFindChestMode, true, (Chest c) => c.CanItemBeAddedToChest(item));

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
            Chest chest = Position.FindClosestChest(maxFindChestMode, true, (Chest c) => c.CanItemBeAddedToChest(item));

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
                bool belongsToThis = (armSlot == 0 && ArmIndex0 == proj.identity)
                    || (armSlot == 1 && ArmIndex1 == proj.identity)
                    || (armSlot == 2 && ArmIndex2 == proj.identity);

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

    /// <summary>
    /// 机械臂状态枚举
    /// </summary>
    internal enum ArmState : byte
    {
        Idle = 0,           //待机
        Searching = 1,      //搜索目标
        MovingToItem = 2,   //移动到物品
        Grasping = 3,       //抓取物品
        MovingToChest = 4,  //移动到箱子
        Depositing = 5      //存放物品
    }

    internal class CollectorArm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalArm")]
        private static Asset<Texture2D> arm = null;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalClamp")]
        private static Asset<Texture2D> clamp = null;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalClampGlow")]
        private static Asset<Texture2D> clampGlow = null;

        //核心引用
        internal CollectorTP collectorTP;
        internal Vector2 startPos;
        private Item graspItem;
        private bool initialized;

        //存储目标（使用坐标而不是直接引用）
        private Point16 targetChestPos = Point16.NegativeOne;
        private Point16 targetMagicStoragePos = Point16.NegativeOne;
        private bool isMagicStorageTarget = false;

        //物理模拟参数
        private Vector2 velocity;
        private Vector2 targetPosition;
        private const float SpringStiffness = 0.15f;
        private const float Damping = 0.85f;
        private const float MaxSpeed = 16f;
        private const float ArrivalThreshold = 8f;

        //视觉效果参数（仅客户端）
        private float clampOpenness = 0f;
        private float shakeIntensity = 0f;
        private int particleTimer = 0;
        private float rotationVelocity = 0f;

        //状态机
        private ArmState currentState = ArmState.Idle;
        private int stateTimer = 0;
        private int targetItemWhoAmI = -1;

        //搜索冷却（避免频繁搜索）
        private int searchCooldown = 0;

        //不重要物品列表
        private readonly static HashSet<int> unimportances = [
            ItemID.Heart, ItemID.CandyCane, ItemID.CandyApple,
            ItemID.Star, ItemID.SoulCake
        ];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.WriteVector2(startPos);
            writer.WriteVector2(velocity);
            writer.WriteVector2(targetPosition);
            writer.Write((byte)currentState);
            writer.Write(targetItemWhoAmI);
            writer.Write(targetChestPos.X);
            writer.Write(targetChestPos.Y);
            writer.Write(targetMagicStoragePos.X);
            writer.Write(targetMagicStoragePos.Y);
            writer.Write(isMagicStorageTarget);

            graspItem ??= new Item();
            ItemIO.Send(graspItem, writer, true);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            startPos = reader.ReadVector2();
            velocity = reader.ReadVector2();
            targetPosition = reader.ReadVector2();
            currentState = (ArmState)reader.ReadByte();
            targetItemWhoAmI = reader.ReadInt32();
            targetChestPos = new Point16(reader.ReadInt16(), reader.ReadInt16());
            targetMagicStoragePos = new Point16(reader.ReadInt16(), reader.ReadInt16());
            isMagicStorageTarget = reader.ReadBoolean();

            graspItem = ItemIO.Receive(reader, true);
        }

        /// <summary>
        /// 查找最近的可收集物品（仅服务器端）
        /// </summary>
        private Item FindNearestItem() {
            if (VaultUtils.isClient) return null;

            Item bestItem = null;
            float minDistSQ = 4000000f;
            int itemFilterType = ModContent.ItemType<ItemFilter>();

            foreach (var item in Main.ActiveItems) {
                if (!IsValidTarget(item)) continue;

                //检查过滤器
                if (collectorTP.TagItemSign == itemFilterType) {
                    var filterData = collectorTP.ItemFilter.GetGlobalItem<ItemFilterData>();
                    if (!filterData.Items.Contains(item.type)) {
                        continue;
                    }
                }
                else if (collectorTP.TagItemSign > ItemID.None && item.type != collectorTP.TagItemSign) {
                    continue;
                }

                //提前检查存储目标（避免抓取后无处存放）
                if (collectorTP.FindStorageTarget(item) == null) {
                    continue;
                }

                float distSQ = item.Center.DistanceSQ(Projectile.Center);
                if (distSQ < minDistSQ) {
                    bestItem = item;
                    minDistSQ = distSQ;
                }
            }

            return bestItem;
        }

        /// <summary>
        /// 检查物品是否为有效目标
        /// </summary>
        private bool IsValidTarget(Item item) {
            if (item.IsAir || !item.active) return false;
            if (unimportances.Contains(item.type)) return false;

            int targetCollector = item.CWR().TargetByCollector;
            //只接受未被锁定或被自己锁定的物品
            if (targetCollector >= 0 && targetCollector != Projectile.identity) return false;

            return true;
        }

        /// <summary>
        /// 获取目标箱子（通过坐标）
        /// </summary>
        private Chest GetTargetChest() {
            if (targetChestPos == Point16.NegativeOne) return null;
            int index = Chest.FindChest(targetChestPos.X, targetChestPos.Y);
            return index >= 0 ? Main.chest[index] : null;
        }

        /// <summary>
        /// 获取目标Magic Storage（通过坐标）
        /// </summary>
        private object GetTargetMagicStorage() {
            if (targetMagicStoragePos == Point16.NegativeOne) return null;
            if (!ModLoader.HasMod("MagicStorage")) return null;

            try {
                return MSRef.FindMagicStorage(graspItem, targetMagicStoragePos, CollectorTP.maxFindChestMode);
            } catch {
                return null;
            }
        }

        /// <summary>
        /// 弹簧物理模拟移动
        /// </summary>
        private void SpringPhysicsMove(Vector2 target, float speedMultiplier = 1f) {
            Vector2 toTarget = target - Projectile.Center;

            //弹簧力
            Vector2 springForce = toTarget * SpringStiffness * speedMultiplier;
            velocity += springForce;

            //阻尼
            velocity *= Damping;

            //限速
            if (velocity.LengthSquared() > MaxSpeed * MaxSpeed) {
                velocity = Vector2.Normalize(velocity) * MaxSpeed;
            }

            Projectile.Center += velocity;

            //平滑旋转
            if (velocity.LengthSquared() > 0.1f) {
                float targetRotation = velocity.ToRotation();
                float rotationDiff = MathHelper.WrapAngle(targetRotation - Projectile.rotation);
                rotationVelocity = MathHelper.Lerp(rotationVelocity, rotationDiff * 0.2f, 0.3f);
                Projectile.rotation += rotationVelocity;
            }
        }

        /// <summary>
        /// 生成机械粒子（仅客户端，降低频率）
        /// </summary>
        private void SpawnMechanicalParticles(bool intensive = false) {
            if (Main.netMode == NetmodeID.Server) return;

            particleTimer++;
            int spawnRate = intensive ? 8 : 16;

            if (particleTimer % spawnRate == 0) {
                Vector2 particleVel = velocity * 0.2f + Main.rand.NextVector2Circular(2, 2);
                Dust dust = Dust.NewDustDirect(Projectile.Center - Vector2.One * 8, 16, 16,
                    DustID.Electric, particleVel.X, particleVel.Y, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }

        private void State_Idle() {
            stateTimer++;
            searchCooldown = Math.Max(0, searchCooldown - 1);

            clampOpenness = MathHelper.Lerp(clampOpenness, 1f, 0.1f);
            shakeIntensity *= 0.9f;

            //每30帧且冷却结束后搜索
            if (stateTimer >= 30 && searchCooldown == 0 && collectorTP.MachineData.UEvalue >= collectorTP.consumeUE) {
                if (!VaultUtils.isClient) {
                    TransitionToState(ArmState.Searching);
                }
            }

            Vector2 idleOffset = GetIdleOffset();
            SpringPhysicsMove(startPos + idleOffset, 0.8f);
        }

        private void State_Searching() {
            //只在服务器端搜索
            if (!VaultUtils.isClient) {
                Item foundItem = FindNearestItem();

                if (foundItem != null) {
                    targetItemWhoAmI = foundItem.whoAmI;
                    foundItem.CWR().TargetByCollector = Projectile.identity;

                    //消耗能量
                    collectorTP.MachineData.UEvalue -= collectorTP.consumeUE;
                    collectorTP.SendData();

                    TransitionToState(ArmState.MovingToItem);
                }
                else {
                    searchCooldown = 60; //设置搜索冷却
                    TransitionToState(ArmState.Idle);
                }
            }

            //播放音效（所有客户端）
            if (stateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        private void State_MovingToItem() {
            if (targetItemWhoAmI < 0 || targetItemWhoAmI >= Main.maxItems) {
                TransitionToState(ArmState.Idle);
                return;
            }

            Item targetItem = Main.item[targetItemWhoAmI];
            if (!IsValidTarget(targetItem) || (targetItem.CWR().TargetByCollector != Projectile.identity && targetItem.CWR().TargetByCollector != -1)) {
                TransitionToState(ArmState.Idle);
                return;
            }

            targetPosition = targetItem.Center;
            SpringPhysicsMove(targetPosition, 1.2f);
            SpawnMechanicalParticles();

            clampOpenness = MathHelper.Lerp(clampOpenness, 0.8f, 0.15f);

            if (Projectile.Distance(targetPosition) < ArrivalThreshold) {
                TransitionToState(ArmState.Grasping);
            }
        }

        private void State_Grasping() {
            stateTimer++;

            if (targetItemWhoAmI < 0 || targetItemWhoAmI >= Main.maxItems) {
                TransitionToState(ArmState.Idle);
                return;
            }

            Item targetItem = Main.item[targetItemWhoAmI];

            clampOpenness = MathHelper.Lerp(clampOpenness, 0f, 0.25f);
            shakeIntensity = 1.5f;

            targetPosition = targetItem.Center;
            SpringPhysicsMove(targetPosition, 0.5f);

            SpawnMechanicalParticles(intensive: true);

            //抓取完成（仅服务器端处理物品）
            if (stateTimer > 12) {
                if (!VaultUtils.isClient) {
                    graspItem = targetItem.Clone();
                    targetItem.TurnToAir();
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, targetItem.whoAmI);

                    //查找存储目标（箱子或Magic Storage）
                    object storageTarget = collectorTP.FindStorageTarget(graspItem);

                    if (storageTarget is Chest chest) {
                        targetChestPos = new Point16(chest.x, chest.y);
                        targetMagicStoragePos = Point16.NegativeOne;
                        isMagicStorageTarget = false;
                        graspItem.CWR().TargetByCollector = Projectile.identity;
                        TransitionToState(ArmState.MovingToChest);
                    }
                    else if (storageTarget != null && ModLoader.HasMod("MagicStorage")) {
                        //Magic Storage目标
                        try {
                            var heartType = CWRMod.Instance.magicStorage.Find<ModTileEntity>("TEStorageHeart").Type;
                            foreach (var te in TileEntity.ByID.Values) {
                                if (te.type == heartType && te == storageTarget) {
                                    targetMagicStoragePos = te.Position;
                                    targetChestPos = Point16.NegativeOne;
                                    isMagicStorageTarget = true;
                                    graspItem.CWR().TargetByCollector = Projectile.identity;
                                    TransitionToState(ArmState.MovingToChest);
                                    break;
                                }
                            }
                        } catch {
                            //找不到Magic Storage，丢弃物品
                            VaultUtils.SpwanItem(Projectile.GetSource_DropAsItem(), Projectile.Hitbox, graspItem);
                            graspItem.TurnToAir();
                            TransitionToState(ArmState.Idle);
                        }
                    }
                    else {
                        //找不到存储位置，丢弃物品
                        VaultUtils.SpwanItem(Projectile.GetSource_DropAsItem(), Projectile.Hitbox, graspItem);
                        graspItem.TurnToAir();
                        TransitionToState(ArmState.Idle);
                    }
                }

                //音效和特效（所有客户端）
                if (stateTimer == 13) {
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);

                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 15; i++) {
                            Vector2 particleVel = Main.rand.NextVector2Circular(4, 4);
                            Dust dust = Dust.NewDustDirect(Projectile.Center - Vector2.One * 16, 32, 32,
                                DustID.Electric, particleVel.X, particleVel.Y, 100, Color.Cyan, 1.5f);
                            dust.noGravity = true;
                        }
                    }
                }
            }
        }

        private void State_MovingToChest() {
            if (graspItem == null || graspItem.type == ItemID.None) {
                TransitionToState(ArmState.Idle);
                return;
            }

            //确定目标位置
            Vector2 targetPos;
            if (isMagicStorageTarget && targetMagicStoragePos != Point16.NegativeOne) {
                targetPos = targetMagicStoragePos.ToWorldCoordinates() + new Vector2(8, 8);
            }
            else if (!isMagicStorageTarget && targetChestPos != Point16.NegativeOne) {
                targetPos = targetChestPos.ToWorldCoordinates() + new Vector2(8, 8);
            }
            else {
                //目标失效
                if (!VaultUtils.isClient) {
                    VaultUtils.SpwanItem(Projectile.GetSource_DropAsItem(), Projectile.Hitbox, graspItem);
                    graspItem.TurnToAir();
                }
                TransitionToState(ArmState.Idle);
                return;
            }

            targetPosition = targetPos;
            SpringPhysicsMove(targetPosition, 1.0f);

            graspItem.Center = Projectile.Center;
            SpawnMechanicalParticles();

            //到达目标
            Rectangle targetRect;
            if (isMagicStorageTarget) {
                targetRect = targetMagicStoragePos.ToWorldCoordinates().GetRectangle(48, 48);
            }
            else {
                targetRect = targetChestPos.ToWorldCoordinates().GetRectangle(32);
            }

            if (Projectile.Hitbox.Intersects(targetRect)) {
                TransitionToState(ArmState.Depositing);
            }
        }

        private void State_Depositing() {
            stateTimer++;

            clampOpenness = MathHelper.Lerp(clampOpenness, 1f, 0.2f);
            shakeIntensity = 0.8f;

            SpawnMechanicalParticles(intensive: true);

            if (stateTimer > 10) {
                //只在服务器端处理物品存储
                if (!VaultUtils.isClient) {
                    if (isMagicStorageTarget && ModLoader.HasMod("MagicStorage")) {
                        //存储到Magic Storage
                        try {
                            object magicStorage = GetTargetMagicStorage();
                            if (magicStorage != null) {
                                MSRef.DepositItemMethod?.Invoke(magicStorage, [graspItem]);
                            }
                        } catch {
                            //失败则掉落物品
                            VaultUtils.SpwanItem(Projectile.GetSource_DropAsItem(), Projectile.Hitbox, graspItem);
                        }
                    }
                    else {
                        //存储到箱子
                        Chest targetChest = GetTargetChest();
                        if (targetChest != null) {
                            targetChest.eatingAnimationTime = 20;
                            targetChest.AddItem(graspItem, true);
                            CheckCoins(targetChest);
                        }
                    }

                    graspItem.TurnToAir();
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, graspItem.whoAmI);
                    }
                }

                //音效（所有客户端）
                if (stateTimer == 11) {
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
                }

                if (stateTimer > 15) {
                    TransitionToState(ArmState.Idle);
                }
            }
        }

        private void TransitionToState(ArmState newState) {
            currentState = newState;
            stateTimer = 0;

            if (newState == ArmState.Idle) {
                targetItemWhoAmI = -1;
                targetChestPos = Point16.NegativeOne;
                targetMagicStoragePos = Point16.NegativeOne;
                isMagicStorageTarget = false;
            }

            //只在服务器端触发网络更新
            if (!VaultUtils.isClient) {
                Projectile.netUpdate = true;
            }
        }

        private Vector2 GetIdleOffset() {
            return Projectile.ai[1] switch {
                1 => new Vector2(120, -20),
                2 => new Vector2(-120, -20),
                _ => new Vector2(0, -120)
            };
        }

        private static void CheckCoins(Chest chest) {
            long totalValue = 0;

            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item != null && !item.IsAir && item.IsACoin) {
                    int value = item.type switch {
                        ItemID.SilverCoin => 100,
                        ItemID.GoldCoin => 10000,
                        ItemID.PlatinumCoin => 1000000,
                        _ => 1
                    };
                    totalValue += (long)value * item.stack;
                    item.TurnToAir();
                }
            }

            if (totalValue <= 0) return;

            if (totalValue >= 1000000) {
                chest.AddItem(new Item(ItemID.PlatinumCoin, (int)(totalValue / 1000000)));
                totalValue %= 1000000;
            }
            if (totalValue >= 10000) {
                chest.AddItem(new Item(ItemID.GoldCoin, (int)(totalValue / 10000)));
                totalValue %= 10000;
            }
            if (totalValue >= 100) {
                chest.AddItem(new Item(ItemID.SilverCoin, (int)(totalValue / 100)));
                totalValue %= 100;
            }
            if (totalValue > 0) {
                chest.AddItem(new Item(ItemID.CopperCoin, (int)totalValue));
            }
        }

        public override void AI() {
            if (!initialized) {
                if (!VaultUtils.isClient) {
                    startPos = Projectile.Center;
                    velocity = Vector2.Zero;
                    Projectile.netUpdate = true;
                }
                initialized = true;
            }

            if (!TileProcessorLoader.AutoPositionGetTP(startPos.ToTileCoordinates16(), out collectorTP)) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            startPos = collectorTP.ArmPos;

            if (startPos.FindClosestPlayer(CollectorTP.killerArmDistance) == null) {
                if (!VaultUtils.isClient) {
                    collectorTP.dontSpawnArmTime = 60;
                }
                Projectile.Kill();
                return;
            }

            //状态机驱动
            switch (currentState) {
                case ArmState.Idle:
                    State_Idle();
                    break;
                case ArmState.Searching:
                    State_Searching();
                    break;
                case ArmState.MovingToItem:
                    State_MovingToItem();
                    break;
                case ArmState.Grasping:
                    State_Grasping();
                    break;
                case ArmState.MovingToChest:
                    State_MovingToChest();
                    break;
                case ArmState.Depositing:
                    State_Depositing();
                    break;
            }

            shakeIntensity *= 0.92f;
        }

        internal void DoDraw(Color lightColor) {
            if (startPos == Vector2.Zero) {
                return;
            }

            if (collectorTP?.BatteryPrompt == true) {
                lightColor = new Color(lightColor.R / 2, lightColor.G / 2, lightColor.B / 2, 255);
            }

            Texture2D tex = arm.Value;
            Vector2 start = startPos;
            Vector2 end = Projectile.Center;

            //添加抖动效果
            if (shakeIntensity > 0.01f) {
                end += Main.rand.NextVector2Circular(shakeIntensity * 2, shakeIntensity * 2);
            }

            //动态贝塞尔曲线控制点
            float dist = Vector2.Distance(start, end);
            float bendHeight = MathHelper.Clamp(dist * 0.5f, 40f, 200f);

            //根据速度添加动态弯曲
            float velocityInfluence = velocity.Length() * 2f;
            bendHeight += velocityInfluence;

            Vector2 midControl = (start + end) / 2 + new Vector2(0, -bendHeight);

            //计算曲线长度
            int sampleCount = 60;
            float curveLength = 0f;
            Vector2 prev = start;
            for (int i = 1; i <= sampleCount; i++) {
                float t = i / (float)sampleCount;
                Vector2 point = Vector2.Lerp(
                    Vector2.Lerp(start, midControl, t),
                    Vector2.Lerp(midControl, end, t),
                    t
                );
                curveLength += Vector2.Distance(prev, point);
                prev = point;
            }

            float segmentLength = tex.Height / 2;
            int segmentCount = Math.Max(2, (int)(curveLength / segmentLength));
            Vector2[] points = new Vector2[segmentCount + 1];

            for (int i = 0; i <= segmentCount; i++) {
                float t = i / (float)segmentCount;
                points[i] = Vector2.Lerp(
                    Vector2.Lerp(start, midControl, t),
                    Vector2.Lerp(midControl, end, t),
                    t
                );
            }

            float clampRot = Projectile.rotation;

            //绘制机械臂
            for (int i = 0; i < segmentCount; i++) {
                Vector2 pos = points[i];
                Vector2 next = points[i + 1];
                Vector2 direction = next - pos;
                Color color = Lighting.GetColor((pos / 16).ToPoint());
                float rotation = direction.ToRotation() + MathHelper.PiOver2;

                if (i == segmentCount - 1) {
                    clampRot = direction.ToRotation();
                }

                //添加轻微的缩放动画
                float scale = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + i * 0.5f) * 0.02f;

                Main.spriteBatch.Draw(tex, pos - Main.screenPosition, null, color, rotation
                    , new Vector2(tex.Width / 2f, tex.Height), scale, SpriteEffects.None, 0f);
            }

            //绘制夹子
            int clampFrame = clampOpenness > 0.5f ? 0 : 1;

            Main.spriteBatch.Draw(clamp.Value, Projectile.Center - Main.screenPosition
                , clamp.Value.GetRectangle(clampFrame, 2)
                , lightColor, clampRot + MathHelper.PiOver2
                , clamp.Value.GetOrig(2), 1f, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(clampGlow.Value, Projectile.Center - Main.screenPosition
                , clampGlow.Value.GetRectangle(clampFrame, 2)
                , Color.White * (0.7f + shakeIntensity * 0.3f), clampRot + MathHelper.PiOver2
                , clampGlow.Value.GetOrig(2), 1f, SpriteEffects.None, 0f);

            //绘制抓取的物品
            if (graspItem != null && !graspItem.IsAir) {
                VaultUtils.SimpleDrawItem(Main.spriteBatch, graspItem.type
                    , Projectile.Center - Main.screenPosition, 1f
                    , clampRot + MathHelper.PiOver2, lightColor);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
