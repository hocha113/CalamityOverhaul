using System;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>菌生蟹物理与地形交互</summary>
    internal class CrabulonPhysics
    {
        private readonly NPC npc;
        private readonly ModifyCrabulon owner;

        public float GroundClearance { get; set; }
        public float JumpHeightUpdate { get; set; }
        public float JumpHeightSetFrame { get; set; }

        //卡入修正
        private int stuckCheckTimer = 0;
        private Vector2 lastValidPosition;
        private const int StuckCheckInterval = 30;

        public CrabulonPhysics(NPC npc, ModifyCrabulon owner) {
            this.npc = npc;
            this.owner = owner;
            lastValidPosition = npc.position;
        }

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

                bool hitTile;
                if (owner.CanFallThroughPlatforms() == true) {
                    hitTile = tile.HasSolidTile();
                }
                else {
                    hitTile = tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
                }

                if (hitTile) {
                    foundGround = true;
                    break;
                }

                GroundClearance += CrabulonConstants.GroundCheckInterval;
            }

            if (!foundGround) {
                GroundClearance = CrabulonConstants.MaxGroundDistance;
            }
        }

        public void AutoStepClimbing() {
            if (VaultUtils.isClient) {
                return;//物理仅权威端
            }

            if (npc.noTileCollide || !npc.collideX) {
                return;
            }

            int direction = Math.Sign(npc.velocity.X);
            if (direction == 0) {
                return;
            }

            Vector2 frontBottom = npc.Bottom + new Vector2(direction * (npc.width / 2f + 8), 0);

            if (!DetectStepAhead(frontBottom, direction, out int stepHeight)) {
                return;
            }

            PerformStepClimb(stepHeight);
        }

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
                return false;//前方悬空
            }

            int maxClimbHeight = CrabulonConstants.MaxStepHeight / CrabulonConstants.StepCheckInterval;
            int foundHeight = 0;

            for (int i = 1; i <= maxClimbHeight; i++) {
                int checkHeightPixels = i * CrabulonConstants.StepCheckInterval;
                Vector2 checkPos = npc.position - new Vector2(0, checkHeightPixels);

                if (Collision.SolidCollision(checkPos, npc.width, npc.height)) {
                    break;
                }

                Vector2 forwardPos = checkPos + new Vector2(direction * (npc.width / 2f + 4), 0);

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

        private void PerformStepClimb(int heightLevel) {
            lastValidPosition = npc.position;

            JumpHeightUpdate = heightLevel * CrabulonConstants.StepCheckInterval;
            JumpHeightSetFrame = CrabulonConstants.MountTimeout;

            npc.velocity.Y = -4f;
            npc.netUpdate = true;
        }

        public void UpdateJumpHeight() {
            if (JumpHeightUpdate > 0) {
                JumpHeightSetFrame = CrabulonConstants.MountTimeout;

                float climbSpeed = 10f;
                float climbDistance = Math.Min(JumpHeightUpdate, climbSpeed);

                Vector2 newPosition = npc.position - new Vector2(0, climbDistance);
                if (!WouldCollideAtPosition(newPosition)) {
                    JumpHeightUpdate -= climbDistance;
                    npc.position.Y -= climbDistance;
                    lastValidPosition = npc.position;
                }
                else {
                    JumpHeightUpdate = 0;
                }

                if (npc.velocity.Y > 0) {
                    npc.velocity.Y *= 0.5f;
                }

                if (climbDistance > 0) {
                    npc.netUpdate = true;
                }
            }

            if (JumpHeightSetFrame > 0) {
                JumpHeightSetFrame--;
            }
        }

        private bool WouldCollideAtPosition(Vector2 position) {
            return Collision.SolidCollision(position, npc.width, npc.height);
        }

        public void CheckAndFixStuckPosition() {
            //骑乘吸附时位置由骑手定，跳过修正
            if (owner.Mount) {
                stuckCheckTimer = 0;
                lastValidPosition = npc.position;
                return;
            }

            stuckCheckTimer++;

            if (stuckCheckTimer < StuckCheckInterval) {
                return;
            }

            stuckCheckTimer = 0;

            if (Collision.SolidCollision(npc.position, npc.width, npc.height)) {
                if (TryFixStuckPosition()) {
                    return;
                }

                if (lastValidPosition != Vector2.Zero &&
                    !Collision.SolidCollision(lastValidPosition, npc.width, npc.height)) {
                    npc.position = lastValidPosition;
                    npc.velocity = Vector2.Zero;
                    npc.netUpdate = true;
                }
            }
            else {
                lastValidPosition = npc.position;
            }
        }

        private bool TryFixStuckPosition() {
            Vector2[] directions = new Vector2[]
            {
                new Vector2(0, -8),
                new Vector2(0, 8),
                new Vector2(-8, 0),
                new Vector2(8, 0),
                new Vector2(-8, -8),
                new Vector2(8, -8),
                new Vector2(-8, 8),
                new Vector2(8, 8)
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

        public void ClampToWorldBounds() {
            ushort border = CrabulonConstants.WorldBorder;
            npc.position.X = MathHelper.Clamp(npc.position.X, border, Main.maxTilesX * 16 - border);
            npc.position.Y = MathHelper.Clamp(npc.position.Y, border, Main.maxTilesY * 16 - border);
        }

        public bool? ShouldFallThroughPlatforms() {
            if (owner.Mount) {
                return true;//骑乘时蟹无碰撞，平台由骑手侧
            }

            if (owner.ai[7] > 0) {
                return false;//垂直追逐不穿平台
            }

            if (owner.ai[10] > 0) {
                if (npc.velocity.Y == 0)
                    npc.position.Y += 1f;
                return true;
            }

            return null;
        }
    }
}
