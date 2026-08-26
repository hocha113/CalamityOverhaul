using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Hollowdeep
{
    /// <summary>
    /// 「暗涌」屏边黑雾层：全黑久待时黑雾自四缘向内渐拢，纯压迫演出。
    /// 不修改光照引擎、不做全屏遮挡（屏心永远留空，单团透明度有顶）。
    /// Fog 是真 alpha 贴图，AlphaBlend 里能画出真正的暗形（黑底加色画不出暗，见 VFX.md 暗层陷阱）
    /// </summary>
    internal sealed class HollowdeepVeilRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.73</summary>
        public override float Weight => 1.73f;

        //16 团黑雾的周缘锚位（0~1 归一化屏幕坐标 + 向内法线）
        private static readonly Vector2[] anchors;
        private static readonly Vector2[] normals;

        static HollowdeepVeilRender() {
            anchors = new Vector2[16];
            normals = new Vector2[16];
            int idx = 0;
            for (int i = 0; i < 5; i++, idx++) {
                anchors[idx] = new Vector2((i + 0.5f) / 5f, 0f);
                normals[idx] = Vector2.UnitY;
            }
            for (int i = 0; i < 5; i++, idx++) {
                anchors[idx] = new Vector2((i + 0.5f) / 5f, 1f);
                normals[idx] = -Vector2.UnitY;
            }
            for (int i = 0; i < 3; i++, idx++) {
                anchors[idx] = new Vector2(0f, (i + 0.5f) / 3f);
                normals[idx] = Vector2.UnitX;
            }
            for (int i = 0; i < 3; i++, idx++) {
                anchors[idx] = new Vector2(1f, (i + 0.5f) / 3f);
                normals[idx] = -Vector2.UnitX;
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float veil = HollowdeepAmbience.DarkVeil;
            if (veil < 0.02f) {
                return;
            }
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null || fog.IsDisposed) {
                return;
            }

            int vpW = graphicsDevice.Viewport.Width;
            int vpH = graphicsDevice.Viewport.Height;
            Vector2 origin = fog.Size() / 2f;
            float time = Main.GlobalTimeWrappedHourly;
            //随强度自缘外向内爬入（起始大半悬在屏外）
            float creep = MathHelper.Lerp(-70f, 150f, veil);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            for (int i = 0; i < anchors.Length; i++) {
                float hash = i * 0.618034f % 1f;
                Vector2 pos = new Vector2(anchors[i].X * vpW, anchors[i].Y * vpH)
                    + normals[i] * creep
                    + new Vector2(MathF.Sin(time * 0.31f + hash * 9.4f),
                        MathF.Cos(time * 0.26f + hash * 6.1f)) * 18f;
                float breathe = 0.92f + 0.1f * MathF.Sin(time * 0.5f + hash * 12.7f);
                float scale = (1.9f + 0.9f * hash) * breathe;
                float alpha = veil * (0.16f + 0.1f * hash);
                spriteBatch.Draw(fog, pos, null, new Color(8, 8, 12) * alpha,
                    hash * MathHelper.TwoPi + time * (0.05f + 0.04f * hash), origin, scale,
                    i % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
            }
            spriteBatch.End();
        }
    }
}
