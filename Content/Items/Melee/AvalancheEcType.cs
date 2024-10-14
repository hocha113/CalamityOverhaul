using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class AvalancheEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Avalanche";
        public override void SetDefaults() {
            Item.SetItemCopySD<Avalanche>();
            Item.SetKnifeHeld<AvalancheHeld>();
        }
    }

    internal class RAvalanche : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Avalanche>();
        public override int ProtogenesisID => ModContent.ItemType<AvalancheEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<AvalancheHeld>();
    }
}
