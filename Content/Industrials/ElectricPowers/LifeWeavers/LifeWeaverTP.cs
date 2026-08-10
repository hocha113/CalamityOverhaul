using CalamityOverhaul.Content.Industrials.ElectricPowers.TreeRegrowths;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.LifeWeavers
{
    internal class LifeWeaverTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<LifeWeaverTile>();
        public override int TargetItem => ModContent.ItemType<LifeWeaver>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 200;

        //搜索范围(像素)
        internal const int maxSearchDistance = 800;
        //抛射间隔
        private const int ShootInterval = 180;
        //一发都抛不出去时的重试间隔
        private const int RetryInterval = 60;
        //能耗
        internal int consumeUE = 5;

        //状态
        private int shootTimer;
        private int textIdleTime;
        internal bool BatteryPrompt;
        /// <summary>范围内确实无处可种，权威端判定后同步给客户端播提示</summary>
        internal bool NoTargetPrompt;

        //缓存的有效种植位置
        private List<PlantPosition> validPositions = new();
        private int positionSearchTimer;
        private const int PositionSearchInterval = 300;
        //缓存上限，超了只留离机器最近的一批
        private const int MaxCachedPositions = 60;
        //单次抛射最多试算几个落点，兜住"四面都被挡住"时的开销
        private const int MaxLaunchTries = 16;

        //抛物线求解区间(帧)，时间越长弧线越高
        private const float MinFlightTime = 36f;
        private const float MaxFlightTime = 168f;
        private const float FlightTimeStep = 12f;
        private const float MaxHorizontalSpeed = 12f;
        private const float MaxUpwardSpeed = 16f;
        private const float MaxDownwardSpeed = 3f;
        //轨迹采样步长(像素)
        private const float SampleStride = 12f;

        //已下种落点的冷却，覆盖飞行加生长演出的时长
        private const int TargetCooldown = 480;
        //树干要占5格宽走廊，这个横向间距内不重复下种
        private const int TargetSpacingX = 2;
        private const int TargetSpacingY = 20;
        private readonly List<PlantedTarget> recentTargets = new();

        private struct PlantPosition
        {
            public int TileX;
            public int TileY;
            public int TreeType;
            //到机器的距离平方，排序用
            public float DistanceSQ;
        }

        /// <summary>刚下过种的落点，冷却期内不再挑它和它旁边</summary>
        private struct PlantedTarget
        {
            public int TileX;
            public int TileY;
            public int Time;
        }

        public override void SetBattery() {
            DrawExtendMode = 1000;
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(BatteryPrompt);
            data.Write(NoTargetPrompt);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            BatteryPrompt = reader.ReadBoolean();
            NoTargetPrompt = reader.ReadBoolean();
        }

        private void SearchValidPositions() {
            validPositions.Clear();

            int searchTiles = maxSearchDistance / 16;
            int machineX = Position.X;
            int machineY = Position.Y;
            float maxDistSQ = maxSearchDistance * maxSearchDistance;

            for (int x = machineX - searchTiles; x <= machineX + searchTiles; x++) {
                for (int y = machineY - searchTiles; y <= machineY + searchTiles; y++) {
                    float distSQ = Vector2.DistanceSquared(CenterInWorld, new Vector2(x * 16 + 8, y * 16));
                    if (distSQ > maxDistSQ) {
                        continue;
                    }
                    if (IsCoolingDown(x, y)) {
                        continue;
                    }
                    //口径与真正长树的校验一致：草花藤蔓不算占位，雪地、高尔夫草、腐化丛林草等也认
                    if (!TreeBlueprint.CanPlantAt(x, y, out int treeType)) {
                        continue;
                    }
                    validPositions.Add(new PlantPosition {
                        TileX = x,
                        TileY = y,
                        TreeType = treeType,
                        DistanceSQ = distSQ
                    });
                }
            }

            //按距离截断，避免像原先那样凑满上限就停、结果只往一侧种
            if (validPositions.Count > MaxCachedPositions) {
                validPositions.Sort(static (a, b) => a.DistanceSQ.CompareTo(b.DistanceSQ));
                validPositions.RemoveRange(MaxCachedPositions, validPositions.Count - MaxCachedPositions);
            }
        }

        /// <summary>挑一个抛得到的落点，顺手剔掉缓存里已经失效的</summary>
        private PlantPosition? GetBestPlantPosition(out Vector2 launchVelocity, out float flightTime) {
            launchVelocity = Vector2.Zero;
            flightTime = 0f;

            if (validPositions.Count == 0) {
                return null;
            }

            //打乱顺序，别老盯着同一片地
            List<int> indices = new List<int>(validPositions.Count);
            for (int i = 0; i < validPositions.Count; i++) {
                indices.Add(i);
            }
            for (int i = indices.Count - 1; i > 0; i--) {
                int j = Rand.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            Vector2 startPos = GetLaunchPosition();
            List<int> staleIndices = new List<int>();
            PlantPosition? result = null;
            int tries = 0;

            foreach (int index in indices) {
                PlantPosition pos = validPositions[index];

                //缓存最长可能过期5秒，那边可能已经盖房或长树了，用前复检
                if (IsCoolingDown(pos.TileX, pos.TileY)
                    || !TreeBlueprint.CanPlantAt(pos.TileX, pos.TileY, out int treeType)) {
                    staleIndices.Add(index);
                    continue;
                }
                if (++tries > MaxLaunchTries) {
                    break;
                }

                Vector2 targetWorld = new Vector2(pos.TileX * 16 + 8, pos.TileY * 16);
                if (TrySolveLaunch(startPos, targetWorld, pos.TileX, pos.TileY, out launchVelocity, out flightTime)) {
                    pos.TreeType = treeType;
                    result = pos;
                    break;
                }
                //地能种但一条弧都递不过去，本轮别再算它了，等下次搜索重新收录
                staleIndices.Add(index);
            }

            RemoveAtSorted(staleIndices);
            return result;
        }

        private void RemoveAtSorted(List<int> indices) {
            if (indices.Count == 0) {
                return;
            }
            indices.Sort();
            for (int i = indices.Count - 1; i >= 0; i--) {
                validPositions.RemoveAt(indices[i]);
            }
        }

        private Vector2 GetLaunchPosition() {
            return CenterInWorld + new Vector2(0, -20);
        }

        /// <summary>解一条落到目标的抛物线：先试平弧，被地形挡住就一路往上抬</summary>
        private static bool TrySolveLaunch(Vector2 start, Vector2 target, int targetTileX, int targetTileY,
            out Vector2 velocity, out float flightTime) {
            velocity = Vector2.Zero;
            flightTime = 0f;

            Vector2 diff = target - start;
            float gravity = LifeWeaverAcorn.Gravity;

            for (float testTime = MinFlightTime; testTime <= MaxFlightTime; testTime += FlightTimeStep) {
                float vx = diff.X / testTime;
                if (Math.Abs(vx) > MaxHorizontalSpeed) {
                    continue;
                }

                //y = vy*t + 0.5*g*t^2 反解垂直初速
                float vy = (diff.Y - 0.5f * gravity * testTime * testTime) / testTime;
                if (vy > MaxDownwardSpeed || vy < -MaxUpwardSpeed) {
                    continue;
                }

                Vector2 testVelocity = new Vector2(vx, vy);
                if (!ValidateTrajectoryPath(start, testVelocity, testTime, targetTileX, targetTileY)) {
                    continue;
                }
                velocity = testVelocity;
                flightTime = testTime;
                return true;
            }

            return false;
        }

        /// <summary>沿抛物线采样查实心块；落点附近与世界上方留白不算阻挡</summary>
        private static bool ValidateTrajectoryPath(Vector2 start, Vector2 velocity, float totalTime, int targetTileX, int targetTileY) {
            float gravity = LifeWeaverAcorn.Gravity;
            float t = 0f;

            while (t < totalTime) {
                //按位移推进，高速段自动细分，别让采样点跨过一整块墙
                float speed = new Vector2(velocity.X, velocity.Y + gravity * t).Length();
                t += speed > SampleStride ? SampleStride / speed : 1f;
                if (t >= totalTime) {
                    break;
                }

                float x = start.X + velocity.X * t;
                float y = start.Y + velocity.Y * t + 0.5f * gravity * t * t;
                int tileX = (int)(x / 16f);
                int tileY = (int)(y / 16f);

                //飞出世界上边沿算空域
                if (tileY < 0) {
                    continue;
                }
                if (!WorldGen.InWorld(tileX, tileY, 5)) {
                    return false;
                }
                //贴近落点的最后一段允许接触地面
                if (Math.Abs(tileX - targetTileX) <= 1 && tileY >= targetTileY - 2) {
                    continue;
                }

                Tile tile = Main.tile[tileX, tileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>抛出一颗橡子，返回是否真的抛了出去</summary>
        private bool TryLaunchAcorn() {
            PlantPosition? pos = GetBestPlantPosition(out Vector2 launchVelocity, out float flightTime);
            if (pos == null) {
                return false;
            }

            PlantPosition plantPos = pos.Value;
            Vector2 startPos = GetLaunchPosition();

            //生成抛射橡子Actor(并行阶段延迟到主线程执行，串行阶段立即执行)
            //种子滚动涉及Main.rand，放入主线程闭包
            Defer(() => {
                if (!TreeBlueprint.TryRollSeed(plantPos.TileX, plantPos.TileY, plantPos.TreeType, out int seed)) {
                    return;
                }
                int actorIndex = ActorLoader.NewActor<LifeWeaverAcorn>(startPos, launchVelocity);
                if (actorIndex >= 0 && actorIndex < ActorLoader.MaxActorCount
                    && ActorLoader.Actors[actorIndex] is LifeWeaverAcorn acorn) {
                    acorn.Setup(plantPos.TileX, plantPos.TileY, plantPos.TreeType, seed, flightTime);
                }
            });

            //落点连同两侧进冷却：紧挨着下种必然互相挤掉走廊，白扔橡子
            recentTargets.Add(new PlantedTarget {
                TileX = plantPos.TileX,
                TileY = plantPos.TileY,
                Time = TargetCooldown
            });
            validPositions.RemoveAll(p => Math.Abs(p.TileX - plantPos.TileX) <= TargetSpacingX
                && Math.Abs(p.TileY - plantPos.TileY) <= TargetSpacingY);
            return true;
        }

        private bool IsCoolingDown(int tileX, int tileY) {
            foreach (PlantedTarget target in recentTargets) {
                if (Math.Abs(target.TileX - tileX) <= TargetSpacingX
                    && Math.Abs(target.TileY - tileY) <= TargetSpacingY) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>无处可种的状态变了才发包，客户端靠它播提示</summary>
        private void SetNoTargetPrompt(bool value) {
            if (value == NoTargetPrompt) {
                return;
            }
            NoTargetPrompt = value;
            SendData();
        }

        private void UpdateTargetCooldown() {
            for (int i = recentTargets.Count - 1; i >= 0; i--) {
                PlantedTarget target = recentTargets[i];
                target.Time--;
                if (target.Time <= 0) {
                    recentTargets.RemoveAt(i);
                    continue;
                }
                recentTargets[i] = target;
            }
        }

        public override void UpdateMachine() {
            consumeUE = 5;

            if (textIdleTime > 0) {
                textIdleTime--;
            }
            UpdateTargetCooldown();

            bool authority = !VaultUtils.isClient;

            //定期更新有效种植位置(落点缓存只在权威端)
            if (++positionSearchTimer >= PositionSearchInterval) {
                positionSearchTimer = 0;
                if (authority) {
                    SearchValidPositions();
                    SetNoTargetPrompt(validPositions.Count == 0);
                }
            }

            //检查能量状态
            BatteryPrompt = MachineData.UEvalue < consumeUE;
            if (BatteryPrompt) {
                if (textIdleTime <= 0) {
                    //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
                    Defer(() => CombatText.NewText(HitBox, Color.YellowGreen, LifeWeaver.NoEnergyText.Value));
                    textIdleTime = 300;
                }
                return;
            }

            //没有有效位置
            if (NoTargetPrompt) {
                if (textIdleTime <= 0) {
                    //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
                    Defer(() => CombatText.NewText(HitBox, Color.Orange, LifeWeaver.NoValidPositionText.Value));
                    textIdleTime = 300;
                }
                return;
            }

            //抛射计时
            if (++shootTimer < ShootInterval) {
                return;
            }
            //扣电与下种都由权威端定，客户端等同步
            if (!authority) {
                shootTimer = 0;
                return;
            }

            //抛得出去才扣电；打不到的落点已被剔除，压缩计时换下一批再试
            if (!TryLaunchAcorn()) {
                shootTimer = ShootInterval - RetryInterval;
                SetNoTargetPrompt(validPositions.Count == 0);
                return;
            }

            shootTimer = 0;
            MachineData.UEvalue -= consumeUE;
            SendData();
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
