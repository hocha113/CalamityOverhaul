using CalamityOverhaul.Content.Items.Magic.Eyetooths;
using CalamityOverhaul.Content.Items.Melee.Shatterfangs;
using CalamityOverhaul.Content.Items.Ranged.BloodshotBombs;
using CalamityOverhaul.Content.Items.Summon.EyekiteStaffs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Modifys.ModifyBag
{
    internal class ModifyEyeOfCthulhuBag : BaseModifyBag
    {
        public override int TargetID => ItemID.EyeOfCthulhuBossBag;

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            itemLoot.SimpleAdd(ModContent.ItemType<EyekiteStaff>(), 4);
            itemLoot.SimpleAdd(ModContent.ItemType<Eyetooth>(), 4);
            itemLoot.SimpleAdd(ModContent.ItemType<Shatterfang>(), 4);
            itemLoot.SimpleAdd(ModContent.ItemType<BloodshotBomb>(), 4);
        }
    }
}
