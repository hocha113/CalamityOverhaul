using InnoVault.Actors;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Lumberjacks
{
    internal class LumberjackSaw : Actor
    {
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalArm")]
        private static Asset<Texture2D> arm = null;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/LumberjackSaw")]
        private static Asset<Texture2D> saw = null;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/LumberjackSawGlow")]
        private static Asset<Texture2D> sawGlow = null;

        //核心引用
        internal LumberjackTP lumberjackTP;

        //同步字段
        [SyncVar]
        public Vector2 startPos;
        [SyncVar]
        public Vector2 velocity;
        [SyncVar]
        public Vector2 targetPosition;
        [SyncVar]
        public byte stateValue;
        [SyncVar]
        public Point16 targetTreePos = Point16.NegativeOne;
        [SyncVar]
        public float rotation = 0f;

        //本地字段(不同步)
        private bool initialized;

        //存储目标坐标
        [SyncVar]
        private Point16 lumberjackPos = Point16.NegativeOne;

        //物理模拟参数
        private const float SpringStiffness = 0.12f;
        private const float Damping = 0.88f;
        private const float MaxSpeed = 14f;
        private const float ArrivalThreshold = 24f;

        //视觉效果参数(仅客户端)
        private float shakeIntensity = 0f;
        private int particleTimer = 0;
        private float rotationVelocity = 0f;
        private int sawFrame = 0;
        private int sawFrameCounter = 0;

        //状态机
        private LumberjackSawState currentState {
            get => (LumberjackSawState)stateValue;
            set => stateValue = (byte)value;
        }
        private int stateTimer = 0;

        //搜索冷却(避免频繁搜索)
        private int searchCooldown = 0;

        //砍伐计时器
        private int cuttingTimer = 0;
        private const int CuttingDuration = 60;

        public override void OnSpawn(params object[] args) {
            Width = 32;
            Height = 32;
            DrawExtendMode = 1200;
            DrawLayer = ActorDrawLayer.BeforeTiles;

            if (args is not null && args.Length >= 1) {
                lumberjackPos = (Point16)args[0];
            }

            if (!VaultUtils.isClient) {
                startPos = Position;
                velocity = Vector2.Zero;
                NetUpdate = true;
            }
            initialized = true;
        }

        /// <summary>
        /// 搜索最近的树木底部位置
        /// </summary>
        private Point16 FindNearestTree() {
            if (VaultUtils.isClient) return Point16.NegativeOne;

            float minDistSQ = LumberjackTP.maxSearchDistance * LumberjackTP.maxSearchDistance;
            Point16 bestTree = Point16.NegativeOne;
            Point16 machinePos = lumberjackTP.Position;

            //在搜索范围内查找树木
            int searchTiles = LumberjackTP.maxSearchDistance / 16;
            for (int x = machinePos.X - searchTiles; x <= machinePos.X + searchTiles; x++) {
                for (int y = machinePos.Y - searchTiles; y <= machinePos.Y + searchTiles; y++) {
                    if (!WorldGen.InWorld(x, y)) continue;

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile) continue;

                    //检查是否是树木类型
                    if (!IsTreeTile(tile.TileType)) continue;

                    //找到树木底部
                    Point16 treeBase = FindTreeBase(x, y, tile.TileType);
                    if (treeBase == Point16.NegativeOne) continue;

                    //检查是否被其他锯臂锁定
                    if (IsTreeTargeted(treeBase)) continue;

                    float distSQ = Vector2.DistanceSquared(
                        lumberjackTP.CenterInWorld,
                        treeBase.ToWorldCoordinates()
                    );

                    if (distSQ < minDistSQ) {
                        minDistSQ = distSQ;
                        bestTree = treeBase;
                    }
                }
            }

            return bestTree;
        }

        /// <summary>
        /// 检查是否是树木类型的物块
        /// </summary>
        private static bool IsTreeTile(int tileType) {
            return tileType == TileID.Trees ||
                   tileType == TileID.PalmTree ||
                   tileType == TileID.VanityTreeSakura ||
                   tileType == TileID.VanityTreeYellowWillow ||
                   tileType == TileID.TreeAsh;
        }

        /// <summary>
        /// 找到树木的底部位置
        /// </summary>
        private static Point16 FindTreeBase(int startX, int startY, int tileType) {
            int y = startY;
            //向下搜索直到找到树木底部
            while (y < Main.maxTilesY - 10) {
                Tile tile = Main.tile[startX, y];
                if (!tile.HasTile || tile.TileType != tileType) {
                    //找到了树木的底部,返回上一格位置
                    if (y > startY) {
                        return new Point16(startX, y - 1);
                    }
                    break;
                }
                y++;
            }
            return Point16.NegativeOne;
        }

        /// <summary>
        /// 检查树木是否已被其他锯臂锁定
        /// </summary>
        private bool IsTreeTargeted(Point16 treePos) {
            foreach (var actor in ActorLoader.GetActiveActors<LumberjackSaw>()) {
                if (actor != this && actor.targetTreePos == treePos) {
                    return true;
                }
            }
            return false;
        }

        private void SpringPhysicsMove(Vector2 target, float speedMultiplier = 1f) {
            Vector2 toTarget = target - Center;

            //弹簧力
            Vector2 springForce = toTarget * SpringStiffness * speedMultiplier;
            velocity += springForce;

            //阻尼
            velocity *= Damping;

            //限速
            if (velocity.LengthSquared() > MaxSpeed * MaxSpeed) {
                velocity = Vector2.Normalize(velocity) * MaxSpeed;
            }

            Position += velocity;

            //平滑旋转
            if (velocity.LengthSquared() > 0.1f) {
                float targetRotation = velocity.ToRotation();
                float rotationDiff = MathHelper.WrapAngle(targetRotation - rotation);
                rotationVelocity = MathHelper.Lerp(rotationVelocity, rotationDiff * 0.2f, 0.3f);
                rotation += rotationVelocity;
            }
        }

        private void SpawnMechanicalParticles(bool intensive = false) {
            if (Main.netMode == NetmodeID.Server) return;

            particleTimer++;
            int spawnRate = intensive ? 4 : 12;

            if (particleTimer % spawnRate == 0) {
                Vector2 particleVel = velocity * 0.2f + Main.rand.NextVector2Circular(2, 2);
                Dust dust = Dust.NewDustDirect(Center - Vector2.One * 8, 16, 16,
                    DustID.Electric, particleVel.X, particleVel.Y, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }

        private void SpawnWoodParticles() {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 3; i++) {
                Vector2 particleVel = Main.rand.NextVector2Circular(4, 4);
                Dust dust = Dust.NewDustDirect(Center - Vector2.One * 8, 16, 16,
                    DustID.WoodFurniture, particleVel.X, particleVel.Y, 100, default, Main.rand.NextFloat(1f, 1.5f));
                dust.noGravity = false;
            }
        }

        private void State_Idle() {
            stateTimer++;
            searchCooldown = Math.Max(0, searchCooldown - 1);

            shakeIntensity *= 0.9f;

            //每60帧且冷却结束后搜索
            if (stateTimer >= 60 && searchCooldown == 0 && lumberjackTP.MachineData.UEvalue >= lumberjackTP.consumeUE) {
                if (!VaultUtils.isClient) {
                    TransitionToState(LumberjackSawState.Searching);
                }
            }

            //待机漂浮
            Vector2 idleOffset = new Vector2(
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 30f,
                (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.5f) * 20f - 60f
            );
            SpringPhysicsMove(startPos + idleOffset, 0.6f);
        }

        private void State_Searching() {
            //只在服务器端搜索
            if (!VaultUtils.isClient) {
                Point16 foundTree = FindNearestTree();

                if (foundTree != Point16.NegativeOne) {
                    targetTreePos = foundTree;

                    //消耗能量
                    lumberjackTP.MachineData.UEvalue -= lumberjackTP.consumeUE;
                    lumberjackTP.SendData();

                    TransitionToState(LumberjackSawState.MovingToTree);
                }
                else {
                    searchCooldown = 120;
                    TransitionToState(LumberjackSawState.Idle);
                }
            }

            //播放音效(所有客户端)
            if (stateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.5f, Pitch = 0.3f }, Center);
            }
        }

        private void State_MovingToTree() {
            if (targetTreePos == Point16.NegativeOne) {
                TransitionToState(LumberjackSawState.Idle);
                return;
            }

            //检查树木是否还存在
            Tile tile = Main.tile[targetTreePos.X, targetTreePos.Y];
            if (!tile.HasTile || !IsTreeTile(tile.TileType)) {
                targetTreePos = Point16.NegativeOne;
                TransitionToState(LumberjackSawState.Idle);
                return;
            }

            targetPosition = targetTreePos.ToWorldCoordinates();
            float distanceToTarget = Vector2.Distance(Center, targetPosition);
            float speedMultiplier = MathHelper.Clamp(distanceToTarget / ArrivalThreshold, 0.3f, 1.2f);

            SpringPhysicsMove(targetPosition, speedMultiplier);
            SpawnMechanicalParticles();

            if (distanceToTarget < ArrivalThreshold) {
                TransitionToState(LumberjackSawState.Cutting);
            }
        }

        private void State_Cutting() {
            stateTimer++;
            cuttingTimer++;

            if (targetTreePos == Point16.NegativeOne) {
                TransitionToState(LumberjackSawState.Idle);
                return;
            }

            //保持在树木位置
            targetPosition = targetTreePos.ToWorldCoordinates();
            SpringPhysicsMove(targetPosition, 0.3f);

            shakeIntensity = 2f;
            SpawnMechanicalParticles(intensive: true);
            SpawnWoodParticles();

            //播放锯木音效
            if (stateTimer % 15 == 0) {
                SoundEngine.PlaySound(SoundID.Item22 with {
                    Volume = 0.6f,
                    Pitch = 0.1f
                }, Center);
            }

            //砍伐完成
            if (cuttingTimer >= CuttingDuration) {
                if (!VaultUtils.isClient) {
                    //砍伐树木
                    CutTree();
                }
                cuttingTimer = 0;
                TransitionToState(LumberjackSawState.Returning);
            }
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

            //记录树木类型用于重生
            int treeType = tile.TileType;

            //找到树木下方的地面位置
            int groundY = FindGroundBelowTree(x, y, treeType);

            //使用WorldGen砍伐树木
            WorldGen.KillTile(x, y, false, false, false);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, x, y);
            }

            //播放砍伐音效
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.8f,
                Pitch = -0.3f
            }, Center);

            //只有在循环模式下才触发树木重生系统
            if (lumberjackTP != null && lumberjackTP.CycleMode && groundY > 0) {
                SpawnTreeRegrowthAnimation(x, groundY, treeType);
            }

            targetTreePos = Point16.NegativeOne;
        }

        /// <summary>
        /// 找到树木下方的地面位置
        /// </summary>
        private static int FindGroundBelowTree(int x, int startY, int treeType) {
            int y = startY;
            //向下搜索直到找到非树木物块(即地面)
            while (y < Main.maxTilesY - 10) {
                Tile tile = Main.tile[x, y];
                if (!tile.HasTile || tile.TileType != treeType) {
                    //找到地面，返回地面的Y坐标
                    if (tile.HasTile && !Main.tileSolid[tile.TileType] == false) {
                        return y;
                    }
                    //如果下方是空的，继续向下找实心地面
                    while (y < Main.maxTilesY - 10) {
                        Tile groundTile = Main.tile[x, y];
                        if (groundTile.HasTile && Main.tileSolid[groundTile.TileType]) {
                            return y;
                        }
                        y++;
                    }
                    break;
                }
                y++;
            }
            return -1;
        }

        /// <summary>
        /// 生成树木重生动画(橡子下落)
        /// </summary>
        private static void SpawnTreeRegrowthAnimation(int tileX, int groundY, int treeType) {
            //生成橡子下落Actor
            Vector2 spawnPos = new Vector2(tileX * 16 + 8, (groundY - 20) * 16);
            int actorIndex = ActorLoader.NewActor<FallingAcorn>(spawnPos, Vector2.Zero);
            ActorLoader.Actors[actorIndex].OnSpawn(tileX, groundY, treeType);
        }

        private void State_Returning() {
            stateTimer++;
            shakeIntensity *= 0.9f;

            SpringPhysicsMove(startPos + new Vector2(0, -60f), 0.8f);
            SpawnMechanicalParticles();

            float distanceToStart = Vector2.Distance(Center, startPos + new Vector2(0, -60f));
            if (distanceToStart < ArrivalThreshold || stateTimer > 120) {
                TransitionToState(LumberjackSawState.Idle);
            }
        }

        private void TransitionToState(LumberjackSawState newState) {
            bool changed = currentState != newState;
            currentState = newState;
            stateTimer = 0;

            if (newState == LumberjackSawState.Idle) {
                targetTreePos = Point16.NegativeOne;
                cuttingTimer = 0;
            }

            if (changed && !VaultUtils.isClient) {
                NetUpdate = true;
            }
        }

        private void UpdateSawAnimation() {
            //锯片动画，共7帧
            bool isCutting = currentState == LumberjackSawState.Cutting;
            int frameSpeed = isCutting ? 2 : 6;

            sawFrameCounter++;
            if (sawFrameCounter >= frameSpeed) {
                sawFrameCounter = 0;
                sawFrame++;
                if (sawFrame >= 7) {
                    sawFrame = 0;
                }
            }
        }

        public override void AI() {
            if (!initialized) {
                if (!VaultUtils.isClient) {
                    startPos = Center;
                    velocity = Vector2.Zero;
                    NetUpdate = true;
                }
                initialized = true;
            }

            if (lumberjackPos == Point16.NegativeOne) {
                return;
            }

            if (!TileProcessorLoader.AutoPositionGetTP(lumberjackPos, out lumberjackTP)) {
                if (!VaultUtils.isClient) {
                    ActorLoader.KillActor(WhoAmI);
                }
                return;
            }

            startPos = lumberjackTP.ArmPos;

            //更新锯片动画
            UpdateSawAnimation();

            //状态机驱动
            switch (currentState) {
                case LumberjackSawState.Idle:
                    State_Idle();
                    break;
                case LumberjackSawState.Searching:
                    State_Searching();
                    break;
                case LumberjackSawState.MovingToTree:
                    State_MovingToTree();
                    break;
                case LumberjackSawState.Cutting:
                    State_Cutting();
                    break;
                case LumberjackSawState.Returning:
                    State_Returning();
                    break;
            }

            shakeIntensity *= 0.92f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (startPos == Vector2.Zero) {
                return false;
            }

            if (lumberjackTP?.BatteryPrompt == true) {
                drawColor = new Color(drawColor.R / 2, drawColor.G / 2, drawColor.B / 2, 255);
            }

            Texture2D tex = arm.Value;
            Vector2 start = startPos;
            Vector2 end = Center;

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

            float sawRot = rotation;

            //绘制机械臂
            for (int i = 0; i < segmentCount; i++) {
                Vector2 pos = points[i];
                Vector2 next = points[i + 1];
                Vector2 direction = next - pos;
                Color color = Lighting.GetColor((pos / 16).ToPoint());
                float rot = direction.ToRotation() + MathHelper.PiOver2;

                if (i == segmentCount - 1) {
                    sawRot = direction.ToRotation();
                }

                //添加轻微的缩放动画
                float scale = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + i * 0.5f) * 0.02f;

                Main.spriteBatch.Draw(tex, pos - Main.screenPosition, null, color, rot
                    , new Vector2(tex.Width / 2f, tex.Height), scale, SpriteEffects.None, 0f);
            }

            //绘制锯片
            Texture2D sawTex = saw.Value;
            Texture2D sawGlowTex = sawGlow.Value;
            int frameHeight = sawTex.Height / 7;
            Rectangle sawRect = new Rectangle(0, sawFrame * frameHeight, sawTex.Width, frameHeight);
            Vector2 sawOrigin = new Vector2(sawTex.Width / 2f, frameHeight / 2f);

            //根据方向决定是否翻转，锯片正面朝右
            SpriteEffects sawEffect = sawRot > MathHelper.PiOver2 || sawRot < -MathHelper.PiOver2
                ? SpriteEffects.FlipVertically
                : SpriteEffects.None;

            Main.spriteBatch.Draw(sawTex, Center - Main.screenPosition
                , sawRect
                , drawColor, sawRot
                , sawOrigin, 1f, sawEffect, 0f);

            Main.spriteBatch.Draw(sawGlowTex, Center - Main.screenPosition
                , sawRect
                , Color.White * (0.7f + shakeIntensity * 0.3f), sawRot
                , sawOrigin, 1f, sawEffect, 0f);

            return false;
        }
    }
}
