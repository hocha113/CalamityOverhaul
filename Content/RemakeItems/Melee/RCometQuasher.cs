using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RCometQuasher : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CometQuasher>();
        public override int ProtogenesisID => ModContent.ItemType<CometQuasherEcType>();
        public override string TargetToolTipItemName => "CometQuasherEcType";
        public override void SetDefaults(Item item) => CometQuasherEcType.SetDefaultsFunc(item);
    }
}
