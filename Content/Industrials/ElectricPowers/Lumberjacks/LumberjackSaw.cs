using CalamityOverhaul.Content.Industrials.ElectricPowers.TreeRegrowths;
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

        //机械运动参数
        private const float MechanicalMaxSpeed = 16f;
        private const float MechanicalAcceleration = 0.8f;
        private const float MechanicalDeceleration = 0.92f;
        private const float ArrivalThreshold = 20f;
        private const float RotationSnapThreshold = 0.05f;

        //液压延迟系统
        private Vector2 hydraulicTargetPos;
        private float hydraulicDelay = 0f;
        private int hydraulicCycleTimer = 0;

        //机械抖动参数
        private float mechanicalJitter = 0f;
        private int jitterTimer = 0;

        //视觉效果参数(仅客户端)
        private float shakeIntensity = 0f;
        private int particleTimer = 0;
        private float targetRotation = 0f;
        private int sawFrame = 0;
        private int sawFrameCounter = 0;

        //伺服电机音效计时
        private int servoSoundTimer = 0;

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

        /// <summary>
        /// 机械式运动，模拟液压臂的硬朗感
        /// </summary>
        private void MechanicalMove(Vector2 target, float speedFactor = 1f) {
            hydraulicCycleTimer++;

            //液压延迟：目标位置变化时有短暂响应延迟
            if (Vector2.Distance(hydraulicTargetPos, target) > 5f) {
                hydraulicDelay = 8f;
                hydraulicTargetPos = target;
            }

            if (hydraulicDelay > 0) {
                hydraulicDelay--;
                //延迟期间产生轻微的准备抖动
                if (Main.netMode != NetmodeID.Server) {
                    mechanicalJitter = 0.5f;
                }
                return;
            }

            Vector2 toTarget = target - Center;
            float distance = toTarget.Length();

            if (distance < 1f) {
                velocity *= 0.5f;
                return;
            }

            Vector2 direction = toTarget / distance;

            //机械式加速：快速达到目标速度
            float targetSpeed = Math.Min(distance * 0.15f, MechanicalMaxSpeed * speedFactor);

            //根据距离调整速度曲线，近距离时快速减速
            if (distance < 60f) {
                targetSpeed *= distance / 60f;
                targetSpeed = Math.Max(targetSpeed, 2f);
            }

            //线性加速到目标速度
            float currentSpeed = velocity.Length();
            if (currentSpeed < targetSpeed) {
                currentSpeed = Math.Min(currentSpeed + MechanicalAcceleration * speedFactor, targetSpeed);
            }
            else {
                currentSpeed *= MechanicalDeceleration;
            }

            velocity = direction * currentSpeed;
            Position += velocity;

            //机械式旋转：步进式而非平滑
            UpdateMechanicalRotation(direction);

            //运动时产生机械抖动
            if (currentSpeed > 2f) {
                mechanicalJitter = Math.Min(mechanicalJitter + 0.1f, 1.5f);

                //伺服电机音效
                if (Main.netMode != NetmodeID.Server && ++servoSoundTimer % 20 == 0) {
                    SoundEngine.PlaySound(SoundID.Item22 with {
                        Volume = 0.15f,
                        Pitch = 0.5f + currentSpeed * 0.02f
                    }, Center);
                }
            }
            else {
                mechanicalJitter *= 0.8f;
            }
        }

        /// <summary>
        /// 机械式旋转，模拟步进电机的顿挫感
        /// </summary>
        private void UpdateMechanicalRotation(Vector2 direction) {
            if (direction.LengthSquared() < 0.01f) return;

            float newTargetRotation = direction.ToRotation();
            float rotationDiff = MathHelper.WrapAngle(newTargetRotation - rotation);

            //步进式旋转：每次只转动固定角度
            float stepAngle = MathHelper.ToRadians(8f);

            if (Math.Abs(rotationDiff) > RotationSnapThreshold) {
                //分步旋转
                if (Math.Abs(rotationDiff) > stepAngle) {
                    rotation += Math.Sign(rotationDiff) * stepAngle;
                }
                else {
                    //小角度直接对齐
                    rotation = newTargetRotation;
                }

                //旋转时产生机械音
                if (Main.netMode != NetmodeID.Server && jitterTimer++ % 8 == 0) {
                    mechanicalJitter += 0.3f;
                }
            }

            targetRotation = newTargetRotation;
        }

        /// <summary>
        /// 待机时的机械式微动，模拟液压系统的维持抖动
        /// </summary>
        private void MechanicalIdleMove(Vector2 basePos) {
            hydraulicCycleTimer++;

            //机械式周期运动：分段式而非正弦波
            int cyclePhase = (hydraulicCycleTimer / 30) % 4;
            Vector2 idleOffset = cyclePhase switch {
                0 => new Vector2(0, -50f),
                1 => new Vector2(15f, -55f),
                2 => new Vector2(0, -60f),
                3 => new Vector2(-15f, -55f),
                _ => new Vector2(0, -55f)
            };

            //添加微小的液压维持抖动
            if (hydraulicCycleTimer % 45 == 0 && Main.netMode != NetmodeID.Server) {
                mechanicalJitter = 0.8f;
                SoundEngine.PlaySound(SoundID.Item22 with {
                    Volume = 0.08f,
                    Pitch = 0.3f
                }, Center);
            }

            MechanicalMove(basePos + idleOffset, 0.5f);
        }

        private void SpawnMechanicalParticles(bool intensive = false) {
            if (Main.netMode == NetmodeID.Server) return;

            particleTimer++;
            int spawnRate = intensive ? 3 : 8;

            if (particleTimer % spawnRate == 0) {
                //金属火花粒子
                Vector2 particleVel = velocity * 0.3f + Main.rand.NextVector2Circular(1.5f, 1.5f);
                Dust dust = Dust.NewDustDirect(Center - Vector2.One * 8, 16, 16,
                    DustID.Torch, particleVel.X, particleVel.Y, 100, new Color(255, 200, 100), Main.rand.NextFloat(0.6f, 1f));
                dust.noGravity = true;
                dust.fadeIn = 0.8f;

                //偶尔产生油烟粒子
                if (Main.rand.NextBool(3)) {
                    Dust smoke = Dust.NewDustDirect(Center - Vector2.One * 6, 12, 12,
                        DustID.Smoke, 0, -0.5f, 150, default, 0.6f);
                    smoke.noGravity = true;
                }
            }

            //高速运动时产生更多火花
            if (velocity.Length() > 8f && particleTimer % 2 == 0) {
                Vector2 sparkVel = -velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f);
                sparkVel += Main.rand.NextVector2Circular(1f, 1f);
                Dust spark = Dust.NewDustDirect(Center, 4, 4, DustID.Torch, sparkVel.X, sparkVel.Y, 0, new Color(255, 220, 150), 0.8f);
                spark.noGravity = true;
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

            shakeIntensity *= 0.85f;
            mechanicalJitter *= 0.9f;

            //每60帧且冷却结束后搜索
            if (stateTimer >= 60 && searchCooldown == 0 && lumberjackTP.MachineData.UEvalue >= lumberjackTP.consumeUE) {
                if (!VaultUtils.isClient) {
                    TransitionToState(LumberjackSawState.Searching);
                }
            }

            //机械式待机运动
            MechanicalIdleMove(startPos);
        }

        private void State_Searching() {
            stateTimer++;

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

            //搜索时的机械扫描动作
            if (stateTimer == 1) {
                //播放启动音效
                SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.6f, Pitch = 0.1f }, Center);
                mechanicalJitter = 2f;
            }

            //保持在待机位置附近，轻微抖动表示扫描
            MechanicalMove(startPos + new Vector2(0, -55f), 0.3f);
        }

        private void State_MovingToTree() {
            stateTimer++;

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

            //根据距离调整速度
            float speedFactor = distanceToTarget > 200f ? 1.2f : 1f;
            MechanicalMove(targetPosition, speedFactor);
            SpawnMechanicalParticles();

            //接近目标时播放减速音效
            if (distanceToTarget < 80f && stateTimer % 10 == 0 && Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item22 with {
                    Volume = 0.2f,
                    Pitch = -0.2f
                }, Center);
            }

            if (distanceToTarget < ArrivalThreshold) {
                //到达时产生机械制动效果
                mechanicalJitter = 2f;
                velocity *= 0.3f;
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

            //保持在树木位置，使用较弱的力度
            targetPosition = targetTreePos.ToWorldCoordinates();

            //锯木时的机械振动：快速小幅度抖动
            Vector2 cuttingOffset = Vector2.Zero;
            if (stateTimer % 2 == 0) {
                cuttingOffset = new Vector2(
                    Main.rand.NextFloat(-2f, 2f),
                    Main.rand.NextFloat(-1f, 1f)
                );
            }

            MechanicalMove(targetPosition + cuttingOffset, 0.4f);

            //持续的机械振动
            shakeIntensity = 2.5f;
            mechanicalJitter = 1.5f;
            SpawnMechanicalParticles(intensive: true);
            SpawnWoodParticles();

            //播放锯木音效，更有节奏感
            if (stateTimer % 12 == 0) {
                SoundEngine.PlaySound(SoundID.Item22 with {
                    Volume = 0.7f,
                    Pitch = -0.1f + Main.rand.NextFloat(-0.05f, 0.05f)
                }, Center);
            }

            //砍伐完成
            if (cuttingTimer >= CuttingDuration) {
                if (!VaultUtils.isClient) {
                    CutTree();
                }
                cuttingTimer = 0;
                //砍伐完成后的机械回弹
                velocity = new Vector2(0, -3f);
                mechanicalJitter = 3f;
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
            shakeIntensity *= 0.85f;
            mechanicalJitter *= 0.9f;

            //返回时播放液压收缩音效
            if (stateTimer == 1 && Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item23 with {
                    Volume = 0.4f,
                    Pitch = -0.2f
                }, Center);
            }

            Vector2 returnTarget = startPos + new Vector2(0, -55f);
            MechanicalMove(returnTarget, 0.9f);
            SpawnMechanicalParticles();

            float distanceToStart = Vector2.Distance(Center, returnTarget);
            if (distanceToStart < ArrivalThreshold || stateTimer > 150) {
                //到达后机械制动
                velocity *= 0.2f;
                mechanicalJitter = 1.5f;
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
                hydraulicTargetPos = Center;
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

            //机械抖动衰减
            shakeIntensity *= 0.88f;
            mechanicalJitter *= 0.92f;
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

            //机械抖动效果：更生硬的方向性抖动
            if (mechanicalJitter > 0.1f) {
                float jitterAngle = (jitterTimer * 0.5f) % MathHelper.TwoPi;
                Vector2 jitterOffset = new Vector2(
                    (float)Math.Cos(jitterAngle * 3f) * mechanicalJitter,
                    (float)Math.Sin(jitterAngle * 2f) * mechanicalJitter * 0.5f
                );
                end += jitterOffset;
            }

            //震动效果
            if (shakeIntensity > 0.1f) {
                end += new Vector2(
                    Main.rand.NextFloat(-1f, 1f) * shakeIntensity,
                    Main.rand.NextFloat(-1f, 1f) * shakeIntensity
                );
            }

            //机械臂曲线：更硬朗的折线感
            float dist = Vector2.Distance(start, end);
            float bendHeight = MathHelper.Clamp(dist * 0.35f, 30f, 150f);

            //根据运动状态调整弯曲
            if (velocity.Length() > 5f) {
                bendHeight += velocity.Length() * 1.5f;
            }

            Vector2 midControl = (start + end) / 2 + new Vector2(0, -bendHeight);

            //计算曲线长度
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

                //机械臂段无动态缩放，保持刚性
                Main.spriteBatch.Draw(tex, pos - Main.screenPosition, null, color, rot
                    , new Vector2(tex.Width / 2f, tex.Height), 1f, SpriteEffects.None, 0f);
            }

            //绘制锯片
            Texture2D sawTex = saw.Value;
            Texture2D sawGlowTex = sawGlow.Value;
            int frameHeight = sawTex.Height / 7;
            Rectangle sawRect = new Rectangle(0, sawFrame * frameHeight, sawTex.Width, frameHeight);
            Vector2 sawOrigin = new Vector2(sawTex.Width / 2f, frameHeight / 2f);

            //根据方向决定是否翻转
            SpriteEffects sawEffect = sawRot > MathHelper.PiOver2 || sawRot < -MathHelper.PiOver2
                ? SpriteEffects.FlipVertically
                : SpriteEffects.None;

            //锯片位置添加机械抖动
            Vector2 sawDrawPos = Center;
            if (mechanicalJitter > 0.5f) {
                sawDrawPos += new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f) * mechanicalJitter,
                    Main.rand.NextFloat(-0.5f, 0.5f) * mechanicalJitter
                );
            }

            Main.spriteBatch.Draw(sawTex, sawDrawPos - Main.screenPosition
                , sawRect
                , drawColor, sawRot
                , sawOrigin, 1f, sawEffect, 0f);

            //发光效果强度随工作状态变化
            float glowIntensity = 0.6f + shakeIntensity * 0.2f + mechanicalJitter * 0.1f;
            Main.spriteBatch.Draw(sawGlowTex, sawDrawPos - Main.screenPosition
                , sawRect
                , Color.White * glowIntensity, sawRot
                , sawOrigin, 1f, sawEffect, 0f);

            return false;
        }
    }
}
