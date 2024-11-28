using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class EvisceratorEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Eviscerator";
        public static void SetDefaultsFunc(Item Item) {
            Item.SetCartridgeGun<CrackshotColtHeld>(8);
        }
        public override void SetDefaults() {
            Item.SetItemCopySD<CrackshotColt>();
            SetDefaultsFunc(Item);
        }
    }

    internal class REviscerator : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Eviscerator>();
        public override int ProtogenesisID => ModContent.ItemType<EvisceratorEcType>();
        public override string TargetToolTipItemName => "EvisceratorEcType";
        public override void SetDefaults(Item item) => EvisceratorEcType.SetDefaultsFunc(item);
    }

    internal class EvisceratorHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Eviscerator";
        public override int targetCayItem => ModContent.ItemType<Eviscerator>();
        public override int targetCWRItem => ModContent.ItemType<EvisceratorEcType>();
        public override void SetRangedProperty() {
            HandDistance = 20;
            kreloadMaxTime = 80;
            Recoil = 2.8f;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 12;
            RecoilOffsetRecoverValue = 0.9f;
            CanCreateCaseEjection = false;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.clipOut = CWRSound.CaseEjection2 with { Pitch = -0.2f };
            FireTime = MagazineSystem ? 10 : 90;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<BloodClotFriendly>();
        }
    }
}
