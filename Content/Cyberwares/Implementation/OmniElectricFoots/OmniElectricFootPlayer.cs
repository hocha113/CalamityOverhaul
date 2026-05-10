using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足的玩家组件
    /// <br/>负责处理：
    /// <list type="bullet">
    ///   <item>蓄力跳的输入采集、能量积累、释放与冷却管理</item>
    ///   <item>空中二段跳的解锁条件、按键消耗与速度施加</item>
    ///   <item>所有视觉粒子与音效反馈，全部本地化在本机玩家上不影响多人同步</item>
    /// </list>
    /// 仅在本机玩家上完成蓄力进度的累积，远程玩家不会执行任何输入相关逻辑
    /// </summary>
    internal class OmniElectricFootPlayer : ModPlayer
    {
        /// <summary>
        /// 当前蓄力跳的能量进度（0~1），公开供 HUD 读取
        /// </summary>
        public float ChargeRatio { get; private set; }

        /// <summary>
        /// 是否正在蓄力（受按键持续按下与"在地面"双重条件约束）
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
        /// 是否处于"未绑定脚踏地面"的状态，便于 HUD 在空中淡出蓄力 UI
        /// </summary>
        public bool IsOnGround { get; private set; }

        /// <summary>
        /// 上一帧的 Y 速度，用于检测起跳瞬间
        /// </summary>
        private float lastVelocityY;

        /// <summary>
        /// 上一帧是否绑定地面，用于检测离地、落地状态切换
        /// </summary>
        private bool wasGroundedLastFrame;

        /// <summary>
        /// 上一帧蓄力按键是否处于按下状态，用于在地面/空中状态切换时正确清理蓄力
        /// </summary>
        private bool wasKeyHeldLastFrame;

        public override void ResetEffects() {
            //冷却统一在 ResetEffects 阶段递减，确保多人同步与本地表现一致
            if (ReleaseCooldown > 0) {
                ReleaseCooldown--;
            }
        }

        public override void PostUpdate() {
            OmniElectricFoot equipped = OmniElectricFoot.GetEquipped(Player);
            if (equipped == null) {
                //卸下义足后立即清空所有状态，防止 HUD 残留
                ChargeRatio = 0f;
                IsCharging = false;
                CanDoubleJump = false;
                wasKeyHeldLastFrame = false;
                wasGroundedLastFrame = false;
                lastVelocityY = Player.velocity.Y;
                return;
            }

            //仅本机玩家执行输入相关逻辑，避免远程玩家被本地按键状态污染
            bool isLocal = Player.whoAmI == Main.myPlayer;

            IsOnGround = DetectOnGround(Player);

            //落地瞬间重置二段跳额度，并复位蓄力进度
            if (IsOnGround && !wasGroundedLastFrame) {
                CanDoubleJump = true;
            }

            //检测原版起跳动作：上一帧速度为 0 / 正向（站立或下落），本帧变为明显向上速度
            if (lastVelocityY >= 0f && Player.velocity.Y * Player.gravDir < -0.1f && wasGroundedLastFrame) {
                //首次起跳后，二段跳额度仍保留，直到玩家在空中消耗它
                CanDoubleJump = true;
            }

            if (isLocal) {
                UpdateChargeJump(equipped);
                UpdateDoubleJump(equipped);
            }

            wasGroundedLastFrame = IsOnGround;
            lastVelocityY = Player.velocity.Y;
        }

        /// <summary>
        /// 蓄力跳逻辑：地面 + 长按蓄力，松开释放或在按住的同时离地强制取消
        /// </summary>
        private void UpdateChargeJump(OmniElectricFoot equipped) {
            if (CWRKeySystem.CyberwareSkill_Key == null) {
                return;
            }
            bool keyHeld = CWRKeySystem.CyberwareSkill_Key.Current;
            bool justReleased = !keyHeld && wasKeyHeldLastFrame;

            //冷却中禁止开始新的蓄力，避免快速点按造成的视觉跳变
            if (ReleaseCooldown > 0) {
                IsCharging = false;
                ChargeRatio = MathF.Max(0f, ChargeRatio - 0.05f);
                wasKeyHeldLastFrame = keyHeld;
                return;
            }

            //蓄力前置条件：脚踏地面、未在使用其他通用动作（飞行/挂钩等不影响起跳的简化判定）
            bool canBeginCharge = IsOnGround && !Player.mount.Active && Player.grappling[0] < 0;

            if (keyHeld && canBeginCharge) {
                if (!IsCharging) {
                    IsCharging = true;
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.4f, Volume = 0.45f }, Player.Center);
                }

                //每帧累积蓄力，越接近满档前 80% 越快，最后 20% 略慢以保留"全力一蹬"的反馈
                float step = 1f / OmniElectricFoot.FullChargeTicks;
                if (ChargeRatio > 0.8f) {
                    step *= 0.6f;
                }
                ChargeRatio = MathF.Min(1f, ChargeRatio + step);

                //蓄力期间限制水平移动，模拟蓄力姿态
                Player.velocity.X *= 0.78f;
                if (MathF.Abs(Player.velocity.X) < 0.1f) {
                    Player.velocity.X = 0f;
                }

                SpawnChargeParticles(ChargeRatio);
            }
            else {
                //松开按键 / 离开地面 / 其他外部条件中断
                if (IsCharging) {
                    if (justReleased && ChargeRatio > 0.05f && IsOnGround) {
                        ReleaseChargeJump(equipped, ChargeRatio);
                    }
                    IsCharging = false;
                }
                //蓄力进度自然回落，提供视觉过渡
                if (ChargeRatio > 0f) {
                    ChargeRatio = MathF.Max(0f, ChargeRatio - 0.04f);
                }
            }

            wasKeyHeldLastFrame = keyHeld;
        }

        /// <summary>
        /// 释放蓄力跳：根据蓄力比例插值跳跃倍率，并附加可观的水平推力
        /// </summary>
        private void ReleaseChargeJump(OmniElectricFoot equipped, float ratio) {
            float baseJumpSpeed = Player.jumpSpeed;
            //jumpSpeed 在原版里偶尔为 0（如挂钩状态），保底一个"普通跳"参考值，避免蓄满 = 0
            if (baseJumpSpeed < 4f) {
                baseJumpSpeed = 5.01f;
            }
            float mul = MathHelper.Lerp(OmniElectricFoot.MinChargeJumpMul, OmniElectricFoot.MaxChargeJumpMul, ratio);

            //垂直分量：朝重力反向施加跳跃速度
            Player.velocity.Y = -baseJumpSpeed * mul * Player.gravDir;

            //水平推力：保留玩家朝向，提供"贴墙起跳"般的弹射感
            float horizontalBoost = MathHelper.Lerp(0f, 4.5f, ratio) * Player.direction;
            //方向键提供合速度的明确目标，避免玩家在原地起跳被强行推离
            if (Player.controlLeft) {
                horizontalBoost = -MathF.Abs(horizontalBoost);
            }
            else if (Player.controlRight) {
                horizontalBoost = MathF.Abs(horizontalBoost);
            }
            Player.velocity.X += horizontalBoost;

            //跳起后立即"离地"，原版 Player.justJumped 会在下一帧自然刷新
            Player.fallStart = (int)(Player.position.Y / 16f);

            //音效与粒子作为强烈的释放反馈
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f - ratio * 0.2f, Volume = 0.7f + ratio * 0.3f }, Player.Center);
            SpawnReleaseParticles(ratio);

            ChargeRatio = 0f;
            ReleaseCooldown = 12;
            CanDoubleJump = true;
        }

        /// <summary>
        /// 二段跳逻辑：玩家处于空中且二段额度未消耗时，按下"跳"键即触发
        /// <br/>这里使用原版 controlJump 的"刚按下"事件而不是新增独立按键，符合 Terraria 的玩家肌肉记忆
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

            //"刚按下跳跃键"的检测：通过 controlJump + releaseJump 完成
            //releaseJump 在原版中代表"上一帧未按下"，组合即为"本帧首次按下"
            if (!Player.controlJump || !Player.releaseJump) {
                return;
            }

            //避免与原版"在挂钩或床上"的特殊跳跃冲突
            if (Player.pulley || Player.sleeping.isSleeping) {
                return;
            }

            CanDoubleJump = false;

            //空中二段跳：固定速度 + 朝向方向轻推
            Player.velocity.Y = -OmniElectricFoot.DoubleJumpSpeed * Player.gravDir;

            float horizontalKick = 0f;
            if (Player.controlLeft) {
                horizontalKick = -2.4f;
            }
            else if (Player.controlRight) {
                horizontalKick = 2.4f;
            }
            //保留玩家原有的水平动量，水平踢出量只是叠加而非覆盖
            Player.velocity.X += horizontalKick;

            //避免本帧的 controlJump 仍被原版 Player.JumpMovement 二次响应
            Player.releaseJump = false;
            Player.jump = 0;
            Player.fallStart = (int)(Player.position.Y / 16f);

            SoundEngine.PlaySound(SoundID.DoubleJump with { Pitch = 0.3f, Volume = 0.85f }, Player.Center);
            SpawnDoubleJumpParticles();
        }

        /// <summary>
        /// 简化版地面判定：原版 player.OnGround 在某些边缘情况下不可靠（如轻微速度抖动）
        /// 这里同时考虑速度与原版 controlJump 的可用性，覆盖 99% 的常规场景
        /// </summary>
        private static bool DetectOnGround(Player player) {
            //gravDir = -1 时玩家倒立行走，速度方向同样反转
            float verticalSpeed = player.velocity.Y * player.gravDir;
            //速度近似为 0 + 玩家可执行普通跳跃即视为在地面
            return verticalSpeed >= -0.05f && verticalSpeed <= 0.05f && (player.jump <= 0);
        }

        /// <summary>
        /// 蓄力期间在足部生成淡蓝色电弧粒子，强度随蓄力进度线性增长
        /// </summary>
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

        /// <summary>
        /// 释放蓄力跳时的爆点粒子，伴随地面冲击的弧形电流
        /// </summary>
        private void SpawnReleaseParticles(float ratio) {
            int particleCount = (int)MathHelper.Lerp(12, 30, ratio);
            for (int i = 0; i < particleCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                vel.Y = -MathF.Abs(vel.Y) * Player.gravDir * (0.5f + ratio);
                Dust dust = Dust.NewDustPerfect(Player.Bottom, DustID.MartianSaucerSpark, vel,
                    100, default, 1.2f + ratio * 0.6f);
                dust.noGravity = true;
            }
            //侧向蹬地烟尘
            for (int i = 0; i < 6; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-1.5f, -0.2f) * Player.gravDir);
                Dust dust = Dust.NewDustPerfect(Player.Bottom, DustID.Smoke, vel, 130, default, 1.4f);
                dust.noGravity = false;
            }
        }

        /// <summary>
        /// 空中二段跳的环形电弧粒子，模拟"踩在空气里"的视觉效果
        /// </summary>
        private void SpawnDoubleJumpParticles() {
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18f;
                Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
                Dust dust = Dust.NewDustPerfect(Player.Bottom, DustID.MartianSaucerSpark, dir * 3.5f,
                    100, default, 1.2f);
                dust.noGravity = true;
            }
            //中心闪光
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.5f, 1.5f);
                Dust dust = Dust.NewDustPerfect(Player.Bottom, DustID.Electric, vel, 100, default, 1.4f);
                dust.noGravity = true;
            }
        }
    }
}
