using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RContagion : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Contagion>();
        public override void SetDefaults(Item item) {
            item.damage = 280;
            item.DamageType = DamageClass.Ranged;
            item.width = 22;
            item.height = 50;
            item.useTime = 20;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.channel = true;
            item.knockBack = 5f;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<ContagionBow>();
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.UseSound = SoundID.Item5;
            item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            item.rare = ModContent.RarityType<HotPink>();
            item.Calamity().devItem = true;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().heldProjType = ModContent.ProjectileType<ContagionHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
