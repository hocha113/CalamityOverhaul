using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions
{
    /// <summary>
    /// 硬模式仆从族（B 批）共用的举杖唤令动作：三拍时间线（快举过冲 / 顶点驻留微颤 / 收杖半落）。
    /// 输入只有 itemAnimation 进度与朝向，纯确定性，各端画同一姿态
    /// </summary>
    internal static class GsMinionCastMotion
    {
        /// <summary>
        /// 举杖姿态（在 GsUseStyle 里调用）。liftScale 微调举高幅度（1 = 过顶）
        /// </summary>
        internal static void ApplyRaise(Player player, float liftScale = 1f) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float progress = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float lift = LiftCurve(progress);
            //方向相对角：0.6（前下垂杖）扫到 -1.83（过顶后仰），过冲时可到 -2.1
            float rel = 0.6f - 2.43f * lift * liftScale;
            Vector2 dir = new(MathF.Cos(rel) * player.direction, MathF.Sin(rel));
            player.itemLocation = player.MountedCenter + dir * 8f;
            player.itemRotation = (dir * player.direction).ToRotation() + player.direction * MathHelper.PiOver4;
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                dir.ToRotation() - MathHelper.PiOver2);
        }

        /// <summary>三拍举杖曲线：easeOutBack 快举（峰值 ~1.1）→ 顶点微颤 → 收杖回落至半举</summary>
        private static float LiftCurve(float progress) {
            if (progress < 0.4f) {
                float t = progress / 0.4f - 1f;
                const float back = 1.7f;
                return 1f + (back + 1f) * t * t * t + back * t * t;
            }
            if (progress < 0.7f) {
                return 1f + 0.03f * MathF.Sin((progress - 0.4f) * 31f);
            }
            float k = (progress - 0.7f) / 0.3f;
            return 1f - 0.55f * k * k;
        }
    }
}
