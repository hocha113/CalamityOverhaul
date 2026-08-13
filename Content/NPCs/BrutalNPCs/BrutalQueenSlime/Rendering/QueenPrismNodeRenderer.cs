using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Rendering
{
    /// <summary>棱晶节点绘制：着色器晶壳+凝胶核+光冕，血量映射裂纹</summary>
    internal static class QueenPrismNodeRenderer
    {
        private const float ShellHalfSize = 96f;
        private const int MaterializeFrames = 45;

        public static void DrawNode(SpriteBatch sb, NPC npc, float age, Vector2 screenPos, Color drawColor) {
            float grow = MathHelper.Clamp(age / MaterializeFrames, 0f, 1f);
            float hurt = 1f - npc.life / (float)npc.lifeMax;
            float hueSeed = npc.whoAmI * 0.161f % 1f;
            //呼吸浮动(纯视觉)
            Vector2 bob = new Vector2(0f, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.1f + npc.whoAmI * 1.7f) * 7f);
            Vector2 center = npc.Center + bob;

            //晶壳(着色器quad)，需要暂停批次
            Effect effect = EffectLoader.QueenPrismCrystal?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool dummy = npc.IsABestiaryIconDummy;
            if (effect != null && noise != null && !dummy) {
                sb.End();
                DrawShell(npc, effect, noise, center, grow, hurt, hueSeed);

                //光冕(加色批)
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                DrawCorona(sb, center - screenPos, grow, hurt, hueSeed);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //凝胶核：原版水晶史莱姆贴图缩在晶心
            Texture2D coreTex = TextureAssets.Npc[npc.type].Value;
            Rectangle coreRect = npc.frame;
            if (coreRect.Height <= 0 || coreRect.Height > coreTex.Height) {
                coreRect = coreTex.Frame(1, Main.npcFrameCount[npc.type], 0, 0);
            }
            Vector2 coreOrigin = coreRect.Size() / 2f;
            float wobble = 1f + 0.05f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.4f + npc.whoAmI);
            sb.Draw(coreTex, center - screenPos, coreRect, npc.GetAlpha(drawColor), 0f,
                coreOrigin, npc.scale * grow * wobble, SpriteEffects.None, 0f);
        }

        private static void DrawShell(NPC npc, Effect effect, Texture2D noise, Vector2 center, float grow, float hurt, float hueSeed) {
            float half = ShellHalfSize * (0.4f + 0.6f * grow);
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(center.X - half, center.Y - half, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(center.X + half, center.Y - half, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(center.X - half, center.Y + half, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(center.X + half, center.Y + half, 0f), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uMode"]?.SetValue(0f);
            effect.Parameters["uGrow"]?.SetValue(grow);
            effect.Parameters["uShatter"]?.SetValue(hurt);
            effect.Parameters["uCharge"]?.SetValue(0f);
            effect.Parameters["uHueSeed"]?.SetValue(hueSeed);
            effect.Parameters["seed"]?.SetValue(npc.whoAmI * 0.173f % 1f);
            //噪声显式绑到 s1（shader 内 register(s1)），参数式绑定废弃
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        private static void DrawCorona(SpriteBatch sb, Vector2 drawPos, float grow, float hurt, float hueSeed) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Color hue = QueenMotion.PrismHue(hueSeed);
            float flick = 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 21f + hueSeed * 30f);
            //受损越重光越不稳
            float instability = 1f + hurt * 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 47f);

            sb.Draw(glow, drawPos, null, hue * (0.5f * grow * instability), 0f, glow.Size() / 2f, 1.6f * flick, SpriteEffects.None, 0f);
            sb.Draw(glow, drawPos, null, Color.White * (0.32f * grow), 0f, glow.Size() / 2f, 0.7f, SpriteEffects.None, 0f);
            sb.Draw(star, drawPos, null, hue * (0.55f * grow * instability),
                Main.GlobalTimeWrappedHourly * 1.4f + hueSeed * 9f, star.Size() / 2f, 0.5f * grow, SpriteEffects.None, 0f);
        }
    }
}
