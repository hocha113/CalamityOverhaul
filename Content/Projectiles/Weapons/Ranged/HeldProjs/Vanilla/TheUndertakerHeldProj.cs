using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class TheUndertakerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.TheUndertaker].Value;
        public override int targetCayItem => ItemID.TheUndertaker;
        public override int targetCWRItem => ItemID.TheUndertaker;
        public override void SetRangedProperty() {
            kreloadMaxTime = 35;
            FireTime = 12;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -3;
            HandDistance = 15;
            HandDistanceY = 0;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = false;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 0.6f;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Revolver;
        }

        public override void KreloadSoundloadTheRounds() {
            base.KreloadSoundloadTheRounds();
            for (int i = 0; i < 6; i++) {
                CaseEjection();
            }
        }
    }
}
