using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class DeadSunsWindEcType : EctypeItem
    {
        public static readonly SoundStyle UseShoot = new("CalamityMod/Sounds/Item/DeadSunShot") { PitchVariance = 0.35f, Volume = 0.4f };
        public static readonly SoundStyle Ricochet = new("CalamityMod/Sounds/Item/DeadSunRicochet") { Volume = 0.35f };
        public static readonly SoundStyle Explosion = new("CalamityMod/Sounds/Item/DeadSunExplosion") { Volume = 0.5f };
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DeadSunsWind";
        public override void SetDefaults() {
            Item.damage = 100;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 70;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 3.5f;
            Item.UseSound = UseShoot;
            Item.value = CalamityGlobalItem.Rarity9BuyPrice;
            Item.rare = ItemRarityID.Cyan;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<CosmicFire>();
            Item.shootSpeed = 9f;
            Item.useAmmo = AmmoID.Gel;
            Item.CWR().hasHeldNoCanUseBool = true;
            Item.CWR().heldProjType = ModContent.ProjectileType<DeadSunsWindHeldProj>();
        }
    }
}
