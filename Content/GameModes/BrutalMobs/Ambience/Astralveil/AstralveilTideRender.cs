using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Astralveil
{
    /// <summary>
    /// 「双色潮」：靛/橙两道极淡的竖向光潮沿世界 X 轴缓慢扫过地表（纯氛围，无判定）。
    /// 潮带位置世界锚定（worldX − 时间×潮速），玩家静止时潮从身边流过；
    /// 挂 EndEntityDraw（人走进光潮里，镜像 DungeonworldAmbientRender 的光柱层序），
    /// 自开自收加色批，无 RT 槽
    /// </summary>
    internal sealed class AstralveilTideRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.84</summary>
        public override float Weight => 1.84f;

        //LightBeam 256x1024 真 alpha 竖梁（内容约 195x911，长轴两端自带渐隐，
        //超尺寸纵向拉伸后屏内取其均匀中段——不犯灰度条带整条拉伸的两端硬切禁令）
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> LightBeam = null;

        private const float BeamContentW = 195f;
        private const float BeamContentH = 911f;

        /// <summary>潮带波长（相邻同色潮带的世界像素间距）</summary>
        private const float WaveLength = 5400f;
        /// <summary>潮速（世界像素/帧，缓慢优雅）</summary>
        private const float TideSpeed = 0.55f;
        /// <summary>潮带可见半宽</summary>
        private const float BandHalfWidth = 420f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = AstralveilFX.Presence;
            if (presence < 0.02f) {
                return;
            }
            //只扫地表：地下星辉瘟疫不见天光潮
            if (Main.LocalPlayer.Center.Y / 16.0 > Main.worldSurface + 14.0) {
                return;
            }
            Texture2D beam = LightBeam?.Value;
            if (beam == null || beam.IsDisposed) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            //相位对波长取模防浮点漂移；两色潮错开半波长交替经过
            float drift = Main.GameUpdateCount * TideSpeed % WaveLength;
            float alphaBase = 0.055f * presence * AstralveilFX.BossDim;
            float viewL = Main.screenPosition.X - BandHalfWidth;
            float viewR = Main.screenPosition.X + Main.screenWidth + BandHalfWidth;
            float drawH = Main.screenHeight + 520f;
            Vector2 origin = new(beam.Width * 0.5f, beam.Height * 0.5f);
            Vector2 scale = new(BandHalfWidth * 2f / BeamContentW, drawH / BeamContentH);

            for (int band = 0; band < 2; band++) {
                Color tint = band == 0 ? AstralveilFX.Indigo : AstralveilFX.Orange;
                float phase0 = drift + band * WaveLength * 0.5f;
                int nMin = (int)MathF.Floor((viewL - phase0) / WaveLength);
                int nMax = (int)MathF.Ceiling((viewR - phase0) / WaveLength);
                for (int n = nMin; n <= nMax; n++) {
                    float centerX = phase0 + n * WaveLength;
                    if (centerX < viewL || centerX > viewR) {
                        continue;
                    }
                    //潮心呼吸：极缓强弱起伏，两色潮错相（加色批源因子=SourceAlpha，
                    //Color * a 让 A 随强度走，不可用 A=0 技法）
                    float breathe = 0.8f + 0.2f * MathF.Sin(
                        centerX * 0.0011f + Main.GameUpdateCount * 0.006f + band * 2.1f);
                    Vector2 pos = new(centerX - Main.screenPosition.X, Main.screenHeight * 0.5f);
                    spriteBatch.Draw(beam, pos, null, tint * (alphaBase * breathe), 0f,
                        origin, scale, SpriteEffects.None, 0f);
                }
            }
            spriteBatch.End();
        }
    }
}
