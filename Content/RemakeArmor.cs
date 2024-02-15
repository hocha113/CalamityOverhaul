using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public class RemakeArmor : GlobalItem
    {
        public override void SetDefaults(Item item) {
            base.SetDefaults(item);
            if (item.type == ItemID.BeetleScaleMail) {
                item.defense = 15;
                base.SetDefaults(item);
            }
            if (item.type == ItemID.BeetleShell) {
                item.defense = 42;
                base.SetDefaults(item);
            }
            if (item.type == ItemID.BeetleHelmet) {
                item.defense = 20;
                base.SetDefaults(item);
            }
            if (item.type == ItemID.BeetleLeggings) {
                item.defense = 16;
                base.SetDefaults(item);
            }
        }
    }
}
