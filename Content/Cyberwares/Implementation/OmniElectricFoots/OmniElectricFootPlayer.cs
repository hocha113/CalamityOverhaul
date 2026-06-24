using CalamityOverhaul.Content.Cyberwares;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足 ModPlayer，二段跳与蓄力跳
    /// <br/>蓄力由 <see cref="OmniElectricFootSkill"/> 经 RadialDrive/Release/Cancel 驱动
    /// </summary>
    internal class OmniElectricFootPlayer : ModPlayer
    {
        /// <summary>蓄力进度 0~1，HUD 读取</summary>
        public float ChargeRatio { get; private set; }

        /// <summary>雷达蓄力中，HUD 显隐用</summary>
        public bool IsCharging { get; private set; }

        /// <summary>释放后冷却帧，防连点抖动</summary>
        public int ReleaseCooldown { get; private set; }
        private float releaseCooldownCarry;

        /// <summary>二段跳可用，落地/起跳重置</summary>
        public bool CanDoubleJump { get; private set; } = true;

        /// <summary>脚踏地面，雷达 IsReady 用</summary>
        public bool IsOnGround { get; private set; }

        //上帧 Y 速度，检起跳沿
        private float lastVelocityY;
        //上一帧是否绑定地面
        private bool wasGroundedLastFrame;
        //上帧蓄力态，外部断键时清姿态
        private bool wasChargingLastFrame;
        //蓄力姿态的最近一次喷射粒子时间，避免帧率叠加导致粒子爆量
        private int chargeParticleTick;

        public override void ResetEffects() {
            //冷却递减
            int releaseCd = ReleaseCooldown;
            BaseCyberware.TickFrameDown(ref releaseCd, ref releaseCooldownCarry);
            ReleaseCooldown = releaseCd;
            //快照上帧蓄力再复位，保证 IsCharging 严格等于本帧雷达是否驱动
            wasChargingLastFrame = IsCharging;
            IsCharging = false;
            //无驱动时 ChargeRatio 衰减，HUD 环自然收回
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

        /// <summary>雷达蓄力每帧回调，同步比例与粒子</summary>
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

        /// <summary>松开方向键释放蓄力跳，ratio&lt;0.05 或空中无效</summary>
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

        /// <summary>移出扇区取消蓄力，不跳跃</summary>
        public void RadialCancelCharge() {
            ChargeRatio = 0f;
            IsCharging = false;
        }

        /// <summary>蓄力跳：倍率插值 + 水平推力，ReleaseCooldown=12</summary>
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
            releaseCooldownCarry = 0f;
            CanDoubleJump = true;
        }

        /// <summary>二段跳，controlJump+releaseJump 触发，与雷达解耦</summary>
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

        /// <summary>简化地面判定，原版 OnGround 边缘不可靠</summary>
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
