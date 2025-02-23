using CalamityMod;
using CalamityOverhaul.Content.Events.TungstenRiotEvent;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content
{
    internal class CWRWorld : ModSystem
    {
        /// <summary>
        /// 是否在进行机械暴乱
        /// </summary>
        internal static bool MachineRebellion;
        /// <summary>
        /// 接下来多少tick的更新里面不能关闭机械暴乱
        /// </summary>
        internal static int DontCloseMachineRebellion;
        /// <summary>
        /// 是否在当前世界击败了机械暴乱
        /// </summary>
        public static bool MachineRebellionDowned;

        public override void ClearWorld() {
            TungstenRiot.Instance.TungstenRiotIsOngoing = false;
            TungstenRiot.Instance.EventKillPoints = 0;
            MachineRebellion = false;
        }

        public override void PostUpdateEverything() {
            if (MachineRebellion) {//在机械暴乱开启下，检测如果全程机械Boss被杀死了后就自动关闭
                bool noBoss = true;

                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.type == NPCID.SkeletronPrime) {
                        noBoss = false;
                    }
                    else if (npc.type == NPCID.Spazmatism) {
                        noBoss = false;
                    }
                    else if (npc.type == NPCID.Retinazer) {
                        noBoss = false;
                    }
                    else if (npc.type == NPCID.TheDestroyer) {
                        noBoss = false;
                    }
                }

                if (DontCloseMachineRebellion > 0) {
                    DontCloseMachineRebellion--;
                }

                if (noBoss && DontCloseMachineRebellion <= 0) {
                    MachineRebellion = false;
                }
            }
        }

        public override void NetSend(BinaryWriter writer) {
            BitsByte flags1 = new BitsByte();
            flags1[0] = TungstenRiot.Instance.TungstenRiotIsOngoing;
            flags1[1] = DownedBossSystem.downedPrimordialWyrm;
            flags1[2] = MachineRebellionDowned;
            writer.Write(flags1);
            writer.Write(TungstenRiot.Instance.EventKillPoints);
            writer.Write(InWorldBossPhase.YharonKillCount);
        }

        public override void NetReceive(BinaryReader reader) {
            BitsByte flags1 = reader.ReadByte();
            TungstenRiot.Instance.TungstenRiotIsOngoing = flags1[0];
            DownedBossSystem.downedPrimordialWyrm = flags1[1];
            MachineRebellionDowned = flags1[2];
            TungstenRiot.Instance.EventKillPoints = reader.ReadInt32();
            InWorldBossPhase.YharonKillCount = reader.ReadInt32();
        }

        public override void SaveWorldData(TagCompound tag) {
            tag.Add("_InWorldBossPhase_YharonKillCount", InWorldBossPhase.YharonKillCount);
            tag.Add("_Event_TungstenRiotIsOngoing", TungstenRiot.Instance.TungstenRiotIsOngoing);
            tag.Add("_Event_EventKillPoints", TungstenRiot.Instance.EventKillPoints);
            tag.Add("_MachineRebellion", MachineRebellionDowned);
        }

        public override void LoadWorldData(TagCompound tag) {
            if (tag.TryGet("_InWorldBossPhase_YharonKillCount", out int _yharonKillCount)) {
                InWorldBossPhase.YharonKillCount = _yharonKillCount;
            }
            if (tag.TryGet("_Event_TungstenRiotIsOngoing", out bool _tungstenRiotIsOngoing)) {
                TungstenRiot.Instance.TungstenRiotIsOngoing = _tungstenRiotIsOngoing;
            }
            if (tag.TryGet("_Event_EventKillPoints", out int _eventKillPoints)) {
                TungstenRiot.Instance.EventKillPoints = _eventKillPoints;
            }
            if (!tag.TryGet("_MachineRebellion", out MachineRebellionDowned)) {
                MachineRebellionDowned = false;
            }
        }
    }
}
