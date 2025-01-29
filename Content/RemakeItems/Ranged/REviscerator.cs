using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.RemakeItems.Core;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class REviscerator : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Eviscerator>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<EvisceratorHeld>(22);
    }

    internal class EvisceratorHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Eviscerator";
        public override int TargetID => ModContent.ItemType<Eviscerator>();
        public override void SetRangedProperty() {
            Recoil = 1.8f;
            HandIdleDistanceX = 20;
            kreloadMaxTime = 80;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 12;
            RecoilOffsetRecoverValue = 0.9f;
            CanCreateCaseEjection = false;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.clipOut = SoundID.DD2_GoblinHurt with { Pitch = -0.2f };
            FireTime = MagazineSystem ? 30 : 36;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<BloodClotFriendly>();
        }
    }
}
