using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Rendering
{
    /// <summary>
    /// 石巨人碎屑工厂：全部借原版素材——蜥蜴砖 Tiles_226 格、石巨人自身 gore 360~371、
    /// 本体贴图沿蚀线切片、Lihzahrd 尘与烟 gore。只在客户端调用，服务端无视觉
    /// </summary>
    internal static class GolemRubble
    {
        //原版 NPC.HitEffect 245/249/247 分支的 gore 分组
        internal static readonly int[] SmallChunks = [368, 370];
        internal static readonly int[] ShoulderGores = [362, 363];
        internal static readonly int[] TorsoGores = [360, 361, 364];
        internal static readonly int[] LegGores = [366, 367];
        internal static readonly int[] HeadGores = [365, 363, 362];
        internal static readonly int[] FistGores = [369, 371, 370];

        //蜥蜴砖内格：(18,18)/(54,18) 素砖，(36,18) 人面刻纹
        private static readonly Point[] BrickCells = [new(18, 18), new(36, 18), new(54, 18)];

        //本体 alpha 掩码：死亡进入时缓存一次，切片全透明则跳过
        private static bool[] bodyMask;
        private static Rectangle bodyMaskFrame;
        private static int bodyMaskType = -1;

        /// <summary>着色器 CrumblePS 的吞没线（帧内归一 0~1）：碎屑从像素真正消失的高度掉出</summary>
        internal static float ErodeFraction(float crumble) => MathHelper.Clamp(crumble * 1.18f - 0.09f, 0f, 1f);

        #region 本体掩码
        internal static void CacheBodyMask(NPC npc) {
            if (Main.dedServ) {
                return;
            }
            try {
                Main.instance.LoadNPC(npc.type);
                Texture2D body = TextureAssets.Npc[npc.type].Value;
                Rectangle frame = npc.frame;
                if (frame.Width <= 0 || frame.Height <= 0) {
                    frame = new Rectangle(0, 0, body.Width, Math.Max(body.Height / Math.Max(Main.npcFrameCount[npc.type], 1), 1));
                }
                frame = Rectangle.Intersect(frame, body.Bounds);
                Color[] pixels = new Color[frame.Width * frame.Height];
                body.GetData(0, frame, pixels, 0, pixels.Length);
                bool[] mask = new bool[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) {
                    mask[i] = pixels[i].A > 40;
                }
                bodyMask = mask;
                bodyMaskFrame = frame;
                bodyMaskType = npc.type;
            } catch (Exception ex) {
                //掩码只是过滤，拿不到就退化为不过滤
                CWRMod.Instance.Logger.Warn($"[GolemRubble] body mask cache failed: {ex.Message}");
                ReleaseBodyMask();
            }
        }

        internal static void ReleaseBodyMask() {
            bodyMask = null;
            bodyMaskFrame = default;
            bodyMaskType = -1;
        }

        /// <summary>切片矩形（贴图坐标）内不透明像素占比；无掩码时视为全不透明</summary>
        private static float OpaqueRatio(Rectangle src) {
            if (bodyMask == null) {
                return 1f;
            }
            int hit = 0;
            int total = 0;
            for (int y = src.Y; y < src.Bottom; y++) {
                int row = y - bodyMaskFrame.Y;
                if (row < 0 || row >= bodyMaskFrame.Height) {
                    continue;
                }
                for (int x = src.X; x < src.Right; x++) {
                    int col = x - bodyMaskFrame.X;
                    if (col < 0 || col >= bodyMaskFrame.Width) {
                        continue;
                    }
                    total++;
                    if (bodyMask[row * bodyMaskFrame.Width + col]) {
                        hit++;
                    }
                }
            }
            return total == 0 ? 0f : hit / (float)total;
        }
        #endregion

        #region 生成
        /// <summary>一块蜥蜴砖：quadrant 为 8×8 小碎块，否则整格 16×16</summary>
        internal static PRT_TempleRubble SpawnBrick(Vector2 pos, Vector2 vel, float heat, bool quadrant, int rest = 120, float scale = 1f) {
            Main.instance.LoadTiles(TileID.LihzahrdBrick);
            Texture2D brick = TextureAssets.Tile[TileID.LihzahrdBrick].Value;
            Point cell = BrickCells[Main.rand.Next(BrickCells.Length)];
            Rectangle src = new(cell.X, cell.Y, 16, 16);
            if (quadrant) {
                src = new Rectangle(cell.X + Main.rand.Next(2) * 8, cell.Y + Main.rand.Next(2) * 8, 8, 8);
            }
            return PRTLoader.NewParticle<PRT_TempleRubble>(pos, vel, Color.White, scale)
                .Configure(brick, src, heat, rest);
        }

        /// <summary>一块石巨人自身 gore（360~371）</summary>
        internal static PRT_TempleRubble SpawnGore(int goreId, Vector2 pos, Vector2 vel, float heat, int rest = 150, float scale = 1f) {
            Main.instance.LoadGore(goreId);
            Texture2D tex = TextureAssets.Gore[goreId].Value;
            return PRTLoader.NewParticle<PRT_TempleRubble>(pos, vel, Color.White, scale)
                .Configure(tex, tex.Bounds, heat, rest);
        }

        /// <summary>一组 gore 从矩形区域内抛出</summary>
        internal static void SpawnGores(int[] ids, Rectangle area, Vector2 baseVel, float spread, float heat, float scale = 1f, int rest = 150) {
            foreach (int id in ids) {
                Vector2 pos = new(area.X + Main.rand.NextFloat(area.Width), area.Y + Main.rand.NextFloat(area.Height));
                Vector2 vel = baseVel + new Vector2(Main.rand.NextFloat(-spread, spread), Main.rand.NextFloat(-spread, spread * 0.4f));
                SpawnGore(id, pos, vel, heat, rest, scale);
            }
        }

        /// <summary>
        /// 沿蚀线切本体：从帧内 [fromFrac, toFrac] 这段刚被吞掉的区域取子矩形当碎块，
        /// 全透明的切片跳过，最多重试三次
        /// </summary>
        internal static void SpawnBodySlices(NPC npc, float fromFrac, float toFrac, int count, float heat, int rest = 130) {
            Main.instance.LoadNPC(npc.type);
            Texture2D body = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = bodyMaskType == npc.type && bodyMask != null ? bodyMaskFrame : npc.frame;
            if (frame.Width <= 0 || frame.Height <= 0) {
                return;
            }

            int yTop = frame.Y + (int)(frame.Height * MathHelper.Clamp(fromFrac, 0f, 1f));
            int yBottom = frame.Y + (int)(frame.Height * MathHelper.Clamp(toFrac, 0f, 1f));

            for (int i = 0; i < count; i++) {
                for (int attempt = 0; attempt < 3; attempt++) {
                    int w = Main.rand.Next(12, 19);
                    int h = Main.rand.Next(10, 17);
                    int x = frame.X + Main.rand.Next(Math.Max(frame.Width - w, 1));
                    int y = Math.Clamp(Main.rand.Next(yTop - h, Math.Max(yBottom - h + 1, yTop - h + 1)), frame.Y, frame.Bottom - h);
                    Rectangle src = new(x, y, w, h);
                    if (OpaqueRatio(src) < 0.35f) {
                        continue;
                    }
                    //切片在世界中的位置：帧中心对齐 npc.Center
                    Vector2 inFrame = src.Center.ToVector2() - frame.Center.ToVector2();
                    Vector2 pos = npc.Center + inFrame * npc.scale;
                    Vector2 vel = new(Main.rand.NextFloat(-2.2f, 2.2f), Main.rand.NextFloat(-1.6f, 0.8f));
                    PRTLoader.NewParticle<PRT_TempleRubble>(pos, vel, Color.White, npc.scale)
                        .Configure(body, src, heat, rest + Main.rand.Next(-20, 30));
                    break;
                }
            }
        }

        /// <summary>Lihzahrd 卡其尘</summary>
        internal static void SpawnDust(Vector2 pos, int count, float spread, Vector2 baseVel, float scale = 1.1f) {
            for (int i = 0; i < count; i++) {
                Dust dust = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(spread, spread), DustID.Lihzahrd,
                    baseVel + Main.rand.NextVector2Circular(0.8f, 0.8f), 60, default, scale * Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = false;
            }
        }

        /// <summary>灰色真 alpha 烟（原版烟 gore 61~63），能压暗背景</summary>
        internal static void SpawnSmoke(IEntitySource source, Vector2 pos, int count, Vector2 baseVel, float scale = 1f) {
            for (int i = 0; i < count; i++) {
                Gore gore = Gore.NewGoreDirect(source, pos + Main.rand.NextVector2Circular(10f, 6f), default, Main.rand.Next(61, 64), scale);
                gore.velocity = baseVel + Main.rand.NextVector2Circular(0.5f, 0.3f);
            }
        }
        #endregion
    }
}
