using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using WeaverGrievancesItem = CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses.WeaverGrievances;

namespace CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses
{
    internal sealed class WGAcquisitionPlayer : ModPlayer
    {
        private const string ClaimedSaveKey = "WeaverGrievancesRitualClaimed";
        internal const int DefaultClaimTimeoutTicks = 60 * 5;

        private bool legacyMigrationPending;
        private bool syncedClaimed;
        private int nextClaimToken;

        public bool Claimed { get; private set; }
        public int PendingClaimToken { get; private set; }
        public int PendingClaimTimeoutTicks { get; private set; }
        public bool HasPendingClaim => PendingClaimToken != 0 && PendingClaimTimeoutTicks > 0;
        public bool CanRequestClaim => !Claimed && !HasPendingClaim;

        public override void Initialize() {
            Claimed = false;
            legacyMigrationPending = false;
            syncedClaimed = false;
            nextClaimToken = 0;
            ClearPendingClaim();
        }

        public override void SaveData(TagCompound tag) {
            tag[ClaimedSaveKey] = Claimed;
        }

        public override void LoadData(TagCompound tag) {
            bool claimed = false;
            bool hasClaimedKey = tag != null && tag.TryGet(ClaimedSaveKey, out claimed);
            Claimed = hasClaimedKey && claimed;
            legacyMigrationPending = !hasClaimedKey;
            ClearPendingClaim();

            if (legacyMigrationPending && HasWeaponInPersonalStorage(Player)) {
                Claimed = true;
                legacyMigrationPending = false;
            }
        }

        public override void OnEnterWorld() {
            ClearPendingClaim();
            if (legacyMigrationPending) {
                Claimed = HasWeaponInPersonalStorage(Player);
                legacyMigrationPending = false;
            }

            if (VaultUtils.isClient && Player.whoAmI == Main.myPlayer) {
                WGManifestationNet.SendClaimedState(Player);
            }
        }

        public override void PostUpdate() {
            if (VaultUtils.isClient && Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (!HasPendingClaim) {
                return;
            }

            PendingClaimTimeoutTicks--;
            if (PendingClaimTimeoutTicks <= 0) {
                ClearPendingClaim();
            }
        }

        public override void UpdateDead() => ClearPendingClaim();

        public override void CopyClientState(ModPlayer targetCopy) {
            ((WGAcquisitionPlayer)targetCopy).syncedClaimed = Claimed;
        }

        public override void SendClientChanges(ModPlayer clientPlayer) {
            WGAcquisitionPlayer snapshot = (WGAcquisitionPlayer)clientPlayer;
            if (snapshot.syncedClaimed != Claimed) {
                WGManifestationNet.SendClaimedState(Player);
            }
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            WGManifestationNet.SendClaimedState(Player, toWho, fromWho);
        }

        internal bool TryBeginPendingClaim(out int token, int timeoutTicks = DefaultClaimTimeoutTicks) {
            token = 0;
            if (!CanRequestClaim) {
                return false;
            }

            nextClaimToken = unchecked(nextClaimToken + 1);
            if (nextClaimToken <= 0) {
                nextClaimToken = 1;
            }

            token = nextClaimToken;
            PendingClaimToken = token;
            PendingClaimTimeoutTicks = System.Math.Max(1, timeoutTicks);
            return true;
        }

        internal bool MatchesPendingClaim(int token)
            => token != 0 && HasPendingClaim && PendingClaimToken == token;

        internal bool ResolvePendingClaim(int token, bool accepted) {
            if (!MatchesPendingClaim(token)) {
                return false;
            }

            ClearPendingClaim();
            if (accepted) {
                Claimed = true;
            }
            return true;
        }

        internal bool CancelPendingClaim(int token = 0) {
            if (!HasPendingClaim || token != 0 && token != PendingClaimToken) {
                return false;
            }

            ClearPendingClaim();
            return true;
        }

        internal bool TryMarkClaimed() {
            ClearPendingClaim();
            if (Claimed) {
                return false;
            }

            Claimed = true;
            return true;
        }

        internal void ApplySyncedClaimed(bool claimed) {
            if (!claimed) {
                return;
            }

            Claimed = true;
            ClearPendingClaim();
        }

        internal static bool HasWeaponInPersonalStorage(Player player) {
            if (player == null) {
                return false;
            }

            int itemType = ModContent.ItemType<WeaverGrievancesItem>();
            return ContainsItem(player.inventory, itemType)
                || ContainsItem(player.bank?.item, itemType)
                || ContainsItem(player.bank2?.item, itemType)
                || ContainsItem(player.bank3?.item, itemType)
                || ContainsItem(player.bank4?.item, itemType);
        }

        private static bool ContainsItem(Item[] items, int itemType) {
            if (items == null) {
                return false;
            }

            for (int i = 0; i < items.Length; i++) {
                Item item = items[i];
                if (item != null && item.type == itemType && item.stack > 0) {
                    return true;
                }
            }
            return false;
        }

        private void ClearPendingClaim() {
            PendingClaimToken = 0;
            PendingClaimTimeoutTicks = 0;
        }
    }
}
