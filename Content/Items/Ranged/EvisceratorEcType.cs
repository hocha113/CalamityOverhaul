using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 开膛者
    /// </summary>
    internal class EvisceratorEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Eviscerator";
        public static void SetDefaultsFunc(Item Item) => Item.SetCartridgeGun<EvisceratorHeld>(22);
        public override void SetDefaults() {
            Item.SetItemCopySD<Eviscerator>();
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
