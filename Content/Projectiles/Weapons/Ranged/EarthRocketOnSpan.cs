using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.GameContent.BaseEntity;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class EarthRocketOnSpan : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 21;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 1.2f;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override void AI() {
            Projectile.MaxUpdates = 2;
            BaseOnSpanProj.FlowerAI(Projectile);
            Item item = Owner.GetItem();
            if (item.type != ItemID.None) {
                item.CWR().NoKreLoadTime = 2;
            }
            if (Projectile.timeLeft % 5 == 0 && DownLeft) {
                Vector2 vr = Projectile.rotation.ToRotationVector2() * 7;
                CWRUtils.SpawnGunDust(Projectile, Projectile.Center, vr);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Owner.GetShootState("CWRGunShoot").Source, Projectile.Center, vr, CWRID.Proj_ScorchedEarthRocket, Owner.GetShootState().WeaponDamage, Owner.GetShootState().WeaponKnockback, Owner.whoAmI, 0);
                }
                Vector2 pos = Projectile.Center - vr * 3 + vr.GetNormalVector() * 10 * Owner.direction;
                if (Projectile.rotation != 0) {
                    for (int i = 0; i < 100; i++) {
                        Vector2 dustVel = (Projectile.rotation + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * -Main.rand.Next(26, 117);
                        float scale = Main.rand.NextFloat(0.5f, 1.5f);
                        int type = DustID.CopperCoin;
                        Vector2 pos2 = pos;
                        if (Main.rand.NextBool()) {
                            type = DustID.FireworkFountain_Red;
                            pos2 += dustVel * 6;
                        }
                        if (Main.rand.NextBool(5)) {
                            type = DustID.FireworkFountain_Pink;
                            pos2 += dustVel * 6;
                        }
                        Dust.NewDust(pos2, 5, 5, type, dustVel.X, dustVel.Y, 0, default, scale);
                    }
                }
            }
        }
    }
}
