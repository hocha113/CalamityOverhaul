using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
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
        //能耗
        internal int consumeUE = 5;

        //状态
        private int shootTimer;
        private int textIdleTime;
        internal bool BatteryPrompt;

        //缓存的有效种植位置
        private List<PlantPosition> validPositions = new();
        private int positionSearchTimer;
        private const int PositionSearchInterval = 300;

        private struct PlantPosition
        {
            public int TileX;
            public int TileY;
            public int GroundType;
        }

        public override void SetBattery() {
            DrawExtendMode = 1000;
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(BatteryPrompt);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            BatteryPrompt = reader.ReadBoolean();
        }

        /// <summary>
        /// 搜索有效的种植位置
        /// </summary>
        private void SearchValidPositions() {
            validPositions.Clear();

            int searchTiles = maxSearchDistance / 16;
            int machineX = Position.X;
            int machineY = Position.Y;

            //在范围内搜索可种植位置
            for (int x = machineX - searchTiles; x <= machineX + searchTiles; x++) {
                for (int y = machineY - searchTiles; y <= machineY + searchTiles; y++) {
                    if (!WorldGen.InWorld(x, y, 10)) continue;

                    //检查是否是有效的种植地面
                    if (!IsValidPlantGround(x, y, out int groundType)) continue;

                    //检查上方是否有足够空间
                    if (!HasEnoughSpace(x, y)) continue;

                    //检查是否已有树木
                    if (HasTreeAbove(x, y)) continue;

                    //检查距离(使用抛物线可达范围)
                    float dist = Vector2.Distance(CenterInWorld, new Vector2(x * 16, y * 16));
                    if (dist > maxSearchDistance) continue;

                    validPositions.Add(new PlantPosition {
                        TileX = x,
                        TileY = y,
                        GroundType = groundType
                    });

                    //限制最大数量
                    if (validPositions.Count >= 50) return;
                }
            }
        }

        /// <summary>
        /// 检查是否是有效的种植地面
        /// </summary>
        private static bool IsValidPlantGround(int x, int y, out int groundType) {
            groundType = -1;
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile) return false;

            groundType = tile.TileType;

            return groundType == TileID.Grass ||
                   groundType == TileID.CorruptGrass ||
                   groundType == TileID.CrimsonGrass ||
                   groundType == TileID.HallowedGrass ||
                   groundType == TileID.JungleGrass ||
                   groundType == TileID.MushroomGrass ||
                   groundType == TileID.Sand ||
                   groundType == TileID.Crimsand ||
                   groundType == TileID.Ebonsand ||
                   groundType == TileID.Pearlsand ||
                   groundType == TileID.Ash;
        }

        /// <summary>
        /// 检查上方是否有足够空间
        /// </summary>
        private static bool HasEnoughSpace(int x, int y) {
            //至少需要12格空间
            for (int checkY = y - 1; checkY >= y - 12; checkY--) {
                if (!WorldGen.InWorld(x, checkY)) return false;
                Tile tile = Main.tile[x, checkY];
                if (tile.HasTile) {
                    return false;
                }
            }

            //左右也需要空间
            for (int checkX = x - 2; checkX <= x + 2; checkX++) {
                Tile tile = Main.tile[checkX, y - 1];
                if (tile.HasTile) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 检查上方是否已有树木
        /// </summary>
        private static bool HasTreeAbove(int x, int y) {
            for (int checkY = y - 1; checkY >= y - 5; checkY--) {
                if (!WorldGen.InWorld(x, checkY)) continue;
                Tile tile = Main.tile[x, checkY];
                if (tile.HasTile && IsTreeTile(tile.TileType)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查是否是树木类型
        /// </summary>
        private static bool IsTreeTile(int tileType) {
            return tileType == TileID.Trees ||
                   tileType == TileID.PalmTree ||
                   tileType == TileID.VanityTreeSakura ||
                   tileType == TileID.VanityTreeYellowWillow ||
                   tileType == TileID.TreeAsh;
        }

        /// <summary>
        /// 获取最佳种植位置(验证抛物线可达)
        /// </summary>
        private PlantPosition? GetBestPlantPosition(out Vector2 launchVelocity, out float flightTime) {
            launchVelocity = Vector2.Zero;
            flightTime = 0f;

            if (validPositions.Count == 0) return null;

            //打乱顺序后依次检查
            List<int> indices = new List<int>();
            for (int i = 0; i < validPositions.Count; i++) indices.Add(i);

            //随机打乱
            for (int i = indices.Count - 1; i > 0; i--) {
                int j = Main.rand.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            Vector2 startPos = GetLaunchPosition();

            foreach (int index in indices) {
                PlantPosition pos = validPositions[index];
                Vector2 targetWorld = new Vector2(pos.TileX * 16 + 8, pos.TileY * 16);

                //尝试计算抛物线轨迹
                if (TryCalculateTrajectory(startPos, targetWorld, out Vector2 velocity, out float time)) {
                    //验证轨迹路径上没有障碍物
                    if (ValidateTrajectoryPath(startPos, velocity, time)) {
                        launchVelocity = velocity;
                        flightTime = time;
                        return pos;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取发射位置(机器上方)
        /// </summary>
        private Vector2 GetLaunchPosition() {
            return CenterInWorld + new Vector2(0, -20);
        }

        /// <summary>
        /// 尝试计算抛物线轨迹
        /// </summary>
        private static bool TryCalculateTrajectory(Vector2 start, Vector2 target, out Vector2 velocity, out float time) {
            velocity = Vector2.Zero;
            time = 0f;

            Vector2 diff = target - start;
            float gravity = LifeWeaverAcorn.Gravity;

            //根据水平距离估算飞行时间
            float horizontalDist = Math.Abs(diff.X);
            float verticalDist = diff.Y;

            //目标速度范围(不能太快也不能太慢)
            float minSpeed = 4f;
            float maxSpeed = 12f;

            //尝试不同的飞行时间找到合适的轨迹
            for (float testTime = 40f; testTime <= 180f; testTime += 10f) {
                float vx = diff.X / testTime;

                //检查水平速度是否在合理范围
                if (Math.Abs(vx) < minSpeed * 0.3f || Math.Abs(vx) > maxSpeed) continue;

                //根据抛物线公式计算垂直初速度: y = vy*t + 0.5*g*t^2
                //vy = (y - 0.5*g*t^2) / t
                float vy = (verticalDist - 0.5f * gravity * testTime * testTime) / testTime;

                //检查初始垂直速度(应该是向上的，即负值，但不能太大)
                if (vy > 2f) continue; //不能向下发射太快
                if (vy < -15f) continue; //不能向上发射太快

                velocity = new Vector2(vx, vy);
                time = testTime;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 验证抛物线路径上没有障碍物
        /// </summary>
        private static bool ValidateTrajectoryPath(Vector2 start, Vector2 velocity, float totalTime) {
            float gravity = LifeWeaverAcorn.Gravity;
            int checkCount = (int)(totalTime / 5f);
            checkCount = Math.Max(checkCount, 10);

            Vector2 prevPos = start;

            for (int i = 1; i <= checkCount; i++) {
                float t = totalTime * i / checkCount;

                //计算该时刻的位置: pos = start + v*t + 0.5*g*t^2
                float x = start.X + velocity.X * t;
                float y = start.Y + velocity.Y * t + 0.5f * gravity * t * t;
                Vector2 pos = new Vector2(x, y);

                //检查这个位置是否有障碍物
                int tileX = (int)(pos.X / 16);
                int tileY = (int)(pos.Y / 16);

                if (!WorldGen.InWorld(tileX, tileY, 5)) return false;

                Tile tile = Main.tile[tileX, tileY];

                //只在最后几个检查点允许碰到地面
                bool isNearEnd = i > checkCount - 2;
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !isNearEnd) {
                    return false;
                }

                prevPos = pos;
            }

            return true;
        }

        /// <summary>
        /// 发射橡子
        /// </summary>
        private void LaunchAcorn() {
            if (VaultUtils.isClient) return;

            //获取有效位置并计算轨迹
            PlantPosition? pos = GetBestPlantPosition(out Vector2 launchVelocity, out float flightTime);
            if (pos == null) return;

            PlantPosition plantPos = pos.Value;
            Vector2 startPos = GetLaunchPosition();

            //生成抛射橡子Actor
            int actorIndex = ActorLoader.NewActor<LifeWeaverAcorn>(startPos, launchVelocity);
            if (actorIndex >= 0 && actorIndex < ActorLoader.MaxActorCount) {
                //传入目标位置、地面类型和预计飞行时间
                ActorLoader.Actors[actorIndex].OnSpawn(plantPos.TileX, plantPos.TileY, plantPos.GroundType, flightTime);
            }

            //从列表中移除已选中的位置
            validPositions.Remove(plantPos);
        }

        public override void UpdateMachine() {
            consumeUE = 5;

            if (textIdleTime > 0) {
                textIdleTime--;
            }

            //定期更新有效种植位置
            if (++positionSearchTimer >= PositionSearchInterval) {
                positionSearchTimer = 0;
                if (!VaultUtils.isClient) {
                    SearchValidPositions();
                }
            }

            //检查能量状态
            BatteryPrompt = MachineData.UEvalue < consumeUE;
            if (BatteryPrompt) {
                if (textIdleTime <= 0) {
                    CombatText.NewText(HitBox, Color.YellowGreen, LifeWeaver.NoEnergyText.Value);
                    textIdleTime = 300;
                }
                return;
            }

            //没有有效位置
            if (validPositions.Count == 0) {
                if (textIdleTime <= 0 && positionSearchTimer == 0) {
                    CombatText.NewText(HitBox, Color.Orange, LifeWeaver.NoValidPositionText.Value);
                    textIdleTime = 300;
                }
                return;
            }

            //抛射计时
            if (++shootTimer >= ShootInterval) {
                shootTimer = 0;

                //消耗能量
                MachineData.UEvalue -= consumeUE;

                //发射橡子
                LaunchAcorn();

                SendData();
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
