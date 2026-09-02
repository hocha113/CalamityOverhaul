using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 头颚叠绘：左右瓣各绕自己根部顶边的铰链转。
    /// 开合优先读已同步状态机声明的颚指令；颚未声明时回落到爪映射（祭舞/挥掷不必双写）。
    /// 不占网络包。图七不含牙，所有画头处都走这里；颚画在头本体之前。
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
        /// <summary>闭合摆角（相对源图半张向内，尖端相触）</summary>
        private const float ClosedAngle = -0.55f;
        /// <summary>全张摆角（约 45°/侧）</summary>
        private const float WideAngle = 0.78f;

        /// <summary>荒花：先颚指令，Idle 回落爪映射</summary>
        internal static float ResolveOpen(BssJawCommand jaw, float jawPhase, float jawBurst,
            BssClawCommand claw, float clawPhase, float clawBurst, float gaitPhase) {
            return jaw == BssJawCommand.Idle
                ? ResolveOpen(claw, clawPhase, clawBurst, gaitPhase)
                : ResolveJaw(jaw, jawPhase, jawBurst, gaitPhase);
        }

        internal static float ResolveJaw(BssJawCommand cmd, float jawPhase, float jawBurst, float gaitPhase) {
            float p = MathHelper.Clamp(jawPhase, 0f, 1f);
            float burst = MathHelper.Clamp(jawBurst, 0f, 1f);
            return cmd switch {
                BssJawCommand.Inhale => MathHelper.Lerp(IdleOpen(gaitPhase), 0.05f, p),
                BssJawCommand.Spit => MathHelper.Lerp(0.35f, 0.95f, burst),
                BssJawCommand.Roar => MathHelper.Lerp(0.2f, 1f, p),
                BssJawCommand.Gape => 0.85f,
                BssJawCommand.Bite => MathHelper.Lerp(0f, 0.9f, p),
                BssJawCommand.Clamp => 0.06f,
                BssJawCommand.Slack => 0.55f + 0.08f * MathF.Sin(gaitPhase),
                _ => IdleOpen(gaitPhase),
            };
        }

        /// <summary>跟手速度：咬/喷快、吼/张中、待机慢</summary>
        internal static float SnapRate(BssJawCommand jaw, BssClawCommand claw) {
            BssJawCommand effective = jaw != BssJawCommand.Idle ? jaw : claw switch {
                BssClawCommand.Snatch => BssJawCommand.Bite,
                BssClawCommand.GuardMouth => BssJawCommand.Spit,
                BssClawCommand.RainFlick => BssJawCommand.Spit,
                _ => BssJawCommand.Idle,
            };
            return effective switch {
                BssJawCommand.Bite or BssJawCommand.Spit => 0.55f,
                BssJawCommand.Roar or BssJawCommand.Gape or BssJawCommand.Inhale => 0.22f,
                BssJawCommand.Clamp => 0.28f,
                _ => 0.08f,
            };
        }

        internal static float ResolveOpen(BssClawCommand cmd, float clawPhase, float clawBurst, float gaitPhase) {
            float idle = IdleOpen(gaitPhase);
            return cmd switch {
                BssClawCommand.GuardMouth => MathHelper.Lerp(0.08f, 0.85f, MathHelper.Clamp(clawBurst, 0f, 1f)),
                BssClawCommand.Snatch => MathHelper.Lerp(0.85f, 0.06f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                BssClawCommand.RainFlick => MathHelper.Lerp(idle, 0.72f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                BssClawCommand.Rite => MathHelper.Lerp(0.15f, 0.7f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                BssClawCommand.Tuck or BssClawCommand.Collapse => 0.08f,
                _ => idle,
            };
        }

        internal static float ResolveOpen(FssClawCommand cmd, float clawPhase, float clawBurst, float gaitPhase) {
            float idle = IdleOpen(gaitPhase);
            return cmd switch {
                FssClawCommand.GuardMouth => MathHelper.Lerp(0.08f, 0.85f, MathHelper.Clamp(clawBurst, 0f, 1f)),
                FssClawCommand.Snatch => MathHelper.Lerp(0.85f, 0.06f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                FssClawCommand.Fling => MathHelper.Lerp(idle, 0.72f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                FssClawCommand.Slam => MathHelper.Lerp(idle, 0.65f, MathHelper.Clamp(clawPhase, 0f, 1f)),
                FssClawCommand.Tuck or FssClawCommand.Collapse => 0.08f,
                _ => idle,
            };
        }

        /// <summary>待机微张：贴源图半张（open≈0.41 时摆角为 0）</summary>
        internal static float IdleOpen(float gaitPhase)
            => 0.42f + 0.04f * MathF.Sin(gaitPhase);

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
            Vector2 texRight = forward.RotatedBy(-MathHelper.PiOver2);
            Vector2 hinge = headCenter + forward * (HingeForward * scale) - screenPos;
            Vector2 leftPos = hinge + texRight * (LeftHingeSide * scale);
            Vector2 rightPos = hinge + texRight * (RightHingeSide * scale);

            float swing = MathHelper.Lerp(ClosedAngle, WideAngle, MathHelper.Clamp(jawOpen, 0f, 1f));
            sb.Draw(left, leftPos, null, tint, headRotation + swing, LeftOrigin, scale, SpriteEffects.None, 0f);
            sb.Draw(right, rightPos, null, tint, headRotation - swing, RightOrigin, scale, SpriteEffects.None, 0f);
        }
    }
}
