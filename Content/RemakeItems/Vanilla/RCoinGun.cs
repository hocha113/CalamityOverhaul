using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RCoinGun : ItemOverride
    {
        public override int TargetID => ItemID.CoinGun;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetCartridgeGun<CoinGunHeld>(200);
    }

    internal class CoinGunHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.CoinGun].Value;
        public override int targetCayItem => ItemID.CoinGun;
        public override int targetCWRItem => ItemID.CoinGun;
        public override void SetRangedProperty() {
            Recoil = 0.6f;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            RecoilOffsetRecoverValue = 0.6f;
            CanCreateCaseEjection = false;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            FireTime = MagazineSystem ? 12 : 14;
            CanCreateSpawnGunDust = false;
        }
    }
}
