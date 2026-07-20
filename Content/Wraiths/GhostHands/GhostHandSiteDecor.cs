using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.Actors;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 焦黑裂隙的客户端贴饰层（§3.3/§5 表 #3#4）：锚点裂缝 + 手印路标。
    /// 印记由锚点坐标做种确定性生成，无需存档与同步（各端同种子同结果，
    /// 客户端锚知识来自 <c>WraithNet.SiteSync</c> 镜像）；认主后保留（反噬取锁的地标）
    /// </summary>
    internal sealed class GhostHandSiteDecor : ModSystem
    {
        private struct HandMark
        {
            public Vector2 Pos;
            public Vector2 Up;          //指向方位(墙面=竖直向上,地/顶面=朝锚点)
            public float Size;
            public float Tilt;
            public int ScratchCount;    //0=无抓痕
            public float ScratchLen;
            public bool NearAnchor;     //≤25 瓦叠余烬微光
        }

        //环带(瓦)与各带印记数,共 24 处:越近越密
        private static readonly int[] RingRadii = [90, 60, 40, 25, 15, 8];
        private static readonly int[] RingCounts = [1, 2, 3, 4, 6, 8];

        private static readonly List<HandMark> marks = [];
        private static Vector2 builtAnchor = new(float.NaN, float.NaN);
        private static int rebuildTimer;

        public override void ClearWorld() {
            marks.Clear();
            builtAnchor = new Vector2(float.NaN, float.NaN);
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            if (!WraithSiteSystem.TryGet(nameof(GhostHand), out WraithSiteRecord record) || !record.Anchored
                || !WraithActs.ActTwo) {
                return;
            }
            Player local = Main.LocalPlayer;
            if (local == null || !local.active) {
                return;
            }
            //印记依赖已加载的瓦数据:贴近后周期重建,同种子结果收敛一致
            float localDistSq = Vector2.DistanceSquared(local.Center, record.Anchor);
            if (localDistSq < 2400f * 2400f && (builtAnchor != record.Anchor || ++rebuildTimer >= 300)) {
                rebuildTimer = 0;
                builtAnchor = record.Anchor;
                RebuildMarks(record.Anchor);
            }
            if (builtAnchor != record.Anchor) {
                return;
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Rectangle view = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200,
                Main.screenWidth + 400, Main.screenHeight + 400);
            DrawFissure(Main.spriteBatch, record.Anchor, view);
            foreach (HandMark mark in marks) {
                if (view.Contains((int)mark.Pos.X, (int)mark.Pos.Y)) {
                    DrawMark(Main.spriteBatch, mark);
                }
            }

            Main.spriteBatch.End();
        }

        //====印记生成（确定性）====

        private static void RebuildMarks(Vector2 anchor) {
            marks.Clear();
            Random rng = new(anchor.GetHashCode());
            Point anchorTile = anchor.ToTileCoordinates();
            for (int ring = 0; ring < RingRadii.Length; ring++) {
                for (int n = 0; n < RingCounts[ring]; n++) {
                    //rng 抽取顺序固定,与放置成败无关,各端同序
                    float angle = (float)(rng.NextDouble() * MathHelper.TwoPi);
                    bool scratches = rng.NextDouble() < 0.6;
                    int scratchCount = 4 + (rng.Next(2));
                    float scratchLen = 8f + (float)rng.NextDouble() * 12f;
                    float size = 0.85f + (float)rng.NextDouble() * 0.4f;
                    float tilt = ((float)rng.NextDouble() - 0.5f) * 0.5f;

                    if (!TryPlaceMark(anchorTile, angle, RingRadii[ring], out Vector2 pos, out Vector2 up)) {
                        continue;
                    }
                    marks.Add(new HandMark {
                        Pos = pos,
                        Up = up,
                        Size = size,
                        Tilt = tilt,
                        ScratchCount = scratches ? scratchCount : 0,
                        ScratchLen = scratchLen,
                        NearAnchor = RingRadii[ring] <= 25,
                    });
                }
            }
        }

        /// <summary>沿方位射线在环带附近找"实体壁面贴邻气窝"的面：优先 ≥4×4 大气窝，退而 ≥2×2</summary>
        private static bool TryPlaceMark(Point anchorTile, float angle, int ringRadius, out Vector2 pos, out Vector2 up) {
            Vector2 dir = angle.ToRotationVector2();
            for (int pass = 0; pass < 2; pass++) {
                bool wantBig = pass == 0;
                for (int step = ringRadius - 4; step <= ringRadius + 4; step++) {
                    Point tile = new(anchorTile.X + (int)(dir.X * step), anchorTile.Y + (int)(dir.Y * step));
                    if (TryFindFace(tile.X, tile.Y, wantBig, out pos, out up)) {
                        return true;
                    }
                }
            }
            pos = default;
            up = default;
            return false;
        }

        /// <summary>该瓦或近邻是否为贴邻气窝的实体面（印记可被实际路过的隧道遇见）</summary>
        private static bool TryFindFace(int x, int y, bool wantBigPocket, out Vector2 pos, out Vector2 up) {
            for (int dx = -1; dx <= 1; dx++) {
                for (int dy = -1; dy <= 1; dy++) {
                    if (TryFaceAt(x + dx, y + dy, wantBigPocket, out pos, out up)) {
                        return true;
                    }
                }
            }
            pos = default;
            up = default;
            return false;
        }

        private static bool TryFaceAt(int x, int y, bool wantBigPocket, out Vector2 pos, out Vector2 up) {
            pos = default;
            up = default;
            if (!WorldGen.InWorld(x, y, 40) || !WorldGen.SolidTile(x, y)) {
                return false;
            }
            int need = wantBigPocket ? 4 : 2;
            //四个面逐一试:面外侧 need×need 全空即算气窝
            Span<Point> normals = [new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1)];
            foreach (Point normal in normals) {
                if (!PocketClear(x + normal.X, y + normal.Y, normal, need)) {
                    continue;
                }
                pos = new Vector2(x * 16f + 8f + normal.X * 8f, y * 16f + 8f + normal.Y * 8f);
                //墙面印记指尖向上;地面/顶面印记指尖沿面横向
                up = normal.Y == 0 ? -Vector2.UnitY : Vector2.UnitX;
                return true;
            }
            return false;
        }

        private static bool PocketClear(int startX, int startY, Point normal, int size) {
            //气窝沿法线方向铺开,横向对中
            for (int depth = 0; depth < size; depth++) {
                for (int lateral = -size / 2; lateral < size - size / 2; lateral++) {
                    int x = startX + normal.X * depth + (normal.X == 0 ? lateral : 0);
                    int y = startY + normal.Y * depth + (normal.Y == 0 ? lateral : 0);
                    if (!WorldGen.InWorld(x, y, 40)) {
                        return false;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        return false;
                    }
                }
            }
            return true;
        }

        //====绘制====

        /// <summary>焦炭手印：软斑叠掌 + 五指痕，60% 带下拖抓痕；近锚叠余烬微光</summary>
        private static void DrawMark(SpriteBatch sb, HandMark mark) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f);
            Vector2 screen = mark.Pos - Main.screenPosition;
            Color soot = new Color(20, 16, 14) * 0.55f;
            Color sootSoft = new Color(20, 16, 14) * 0.28f;
            float upAngle = mark.Up.ToRotation();
            float s = mark.Size;

            //掌:三层收窄软斑(以透明度层叠近似软边)
            sb.Draw(pixel, screen, src, sootSoft, mark.Tilt, half, new Vector2(16f, 13f) * s, SpriteEffects.None, 0f);
            sb.Draw(pixel, screen, src, sootSoft, mark.Tilt, half, new Vector2(12.5f, 10f) * s, SpriteEffects.None, 0f);
            sb.Draw(pixel, screen, src, soot, mark.Tilt, half, new Vector2(9f, 7.5f) * s, SpriteEffects.None, 0f);

            //五指痕:掌缘沿 up 展开的竖条
            Vector2 side = mark.Up.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 5; i++) {
                float lateral = (i - 2) * 3.4f * s;
                float len = (6.5f - MathF.Abs(i - 2) * 1.1f) * s;
                Vector2 root = screen + side * lateral + mark.Up * 6f * s;
                sb.Draw(pixel, root + mark.Up * len * 0.5f, src, soot, upAngle + MathHelper.PiOver2 + mark.Tilt * 0.5f,
                    half, new Vector2(2.1f * s, len), SpriteEffects.None, 0f);
            }

            //下拖抓痕:指痕沿 -up 拖出的长线(它抓过又滑脱)
            for (int i = 0; i < mark.ScratchCount; i++) {
                float lateral = (i - mark.ScratchCount * 0.5f) * 3.2f * s;
                float len = mark.ScratchLen * (0.8f + (i % 3) * 0.15f) * s;
                Vector2 from = screen + side * lateral - mark.Up * 4f * s;
                sb.Draw(pixel, from - mark.Up * len * 0.5f, src, sootSoft, upAngle + MathHelper.PiOver2,
                    half, new Vector2(1.4f * s, len), SpriteEffects.None, 0f);
            }

            //近锚余烬微光:0.06 alpha 闪烁(SoftGlow 加色不遮挡)
            if (mark.NearAnchor) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    float flick = 0.5f + 0.5f * MathF.Sin((float)Main.timeForVisualEffects * 0.07f + mark.Pos.X * 0.05f);
                    Color ember = GhostHandDrawHelper.Ember with { A = 0 };
                    sb.Draw(glow, screen, null, ember * (0.06f + 0.05f * flick), 0f, glow.Size() * 0.5f, 0.5f * s, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>裂隙锚贴饰：种子折线裂缝（焦黑描边+余烬内缝闪烁）+ 缝口烟缕</summary>
        private static void DrawFissure(SpriteBatch sb, Vector2 anchor, Rectangle view) {
            if (!view.Contains((int)anchor.X, (int)anchor.Y)) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f);
            Vector2 screen = anchor - Main.screenPosition;
            int seed = anchor.GetHashCode();
            int segments = 8 + (seed & 3);
            float flicker = 0.5f + 0.5f * MathF.Sin((float)Main.timeForVisualEffects * 0.06f);

            //两条相背的折线,自锚点向外撕开
            for (int branch = -1; branch <= 1; branch += 2) {
                Vector2 cursor = screen;
                float angle = branch > 0 ? ((seed >> 3 & 15) / 15f - 0.5f) * 1.4f : MathHelper.Pi + ((seed >> 7 & 15) / 15f - 0.5f) * 1.4f;
                for (int i = 0; i < segments / 2 + 2; i++) {
                    int h = seed * 17 + i * 131 + branch * 977;
                    angle += ((h & 63) / 63f - 0.5f) * 0.9f;
                    float len = 6f + (h >> 6 & 7) * 1.4f;
                    Vector2 dir = angle.ToRotationVector2();
                    Vector2 segCenter = cursor + dir * len * 0.5f;
                    sb.Draw(pixel, segCenter, src, Color.Black * 0.85f, angle, half, new Vector2(len, 3.4f), SpriteEffects.None, 0f);
                    sb.Draw(pixel, segCenter, src, GhostHandDrawHelper.Ember * (0.5f * flicker), angle, half, new Vector2(len, 1.3f), SpriteEffects.None, 0f);
                    cursor += dir * len;
                }
            }

            //缝口烟缕:限入屏,每 90t 一缕
            if (!Main.gamePaused && Main.GameUpdateCount % 90 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(anchor + Main.rand.NextVector2Circular(8f, 4f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.25f, 0.6f),
                    GhostHandDrawHelper.Charcoal * 0.5f, Main.rand.NextFloat(0.10f, 0.15f))
                    ?.Configure(Main.rand.Next(26, 40), 0.35f);
            }
        }
    }

    /// <summary>
    /// 入场压场（§5-A）：焦黑枯手在场（含反噬身）时，400px 内的火把/篝火/蜡烛族瓦光发虚变暗，
    /// 潜壁期缓入、消散期随显形强度退场。玩家携行光无钩可拦，刻意不做
    /// </summary>
    internal sealed class GhostHandLightDread : GlobalTile
    {
        //每帧一次的实体扫描缓存(客户端视觉,非玩法状态)
        private static uint cacheFrame;
        private static GhostHandActor cachedHand;

        private static GhostHandActor ResolveHand() {
            if (Main.GameUpdateCount != cacheFrame) {
                cacheFrame = Main.GameUpdateCount;
                cachedHand = null;
                foreach (GhostHandActor hand in ActorLoader.GetActiveActors<GhostHandActor>()) {
                    cachedHand = hand;
                    break;
                }
            }
            return cachedHand;
        }

        public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b) {
            if (type != TileID.Torches && type != TileID.Campfire && type != TileID.Candles && type != TileID.PlatinumCandle) {
                return;
            }
            GhostHandActor hand = ResolveHand();
            if (hand == null) {
                return;
            }
            Vector2 tileWorld = new(i * 16f + 8f, j * 16f + 8f);
            if (Vector2.DistanceSquared(tileWorld, hand.Center) > 400f * 400f) {
                return;
            }
            //发虚摇曳:0.3±0.15;潜壁期 1→0.3 缓入,消散期随强度还光
            float dim = 0.3f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.11f);
            float weight = 1f;
            if (hand.Phase == GhostHandPhase.InWall) {
                weight = MathHelper.Clamp(hand.LocalPhaseTimer / 180f, 0f, 1f);
            }
            else if (hand.Presence == WraithPresence.Dematerializing) {
                weight = hand.PresenceStrength;
            }
            float factor = MathHelper.Lerp(1f, dim, weight);
            r *= factor;
            g *= factor;
            b *= factor;
        }
    }
}
