using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions
{
    /// <summary>
    /// 硬模式仆从族（B 批）共用的举杖唤令动作：三拍时间线（快举过冲 / 顶点驻留微颤 / 收杖半落）。
    /// 输入只有 itemAnimation 进度与朝向，纯确定性，各端画同一姿态；
    /// 唤令光爆由各方案传自家主题色调用，杖尖位置按举杖角推算
    /// </summary>
    internal static class GsMinionCastMotion
    {
        /// <summary>光爆兑现进度：快举段收尾、杖已过顶（LiftCurve 首段 0~0.4 的尾部）</summary>
        private const float BurstProgress = 0.35f;

        /// <summary>握点（MountedCenter + dir*8）到杖尖的沿轴距离估算（手握杆身下段）</summary>
        private const float StaffReach = 38f;

        /// <summary>
        /// 待发光爆的每玩家寄存（纯表现层，各端本地）：GsUseAnimation 在动画第 0 帧
        /// 只登记主题色，举杖到位帧由 ApplyRaise 兑现，演出不再先于动作
        /// </summary>
        private struct PendingBurst
        {
            public bool Armed;
            public Color Accent;
            public Color Ember;
        }

        private static readonly PendingBurst[] pendingBursts = new PendingBurst[Main.maxPlayers];

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

            //唤令光爆兑现：举杖到位帧、在当前举角推算的真实杖尖上（时序与位置都跟着动作走）
            ref PendingBurst pending = ref pendingBursts[player.whoAmI];
            if (pending.Armed && progress >= BurstProgress) {
                pending.Armed = false;
                EmitBurst(player.MountedCenter + dir * (8f + StaffReach), pending.Accent, pending.Ember);
            }
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

        /// <summary>
        /// 唤令光爆（在 GsUseAnimation 时机调用，各端同步登记；内部已守服务端）。
        /// 动画第 0 帧杖还指前下方，故此处只登记，实际兑现在举杖到位帧（见 ApplyRaise）。
        /// accent = 主题主色（上涌光粒），ember = 余烬色（散射火花）
        /// </summary>
        internal static void CastBurst(Player player, Color accent, Color ember) {
            if (VaultUtils.isServer) {
                return;
            }
            ref PendingBurst pending = ref pendingBursts[player.whoAmI];
            pending.Armed = true;
            pending.Accent = accent;
            pending.Ember = ember;
        }

        /// <summary>光爆本体（粒子样式原封不动，只挪了触发时机与生成位置）</summary>
        private static void EmitBurst(Vector2 tip, Color accent, Color ember) {
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Light>(tip + Main.rand.NextVector2Circular(9f, 9f),
                    -Vector2.UnitY.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f))
                        * Main.rand.NextFloat(1.2f, 3.2f),
                    accent, Main.rand.NextFloat(0.10f, 0.16f))?.Configure(14, 0.8f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(tip,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                    ember, Main.rand.NextFloat(0.20f, 0.32f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }
    }
}
