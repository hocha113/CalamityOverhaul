using InnoVault.GameContent.BaseEntity;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class TarragonArrowOnSpan : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public sealed override bool? CanDamage() => false;

        public sealed override void AI() {
            Projectile owner = null;
            if (Projectile.ai[1] >= 0 && Projectile.ai[1] < Main.maxProjectiles) {
                owner = Main.projectile[(int)Projectile.ai[1]];
            }
            if (owner == null) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Center;
            Projectile.rotation = owner.rotation;
            if (!DownLeft) {
                Projectile.Kill();
                return;
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                ShootState shootState = Owner.GetShootState();
                if (Projectile.timeLeft == 40) {
                    SoundEngine.PlaySound(SoundID.Item108 with { Volume = 0.6f, Pitch = -0.8f }, Projectile.Center);
                    Projectile.NewProjectile(shootState.Source, Projectile.Center, Projectile.rotation.ToRotationVector2() * 7
                        , ModContent.ProjectileType<TarragonArrow>(), shootState.WeaponDamage, shootState.WeaponKnockback, Owner.whoAmI, 0);
                }
                if (Projectile.timeLeft == 20) {
                    SoundEngine.PlaySound(SoundID.Item108 with { Volume = 0.9f, Pitch = -0.5f }, Projectile.Center);
                    for (int i = 0; i < 3; i++) {
                        Projectile.NewProjectile(shootState.Source, Projectile.Center, (Projectile.rotation + (-1 + i) * 0.06f).ToRotationVector2() * 12
                        , ModContent.ProjectileType<TarragonArrow>(), shootState.WeaponDamage, shootState.WeaponKnockback, Owner.whoAmI, 1);
                    }
                }
                if (Projectile.timeLeft == 1) {
                    SoundEngine.PlaySound(SoundID.Item108 with { Volume = 1.2f, Pitch = -0.2f }, Projectile.Center);
                    for (int i = 0; i < 5; i++) {
                        Projectile.NewProjectile(shootState.Source, Projectile.Center, (Projectile.rotation + (-2 + i) * 0.06f).ToRotationVector2() * 15
                        , ModContent.ProjectileType<TarragonArrow>(), shootState.WeaponDamage, shootState.WeaponKnockback, Owner.whoAmI, 2);
                    }
                }
            }
        }
    }
}
