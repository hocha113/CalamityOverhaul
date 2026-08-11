using CalamityOverhaul.Content.HackTimes.PvP.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 地图熄灭的防守方本机渲染钩子。两条路各管一半：<br/>
    /// · 常规界面：整层禁用 "Vanilla: Map / Minimap"（小地图、覆盖式大地图、
    ///   其上的队友头像与图钉一并熄灭）——只翻本帧的图层 Active 位，
    ///   图层表每帧由 tML 重建，效果一到期自动复原；<br/>
    /// · 全屏地图：它不走界面图层，在 <c>PostDrawFullscreenMap</c> 里糊整幅雪花
    ///   （全屏地图本来就盖住战场，这里糊满不违反"不遮挡角色与弹幕"红线），
    ///   并顺手清掉地图悬停文本，防止坐标信息从 tooltip 漏出去。<br/>
    /// 判定只读本机帐本（<see cref="PvPDefenderLocal"/>），远端与服务端天然失活
    /// </summary>
    internal sealed class MapBlackoutRenderer : ModSystem
    {
        private const string MapLayerName = "Vanilla: Map / Minimap";

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (!PvPDefenderLocal.HasEffect<MapBlackout>()) {
                return;
            }
            for (int i = 0; i < layers.Count; i++) {
                if (layers[i].Name == MapLayerName) {
                    layers[i].Active = false;
                    return;
                }
            }
        }

        public override void PostDrawFullscreenMap(ref string mouseText) {
            PlayerHackEffect effect = PvPDefenderLocal.FindEffect<MapBlackout>();
            if (effect == null) {
                return;
            }
            //全屏地图批是原生屏幕空间（无 UIScale 矩阵），按真实分辨率封顶覆盖
            int w = Math.Max(Main.screenWidth, PlayerInput.RealScreenWidth) + 8;
            int h = Math.Max(Main.screenHeight, PlayerInput.RealScreenHeight) + 8;
            float seed = (effect.ActivationId % 1000) * 0.377f;
            DrawSnow(Main.spriteBatch, new Rectangle(-4, -4, w, h), seed, 1f, 20);
            mouseText = string.Empty;
        }

        /// <summary>
        /// 雪花面板：实底 + 噪声细胞 + 扫描亮线 + 1px 边框 + 失联角标。
        /// 亮色语汇，不做 magic-pixel 暗羽化（UI 律）。cell 越大越省——
        /// 小地图用 8，全屏用 20
        /// </summary>
        internal static void DrawSnow(SpriteBatch sb, Rectangle rect, float seed,
            float alpha, int cell) {
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null || rect.Width <= 0 || rect.Height <= 0) {
                return;
            }

            //实底：断讯的屏是深色的
            sb.Draw(pixel, rect, HackTheme.SrcPixel,
                HackTheme.BgPanel * (0.95f * alpha));

            //噪声细胞：~20Hz 重掷，读作雪花而不是平滑流动
            float step = MathF.Floor(Main.GameUpdateCount / 3f) + seed;
            int columns = rect.Width / cell + 1;
            int rows = rect.Height / cell + 1;
            for (int gy = 0; gy < rows; gy++) {
                for (int gx = 0; gx < columns; gx++) {
                    float h1 = Hash(gx * 7.31f + gy * 3.77f + step * 13.7f);
                    if (h1 < 0.46f) {
                        continue;
                    }
                    float brightness = 0.10f + Frac(h1 * 7.77f) * 0.72f;
                    //少量敌对红细胞点缀，其余灰白雪点
                    Color body = Frac(h1 * 3.13f) < 0.08f
                        ? PvPTheme.Hostile * (brightness + 0.15f)
                        : new Color(brightness, brightness, brightness);
                    int px = rect.X + gx * cell;
                    int py = rect.Y + gy * cell;
                    int cw = Math.Min(cell - 1, rect.Right - px);
                    int ch = Math.Min(cell - 1, rect.Bottom - py);
                    if (cw <= 0 || ch <= 0) {
                        continue;
                    }
                    sb.Draw(pixel, new Rectangle(px, py, cw, ch),
                        HackTheme.SrcPixel, body * (alpha * 0.5f));
                }
            }

            //一道下行扫描亮线（亮色 additive 感，合法发光）
            int sweepY = rect.Y + (int)(Main.GameUpdateCount * 2 % rect.Height);
            sb.Draw(pixel, new Rectangle(rect.X, sweepY, rect.Width, 2),
                HackTheme.SrcPixel, PvPTheme.HostileAlt * (alpha * 0.30f));
            sb.Draw(pixel, new Rectangle(rect.X, sweepY + 1, rect.Width, 1),
                HackTheme.SrcPixel, Color.White * (alpha * 0.16f));

            //1px 边框（实底 + 细边，不做假投影）
            DrawBorder(sb, pixel, rect, PvPTheme.HostileBorder * alpha);

            //中央失联角标
            HackTheme.DrawBadge(sb,
                new Vector2(rect.Center.X - 52f, rect.Center.Y - 9f),
                PvPHudText.SignalLost.Value, PvPTheme.Hostile, alpha, 0.7f);
        }

        private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect,
            Color color) {
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                HackTheme.SrcPixel, color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                HackTheme.SrcPixel, color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height),
                HackTheme.SrcPixel, color);
            sb.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                HackTheme.SrcPixel, color);
        }

        private static float Hash(float p) {
            p = MathF.Abs(p * 0.1031f % 1f);
            p *= p + 33.33f;
            p *= p + p;
            return MathF.Abs(p % 1f);
        }

        private static float Frac(float v) => v - MathF.Floor(v);
    }
}
