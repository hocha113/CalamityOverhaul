using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.OtherMods.ImproveGame;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //Enter前Snapshot，回主世界RestoreOnReturn
    //绑_sourceWorldId，世界不符整组丢弃
    //SHPC按进入前后差量Prune，Level升序删
    internal static class CybCourseWorldGuard
    {
        private static Dictionary<string, bool> _calBossFlags;

        private readonly record struct NpcEntry(int Type, int X, int Y);
        private static List<NpcEntry> _npcSnapshot;

        //进入前SHPC总数；差量Prune，_shpcSnapshotValid一次性消费
        private static int _shpcOwnedSnapshot;
        private static bool _shpcSnapshotValid;

        private static Guid _sourceWorldId;
        private static bool _hasSnapshot;

        private static Guid CurrentWorldId() => Main.ActiveWorldFileData?.UniqueId ?? Guid.Empty;

        internal static void Snapshot() {
            _sourceWorldId = CurrentWorldId();
            _hasSnapshot = true;
            SnapshotCalamityFlags();
            SnapshotTownNPCs();
            SnapshotSHPCOwnership();
        }

        private static void DiscardSnapshot() {
            _calBossFlags = null;
            _npcSnapshot = null;
            _shpcSnapshotValid = false;
            _sourceWorldId = Guid.Empty;
        }

        private static void SnapshotCalamityFlags() {
            _calBossFlags = new Dictionary<string, bool>(36);
            CWRRef.BulkCopyCalamityFlags((k, v) => _calBossFlags[k] = v);
        }

        private static void SnapshotTownNPCs() {
            _npcSnapshot = new List<NpcEntry>(32);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.townNPC)
                    _npcSnapshot.Add(new NpcEntry(n.type, (int)n.Center.X, (int)n.Center.Y));
            }
        }

        //OnEnterWorld每次进世界都触发，一次性消费+世界校验
        internal static void RestoreOnReturn() {
            if (!_hasSnapshot) {
                return;
            }
            _hasSnapshot = false;

            if (CurrentWorldId() != _sourceWorldId) {
                DiscardSnapshot();
                return;
            }

            RestoreCalamityFlags();
            RestoreTownNPCs();
            PruneTutorialSHPC();
            _sourceWorldId = Guid.Empty;
        }

        //OR补true，不清除已有true
        private static void RestoreCalamityFlags() {
            if (_calBossFlags is null) return;
            CWRRef.BulkRestoreCalamityFlagsOr(k => _calBossFlags.TryGetValue(k, out bool v) && v);
            _calBossFlags = null;
        }

        private static void RestoreTownNPCs() {
            if (_npcSnapshot is null) return;
            //MP客户端勿本地补城镇NPC
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                _npcSnapshot = null;
                return;
            }
            HashSet<int> present = new(64);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.townNPC)
                    present.Add(n.type);
            }
            foreach (NpcEntry e in _npcSnapshot) {
                if (present.Contains(e.Type)) continue;
                //坐标越界跳过
                if (!WorldGen.InWorld(e.X / 16, e.Y / 16, 10)) continue;
                NPC.NewNPC(new EntitySource_Misc("CybCourse_NPCRestore"), e.X, e.Y, e.Type);
            }
            _npcSnapshot = null;
        }

        private static void SnapshotSHPCOwnership() {
            _shpcSnapshotValid = false;
            Player p = Main.LocalPlayer;
            if (p == null || !p.active) return;
            _shpcOwnedSnapshot = CountSHPCOnPlayer(p);
            _shpcSnapshotValid = true;
        }

        //差量剔除，Level升序先动兜底货
        private static void PruneTutorialSHPC() {
            if (!_shpcSnapshotValid) return;
            _shpcSnapshotValid = false;

            Player p = Main.LocalPlayer;
            if (p == null || !p.active) return;

            int currentCount = CountSHPCOnPlayer(p);
            int excess = currentCount - _shpcOwnedSnapshot;
            if (excess <= 0) return;

            RemoveLowestLevelSHPC(p, excess);
        }

        private static int CountSHPCOnPlayer(Player p) {
            int total = 0;
            foreach (Item[] arr in EnumeratePlayerContainers(p)) {
                total += CountSHPCInArray(arr);
            }
            if (IsSHPC(p.trashItem)) total++;
            return total;
        }

        private static void RemoveLowestLevelSHPC(Player p, int toRemove) {
            List<Item> shpcs = new(8);
            foreach (Item[] arr in EnumeratePlayerContainers(p)) {
                CollectSHPC(arr, shpcs);
            }
            if (IsSHPC(p.trashItem)) shpcs.Add(p.trashItem);

            shpcs.Sort((a, b) => SHPCOverride.GetLevel(a).CompareTo(SHPCOverride.GetLevel(b)));

            int removed = 0;
            for (int i = 0; i < shpcs.Count && removed < toRemove; i++) {
                shpcs[i].TurnToAir();
                removed++;
            }
        }

        private static IEnumerable<Item[]> EnumeratePlayerContainers(Player p) {
            yield return p.inventory;
            if (p.bank?.item != null) yield return p.bank.item;
            if (p.bank2?.item != null) yield return p.bank2.item;
            if (p.bank3?.item != null) yield return p.bank3.item;
            if (p.bank4?.item != null) yield return p.bank4.item;
            //QoT大背包，无Mod则null
            List<Item> bigBag = p.GetBigBagItems();
            if (bigBag != null && bigBag.Count > 0) {
                yield return bigBag.ToArray();
            }
        }

        private static int CountSHPCInArray(Item[] arr) {
            if (arr == null) return 0;
            int c = 0;
            for (int i = 0; i < arr.Length; i++) {
                if (IsSHPC(arr[i])) c++;
            }
            return c;
        }

        private static void CollectSHPC(Item[] arr, List<Item> sink) {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++) {
                if (IsSHPC(arr[i])) sink.Add(arr[i]);
            }
        }

        private static bool IsSHPC(Item item)
            => item != null && !item.IsAir && item.type == SHPCOverride.ID;
    }
}
