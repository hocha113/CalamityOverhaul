using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class PearlGodEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PearlGod";
        public override void SetDefaults() {
            Item.damage = 150;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 80;
            Item.height = 46;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 3f;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = SoundID.Item41;
            Item.autoReuse = true;
            Item.shootSpeed = 12f;
            Item.shoot = ModContent.ProjectileType<ShockblastRound>();
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 12;
            Item.SetHeldProj<PearlGodHeldProj>();
        }
    }
}
