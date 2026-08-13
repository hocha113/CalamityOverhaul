using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering
{
    /// <summary>
    /// 吞没投技·凝胶前壁：持人期在玩家层之上把王体贴图以半透明凝胶色再画一层，
    /// 被吞玩家被夹在身体与前壁之间，读作"沉在半透明凝胶体内"。
    /// 全部由同步状态驱动，所有客户端(含旁观者)可见
    /// </summary>
    internal class KingSlimeEngulfRender : RenderHandle
    {
        /// <summary>权重槽 1.39(批次预分配)，玩家层之后画前壁</summary>
        public override float Weight => 1.39f;

        public override void DrawAfterPlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }

            bool begun = false;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != Terraria.ID.NPCID.KingSlime
                    || (int)npc.ai[2] != (int)KingSlimeStateIndex.Engulf) {
                    continue;
                }
                if (!KingSlimeAI.TryGetKingAI(npc, out KingSlimeAI king) || king.StateContext == null) {
                    continue;
                }
                //只在持人期(消化/高压)画前壁，喷出后立即消失
                int grabPhase = (int)king.ai[KingSlimeEngulfState.SlotGrabPhase];
                if (grabPhase is not 1 and not 2) {
                    continue;
                }
                if (!KingSlimeGelFX.OnScreen(npc.Center)) {
                    continue;
                }

                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                }
                DrawGelFront(spriteBatch, npc, king.StateContext);
            }

            if (begun) {
                spriteBatch.End();
            }
        }

        /// <summary>与本体绘制同参数复算形变，凝胶色半透明盖画+泡沫轮廓微光</summary>
        private static void DrawGelFront(SpriteBatch spriteBatch, NPC npc, KingSlimeStateContext ctx) {
            Texture2D bodyTex = TextureAssets.Npc[npc.type].Value;
            Rectangle frameRec = npc.frame;
            if (frameRec.Height <= 0) {
                frameRec = bodyTex.GetRectangle(0, Main.npcFrameCount[npc.type]);
            }
            //与本体同款的帧界内缩，防邻帧渗线
            if (frameRec.Height > 4) {
                frameRec.Y += 1;
                frameRec.Height -= 2;
            }

            //形变参数与 KingSlimeRenderer.DrawBody 保持一致，前壁与身体完全贴合
            float squash = ctx.VisualSquash;
            float wobble = ctx.WobbleAmp;
            float wobbleX = 1f + (float)Math.Sin(ctx.WobblePhase) * wobble;
            float wobbleY = 1f - (float)Math.Sin(ctx.WobblePhase + 1.1f) * wobble * 0.8f;
            float scaleY = npc.scale * squash * wobbleY;
            float scaleX = npc.scale * (1f + (1f - squash) * 0.85f) * wobbleX;

            Vector2 bottom = new Vector2(npc.Center.X, npc.position.Y + npc.height) - Main.screenPosition
                + new Vector2(0f, npc.gfxOffY + 4f);
            Vector2 origin = new Vector2(frameRec.Width * 0.5f, frameRec.Height);
            SpriteEffects flip = npc.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //凝胶前壁：原贴图真透明通道，凝胶色调+受光衰减，半透明能看见里面的玩家
            Color light = Lighting.GetColor((npc.Center / 16f).ToPoint());
            Color film = Color.Lerp(light.MultiplyRGB(KingSlimeGelFX.GelMid), KingSlimeGelFX.GelMid, 0.4f);
            float opacity = 0.52f * MathHelper.Clamp(ctx.BodyOpacity, 0f, 1f);
            spriteBatch.Draw(bodyTex, bottom, frameRec, film * opacity, ctx.BodyLean,
                origin, new Vector2(scaleX, scaleY), flip, 0f);

            //泡沫轮廓微光(加色语义：A=0)，随呼吸轻闪
            float breath = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5.2f);
            Color foam = KingSlimeGelFX.GelFoam with { A = 0 };
            spriteBatch.Draw(bodyTex, bottom, frameRec, foam * (0.1f + 0.05f * breath), ctx.BodyLean,
                origin, new Vector2(scaleX * 1.015f, scaleY * 1.015f), flip, 0f);
        }
    }
}
