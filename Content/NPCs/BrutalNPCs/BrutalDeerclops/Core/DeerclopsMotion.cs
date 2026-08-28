using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core
{
    /// <summary>地面行走物理(移植原版 AI_123_Deerclops_Movement)与地形工具</summary>
    internal static class DeerclopsMotion
    {
        /// <summary>冰蓝主色</summary>
        internal static readonly Color IceBlue = new Color(140, 215, 255);
        /// <summary>深冰蓝</summary>
        internal static readonly Color DeepIce = new Color(62, 118, 196);
        /// <summary>暗影紫</summary>
        internal static readonly Color ShadowViolet = new Color(84, 38, 140);
        /// <summary>凝视红</summary>
        internal static readonly Color GazeRed = new Color(255, 62, 48);
        /// <summary>冷白</summary>
        internal static readonly Color ColdWhite = new Color(235, 250, 255);

        /// <summary>
        /// 行走一帧。原版物理：lerp 加速、上台阶、跳跃(-8)、手动重力(0.4)。
        /// 独眼巨鹿 noGravity+noTileCollide，垂直运动必须每帧经这里维护
        /// </summary>
        public static void Walk(NPC npc, DeerclopsStateContext ctx, bool halt) {
            float lifeRatio = npc.life / (float)npc.lifeMax;
            float baseSpeed = 3.5f + 1.2f * (1f - lifeRatio);
            if (ctx.IsPhase2) {
                baseSpeed += 0.7f;
            }
            if (ctx.IsAsuraMode) {
                baseSpeed += 0.5f;
            }
            baseSpeed *= ctx.MoveSpeedMult;

            //目标X：覆盖优先，否则追目标玩家
            float targetX = float.IsNaN(ctx.TargetXOverride)
                ? (ctx.Target?.Center.X ?? npc.Center.X)
                : ctx.TargetXOverride;

            float dx = targetX - npc.Center.X;
            bool closeToTarget = Math.Abs(dx) < 80f;
            bool stopMoving = closeToTarget || halt;

            if (stopMoving) {
                npc.velocity.X *= 0.88f;
                if (npc.velocity.X > -0.1f && npc.velocity.X < 0.1f) {
                    npc.velocity.X = 0f;
                }
            }
            else {
                int moveDir = Math.Sign(dx);
                npc.velocity.X = MathHelper.Lerp(npc.velocity.X, moveDir * baseSpeed, 0.25f);
            }

            //朝向：强制>运动方向
            if (ctx.ForcedDirection != 0) {
                npc.direction = npc.spriteDirection = ctx.ForcedDirection;
            }
            else if (Math.Abs(dx) > 24f) {
                npc.direction = npc.spriteDirection = Math.Sign(dx);
            }

            ApplyVertical(npc, ctx, allowJump: !stopMoving);
        }

        /// <summary>
        /// 垂直物理：SolidCollision 上台阶/落地/跳跃/重力，冲撞等自管水平时也要每帧调用
        /// </summary>
        public static void ApplyVertical(NPC npc, DeerclopsStateContext ctx, bool allowJump) {
            Rectangle targetHitbox = ctx.Target?.Hitbox ?? npc.Hitbox;

            int footW = 40;
            int footH = 20;
            Vector2 foot = new Vector2(npc.Center.X - footW / 2f, npc.position.Y + npc.height - footH);
            bool straddling = foot.X < targetHitbox.X && foot.X + npc.width > targetHitbox.X + targetHitbox.Width;
            bool aboveTarget = foot.Y + footH < targetHitbox.Y + targetHitbox.Height - 16;
            bool acceptTopSurfaces = npc.Bottom.Y >= targetHitbox.Top;
            bool insideTiles = Collision.SolidCollision(foot, footW, footH, acceptTopSurfaces);
            bool insideTilesShallow = Collision.SolidCollision(foot, footW, footH - 4, acceptTopSurfaces);
            bool wallAhead = !Collision.SolidCollision(foot + new Vector2(footW * npc.direction, 0f), 16, 80, acceptTopSurfaces);

            float jumpVel = ctx.IsAsuraMode ? -11f : -8.5f;
            float gravity = 0.4f;
            float riseCap = ctx.IsAsuraMode ? -12f : -8f;

            if (insideTiles || insideTilesShallow) {
                //贴地重置跳跃闩
                npc.localAI[0] = 0f;
            }

            bool closeToTargetX = Math.Abs(targetHitbox.Center.X - npc.Center.X) < 80f;
            if ((straddling || closeToTargetX) && aboveTarget) {
                //目标在正下方，主动下沉(穿平台)
                npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + gravity * 2f, 0.001f, 16f);
            }
            else if (insideTiles && !insideTilesShallow) {
                //刚好踩面，站稳
                npc.velocity.Y = 0f;
            }
            else if (insideTiles) {
                //陷入地形，上浮台阶
                npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y - gravity, riseCap, 0f);
            }
            else if (allowJump && npc.velocity.Y == 0f && wallAhead && npc.localAI[0] == 0f) {
                //前方悬空或需要越障，起跳
                npc.velocity.Y = jumpVel;
                npc.localAI[0] = 1f;
            }
            else {
                npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + gravity, jumpVel, 16f);
            }
        }

        #region 地形查询

        /// <summary>自某世界坐标向下找地表，返回地表世界坐标(格顶)</summary>
        public static Vector2 FindGroundBelow(Vector2 worldPos, int maxTilesDown = 42) {
            Point tile = worldPos.ToTileCoordinates();
            for (int i = 0; i < maxTilesDown; i++) {
                int y = tile.Y + i;
                if (y >= Main.maxTilesY - 10) {
                    break;
                }
                if (WorldGen.SolidTile(tile.X, y)) {
                    return new Vector2(tile.X * 16f + 8f, y * 16f);
                }
            }
            return worldPos + new Vector2(0f, maxTilesDown * 16f);
        }

        /// <summary>
        /// 冰刺落点搜寻(移植原版 FindBestY)：以源行起步、偏向目标脚底，上出实心下找可站
        /// </summary>
        public static int FindSpikeY(NPC npc, Point sourceTileCoords, int x) {
            int bestY = sourceTileCoords.Y;
            if (npc.HasValidTarget) {
                Rectangle hitbox = Main.player[npc.target].Hitbox;
                Vector2 targetFeet = new Vector2(hitbox.Center.X, hitbox.Bottom);
                int feetY = (int)(targetFeet.Y / 16f);
                int sign = Math.Sign(feetY - bestY);
                if (sign == 0) {
                    sign = 1;
                }
                int scanEnd = feetY + sign * 15;
                int? candidate = null;
                float bestDist = float.PositiveInfinity;
                for (int i = bestY; i != scanEnd; i += sign) {
                    if (i < 20 || i > Main.maxTilesY - 20) {
                        break;
                    }
                    if (WorldGen.ActiveAndWalkableTile(x, i)) {
                        float dist = new Point(x, i).ToWorldCoordinates().Distance(targetFeet);
                        if (!candidate.HasValue || dist < bestDist) {
                            candidate = i;
                            bestDist = dist;
                        }
                    }
                }
                if (candidate.HasValue) {
                    bestY = candidate.Value;
                }
            }
            for (int j = 0; j < 20; j++) {
                if (bestY < 10 || !WorldGen.SolidTile(x, bestY)) {
                    break;
                }
                bestY--;
            }
            for (int k = 0; k < 20; k++) {
                if (bestY > Main.maxTilesY - 10 || WorldGen.ActiveAndWalkableTile(x, bestY)) {
                    break;
                }
                bestY++;
            }
            return bestY;
        }

        #endregion

        #region 通用表现

        /// <summary>本地震屏(带减震配置门)</summary>
        public static void CameraPunch(Vector2 pos, float strength, int frames, string uniqueId, Vector2? dir = null) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 direction = dir ?? Main.rand.NextVector2Unit();
            PunchCameraModifier modifier = new PunchCameraModifier(pos, direction, strength, 6f, frames, 1600f, uniqueId);
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>是否屏内(含边距)</summary>
        public static bool OnScreen(Vector2 worldPos, float margin = 260f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        #endregion
    }
}
