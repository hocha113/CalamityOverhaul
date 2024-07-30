using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class ForbiddenOathbladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<ForbiddenOathblade>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "DevilsDevastationEffectColorBar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 44;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 2.5f;
            drawTrailBtommMode = 50;
            distanceToOwner = 28;
            trailTopWidth = 30;
            SwingAIType = SwingAITypeEnum.UpAndDown;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<ForbiddenOathbladeProjectile>(), Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 120);
            target.AddBuff(BuffID.OnFire, 240);
            if (hit.Crit) {
                target.AddBuff(BuffID.ShadowFlame, 360);
                target.AddBuff(BuffID.OnFire, 720);
                int onHitDamage = Owner.CalcIntDamage<MeleeDamageClass>(2 * Item.damage);
                Owner.ApplyDamageToNPC(target, onHitDamage, 0f, 0, false);
                float firstDustScale = 1.7f;
                float secondDustScale = 0.8f;
                float thirdDustScale = 2f;
                Vector2 dustRotation = (target.rotation - 1.57079637f).ToRotationVector2();
                Vector2 dustVelocity = dustRotation * target.velocity.Length();
                SoundEngine.PlaySound(SoundID.Item14, target.Center);
                int dustIncr;
                for (int i = 0; i < 40; i = dustIncr + 1) {
                    int swingDust = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, 173, 0f, 0f, 200, default, firstDustScale);
                    Dust dust = Main.dust[swingDust];
                    dust.position = target.Center + Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * (float)target.width / 2f;
                    dust.noGravity = true;
                    dust.velocity.Y -= 4.5f;
                    dust.velocity *= 3f;
                    dust.velocity += dustVelocity * Main.rand.NextFloat();
                    swingDust = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, 173, 0f, 0f, 100, default, secondDustScale);
                    dust.position = target.Center + Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * (float)target.width / 2f;
                    dust.velocity.Y -= 3f;
                    dust.velocity *= 2f;
                    dust.noGravity = true;
                    dust.fadeIn = 1f;
                    dust.color = Color.Crimson * 0.5f;
                    dust.velocity += dustVelocity * Main.rand.NextFloat();
                    dustIncr = i;
                }
                for (int j = 0; j < 20; j = dustIncr + 1) {
                    int swingDust2 = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, 173, 0f, 0f, 0, default, thirdDustScale);
                    Dust dust = Main.dust[swingDust2];
                    dust.position = target.Center + Vector2.UnitX.RotatedByRandom(3.1415927410125732).RotatedBy((double)target.velocity.ToRotation(), default) * (float)target.width / 3f;
                    dust.noGravity = true;
                    dust.velocity.Y -= 1.5f;
                    dust.velocity *= 0.5f;
                    dust.velocity += dustVelocity * (0.6f + 0.6f * Main.rand.NextFloat());
                    dustIncr = j;
                }
            }
        }
        
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Shadowflame>(), 360);
            target.AddBuff(BuffID.OnFire, 720);
            SoundEngine.PlaySound(SoundID.Item14, target.Center);
        }
    }
}
