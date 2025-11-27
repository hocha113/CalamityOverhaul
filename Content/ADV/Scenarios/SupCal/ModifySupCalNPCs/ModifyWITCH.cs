using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using InnoVault.GameSystem;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.ModifySupCalNPCs
{
    internal class ModifyWITCH : NPCOverride, ICWRLoader, ILocalizedModType
    {
        private delegate bool OnCanTownNPCSpawnDelegate(object obj, int numTownNPCs);
        public string LocalizationCategory => "NPCModifys";
        private readonly static List<LocalizedText> Homeless = [];
        private static LocalizedText L0;
        private static LocalizedText L1;
        private static LocalizedText L2;
        private static LocalizedText L3;
        private static LocalizedText L4;
        private static LocalizedText BloodMoon0;
        private static LocalizedText BloodMoon1;
        private static LocalizedText Night0;
        private static LocalizedText Night1;
        private static LocalizedText SeaKing;
        private static LocalizedText Party;

        public override int TargetID => CWRID.NPC_WITCH;

        void ICWRLoader.LoadData() {
            var type = CWRRef.GetNPC_WITCH_Type();
            if (type != null) {
                var meth = type.GetMethod("CanTownNPCSpawn", BindingFlags.Instance | BindingFlags.Public);
                VaultHook.Add(meth, OnCanTownNPCSpawnHook);
            }
        }

        //临时钩子，后续改用前置实现
        private static bool OnCanTownNPCSpawnHook(OnCanTownNPCSpawnDelegate orig, object obj, int numTownNPCs) {
            if (Main.player.Any(p => p.Alives() && EbnPlayer.OnEbn(p))) {//如果有玩家达成永恒燃烧的现在结局
                return false;//女巫不生成
            }
            return orig.Invoke(obj, numTownNPCs);
        }

        public override void SetStaticDefaults() {
            Homeless.Add(this.GetLocalization("Homeless0"));
            Homeless.Add(this.GetLocalization("Homeless1"));
            Homeless.Add(this.GetLocalization("Homeless2"));
            Homeless.Add(this.GetLocalization("Homeless3"));
            L0 = this.GetLocalization("L0");
            L1 = this.GetLocalization("L1");
            L2 = this.GetLocalization("L2");
            L3 = this.GetLocalization("L3");
            L4 = this.GetLocalization("L4");
            BloodMoon0 = this.GetLocalization("BloodMoon0");
            BloodMoon1 = this.GetLocalization("BloodMoon1");
            Night0 = this.GetLocalization("Night0");
            Night1 = this.GetLocalization("Night1");
            SeaKing = this.GetLocalization("SeaKing");
            Party = this.GetLocalization("Party");
        }

        public override void GetChat(ref string chat) {
            WeightedRandom<string> randChat = new();

            if (npc.homeless) {
                chat = Homeless[Main.rand.Next(Homeless.Count)].Value;
                return;
            }

            randChat.Add(L0.Value);
            randChat.Add(L1.Value);
            randChat.Add(L2.Value);
            randChat.Add(L3.Value);
            randChat.Add(L4.Value);

            if (!Main.dayTime) {
                if (Main.bloodMoon) {
                    randChat.Add(BloodMoon0.Value, 5.15);
                    randChat.Add(BloodMoon1.Value, 5.15);
                }
                else {
                    randChat.Add(Night0.Value, 2.8);
                    randChat.Add(Night1.Value, 2.8);
                }
            }

            if (NPC.AnyNPCs(CWRID.NPC_SEAHOE)) {
                randChat.Add(SeaKing.Value, 1.45);
            }

            if (BirthdayParty.PartyIsUp) {
                randChat.Add(Party.Value, 5.5);
            }

            chat = randChat;
        }
    }
}
