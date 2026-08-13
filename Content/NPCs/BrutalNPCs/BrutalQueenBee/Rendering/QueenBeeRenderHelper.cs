using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering
{
    /// <summary>女王本体绘制：残影/怒辉/蓄力过热层</summary>
    internal static class QueenBeeRenderHelper
    {
        /// <summary>女王12帧图集</summary>
        private const int FrameCount = 12;

        public static void DrawQueen(SpriteBatch spriteBatch, NPC npc, QueenBeeStateContext context,
            Texture2D texture, Vector2 screenPos, Color drawColor) {
            int frameHeight = texture.Height / FrameCount;
            Rectangle frameRec = new Rectangle(0, npc.frame.Y, texture.Width, frameHeight);
            //帧Y越界保护(接管前残留)
            if (frameRec.Y < 0 || frameRec.Y + frameHeight > texture.Height) {
                frameRec.Y = frameHeight * 4;
            }
            Vector2 origin = frameRec.Size() * 0.5f;
            Vector2 mainPos = npc.Center - screenPos;
            SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //高速残影：oldPos琥珀鬼影，越旧越淡越小
            if (context.AfterimageBoost > 0.05f) {
                for (int i = npc.oldPos.Length - 1; i >= 1; i -= 2) {
                    if (npc.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 ghostPos = npc.oldPos[i] + npc.Size * 0.5f - screenPos;
                    //瞬移长段跳过
                    if (Vector2.DistanceSquared(ghostPos, mainPos) > 700f * 700f) {
                        continue;
                    }
                    float fade = (1f - i / (float)npc.oldPos.Length) * context.AfterimageBoost;
                    Color ghost = new Color(255, 175, 55, 0) * (0.34f * fade);
                    spriteBatch.Draw(texture, ghostPos, frameRec, ghost, npc.rotation,
                        origin, npc.scale * (0.9f + 0.1f * fade), effects, 0f);
                }
            }

            //本体
            spriteBatch.Draw(texture, mainPos, frameRec, drawColor, npc.rotation,
                origin, npc.scale, effects, 0f);

            //怒辉描边：双层加色错相呼吸
            if (context.RageGlow > 0.03f) {
                float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6.5f);
                Color rim = new Color(255, 168, 46, 0) * (context.RageGlow * 0.4f * pulse);
                spriteBatch.Draw(texture, mainPos, frameRec, rim, npc.rotation,
                    origin, npc.scale * 1.045f, effects, 0f);
                Color rimHot = new Color(255, 226, 140, 0) * (context.RageGlow * 0.2f * pulse);
                spriteBatch.Draw(texture, mainPos, frameRec, rimHot, npc.rotation,
                    origin, npc.scale * 1.015f, effects, 0f);
            }

            //蓄力过热层：进度推白但压住(暖白非纯白)
            if (context.IsCharging && context.ChargeProgress > 0.05f) {
                float p = context.ChargeProgress;
                float flicker = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 24f);
                Color hot = new Color(255, 214, 120, 0) * (p * 0.45f * flicker);
                spriteBatch.Draw(texture, mainPos, frameRec, hot, npc.rotation,
                    origin, npc.scale * (1f + p * 0.03f), effects, 0f);
            }
        }
    }
}
