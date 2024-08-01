using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ChainGunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override int targetCayItem => ItemID.ChainGun;
        public override int targetCWRItem => ItemID.ChainGun;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ChainGun].Value;

        private float randomShootRotset;
        public override void SetRangedProperty() {
            FireTime = 4;
            Recoil = 0.3f;
            SpwanGunDustMngsData.splNum = 0.3f;
        }

        public override float GetGunInFireRot() {
            float rot = base.GetGunInFireRot();
            if (kreloadTimeValue == 0) {
                rot += randomShootRotset;
            }
            return rot;
        }

        public override void NetCodeHeldSend(BinaryWriter writer) {
            writer.Write(randomShootRotset);
        }

        public override void NetCodeReceiveHeld(BinaryReader reader) {
            randomShootRotset = reader.ReadSingle();
        }

        public override void SetShootAttribute() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                randomShootRotset = Main.rand.NextFloat(-0.16f, 0.16f);
                NetUpdate();
            }
        }

        public override void FiringShoot() =>
        Projectile.NewProjectile(Source, GunShootPos, ShootVelocityInProjRot
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
    }
}
