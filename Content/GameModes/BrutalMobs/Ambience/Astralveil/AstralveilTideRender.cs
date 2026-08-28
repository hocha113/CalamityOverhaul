using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Astralveil
{
    /// <summary>
    /// 「双色潮」星辉尘幕：靛/橙两幕极淡的星尘雾带沿世界 X 轴缓慢扫过地表（纯氛围，无判定）。
    /// 幕带位置世界锚定（worldX − 时间×潮速），玩家静止时尘幕从身边流过；
    /// 幕体用 Fog 真 alpha 走 AlphaBlend 并乘所在处环境光（尘雾的本分是遮挡，加色压不出暗），
    /// 只以少量加色光缕点缀尘中星辉，不再全屏扫光；
    /// 挂 EndEntityDraw（人走进尘幕里），自开自收批，无 RT 槽
    /// </summary>
    internal sealed class AstralveilTideRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.84</summary>
        public override float Weight => 1.84f;

        //Fog 256 单帧烟羽（白 RGB+真 alpha），AlphaBlend 直接染色，暗色尘体合法
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Fog = null;

        /// <summary>潮带波长（相邻同色潮带的世界像素间距）</summary>
        private const float WaveLength = 5400f;
        /// <summary>潮速（世界像素/帧，缓慢优雅）</summary>
        private const float TideSpeed = 0.55f;
        /// <summary>潮带可见半宽</summary>
        private const float BandHalfWidth = 420f;
        /// <summary>每幕尘团数（纵向铺满屏高）</summary>
        private const int PlumesPerBand = 7;
        /// <summary>每幕光缕数（点缀量级，禁再回全屏扫光）</summary>
        private const int WispsPerBand = 2;

        /// <summary>靛幕尘体暗色（真 alpha 暗层专用）</summary>
        private static readonly Color DustIndigo = new(58, 50, 122);
        /// <summary>橙幕尘体暗色（真 alpha 暗层专用）</summary>
        private static readonly Color DustOrange = new(128, 82, 40);

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
            Texture2D fog = Fog?.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            if (fog == null || fog.IsDisposed || glow == null) {
                return;
            }

            //相位对波长取模防浮点漂移；两色幕错开半波长交替经过
            float drift = Main.GameUpdateCount * TideSpeed % WaveLength;
            float strength = presence * AstralveilFX.BossDim;
            float viewL = Main.screenPosition.X - BandHalfWidth;
            float viewR = Main.screenPosition.X + Main.screenWidth + BandHalfWidth;
            float drawH = Main.screenHeight + 520f;
            Vector2 fogOrig = fog.Size() * 0.5f;
            Vector2 glowOrig = glow.Size() * 0.5f;

            //第一批：真 alpha 尘雾幕体（AlphaBlend 乘环境光，黑暗处沉没）
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            for (int band = 0; band < 2; band++) {
                float phase0 = drift + band * WaveLength * 0.5f;
                int nMin = (int)MathF.Floor((viewL - phase0) / WaveLength);
                int nMax = (int)MathF.Ceiling((viewR - phase0) / WaveLength);
                for (int n = nMin; n <= nMax; n++) {
                    float centerX = phase0 + n * WaveLength;
                    if (centerX < viewL || centerX > viewR) {
                        continue;
                    }
                    DrawDustColumn(spriteBatch, fog, fogOrig, band, n, centerX, drawH, strength);
                }
            }
            spriteBatch.End();

            //第二批：加色光缕点缀（尘中透出的星辉；加色批源因子=SourceAlpha，
            //Color * a 让 A 随强度走，不可用 A=0 技法）
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            for (int band = 0; band < 2; band++) {
                float phase0 = drift + band * WaveLength * 0.5f;
                int nMin = (int)MathF.Floor((viewL - phase0) / WaveLength);
                int nMax = (int)MathF.Ceiling((viewR - phase0) / WaveLength);
                for (int n = nMin; n <= nMax; n++) {
                    float centerX = phase0 + n * WaveLength;
                    if (centerX < viewL || centerX > viewR) {
                        continue;
                    }
                    DrawWisps(spriteBatch, glow, glowOrig, band, n, centerX, drawH, strength);
                }
            }
            spriteBatch.End();
        }

        /// <summary>潮心呼吸：极缓强弱起伏，两色幕错相</summary>
        private static float Breathe(int band, float centerX)
            => 0.8f + 0.2f * MathF.Sin(centerX * 0.0011f + Main.GameUpdateCount * 0.006f + band * 2.1f);

        /// <summary>一幕尘柱：纵向错落的暗色尘团缓旋缓浮，亮度乘所在处环境光（保 0.3 底，黑暗处近乎不可见）</summary>
        private static void DrawDustColumn(SpriteBatch spriteBatch, Texture2D fog, Vector2 fogOrig,
            int band, int n, float centerX, float drawH, float strength) {
            float breathe = Breathe(band, centerX);
            Color dustTint = band == 0 ? DustIndigo : DustOrange;
            for (int i = 0; i < PlumesPerBand; i++) {
                float h1 = 0.5f + 0.5f * MathF.Sin(n * 5.7f + band * 2.3f + i * 12.9f);
                float h2 = 0.5f + 0.5f * MathF.Sin(n * 3.1f + band * 7.7f + i * 78.2f);
                float bob = MathF.Sin(Main.GameUpdateCount * 0.004f + h1 * MathHelper.TwoPi) * 46f;
                float worldX = centerX + (h1 - 0.5f) * BandHalfWidth * 1.1f;
                float screenY = (i + 0.5f) / PlumesPerBand * drawH - 260f + bob;
                int tileX = Utils.Clamp((int)(worldX / 16f), 8, Main.maxTilesX - 8);
                int tileY = Utils.Clamp((int)((Main.screenPosition.Y + screenY) / 16f), 8, Main.maxTilesY - 8);
                Color light = Lighting.GetColor(tileX, tileY);
                float lightK = 0.30f + 0.70f * ((light.R + light.G + light.B) / 765f);
                float alpha = 0.13f * strength * breathe * lightK;
                if (alpha < 0.005f) {
                    continue;
                }
                float rot = Main.GameUpdateCount * 0.0016f * (h2 > 0.5f ? 1f : -1f) + h2 * MathHelper.TwoPi;
                spriteBatch.Draw(fog, new Vector2(worldX - Main.screenPosition.X, screenY), null,
                    dustTint * alpha, rot, fogOrig, 1.7f + 1.2f * h2, SpriteEffects.None, 0f);
            }
        }

        /// <summary>幕内光缕：两缕细窄软光随尘缓摆（径向衰减自带两端收口，不犯长条硬切禁令）</summary>
        private static void DrawWisps(SpriteBatch spriteBatch, Texture2D glow, Vector2 glowOrig,
            int band, int n, float centerX, float drawH, float strength) {
            float breathe = Breathe(band, centerX);
            Color tint = band == 0 ? AstralveilFX.Indigo : AstralveilFX.Orange;
            for (int w = 0; w < WispsPerBand; w++) {
                float hw = 0.5f + 0.5f * MathF.Sin(n * 4.3f + band * 5.1f + w * 9.7f);
                float sway = MathF.Sin(Main.GameUpdateCount * 0.005f + hw * MathHelper.TwoPi)
                    * BandHalfWidth * 0.30f;
                float x = centerX - Main.screenPosition.X + (hw - 0.5f) * BandHalfWidth * 0.7f + sway;
                float alpha = 0.075f * strength * breathe;
                spriteBatch.Draw(glow, new Vector2(x, Main.screenHeight * (0.35f + 0.30f * hw)), null,
                    tint * alpha, 0f, glowOrig, new Vector2(0.55f, drawH * (0.5f + 0.3f * hw) / 64f),
                    SpriteEffects.None, 0f);
            }
        }
    }
}
