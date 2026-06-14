using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors.States
{
    /// <summary>
    /// 飞行状态：阿波利娅飞行越过障碍物或深坑，使用单帧Jump纹理。 起飞前扫描前方障碍的高度和宽度，动态规划三阶段弧线（爬升→巡航→下降）， 一次飞行连续越过多个障碍，避免反复起降。 接近目标时自动减速并提前下降，不会飞过头。 下降时若脚下仍是深坑则保持滑翔直到找到可着陆地面
    /// </summary>
    internal class ApolliaFlyingState : IApolliaState
    {
        //── 飞行参数 ──
        private const float FlySpeed = 3.8f;
        private const float LiftSpeed = -4f;
        private const float CruiseVerticalDamp = 0.15f;
        private const float DescentGravity = 0.22f;
        private const float MaxFallSpeed = 8f;
        private const float MaxFlyHeight = 320f;
        private const int MaxFlyDuration = 300;
        private const int MinAirTime = 20;
        private const float ArrivalSlowdownDist = 100f;
        private const float ClearancePadding = 48f;
        private const float MinClearance = 80f;

        //── 状态字段 ──
        private readonly Vector2 moveTarget;
        private float startY;
        private int timer;
        private int flyDir;
        private float requiredHeight;
        private float obstacleEndX;

        /// <summary>

        /// 着陆后的状态工厂。若为 null，则使用默认逻辑（Follow→Walking，Hold→Idle）

        /// </summary>
        private readonly Func<ApolliaActor, IApolliaState> onLand;

        private enum Phase { Ascend, Cruise, Descend, Landing }
        private Phase phase;
        private float landingGroundY;

        /// <param name="target">飞行要趋近的世界坐标目标点</param>
        /// <param name="onLand">着陆后要切换的状态工厂；传 null 时使用默认行为</param>
        public ApolliaFlyingState(Vector2 target, Func<ApolliaActor, IApolliaState> onLand = null) {
            moveTarget = target;
            this.onLand = onLand;
        }

        public void Enter(ApolliaActor actor) {
            timer = 0;
            startY = actor.Position.Y;
            phase = Phase.Ascend;

            actor.UseJumpTexture = true;
            actor.OnGround = false;
            actor.JetTrailActive = true;
            actor.Velocity = new Vector2(0, LiftSpeed);

            //起飞时锁定水平方向，避免穿越目标时来回抖动
            flyDir = Math.Sign(moveTarget.X - actor.Center.X);
            if (flyDir == 0) flyDir = actor.WalkDirection;
            actor.WalkDirection = flyDir;

            //扫描前方障碍，动态规划飞行弧线
            ScanObstacleExtent(actor, flyDir, out float obstacleHeight, out float obstacleWidth);
            requiredHeight = MathHelper.Clamp(obstacleHeight + ClearancePadding, MinClearance, MaxFlyHeight);
            obstacleEndX = actor.Center.X + flyDir * (obstacleWidth + 64f);

            SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.4f, Pitch = 0.3f }, actor.Center);
        }

        public IApolliaState Update(ApolliaActor actor) {
            timer++;
            actor.WalkDirection = flyDir;

            //尾焰粒子
            actor.SpawnJetParticle();
            if (Main.rand.NextBool(2)) {
                actor.SpawnJetParticle();
            }

            //── 水平移动 ──
            float distToTargetX = (moveTarget.X - actor.Center.X) * flyDir;
            float horizSpeed = FlySpeed;

            //接近目标时减速，避免飞过头
            if (distToTargetX > 0 && distToTargetX < ArrivalSlowdownDist) {
                horizSpeed *= MathHelper.Clamp(distToTargetX / ArrivalSlowdownDist, 0.25f, 1f);
            }

            //目标仍在前方才继续前进
            if (distToTargetX > 0) {
                actor.Position.X += horizSpeed * flyDir;
            }

            //── 垂直移动（三阶段） ──
            float heightGained = startY - actor.Position.Y;
            bool passedObstacle = (actor.Center.X - obstacleEndX) * flyDir >= 0;
            bool nearTarget = distToTargetX >= 0 && distToTargetX <= ArrivalSlowdownDist;

            switch (phase) {
                case Phase.Ascend:
                    actor.Velocity.Y = MathHelper.Lerp(actor.Velocity.Y, LiftSpeed, 0.12f);
                    //达到所需高度 → 进入巡航
                    if (heightGained >= requiredHeight) {
                        phase = Phase.Cruise;
                    }
                    //已经接近目标 → 跳过巡航直接下降
                    if (nearTarget && heightGained >= MinClearance * 0.5f) {
                        phase = Phase.Descend;
                    }
                    break;

                case Phase.Cruise:
                    //保持高度平飞，越过障碍后转入下降
                    actor.Velocity.Y = MathHelper.Lerp(actor.Velocity.Y, 0f, CruiseVerticalDamp);
                    if (passedObstacle || nearTarget) {
                        phase = Phase.Descend;
                    }
                    break;

                case Phase.Descend:
                    actor.Velocity.Y = Math.Min(actor.Velocity.Y + DescentGravity, MaxFallSpeed);
                    //脚下仍是深坑 → 减缓下降，保持滑翔
                    if (actor.Velocity.Y > 1.5f && IsOverGap(actor)) {
                        actor.Velocity.Y = MathHelper.Lerp(actor.Velocity.Y, 0.5f, 0.08f);
                    }
                    //探测到脚下地面 → 进入平滑着陆阶段
                    float probeDistance = actor.ProbeGroundDistance(6);
                    if (probeDistance >= 0 && probeDistance < 96f && actor.Velocity.Y > 0) {
                        landingGroundY = actor.Position.Y + actor.Height + probeDistance - actor.Height;
                        phase = Phase.Landing;
                    }
                    break;

                case Phase.Landing:
                    //平滑减速着陆：逐渐减小下落速度，柔和接近地面
                    float distToGround = landingGroundY - actor.Position.Y;
                    float landingDecel = MathHelper.Clamp(distToGround / 60f, 0.05f, 1f);
                    actor.Velocity.Y = MathHelper.Lerp(actor.Velocity.Y, MathHelper.Clamp(distToGround * 0.12f, 0.3f, 3f), 0.15f);

                    //足够接近地面时完成着陆
                    if (distToGround <= 2f) {
                        actor.Position.Y = landingGroundY;
                        actor.Velocity.Y = 0;
                        actor.OnGround = true;
                    }
                    break;
            }

            actor.Position.Y += actor.Velocity.Y;

            //── 安全超时 ──
            if (timer >= MaxFlyDuration) {
                return LandAndTransition(actor);
            }

            //── 着陆检测 ──
            if (timer >= MinAirTime) {
                //Landing 阶段完成
                if (phase == Phase.Landing && actor.OnGround) {
                    return LandAndTransition(actor);
                }
                //Descend 阶段使用 SnapToGround 作为兜底（长距离飞行跳过 Landing 探测的情况）
                if (phase == Phase.Descend && actor.Velocity.Y >= 1f) {
                    actor.SnapToGround();
                    if (actor.OnGround) {
                        return LandAndTransition(actor);
                    }
                }
            }

            return null;
        }

        public void Exit(ApolliaActor actor) {
            actor.UseJumpTexture = false;
            actor.Velocity = Vector2.Zero;
            actor.StopJetTrail();
        }

        #region 障碍扫描

        /// <summary>

        /// 扫描角色前方障碍区域的最大高度和总宽度（像素）， 同时考虑墙壁和深坑，连续3格无障碍视为障碍区域结束

        /// </summary>
        private static void ScanObstacleExtent(ApolliaActor actor, int dir, out float height, out float width) {
            int footTileY = (int)((actor.Position.Y + actor.Height) / 16f);
            int startTileX = (int)((actor.Center.X + dir * (actor.Width * 0.5f + 8f)) / 16f);

            const int maxScanTilesX = 30;
            int highestWallY = footTileY;
            int obstacleEndTileX = startTileX;
            bool foundObstacle = false;
            int clearCount = 0;

            for (int dx = 0; dx < maxScanTilesX; dx++) {
                int tileX = startTileX + dir * dx;
                if (!WorldGen.InWorld(tileX, footTileY)) break;

                bool columnBlocked = false;

                //检测墙壁：从脚底往上扫4格
                for (int dy = 0; dy <= 4; dy++) {
                    int tileY = footTileY - dy;
                    if (!WorldGen.InWorld(tileX, tileY)) continue;
                    Tile tile = Framing.GetTileSafely(tileX, tileY);
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                        columnBlocked = true;
                        if (tileY < highestWallY) highestWallY = tileY;
                    }
                }

                //检测深坑：脚下6格内无地面
                bool hasGround = false;
                for (int dy = 0; dy < 6; dy++) {
                    int tileY = footTileY + dy;
                    if (!WorldGen.InWorld(tileX, tileY)) break;
                    Tile tile = Framing.GetTileSafely(tileX, tileY);
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        hasGround = true;
                        break;
                    }
                }
                if (!hasGround) columnBlocked = true;

                if (columnBlocked) {
                    foundObstacle = true;
                    obstacleEndTileX = tileX;
                    clearCount = 0;
                }
                else {
                    clearCount++;
                    //连续3格无障碍 → 障碍区域结束
                    if (foundObstacle && clearCount >= 3) break;
                }
            }

            height = foundObstacle ? (footTileY - highestWallY) * 16f : 0f;
            width = foundObstacle ? Math.Abs(obstacleEndTileX - startTileX) * 16f + 16f : 0f;
        }

        /// <summary>

        /// 检测角色当前位置正下方是否是深坑（8格内无可着陆地面）

        /// </summary>
        private static bool IsOverGap(ApolliaActor actor) {
            int tileX = (int)(actor.Center.X / 16f);
            int tileY = (int)((actor.Position.Y + actor.Height) / 16f);

            for (int y = tileY; y < tileY + 8; y++) {
                if (!WorldGen.InWorld(tileX, y)) return true;
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) return false;
            }
            return true;
        }

        #endregion

        #region 着陆效果

        private IApolliaState LandAndTransition(ApolliaActor actor) {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Vector2 vel = new(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2f, -0.5f));
                    Dust dust = Dust.NewDustDirect(
                        actor.Center + new Vector2(Main.rand.NextFloat(-8, 8), 20),
                        1, 1, DustID.Smoke, vel.X, vel.Y, 150, default, Main.rand.NextFloat(0.6f, 1f));
                    dust.noGravity = true;
                }
            }

            SoundEngine.PlaySound(SoundID.Run with { Volume = 0.25f, Pitch = 0.2f }, actor.Center);

            if (onLand != null) {
                return onLand(actor);
            }

            return actor.CurrentCommand switch {
                HeroCommand.Hold => new ApolliaIdleState(),
                _ => new ApolliaWalkingState(),
            };
        }

        #endregion
    }
}
