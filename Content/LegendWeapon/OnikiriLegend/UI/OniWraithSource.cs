using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    internal sealed class OniWraithSource : IOniGhostSource, ICWRLoader
    {
        private static readonly List<OniGhostEntry> entries = [];
        private static int cachedPlayer = -1;
        private static uint cachedLoadoutRevision = uint.MaxValue;
        private static uint cachedResourceRevision = uint.MaxValue;

        public IReadOnlyList<OniGhostEntry> Entries {
            get {
                TryRefresh();
                return entries;
            }
        }

        public string EquippedKey {
            get {
                return TryResolvePlayer(out WraithPlayer wraithPlayer)
                    ? wraithPlayer.EquippedWraithKey
                    : string.Empty;
            }
        }

        public float Erosion {
            get {
                return TryResolvePlayer(out WraithPlayer wraithPlayer)
                    ? wraithPlayer.Erosion
                    : 0f;
            }
        }

        public bool TrySetEquipped(Item sourceItem, string key, Action<bool> completed) {
            Player player = Main.LocalPlayer;
            if (player == null || sourceItem == null || OnikiriData.TryGet(sourceItem) == null) {
                return false;
            }
            if (!string.IsNullOrEmpty(key)
                && (!WraithRegistry.TryGet(key, out WraithDefinition definition) || !definition.CanEquip)) {
                return false;
            }
            return WraithNet.RequestEquippedWraith(player, sourceItem, key, success => {
                Invalidate();
                TryRefresh();
                completed?.Invoke(success);
            });
        }

        void ICWRLoader.SetupData() => OniRegistry.SetSource(this);

        void ICWRLoader.UnLoadData() {
            OniRegistry.SetSource(null);
            entries.Clear();
            Invalidate();
        }

        private static bool TryResolvePlayer(out WraithPlayer wraithPlayer) {
            wraithPlayer = null;
            if (Main.dedServ || Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                return false;
            }
            return Main.LocalPlayer.TryGetModPlayer(out wraithPlayer);
        }

        private static void TryRefresh() {
            if (!TryResolvePlayer(out WraithPlayer wraithPlayer)) {
                entries.Clear();
                Invalidate();
                return;
            }
            if (cachedPlayer == wraithPlayer.Player.whoAmI
                && cachedLoadoutRevision == wraithPlayer.LoadoutRevision
                && cachedResourceRevision == wraithPlayer.ResourceRevision
                && entries.Count == WraithRegistry.All.Count) {
                return;
            }

            cachedPlayer = wraithPlayer.Player.whoAmI;
            cachedLoadoutRevision = wraithPlayer.LoadoutRevision;
            cachedResourceRevision = wraithPlayer.ResourceRevision;
            Rebuild(wraithPlayer);
        }

        private static void Rebuild(WraithPlayer wraithPlayer) {
            entries.Clear();
            foreach (WraithDefinition definition in WraithRegistry.Usable) {
                entries.Add(BuildEntry(definition, wraithPlayer));
            }
            foreach (WraithDefinition definition in WraithRegistry.All) {
                if (!definition.CanEquip) {
                    entries.Add(BuildEntry(definition, wraithPlayer));
                }
            }
        }

        private static OniGhostEntry BuildEntry(WraithDefinition definition, WraithPlayer wraithPlayer) {
            bool canEquip = definition.CanEquip;
            return new OniGhostEntry {
                Key = definition.Key,
                Name = () => definition.DisplayName.Value,
                Origin = () => definition.Origin.Value,
                Power = () => definition.Power.Value,
                Revival = canEquip ? wraithPlayer.GetRevival(definition.Key) : 0f,
                RevivalCost = definition.RevivalCost,
                ErosionCost = definition.ErosionCost,
                State = canEquip ? OniGhostState.Ready : OniGhostState.Archive,
                CanEquip = canEquip,
            };
        }

        private static void Invalidate() {
            cachedPlayer = -1;
            cachedLoadoutRevision = uint.MaxValue;
            cachedResourceRevision = uint.MaxValue;
        }
    }
}
