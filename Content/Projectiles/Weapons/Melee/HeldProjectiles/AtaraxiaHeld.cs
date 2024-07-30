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
    internal class AtaraxiaHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Ataraxia>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Ataraxia_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5f;
            distanceToOwner = 76;
            trailTopWidth = 30;
        }

        public override void Shoot() {
            SoundEngine.PlaySound(SoundID.Item60, Owner.Center);
            Vector2 origVr = ShootVelocity * 1.5f;
            Projectile.NewProjectile(Source, ShootSpanPos + origVr * 2, origVr
                , ModContent.ProjectileType<AtaraxiaMain>(), Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI);
            Vector2 offsetPos = ShootVelocity.GetNormalVector() * 15;
            Projectile.NewProjectile(Source, ShootSpanPos + offsetPos, origVr
                , ModContent.ProjectileType<AtaraxiaSide>(), Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI, 0, 2);
            Projectile.NewProjectile(Source, ShootSpanPos - offsetPos, origVr
                , ModContent.ProjectileType<AtaraxiaSide>(), Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI, 0, 1);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 480);
            if (Projectile.numHits == 0) {
                SoundStyle fire = new("CalamityMod/Sounds/Item/CursedDaggerThrow");
                SoundEngine.PlaySound(fire with { Volume = 0.5f, Pitch = 0.9f, PitchVariance = 0.2f, MaxInstances = -1 }, Owner.Center);
            }

            int trueMeleeID = ModContent.ProjectileType<AtaraxiaBoom>();
            int trueMeleeDamage = (int)Owner.GetTotalDamage<MeleeDamageClass>().ApplyTo(0.7f * Item.damage);
            Projectile.NewProjectile(Source, target.Center, Vector2.Zero, trueMeleeID, trueMeleeDamage, Item.knockBack, Owner.whoAmI, 0.0f, 0.0f);
        }
    }
}
