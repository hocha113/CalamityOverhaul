using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Liquid;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Renders
{
    /// <summary>
    /// 旧网涂黑修补。原版 <c>Main.DrawBlack</c> 非 force 只处理两段行域：
    /// 屏顶在世界上半（&lt;maxTilesY/2）时只涂 worldSurface+1 以上，
    /// 屏顶进下半后只涂 UnderworldLayer(=maxTilesY-200) 以下。旧网 600 行世界
    /// （半高 300 / UnderworldLayer 400 / worldSurface 430）的地板带与浅层地下
    /// 恰好落进 300~400 行的涂黑死窗，无光瓦片被瓦片渲染器剔除
    /// （TileDrawing 光照全零跳绘）后，天幕直接透出。<br/>
    /// 这里在旧网内把 DrawBlack 整体替换为无行域钳制的等价实现。不走 force=true
    /// 转发：force 路径对 UnderworldLayer 以下改用 0.2 地狱亮度阈值，
    /// 旧网深层的昏暗照明会被硬切出成片黑方块边
    /// </summary>
    internal sealed class OldNetDrawBlackHook : ModSystem
    {
        private delegate void OrigDrawBlack(Main self, bool force);
        private delegate void DrawBlackHook(OrigDrawBlack orig, Main self, bool force);

        public override void Load() {
            if (Main.dedServ) {
                return;
            }
            //MonoModHooks 随模组卸载自动摘钩，无需手动移除
            MethodInfo drawBlack = typeof(Main).GetMethod("DrawBlack",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (drawBlack == null) {
                Mod.Logger.Warn("[OldNet] Main.DrawBlack 反射未命中，地下涂黑死窗修补未挂载");
                return;
            }
            MonoModHooks.Add(drawBlack, new DrawBlackHook(OnDrawBlack));
        }

        private static void OnDrawBlack(OrigDrawBlack orig, Main self, bool force) {
            if (!OldNetWorld.Active || Main.gameMenu) {
                orig(self, force);
                return;
            }
            DrawBlackFullRange();
        }

        //镜像 Main.DrawBlack 主体（Main.cs L55783），刻意差异只有两处：
        //①去掉 worldSurface/UnderworldLayer 的行域钳制（死窗根源）
        //②不做地狱行的 0.2 阈值抬升，整图统一 tileColor 阈值
        private static void DrawBlackFullRange() {
            if (Main.shimmerAlpha == 1f) {
                return;
            }
            Vector2 off = Main.drawToScreen ? Vector2.Zero
                : new Vector2(Main.offScreenRange, Main.offScreenRange);
            int avg = (Main.tileColor.R + Main.tileColor.G + Main.tileColor.B) / 3;
            float threshold = avg * 0.4f / 255f;
            if (Lighting.Mode == LightMode.Retro) {
                threshold = Math.Max((Main.tileColor.R - 55) / 255f, 0f);
            }
            else if (Lighting.Mode == LightMode.Trippy) {
                threshold = Math.Max((avg - 55) / 255f, 0f);
            }

            Point overdraw = Main.GetScreenOverdrawOffset();
            var origin = new Point(-Main.offScreenRange / 16 + overdraw.X,
                -Main.offScreenRange / 16 + overdraw.Y);
            int x0 = (int)((Main.screenPosition.X - off.X) / 16f - 1f) + origin.X;
            int x1 = (int)((Main.screenPosition.X + Main.screenWidth + off.X) / 16f) + 2 - origin.X;
            int y0 = (int)((Main.screenPosition.Y - off.Y) / 16f - 1f) + origin.Y;
            int y1 = (int)((Main.screenPosition.Y + Main.screenHeight + off.Y) / 16f) + 5 - origin.Y;
            x0 = Math.Clamp(x0, 0, Main.maxTilesX);
            x1 = Math.Clamp(x1, 0, Main.maxTilesX);
            y0 = Math.Clamp(y0, 0, Main.maxTilesY);
            y1 = Math.Clamp(y1, 0, Main.maxTilesY);

            Texture2D black = TextureAssets.BlackTile.Value;
            bool showInvisible = Main.ShouldShowInvisibleWalls();
            for (int y = y0; y < y1; y++) {
                for (int x = x0; x < x1; x++) {
                    int runStart = x;
                    for (; x < x1; x++) {
                        Tile tile = Main.tile[x, y];
                        float bright = (float)Math.Floor(Lighting.Brightness(x, y) * 255f) / 255f;
                        byte liquid = tile.LiquidAmount;
                        bool darkEnough = bright <= threshold
                            && (liquid < 250 || WorldGen.SolidTile(tile)
                                || (liquid >= 200 && bright == 0f));
                        bool blockOpaque = tile.HasTile && Main.tileBlockLight[tile.TileType]
                            && (!tile.IsTileInvisible || showInvisible);
                        bool wallOpaque = !WallID.Sets.Transparent[tile.WallType]
                            && (!tile.IsWallInvisible || showInvisible);
                        //满水无墙格让位液体渲染（与原版一致，地表以上不让）
                        if (!darkEnough || (!wallOpaque && !blockOpaque)
                            || (!Main.drawToScreen && LiquidRenderer.Instance.HasFullWater(x, y)
                                && tile.WallType == WallID.None && !tile.IsHalfBlock
                                && y > Main.worldSurface)) {
                            break;
                        }
                    }
                    if (x - runStart > 0) {
                        Main.spriteBatch.Draw(black,
                            new Vector2(runStart << 4, y << 4) - Main.screenPosition + off,
                            new Rectangle(0, 0, x - runStart << 4, 16), Color.Black);
                    }
                }
            }
        }
    }
}
