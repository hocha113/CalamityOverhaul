using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Woodsong
{
    /// <summary>
    /// 暮雾屏幕层：夜里贴着地形涌起的低雾带。雾团锚定露天地表列，三层错相叠绘
    /// （宽底/中腰/散顶），随风缓移、正弦包络进出场；亮度乘所在处环境光，
    /// 月照下泛冷灰青、全黑处近乎不可见。挂 EndEntityDraw：人在雾里走，雾压人前。
    /// Masking/Fog 为真 alpha 贴图，AlphaBlend 直绘合法（暗层规则）。
    /// </summary>
    internal sealed class WoodsongMistRender : RenderHandle
    {
        /// <summary>权重 1.63（残酷群系氛围批 Woodsong 槽位专属）</summary>
        public override float Weight => 1.63f;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Fog = null;

        private const int BankCap = 16;
        private static readonly Color MistPale = new(174, 188, 208);

        private struct Bank
        {
            internal bool Active;
            internal Vector2 Anchor;
            internal float Phase;
            internal int Life;
            internal int MaxLife;
            internal float Width;
        }

        private static readonly Bank[] banks = new Bank[BankCap];
        private static int spawnIn;

        /// <summary>清空全部雾团（进出世界防坐标残留）</summary>
        internal static void ClearBanks() {
            for (int i = 0; i < banks.Length; i++) {
                banks[i].Active = false;
            }
            spawnIn = 0;
        }

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float fog = WoodsongAmbience.FogStrength;
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            //推进在场雾团：随风缓移，寿终或漂出远界即回收
            int active = 0;
            for (int i = 0; i < banks.Length; i++) {
                if (!banks[i].Active) {
                    continue;
                }
                banks[i].Life++;
                banks[i].Anchor.X += Main.windSpeedCurrent * 0.22f
                    + MathF.Sin(Main.GlobalTimeWrappedHourly * 0.3f + banks[i].Phase) * 0.05f;
                if (banks[i].Life >= banks[i].MaxLife
                    || Math.Abs(banks[i].Anchor.X - player.Center.X) > Main.screenWidth * 1.6f) {
                    banks[i].Active = false;
                    continue;
                }
                active++;
            }

            if (fog < 0.04f) {
                return;
            }
            if (--spawnIn > 0) {
                return;
            }
            spawnIn = Main.rand.Next(9, 18);

            //在场数量目标随雾浓度走
            if (active >= 3 + (int)(fog * 13f)) {
                return;
            }
            int tileX = (int)(player.Center.X / 16f) + Main.rand.Next(-70, 71);
            if (!WoodsongAmbience.TryFindOutdoorSurface(tileX, out int surfY)) {
                return;
            }
            for (int i = 0; i < banks.Length; i++) {
                if (banks[i].Active) {
                    continue;
                }
                banks[i] = new Bank {
                    Active = true,
                    Anchor = new Vector2(tileX * 16f + 8f, surfY * 16f),
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Life = 0,
                    MaxLife = Main.rand.Next(480, 840),
                    Width = Main.rand.NextFloat(0.8f, 1.35f),
                };
                return;
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float fog = WoodsongAmbience.FogStrength;
            if (fog < 0.02f) {
                return;
            }
            Texture2D tex = Fog?.Value;
            if (tex == null || tex.IsDisposed) {
                return;
            }
            bool any = false;
            for (int i = 0; i < banks.Length; i++) {
                if (banks[i].Active) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            //三层剖面：宽底贴地、中腰略收、散顶轻飘；透明度阶梯向上递减
            ReadOnlySpan<float> layerW = [420f, 300f, 210f];
            ReadOnlySpan<float> layerH = [86f, 108f, 128f];
            ReadOnlySpan<float> layerUp = [6f, 16f, 28f];
            ReadOnlySpan<float> layerA = [0.22f, 0.13f, 0.08f];

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < banks.Length; i++) {
                if (!banks[i].Active) {
                    continue;
                }
                float env = MathF.Sin(MathHelper.Pi * banks[i].Life / banks[i].MaxLife);
                //乘环境光：月照泛白，纯黑处沉没（保 0.3 底读得出雾的存在）
                Color light = Lighting.GetColor((int)(banks[i].Anchor.X / 16f), (int)(banks[i].Anchor.Y / 16f) - 1);
                float lightK = 0.30f + 0.70f * ((light.R + light.G + light.B) / 765f);

                for (int s = 0; s < 3; s++) {
                    float alpha = layerA[s] * env * fog * lightK;
                    if (alpha < 0.004f) {
                        continue;
                    }
                    float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * (0.10f + 0.03f * s)
                        + banks[i].Phase + s * 1.9f) * 0.05f;
                    Vector2 scale = new(
                        banks[i].Width * layerW[s] / tex.Width,
                        layerH[s] / tex.Height);
                    //底边坐在地表线上，向下沉 6px 咬住地形
                    Vector2 pos = banks[i].Anchor - Main.screenPosition
                        - new Vector2(0f, layerH[s] * 0.5f - 6f + layerUp[s]);
                    spriteBatch.Draw(tex, pos, null, MistPale * alpha, sway,
                        new Vector2(tex.Width * 0.5f, tex.Height * 0.5f), scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.End();
        }
    }
}
