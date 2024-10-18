using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.Items.Rogue.Extras;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content
{
    internal class CWRWorld : ModSystem
    {
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
            TungstenRiot.Instance.TungstenRiotIsOngoing = false;
            TungstenRiot.Instance.EventKillPoints = 0;
            Time = 0;
        }

        public override void PostUpdateTime() {
            Time++;
        }

        public override void NetSend(BinaryWriter writer) {
            BitsByte flags1 = new BitsByte();
            flags1[0] = TungstenRiot.Instance.TungstenRiotIsOngoing;
            writer.Write(flags1);
            writer.Write(TungstenRiot.Instance.EventKillPoints);
        }

        public override void NetReceive(BinaryReader reader) {
            BitsByte flags1 = reader.ReadByte();
            TungstenRiot.Instance.TungstenRiotIsOngoing = flags1[0];
            TungstenRiot.Instance.EventKillPoints = reader.ReadInt32();
        }

        public override void OnWorldLoad() {
            ModGanged.Set_MS_Config_recursionCraftingDepth();
            if (CWRServerConfig.Instance.AddExtrasContent) {
                if (SupertableUI.Instance != null) {
                    SupertableUI.Instance.loadOrUnLoadZenithWorldAsset = true;
                    SupertableUI.Instance.Active = false;
                }
                if (RecipeUI.Instance != null) {
                    RecipeUI.Instance.index = 0;
                    RecipeUI.Instance.LoadPsreviewItems();
                }
            }
            Gangarus.ZenithWorldAsset();
        }

        public override void SaveWorldData(TagCompound tag) {
            tag.Add("_Event_DefeatTheTungstenArmy_Tag", DefeatTheTungstenArmy);
            tag.Add("_Event_TungstenRiotIsOngoing", TungstenRiot.Instance.TungstenRiotIsOngoing);
            tag.Add("_Event_EventKillPoints", TungstenRiot.Instance.EventKillPoints);
            if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
                TungstenRiot.Instance.EventNetWorkSend();
            }
            if (CWRServerConfig.Instance.AddExtrasContent) {
                if (SupertableUI.Instance != null && SupertableUI.Instance?.items != null) {
                    for (int i = 0; i < SupertableUI.Instance.items.Length; i++) {
                        if (SupertableUI.Instance.items[i] == null) {
                            SupertableUI.Instance.items[i] = new Item(0);
                        }
                    }
                    tag.Add("SupertableUI_ItemDate", SupertableUI.Instance.items);
                }
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            DefeatTheTungstenArmy = tag.GetBool("_Event_DefeatTheTungstenArmy_Tag");
            TungstenRiot.Instance.TungstenRiotIsOngoing = tag.GetBool("_Event_TungstenRiotIsOngoing");
            TungstenRiot.Instance.EventKillPoints = tag.GetInt("_Event_EventKillPoints");
            if (CWRServerConfig.Instance.AddExtrasContent) {
                if (SupertableUI.Instance != null && tag.ContainsKey("SupertableUI_ItemDate")) {
                    Item[] loadSupUIItems = tag.Get<Item[]>("SupertableUI_ItemDate");
                    for (int i = 0; i < loadSupUIItems.Length; i++) {
                        if (loadSupUIItems[i] == null) {
                            loadSupUIItems[i] = new Item(0);
                        }
                    }
                    SupertableUI.Instance.items = tag.Get<Item[]>("SupertableUI_ItemDate");
                }
            }
        }
    }
}
