using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class PulseBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.PulseBow].Value;
        public override int targetCayItem => ItemID.PulseBow;
        public override int targetCWRItem => ItemID.PulseBow;
        public override void SetRangedProperty() {
            ArmRotSengsBackBaseValue = 70;
            ShootSpanTypeValue = SpanTypesEnum.PulseBow;
        }

        public override void BowShoot() {
            AmmoTypes = ProjectileID.PulseBolt;
            int proj = Projectile.NewProjectile(Source, Projectile.Center + FireOffsetPos, ShootVelocity + FireOffsetVector
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].usesLocalNPCImmunity = true;
            Main.projectile[proj].localNPCHitCooldown = 15;
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
