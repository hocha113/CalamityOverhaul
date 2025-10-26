using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public override bool RightClick(int i, int j) {
            if (TileProcessorLoader.AutoPositionGetTP(i, j, out CollectorTP collector)) {
                collector.RightClick(Main.LocalPlayer);
            }
            return base.RightClick(i, j);
        }

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
                if (Main.dedServ) {
                    SendData();
                }
                SoundEngine.PlaySound(CWRSound.CollectorStart, PosInWorld);
            }
            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        internal static void CheckArm(ref int armIndex, int armID, Vector2 armPos) {
            if (armIndex < 0) {
                return;
            }

            Projectile projectile = Main.projectile.FindByIdentity(armIndex);
            if (!projectile.Alives() || projectile.type != armID) {
                armIndex = -1;
                return;
            }
        }

        internal void RightClick(Player player) {
            Item item = player.GetItem();
            if (!item.Alives()) {
                if (TagItemSign != ItemID.None) {
                    SoundEngine.PlaySound(CWRSound.Select with { Pitch = 0.2f });
                    TagItemSign = ItemID.None;
                }
                return;
            }
            if (TagItemSign > ItemID.None && TagItemSign == item.type) {
                SoundEngine.PlaySound(CWRSound.Select with { Pitch = 0.2f });
                TagItemSign = ItemID.None;
                return;
            }
            SoundEngine.PlaySound(CWRSound.Select with { Pitch = -0.2f });
            TagItemSign = item.type;

            if (TagItemSign == ModContent.ItemType<ItemFilter>()) {
                ItemFilter = item.Clone();
                var data = ItemFilter.GetGlobalItem<ItemFilterData>().Items.ToHashSet();
                ItemFilter.GetGlobalItem<ItemFilterData>().Items = data;
            }

            SendData();
        }

        internal Chest FindChest(Item item) {
            Chest chest = Position.FindClosestChest(maxFindChestMode, true, (Chest c) => c.CanItemBeAddedToChest(item));
            if (chest == null && textIdleTime <= 0) {
                CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text2.Value);
                textIdleTime = 300;
                for (int i = 0; i < 220; i++) {
                    Vector2 spwanPos = PosInWorld + VaultUtils.RandVr(maxFindChestMode, maxFindChestMode + 1);
                    int dust = Dust.NewDust(spwanPos, 2, 2, DustID.OrangeTorch, 0, 0);
                    Main.dust[dust].noGravity = true;
                }
            }
            return chest;
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

            if (VaultUtils.CountProjectilesOfID<CollectorArm>() > 300) {
                if (textIdleTime <= 0) {
                    CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text1.Value);
                    textIdleTime = 300;
                }
                return;
            }

            if (ArmPos.FindClosestPlayer(killerArmDistance) != null && dontSpawnArmTime <= 0 && !VaultUtils.isClient) {
                bool doNet = false;

                CheckArm(ref ArmIndex0, ModContent.ProjectileType<CollectorArm>(), ArmPos);
                if (ArmIndex0 == -1) {
                    ArmIndex0 = Projectile.NewProjectileDirect(this.FromObjectGetParent(), ArmPos
                        , Vector2.Zero, ModContent.ProjectileType<CollectorArm>(), 0, 0, -1, ai0: 0, ai1: 0).identity;
                    doNet = true;
                }

                CheckArm(ref ArmIndex1, ModContent.ProjectileType<CollectorArm>(), ArmPos);
                if (ArmIndex1 == -1) {
                    ArmIndex1 = Projectile.NewProjectileDirect(this.FromObjectGetParent(), ArmPos
                        , Vector2.Zero, ModContent.ProjectileType<CollectorArm>(), 0, 0, -1, ai0: 0, ai1: 1).identity;
                    doNet = true;
                }

                CheckArm(ref ArmIndex2, ModContent.ProjectileType<CollectorArm>(), ArmPos);
                if (ArmIndex2 == -1) {
                    ArmIndex2 = Projectile.NewProjectileDirect(this.FromObjectGetParent(), ArmPos
                        , Vector2.Zero, ModContent.ProjectileType<CollectorArm>(), 0, 0, -1, ai0: 0, ai1: 2).identity;
                    doNet = true;
                }

                if (doNet) {
                    SendData();
                }
            }

            BatteryPrompt = MachineData.UEvalue < consumeUE;
            if (BatteryPrompt) {
                if (textIdleTime <= 0) {
                    CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text3.Value);
                    textIdleTime = 300;
                }
            }
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<CollectorArm>()) {
                    continue;
                }
                if ((proj.ai[1] == 0 && ArmIndex0 == proj.identity)
                    || (proj.ai[1] == 1 && ArmIndex1 == proj.identity)
                    || (proj.ai[1] == 2 && ArmIndex2 == proj.identity)) {
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
                int[] filterItems = [.. ItemFilter.GetGlobalItem<ItemFilterData>().Items];
                if (filterItems.Length > 0) {
                    const float maxRadius = 150f;
                    float currentRadius = maxRadius * hoverSengs;
                    float angleIncrement = MathHelper.TwoPi / filterItems.Length;

                    Vector2 drawCenter = CenterInWorld - Main.screenPosition + new Vector2(0, 32);

                    for (int i = 0; i < filterItems.Length; i++) {
                        if (filterItems[i] <= ItemID.None) continue;

                        float currentAngle = angleIncrement * i - MathHelper.PiOver2;
                        Vector2 offset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * currentRadius;
                        Vector2 itemPos = drawCenter + offset;

                        Color drawColor = VaultUtils.MultiStepColorLerp(hoverSengs, Lighting.GetColor(Position.ToPoint()), Color.White);
                        float scale = hoverSengs * 1.25f;

                        VaultUtils.SafeLoadItem(filterItems[i]);
                        VaultUtils.SimpleDrawItem(Main.spriteBatch, filterItems[i], itemPos, itemWidth: 32, scale, 0, drawColor);
                    }
                }
            }

            DrawChargeBar();
        }
    }

    /// <summary>
    /// 机械臂状态枚举
    /// </summary>
    internal enum ArmState
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
        private bool spawn;
        internal Chest targetChest;

        //物理模拟参数
        private Vector2 velocity;
        private Vector2 targetPosition;
        private const float SpringStiffness = 0.15f;      //弹簧刚度
        private const float Damping = 0.85f;              //阻尼系数
        private const float MaxSpeed = 16f;               //最大速度
        private const float ArrivalThreshold = 8f;        //到达阈值

        //视觉效果参数
        private float clampOpenness = 0f;                 //夹子开合度 (0=闭合, 1=完全打开)
        private float shakeIntensity = 0f;                //抖动强度
        private int particleTimer = 0;                    //粒子生成计时器
        private float rotationVelocity = 0f;              //旋转速度
        
        //状态机
        private ArmState currentState = ArmState.Idle;
        private int stateTimer = 0;
        private int targetItemWhoAmI = -1;

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
            writer.Write(clampOpenness);
            writer.Write(shakeIntensity);
            
            graspItem ??= new Item();
            ItemIO.Send(graspItem, writer, true);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            startPos = reader.ReadVector2();
            velocity = reader.ReadVector2();
            targetPosition = reader.ReadVector2();
            currentState = (ArmState)reader.ReadByte();
            targetItemWhoAmI = reader.ReadInt32();
            clampOpenness = reader.ReadSingle();
            shakeIntensity = reader.ReadSingle();
            
            graspItem = ItemIO.Receive(reader, true);
        }

        /// <summary>
        /// 查找最近的可收集物品
        /// </summary>
        private Item FindNearestItem() {
            Item bestItem = null;
            float minDistSQ = 4000000f;
            int itemFilterType = ModContent.ItemType<ItemFilter>();

            foreach (var item in Main.ActiveItems) {
                if (!IsValidTarget(item)) continue;

                //检查过滤器
                if (collectorTP.TagItemSign == itemFilterType) {
                    if (!collectorTP.ItemFilter.GetGlobalItem<ItemFilterData>().Items.Contains(item.type)) {
                        continue;
                    }
                }
                else if (collectorTP.TagItemSign > ItemID.None && item.type != collectorTP.TagItemSign) {
                    continue;
                }

                //提前检查是否有箱子可用
                if (collectorTP.FindChest(item) == null) {
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
            
            var targetInfo = item.CWR().TargetByCollector;
            if (targetInfo >= 0 && targetInfo != Projectile.identity) return false;

            return true;
        }

        /// <summary>
        /// 弹簧物理模拟移动
        /// </summary>
        private void SpringPhysicsMove(Vector2 target, float speedMultiplier = 1f) {
            Vector2 toTarget = target - Projectile.Center;
            
            //弹簧力 = 刚度 × 位移
            Vector2 springForce = toTarget * SpringStiffness * speedMultiplier;
            
            //应用力到速度
            velocity += springForce;
            
            //应用阻尼
            velocity *= Damping;
            
            //限制最大速度
            if (velocity.LengthSquared() > MaxSpeed * MaxSpeed) {
                velocity = Vector2.Normalize(velocity) * MaxSpeed;
            }
            
            //更新位置
            Projectile.Center += velocity;
            
            //平滑旋转
            float targetRotation = velocity.ToRotation();
            float rotationDifference = MathHelper.WrapAngle(targetRotation - Projectile.rotation);
            rotationVelocity = MathHelper.Lerp(rotationVelocity, rotationDifference * 0.2f, 0.3f);
            Projectile.rotation += rotationVelocity;
        }

        /// <summary>
        /// 生成机械效果粒子
        /// </summary>
        private void SpawnMechanicalParticles(bool intensive = false) {
            particleTimer++;
            int spawnRate = intensive ? 6 : 12;
            
            if (particleTimer % spawnRate == 0) {
                Vector2 particleVel = velocity * 0.2f + Main.rand.NextVector2Circular(2, 2);
                Dust dust = Dust.NewDustDirect(Projectile.Center - Vector2.One * 8, 16, 16,
                    DustID.Electric, particleVel.X, particleVel.Y, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }

        /// <summary>
        /// 状态机，待机状态
        /// </summary>
        private void State_Idle() {
            stateTimer++;
            
            //平滑打开夹子
            clampOpenness = MathHelper.Lerp(clampOpenness, 1f, 0.1f);
            shakeIntensity *= 0.9f;

            //每30帧搜索一次
            if (stateTimer >= 30 && collectorTP.MachineData.UEvalue >= collectorTP.consumeUE) {
                TransitionToState(ArmState.Searching);
            }

            //回到待机位置
            Vector2 idleOffset = GetIdleOffset();
            SpringPhysicsMove(startPos + idleOffset, 0.8f);
        }

        /// <summary>
        /// 状态机，搜索状态
        /// </summary>
        private void State_Searching() {
            Item foundItem = FindNearestItem();
            
            if (foundItem != null) {
                targetItemWhoAmI = foundItem.whoAmI;
                foundItem.CWR().TargetByCollector = Projectile.identity;
                
                //消耗能量
                if (!VaultUtils.isClient) {
                    collectorTP.MachineData.UEvalue -= collectorTP.consumeUE;
                    collectorTP.SendData();
                }
                
                //播放声音
                SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
                
                TransitionToState(ArmState.MovingToItem);
            }
            else {
                TransitionToState(ArmState.Idle);
            }
        }

        /// <summary>
        /// 状态机，移动到物品
        /// </summary>
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

            //夹子准备抓取
            clampOpenness = MathHelper.Lerp(clampOpenness, 0.8f, 0.15f);

            if (Projectile.Distance(targetPosition) < ArrivalThreshold) {
                TransitionToState(ArmState.Grasping);
            }
        }

        /// <summary>
        /// 状态机，抓取物品
        /// </summary>
        private void State_Grasping() {
            stateTimer++;
            
            Item targetItem = Main.item[targetItemWhoAmI];
            
            //夹子快速闭合
            clampOpenness = MathHelper.Lerp(clampOpenness, 0f, 0.25f);
            shakeIntensity = 1.5f;
            
            //保持在物品位置
            targetPosition = targetItem.Center;
            SpringPhysicsMove(targetPosition, 0.5f);
            
            SpawnMechanicalParticles(intensive: true);

            //抓取完成
            if (stateTimer > 12) {
                graspItem = targetItem.Clone();
                targetItem.TurnToAir();

                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, targetItem.whoAmI);
                }

                targetChest = collectorTP.FindChest(graspItem);
                graspItem.CWR().TargetByCollector = Projectile.identity;

                //播放抓取音效
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);
                
                //生成抓取特效
                for (int i = 0; i < 15; i++) {
                    Vector2 particleVel = Main.rand.NextVector2Circular(4, 4);
                    Dust dust = Dust.NewDustDirect(Projectile.Center - Vector2.One * 16, 32, 32,
                        DustID.Electric, particleVel.X, particleVel.Y, 100, Color.Cyan, 1.5f);
                    dust.noGravity = true;
                }

                TransitionToState(ArmState.MovingToChest);
            }
        }

        /// <summary>
        /// 状态机，移动到箱子
        /// </summary>
        private void State_MovingToChest() {
            if (graspItem == null || graspItem.type == ItemID.None) {
                TransitionToState(ArmState.Idle);
                return;
            }

            if (targetChest == null || !Main.chest.Contains(targetChest)) {
                //箱子失效，扔掉物品
                if (!VaultUtils.isClient) {
                    VaultUtils.SpwanItem(Projectile.GetSource_DropAsItem(), Projectile.Hitbox, graspItem);
                }
                graspItem.TurnToAir();
                TransitionToState(ArmState.Idle);
                return;
            }

            Vector2 chestPos = new Vector2(targetChest.x, targetChest.y) * 16 + new Vector2(8, 8);
            targetPosition = chestPos;
            SpringPhysicsMove(targetPosition, 1.0f);
            
            graspItem.Center = Projectile.Center;
            SpawnMechanicalParticles();

            if (Projectile.Hitbox.Intersects(targetChest.GetPoint16().ToWorldCoordinates().GetRectangle(32))) {
                TransitionToState(ArmState.Depositing);
            }
        }

        /// <summary>
        /// 状态机，存放物品
        /// </summary>
        private void State_Depositing() {
            stateTimer++;
            
            //夹子打开
            clampOpenness = MathHelper.Lerp(clampOpenness, 1f, 0.2f);
            shakeIntensity = 0.8f;
            
            SpawnMechanicalParticles(intensive: true);

            if (stateTimer > 10) {
                targetChest.eatingAnimationTime = 20;
                targetChest.AddItem(graspItem, true);
                CheckCoins(targetChest);
                
                //播放存放音效
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
                
                graspItem.TurnToAir();
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, graspItem.whoAmI);
                }

                TransitionToState(ArmState.Idle);
            }
        }

        /// <summary>
        /// 状态转换
        /// </summary>
        private void TransitionToState(ArmState newState) {
            currentState = newState;
            stateTimer = 0;
            
            if (newState == ArmState.Idle) {
                targetItemWhoAmI = -1;
                targetChest = null;
            }
            
            if (!VaultUtils.isClient) {
                Projectile.netUpdate = true;
            }
        }

        /// <summary>
        /// 获取待机偏移位置
        /// </summary>
        private Vector2 GetIdleOffset() {
            return Projectile.ai[1] switch {
                1 => new Vector2(120, -20),
                2 => new Vector2(-120, -20),
                _ => new Vector2(0, -120)
            };
        }

        /// <summary>
        /// 整理箱子中的钱币
        /// </summary>
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
                int platinumCoins = (int)(totalValue / 1000000);
                chest.AddItem(new Item(ItemID.PlatinumCoin, platinumCoins));
                totalValue %= 1000000;
            }
            if (totalValue >= 10000) {
                int goldCoins = (int)(totalValue / 10000);
                chest.AddItem(new Item(ItemID.GoldCoin, goldCoins));
                totalValue %= 10000;
            }
            if (totalValue >= 100) {
                int silverCoins = (int)(totalValue / 100);
                chest.AddItem(new Item(ItemID.SilverCoin, silverCoins));
                totalValue %= 100;
            }
            if (totalValue > 0) {
                chest.AddItem(new Item(ItemID.CopperCoin, (int)totalValue));
            }
        }

        public override void AI() {
            if (!spawn) {
                if (!VaultUtils.isClient) {
                    startPos = Projectile.Center;
                    velocity = Vector2.Zero;
                    Projectile.netUpdate = true;
                }
                spawn = true;
            }

            if (!TileProcessorLoader.AutoPositionGetTP(startPos.ToTileCoordinates16(), out collectorTP)) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            startPos = collectorTP.ArmPos;

            if (startPos.FindClosestPlayer(CollectorTP.killerArmDistance) == null) {
                collectorTP.dontSpawnArmTime = 60;
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

            //衰减抖动效果
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

            //精确计算曲线长度
            int sampleCount = 60;
            float curveLength = 0f;
            Vector2 prev = start;
            for (int i = 1; i <= sampleCount; i++) {
                float t = i / (float)sampleCount;
                Vector2 a = Vector2.Lerp(start, midControl, t);
                Vector2 b = Vector2.Lerp(midControl, end, t);
                Vector2 point = Vector2.Lerp(a, b, t);
                curveLength += Vector2.Distance(prev, point);
                prev = point;
            }

            float segmentLength = tex.Height / 2;
            int segmentCount = Math.Max(2, (int)(curveLength / segmentLength));
            Vector2[] points = new Vector2[segmentCount + 1];

            //构建点位
            for (int i = 0; i <= segmentCount; i++) {
                float t = i / (float)segmentCount;
                Vector2 pos = Vector2.Lerp(
                    Vector2.Lerp(start, midControl, t),
                    Vector2.Lerp(midControl, end, t),
                    t
                );
                points[i] = pos;
            }

            float clampRot = Projectile.rotation;

            //绘制机械臂体节
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

            //绘制夹子（根据clampOpenness调整帧）
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
