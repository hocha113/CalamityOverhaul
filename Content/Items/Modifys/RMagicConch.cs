using InnoVault.GameSystem;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Modifys
{
    internal class RMagicConch : ItemOverride, ILocalizedModType
    {
        public override string LocalizationCategory => "Items.RMagicConch";

        public static LocalizedText DontUseMagicConch { get; private set; }

        public override int TargetID => ItemID.MagicConch;
        public override bool DrawingInfo => false;

        public override void SetStaticDefaults() {
            DontUseMagicConch = this.GetLocalization(nameof(DontUseMagicConch), () => "你被一个强大的生物盯住了...");
        }

        public override bool? On_CanUseItem(Item item, Player player) => DontInBossUseItem(player);
        public static bool? DontInBossUseItem(Player player) {
            if (CWRWorld.Asura) {
                bool myIsBossTarget = false;
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.boss) {
                        myIsBossTarget = npc.target == player.whoAmI;
                        break;
                    }
                }
                if (myIsBossTarget) {
                    if (player.whoAmI == Main.myPlayer) {
                        VaultUtils.Text(DontUseMagicConch.Value, Color.Goldenrod);
                    }
                    return false;
                }
            }
            return null;
        }
    }

    internal class RDemonConch : ItemOverride
    {
        public override int TargetID => ItemID.DemonConch;
        public override bool DrawingInfo => false;
        public override bool? On_CanUseItem(Item item, Player player) => RMagicConch.DontInBossUseItem(player);
    }
}
