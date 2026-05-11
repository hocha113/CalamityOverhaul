using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足的玩家组件
    /// <br/>承担两件事：
    /// <list type="bullet">
    ///   <item>空中二段跳：监听原版跳跃键，无需经过雷达，独立运作</item>
    ///   <item>蓄力跳：完全由 <see cref="CyberwareSkillRadialUI"/> 通过 <see cref="OmniElectricFootSkill"/>
    ///         驱动，本类只暴露 <see cref="RadialDriveCharge"/> / <see cref="RadialReleaseCharge"/> /
    ///         <see cref="RadialCancelCharge"/> 三个入口，把蓄力比例与释放命令转换为实际游戏效果</item>
    /// </list>
    /// 头顶 HUD 通过本类暴露的 <see cref="ChargeRatio"/> / <see cref="IsCharging"/> 实时显示蓄力进度
    /// </summary>
    internal class OmniElectricFootPlayer : ModPlayer
    {
        /// <summary>
        /// 当前蓄力跳的能量进度（0~1），公开供 HUD 读取
        /// </summary>
        public float ChargeRatio { get; private set; }

        /// <summary>
        /// 是否正在通过雷达蓄力中（HUD 据此决定显隐）
        /// </summary>
        public bool IsCharging { get; private set; }

        /// <summary>
        /// 蓄力跳释放后的冷却剩余帧数，避免按键释放后立刻再次蓄力造成视觉抖动
        /// </summary>
        public int ReleaseCooldown { get; private set; }

        /// <summary>
        /// 二段跳是否仍可使用，落地或起跳后会重置为 true
        /// </summary>
        public bool CanDoubleJump { get; private set; } = true;

        /// <summary>
        /// 是否处于"脚踏地面"的状态，便于雷达据此决定蓄力技能是否可选
        /// </summary>
        public bool IsOnGround { get; private set; }

        //上一帧的 Y 速度，用于检测起跳瞬间
        private float lastVelocityY;
        //上一帧是否绑定地面
        private bool wasGroundedLastFrame;
        //上一帧蓄力状态，用于在按键被外部切断时清理蓄力姿态
        private bool wasChargingLastFrame;
        //蓄力姿态的最近一次喷射粒子时间，避免帧率叠加导致粒子爆量
        private int chargeParticleTick;

        public override void ResetEffects() {
            //冷却递减
            if (ReleaseCooldown > 0) {
                ReleaseCooldown--;
            }
            //帧首先快照上一帧的蓄力状态，再把 IsCharging 复位为 false
            //本帧后续如果雷达再次调用 RadialDriveCharge，IsCharging 才会被重新置 true
            //如此保证 HUD 读到的 IsCharging 严格等于"本帧雷达是否仍在驱动"
            wasChargingLastFrame = IsCharging;
            IsCharging = false;
            //没有驱动时，ChargeRatio 也按节奏衰减，让 HUD 的进度环有自然收回的过渡
            if (!wasChargingLastFrame && ChargeRatio > 0f) {
                ChargeRatio = MathF.Max(0f, ChargeRatio - 0.04f);
            }
        }

        public override void PostUpdate() {
            OmniElectricFoot equipped = OmniElectricFoot.GetEquipped(Player);
            if (equipped == null) {
                //卸下义足后立即清空所有状态
                ChargeRatio = 0f;
                IsCharging = false;
                CanDoubleJump = false;
                wasGroundedLastFrame = false;
                wasChargingLastFrame = false;
                lastVelocityY = Player.velocity.Y;
                return;
            }

            IsOnGround = DetectOnGround(Player);

            //落地瞬间重置二段跳额度
            if (IsOnGround && !wasGroundedLastFrame) {
                CanDoubleJump = true;
            }

            //首次起跳的容错：保证起跳后二段跳额度仍然保留到玩家真正消耗
            if (lastVelocityY >= 0f && Player.velocity.Y * Player.gravDir < -0.1f && wasGroundedLastFrame) {
                CanDoubleJump = true;
            }

            //仅本机玩家执行输入相关逻辑
            if (Player.whoAmI == Main.myPlayer) {
                UpdateDoubleJump(equipped);
            }

            wasGroundedLastFrame = IsOnGround;
            lastVelocityY = Player.velocity.Y;
        }

        /// <summary>
        /// 由 <see cref="OmniElectricFootSkill.OnChargeTick"/> 在雷达悬停期间每帧调用
        /// <br/>把雷达累积的比例直接写入本组件，并按需播放第一次蓄力的音效与粒子
        /// </summary>
        public void RadialDriveCharge(float ratio) {
            if (ReleaseCooldown > 0) {
                //冷却中拒绝任何蓄力输入，防止快速点按造成视觉抖动
                return;
            }
            if (!IsOnGround) {
                return;
            }

            ratio = MathHelper.Clamp(ratio, 0f, 1f);
            //首帧蓄力时播一次音效
            if (!wasChargingLastFrame) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.4f, Volume = 0.45f }, Player.Center);
                chargeParticleTick = 0;
            }
            ChargeRatio = ratio;
            IsCharging = true;

            //蓄力姿态：限制水平速度，强化"屈膝蹬地"的视觉
            Player.velocity.X *= 0.78f;
            if (MathF.Abs(Player.velocity.X) < 0.1f) {
                Player.velocity.X = 0f;
            }

            //粒子节奏：高蓄力时每帧都喷，低蓄力时每 3 帧一次，避免低进度阶段过载
            chargeParticleTick++;
            int interval = ratio > 0.6f ? 1 : (ratio > 0.3f ? 2 : 3);
            if (chargeParticleTick >= interval) {
                chargeParticleTick = 0;
                SpawnChargeParticles(ratio);
            }
        }

        /// <summary>
        /// 由 <see cref="OmniElectricFootSkill.OnChargeRelease"/> 在玩家松开方向键瞬间调用
        /// <br/>蓄力比例过低或处于空中时视为无效释放，仅播一次清空粒子
        /// </summary>
        public void RadialReleaseCharge(float ratio) {
            OmniElectricFoot equipped = OmniElectricFoot.GetEquipped(Player);
            if (equipped == null) {
                ChargeRatio = 0f;
                IsCharging = false;
                return;
            }
            ratio = MathHelper.Clamp(ratio, 0f, 1f);
            if (ratio < 0.05f || !IsOnGround) {
                ChargeRatio = 0f;
                IsCharging = false;
                return;
            }
            ReleaseChargeJump(ratio);
        }

        /// <summary>
        /// 由 <see cref="OmniElectricFootSkill.OnChargeCancel"/> 在玩家把光标移出扇区时调用
        /// <br/>仅清理视觉状态，不触发跳跃
        /// </summary>
        public void RadialCancelCharge() {
            ChargeRatio = 0f;
            IsCharging = false;
        }

        /// <summary>
        /// 释放蓄力跳：根据蓄力比例插值跳跃倍率，并附加可观的水平推力
        /// </summary>
        private void ReleaseChargeJump(float ratio) {
            float baseJumpSpeed = Player.jumpSpeed;
            if (baseJumpSpeed < 4f) {
                baseJumpSpeed = 5.01f;
            }
            float mul = MathHelper.Lerp(OmniElectricFoot.MinChargeJumpMul, OmniElectricFoot.MaxChargeJumpMul, ratio);

            Player.velocity.Y = -baseJumpSpeed * mul * Player.gravDir;

            float horizontalBoost = MathHelper.Lerp(0f, 4.5f, ratio) * Player.direction;
            if (Player.controlLeft) {
                horizontalBoost = -MathF.Abs(horizontalBoost);
            }
            else if (Player.controlRight) {
                horizontalBoost = MathF.Abs(horizontalBoost);
            }
            Player.velocity.X += horizontalBoost;

            Player.fallStart = (int)(Player.position.Y / 16f);

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f - ratio * 0.2f, Volume = 0.7f + ratio * 0.3f }, Player.Center);
            SpawnReleaseParticles(ratio);

            ChargeRatio = 0f;
            IsCharging = false;
            ReleaseCooldown = 12;
            CanDoubleJump = true;
        }

        /// <summary>
        /// 二段跳：与雷达完全解耦，只依赖原版 controlJump，保留 Terraria 玩家的肌肉记忆
        /// </summary>
        private void UpdateDoubleJump(OmniElectricFoot equipped) {
            if (IsOnGround) {
                return;
            }
            if (Player.mount.Active || Player.grappling[0] >= 0) {
                return;
            }
            if (!CanDoubleJump) {
                return;
            }

            //"刚按下跳跃键"：controlJump + releaseJump 的组合
            if (!Player.controlJump || !Player.releaseJump) {
                return;
            }

            if (Player.pulley || Player.sleeping.isSleeping) {
                return;
            }

            CanDoubleJump = false;

            Player.velocity.Y = -OmniElectricFoot.DoubleJumpSpeed * Player.gravDir;

            float horizontalKick = 0f;
            if (Player.controlLeft) {
                horizontalKick = -2.4f;
            }
            else if (Player.controlRight) {
                horizontalKick = 2.4f;
            }
            Player.velocity.X += horizontalKick;

            Player.releaseJump = false;
            Player.jump = 0;
            Player.fallStart = (int)(Player.position.Y / 16f);

            SoundEngine.PlaySound(SoundID.DoubleJump with { Pitch = 0.3f, Volume = 0.85f }, Player.Center);
            SpawnDoubleJumpParticles();
        }

        /// <summary>
        /// 简化版地面判定：原版 player.OnGround 在某些边缘情况下不可靠
        /// </summary>
        private static bool DetectOnGround(Player player) {
            float verticalSpeed = player.velocity.Y * player.gravDir;
            return verticalSpeed >= -0.05f && verticalSpeed <= 0.05f && (player.jump <= 0);
        }

        private void SpawnChargeParticles(float ratio) {
            int count = ratio > 0.85f ? 3 : (ratio > 0.5f ? 2 : 1);
            for (int i = 0; i < count; i++) {
                Vector2 offset = new(Main.rand.NextFloat(-Player.width * 0.5f, Player.width * 0.5f),
                    Player.height * 0.5f * Player.gravDir);
                Vector2 vel = new(Main.rand.NextFloat(-1.2f, 1.2f),
                    -Main.rand.NextFloat(0.8f, 2.4f) * Player.gravDir * (0.6f + ratio));
                Dust dust = Dust.NewDustPerfect(Player.Center + offset, DustID.MartianSaucerSpark, vel,
                    100, default, 0.9f + ratio * 0.7f);
                dust.noGravity = true;
            }
        }

        private void SpawnReleaseParticles(float ratio) {
            int particleCount = (int)MathHelper.Lerp(12, 30, ratio);
            for (int i = 0; i < particleCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                vel.Y = -MathF.Abs(vel.Y) * Player.gravDir * (0.5f + ratio);
                Dust dust = Dust.NewDustPerfect(Player.Bottom, DustID.MartianSaucerSpark, vel,
                    100, default, 1.2f + ratio * 0.6f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-1.5f, -0.2f) * Player.gravDir);
                Dust dust = Dust.NewDustPerfect(Player.Bottom, DustID.Smoke, vel, 130, default, 1.4f);
                dust.noGravity = false;
            }
        }

        private void SpawnDoubleJumpParticles() {
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18f;
                Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
                Dust dust = Dust.NewDustPerfect(Player.Bottom, DustID.MartianSaucerSpark, dir * 3.5f,
                    100, default, 1.2f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.5f, 1.5f);
                Dust dust = Dust.NewDustPerfect(Player.Bottom, DustID.Electric, vel, 100, default, 1.4f);
                dust.noGravity = true;
            }
        }
    }
}
