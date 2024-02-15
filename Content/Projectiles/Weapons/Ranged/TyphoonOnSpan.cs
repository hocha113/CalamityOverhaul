using CalamityMod;
using CalamityMod.Dusts;
using CalamityMod.Projectiles.Ranged;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ModLoader;
using static Terraria.ModLoader.PlayerDrawLayer;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class TyphoonOnSpan : BaseOnSpanProj
    {
        protected override Color[] colors => new Color[] { Color.LightSkyBlue, Color.Blue, Color.AliceBlue, Color.LightBlue };
        protected override float halfSpreadAngleRate => 1.15f;
        protected override float edgeBlendLength => 0.17f;
        protected override float edgeBlendStrength => 9;
        public override float MaxCharge => 180; 

        public override void SpanProjFunc() {
            int arrowTypes;
            int weaponDamage;
            float weaponKnockback;
            bool haveAmmo = Owner.PickAmmo(Owner.ActiveItem(), out arrowTypes, out _, out weaponDamage, out weaponKnockback, out _, false);
            if (haveAmmo && !CalamityUtils.CheckWoodenAmmo(arrowTypes, Owner)) {
                for (int i = 0; i < 64; i++) {
                    float rot = MathHelper.TwoPi / 64 * i;
                    Vector2 velocity = rot.ToRotationVector2() * (9 + (-1f + rot % MathHelper.PiOver4) * 7);
                    Projectile.NewProjectile(Owner.parent(), Projectile.Center, velocity
                        , arrowTypes, weaponDamage, weaponKnockback, Owner.whoAmI);
                }
                return;
            }
            weaponDamage = Projectile.damage;
            weaponKnockback = Projectile.knockBack;
            for (int i = 0; i < 94; i++) {
                float rot = MathHelper.TwoPi / 94 * i;
                Vector2 velocity = rot.ToRotationVector2() * (11 + (-1f + rot % MathHelper.PiOver4) * 9);
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, velocity
                    , ModContent.ProjectileType<TorrentialArrow>()
                    , weaponDamage, weaponKnockback, Owner.whoAmI);
            }
        }
    }
}
