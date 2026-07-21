using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>预警收黑+三阶侵蚀暗角，只读本地 WraithPlayer</summary>
    internal sealed class WraithOmenRender : UIHandle
    {
        /// <summary>压在常规 HUD 下</summary>
        public override float RenderPriority => 0.5f;

        //收黑缓动
        private float omenEase;
        private float ambientEase;

        private static WraithPlayer LocalWraith {
            get {
                if (Main.gameMenu || Main.dedServ) {
                    return null;
                }
                Player player = Main.LocalPlayer;
                return player != null && player.active ? player.GetModPlayer<WraithPlayer>() : null;
            }
        }

        public override bool Active {
            get {
                if (omenEase > 0.004f || ambientEase > 0.004f) {
                    return true;
                }
                WraithPlayer wraith = LocalWraith;
                return wraith != null && (wraith.OmenActive || wraith.ErosionTier >= 3);
            }
        }

        public override void Update() {
            WraithPlayer wraith = LocalWraith;
            float omenTarget = wraith?.OmenActive == true ? wraith.OmenProgress : 0f;
            float ambientTarget = wraith != null && wraith.ErosionTier >= 3 ? 1f : 0f;
            //进快退慢
            omenEase += (omenTarget - omenEase) * (omenTarget > omenEase ? 0.16f : 0.06f);
            ambientEase += (ambientTarget - ambientEase) * 0.03f;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            float omen = omenEase;
            float ambient = ambientEase;
            if (omen < 0.004f && ambient < 0.004f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            int w = (int)MathF.Ceiling(PlayerInput.RealScreenWidth / Main.UIScale) + 2;
            int h = (int)MathF.Ceiling(PlayerInput.RealScreenHeight / Main.UIScale) + 2;

            //心跳脉动
            float beat = 0.86f + 0.14f * MathF.Sin(GlobalTimer * MathHelper.Lerp(0.06f, 0.30f, omen));

            //整体压暗
            float dim = omen * omen * 0.42f * beat;
            if (dim > 0.004f) {
                spriteBatch.Draw(pixel, new Rectangle(0, 0, w, h), src, Color.Black * dim);
            }

            //四缘收黑
            float edge = MathHelper.Clamp(omen * 0.85f * beat + ambient * 0.30f, 0f, 1f);
            if (edge > 0.004f) {
                int band = (int)(MathF.Min(w, h) * (0.16f + omen * 0.10f));
                const int Steps = 9;
                int thick = Math.Max(band / Steps, 1);
                for (int i = 0; i < Steps; i++) {
                    float t = i / (float)(Steps - 1);
                    float alpha = edge * (1f - t) * (1f - t) * 0.34f;
                    if (alpha < 0.004f) {
                        continue;
                    }
                    int offset = i * thick;
                    Color color = Color.Black * alpha;
                    spriteBatch.Draw(pixel, new Rectangle(0, offset, w, thick), src, color);
                    spriteBatch.Draw(pixel, new Rectangle(0, h - offset - thick, w, thick), src, color);
                    spriteBatch.Draw(pixel, new Rectangle(offset, 0, thick, h), src, color);
                    spriteBatch.Draw(pixel, new Rectangle(w - offset - thick, 0, thick, h), src, color);
                }
            }

            //濒死薄绯
            float red = MathF.Max(omen - 0.45f, 0f) / 0.55f;
            if (red > 0.004f) {
                spriteBatch.Draw(pixel, new Rectangle(0, 0, w, h), src, new Color(96, 8, 16) * (red * 0.16f * beat));
            }
        }
    }
}
