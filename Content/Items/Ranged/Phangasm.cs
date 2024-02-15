using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class Phangasm : EctypeItem
    {
        public override string Texture => "CalamityMod/Items/Weapons/Ranged/Phangasm";
        public override void SetDefaults() {
            Item.damage = 160;
            Item.width = 48;
            Item.height = 82;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.UseSound = SoundID.Item5;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Ranged;
            Item.channel = true;
            Item.autoReuse = true;
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Arrow;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<PhangasmBowHeldProj>();
            Item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
