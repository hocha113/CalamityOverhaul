using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTheStorm : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TheStorm>();
        public override int ProtogenesisID => ModContent.ItemType<TheStormEcType>();
        public override string TargetToolTipItemName => "TheStormEcType";
        public override void SetDefaults(Item item) {
            item.damage = 35;
            item.DamageType = DamageClass.Ranged;
            item.width = 54;
            item.height = 90;
            item.useTime = 14;
            item.useAnimation = 14;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 3.5f;
            item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.UseSound = SoundID.Item122;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<Bolt>();
            item.shootSpeed = 28f;
            item.useAmmo = AmmoID.Arrow;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<TheStormHeldProj>();
        }
    }
}
