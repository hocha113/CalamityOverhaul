using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class PlanetaryAnnihilationEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PlanetaryAnnihilation";
        public override void SetDefaults() {
            Item.damage = 66;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 58;
            Item.height = 102;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5.5f;
            Item.value = CalamityGlobalItem.Rarity11BuyPrice;
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item75;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PlanetaryAnnihilationProj>();
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Arrow;
            Item.CWR().hasHeldNoCanUseBool = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<PlanetaryAnnihilationHeldProj>();
        }
    }
}
