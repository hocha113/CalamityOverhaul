using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDeadSunsWind : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<DeadSunsWind>();

        public override void SetDefaults(Item item) {
            item.damage = 100;
            item.DamageType = DamageClass.Ranged;
            item.width = 70;
            item.height = 24;
            item.useTime = item.useAnimation = 22;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 3.5f;
            item.UseSound = new("CalamityMod/Sounds/Item/DeadSunShot") { PitchVariance = 0.35f, Volume = 0.4f };
            item.value = CalamityGlobalItem.RarityCyanBuyPrice;
            item.rare = ItemRarityID.Cyan;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<CosmicFire>();
            item.shootSpeed = 9f;
            item.useAmmo = AmmoID.Gel;
            item.SetCartridgeGun<DeadSunsWindHeldProj>(120);
            item.CWR().CartridgeType = CartridgeUIEnum.JAR;
        }
    }
}
