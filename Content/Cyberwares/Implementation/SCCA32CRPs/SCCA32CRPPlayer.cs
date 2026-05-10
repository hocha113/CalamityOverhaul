using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SCCA32CRPs
{
    /// <summary>
    /// SCCA-32 CRP 的玩家组件
    /// <list type="bullet">
    ///   <item>常驻：在 PostUpdateEquips/UpdateEquips 阶段写入暴击率与移速加成</item>
    ///   <item>反射闪避：通过 <see cref="ModPlayer.FreeDodge"/> 截胡致命攻击，按概率与冷却决定是否生效</item>
    ///   <item>反射亢奋：触发后进入有限帧的增益状态，再追加暴击率与移速</item>
    /// </list>
    /// 反射闪避有 <see cref="SCCA32CRP.DodgeCooldownFrames"/> 内部冷却，
    /// 与原版/其他来源的闪避独立运行
    /// </summary>
    internal class SCCA32CRPPlayer : ModPlayer
    {
        /// <summary>
        /// 闪避冷却剩余帧数，0 时可再次触发反射闪避
        /// </summary>
        public int DodgeCooldownTimer { get; private set; }

        /// <summary>
        /// 反射亢奋剩余帧数，0 时退出亢奋状态
        /// </summary>
        public int ReflexBoostTimer { get; private set; }

        /// <summary>
        /// 公开亢奋剩余比例，便于将来对 HUD/视觉做接入
        /// </summary>
        public float BoostRatio => SCCA32CRP.ReflexBoostFrames > 0
            ? MathHelper.Clamp((float)ReflexBoostTimer / SCCA32CRP.ReflexBoostFrames, 0f, 1f)
            : 0f;

        public override void ResetEffects() {
            //冷却与亢奋计时统一在 ResetEffects 阶段递减，确保单源单减
            if (DodgeCooldownTimer > 0) {
                DodgeCooldownTimer--;
            }
            if (ReflexBoostTimer > 0) {
                ReflexBoostTimer--;
            }
        }

        public override void PostUpdateEquips() {
            if (SCCA32CRP.GetEquipped(Player) == null) {
                return;
            }
            //常驻基础加成：暴击率作用于通用伤害类，所有派生类都会受益
            Player.GetCritChance(DamageClass.Generic) += SCCA32CRP.CritChanceBonus;
            Player.moveSpeed += SCCA32CRP.MoveSpeedBonus;

            //亢奋状态额外加成
            if (ReflexBoostTimer > 0) {
                Player.GetCritChance(DamageClass.Generic) += SCCA32CRP.BoostExtraCrit;
                Player.moveSpeed += SCCA32CRP.BoostExtraMoveSpeed;
                //亢奋期间每隔几帧泼洒一束反射粒子，强化"亚意识接管"的感觉
                if (Player.whoAmI == Main.myPlayer && Main.GameUpdateCount % 6 == 0) {
                    SpawnBoostTrail();
                }
            }
        }

        public override bool FreeDodge(Player.HurtInfo info) {
            if (SCCA32CRP.GetEquipped(Player) == null) {
                return false;
            }
            if (DodgeCooldownTimer > 0) {
                return false;
            }
            //仅造成实际伤害的攻击才允许反射，避免触发"零伤害"的伤害事件浪费冷却
            if (info.SourceDamage <= 0) {
                return false;
            }

            //按概率判定。Main.rand 在多人模式下双方都共享一致的种子，但 FreeDodge 仅在受击玩家本机调用，
            //因此即便概率结果存在差异也只影响该玩家自身，无同步问题
            if (Main.rand.NextFloat() > SCCA32CRP.DodgeChance) {
                return false;
            }

            //成功反射：进入冷却 + 亢奋 + 短暂无敌窗口
            DodgeCooldownTimer = SCCA32CRP.DodgeCooldownFrames;
            ReflexBoostTimer = SCCA32CRP.ReflexBoostFrames;
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, SCCA32CRP.DodgeImmunityFrames);
            Player.immuneNoBlink = false;

            //音效与粒子作为强烈的反射反馈
            SoundEngine.PlaySound(SoundID.Item68 with { Pitch = 0.4f, Volume = 0.7f }, Player.Center);
            SpawnDodgeBurst();

            return true;
        }

        /// <summary>
        /// 反射触发瞬间的爆点：环形闪光 + 数道朝向攻击源的反方向尾迹
        /// </summary>
        private void SpawnDodgeBurst() {
            //外环
            int count = 22;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(2.6f, 4.4f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.YellowTorch, vel, 100, default, 1.3f);
                dust.noGravity = true;
            }
            //中心闪光
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.GoldFlame, vel, 100, default, 1.6f);
                dust.noGravity = true;
            }
        }

        /// <summary>
        /// 亢奋期间的拖尾粒子：贴在玩家身上随机偏移，颜色与反射闪避一致
        /// </summary>
        private void SpawnBoostTrail() {
            Vector2 offset = new(Main.rand.NextFloat(-Player.width * 0.4f, Player.width * 0.4f),
                Main.rand.NextFloat(-Player.height * 0.45f, Player.height * 0.45f));
            Vector2 vel = new(Player.velocity.X * -0.2f, Player.velocity.Y * -0.2f);
            Dust dust = Dust.NewDustPerfect(Player.Center + offset, DustID.YellowTorch, vel, 130, default, 0.95f);
            dust.noGravity = true;
        }
    }
}
