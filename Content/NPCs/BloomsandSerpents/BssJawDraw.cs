using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 头颚叠绘：左右瓣铰在嘴位，开合由已同步的爪指令/步态推导，不占网络包。
    /// 图七不含牙，所有画头处都走这里，避免 BSS 有嘴、借皮端没嘴。
    /// </summary>
    internal static class BssJawDraw
    {
        /// <summary>铰链在每瓣上端（贴图像素，2x）</summary>
        private static readonly Vector2 LeftOrigin = new(50f, 8f);
        private static readonly Vector2 RightOrigin = new(2f, 8f);
        private const float MaxOpen = 0.38f;

        internal static float ResolveOpen(BssClawCommand cmd, float clawPhase, float clawBurst, float gaitPhase) {
            float idle = IdleOpen(gaitPhase);
            return cmd switch {
                BssClawCommand.GuardMouth => MathHelper.Lerp(0.08f, 0.72f, MathHelper.Clamp(clawBurst, 0f, 1f)),
                BssClawCommand.Snatch => MathHelper.Lerp(0.85f, 0.06f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                BssClawCommand.RainFlick => MathHelper.Lerp(idle, 0.42f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                BssClawCommand.Rite => 0.22f,
                BssClawCommand.Tuck or BssClawCommand.Collapse => 0.08f,
                _ => idle,
            };
        }

        internal static float ResolveOpen(FssClawCommand cmd, float clawPhase, float clawBurst, float gaitPhase) {
            float idle = IdleOpen(gaitPhase);
            return cmd switch {
                FssClawCommand.GuardMouth => MathHelper.Lerp(0.08f, 0.72f, MathHelper.Clamp(clawBurst, 0f, 1f)),
                FssClawCommand.Snatch => MathHelper.Lerp(0.85f, 0.06f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                FssClawCommand.Fling => MathHelper.Lerp(idle, 0.55f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                FssClawCommand.Slam => MathHelper.Lerp(idle, 0.4f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                FssClawCommand.Tuck or FssClawCommand.Collapse => 0.08f,
                _ => idle,
            };
        }

        internal static float IdleOpen(float gaitPhase)
            => 0.16f + 0.05f * MathF.Sin(gaitPhase * 2f);

        internal static void Draw(SpriteBatch sb, Vector2 headCenter, float headRotation,
            float jawOpen, Color tint, Vector2 screenPos, float scale = 1f) {
            Texture2D left = BssHead.JawLeftAsset?.Value;
            Texture2D right = BssHead.JawRightAsset?.Value;
            if (left == null || right == null) {
                return;
            }

            Vector2 mouth = BssClawScript.MouthPos(headCenter, headRotation);
            float open = MathHelper.Clamp(jawOpen, 0f, 1f) * MaxOpen;
            Vector2 pos = mouth - screenPos;

            sb.Draw(left, pos, null, tint, headRotation - open, LeftOrigin, scale, SpriteEffects.None, 0f);
            sb.Draw(right, pos, null, tint, headRotation + open, RightOrigin, scale, SpriteEffects.None, 0f);
        }
    }
}
