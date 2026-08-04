using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Restart
{
    internal class CyberRestart : ICWRLoader
    {
        public const int RequiredLayer = 1;
        public const float RamCostPerCast = 6f;
        public const int RamLockFrames = 60 * 22;
        public const int PhaseTearEnd = 22;
        public const int PhaseCollapseEnd = 50;
        public const int PhaseSingularityEnd = 64;
        public const int PhaseBurstEnd = 92;
        public const int TotalFrames = PhaseBurstEnd;

        internal sealed class Runtime {
            internal int ProgressTimer;
            internal float ProgressCarry;
            internal int AnchorLayer;
            internal bool RestoreFired;
            internal uint Revision = 1;
        }

        private static readonly Dictionary<int, Runtime> runtimes = [];

        void ICWRLoader.UnLoadData() => Reset();

        private static Runtime GetRuntime(Player player, bool create = true) {
            if (player == null || player.whoAmI < 0
                || player.whoAmI >= Main.maxPlayers) {
                return null;
            }
            if (!runtimes.TryGetValue(player.whoAmI, out Runtime state)
                && create) {
                state = new Runtime();
                runtimes[player.whoAmI] = state;
            }
            return state;
        }

        private static Runtime LocalRuntime
            => Main.netMode == NetmodeID.Server ? null
            : GetRuntime(Main.LocalPlayer);

        public static bool IsActive => (LocalRuntime?.ProgressTimer ?? 0) > 0;

        public static float Progress {
            get {
                int timer = LocalRuntime?.ProgressTimer ?? 0;
                return timer <= 0 ? 0f
                    : MathHelper.Clamp(timer / (float)TotalFrames, 0f, 1f);
            }
        }

        public static int CooldownRemain => RamSystem.LockRemain;
        public static bool OnCooldown => RamSystem.IsLocked;

        public enum Phase
        {
            None,
            Tear,
            Collapse,
            Singularity,
            Burst,
        }

        public static Phase CurrentPhase {
            get {
                int timer = LocalRuntime?.ProgressTimer ?? 0;
                if (timer <= 0) return Phase.None;
                if (timer <= PhaseTearEnd) return Phase.Tear;
                if (timer <= PhaseCollapseEnd) return Phase.Collapse;
                if (timer <= PhaseSingularityEnd) return Phase.Singularity;
                return Phase.Burst;
            }
        }

        public static bool IsLocalPlayerHidden {
            get {
                int timer = LocalRuntime?.ProgressTimer ?? 0;
                return timer > PhaseCollapseEnd - 8
                    && timer <= PhaseSingularityEnd + 2;
            }
        }

        public static void TryRestart(Player owner) {
            if (owner == null || !owner.Alives()) {
                return;
            }
            Runtime state = GetRuntime(owner);
            if (state.ProgressTimer > 0 || RamSystem.IsLocked) {
                PlayFailure(owner);
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                if (!CyberspaceActionNet.SendActionRequest(owner,
                    CyberspaceActionKind.Restart, Vector2.Zero)) {
                    PlayFailure(owner);
                }
                return;
            }
            if (ExecuteAuthority(owner, out _) != CyberspaceActionResultCode.Success) {
                PlayFailure(owner);
            }
        }

        internal static CyberspaceActionResultCode ExecuteAuthority(Player owner,
            out float paid) {
            paid = 0f;
            if (Main.netMode == NetmodeID.MultiplayerClient
                || owner?.active != true || !owner.Alives()) {
                return CyberspaceActionResultCode.InvalidPlayer;
            }
            CyberspacePlayer domain = Cyberspace.For(owner);
            if (domain == null || !domain.Active || domain.Intensity < 0.5f
                || domain.CurrentLayer < RequiredLayer
                || owner.HeldItem.type != SHPCOverride.ID) {
                return CyberspaceActionResultCode.InvalidState;
            }
            Runtime state = GetRuntime(owner);
            if (state.ProgressTimer > 0 || RamSystem.IsLocked) {
                return CyberspaceActionResultCode.Cooldown;
            }
            if (!HackTime.InfiniteHackAuthority
                && !RamSystem.TryConsume(owner, RamCostPerCast, out paid)) {
                return CyberspaceActionResultCode.InsufficientRam;
            }

            Activate(owner, state);
            return CyberspaceActionResultCode.Success;
        }

        private static void Activate(Player owner, Runtime state) {
            state.ProgressTimer = 1;
            state.ProgressCarry = 0f;
            state.AnchorLayer = Math.Clamp(Cyberspace.For(owner).CurrentLayer,
                1, Cyberspace.MaxLayerCount);
            state.RestoreFired = false;
            AdvanceRevision(state);
            if (Main.netMode == NetmodeID.Server) {
                CyberspaceActionNet.SendRestartState(owner, state, true);
            }
            else {
                SpawnStartVFX(owner);
            }
        }

        public static void Update() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player owner = Main.player[i];
                if (owner?.active != true) {
                    continue;
                }
                Runtime state = GetRuntime(owner);
                CyberspacePlayer domain = Cyberspace.For(owner);
                if (state.ProgressTimer <= 0) {
                    if (domain != null && domain.RestartCollapse > 0f) {
                        domain.RestartCollapse = MathHelper.Lerp(
                            domain.RestartCollapse, 0f, 0.35f);
                        if (domain.RestartCollapse < 0.005f) {
                            domain.RestartCollapse = 0f;
                        }
                    }
                    continue;
                }

                if (domain == null) {
                    state.ProgressTimer = 0;
                    continue;
                }
                domain.RestartCollapse = ComputeCollapse(state.ProgressTimer);
                int restoreFrame = (PhaseCollapseEnd + PhaseSingularityEnd) / 2;
                if (!state.RestoreFired && state.ProgressTimer >= restoreFrame) {
                    state.RestoreFired = true;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        ApplyRestoreEffects(owner);
                    }
                }
                if (state.ProgressTimer == PhaseSingularityEnd + 1
                    && Main.netMode != NetmodeID.MultiplayerClient) {
                    if (Main.netMode == NetmodeID.Server) {
                        CyberspaceActionNet.SendRestartState(owner, state, true);
                    }
                    else {
                        SpawnBurstVFX(owner, state.AnchorLayer);
                    }
                }

                int advance = TimeGear.PullFrameAdvance(ref state.ProgressCarry);
                state.ProgressTimer += advance;
                if (state.ProgressTimer > TotalFrames) {
                    state.ProgressTimer = 0;
                    state.ProgressCarry = 0f;
                    state.RestoreFired = false;
                    domain.RestartCollapse = 0f;
                    AdvanceRevision(state);
                    if (Main.netMode == NetmodeID.Server) {
                        CyberspaceActionNet.SendRestartState(owner, state, false);
                    }
                }
            }
        }

        private static float ComputeCollapse(int timer) {
            if (timer <= PhaseTearEnd) {
                return MathHelper.Clamp(timer / (float)PhaseTearEnd * 0.05f,
                    0f, 0.05f);
            }
            if (timer <= PhaseCollapseEnd) {
                float k = (timer - PhaseTearEnd)
                    / (float)(PhaseCollapseEnd - PhaseTearEnd);
                return MathHelper.Lerp(0.05f, 1f, MathF.Pow(k, 2.2f));
            }
            if (timer <= PhaseSingularityEnd) {
                float k = (timer - PhaseCollapseEnd)
                    / (float)(PhaseSingularityEnd - PhaseCollapseEnd);
                return MathHelper.Clamp(0.96f
                    + MathF.Sin(k * MathF.PI * 2.5f) * 0.04f, 0.92f, 1f);
            }
            float burst = (timer - PhaseSingularityEnd)
                / (float)(PhaseBurstEnd - PhaseSingularityEnd);
            return MathHelper.Clamp(1f - (1f - MathF.Pow(1f - burst, 3f)),
                0f, 1f);
        }

        private static void ApplyRestoreEffects(Player owner) {
            if (owner?.active != true) {
                return;
            }
            owner.statLife = owner.statLifeMax2;
            owner.statMana = owner.statManaMax2;
            for (int i = 0; i < Player.MaxBuffs; i++) {
                int buffType = owner.buffType[i];
                if (buffType > 0 && Main.debuff[buffType]) {
                    owner.DelBuff(i);
                    i--;
                }
            }
            RamSystem.SystemLock(owner, RamLockFrames);
            owner.immune = true;
            owner.immuneTime = Math.Max(owner.immuneTime, 40);
            if (!VaultUtils.isServer && owner.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(CWRSound.Faultrelease with {
                    Volume = 0.85f,
                    Pitch = 0.25f,
                }, owner.Center);
                SoundEngine.PlaySound(CWRSound.FaultTransition with {
                    Volume = 0.55f,
                    Pitch = 0.4f,
                }, owner.Center);
                CombatText.NewText(owner.Hitbox, new Color(255, 220, 200),
                    "// REBOOT", true);
            }
        }

        private static void SpawnStartVFX(Player owner) {
            if (Main.dedServ || owner?.whoAmI != Main.myPlayer) {
                return;
            }
            IEntitySource source = owner.GetSource_FromThis();
            Projectile.NewProjectile(source, owner.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberRestartProj>(), 0, 0,
                owner.whoAmI);
            SoundEngine.PlaySound(CWRSound.FaultOccurred with {
                Volume = 0.7f,
                Pitch = -0.15f,
            }, owner.Center);
            SoundEngine.PlaySound(CWRSound.Fault with {
                Volume = 0.6f,
                Pitch = -0.35f,
            }, owner.Center);
        }

        internal static void SpawnBurstVFX(Player owner, int anchorLayer) {
            if (Main.dedServ || owner?.whoAmI != Main.myPlayer) {
                return;
            }
            IEntitySource source = owner.GetSource_FromThis();
            Vector2 center = owner.Center;
            Projectile.NewProjectile(source, center, Vector2.Zero,
                ModContent.ProjectileType<CyberShockwaveProj>(), 0, 0,
                owner.whoAmI);
            int boltCount = 6 + anchorLayer * 2;
            float baseAngle = Main.rand.NextFloat() * MathHelper.TwoPi;
            for (int i = 0; i < boltCount; i++) {
                float angle = baseAngle + MathHelper.TwoPi * i / boltCount
                    + Main.rand.NextFloat(-0.3f, 0.3f);
                Projectile.NewProjectile(source, center, Vector2.Zero,
                    ModContent.ProjectileType<CyberGlitchBoltProj>(), 0, 0,
                    owner.whoAmI, ai0: angle, ai1: Main.rand.Next(0, 5));
            }
            SoundEngine.PlaySound(CWRSound.FaultTransition with {
                Volume = 0.85f,
                Pitch = 0.1f,
            }, center);
            SoundEngine.PlaySound(CWRSound.Faultrelease with {
                Volume = 0.7f,
                Pitch = -0.05f,
            }, center);
        }

        private static void PlayFailure(Player owner) {
            if (Main.dedServ || owner?.whoAmI != Main.myPlayer) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                Volume = 0.35f,
                Pitch = -0.4f,
            }, owner.Center);
            RamSystem.NotifyInsufficient();
        }

        internal static void ApplyReplicatedState(Player owner, uint revision,
            int progress, int anchorLayer, bool restoreFired, bool playVisual) {
            if (Main.netMode != NetmodeID.MultiplayerClient
                || owner?.active != true || revision == 0
                || progress < 0 || progress > TotalFrames
                || anchorLayer < 0 || anchorLayer > Cyberspace.MaxLayerCount
                || progress > 0 && anchorLayer < 1) {
                return;
            }
            Runtime state = GetRuntime(owner);
            if (!IsRevisionAtLeast(revision, state.Revision)) {
                return;
            }
            bool start = playVisual && progress > 0
                && revision != state.Revision;
            state.Revision = revision;
            state.ProgressTimer = progress;
            state.ProgressCarry = 0f;
            state.AnchorLayer = anchorLayer;
            state.RestoreFired = restoreFired;
            if (start) {
                SpawnStartVFX(owner);
            }
        }

        internal static void SendSnapshot(Player owner, int toWho) {
            if (Main.netMode == NetmodeID.Server && owner?.active == true) {
                CyberspaceActionNet.SendRestartState(owner,
                    GetRuntime(owner), false, toWho);
            }
        }

        internal static void ResetPlayer(Player owner) {
            if (owner != null) {
                runtimes.Remove(owner.whoAmI);
            }
        }

        private static void AdvanceRevision(Runtime state) {
            state.Revision++;
            if (state.Revision == 0) state.Revision = 1;
        }

        private static bool IsRevisionAtLeast(uint candidate, uint baseline)
            => candidate == baseline || unchecked((int)(candidate - baseline)) > 0;

        public static void Reset() {
            runtimes.Clear();
            Cyberspace.RestartCollapse = 0f;
        }
    }

    internal sealed class CyberRestartPlayer : ModPlayer
    {
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server) {
                CyberRestart.SendSnapshot(Player, toWho);
            }
        }

        public override void PlayerDisconnect() {
            CyberRestart.ResetPlayer(Player);
        }
    }
}
