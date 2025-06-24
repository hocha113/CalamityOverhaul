using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RMarksmanBow : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<MarksmanBow>();
        public override void SetDefaults(Item item) {
            item.damage = 35;
            item.DamageType = DamageClass.Ranged;
            item.width = 36;
            item.height = 110;
            item.useTime = 18;
            item.useAnimation = 18;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 6f;
            item.UseSound = SoundID.Item5;
            item.autoReuse = true;
            item.shoot = ProjectileID.JestersArrow;
            item.shootSpeed = 10f;
            item.useAmmo = AmmoID.Arrow;
            item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.Calamity().donorItem = true;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().heldProjType = ModContent.ProjectileType<MarksmanBowHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
