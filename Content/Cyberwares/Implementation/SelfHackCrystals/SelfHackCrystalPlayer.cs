using CalamityOverhaul.Content.Cyberwares;
using CalamityOverhaul.Content.RAMSystems;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals
{
    /// <summary>自骇水晶 RAM 修饰器，OnEnterWorld 挂入，IsActive 自查装备</summary>
    internal sealed class SelfHackCrystalRamProvider : IRamModifierProvider
    {
        public int MaxRamBonus => 0;
        public float RecoveryRateBonus => SelfHackCrystal.RamRecoveryBonus;
        public bool IsActive => SelfHackCrystal.GetEquipped(Main.LocalPlayer) != null;
    }

    /// <summary>
    /// 自骇水晶 ModPlayer，RAM 注册与技能冷却
    /// <br/>CyberwareSkill_Key 刚按下经 Skill.OnInstantTrigger 触发自骇
    /// </summary>
    internal class SelfHackCrystalPlayer : ModPlayer
    {
        /// <summary>技能冷却剩余帧，0 可释放</summary>
        public int SkillCooldownTimer { get; private set; }
        private float skillCooldownCarry;

        public override void OnEnterWorld() {
            //仅本机玩家需把贡献项写入本机 RAM 列表，多人模式下其他玩家的实例不必参与本机聚合
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            RamSystem.RegisterProvider(new SelfHackCrystalRamProvider());
        }

        public override void ResetEffects() {
            int timer = SkillCooldownTimer;
            BaseCyberware.TickFrameDown(ref timer, ref skillCooldownCarry);
            SkillCooldownTimer = timer;
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (SelfHackCrystal.GetEquipped(Player) == null) {
                //未装备时清空冷却，使重新装备后立刻可用
                SkillCooldownTimer = 0;
                skillCooldownCarry = 0f;
            }
        }

        /// <summary>Skill.OnInstantTrigger 入口，冷却/RAM 不足播失败音</summary>
        public void TryFireSelfHackFromRadial() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (SelfHackCrystal.GetEquipped(Player) == null) {
                return;
            }

            if (SkillCooldownTimer > 0) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.4f, Volume = 0.5f }, Player.Center);
                return;
            }

            if (!RamSystem.CanAfford(SelfHackCrystal.SkillRamCost)) {
                RamSystem.NotifyInsufficient();
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.6f, Volume = 0.5f }, Player.Center);
                return;
            }

            TryFireSelfHack();
        }

        /// <summary>扣 RAM、清 debuff、给无敌，immuneTime 取 max 防覆盖</summary>
        private void TryFireSelfHack() {
            if (!RamSystem.TryConsume(SelfHackCrystal.SkillRamCost)) {
                //保险：极端时序下 RAM 已被其他事件耗尽
                RamSystem.NotifyInsufficient();
                return;
            }

            //清除全部 debuff，保留正面 buff
            int cleared = 0;
            for (int i = 0; i < Player.MaxBuffs; i++) {
                int buffType = Player.buffType[i];
                if (buffType <= 0) {
                    continue;
                }
                if (Main.debuff[buffType]) {
                    Player.DelBuff(i);
                    i--;
                    cleared++;
                }
            }

            //无敌帧：取 max 避免覆盖更长的现有无敌
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, SelfHackCrystal.ImmunityFrames);
            Player.immuneNoBlink = false;

            //冷却进入计时
            SkillCooldownTimer = SelfHackCrystal.SkillCooldown;
            skillCooldownCarry = 0f;

            //音效与粒子：清债越多反馈越强，让玩家直观感知"清理了多少负面"
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.4f, Volume = 0.7f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 0.6f }, Player.Center);
            SpawnSelfHackParticles(cleared);
        }

        /// <summary>自骇粒子，环数随 cleared debuff 数提升</summary>
        private void SpawnSelfHackParticles(int debuffsCleared) {
            //外环脉冲粒子，数量随被清的 debuff 数提升
            int rings = 24 + Math.Min(debuffsCleared, 6) * 4;
            for (int i = 0; i < rings; i++) {
                float angle = MathHelper.TwoPi * i / rings;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(2.6f, 4.2f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.MartianSaucerSpark, vel, 100, default, 1.25f);
                dust.noGravity = true;
            }

            //斜向闪光线条
            for (int i = 0; i < 6; i++) {
                Vector2 dir = Main.rand.NextVector2Unit();
                for (int k = 0; k < 8; k++) {
                    Dust dust = Dust.NewDustPerfect(Player.Center + dir * (k * 4f),
                        DustID.Electric, dir * 1.5f, 100, default, 1.0f);
                    dust.noGravity = true;
                }
            }

            //中心高光
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.Electric, vel, 100, default, 1.4f);
                dust.noGravity = true;
            }
        }
    }
}
