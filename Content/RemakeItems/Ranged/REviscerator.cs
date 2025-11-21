using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class REviscerator : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetCartridgeGun<EvisceratorHeld>(22);
    }

    internal class BloodAmmoBolt : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.extraUpdates = 3;
        }

        public override void AI() {
            if (++Projectile.localAI[0] > 4f) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dspeed = -Projectile.velocity * 0.7f;
                    int dust = Dust.NewDust(Projectile.Center, 1, 1, DustID.Blood, dspeed.X, dspeed.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(CWRID.Buff_BurningBlood, 240);

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(CWRID.Buff_BurningBlood, 240);

        public override void OnKill(int timeLeft) {
            CWRUtils.BlastingSputteringDust(Projectile, DustID.Blood, DustID.Blood, DustID.Blood, DustID.Blood, DustID.Blood);
            Projectile.damage /= 2;
            Projectile.Explode(explosionSound: SoundID.Item62);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
            return true;
        }
    }

    internal class EvisceratorHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Eviscerator";
        public override void SetRangedProperty() {
            Recoil = 1.8f;
            HandIdleDistanceX = 20;
            KreloadMaxTime = 80;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 12;
            RecoilOffsetRecoverValue = 0.9f;
            CanCreateCaseEjection = false;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.clipOut = SoundID.DD2_GoblinHurt with { Pitch = -0.2f };
            FireTime = MagazineSystem ? 60 : 80;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<BloodAmmoBolt>();
        }
    }
}
