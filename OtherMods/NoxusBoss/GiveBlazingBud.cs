using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Services;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.NoxusBoss
{
    internal class GiveBlazingBud : ModPlayer, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        public static LocalizedText MessageText;
        public static int RandTimer;
        public override void SetStaticDefaults() {
            MessageText = this.GetLocalization(nameof(MessageText), () => "焚烧的余烬随风低语，一朵奇异的花飘落到你手中……");
        }
        public override void OnEnterWorld() {
            RandTimer = Main.rand.Next(60 * 6, 60 * 10);
        }
        public override void PostUpdate() {
            if (VaultUtils.isServer) {
                return;
            }
            if (CWRMod.Instance.noxusBoss == null) {
                return;
            }
            if (!EbnState.OnEbn(Player)) {
                return;
            }

            SupCalStoryData data = Player.GetModPlayer<StoryPlayer>().Get<SupCalStoryData>();
            if (data.GiveBlazingBud) {
                return;
            }
            if (--RandTimer > 0) {
                return;
            }
            if (!CWRMod.Instance.noxusBoss.TryFind("BlazingBud", out ModItem blazingBudItem)) {
                return;
            }

            data.GiveBlazingBud = true;
            NarrativeServices.RewardGrant?.Grant(new RewardPayload {
                ItemType = blazingBudItem.Type,
                Stack = 1
            }, Player);

            VaultUtils.Text(MessageText.Value, Color.Orange);
        }
    }
}
