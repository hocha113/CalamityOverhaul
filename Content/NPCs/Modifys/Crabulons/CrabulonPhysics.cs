using System;
using Terraria;

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

        public CrabulonPhysics(NPC npc, ModifyCrabulon owner) {
            this.npc = npc;
            this.owner = owner;
        }

        //获取到地面的距离
        public void UpdateGroundDistance() {
            GroundClearance = 0;
            Vector2 startPos = npc.Bottom;

            while (true) {
                Vector2 checkPos = startPos + new Vector2(0, GroundClearance);
                Tile tile = Framing.GetTileSafely(checkPos.ToTileCoordinates16());

                bool hitTile = owner.CanFallThroughPlatforms() == true
                    ? tile.HasSolidTile()
                    : tile.HasTile;

                if (hitTile || GroundClearance > CrabulonConstants.MaxGroundDistance) {
                    break;
                }

                GroundClearance += CrabulonConstants.GroundCheckInterval;
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
            for (int y = 0; y < 16; y += 2) {
                Tile checkTile = Framing.GetTileSafely((checkStart + new Vector2(0, y)).ToTileCoordinates16());
                if (checkTile.HasSolidTile()) {
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

                //检查在这个高度是否可以通过
                if (Collision.SolidCollision(checkPos, npc.width, npc.height)) {
                    break;//遇到障碍物，不能再往上
                }

                //检查这个高度前方是否有空间
                Vector2 forwardPos = checkPos + new Vector2(direction * (npc.width / 2f + 4), 0);
                if (!Collision.SolidCollision(forwardPos, npc.width / 2, npc.height)) {
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
            //设置攀爬参数
            JumpHeightUpdate = heightLevel;
            JumpHeightSetFrame = CrabulonConstants.MountTimeout;
            npc.velocity.Y = -6f;//给一个小的向上速度
            //标记需要网络同步
            npc.netUpdate = true;
        }

        //处理跳跃高度更新，使用平滑过渡
        public void UpdateJumpHeight() {
            if (JumpHeightUpdate > 0) {
                JumpHeightSetFrame = CrabulonConstants.MountTimeout;

                //计算本帧应该上升的距离
                float climbSpeed = 14f;
                float climbDistance = Math.Min(JumpHeightUpdate, climbSpeed);

                JumpHeightUpdate -= climbDistance;
                npc.position.Y -= climbDistance;

                //在攀爬期间禁用重力
                npc.noGravity = true;

                //标记需要同步
                if (climbDistance > 0) {
                    npc.netUpdate = true;
                }
            }

            //更新攀爬冷却
            if (JumpHeightSetFrame > 0) {
                JumpHeightSetFrame--;

                //冷却结束时恢复重力
                if (JumpHeightSetFrame == 0) {
                    npc.noGravity = false;
                }
            }
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
