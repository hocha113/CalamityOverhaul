using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SCCA32CRPs
{
    /// <summary>
    /// SCCA-32 CRP ModPlayer，常驻加成与反射闪避
    /// <br/>FreeDodge 截胡致命攻击；亢奋追加暴击/移速；冷却 DodgeCooldownFrames 帧
    /// </summary>
    internal class SCCA32CRPPlayer : ModPlayer
    {
        /// <summary>闪避冷却剩余帧</summary>
        public int DodgeCooldownTimer { get; private set; }
        private float dodgeCooldownCarry;

        /// <summary>亢奋剩余帧</summary>
        public int ReflexBoostTimer { get; private set; }
        private float reflexBoostCarry;

        /// <summary>亢奋剩余比例，HUD 接入用</summary>
        public float BoostRatio => SCCA32CRP.ReflexBoostFrames > 0
            ? MathHelper.Clamp((float)ReflexBoostTimer / SCCA32CRP.ReflexBoostFrames, 0f, 1f)
            : 0f;

        public override void ResetEffects() {
            //ResetEffects 统一递减，单源单减
            int dodgeTimer = DodgeCooldownTimer;
            BaseCyberware.TickFrameDown(ref dodgeTimer, ref dodgeCooldownCarry);
            DodgeCooldownTimer = dodgeTimer;

            int boostTimer = ReflexBoostTimer;
            BaseCyberware.TickFrameDown(ref boostTimer, ref reflexBoostCarry);
            ReflexBoostTimer = boostTimer;
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
            //仅 SourceDamage>0 才触发，防零伤害事件浪费冷却
            if (info.SourceDamage <= 0) {
                return false;
            }

            //FreeDodge 仅受击玩家本机调用，概率结果无联机同步问题
            if (Main.rand.NextFloat() > SCCA32CRP.DodgeChance) {
                return false;
            }

            //成功反射：进入冷却 + 亢奋 + 短暂无敌窗口
            DodgeCooldownTimer = SCCA32CRP.DodgeCooldownFrames;
            dodgeCooldownCarry = 0f;
            ReflexBoostTimer = SCCA32CRP.ReflexBoostFrames;
            reflexBoostCarry = 0f;
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, SCCA32CRP.DodgeImmunityFrames);
            Player.immuneNoBlink = false;

            //音效与粒子作为强烈的反射反馈
            SoundEngine.PlaySound(SoundID.Item68 with { Pitch = 0.4f, Volume = 0.7f }, Player.Center);
            SpawnDodgeBurst();

            return true;
        }

        /// <summary>反射触发爆点粒子</summary>
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

        /// <summary>亢奋拖尾粒子</summary>
        private void SpawnBoostTrail() {
            Vector2 offset = new(Main.rand.NextFloat(-Player.width * 0.4f, Player.width * 0.4f),
                Main.rand.NextFloat(-Player.height * 0.45f, Player.height * 0.45f));
            Vector2 vel = new(Player.velocity.X * -0.2f, Player.velocity.Y * -0.2f);
            Dust dust = Dust.NewDustPerfect(Player.Center + offset, DustID.YellowTorch, vel, 130, default, 0.95f);
            dust.noGravity = true;
        }
    }
}
