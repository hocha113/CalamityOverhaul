using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.UI
{
    /// <summary>
    /// 复苏进度 HUD，屏幕正上方居中，复苏值变化后滑入，静止约2秒后淡出<br/>
    /// 进度条由 WraithRevivalHud shader 绘制（墨色→血色，前沿撕裂，危险脉冲）
    /// </summary>
    internal sealed class WraithRevivalHud : UIHandle
    {
        public static WraithRevivalHud Instance
            => UIHandleLoader.GetUIHandleOfType<WraithRevivalHud>();

        private const float BarW = 210f;
        private const float BarH = 16f;
        //appear=0 → 完全收进屏幕上方；appear=1 → 完全滑入
        private float appear;
        private static WraithPlayer LocalWraith {
            get {
                if (Main.gameMenu || Main.dedServ) { return null; }
                Player p = Main.LocalPlayer;
                return p != null && p.active ? p.GetModPlayer<WraithPlayer>() : null;
            }
        }

        public override bool Active {
            get {
                WraithPlayer wp = LocalWraith;
                return wp != null && (wp.Revival > 0.005f || appear > 0.01f);
            }
        }

        public override void Update() {
            WraithPlayer wp = LocalWraith;
            if (wp == null) {
                appear = 0f;
                return;
            }
            //RevivalChangedTimer < 120（2s）→ 完全展开；之后淡出
            bool show = wp.Revival > 0.005f && wp.RevivalChangedTimer < 120;
            float target = show ? 1f : 0f;
            appear += (target - appear) * (target > appear ? 0.12f : 0.07f);
            appear = MathHelper.Clamp(appear, 0f, 1f);
        }

        public override void Draw(SpriteBatch sb) {
            if (appear < 0.01f) { return; }
            WraithPlayer wp = LocalWraith;
            if (wp == null) { return; }

            float screenW = PlayerInput.RealScreenWidth / Main.UIScale;
            float barX = (screenW - BarW) * 0.5f;
            float barY = -BarH + appear * (BarH + 14f) + 100;

            //--- shader 绘制进度条 ---
            Effect effect = EffectLoader.WraithRevivalHud?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            float danger = MathHelper.Clamp((wp.Revival - 0.7f) / 0.3f, 0f, 1f);
            float pulse = 0.5f + 0.5f * MathF.Sin(GlobalTimer * 9.8f);

            effect.Parameters["transformMatrix"]?.SetValue(Main.UIScaleMatrix);
            effect.Parameters["uTime"]?.SetValue(GlobalTimer);
            effect.Parameters["uProgress"]?.SetValue(wp.Revival);
            effect.Parameters["uDangerPulse"]?.SetValue(danger * pulse);
            effect.Parameters["uColInk"]?.SetValue(new Vector3(0.07f, 0.047f, 0.086f));
            effect.Parameters["uColBlood"]?.SetValue(new Vector3(0.63f, 0.078f, 0.118f));
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

            float x = barX, y = barY, w = BarW, h = BarH;
            var verts = new VertexPositionColorTexture[4];
            verts[0] = new(new Vector3(x, y, 0), Color.White, new Vector2(0, 0));
            verts[1] = new(new Vector3(x + w, y, 0), Color.White, new Vector2(1, 0));
            verts[2] = new(new Vector3(x, y + h, 0), Color.White, new Vector2(0, 1));
            verts[3] = new(new Vector3(x + w, y + h, 0), Color.White, new Vector2(1, 1));
            short[] indices = [0, 1, 2, 1, 3, 2];

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 4, indices, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;

            //--- 小字标题 ---
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string label = $"{(int)(wp.Revival * 100)}%";
            Vector2 labelSize = font.MeasureString(label) * 0.52f;
            Vector2 labelPos = new((screenW - labelSize.X) * 0.5f, barY - labelSize.Y - 2f);
            Utils.DrawBorderString(sb, label, labelPos, new Color(168, 42, 55) * appear, 0.52f);
        }
    }
}