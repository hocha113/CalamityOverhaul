using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class FetidEmesisOnSpan : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public sealed override bool? CanDamage() => false;

        public sealed override void AI() {
            Player player = Main.player[Projectile.owner];
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
            if (!player.PressKey()) {
                Projectile.Kill();
                return;
            }
            ShootState shootState = player.GetShootState();
            if (Projectile.timeLeft == 40) {
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.6f, Pitch = -0.8f }, Projectile.Center);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    int proj = Projectile.NewProjectile(player.parent(), Projectile.Center, Projectile.rotation.ToRotationVector2() * 7
                    , ModContent.ProjectileType<EmesisGore>(), shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI, 0);
                    Main.projectile[proj].penetrate = 5;
                }
                for (int i = 0; i < 5; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.Center, 10, 10, DustID.Shadowflame);
                    dust.velocity = Vector2.Normalize(owner.rotation.ToRotationVector2() * 7).RotatedByRandom(MathHelper.ToRadians(15f));
                    dust.noGravity = true;
                }
            }
            if (Projectile.timeLeft == 20) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    for (int i = 0; i < 3; i++) {
                        int proj = Projectile.NewProjectile(player.parent(), Projectile.Center, (Projectile.rotation + (-1 + i) * 0.1f).ToRotationVector2() * 12
                        , ModContent.ProjectileType<EmesisGore>(), shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI, 0);
                        Main.projectile[proj].penetrate = 3;
                    }
                }

                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.Center, 10, 10, DustID.Shadowflame);
                    dust.velocity = Vector2.Normalize(owner.rotation.ToRotationVector2() * 7).RotatedByRandom(MathHelper.ToRadians(15f));
                    dust.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.9f, Pitch = -0.5f }, Projectile.Center);
            }
            if (Projectile.timeLeft == 1) {
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 1.2f, Pitch = -0.2f }, Projectile.Center);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    for (int i = 0; i < (Main.zenithWorld ? 115 : 5); i++) {
                        int proj = Projectile.NewProjectile(player.parent(), Projectile.Center, (Projectile.rotation + (-2 + i) * 0.1f).ToRotationVector2() * 15
                        , ModContent.ProjectileType<EmesisGore>(), shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI, 0);
                        Main.projectile[proj].penetrate = 2;
                    }
                }
                for (int i = 0; i < 15; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.Center, 10, 10, DustID.Shadowflame);
                    dust.velocity = Vector2.Normalize(owner.rotation.ToRotationVector2() * 7).RotatedByRandom(MathHelper.ToRadians(15f));
                    dust.noGravity = true;
                }
            }
        }
    }
}
