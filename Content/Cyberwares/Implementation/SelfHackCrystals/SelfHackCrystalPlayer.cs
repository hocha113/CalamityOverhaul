using CalamityOverhaul.Content.RAMSystems;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals
{
    /// <summary>自骇水晶 RAM 修饰器</summary>
    internal sealed class SelfHackCrystalRamProvider : IRamModifierProvider, ICWRLoader
    {
        public int MaxRamBonus => 0;
        public float RecoveryRateBonus => SelfHackCrystal.RamRecoveryBonus;
        public bool IsActive(Player player) => SelfHackCrystal.GetEquipped(player) != null;

        void ICWRLoader.LoadData() => RamSystem.RegisterProvider(this);
        void ICWRLoader.UnLoadData() => RamSystem.UnregisterProvider(this);
    }

    /// <summary>
    /// 自骇水晶 ModPlayer，技能冷却
    /// <br/>CyberwareSkill_Key 刚按下经 Skill.OnInstantTrigger 触发自骇
    /// </summary>
    internal class SelfHackCrystalPlayer : ModPlayer
    {
        /// <summary>技能冷却剩余帧，0 可释放</summary>
        public int SkillCooldownTimer { get; private set; }
        internal uint StateRevision { get; private set; } = 1;
        private float skillCooldownCarry;

        public override void ResetEffects() {
            int before = SkillCooldownTimer;
            int timer = SkillCooldownTimer;
            BaseCyberware.TickFrameDown(ref timer, ref skillCooldownCarry);
            SkillCooldownTimer = timer;
            if (before > 0 && timer == 0
                && Main.netMode != NetmodeID.MultiplayerClient) {
                AdvanceRevision();
                SelfHackCrystalNet.SendState(Player);
            }
        }

        public override void PostUpdate() {
            if (SelfHackCrystal.GetEquipped(Player) == null
                && SkillCooldownTimer > 0) {
                SkillCooldownTimer = 0;
                skillCooldownCarry = 0f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    AdvanceRevision();
                    SelfHackCrystalNet.SendState(Player);
                }
            }
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server) {
                SelfHackCrystalNet.SendState(Player, toWho);
            }
        }

        public override void PlayerDisconnect() {
            SkillCooldownTimer = 0;
            skillCooldownCarry = 0f;
            StateRevision = 1;
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

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                if (!SelfHackCrystalNet.SendRequest(Player)) {
                    PlayFailure();
                }
                return;
            }

            if (TryFireSelfHackAuthority(out _, out _)
                != SelfHackResultCode.Success) {
                PlayFailure();
            }
        }

        internal SelfHackResultCode TryFireSelfHackAuthority(out float paid,
            out int cleared) {
            paid = 0f;
            cleared = 0;
            if (Main.netMode == NetmodeID.MultiplayerClient
                || Player?.active != true || !Player.Alives()) {
                return SelfHackResultCode.InvalidPlayer;
            }
            if (SelfHackCrystal.GetEquipped(Player) == null) {
                return SelfHackResultCode.MissingCyberware;
            }
            if (SkillCooldownTimer > 0) {
                return SelfHackResultCode.Cooldown;
            }
            if (!CalamityOverhaul.Content.HackTimes.HackTime.InfiniteHackAuthority
                && !RamSystem.TryConsume(Player, SelfHackCrystal.SkillRamCost,
                    out paid)) {
                return SelfHackResultCode.InsufficientRam;
            }

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

            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, SelfHackCrystal.ImmunityFrames);
            Player.immuneNoBlink = false;

            SkillCooldownTimer = SelfHackCrystal.SkillCooldown;
            skillCooldownCarry = 0f;
            AdvanceRevision();

            if (Main.netMode == NetmodeID.SinglePlayer) {
                PlayActivationVisuals(cleared);
            }
            return SelfHackResultCode.Success;
        }

        internal bool ApplyReplicatedState(uint revision, int cooldown,
            bool playActivation) {
            if (revision == 0 || cooldown < 0
                || cooldown > SelfHackCrystal.SkillCooldown
                || !IsRevisionAtLeast(revision, StateRevision)) {
                return false;
            }
            bool newlyActivated = playActivation && cooldown > 0
                && (revision != StateRevision || SkillCooldownTimer <= 0);
            StateRevision = revision;
            SkillCooldownTimer = cooldown;
            skillCooldownCarry = 0f;
            if (newlyActivated && !Main.dedServ) {
                PlayActivationVisuals(0);
            }
            return true;
        }

        internal void PlayFailure() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            RamSystem.NotifyInsufficient();
            SoundEngine.PlaySound(SoundID.MenuTick with {
                Pitch = -0.6f,
                Volume = 0.5f,
            }, Player.Center);
        }

        private void PlayActivationVisuals(int cleared) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with {
                Pitch = 0.4f,
                Volume = 0.7f,
            }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item122 with {
                Pitch = 0.2f,
                Volume = 0.6f,
            }, Player.Center);
            SpawnSelfHackParticles(cleared);
        }

        private void AdvanceRevision() {
            StateRevision++;
            if (StateRevision == 0) {
                StateRevision = 1;
            }
        }

        /// <summary>自骇粒子，环数随 cleared 提升</summary>
        private void SpawnSelfHackParticles(int debuffsCleared) {
            int rings = 24 + Math.Min(debuffsCleared, 6) * 4;
            for (int i = 0; i < rings; i++) {
                float angle = MathHelper.TwoPi * i / rings;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(2.6f, 4.2f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.MartianSaucerSpark, vel, 100, default, 1.25f);
                dust.noGravity = true;
            }

            for (int i = 0; i < 6; i++) {
                Vector2 dir = Main.rand.NextVector2Unit();
                for (int k = 0; k < 8; k++) {
                    Dust dust = Dust.NewDustPerfect(Player.Center + dir * (k * 4f),
                        DustID.Electric, dir * 1.5f, 100, default, 1.0f);
                    dust.noGravity = true;
                }
            }

            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.Electric, vel, 100, default, 1.4f);
                dust.noGravity = true;
            }
        }

        private static bool IsRevisionAtLeast(uint candidate, uint baseline)
            => candidate == baseline || unchecked((int)(candidate - baseline)) > 0;
    }
}
