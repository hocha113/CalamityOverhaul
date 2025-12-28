using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
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
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Lumberjacks
{
    internal class WGGLumberjack : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/WGGLumberjack";
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
            Item.value = Item.buyPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<WGGLumberjackTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 600;
        }
    }

    internal class WGGLumberjackTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/WGGLumberjackTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(120, 80, 60), VaultUtils.GetLocalizedItemName<WGGLumberjack>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(0, 1);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
            //敌对建筑设置
            HitSound = SoundID.Item14;
            MineResist = 3f;
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.WoodFurniture);
            return false;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) {
            num = fail ? 1 : 3;
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out WGGLumberjackTP lumberjack)) {
                return;
            }
            lumberjack.byHitSyncopeTime = 60;
            lumberjack.SendData();
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out WGGLumberjackTP lumberjack)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class WGGLumberjackTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<WGGLumberjackTile>();
        public override int TargetItem => ModContent.ItemType<WGGLumberjack>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 600;
        public Vector2 ArmPos => CenterInWorld + new Vector2(0, -8);
        internal const int killerArmDistance = 2200;
        internal const int maxSearchDistance = 1000;
        internal int dontSpawnArmTime;
        internal int byHitSyncopeTime;
        internal int ArmIndex = -1;

        public override void SetBattery() {
            Efficiency = 0;//敌对建筑不具备电力传输能力
            IdleDistance = 1800;
            PlaceNet = true;
            DrawExtendMode = 1400;
        }

        public override void Initialize() {
            MachineData.UEvalue = MaxUEValue;
        }

        public override void SendData(ModPacket data) {
            data.Write(byHitSyncopeTime);
            data.Write(ArmIndex);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            byHitSyncopeTime = reader.ReadInt32();
            ArmIndex = reader.ReadInt32();
        }

        public override void UpdateMachine() {
            if (byHitSyncopeTime > 0) {
                byHitSyncopeTime--;
            }

            if (dontSpawnArmTime > 0) {
                dontSpawnArmTime--;
            }

            //玩家接近时生成锯臂
            if (ArmPos.FindClosestPlayer(killerArmDistance) != null && dontSpawnArmTime <= 0 && !VaultUtils.isClient) {
                CheckArm(ref ArmIndex, ModContent.ProjectileType<WGGLumberjackSaw>(), ArmPos);
                if (ArmIndex == -1) {
                    int dmg = 15;
                    if (Main.masterMode || Main.expertMode) {
                        dmg = 12;
                    }
                    ArmIndex = Projectile.NewProjectileDirect(this.FromObjectGetParent()
                    , ArmPos, Vector2.Zero, ModContent.ProjectileType<WGGLumberjackSaw>(), dmg, 3, -1).identity;
                    SendData();
                }
            }
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

        public override void MachineKill() {
            if (!VaultUtils.isClient) {
                DropItem(ModContent.ItemType<Lumberjack>());
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }

    /// <summary>
    /// 荒野伐木者的锯臂，敌对弹幕
    /// </summary>
    internal class WGGLumberjackSaw : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/WGGMechanicalArmAlt")]
        private static Asset<Texture2D> arm = null;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/WGGLumberjackSaw")]
        private static Asset<Texture2D> saw = null;
        private Player player;
        internal WGGLumberjackTP lumberjackTP;
        private int attackTimer;
        private int idleTimer;
        internal bool BatteryPrompt;
        internal Vector2 startPos;
        private SawState currentState = SawState.Idle;

        //目标树木
        private Point16 targetTreePos = Point16.NegativeOne;

        //攻击相关
        private Vector2 strikeTargetPos;//下砸目标位置
        private Vector2 hoverTargetPos;//悬停目标位置
        private int warningTimer;//警告闪烁计时
        private bool isCharging;//是否正在蓄力

        private enum SawState
        {
            Idle,           //待机
            Searching,      //搜索
            MovingToTree,   //移动到树木
            Cutting,        //砍伐
            Tracking,       //追踪玩家(移动到头顶)
            Hovering,       //悬停蓄力(给玩家反应时间)
            Diving,         //下砸攻击
            Recovery,       //攻击后恢复
            Returning       //返回
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 120;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.WriteVector2(startPos);
            writer.Write(attackTimer);
            writer.Write(idleTimer);
            writer.Write((byte)currentState);
            writer.Write(targetTreePos.X);
            writer.Write(targetTreePos.Y);
            writer.WriteVector2(strikeTargetPos);
            writer.WriteVector2(hoverTargetPos);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            startPos = reader.ReadVector2();
            attackTimer = reader.ReadInt32();
            idleTimer = reader.ReadInt32();
            currentState = (SawState)reader.ReadByte();
            targetTreePos = new Point16(reader.ReadInt16(), reader.ReadInt16());
            strikeTargetPos = reader.ReadVector2();
            hoverTargetPos = reader.ReadVector2();
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                startPos = Projectile.Center;
                Projectile.localAI[0] = 1f;
                currentState = SawState.Searching;
                Projectile.netUpdate = true;
            }

            //检查基座是否存在
            if (TileProcessorLoader.AutoPositionGetTP(startPos.ToTileCoordinates16(), out lumberjackTP)) {
                Projectile.timeLeft = 2;
                startPos = lumberjackTP.ArmPos;
                if (Projectile.identity != lumberjackTP.ArmIndex) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                Projectile.Kill();
                return;
            }

            //检查玩家距离
            if (startPos.FindClosestPlayer(WGGLumberjackTP.killerArmDistance) == null) {
                lumberjackTP.dontSpawnArmTime = 60;
                Projectile.Kill();
                return;
            }

            ExecuteBehavior();
            UpdateRotation();

            //警告计时器更新
            if (isCharging) {
                warningTimer++;
            }
            else {
                warningTimer = 0;
            }
        }

        private void ExecuteBehavior() {
            //被攻击时的硬直
            if (lumberjackTP.byHitSyncopeTime > 0) {
                isCharging = false;
                DoSyncopeMotion();
                return;
            }

            //能量检查
            if (lumberjackTP.MachineData.UEvalue < 10) {
                Projectile.damage = 0;
                isCharging = false;
                if (!BatteryPrompt) {
                    CombatText.NewText(lumberjackTP.HitBox, new Color(111, 247, 200), CWRLocText.Instance.Turret_Text1.Value, false);
                    BatteryPrompt = true;
                }
                DoIdleMotion();
                return;
            }
            else {
                BatteryPrompt = false;
            }

            //更新玩家目标
            if (player?.Alives() != true || player.To(startPos).Length() > 800) {
                player = startPos.FindClosestPlayer();
            }

            switch (currentState) {
                case SawState.Idle:
                    BehaviorIdle();
                    break;
                case SawState.Searching:
                    BehaviorSearch();
                    break;
                case SawState.MovingToTree:
                    BehaviorMoveToTree();
                    break;
                case SawState.Cutting:
                    BehaviorCutting();
                    break;
                case SawState.Tracking:
                    BehaviorTracking();
                    break;
                case SawState.Hovering:
                    BehaviorHovering();
                    break;
                case SawState.Diving:
                    BehaviorDiving();
                    break;
                case SawState.Recovery:
                    BehaviorRecovery();
                    break;
                case SawState.Returning:
                    BehaviorReturn();
                    break;
                default:
                    DoIdleMotion();
                    break;
            }
        }

        private void BehaviorIdle() {
            Projectile.damage = 0;
            isCharging = false;
            idleTimer++;

            if (idleTimer > 50) {
                currentState = SawState.Searching;
                idleTimer = 0;
            }

            DoIdleMotion();
        }

        private void BehaviorSearch() {
            Projectile.damage = 0;
            isCharging = false;
            attackTimer++;

            //优先检测玩家(进入追踪模式)
            if (player.Alives() && player.Distance(startPos) < 450) {
                currentState = SawState.Tracking;
                attackTimer = 0;
                SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.5f }, Projectile.Center);
                return;
            }

            //搜索树木，玩家在附近时才会试图寻找树木，这样避免玩家不在的时候把树木砍光
            if (attackTimer % 60 == 1 && player.Alives() && player.Distance(startPos) < 1550) {
                targetTreePos = FindNearestTree();
                if (targetTreePos != Point16.NegativeOne) {
                    currentState = SawState.MovingToTree;
                    attackTimer = 0;
                    return;
                }
            }

            //缓慢扫描运动
            float angle = Main.GlobalTimeWrappedHourly * 1.5f;
            Vector2 scanPos = startPos + new Vector2((float)Math.Cos(angle) * 120f, -70f + (float)Math.Sin(angle * 0.6f) * 25f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, scanPos, 0.04f);
            Projectile.velocity *= 0.9f;
        }

        private void BehaviorMoveToTree() {
            isCharging = false;

            if (targetTreePos == Point16.NegativeOne) {
                currentState = SawState.Searching;
                return;
            }

            //途中发现玩家靠近则转为追踪
            if (player != null && player.Distance(Projectile.Center) < 250) {
                currentState = SawState.Tracking;
                targetTreePos = Point16.NegativeOne;
                attackTimer = 0;
                return;
            }

            //检查树木是否还存在
            Tile tile = Main.tile[targetTreePos.X, targetTreePos.Y];
            if (!tile.HasTile || !IsTreeTile(tile.TileType)) {
                targetTreePos = Point16.NegativeOne;
                currentState = SawState.Searching;
                return;
            }

            Vector2 targetPos = targetTreePos.ToWorldCoordinates();
            float dist = Vector2.Distance(Projectile.Center, targetPos);

            Vector2 toTarget = targetPos - Projectile.Center;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 0.06f, 0.12f);

            SpawnMechanicalParticles();

            if (dist < 30f) {
                currentState = SawState.Cutting;
                attackTimer = 0;
            }
        }

        private void BehaviorCutting() {
            attackTimer++;
            isCharging = false;

            if (targetTreePos == Point16.NegativeOne) {
                currentState = SawState.Searching;
                return;
            }

            //砍伐时如果玩家靠近则中断
            if (player != null && player.Distance(Projectile.Center) < 180) {
                currentState = SawState.Tracking;
                targetTreePos = Point16.NegativeOne;
                attackTimer = 0;
                return;
            }

            Vector2 targetPos = targetTreePos.ToWorldCoordinates();
            Vector2 offset = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 1f));
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos + offset, 0.15f);
            Projectile.velocity *= 0.5f;

            SpawnMechanicalParticles(true);
            SpawnWoodParticles();

            if (attackTimer % 15 == 0) {
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
            }

            if (attackTimer >= 55) {
                CutTree();
                if (lumberjackTP.MachineData.UEvalue > 6) {
                    lumberjackTP.MachineData.UEvalue -= 6;
                    lumberjackTP.SendData();
                }
                currentState = SawState.Returning;
                attackTimer = 0;
            }
        }

        /// <summary>
        /// 追踪玩家，移动到玩家头顶上方
        /// </summary>
        private void BehaviorTracking() {
            Projectile.damage = 0;
            isCharging = false;

            if (player == null || !player.Alives()) {
                currentState = SawState.Searching;
                return;
            }

            attackTimer++;

            //目标位置：玩家头顶上方150像素
            hoverTargetPos = player.Center + new Vector2(0, -150f);

            //限制不要超出机械臂的范围
            float maxArmLength = 600f;
            if (Vector2.Distance(hoverTargetPos, startPos) > maxArmLength) {
                Vector2 dir = (hoverTargetPos - startPos).SafeNormalize(Vector2.Zero);
                hoverTargetPos = startPos + dir * maxArmLength;
            }

            //平滑移动到玩家头顶
            Vector2 toTarget = hoverTargetPos - Projectile.Center;
            float speed = Math.Min(toTarget.Length() * 0.08f, 10f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget.SafeNormalize(Vector2.Zero) * speed, 0.15f);

            SpawnMechanicalParticles();

            //到达玩家头顶后进入悬停蓄力阶段
            float distToHover = Vector2.Distance(Projectile.Center, hoverTargetPos);
            if (distToHover < 40f || attackTimer > 80) {
                currentState = SawState.Hovering;
                attackTimer = 0;
                //记录下砸目标位置(玩家当前位置)
                strikeTargetPos = player.Center;
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = 0.5f }, Projectile.Center);
            }
        }

        /// <summary>
        /// 悬停蓄力阶段，给玩家反应时间
        /// </summary>
        private void BehaviorHovering() {
            Projectile.damage = 0;
            isCharging = true;
            attackTimer++;

            //悬停时轻微抖动，表示蓄力
            float shakeIntensity = attackTimer / 60f * 4f;
            Vector2 shake = Main.rand.NextVector2Circular(shakeIntensity, shakeIntensity * 0.5f);

            //保持在玩家头顶，但不完全跟踪(给躲避空间)
            if (player != null && player.Alives()) {
                //只缓慢跟踪，让玩家有机会跑开
                Vector2 slowTrack = player.Center + new Vector2(0, -140f);
                hoverTargetPos = Vector2.Lerp(hoverTargetPos, slowTrack, 0.02f);
                strikeTargetPos = Vector2.Lerp(strikeTargetPos, player.Center, 0.1f);
            }

            Projectile.Center = Vector2.Lerp(Projectile.Center, hoverTargetPos + shake, 0.2f);
            Projectile.velocity *= 0.5f;

            //蓄力粒子效果
            if (attackTimer % 4 == 0 && Main.netMode != NetmodeID.Server) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(20f, 10f);
                Dust dust = Dust.NewDustDirect(dustPos, 4, 4, DustID.Torch, 0, 2f, 100, new Color(255, 100, 50), 1.2f);
                dust.noGravity = true;
                dust.velocity = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero) * 2f;
            }

            //播放蓄力音效
            if (attackTimer == 30) {
                SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.7f, Pitch = 0.8f }, Projectile.Center);
            }



            //蓄力完成，开始下砸
            if (attackTimer >= 50) {
                currentState = SawState.Diving;
                attackTimer = 0;

                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);

                //消耗能量
                if (lumberjackTP.MachineData.UEvalue > 10) {
                    lumberjackTP.MachineData.UEvalue -= 10;
                    lumberjackTP.SendData();
                }
            }
        }

        /// <summary>
        /// 下砸攻击阶段
        /// </summary>
        private void BehaviorDiving() {
            Projectile.damage = Projectile.originalDamage;
            isCharging = false;
            attackTimer++;

            //快速向下砸
            Vector2 diveDir = (strikeTargetPos - Projectile.Center).SafeNormalize(new Vector2(0, 1));

            if (attackTimer == 1) {
                Projectile.velocity = diveDir * 18f;
            }

            //下砸过程中产生火花
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dustVel = -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(2f, 2f);
                    Dust dust = Dust.NewDustDirect(Projectile.Center, 8, 8, DustID.Torch, dustVel.X, dustVel.Y, 100, new Color(255, 150, 50), 1.1f);
                    dust.noGravity = true;
                }
            }

            //检测是否到达目标或碰到地面
            bool reachedTarget = Projectile.Center.Y >= strikeTargetPos.Y;
            bool hitGround = CheckTileCollision();

            if (reachedTarget || hitGround || attackTimer > 40) {
                currentState = SawState.Recovery;
                attackTimer = 0;
                Projectile.velocity *= 0.1f;

                //砸地效果
                SpawnImpactEffect();
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
            }
        }

        /// <summary>
        /// 攻击后恢复阶段
        /// </summary>
        private void BehaviorRecovery() {
            attackTimer++;
            isCharging = false;

            //短暂硬直，给玩家反击/逃跑机会
            Projectile.velocity *= 0.85f;
            Projectile.damage = 0;

            //轻微抖动表示恢复中
            if (attackTimer < 20) {
                Vector2 shake = Main.rand.NextVector2Circular(2f, 2f);
                Projectile.Center += shake;
            }

            if (attackTimer >= 35) {
                //检查是否继续攻击玩家
                if (player != null && player.Alives() && player.Distance(startPos) < 500) {
                    currentState = SawState.Tracking;
                }
                else {
                    currentState = SawState.Returning;
                }
                attackTimer = 0;
            }
        }

        private void BehaviorReturn() {
            attackTimer++;
            Projectile.damage = 0;
            isCharging = false;

            Vector2 returnPos = startPos + new Vector2(0, -70);
            Vector2 toReturn = returnPos - Projectile.Center;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toReturn * 0.06f, 0.1f);

            if (Projectile.Distance(returnPos) < 25 || attackTimer > 100) {
                currentState = SawState.Idle;
                attackTimer = 0;
            }
        }

        /// <summary>
        /// 检测物块碰撞
        /// </summary>
        private bool CheckTileCollision() {
            int tileX = (int)(Projectile.Center.X / 16);
            int tileY = (int)((Projectile.Center.Y + 16) / 16);

            if (WorldGen.InWorld(tileX, tileY)) {
                Tile tile = Main.tile[tileX, tileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 生成砸地冲击效果
        /// </summary>
        private void SpawnImpactEffect() {
            if (Main.netMode == NetmodeID.Server) return;

            //火花爆发
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 dustVel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle) * 0.5f - 0.5f) * Main.rand.NextFloat(3f, 6f);
                Dust dust = Dust.NewDustDirect(Projectile.Center, 8, 8, DustID.Torch, dustVel.X, dustVel.Y, 100, new Color(255, 180, 80), 1.3f);
                dust.noGravity = false;
            }

            //烟尘
            for (int i = 0; i < 5; i++) {
                Vector2 dustVel = Main.rand.NextVector2Circular(4f, 2f) + new Vector2(0, -1f);
                Dust dust = Dust.NewDustDirect(Projectile.Center + new Vector2(0, 10), 16, 8, DustID.Smoke, dustVel.X, dustVel.Y, 150, default, 1.5f);
                dust.noGravity = true;
            }
        }

        private Point16 FindNearestTree() {
            float minDistSQ = WGGLumberjackTP.maxSearchDistance * WGGLumberjackTP.maxSearchDistance;
            Point16 bestTree = Point16.NegativeOne;
            Point16 machinePos = lumberjackTP.Position;

            int searchTiles = WGGLumberjackTP.maxSearchDistance / 16;
            for (int x = machinePos.X - searchTiles; x <= machinePos.X + searchTiles; x++) {
                for (int y = machinePos.Y - searchTiles; y <= machinePos.Y + searchTiles; y++) {
                    if (!WorldGen.InWorld(x, y)) continue;

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || !IsTreeTile(tile.TileType)) continue;

                    Point16 treeBase = FindTreeBase(x, y, tile.TileType);
                    if (treeBase == Point16.NegativeOne) continue;

                    float distSQ = Vector2.DistanceSquared(lumberjackTP.CenterInWorld, treeBase.ToWorldCoordinates());
                    if (distSQ < minDistSQ) {
                        minDistSQ = distSQ;
                        bestTree = treeBase;
                    }
                }
            }

            return bestTree;
        }

        private static bool IsTreeTile(int tileType) {
            return tileType == TileID.Trees ||
                   tileType == TileID.PalmTree ||
                   tileType == TileID.VanityTreeSakura ||
                   tileType == TileID.VanityTreeYellowWillow ||
                   tileType == TileID.TreeAsh;
        }

        private static Point16 FindTreeBase(int startX, int startY, int tileType) {
            int y = startY;
            while (y < Main.maxTilesY - 10) {
                Tile tile = Main.tile[startX, y];
                if (!tile.HasTile || tile.TileType != tileType) {
                    if (y > startY) {
                        return new Point16(startX, y - 1);
                    }
                    break;
                }
                y++;
            }
            return Point16.NegativeOne;
        }

        private void CutTree() {
            if (targetTreePos == Point16.NegativeOne) return;

            int x = targetTreePos.X;
            int y = targetTreePos.Y;

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || !IsTreeTile(tile.TileType)) {
                targetTreePos = Point16.NegativeOne;
                return;
            }

            WorldGen.KillTile(x, y, false, false, false);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, x, y);
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
            targetTreePos = Point16.NegativeOne;
        }

        private void UpdateRotation() {
            if (currentState == SawState.Diving) {
                //下砸时朝向移动方向
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
            else if (currentState == SawState.Hovering) {
                //悬停时朝下
                Projectile.rotation = Projectile.rotation.AngleLerp(MathHelper.PiOver2, 0.15f);
            }
            else if (player != null && (currentState == SawState.Tracking)) {
                //追踪时看向玩家
                float targetRot = (player.Center - Projectile.Center).ToRotation();
                Projectile.rotation = Projectile.rotation.AngleLerp(targetRot, 0.1f);
            }
            else {
                //其他状态缓慢恢复水平
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.08f);
            }
        }

        private void DoSyncopeMotion() {
            idleTimer++;
            float shakeRadius = MathHelper.Lerp(18f, 4f, 1f - lumberjackTP.byHitSyncopeTime / 60f);
            Vector2 offset = Main.rand.NextVector2Circular(shakeRadius, shakeRadius);
            Projectile.Center = Vector2.Lerp(Projectile.Center, startPos + new Vector2(0, -70) + offset, 0.08f);
            Projectile.rotation += 0.12f;
        }

        private void DoIdleMotion() {
            idleTimer++;
            Vector2 idlePos = startPos + new Vector2(0, -70 + (float)Math.Sin(idleTimer * 0.03f) * 12f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, idlePos, 0.035f);
        }

        private void SpawnMechanicalParticles(bool intensive = false) {
            if (Main.netMode == NetmodeID.Server) return;

            int spawnRate = intensive ? 3 : 12;
            if (Main.rand.NextBool(spawnRate)) {
                Vector2 dustVel = Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1.2f, 1.2f);
                Dust dust = Dust.NewDustDirect(Projectile.Center - Vector2.One * 8, 16, 16,
                    DustID.Torch, dustVel.X, dustVel.Y, 100, new Color(255, 180, 100), 0.7f);
                dust.noGravity = true;
            }
        }

        private void SpawnWoodParticles() {
            if (Main.netMode == NetmodeID.Server) return;

            if (Main.rand.NextBool(2)) {
                Vector2 dustVel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                Dust.NewDustDirect(Projectile.Center - Vector2.One * 6, 12, 12,
                    DustID.WoodFurniture, dustVel.X, dustVel.Y, 100, default, 1.1f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (startPos == Vector2.Zero) return false;

            if (BatteryPrompt) {
                lightColor = Color.Red;
            }

            Texture2D armTex = arm.Value;
            Texture2D sawTex = saw.Value;

            Vector2 start = startPos;
            Vector2 end = Projectile.Center;

            //震动效果
            if (isCharging) {
                float shakeIntensity = warningTimer / 60f * 2f;
                end += Main.rand.NextVector2Circular(shakeIntensity, shakeIntensity);
            }

            //贝塞尔曲线：计算弯曲高度
            float dist = Vector2.Distance(start, end);
            float bendHeight = MathHelper.Clamp(dist * 0.35f, 30f, 150f);

            //根据运动状态调整弯曲
            if (Projectile.velocity.Length() > 5f) {
                bendHeight += Projectile.velocity.Length() * 1.5f;
            }

            Vector2 midControl = (start + end) / 2 + new Vector2(0, -bendHeight);

            //计算曲线长度用于确定段数
            int sampleCount = 50;
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

            float segmentLength = armTex.Height / 2;
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

            //绘制机械臂
            for (int i = 0; i < segmentCount; i++) {
                Vector2 pos = points[i];
                Vector2 next = points[i + 1];
                Vector2 direction = next - pos;
                Color color = Lighting.GetColor((pos / 16).ToPoint());
                float rot = direction.ToRotation() + MathHelper.PiOver2;

                Main.EntitySpriteDraw(armTex, pos - Main.screenPosition, null, color, rot
                    , new Vector2(armTex.Width / 2f, armTex.Height), 1f, SpriteEffects.None, 0);
            }

            //锯片颜色(蓄力时闪烁警告)
            Color sawColor = lightColor;
            if (isCharging) {
                float flash = (float)Math.Sin(warningTimer * 0.4f) * 0.5f + 0.5f;
                sawColor = Color.Lerp(lightColor, new Color(255, 100, 50), flash * 0.6f);
            }

            SpriteEffects sawEffect = Projectile.rotation > MathHelper.PiOver2 || Projectile.rotation < -MathHelper.PiOver2
                ? SpriteEffects.FlipVertically
                : SpriteEffects.None;

            Main.EntitySpriteDraw(sawTex, Projectile.Center - Main.screenPosition, null, sawColor, Projectile.rotation,
                sawTex.Size() / 2, 1f, sawEffect, 0);

            //蓄力时绘制警告指示线
            if (isCharging && player != null) {
                DrawWarningLine();
            }

            return false;
        }

        /// <summary>
        /// 绘制下砸警告线
        /// </summary>
        private void DrawWarningLine() {
            if (Main.netMode == NetmodeID.Server) return;

            float alpha = (float)Math.Sin(warningTimer * 0.3f) * 0.3f + 0.4f;
            Color lineColor = new Color(255, 50, 50) * alpha;

            Vector2 start = Projectile.Center;
            Vector2 end = strikeTargetPos;

            //使用虚线绘制
            float dist = Vector2.Distance(start, end);
            Vector2 dir = (end - start).SafeNormalize(Vector2.Zero);
            int dashCount = (int)(dist / 12f);

            for (int i = 0; i < dashCount; i++) {
                if (i % 2 == 0) {
                    Vector2 dashStart = start + dir * (i * 12f);
                    Vector2 dashEnd = start + dir * ((i + 1) * 12f);

                    //简单的线段绘制
                    Main.EntitySpriteDraw(VaultAsset.placeholder2.Value,
                        dashStart - Main.screenPosition,
                        new Rectangle(0, 0, 1, 1),
                        lineColor,
                        dir.ToRotation(),
                        Vector2.Zero,
                        new Vector2(10f, 2f),
                        SpriteEffects.None, 0);
                }
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }
    }
}
