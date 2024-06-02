using CalamityMod;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DaemonsFlameHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "DaemonsFlameBow";
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<DaemonsFlameEcType>();
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.DaemonsFlame>();
        public override int targetCWRItem => ModContent.ItemType<DaemonsFlameEcType>();
        public override void PostInOwner() => CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);

        public override void BowDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 4)
                , onFire ? Color.White : lightColor, Projectile.rotation, CWRUtils.GetOrig(value, 4), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            value = CWRUtils.GetT2DValue(Texture + "Glow");
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 4)
                , Color.White, Projectile.rotation, CWRUtils.GetOrig(value, 4), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            value = CWRUtils.GetT2DValue(Texture + "Fire");
            Vector2 offset = Projectile.rotation.ToRotationVector2() * 20;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition + offset, CWRUtils.GetRec(value, Projectile.frame, 4)
                , Color.White, 0, new Vector2(value.Width / 2, value.Height / 4), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
        }

        public override void BowShoot() {
            if (CalamityUtils.CheckWoodenAmmo(AmmoTypes, Owner)) {
                int types = ModContent.ProjectileType<FateCluster>();
                for (int i = 0; i < 4; i++) {
                    Vector2 vr = (Projectile.rotation + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * Main.rand.Next(8, 18);
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * 8;
                    int doms = Projectile.NewProjectile(Source, pos, vr, types, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                    Projectile newDoms = Main.projectile[doms];
                    newDoms.DamageType = DamageClass.Ranged;
                    newDoms.timeLeft = 120;
                    newDoms.ai[0] = 1;
                }
            }
            else {
                for (int i = 0; i < 4; i++) {
                    Vector2 vr = Projectile.rotation.ToRotationVector2() * Main.rand.Next(20, 30);
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * 8;
                    Projectile.NewProjectile(Source2, pos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                }
                Projectile.NewProjectile(Source, Projectile.Center, Projectile.rotation.ToRotationVector2() * 18
                    , ModContent.ProjectileType<DaemonsFlameArrow>(), WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }
        }
    }
}
