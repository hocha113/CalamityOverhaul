using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    //TODO
    //internal class RDodusHandcannon : ItemOverride
    //{
    //    public override int TargetID => ModContent.ItemType<DodusHandcannon>();

    //    public override void SetDefaults(Item item) {
    //        item.width = 62;
    //        item.height = 34;
    //        item.damage = 1020;
    //        item.DamageType = DamageClass.Ranged;
    //        item.useTime = 30;
    //        item.useAnimation = 30;
    //        item.autoReuse = true;
    //        item.useStyle = ItemUseStyleID.Shoot;
    //        item.noMelee = true;
    //        item.knockBack = 6f;
    //        item.UseSound = CommonCalamitySounds.LargeWeaponFireSound with { Volume = 0.3f };
    //        item.shoot = ModContent.ProjectileType<HighExplosivePeanutShell>();
    //        item.shootSpeed = 13f;
    //        item.useAmmo = AmmoID.Bullet;
    //        item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
    //        item.rare = ModContent.RarityType<PureGreen>();
    //        item.Calamity().donorItem = true;
    //        item.Calamity().canFirePointBlankShots = true;
    //        item.CWR().HasCartridgeHolder = true;
    //        item.CWR().AmmoCapacity = 22;
    //        item.SetHeldProj<DodusHandcannonHeldProj>();
    //    }

    //    public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
    //        , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
    //        return false;
    //    }
    //}
}
