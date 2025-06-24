using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RNettlevineGreatbow : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<NettlevineGreatbow>();
        public override void SetDefaults(Item item) {
            item.damage = 73;
            item.DamageType = DamageClass.Ranged;
            item.width = 36;
            item.height = 64;
            item.useTime = 18;
            item.useAnimation = 18;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 3f;
            item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.UseSound = SoundID.Item5;
            item.autoReuse = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 16f;
            item.useAmmo = AmmoID.Arrow;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().heldProjType = ModContent.ProjectileType<NettlevineGreatbowHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
