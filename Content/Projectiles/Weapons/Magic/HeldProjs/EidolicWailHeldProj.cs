using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class EidolicWailHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "EidolicWail";
        public override int TargetID => ModContent.ItemType<EidolicWail>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 20;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 20;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RecoilOffsetRecoverValue = 0.75f;
            SetRegenDelayValue = 60;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 23;
        }

        public override void PostInOwnerUpdate() {
            if (onFire) {
                //OffsetPos += CWRUtils.randVr(0.5f + (Item.useTime - GunShootCoolingValue) * 0.03f);
                if (Time % 10 == 0) {
                    Vector2 spanPos = Main.MouseWorld;
                    spanPos.X += Main.rand.Next(-260, 260);
                    spanPos.Y += Main.rand.Next(60, 100);
                    Vector2 vr = new Vector2(0, -Main.rand.Next(3, 19));
                    BasePRT pulse3 = new PRT_DWave(spanPos, vr, Color.BlueViolet
                    , new Vector2(0.7f, 1.3f) * 0.8f, vr.ToRotation(), 0.18f, 0.32f, 60);
                    PRTLoader.AddParticle(pulse3);
                }
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 13; i++) {
                Vector2 rand = CWRUtils.randVr(480, 800);
                Vector2 pos = Main.MouseWorld + rand;
                Vector2 vr = rand.UnitVector() * -ShootSpeedModeFactor;
                if (CWRUtils.GetTile(pos / 16).HasSolidTile()) {
                    pos = Projectile.Center;
                    vr = ShootVelocity * Main.rand.NextFloat(0.3f, 1.13f);
                }
                Projectile proj = Projectile.NewProjectileDirect(Source, pos, vr, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                proj.scale += Main.rand.NextFloat(-0.2f, 0.2f);
                proj.velocity *= Main.rand.NextFloat(1, 1.13f);

                BasePRT pulse3 = new PRT_DWave(ShootPos, ShootVelocity * (0.3f + i * 0.1f), Color.BlueViolet
                , new Vector2(0.7f, 1.3f) * 0.8f, ShootVelocity.ToRotation(), 0.18f, 0.22f + i * 0.05f, 40);
                PRTLoader.AddParticle(pulse3);
            }
        }
    }
}
