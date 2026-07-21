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
            //单源单减
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
            Player.GetCritChance(DamageClass.Generic) += SCCA32CRP.CritChanceBonus;
            Player.moveSpeed += SCCA32CRP.MoveSpeedBonus;

            if (ReflexBoostTimer > 0) {
                Player.GetCritChance(DamageClass.Generic) += SCCA32CRP.BoostExtraCrit;
                Player.moveSpeed += SCCA32CRP.BoostExtraMoveSpeed;
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
            //SourceDamage≤0 不耗冷却
            if (info.SourceDamage <= 0) {
                return false;
            }

            //FreeDodge 仅本机，概率无需同步
            if (Main.rand.NextFloat() > SCCA32CRP.DodgeChance) {
                return false;
            }

            //冷却+亢奋+短无敌
            DodgeCooldownTimer = SCCA32CRP.DodgeCooldownFrames;
            dodgeCooldownCarry = 0f;
            ReflexBoostTimer = SCCA32CRP.ReflexBoostFrames;
            reflexBoostCarry = 0f;
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, SCCA32CRP.DodgeImmunityFrames);
            Player.immuneNoBlink = false;

            SoundEngine.PlaySound(SoundID.Item68 with { Pitch = 0.4f, Volume = 0.7f }, Player.Center);
            SpawnDodgeBurst();

            return true;
        }

        /// <summary>反射爆点粒子</summary>
        private void SpawnDodgeBurst() {
            int count = 22;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(2.6f, 4.4f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.YellowTorch, vel, 100, default, 1.3f);
                dust.noGravity = true;
            }
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
