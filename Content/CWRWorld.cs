using CalamityMod;
using CalamityOverhaul.Content.Events;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content
{
    internal class CWRWorld : ModSystem
    {
        public static bool DontSetHoverItem;
        public static bool _defeatTheTungstenArmy;
        public static bool DefeatTheTungstenArmy {
            get => _defeatTheTungstenArmy;
            set {
                if (!value) {
                    _defeatTheTungstenArmy = false;
                }
                else {
                    NPC.SetEventFlagCleared(ref _defeatTheTungstenArmy, -1);
                }
            }
        }

        public override void ClearWorld() {
            TungstenRiot.Instance.TungstenRiotIsOngoing = false;
            TungstenRiot.Instance.EventKillPoints = 0;
        }

        public override void PostUpdateEverything() {
            if (!DontSetHoverItem) {
                CWRLoad.HoverItem = Main.HoverItem;
            }
            DontSetHoverItem = false;
        }

        public override void NetSend(BinaryWriter writer) {
            BitsByte flags1 = new BitsByte();
            flags1[0] = TungstenRiot.Instance.TungstenRiotIsOngoing;
            flags1[1] = DownedBossSystem.downedPrimordialWyrm;
            writer.Write(flags1);
            writer.Write(TungstenRiot.Instance.EventKillPoints);
            writer.Write(InWorldBossPhase.YharonKillCount);
        }

        public override void NetReceive(BinaryReader reader) {
            BitsByte flags1 = reader.ReadByte();
            TungstenRiot.Instance.TungstenRiotIsOngoing = flags1[0];
            DownedBossSystem.downedPrimordialWyrm = flags1[1];
            TungstenRiot.Instance.EventKillPoints = reader.ReadInt32();
            InWorldBossPhase.YharonKillCount = reader.ReadInt32();
        }

        public override void SaveWorldData(TagCompound tag) {
            tag.Add("_InWorldBossPhase_YharonKillCount", InWorldBossPhase.YharonKillCount);
            tag.Add("_Event_DefeatTheTungstenArmy_Tag", DefeatTheTungstenArmy);
            tag.Add("_Event_TungstenRiotIsOngoing", TungstenRiot.Instance.TungstenRiotIsOngoing);
            tag.Add("_Event_EventKillPoints", TungstenRiot.Instance.EventKillPoints);
        }

        public override void LoadWorldData(TagCompound tag) {
            if (tag.TryGet("_InWorldBossPhase_YharonKillCount", out int _yharonKillCount)) {
                InWorldBossPhase.YharonKillCount = _yharonKillCount;
            }
            if (tag.TryGet("_Event_DefeatTheTungstenArmy_Tag", out bool _defeatTheTungstenArmy)) {
                DefeatTheTungstenArmy = _defeatTheTungstenArmy;
            }
            if (tag.TryGet("_Event_TungstenRiotIsOngoing", out bool _tungstenRiotIsOngoing)) {
                TungstenRiot.Instance.TungstenRiotIsOngoing = _tungstenRiotIsOngoing;
            }
            if (tag.TryGet("_Event_EventKillPoints", out int _eventKillPoints)) {
                TungstenRiot.Instance.EventKillPoints = _eventKillPoints;
            }
        }
    }
}
