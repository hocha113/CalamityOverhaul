using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 结印盘几何：外环六芒摆六只鬼，内三角三个结印位，三边是两两组合的墨线，
    /// 心是三鬼合鬼印。<br/>
    /// 绘制、命中、悬停预览一律读这一份——照鏨盘扇骨的规矩，同一套角度绝不各算一遍
    /// </summary>
    internal readonly struct OniSigilWheel
    {
        /// <summary>外环鬼位数（六芒）</summary>
        internal const int NodeCount = 6;
        /// <summary>内三角结印位数</summary>
        internal const int SlotCount = OniRegistry.SlotCount;

        /// <summary>结印位所在半径与外径之比</summary>
        private const float SlotRadiusRatio = 0.42f;
        /// <summary>鬼位所在半径与外径之比（留出印章与读数的余量）</summary>
        private const float NodeRadiusRatio = 0.84f;

        internal Vector2 Center { get; }
        /// <summary>盘外径（六芒星外接圆）</summary>
        internal float Radius { get; }
        /// <summary>鬼位印命中半径</summary>
        internal float NodeHit { get; }
        /// <summary>结印位命中半径</summary>
        internal float SlotHit { get; }

        internal OniSigilWheel(Vector2 center, float radius) {
            Center = center;
            Radius = MathF.Max(radius, 80f);
            //盘越大印越大，但不至于糊成一片；命中随之等比
            NodeHit = MathHelper.Clamp(Radius * 0.15f, 26f, 54f);
            SlotHit = MathHelper.Clamp(Radius * 0.13f, 24f, 46f);
        }

        /// <summary>本屏主体半径：吃屏，但给上梁与底部提示留道</summary>
        internal static float BodyRadius(float screenW, float screenH) {
            float byHeight = (screenH - OniLedgerBeam.Height - 96f) * 0.5f;
            float byWidth = screenW * 0.34f;
            return MathHelper.Clamp(MathF.Min(byHeight, byWidth), 150f, 330f);
        }

        /// <summary>第 i 个鬼位的方位角；正上起，顺时针</summary>
        internal static float NodeAngle(int index)
            => -MathHelper.PiOver2 + MathHelper.TwoPi * index / NodeCount;

        /// <summary>第 i 个结印位的方位角；正上起，三分</summary>
        internal static float SlotAngle(int index)
            => -MathHelper.PiOver2 + MathHelper.TwoPi * index / SlotCount;

        internal Vector2 NodePos(int index)
            => Center + NodeAngle(index).ToRotationVector2() * (Radius * NodeRadiusRatio);

        internal Vector2 SlotPos(int index)
            => Center + SlotAngle(index).ToRotationVector2() * (Radius * SlotRadiusRatio);

        /// <summary>六芒星尖端（外环轮廓的角）</summary>
        internal Vector2 StarPos(int index)
            => Center + NodeAngle(index).ToRotationVector2() * Radius;

        internal bool HitNode(Vector2 point, out int index) {
            for (int i = 0; i < NodeCount; i++) {
                if (Vector2.DistanceSquared(point, NodePos(i)) <= NodeHit * NodeHit) {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        internal bool HitSlot(Vector2 point, out int index) {
            for (int i = 0; i < SlotCount; i++) {
                if (Vector2.DistanceSquared(point, SlotPos(i)) <= SlotHit * SlotHit) {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        /// <summary>三角中心的合鬼印命中</summary>
        internal bool HitCore(Vector2 point)
            => Vector2.DistanceSquared(point, Center) <= SlotHit * SlotHit * 0.64f;

        /// <summary>盘面整体命中（点盘外算收屏）</summary>
        internal bool HitBoard(Vector2 point) {
            float reach = Radius + NodeHit;
            return Vector2.DistanceSquared(point, Center) <= reach * reach;
        }

        /// <summary>三角第 i 条边的两端结印位序号</summary>
        internal static (int A, int B) EdgeSlots(int edge) => edge switch {
            0 => (0, 1),
            1 => (1, 2),
            _ => (2, 0),
        };
    }
}
