using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class StakeLauncherHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.StakeLauncher].Value;
        public override int targetCayItem => ItemID.StakeLauncher;
        public override int targetCWRItem => ItemID.StakeLauncher;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0f;
            ControlForce = 0f;
            Recoil = 0f;
            kreloadMaxTime = 45;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override bool PreReloadEffects(int time, int maxTime) {
            if (time == maxTime - 1) {
                SoundEngine.PlaySound(CWRSound.CaseEjection with { Volume = 0.5f, Pitch = -1f }, Projectile.Center);
            }
            return false;
        }

        public override void HandleEmptyAmmoEjection() {
            SoundEngine.PlaySound(CWRSound.Ejection with { Volume = 0.5f, Pitch = -1f }, Projectile.Center);
            CombatText.NewText(Owner.Hitbox, Color.Gold, CWRLocText.GetTextValue("CaseEjection_TextContent"));
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
