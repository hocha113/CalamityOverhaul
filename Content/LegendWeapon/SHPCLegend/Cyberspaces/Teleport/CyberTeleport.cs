using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport
{
    /// <summary>领域瞬移</summary>
    internal class CyberTeleport : ICWRLoader
    {
        public const int RequiredLayer = 1;
        public const float RamCostPerCast = 2f;
        public const int CooldownFrames = 30;
        public const int HideDuration = 22;

        private const int TeleportStyle = 999;

        void ICWRLoader.UnLoadData() => Reset();

        private static CyberTeleportPlayer LocalState {
            get {
                if (Main.netMode == NetmodeID.Server || Main.myPlayer < 0
                    || Main.myPlayer >= Main.maxPlayers) {
                    return null;
                }
                Player player = Main.player[Main.myPlayer];
                return player?.active == true
                    ? player.GetModPlayer<CyberTeleportPlayer>()
                    : null;
            }
        }

        public static bool IsLocalPlayerHidden
            => (LocalState?.HideTimer ?? 0) > 0;

        public static int CooldownRemain => LocalState?.CooldownTimer ?? 0;

        public static bool OnCooldown => CooldownRemain > 0;

        public static Vector2 ClampToDomain(Player owner, Vector2 mouseWorld) {
            if (owner == null) {
                return mouseWorld;
            }
            CyberspacePlayer domain = Cyberspace.For(owner);
            float effectiveRadius = domain?.EffectiveOuterRadius ?? 0f;
            if (effectiveRadius <= 1f) {
                return owner.Center;
            }

            float maxRadius = Math.Max(0f, effectiveRadius - 8f);
            Vector2 offset = mouseWorld - owner.Center;
            float distance = offset.Length();
            if (distance <= maxRadius) {
                return mouseWorld;
            }
            if (distance <= 1f) {
                return owner.Center;
            }
            return owner.Center + offset * (maxRadius / distance);
        }

        public static void TryTeleport(Player owner) {
            if (owner == null || !owner.Alives()) {
                return;
            }
            CyberspacePlayer domain = Cyberspace.For(owner);
            if (domain == null || !domain.Active || domain.Intensity < 0.5f
                || domain.CurrentLayer < RequiredLayer) {
                return;
            }
            CyberTeleportPlayer state = owner.GetModPlayer<CyberTeleportPlayer>();
            if (state.CooldownTimer > 0) {
                PlayFailure(owner);
                return;
            }
            if (!HackTime.InfiniteHack
                && (RamSystem.IsLocked
                    || RamSystem.CurrentRam < RamCostPerCast)) {
                PlayFailure(owner);
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                if (!CyberspaceActionNet.SendActionRequest(owner,
                    CyberspaceActionKind.Teleport, Main.MouseWorld)) {
                    PlayFailure(owner);
                }
                return;
            }

            CyberspaceActionResultCode result = ExecuteAuthority(owner,
                Main.MouseWorld, out _);
            if (result != CyberspaceActionResultCode.Success) {
                PlayFailure(owner);
            }
        }

        internal static CyberspaceActionResultCode ExecuteAuthority(Player owner,
            Vector2 requestedTarget, out float paid) {
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
            CyberTeleportPlayer state = owner.GetModPlayer<CyberTeleportPlayer>();
            if (state.CooldownTimer > 0) {
                return CyberspaceActionResultCode.Cooldown;
            }

            Vector2 origin = owner.Center;
            Vector2 target = ClampToDomain(owner, requestedTarget);
            if (!float.IsFinite(target.X) || !float.IsFinite(target.Y)
                || Vector2.DistanceSquared(origin, target) < 64f * 64f) {
                return CyberspaceActionResultCode.InvalidPayload;
            }
            if (!HackTime.InfiniteHackAuthority
                && !RamSystem.TryConsume(owner, RamCostPerCast, out paid)) {
                return CyberspaceActionResultCode.InsufficientRam;
            }

            state.BeginAuthority();
            Vector2 newPosition = target
                - new Vector2(owner.width * 0.5f, owner.height * 0.5f);
            domain.NotifyTeleport(origin);
            owner.Teleport(newPosition, TeleportStyle);
            owner.velocity *= 0.25f;
            owner.immune = true;
            owner.immuneTime = Math.Max(owner.immuneTime, 18);

            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null,
                    0, owner.whoAmI, newPosition.X, newPosition.Y,
                    TeleportStyle);
                CyberspaceActionNet.SendTeleportState(owner, origin, target,
                    playVisual: true);
            }
            else {
                PlayActivationVisuals(owner, origin, target);
            }
            return CyberspaceActionResultCode.Success;
        }

        public static void Update() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active == true) {
                    player.GetModPlayer<CyberTeleportPlayer>().Tick();
                }
            }
        }

        public static void Reset() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active == true) {
                    player.GetModPlayer<CyberTeleportPlayer>().ResetState();
                }
            }
        }

        internal static void PlayActivationVisuals(Player owner, Vector2 origin,
            Vector2 target) {
            if (Main.dedServ || owner?.active != true
                || owner.whoAmI != Main.myPlayer) {
                return;
            }
            IEntitySource source = owner.GetSource_FromThis();
            Projectile.NewProjectile(source, origin, Vector2.Zero,
                ModContent.ProjectileType<CyberPixelDecomposeProj>(), 0, 0,
                owner.whoAmI);
            Projectile.NewProjectile(source, origin, Vector2.Zero,
                ModContent.ProjectileType<CyberRiftSlashProj>(), 0, 0,
                owner.whoAmI, ai0: target.X, ai1: target.Y);
            Projectile.NewProjectile(source, target, Vector2.Zero,
                ModContent.ProjectileType<CyberReformProj>(), 0, 0,
                owner.whoAmI);
            SoundEngine.PlaySound(CWRSound.FaultOccurred with {
                Volume = 0.65f,
                Pitch = 0.35f,
            }, origin);
            SoundEngine.PlaySound(CWRSound.Faultrelease with {
                Volume = 0.7f,
                Pitch = 0.15f,
            }, target);
            SoundEngine.PlaySound(CWRSound.FaultTransition with {
                Volume = 0.45f,
                Pitch = 0.5f,
            }, target);
        }

        private static void PlayFailure(Player owner) {
            if (Main.dedServ || owner?.active != true
                || owner.whoAmI != Main.myPlayer) {
                return;
            }
            RamSystem.NotifyInsufficient();
            SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                Volume = 0.4f,
                Pitch = -0.3f,
            }, owner.Center);
        }
    }

    internal sealed class CyberTeleportPlayer : ModPlayer
    {
        internal int CooldownTimer { get; private set; }
        internal int HideTimer { get; private set; }
        internal uint StateRevision { get; private set; } = 1;

        private float cooldownCarry;
        private float hideCarry;

        internal void BeginAuthority() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            CooldownTimer = CyberTeleport.CooldownFrames;
            HideTimer = CyberTeleport.HideDuration;
            cooldownCarry = 0f;
            hideCarry = 0f;
            AdvanceRevision();
        }

        internal void Tick() {
            int before = CooldownTimer;
            int cooldown = CooldownTimer;
            int hide = HideTimer;
            TimeGear.ConsumeFrames(ref cooldown, ref cooldownCarry);
            TimeGear.ConsumeFrames(ref hide, ref hideCarry);
            CooldownTimer = cooldown;
            HideTimer = hide;
            if (before > 0 && cooldown == 0
                && Main.netMode == NetmodeID.Server) {
                AdvanceRevision();
                CyberspaceActionNet.SendTeleportState(Player, Vector2.Zero,
                    Vector2.Zero, playVisual: false);
            }
        }

        internal bool ApplyReplicatedState(uint revision, int cooldown,
            int hide, bool playVisual, Vector2 origin, Vector2 target) {
            if (revision == 0 || cooldown < 0
                || cooldown > CyberTeleport.CooldownFrames || hide < 0
                || hide > CyberTeleport.HideDuration
                || !IsRevisionAtLeast(revision, StateRevision)) {
                return false;
            }
            bool shouldPlay = playVisual && cooldown > 0
                && revision != StateRevision;
            StateRevision = revision;
            CooldownTimer = cooldown;
            HideTimer = hide;
            cooldownCarry = 0f;
            hideCarry = 0f;
            if (shouldPlay) {
                CyberTeleport.PlayActivationVisuals(Player, origin, target);
                Player.GetModPlayer<CyberspacePlayer>().NotifyTeleport(origin);
            }
            return true;
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server) {
                CyberspaceActionNet.SendTeleportState(Player, Vector2.Zero,
                    Vector2.Zero, playVisual: false, toWho: toWho);
            }
        }

        public override void PlayerDisconnect() => ResetState();

        internal void ResetState() {
            CooldownTimer = 0;
            HideTimer = 0;
            cooldownCarry = 0f;
            hideCarry = 0f;
            StateRevision = 1;
        }

        private void AdvanceRevision() {
            StateRevision++;
            if (StateRevision == 0) {
                StateRevision = 1;
            }
        }

        private static bool IsRevisionAtLeast(uint candidate, uint baseline)
            => candidate == baseline || unchecked((int)(candidate - baseline)) > 0;
    }
}
