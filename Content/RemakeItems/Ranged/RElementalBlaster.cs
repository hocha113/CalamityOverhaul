using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RElementalBlaster : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<ElementalBlaster>();

        public override void SetDefaults(Item item) {
            item.damage = 67;
            item.DamageType = DamageClass.Ranged;
            item.width = 104;
            item.height = 42;
            item.useTime = 6;
            item.useAnimation = 6;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 1.75f;
            item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            item.rare = ItemRarityID.Purple;
            item.UseSound = CommonCalamitySounds.PlasmaBoltSound;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<RainbowBlast>();
            item.shootSpeed = 18f;
            item.useAmmo = AmmoID.Bullet;
            item.CWR().heldProjType = ModContent.ProjectileType<ElementalBlasterHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
