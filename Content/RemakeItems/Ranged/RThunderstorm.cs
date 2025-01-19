using CalamityMod.Items;
using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Projectiles.Magic;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RThunderstorm : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Thunderstorm>();
        public override int ProtogenesisID => ModContent.ItemType<ThunderstormEcType>();
        public override string TargetToolTipItemName => "ThunderstormEcType";
        public override void SetDefaults(Item item) {
            item.damage = 132;
            item.mana = 50;
            item.DamageType = DamageClass.Magic;
            item.width = 48;
            item.height = 22;
            item.useTime = 24;
            item.useAnimation = 24;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 2f;
            item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.UseSound = CommonCalamitySounds.PlasmaBlastSound;
            item.autoReuse = true;
            item.shootSpeed = 6f;
            item.shoot = ModContent.ProjectileType<ThunderstormShot>();
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<ThunderstormHeldProj>();
            item.CWR().Scope = true;
        }
    }
}
