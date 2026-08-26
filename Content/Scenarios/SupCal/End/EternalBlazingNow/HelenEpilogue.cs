using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow
{
    internal sealed class HelenEpilogue : NarrativeScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.SupCal.EternalBlazingNow";

        public static LocalizedText EpilogueLine1 { get; private set; }
        public static LocalizedText EpilogueLine2 { get; private set; }
        public static LocalizedText EpilogueLine3 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override void SetStaticDefaults() {
            EpilogueLine1 = this.GetLocalization(nameof(EpilogueLine1), () => "我在等一个笨蛋");
            EpilogueLine2 = this.GetLocalization(nameof(EpilogueLine2), () => ".....");
            EpilogueLine3 = this.GetLocalization(nameof(EpilogueLine3), () => "欢迎回来.....");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Helen", "Silence", EpilogueLine1.Value)
             .Say("Helen", "Silence", EpilogueLine2.Value)
             .Say("Helen", EpilogueLine3.Value);
        }

        protected override void OnCompleted() => MarkSeen(Main.LocalPlayer);

        /// <summary>
        /// 尾声待兑现。是每玩家剧情债务，随玩家存档，跨存档仍然有效
        /// </summary>
        public static bool IsPending(Player player) {
            if (player?.active != true) {
                return false;
            }

            SupCalStoryData data = GetData(player);
            return data.HelenEpiloguePending && !data.HelenEpilogueSeen;
        }

        /// <summary>比目鱼被带走时武装；已播过则不再武装</summary>
        public static void RequestSpawn(Player player) {
            if (player?.active != true) {
                return;
            }

            SupCalStoryData data = GetData(player);
            if (data.HelenEpilogueSeen) {
                return;
            }

            data.HelenEpiloguePending = true;
        }

        /// <summary>播完才落位，中途退出仍会重来</summary>
        private static void MarkSeen(Player player) {
            if (player?.active != true) {
                return;
            }

            SupCalStoryData data = GetData(player);
            data.HelenEpiloguePending = false;
            data.HelenEpilogueSeen = true;
        }

        private static SupCalStoryData GetData(Player player) => player.GetModPlayer<StoryPlayer>().Get<SupCalStoryData>();
    }

    internal sealed class HelenEpilogueNPC : DeathTrackingNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == CWRID.NPC_PrimordialWyrmHead;

        public override void OnNPCDeath(NPC npc) {
            //HitEffect 各端都跑，本地进度自行挡服务端
            if (Main.dedServ || npc.type != CWRID.NPC_PrimordialWyrmHead) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (!HelenEpilogue.IsPending(player) || player.HasItem(HalibutOverride.ID)) {
                return;
            }

            //选中槽空着就直接塞进去，便于立刻握在手里
            int slot = player.selectedItem is >= 0 and < 10 ? player.selectedItem : 0;
            if (player.inventory[slot].IsAir) {
                player.inventory[slot].SetDefaults(HalibutOverride.ID);
                return;
            }

            player.GiveItem(player.GetSource_GiftOrReward(), HalibutOverride.ID);
        }
    }
}
