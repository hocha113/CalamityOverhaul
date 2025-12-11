using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class WGGCollector : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/WGGCollector";
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
            Item.rare = ItemRarityID.Green;
            Item.createTile = ModContent.TileType<WGGCollectorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }
    }

    internal class WGGCollectorTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/WGGCollectorTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<WGGCollector>());

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
            //设置声音和挖掘强度，毕竟属于敌对建筑
            HitSound = SoundID.Item14;
            MineResist = 4f;
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) {
            num = fail ? 1 : 3;
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out WGGCollectorTP collector)) {
                return;
            }
            collector.byHitSyncopeTime = 60;
            collector.SendData();
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out WGGCollectorTP collector)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += (collector.altState ? 0 : 1) * 18 * 5;
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

    internal class WGGCollectorTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<WGGCollectorTile>();
        public override int TargetItem => ModContent.ItemType<WGGCollector>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 800;
        public Vector2 ArmPos => CenterInWorld + new Vector2(0, 14);
        internal const int killerArmDistance = 1400;
        internal int dontSpawnArmTime;
        internal int byHitSyncopeTime;
        internal int ArmIndex = -1;
        internal bool altState;
        public override void SetBattery() {
            Efficiency = 0;//敌对建筑不具备电力传输能力
            IdleDistance = 2000;
            PlaceNet = true;
        }
        public override void Initialize() {
            altState = Main.rand.NextBool();
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

            if (ArmPos.FindClosestPlayer(killerArmDistance) != null && dontSpawnArmTime <= 0 && !VaultUtils.isClient) {
                CheckArm(ref ArmIndex, ModContent.ProjectileType<WGGCollectorArm>(), ArmPos);
                if (ArmIndex == -1) {
                    ArmIndex = Projectile.NewProjectileDirect(this.FromObjectGetParent()
                    , ArmPos, Vector2.Zero, ModContent.ProjectileType<WGGCollectorArm>(), 10, 2, -1).identity;
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
                DropItem(ModContent.ItemType<Collector>());
            }
        }
        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }

    internal class WGGCollectorArm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/WGGMechanicalArm")]
        private static Asset<Texture2D> arm = null;//手臂的体节纹理
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/WGGMechanicalClamp")]
        private static Asset<Texture2D> clamp = null;//手臂的夹子纹理
        private Player player;
        internal WGGCollectorTP collectorTP;
        private int attackTimer;
        private int idleWiggleTime;
        internal bool BatteryPrompt;
        internal Vector2 startPos;//记录这个弹幕的起点位置
        private ArmState currentState = ArmState.Idle;
        
        //物理模拟相关
        private Vector2[] segments;
        private const int SegmentCount = 60;
        private const float SegmentLength = 16f;

        private enum ArmState
        {
            Idle,
            Searching,
            LockOn, //锁定目标
            Strike, //突袭
            CoolDown, //冷却/回收
            Retreating
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
            writer.Write(idleWiggleTime);
            writer.Write((byte)currentState);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            startPos = reader.ReadVector2();
            attackTimer = reader.ReadInt32();
            idleWiggleTime = reader.ReadInt32();
            currentState = (ArmState)reader.ReadByte();
        }

        private void InitializeSegments() {
            segments = new Vector2[SegmentCount];
            for (int i = 0; i < SegmentCount; i++) {
                segments[i] = Projectile.Center;
            }
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                startPos = Projectile.Center;
                Projectile.localAI[0] = 1f;
                currentState = ArmState.Searching;
                InitializeSegments();
                Projectile.netUpdate = true;
            }

            if (segments == null) {
                InitializeSegments();
            }

            if (TileProcessorLoader.AutoPositionGetTP(startPos.ToTileCoordinates16(), out collectorTP)) {
                Projectile.timeLeft = 2;
                startPos = collectorTP.ArmPos;
                if (Projectile.identity != collectorTP.ArmIndex) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                Projectile.Kill();
                return;
            }

            if (startPos.FindClosestPlayer(WGGCollectorTP.killerArmDistance) == null) {
                collectorTP.dontSpawnArmTime = 60;
                Projectile.Kill();
                return;
            }

            //更新物理模拟
            UpdateVerletPhysics();

            Projectile.damage = Projectile.originalDamage;
            
            //状态机逻辑
            ExecuteBehavior();

            //头部旋转逻辑
            UpdateRotation();
        }

        private void UpdateVerletPhysics() {
            //简单的Verlet积分模拟绳索/机械臂
            //头部跟随弹幕中心
            segments[SegmentCount - 1] = Projectile.Center;
            //尾部固定在基座
            segments[0] = startPos;

            //迭代约束
            for (int k = 0; k < 10; k++) {
                for (int i = 0; i < SegmentCount - 1; i++) {
                    Vector2 segmentStart = segments[i];
                    Vector2 segmentEnd = segments[i + 1];
                    Vector2 vector = segmentEnd - segmentStart;
                    float dist = vector.Length();
                    if (dist > 0) {
                        float error = dist - SegmentLength;
                        Vector2 correction = vector.SafeNormalize(Vector2.Zero) * error * 0.5f;
                        
                        if (i > 0) segments[i] += correction; //基座不动
                        if (i + 1 < SegmentCount - 1) segments[i + 1] -= correction; //头部由AI控制位置，不完全受物理约束
                    }
                }
                //再次强制约束头部和尾部
                segments[0] = startPos;
                segments[SegmentCount - 1] = Projectile.Center;
            }
        }

        private void ExecuteBehavior() {
            if (collectorTP.byHitSyncopeTime > 0) {
                DoSyncopeMotion();
                return;
            }

            if (collectorTP.MachineData.UEvalue < 10) {
                Projectile.damage = 0;
                if (!BatteryPrompt) {
                    CombatText.NewText(collectorTP.HitBox, new Color(111, 247, 200), CWRLocText.Instance.Turret_Text1.Value, false);
                    BatteryPrompt = true;
                }
                DoIdleMotion();
                return;
            }
            else {
                BatteryPrompt = false;
            }

            //索敌逻辑
            if (player?.Alives() != true || Projectile.Center.Distance(startPos) > 1400) {
                player = startPos.FindClosestPlayer(800);
                if (player == null) {
                    currentState = ArmState.Searching;
                }
                else if (currentState == ArmState.Searching) {
                    currentState = ArmState.LockOn;
                    attackTimer = 0;
                }
            }

            switch (currentState) {
                case ArmState.Searching:
                    BehaviorSearch();
                    break;
                case ArmState.LockOn:
                    BehaviorLockOn();
                    break;
                case ArmState.Strike:
                    BehaviorStrike();
                    break;
                case ArmState.CoolDown:
                    BehaviorCoolDown();
                    break;
                case ArmState.Retreating:
                    BehaviorRetreat();
                    break;
                default:
                    DoIdleMotion();
                    break;
            }
        }

        private void BehaviorSearch() {
            Projectile.damage = 0;
            //机械式扫描：移动到一个点，停顿，再移动
            idleWiggleTime++;
            if (idleWiggleTime % 120 == 0) {
                //随机选择一个新的扫描点
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(100, 300);
                Projectile.ai[1] = angle; //存储目标角度
                Projectile.ai[2] = dist;  //存储目标距离
            }

            Vector2 target = startPos + new Vector2((float)Math.Cos(Projectile.ai[1]), (float)Math.Sin(Projectile.ai[1])) * Projectile.ai[2];
            //使用平滑阻尼移动，模拟伺服电机
            Projectile.Center = Vector2.Lerp(Projectile.Center, target, 0.05f);
            Projectile.velocity *= 0.9f;
        }

        private void BehaviorLockOn() {
            if (player == null) return;
            
            //悬停在玩家附近，准备攻击
            Vector2 hoverTarget = player.Center + (startPos - player.Center).SafeNormalize(Vector2.Zero) * 200f;
            
            //快速逼近
            Vector2 toTarget = hoverTarget - Projectile.Center;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 0.1f, 0.2f);
            
            attackTimer++;
            //锁定时间结束，发起攻击
            if (attackTimer > 40) {
                currentState = ArmState.Strike;
                attackTimer = 0;
                //播放锁定音效
                SoundEngine.PlaySound(SoundID.Item23, Projectile.Center);
            }
        }

        private void BehaviorStrike() {
            attackTimer++;
            
            if (attackTimer == 1) {
                //计算突袭向量
                Vector2 strikeDir = (player.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = strikeDir * 25f; //高速突袭
                
                //消耗能量
                if (collectorTP.MachineData.UEvalue > 10) {
                    collectorTP.MachineData.UEvalue -= 10;
                    collectorTP.SendData();
                }
            }
            
            //突袭过程中产生粒子
            if (attackTimer < 15) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Electric);
            }

            //减速并进入冷却
            if (attackTimer > 20) {
                Projectile.velocity *= 0.8f;
            }
            
            if (attackTimer > 40) {
                currentState = ArmState.CoolDown;
                attackTimer = 0;
            }
        }

        private void BehaviorCoolDown() {
            attackTimer++;
            Projectile.velocity *= 0.9f;
            
            //短暂硬直后重新寻找目标
            if (attackTimer > 30) {
                currentState = ArmState.LockOn;
                attackTimer = 0;
            }
        }

        private void BehaviorRetreat() {
            Vector2 retreatPos = startPos + new Vector2(0, -100);
            Projectile.velocity = (retreatPos - Projectile.Center) * 0.1f;
            if (Projectile.Distance(retreatPos) < 20) {
                currentState = ArmState.Searching;
            }
        }

        private void UpdateRotation() {
            if (currentState == ArmState.Strike) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            } else if (player != null) {
                //始终注视玩家
                float targetRot = (player.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                Projectile.rotation = Projectile.rotation.AngleLerp(targetRot, 0.2f);
            } else {
                Projectile.rotation = Projectile.rotation.AngleLerp(Projectile.velocity.ToRotation() + MathHelper.PiOver2, 0.1f);
            }
        }

        private void DoSyncopeMotion() {
            idleWiggleTime++;
            float shakeRadius = MathHelper.Lerp(24f, 6f, 1f - collectorTP.byHitSyncopeTime / 60f);
            Vector2 offset = Main.rand.NextVector2Circular(shakeRadius, shakeRadius);
            Projectile.Center = Vector2.Lerp(Projectile.Center, startPos + new Vector2(0, -100) + offset, 0.1f);
            Projectile.rotation += 0.1f;
        }

        private void DoIdleMotion() {
            //简单的上下浮动
            idleWiggleTime++;
            Vector2 idlePos = startPos + new Vector2(0, -100 + (float)Math.Sin(idleWiggleTime * 0.05f) * 20f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, idlePos, 0.05f);
            Projectile.rotation = 0;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (segments == null || segments.Length == 0) return false;

            if (BatteryPrompt) {
                lightColor = Color.Red; //能量不足显示红色警告
            }

            Texture2D armTex = arm.Value;
            Texture2D clampTex = clamp.Value;
            float step = armTex.Height * 0.6f; //步长设置为纹理高度的0.6倍以保证重叠

            //绘制机械臂体节
            for (int i = 0; i < SegmentCount - 1; i++) {
                Vector2 start = segments[i];
                Vector2 end = segments[i + 1];
                Vector2 vector = end - start;
                float dist = vector.Length();
                
                //计算需要绘制的数量，确保填满间隙
                int numDraws = (int)Math.Ceiling(dist / step);
                if (numDraws <= 0) numDraws = 1;

                for (int j = 0; j < numDraws; j++) {
                    float t = j / (float)numDraws;
                    Vector2 drawPos = Vector2.Lerp(start, end, t);
                    Color color = Lighting.GetColor(drawPos.ToTileCoordinates());
                    float rotation = vector.ToRotation() + MathHelper.PiOver2;
                    
                    Main.EntitySpriteDraw(armTex, drawPos - Main.screenPosition, null, color, rotation, 
                        new Vector2(armTex.Width / 2, armTex.Height / 2), 1f, SpriteEffects.None, 0);
                }
            }

            //绘制头部夹子
            Main.EntitySpriteDraw(clampTex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, 
                clampTex.Size() / 2, 1f, SpriteEffects.None, 0);

            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }
    }
}
