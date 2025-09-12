using CalamityMod.Projectiles.Ranged;
using CalamityMod.Projectiles;
using CalamityMod;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria.Audio;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class FungalRevolver : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "FungalRevolver";
        public override void SetDefaults() {
            Item.CloneDefaults(ItemID.Revolver);
            Item.shoot = ModContent.ProjectileType<FungiAmmo>();
            Item.SetCartridgeGun<FungalRevolverHeld>(6);
        }
    }

    internal class FungalRevolverHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "FungalRevolver";
        public override int TargetID => ModContent.ItemType<FungalRevolver>();
        public override void SetRangedProperty() {
            KreloadMaxTime = 30;
            FireTime = 8;
            HandIdleDistanceX = 18;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 18;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -5;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = false;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.9f;
            RangeOfStress = 25;
            CanCreateSpawnGunDust = false;
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<FungiAmmo>();
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Revolver;
            if (!MagazineSystem) {
                FireTime += 5;
            }
        }

        public override void KreloadSoundloadTheRounds() {
            base.KreloadSoundloadTheRounds();
            for (int i = 0; i < 6; i++) {
                CaseEjection();
            }
        }
    }

    public class FungiAmmo : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/LaserProj";
        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            AIType = ProjectileID.Bullet;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            Projectile.spriteDirection = Projectile.direction = (Projectile.velocity.X > 0).ToDirectionInt();
            Projectile.rotation = Projectile.velocity.ToRotation() + (Projectile.spriteDirection == 1 ? 0f : MathHelper.Pi) + MathHelper.ToRadians(90) * Projectile.direction;

            Lighting.AddLight(Projectile.Center, new Vector3(0, 244, 252) * (1.2f / 255));

            if (++Projectile.localAI[0] > 4f) {
                Vector2 dspeed = -Projectile.velocity * 0.5f;
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueFairy, 0f, 0f, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = dspeed;
            }

            if (Projectile.localAI[1] < 22) {
                Projectile.localAI[1]++;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.owner == Main.myPlayer) {
                for (int f = 0; f < 3; f++) {
                    Vector2 velocity = CalamityUtils.RandomVelocity(100f, 60f, 85f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity, ModContent.ProjectileType<FungiOrb2>(), (int)(Projectile.damage * 0.4), 0f, Projectile.owner);
                }
            }
            SoundEngine.PlaySound(SoundID.NPCDeath1, Projectile.position);
            for (int k = 0; k < 5; k++) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.BlueFairy, Projectile.oldVelocity.X * 0.5f, Projectile.oldVelocity.Y * 0.5f);
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(53, 12, 255, Projectile.alpha);

        public override bool PreDraw(ref Color lightColor) => Projectile.DrawBeam(Projectile.localAI[1], 1.5f, lightColor);
    }
}
