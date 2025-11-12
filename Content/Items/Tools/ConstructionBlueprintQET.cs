using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class ConstructionBlueprintQET : ModItem
    {
        public override string Texture => CWRConstant.Item_Tools + "ConstructionBlueprintQET";
        public static LocalizedText L1;
        public static LocalizedText L2;
        public override void SetStaticDefaults() {
            L1 = this.GetLocalization(nameof(L1), () => "已获得制作量子塔自我构建器的知识!");
            L2 = this.GetLocalization(nameof(L2), () => "你已经学习过这个图纸了!");
        }
        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 32;
            Item.maxStack = 1;
            Item.value = Item.buyPrice(gold: 92);
            Item.rare = ItemRarityID.LightRed;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useTurn = true;
            Item.autoReuse = false;
        }

        public override bool? UseItem(Player player) {
            if (player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 1.5f }, player.Center);
                int combat;
                if (halibutPlayer.ADCSave.UseConstructionBlueprint) {
                    combat = CombatText.NewText(new Rectangle((int)player.Center.X - 100, (int)player.Center.Y - 100, 200, 50),
                        Color.BlueViolet, L2.Value, true);
                }
                else {
                    combat = CombatText.NewText(new Rectangle((int)player.Center.X - 100, (int)player.Center.Y - 100, 200, 50),
                        Color.Gold, L1.Value, true);
                }
                Main.combatText[combat].lifeTime = 300;
                halibutPlayer.ADCSave.UseConstructionBlueprint = true;
            }
            return true;
        }
    }
}
