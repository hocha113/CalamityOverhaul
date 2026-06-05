using CalamityOverhaul.Content.RangedModify.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SnowQuayMK2 : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "SnowQuayMK2";
        public override void SetDefaults() {
            Item.CloneDefaults(CWRID.Item_Onyxia);
            Item.damage = 28;
            Item.useAmmo = AmmoID.Snowball;
            Item.UseSound = SoundID.Item36 with { Pitch = -0.2f };
            Item.SetHeldProj<SnowQuayMK2Held>();
            Item.value = Terraria.Item.buyPrice(0, 1, 75, 0);
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddIngredient<SnowQuay>().
                AddIngredient(ItemID.IceBlock, 1000).
                AddTile(TileID.IceMachine).
                Register();
                return;
            }
            _ = CreateRecipe().
                AddIngredient<SnowQuay>().
                AddIngredient(CWRID.Item_CryonicBar, 5).
                AddIngredient(CWRID.Item_EssenceofEleum, 5).
                AddIngredient(ItemID.IceBlock, 1000).
                AddTile(TileID.IceMachine).
                Register();
        }
    }

    internal class SnowQuayMK2Held : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "SnowQuayMK2";
        public override int TargetID => ModContent.ItemType<SnowQuayMK2>();
        public override void SetRangedProperty() {
            GunPressure = 0;
            HandIdleDistanceX = 40;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 40;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -6;
            ShootPosToMouLengValue = 22;
            RecoilRetroForceMagnitude = 5;
            EnableRecoilRetroEffect = true;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<SnowQuayBall>();
            SpwanGunDustData.dustID1 = 76;
            SpwanGunDustData.dustID2 = 149;
            SpwanGunDustData.dustID3 = 76;
        }
    }
}
