using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 头颚叠绘：左右瓣各绕自己根部顶边的铰链转，开合由已同步的爪指令/步态推导，不占网络包。
    /// 图七不含牙，所有画头处都走这里；颚画在头本体之前（红根藏在头底，只露刃）。
    ///
    /// 挂点来自源图实测（1x → ×2）：两瓣根部相距 13px 居中于头轴，铰链行在头底上方 18px；
    /// 源图姿态是半张，open=0 向内合到尖端相触，open=1 向外张开。
    /// </summary>
    internal static class BssJawDraw
    {
        /// <summary>铰链行：头心前向偏移（2x 像素）</summary>
        internal const float HingeForward = 30f;
        /// <summary>两瓣根心相对头轴的横向偏移（贴图 +x 为正）</summary>
        private const float LeftHingeSide = -27f;
        private const float RightHingeSide = 25f;
        /// <summary>各瓣贴图内的铰链像素（根部顶边中点）</summary>
        private static readonly Vector2 LeftOrigin = new(39f, 1f);
        private static readonly Vector2 RightOrigin = new(13f, 1f);
        /// <summary>闭合摆角（相对源图姿态向内，尖端相触不交叉）</summary>
        private const float ClosedAngle = -0.28f;
        /// <summary>全张摆角（向外）</summary>
        private const float WideAngle = 0.34f;

        internal static float ResolveOpen(BssClawCommand cmd, float clawPhase, float clawBurst, float gaitPhase) {
            float idle = IdleOpen(gaitPhase);
            return cmd switch {
                BssClawCommand.GuardMouth => MathHelper.Lerp(0.08f, 0.72f, MathHelper.Clamp(clawBurst, 0f, 1f)),
                BssClawCommand.Snatch => MathHelper.Lerp(0.85f, 0.06f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                BssClawCommand.RainFlick => MathHelper.Lerp(idle, 0.62f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                BssClawCommand.Rite => 0.3f,
                BssClawCommand.Tuck or BssClawCommand.Collapse => 0.08f,
                _ => idle,
            };
        }

        internal static float ResolveOpen(FssClawCommand cmd, float clawPhase, float clawBurst, float gaitPhase) {
            float idle = IdleOpen(gaitPhase);
            return cmd switch {
                FssClawCommand.GuardMouth => MathHelper.Lerp(0.08f, 0.72f, MathHelper.Clamp(clawBurst, 0f, 1f)),
                FssClawCommand.Snatch => MathHelper.Lerp(0.85f, 0.06f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                FssClawCommand.Fling => MathHelper.Lerp(idle, 0.72f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                FssClawCommand.Slam => MathHelper.Lerp(idle, 0.6f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                FssClawCommand.Tuck or FssClawCommand.Collapse => 0.08f,
                _ => idle,
            };
        }

        /// <summary>待机微张：随步态轻微开合（半张附近呼吸）</summary>
        internal static float IdleOpen(float gaitPhase)
            => 0.38f + 0.06f * MathF.Sin(gaitPhase * 2f);

        /// <summary>
        /// 画两瓣颚。headCenter 为头本体绘制中心（含绘制偏移），headRotation 为头旋转；
        /// 调用方须在画头本体之前调用，颚根才会被头压住。
        /// </summary>
        internal static void Draw(SpriteBatch sb, Vector2 headCenter, float headRotation,
            float jawOpen, Color tint, Vector2 screenPos, float scale = 1f) {
            Texture2D left = BssHead.JawLeftAsset?.Value;
            Texture2D right = BssHead.JawRightAsset?.Value;
            if (left == null || right == null) {
                return;
            }

            Vector2 forward = BssClawScript.Forward(headRotation);
            //贴图 +x 在世界里的方向（头正面朝下、rotation=0 时即屏幕右）
            Vector2 texRight = forward.RotatedBy(-MathHelper.PiOver2);
            Vector2 hinge = headCenter + forward * (HingeForward * scale) - screenPos;
            Vector2 leftPos = hinge + texRight * (LeftHingeSide * scale);
            Vector2 rightPos = hinge + texRight * (RightHingeSide * scale);

            float swing = MathHelper.Lerp(ClosedAngle, WideAngle, MathHelper.Clamp(jawOpen, 0f, 1f));
            //屏幕坐标顺时针为正：左瓣尖端在铰链下方，正角把尖端甩向 -x（外侧）；右瓣镜像
            sb.Draw(left, leftPos, null, tint, headRotation + swing, LeftOrigin, scale, SpriteEffects.None, 0f);
            sb.Draw(right, rightPos, null, tint, headRotation - swing, RightOrigin, scale, SpriteEffects.None, 0f);
        }
    }
}
