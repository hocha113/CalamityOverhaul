using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.Items.Rogue.Extras;
using CalamityOverhaul.Content.TileModules.Core;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content
{
    internal class CWRWorld : ModSystem
    {
        public static bool TitleMusicBoxEasterEgg = true;
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

        private static int _time;
        public static int Time {
            get => _time;
            set => _time = value;
        }

        public override void ClearWorld() {
            TitleMusicBoxEasterEgg = true;
            TungstenRiot.Instance.TungstenRiotIsOngoing = false;
            TungstenRiot.Instance.EventKillPoints = 0;
            Time = 0;
        }

        public override void PostUpdateTime() {
            Time++;
        }

        public override void NetSend(BinaryWriter writer) {
            BitsByte flags1 = new BitsByte();
            flags1[0] = TitleMusicBoxEasterEgg;
            flags1[1] = TungstenRiot.Instance.TungstenRiotIsOngoing;
            writer.Write(flags1);
            writer.Write(TungstenRiot.Instance.EventKillPoints);
        }

        public override void NetReceive(BinaryReader reader) {
            BitsByte flags1 = reader.ReadByte();
            TitleMusicBoxEasterEgg = flags1[0];
            TungstenRiot.Instance.TungstenRiotIsOngoing = flags1[1];
            TungstenRiot.Instance.EventKillPoints = reader.ReadInt32();
        }

        public override void OnWorldLoad() {
            ModGanged.Set_MS_Config_recursionCraftingDepth();
            if (SupertableUI.Instance != null) {
                SupertableUI.Instance.loadOrUnLoadZenithWorldAsset = true;
                SupertableUI.Instance.Active = false;
            }
            if (RecipeUI.Instance != null) {
                RecipeUI.Instance.index = 0;
                RecipeUI.Instance.LoadPsreviewItems();
            }
            Gangarus.ZenithWorldAsset();
        }

        public override void SaveWorldData(TagCompound tag) {
            tag.Add("_TitleMusicBoxEasterEgg", TitleMusicBoxEasterEgg);
            tag.Add("_Event_DefeatTheTungstenArmy_Tag", DefeatTheTungstenArmy);
            tag.Add("_Event_TungstenRiotIsOngoing", TungstenRiot.Instance.TungstenRiotIsOngoing);
            tag.Add("_Event_EventKillPoints", TungstenRiot.Instance.EventKillPoints);
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                TungstenRiot.Instance.EventNetWorkSend();
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            TitleMusicBoxEasterEgg = tag.GetBool("_TitleMusicBoxEasterEgg");
            DefeatTheTungstenArmy = tag.GetBool("_Event_DefeatTheTungstenArmy_Tag");
            TungstenRiot.Instance.TungstenRiotIsOngoing = tag.GetBool("_Event_TungstenRiotIsOngoing");
            TungstenRiot.Instance.EventKillPoints = tag.GetInt("_Event_EventKillPoints");
        }
    }
}
