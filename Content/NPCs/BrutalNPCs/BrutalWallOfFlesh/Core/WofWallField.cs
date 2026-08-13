using System;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core
{
    /// <summary>
    /// 墙域几何与原版契约维护：wofNPCIndex / wofDrawArea 扫描(移植原版 aiStyle27)、
    /// 墙面坐标、本地玩家带状伤害、部件读取的视觉通道
    /// </summary>
    internal static class WofWallField
    {
        /// <summary>墙域上缘(世界像素)</summary>
        public static float Top => Main.wofDrawAreaTop;
        /// <summary>墙域下缘(世界像素)</summary>
        public static float Bottom => Main.wofDrawAreaBottom;
        /// <summary>墙域中线</summary>
        public static float MiddleY => (Main.wofDrawAreaTop + Main.wofDrawAreaBottom) * 0.5f;
        /// <summary>墙域高度</summary>
        public static float Height => Main.wofDrawAreaBottom - Main.wofDrawAreaTop;

        /// <summary>演出接管墙域标志：>0 时跳过原版扫描，由演出直接写 wofDrawArea</summary>
        public static int CinematicAreaLock;

        /// <summary>推进方向上的墙面X(含贴图前伸修正)</summary>
        public static float WallFaceX(NPC wall) {
            return wall.direction > 0 ? wall.position.X + wall.width + 48f : wall.position.X - 48f;
        }

        /// <summary>x 是否已被墙面吞没(位于墙面之后)</summary>
        public static bool BehindFace(NPC wall, float x) {
            return (WallFaceX(wall) - x) * wall.direction > 0f;
        }

        /// <summary>
        /// 每帧维护原版契约：wofNPCIndex、墙域上下缘扫描(1px/帧渐变)、上下缘160px最小间距。
        /// 移植自 NPC.cs aiStyle27，行为一致以保证原版绘制/舌头/音乐正常工作
        /// </summary>
        public static void MaintainWallArea(NPC npc) {
            Main.wofNPCIndex = npc.whoAmI;

            if (CinematicAreaLock > 0) {
                //演出直控期不扫描，仍夹紧最小间距
                ClampAreaGap();
                return;
            }

            ComputeScanTargets(npc, out int topTarget, out int bottomTarget);
            StepToward(ref Main.wofDrawAreaBottom, bottomTarget);
            StepToward(ref Main.wofDrawAreaTop, topTarget);

            int layerTop = Main.UnderworldLayer + 10;
            int layerBottom = layerTop + 70;
            Main.wofDrawAreaTop = (int)MathHelper.Clamp(Main.wofDrawAreaTop, layerTop * 16f, layerBottom * 16f);
            Main.wofDrawAreaBottom = (int)MathHelper.Clamp(Main.wofDrawAreaBottom, layerTop * 16f, layerBottom * 16f);
            ClampAreaGap();
        }

        /// <summary>扫描地形得到墙域上下缘目标(世界像素)，演出直控时也可单独调用</summary>
        public static void ComputeScanTargets(NPC npc, out int topTargetPx, out int bottomTargetPx) {
            int layerTop = Main.UnderworldLayer + 10;
            int layerBottom = layerTop + 70;
            int tileX0 = (int)(npc.position.X / 16f);
            int tileX1 = (int)((npc.position.X + npc.width) / 16f);
            int tileYMid = (int)((npc.position.Y + npc.height / 2) / 16f);

            //向下扫实心/液体，找地板
            int solidCount = 0;
            int scanY = tileYMid + 7;
            while (solidCount < 15 && scanY > Main.UnderworldLayer) {
                scanY++;
                if (scanY > Main.maxTilesY - 10) {
                    scanY = Main.maxTilesY - 10;
                    break;
                }
                if (scanY < layerTop) {
                    continue;
                }
                for (int x = tileX0; x <= tileX1; x++) {
                    try {
                        if (WorldGen.InWorld(x, scanY, 2) && (WorldGen.SolidTile(x, scanY) || Main.tile[x, scanY].LiquidAmount > 0)) {
                            solidCount++;
                        }
                    } catch {
                        solidCount += 15;
                    }
                }
            }
            bottomTargetPx = (scanY + 4) * 16;

            //向上扫，找顶板
            solidCount = 0;
            scanY = tileYMid - 7;
            while (solidCount < 15 && scanY < Main.maxTilesY - 10) {
                scanY--;
                if (scanY <= 10) {
                    scanY = 10;
                    break;
                }
                if (scanY > layerBottom) {
                    continue;
                }
                if (scanY < layerTop) {
                    scanY = layerTop;
                    break;
                }
                for (int x = tileX0; x <= tileX1; x++) {
                    try {
                        if (WorldGen.InWorld(x, scanY, 2) && (WorldGen.SolidTile(x, scanY) || Main.tile[x, scanY].LiquidAmount > 0)) {
                            solidCount++;
                        }
                    } catch {
                        solidCount += 15;
                    }
                }
            }
            topTargetPx = (scanY - 4) * 16;
        }

        /// <summary>原版1px/帧渐变逼近，-1 时直接初始化</summary>
        private static void StepToward(ref int area, int targetPx) {
            if (area == -1) {
                area = targetPx;
            }
            else if (area > targetPx) {
                area--;
                if (area < targetPx) {
                    area = targetPx;
                }
            }
            else if (area < targetPx) {
                area++;
                if (area > targetPx) {
                    area = targetPx;
                }
            }
        }

        /// <summary>上下缘最小 160px 间距(原版行为)</summary>
        private static void ClampAreaGap() {
            if (Main.wofDrawAreaTop > Main.wofDrawAreaBottom - 160) {
                Main.wofDrawAreaTop = Main.wofDrawAreaBottom - 160;
            }
            else if (Main.wofDrawAreaBottom < Main.wofDrawAreaTop + 160) {
                Main.wofDrawAreaBottom = Main.wofDrawAreaTop + 160;
            }
        }

        /// <summary>
        /// 本地玩家X带状伤害(镜像原版 WOFTongue 的自伤模型：各客户端只对自己的玩家判定)。
        /// 返回是否命中
        /// </summary>
        public static bool HurtLocalPlayerInBand(NPC wall, float xMin, float xMax, int scaledDamage, PlayerDeathReason reason, int hitDirection) {
            if (Main.dedServ || Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers) {
                return false;
            }
            Player player = Main.LocalPlayer;
            if (!player.Alives() || player.immune || player.ghost) {
                return false;
            }
            if (player.position.X + player.width < xMin || player.position.X > xMax) {
                return false;
            }
            player.Hurt(reason, scaledDamage, hitDirection);
            return true;
        }

        /// <summary>本地玩家与线段的距离判定伤害(饥饿网链用)</summary>
        public static bool HurtLocalPlayerNearSegment(Vector2 a, Vector2 b, float radius, int scaledDamage, PlayerDeathReason reason) {
            if (Main.dedServ || Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers) {
                return false;
            }
            Player player = Main.LocalPlayer;
            if (!player.Alives() || player.immune || player.ghost) {
                return false;
            }
            float dist = DistancePointToSegment(player.Center, a, b);
            if (dist > radius + Math.Max(player.width, player.height) * 0.5f) {
                return false;
            }
            player.Hurt(reason, scaledDamage, player.Center.X < (a.X + b.X) * 0.5f ? -1 : 1);
            return true;
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 0.001f) {
                return Vector2.Distance(p, a);
            }
            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return Vector2.Distance(p, a + ab * t);
        }

        #region 部件视觉通道(各端本地由状态驱动，不走网络)

        private struct VisualEntry
        {
            public float Flush;
            public float EyeCharge;
            public uint Stamp;
        }

        private static VisualEntry visual;
        private static int visualOwner = -1;

        /// <summary>主控每帧推送：潮红强度 + 眼部蓄能(供眼/网/着色器读取)</summary>
        public static void PushVisual(int wallWhoAmI, float flush, float eyeCharge) {
            visualOwner = wallWhoAmI;
            visual.Flush = flush;
            visual.EyeCharge = eyeCharge;
            visual.Stamp = Main.GameUpdateCount;
        }

        /// <summary>读取视觉通道，过期(超过2帧)返回零值</summary>
        public static (float flush, float eyeCharge) ReadVisual(int wallWhoAmI) {
            if (visualOwner != wallWhoAmI || Main.GameUpdateCount - visual.Stamp > 2) {
                return (0f, 0f);
            }
            return (visual.Flush, visual.EyeCharge);
        }

        #endregion
    }
}
