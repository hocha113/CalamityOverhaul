using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RAMSystems;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals
{
    /// <summary>
    /// 自骇水晶向 RAM 系统提供的修饰器
    /// <br/>以单例方式挂入 <see cref="RamSystem"/>，每帧聚合时自查装备状态决定是否生效，
    /// 与 <see cref="CstmVisualEyes.CstmVisualEyeRamProvider"/> 保持完全一致的设计模式
    /// </summary>
    internal sealed class SelfHackCrystalRamProvider : IRamModifierProvider
    {
        public int MaxRamBonus => 0;
        public float RecoveryRateBonus => SelfHackCrystal.RamRecoveryBonus;
        public bool IsActive => SelfHackCrystal.GetEquipped(Main.LocalPlayer) != null;
    }

    /// <summary>
    /// 自骇水晶的玩家组件
    /// <list type="bullet">
    ///   <item>OnEnterWorld 阶段把 RAM 修饰器挂入本机玩家的 <see cref="RamSystem"/></item>
    ///   <item>响应 <see cref="CWRKeySystem.CyberwareSkill_Key"/> 的"刚按下"事件触发自骇技能</item>
    ///   <item>维护本机玩家的技能冷却倒计时</item>
    /// </list>
    /// </summary>
    internal class SelfHackCrystalPlayer : ModPlayer
    {
        /// <summary>
        /// 自骇技能剩余冷却帧数，0 时可再次释放
        /// </summary>
        public int SkillCooldownTimer { get; private set; }

        public override void OnEnterWorld() {
            //仅本机玩家需把贡献项写入本机 RAM 列表，多人模式下其他玩家的实例不必参与本机聚合
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            RamSystem.RegisterProvider(new SelfHackCrystalRamProvider());
        }

        public override void ResetEffects() {
            if (SkillCooldownTimer > 0) {
                SkillCooldownTimer--;
            }
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (SelfHackCrystal.GetEquipped(Player) == null) {
                //未装备时清空冷却，使重新装备后立刻可用
                SkillCooldownTimer = 0;
                return;
            }
            if (CWRKeySystem.CyberwareSkill_Key?.JustPressed != true) {
                return;
            }

            //冷却中给出短促失败反馈，避免误以为按键失效
            if (SkillCooldownTimer > 0) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.4f, Volume = 0.5f }, Player.Center);
                return;
            }

            //RAM 不足同样视为失败：通知 RAM 系统进入不足闪烁，与其他需要 RAM 的功能保持一致
            if (!RamSystem.CanAfford(SelfHackCrystal.SkillRamCost)) {
                RamSystem.NotifyInsufficient();
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.6f, Volume = 0.5f }, Player.Center);
                return;
            }

            TryFireSelfHack();
        }

        /// <summary>
        /// 实际触发自骇：扣 RAM、清 debuff、给无敌、播粒子和音效
        /// </summary>
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

            //音效与粒子：清债越多反馈越强，让玩家直观感知"清理了多少负面"
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.4f, Volume = 0.7f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 0.6f }, Player.Center);
            SpawnSelfHackParticles(cleared);
        }

        /// <summary>
        /// 自骇释放粒子：以玩家为中心向外辐散青色脉冲环 + 几道斜向闪光
        /// </summary>
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
