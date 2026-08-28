using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Kiame.Backgrounds;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Water
{
    /// <summary>
    /// 鬼雨洼地黑水的屏幕空间招牌层：逐列扫可见水面顶行上传条带纹理，
    /// KiameWater.fx 在水下绕各列水面线垂直镜像拷屏（废屋/伞鬼/玩家一并入镜）、
    /// 压墨渊纵深、点亮面线与碎波、按世界锚定格子砸溅环、给涉水实体画接触涟漪，
    /// 雷闪与风暴脉动由 <see cref="KiameSky"/> / <see cref="KiameAmbience"/> 同源驱动。<br/>
    /// 管线形制镜像 OniUmbrellaPuddleRender（拷屏→效果合成回写）；
    /// 着色器/RT 不可用时静默跳过，兜底是压光下的原版水，身份不塌
    /// </summary>
    internal sealed class KiameWaterRender : RenderHandle, ICWRLoader
    {
        public override float Weight => 1.42f;

        //条带几何：256 texel x 每 texel 16px（一格一列），覆盖 4096px ≥ 最大缩放下整屏
        private const int StripLen = 256;
        private const float StripSpanPx = StripLen * 16f;
        /// <summary>伪透视压缩：水下每深 1px，镜像源上移这么多 px</summary>
        private const float ReflScale = 1.55f;

        //湿墨色板，与鬼雨体系一致
        private static readonly Vector3 InkShallow = new(0.10f, 0.13f, 0.15f);
        private static readonly Vector3 InkDeep = new(0.035f, 0.05f, 0.06f);
        private static readonly Vector3 Sheen = new(120f / 255f, 150f / 255f, 146f / 255f);
        private static readonly Vector3 FlashPale = new(143f / 255f, 161f / 255f, 166f / 255f);

        private static Texture2D stripTex;
        private static readonly Color[] stripData = new Color[StripLen];
        private static readonly Vector4[] feet = new Vector4[8];
        //外部涉水者（伞鬼等）：逐帧报告制，画完即清
        private static readonly List<Vector4> reportedWaders = new(16);

        void ICWRLoader.UnLoadData() {
            stripTex?.Dispose();
            stripTex = null;
            reportedWaders.Clear();
        }

        /// <summary>
        /// 涉水报告：非玩家实体（伞鬼等）每帧把足点报进来，本帧水面层为它画接触涟漪。
        /// 世界坐标足点 + 涟漪半径（世界px）+ 强度 0~1
        /// </summary>
        internal static void ReportWader(Vector2 feetWorld, float radiusPx, float strength) {
            if (reportedWaders.Count < 24) {
                reportedWaders.Add(new Vector4(feetWorld.X, feetWorld.Y, strength, radiusPx));
            }
        }

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            //报告制清账：无论本帧画不画，外部报告都只活一帧
            List<Vector4> waders = reportedWaders;
            if (Main.gameMenu || !KiameWorld.Active) {
                waders.Clear();
                return;
            }

            //技术门禁：RT 不可用时静默跳过，压光下的原版水仍在
            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                waders.Clear();
                return;
            }
            Effect fx = EffectLoader.KiameWater?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null || noise.IsDisposed) {
                waders.Clear();
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed
                || Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                waders.Clear();
                return;
            }
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                waders.Clear();
                return;
            }

            //屏幕↔世界映射：拷屏是经 GameViewMatrix 缩放后的画面，取逆阵还原
            Matrix inv = Matrix.Invert(Main.GameViewMatrix.TransformationMatrix);
            Vector2 topLeftWorld = Vector2.Transform(Vector2.Zero, inv) + Main.screenPosition;
            float pxToWorld = Vector2.Transform(Vector2.UnitX, inv).X - Vector2.Transform(Vector2.Zero, inv).X;
            if (pxToWorld <= 0f || !float.IsFinite(pxToWorld)) {
                waders.Clear();
                return;
            }

            int stripLeftTile = (int)MathF.Floor(topLeftWorld.X / 16f) - 8;
            float stripTopPx = topLeftWorld.Y - 160f;
            float stripSpanYPx = Main.screenHeight * pxToWorld + 480f;

            if (!BuildStrip(stripLeftTile, stripTopPx, stripSpanYPx, topLeftWorld, pxToWorld)) {
                //屏内无水：这帧不必开合成
                waders.Clear();
                return;
            }
            CollectFeet(waders);
            waders.Clear();

            //条带上载：绑定中的纹理不能 SetData，先解绑（Kiyume 既有教训）
            if (stripTex == null || stripTex.IsDisposed) {
                stripTex = new Texture2D(graphicsDevice, StripLen, 1, false, SurfaceFormat.Color);
            }
            graphicsDevice.Textures[2] = null;
            stripTex.SetData(stripData);

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //拷屏到交换缓冲
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            //水面合成回写主屏
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            SetParams(fx, topLeftWorld, pxToWorld, stripLeftTile * 16f, stripTopPx, stripSpanYPx);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            graphicsDevice.Textures[2] = stripTex;
            graphicsDevice.SamplerStates[2] = SamplerState.PointClamp;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            fx.CurrentTechnique = fx.Techniques["TechWater"];
            fx.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //还原 RT 绑定
            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        /// <summary>
        /// 逐列扫可见洼地的水面顶行与水深，编码进条带（R=有水 G/B=面高16位 A=深px/2）。
        /// 返回屏内是否有水
        /// </summary>
        private static bool BuildStrip(int stripLeftTile, float stripTopPx, float stripSpanYPx,
            Vector2 topLeftWorld, float pxToWorld) {
            bool anyWater = false;
            int topTile = Math.Max((int)(stripTopPx / 16f) - 1, 1);
            int bottomTile = Math.Min(
                (int)((topLeftWorld.Y + Main.screenHeight * pxToWorld) / 16f) + 12,
                Main.maxTilesY - 2);

            for (int i = 0; i < StripLen; i++) {
                stripData[i] = default;
                int tx = stripLeftTile + i;
                if (tx < 1 || tx >= Main.maxTilesX - 1) {
                    continue;
                }
                bool prevWater = CellWater(tx, topTile - 1);
                for (int ty = topTile; ty <= bottomTile; ty++) {
                    bool water = CellWater(tx, ty);
                    if (water && !prevWater) {
                        //找到该列第一处水面，向下量水深
                        int bottom = ty;
                        while (bottom < bottomTile + 24 && CellWater(tx, bottom + 1)) {
                            bottom++;
                        }
                        Tile surf = Framing.GetTileSafely(tx, ty);
                        float surfaceWorldY = ty * 16f + (1f - surf.LiquidAmount / 255f) * 16f;
                        float norm = MathHelper.Clamp((surfaceWorldY - stripTopPx) / stripSpanYPx, 0f, 1f);
                        int v16 = (int)(norm * 65535f);
                        int depthPx = Math.Clamp((bottom - ty + 1) * 16, 0, 510);
                        stripData[i] = new Color(255, (v16 >> 8) & 255, v16 & 255, depthPx / 2);
                        anyWater = true;
                        break;
                    }
                    prevWater = water;
                }
            }
            return anyWater;
        }

        private static bool CellWater(int x, int y) {
            Tile tile = Framing.GetTileSafely(x, y);
            if (tile.LiquidAmount <= 32 || tile.LiquidType != LiquidID.Water) {
                return false;
            }
            //实心格里的液体读作墙内渗水，不算面
            return !(tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]);
        }

        /// <summary>收集涉水实体足点：玩家自动收集，外部实体走 <see cref="ReportWader"/> 报告</summary>
        private static void CollectFeet(List<Vector4> waders) {
            int slot = 0;
            for (int i = 0; i < Main.maxPlayers && slot < feet.Length; i++) {
                Player player = Main.player[i];
                if (player?.active != true || player.dead) {
                    continue;
                }
                Vector2 foot = player.Bottom;
                if (!WadingAt(foot)) {
                    continue;
                }
                float strength = 0.55f + MathHelper.Clamp(MathF.Abs(player.velocity.X) / 5f, 0f, 1f) * 0.45f;
                feet[slot++] = new Vector4(foot.X, foot.Y, strength, player.width * 1.6f);
            }
            for (int i = 0; i < waders.Count && slot < feet.Length; i++) {
                Vector4 wader = waders[i];
                if (WadingAt(new Vector2(wader.X, wader.Y))) {
                    feet[slot++] = wader;
                }
            }
            for (; slot < feet.Length; slot++) {
                feet[slot] = new Vector4(0f, 0f, 0f, 1f);
            }
        }

        private static bool WadingAt(Vector2 worldPos) {
            int tx = (int)(worldPos.X / 16f);
            int ty = (int)((worldPos.Y + 2f) / 16f);
            return CellWater(tx, ty) || CellWater(tx, ty - 1);
        }

        /// <summary>全参数上载（uniform 是设备全局状态，每个调用点必须全参数重设）</summary>
        private static void SetParams(Effect fx, Vector2 topLeftWorld, float pxToWorld,
            float stripLeftPx, float stripTopPx, float stripSpanYPx) {
            fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            fx.Parameters["uScreenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            fx.Parameters["uTopLeftWorld"]?.SetValue(topLeftWorld);
            fx.Parameters["uPxToWorld"]?.SetValue(pxToWorld);
            fx.Parameters["uStripLeftPx"]?.SetValue(stripLeftPx);
            fx.Parameters["uStripTopPx"]?.SetValue(stripTopPx);
            fx.Parameters["uStripSpanYPx"]?.SetValue(stripSpanYPx);
            fx.Parameters["uReflScale"]?.SetValue(ReflScale);
            fx.Parameters["uFlash"]?.SetValue(KiameSky.FlashStrength);
            fx.Parameters["uGust"]?.SetValue(KiameAmbience.StormPulse);
            fx.Parameters["uRainDensity"]?.SetValue(KiameAmbience.RainDensity01);
            fx.Parameters["uAlpha"]?.SetValue(KiameAmbience.Presence);
            fx.Parameters["uInkShallow"]?.SetValue(InkShallow);
            fx.Parameters["uInkDeep"]?.SetValue(InkDeep);
            fx.Parameters["uSheen"]?.SetValue(Sheen);
            fx.Parameters["uFlashPale"]?.SetValue(FlashPale);
            fx.Parameters["uFeet"]?.SetValue(feet);
        }
    }
}
