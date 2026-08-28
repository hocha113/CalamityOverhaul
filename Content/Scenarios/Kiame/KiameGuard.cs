using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiame
{
    //主世界保护快照（镜像 KiyumeGuard，F34）
    //Enter 前 Snapshot，回主世界 RestoreOnReturn
    //绑 _sourceWorldId，回到不同世界整组丢弃
    internal static class KiameGuard
    {
        private static Dictionary<string, bool> _calBossFlags;

        private readonly record struct NpcEntry(int Type, int X, int Y);
        private static List<NpcEntry> _npcSnapshot;

        private static Guid _sourceWorldId;
        private static bool _hasSnapshot;

        private static Guid CurrentWorldId() => Main.ActiveWorldFileData?.UniqueId ?? Guid.Empty;

        internal static void Snapshot() {
            _sourceWorldId = CurrentWorldId();
            _hasSnapshot = true;
            SnapshotCalamityFlags();
            SnapshotTownNPCs();
        }

        private static void DiscardSnapshot() {
            _calBossFlags = null;
            _npcSnapshot = null;
            _sourceWorldId = Guid.Empty;
        }

        private static void SnapshotCalamityFlags() {
            _calBossFlags = new Dictionary<string, bool>(36);
            CWRRef.BulkCopyCalamityFlags((k, v) => _calBossFlags[k] = v);
        }

        private static void SnapshotTownNPCs() {
            _npcSnapshot = new List<NpcEntry>(32);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.townNPC) {
                    _npcSnapshot.Add(new NpcEntry(npc.type, (int)npc.Center.X, (int)npc.Center.Y));
                }
            }
        }

        //OnEnterWorld 每次进世界都触发，一次性消费+世界校验
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
            _sourceWorldId = Guid.Empty;
        }

        //OR 补 true，不清除已有 true
        private static void RestoreCalamityFlags() {
            if (_calBossFlags is null) {
                return;
            }
            CWRRef.BulkRestoreCalamityFlagsOr(k => _calBossFlags.TryGetValue(k, out bool v) && v);
            _calBossFlags = null;
        }

        private static void RestoreTownNPCs() {
            if (_npcSnapshot is null) {
                return;
            }
            //MP 客户端勿本地补城镇 NPC
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                _npcSnapshot = null;
                return;
            }
            HashSet<int> present = new(64);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.townNPC) {
                    present.Add(npc.type);
                }
            }
            foreach (NpcEntry entry in _npcSnapshot) {
                if (present.Contains(entry.Type)) {
                    continue;
                }
                //坐标越界跳过
                if (!WorldGen.InWorld(entry.X / 16, entry.Y / 16, 10)) {
                    continue;
                }
                NPC.NewNPC(new EntitySource_Misc("Kiame_NPCRestore"), entry.X, entry.Y, entry.Type);
            }
            _npcSnapshot = null;
        }
    }
}
