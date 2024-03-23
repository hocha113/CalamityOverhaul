using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class KingsbaneEctype : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Kingsbane";
        public override void SetDefaults() {
            Item.damage = 175;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 92;
            Item.height = 44;
            Item.useTime = 3;
            Item.useAnimation = 3;
            Item.channel = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 2.5f;
            Item.value = CalamityGlobalItem.Rarity15BuyPrice;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<KingsbaneHeldProj>();
            Item.shootSpeed = 2f;
            Item.useAmmo = AmmoID.Bullet;
            Item.rare = ModContent.RarityType<Violet>();
            Item.SetHeldProj<KingsbaneHeldProj>();
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 1200;
        }

        public override bool CanConsumeAmmo(Item ammo, Player player) 
            => Main.rand.NextFloat() > 0.35f && player.ownedProjectileCounts[Item.shoot] > 0;
    }
}
