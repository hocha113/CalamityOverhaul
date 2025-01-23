using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPhangasm : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Phangasm>();
        public override int ProtogenesisID => ModContent.ItemType<PhangasmEcType>();
        public override string TargetToolTipItemName => "PhangasmEcType";
        public override void SetDefaults(Item item) {
            item.damage = 160;
            item.width = 48;
            item.height = 82;
            item.useTime = 12;
            item.useAnimation = 12;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3f;
            item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            item.UseSound = SoundID.Item5;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Ranged;
            item.channel = true;
            item.autoReuse = true;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.rare = ModContent.RarityType<DarkBlue>();
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().heldProjType = ModContent.ProjectileType<PhangasmBowHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
