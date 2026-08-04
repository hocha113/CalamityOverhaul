using InnoVault.Actors;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>斯安威斯坦本地门面与多人聚合</summary>
    internal sealed class Sandevistan : ModSystem
    {
        private const float AggregateEpsilon = 0.0001f;
        private static float aggregateTimeScale = 1f;
        private static uint aggregateRevision;

        public static bool IsActive => LocalState?.IsActive == true;
        public static float ScreenEffectIntensity
            => LocalState?.ScreenEffectIntensity ?? 0f;
        public static float CurrentCooldown {
            get => LocalState?.CurrentCooldown ?? 0f;
            set => LocalState?.SetLegacyCooldown(value);
        }
        public static float MaxCooldown => LocalState?.MaxCooldown ?? 0f;
        public static float ConsumptionRate => LocalState?.ConsumptionRate ?? 0f;
        public static float RecoveryRate => LocalState?.RecoveryRate ?? 0f;
        public static float CooldownRatio {
            get {
                SandevistanPlayer state = LocalState;
                return state?.MaxCooldown > 0f
                    ? Math.Clamp(state.CurrentCooldown / state.MaxCooldown, 0f, 1f)
                    : 0f;
            }
        }

        internal static float AggregateTimeScale => aggregateTimeScale;
        internal static uint AggregateRevision => aggregateRevision;
        public const int SpawnInterval = SandevistanPlayer.SpawnInterval;

        private static SandevistanPlayer LocalState {
            get {
                Player player = Main.LocalPlayer;
                return player?.active == true
                    ? player.GetModPlayer<SandevistanPlayer>()
                    : null;
            }
        }

        public static SandevistanPlayer GetState(Player player)
            => player?.active == true
                ? player.GetModPlayer<SandevistanPlayer>()
                : null;

        public static SandevistansItem GetEquipped(Player player) {
            if (player?.active != true) {
                return null;
            }
            CyberwarePlayer cyberware = player.GetModPlayer<CyberwarePlayer>();
            if (cyberware?.EquippedCyberwares == null) {
                return null;
            }

            int count = Math.Min(CyberwarePlayer.SlotCount,
                cyberware.EquippedCyberwares.Length);
            for (int i = 0; i < count; i++) {
                if (cyberware.EquippedCyberwares[i]?.ModItem
                    is SandevistansItem equipped) {
                    return equipped;
                }
            }
            return null;
        }

        public static bool TryActivate()
            => LocalState?.RequestToggle(true) == true;

        public static void ForceDeactivate() {
            LocalState?.RequestToggle(false);
        }

        public override void PostUpdatePlayers() {
            SandevistanNet.UpdatePending();
            if (Main.gameMenu) {
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                RecalculateAuthorityAggregate();
            }
            else {
                SandevistanTimeSlow.ApplyAggregate(aggregateTimeScale);
            }
        }

        public override void OnWorldLoad() {
            SandevistanTimeSlow.Reset();
            ResetAggregate();
            SandevistanNet.Reset();
        }

        public override void OnWorldUnload() {
            SandevistanTimeSlow.Reset();
            SandevistanNet.Reset();
            ResetAggregate();
        }

        public override void Unload() {
            SandevistanTimeSlow.Reset();
            SandevistanNet.Reset();
            ResetAggregate();
        }

        internal static void ApplyReplicatedAggregate(uint revision, float scale) {
            if (Main.netMode != NetmodeID.MultiplayerClient || revision == 0
                || !float.IsFinite(scale) || scale <= 0f || scale > 1f) {
                return;
            }
            if (aggregateRevision != 0 && revision != aggregateRevision
                && !CyberwarePlayer.IsRevisionNewer(revision,
                    aggregateRevision)) {
                return;
            }

            aggregateRevision = revision;
            aggregateTimeScale = Math.Clamp(scale, 0.001f, 1f);
            SandevistanTimeSlow.ApplyAggregate(aggregateTimeScale);
        }

        internal static void ForceAuthorityRecalculation() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                RecalculateAuthorityAggregate(forceBroadcast: true);
            }
        }

        private static void RecalculateAuthorityAggregate(
            bool forceBroadcast = false) {
            float nextScale = 1f;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active != true) {
                    continue;
                }
                SandevistanPlayer state = player.GetModPlayer<SandevistanPlayer>();
                if (state?.EligibleForAggregate != true) {
                    continue;
                }
                float scale = state.SlowFactor;
                if (float.IsFinite(scale) && scale > 0f && scale < nextScale) {
                    nextScale = scale;
                }
            }

            nextScale = Math.Clamp(nextScale, 0.001f, 1f);
            bool changed = MathF.Abs(nextScale - aggregateTimeScale)
                > AggregateEpsilon;
            if (changed) {
                aggregateTimeScale = nextScale;
                aggregateRevision++;
                if (aggregateRevision == 0) {
                    aggregateRevision = 1;
                }
            }

            SandevistanTimeSlow.ApplyAggregate(aggregateTimeScale);
            if (Main.netMode == NetmodeID.Server && (changed || forceBroadcast)) {
                if (aggregateRevision == 0) {
                    aggregateRevision = 1;
                }
                SandevistanNet.SendAggregate();
            }
        }

        private static void ResetAggregate() {
            aggregateTimeScale = 1f;
            aggregateRevision = Main.netMode == NetmodeID.MultiplayerClient
                ? 0u
                : 1u;
        }

        public static void SpawnGhost(Player player) {
            if (Main.dedServ || player?.active != true) {
                return;
            }

            int index = ActorLoader.NewActor<SandevistanGhostActor>(
                player.Center, Vector2.Zero);
            if (index >= 0) {
                ActorLoader.Actors[index].OnSpawn(player);
            }
        }
    }
}
