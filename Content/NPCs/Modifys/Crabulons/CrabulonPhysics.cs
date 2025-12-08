using System;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    //菌生蟹物理系统，处理地形交互和移动
    internal class CrabulonPhysics
    {
        private readonly NPC npc;
        private readonly ModifyCrabulon owner;

        public float GroundClearance { get; private set; }
        public float JumpHeightUpdate { get; set; }
        public float JumpHeightSetFrame { get; set; }

        //位置修正相关
        private int stuckCheckTimer = 0;
        private Vector2 lastValidPosition;
        private const int StuckCheckInterval = 30;

        public CrabulonPhysics(NPC npc, ModifyCrabulon owner) {
            this.npc = npc;
            this.owner = owner;
            lastValidPosition = npc.position;
        }

        //获取到地面的距离
        public void UpdateGroundDistance() {
            GroundClearance = 0;
            Vector2 startPos = npc.Bottom;
            bool foundGround = false;

            while (GroundClearance < CrabulonConstants.MaxGroundDistance) {
                Vector2 checkPos = startPos + new Vector2(0, GroundClearance);
                Point16 tileCoords16 = checkPos.ToTileCoordinates16();
                Point tileCoords = new Point(tileCoords16.X, tileCoords16.Y);

                if (!WorldGen.InWorld(tileCoords.X, tileCoords.Y)) {
                    break;
                }

                Tile tile = Framing.GetTileSafely(tileCoords);

                //改进的地面检测逻辑
                bool hitTile = false;
                if (owner.CanFallThroughPlatforms() == true) {
                    //只检测实心方块
                    hitTile = tile.HasSolidTile();
                }
                else {
                    //检测所有可碰撞的方块（包括平台）
                    hitTile = tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
                }

                if (hitTile) {
                    foundGround = true;
                    break;
                }

                GroundClearance += CrabulonConstants.GroundCheckInterval;
            }

            //如果没找到地面，设置为最大值
            if (!foundGround) {
                GroundClearance = CrabulonConstants.MaxGroundDistance;
            }
        }

        //自动攀爬台阶
        public void AutoStepClimbing() {
            //只在服务器或单人模式执行物理计算
            if (VaultUtils.isClient) {
                return;
            }

            //必要条件检查
            if (npc.noTileCollide || !npc.collideX) {
                return;
            }

            //使用方向感知检测
            int direction = Math.Sign(npc.velocity.X);
            if (direction == 0) {
                return;
            }

            //从NPC前方检测台阶高度
            Vector2 frontBottom = npc.Bottom + new Vector2(direction * (npc.width / 2f + 8), 0);

            //检测是否存在需要攀爬的台阶
            if (!DetectStepAhead(frontBottom, direction, out int stepHeight)) {
                return;
            }

            //执行攀爬
            PerformStepClimb(stepHeight);
        }

        //检测前方是否有台阶
        private bool DetectStepAhead(Vector2 checkStart, int direction, out int stepHeight) {
            stepHeight = 0;

            //向下检测是否有实心方块
            bool hasGroundAhead = false;
            for (int y = 0; y < 20; y += 2) {
                Vector2 checkPos = checkStart + new Vector2(0, y);
                Point16 tileCoords16 = checkPos.ToTileCoordinates16();
                Point tileCoords = new Point(tileCoords16.X, tileCoords16.Y);

                if (!WorldGen.InWorld(tileCoords.X, tileCoords.Y)) {
                    continue;
                }

                Tile checkTile = Framing.GetTileSafely(tileCoords);
                if (checkTile.HasTile) {
                    hasGroundAhead = true;
                    break;
                }
            }

            if (!hasGroundAhead) {
                return false;//前方是空的，不需要爬
            }

            //向上检测可以攀爬的最大高度
            int maxClimbHeight = CrabulonConstants.MaxStepHeight / CrabulonConstants.StepCheckInterval;
            int foundHeight = 0;

            for (int i = 1; i <= maxClimbHeight; i++) {
                int checkHeightPixels = i * CrabulonConstants.StepCheckInterval;
                Vector2 checkPos = npc.position - new Vector2(0, checkHeightPixels);

                //检查在这个高度是否可以通过，检查整个NPC的碰撞箱
                if (Collision.SolidCollision(checkPos, npc.width, npc.height)) {
                    break;//遇到障碍物，不能再往上
                }

                //检查这个高度前方是否有足够空间
                Vector2 forwardPos = checkPos + new Vector2(direction * (npc.width / 2f + 4), 0);

                //确保前方和上方都有空间
                bool hasSpace = !Collision.SolidCollision(forwardPos, npc.width / 2, npc.height);
                bool hasHeadroom = !Collision.SolidCollision(checkPos + new Vector2(0, -npc.height / 2), npc.width, npc.height / 2);

                if (hasSpace && hasHeadroom) {
                    foundHeight = i;
                }
            }

            if (foundHeight > 0) {
                stepHeight = foundHeight;
                return true;
            }

            return false;
        }

        //执行台阶攀爬
        private void PerformStepClimb(int heightLevel) {
            //记录当前位置为有效位置
            lastValidPosition = npc.position;

            //设置攀爬参数
            JumpHeightUpdate = heightLevel * CrabulonConstants.StepCheckInterval;
            JumpHeightSetFrame = CrabulonConstants.MountTimeout;

            //给一个小的向上速度，但不要太大
            npc.velocity.Y = -4f;

            //标记需要网络同步
            npc.netUpdate = true;
        }

        //处理跳跃高度更新
        public void UpdateJumpHeight() {
            if (JumpHeightUpdate > 0) {
                JumpHeightSetFrame = CrabulonConstants.MountTimeout;

                //计算本帧应该上升的距离
                float climbSpeed = 10f; //降低速度以提高稳定性
                float climbDistance = Math.Min(JumpHeightUpdate, climbSpeed);

                //在实际移动前检查是否会卡入方块
                Vector2 newPosition = npc.position - new Vector2(0, climbDistance);
                if (!WouldCollideAtPosition(newPosition)) {
                    JumpHeightUpdate -= climbDistance;
                    npc.position.Y -= climbDistance;
                    lastValidPosition = npc.position; //更新有效位置
                }
                else {
                    //如果会卡入方块，停止攀爬
                    JumpHeightUpdate = 0;
                }

                //在攀爬期间减少重力影响而不是完全禁用
                if (npc.velocity.Y > 0) {
                    npc.velocity.Y *= 0.5f;
                }

                //标记需要同步
                if (climbDistance > 0) {
                    npc.netUpdate = true;
                }
            }

            //更新攀爬冷却
            if (JumpHeightSetFrame > 0) {
                JumpHeightSetFrame--;
            }
        }

        //检查在指定位置是否会发生碰撞
        private bool WouldCollideAtPosition(Vector2 position) {
            return Collision.SolidCollision(position, npc.width, npc.height);
        }

        //卡入方块检测和修正
        public void CheckAndFixStuckPosition() {
            stuckCheckTimer++;

            if (stuckCheckTimer < StuckCheckInterval) {
                return;
            }

            stuckCheckTimer = 0;

            //检查NPC是否卡在方块中
            if (Collision.SolidCollision(npc.position, npc.width, npc.height)) {
                //尝试修正位置
                if (TryFixStuckPosition()) {
                    return;
                }

                //如果修正失败，回退到上次有效位置
                if (lastValidPosition != Vector2.Zero &&
                    !Collision.SolidCollision(lastValidPosition, npc.width, npc.height)) {
                    npc.position = lastValidPosition;
                    npc.velocity = Vector2.Zero;
                    npc.netUpdate = true;
                }
            }
            else {
                //当前位置有效，更新记录
                lastValidPosition = npc.position;
            }
        }

        //尝试修正卡入位置
        private bool TryFixStuckPosition() {
            //尝试8个方向的偏移
            Vector2[] directions = new Vector2[]
            {
                new Vector2(0, -8),  //向上
                new Vector2(0, 8),   //向下
                new Vector2(-8, 0),  //向左
                new Vector2(8, 0),   //向右
                new Vector2(-8, -8), //左上
                new Vector2(8, -8),  //右上
                new Vector2(-8, 8),  //左下
                new Vector2(8, 8)    //右下
            };

            foreach (Vector2 offset in directions) {
                Vector2 testPos = npc.position + offset;
                if (!Collision.SolidCollision(testPos, npc.width, npc.height)) {
                    npc.position = testPos;
                    npc.velocity *= 0.5f;
                    lastValidPosition = testPos;
                    npc.netUpdate = true;
                    return true;
                }
            }

            return false;
        }

        //限制NPC在世界边界内
        public void ClampToWorldBounds() {
            ushort border = CrabulonConstants.WorldBorder;
            npc.position.X = MathHelper.Clamp(npc.position.X, border, Main.maxTilesX * 16 - border);
            npc.position.Y = MathHelper.Clamp(npc.position.Y, border, Main.maxTilesY * 16 - border);
        }

        //检测是否应该穿过平台
        public bool? ShouldFallThroughPlatforms() {
            //骑乘模式下按S键穿过平台
            if (owner.Mount && owner.Owner.Alives() && owner.Owner.holdDownCardinalTimer[0] > 2) {
                return true;
            }

            //垂直追逐期间不穿平台
            if (owner.ai[7] > 0) {
                return false;
            }

            //需要下落时穿过平台
            if (owner.ai[10] > 0) {
                return true;
            }

            return null;
        }
    }
}
