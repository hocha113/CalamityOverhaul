using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class TsunamiHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Tsunami].Value;
        public override int targetCayItem => ItemID.Tsunami;
        public override int targetCWRItem => ItemID.Tsunami;
        private SlotId accumulator;
        public override void SetRangedProperty() {
            CanRightClick = true;
        }

        public override void PostInOwner() {
            CanFireMotion = true;
            FiringDefaultSound = true;
            if (onFireR) {
                FiringDefaultSound = CanFireMotion = false;
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = -MathHelper.PiOver2;
                Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 12;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
            }
            SetCompositeArm();
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<TsunamiOnSpan>()] == 0) {
                if (SoundEngine.TryGetActiveSound(accumulator, out var sound)) {
                    sound.Stop();
                    accumulator = SlotId.Invalid;
                }
            }
        }

        public override void BowShoot() {
            for (int i = 0; i < 5; i++) {
                Projectile.NewProjectile(Source, Projectile.Center + UnitToMouseV.GetNormalVector() * (-2 + i) * 10
                    , ShootVelocity.UnitVector() * 17, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void BowShootR() {
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<TsunamiOnSpan>()] == 0) {
                accumulator = SoundEngine.PlaySound(CWRSound.Accumulator with { Pitch = 0.3f }, Projectile.Center);
                Projectile.NewProjectile(Source, Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TsunamiOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
            }
        }
    }
}
