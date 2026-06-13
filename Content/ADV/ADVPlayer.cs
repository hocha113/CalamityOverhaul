using CalamityOverhaul.Content.ADV.Scenarios;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    internal class ADVPlayer : ModPlayer
    {
        public override void OnEnterWorld() {
            OldDukeCampsite.RequestOldDukeCampsiteData();
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            // 仅本地玩家更新 ADV 场景
            var advSave = Player.GetModPlayer<ADVSavePlayer>().ADVSave;
            ADVScenarioScheduler.Tick(advSave, Player);
        }
    }
}
