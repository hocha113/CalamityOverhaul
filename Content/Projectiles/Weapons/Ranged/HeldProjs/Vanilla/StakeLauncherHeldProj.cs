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
            if (kreloadTimeValue > 0) {//设置一个特殊的装弹动作，调整转动角度和中心点，让枪身看起来上抬
                Owner.direction = ToMouse.X > 0 ? 1 : -1;//为了防止抽搐，这里额外设置一次玩家朝向
                FeederOffsetRot = MathHelper.ToRadians(10) * DirSign;
                FeederOffsetPos = new Vector2(0, 3);
            }
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
