using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class AtaraxiaHeld : BaseKnife
    {
        public override string gradientTexturePath => CWRConstant.ColorBar + "Ataraxia_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            drawTrailHighlight = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5f;
            distanceToOwner = 26;
            drawTrailTopWidth = 30;
            Length = 80;
        }

        public override void Shoot() {
            SoundEngine.PlaySound(SoundID.Item60, Owner.Center);
            Vector2 origVr = ShootVelocity * 1.5f;
            Projectile.NewProjectile(Source, ShootSpanPos + origVr * 2, origVr
                , CWRID.Proj_AtaraxiaMain, (int)(Projectile.damage * 0.65f), Projectile.knockBack, Owner.whoAmI);
            Vector2 offsetPos = ShootVelocity.GetNormalVector() * 15;
            Projectile.NewProjectile(Source, ShootSpanPos + offsetPos, origVr
                , CWRID.Proj_AtaraxiaSide, Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI, 0, 2);
            Projectile.NewProjectile(Source, ShootSpanPos - offsetPos, origVr
                , CWRID.Proj_AtaraxiaSide, Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI, 0, 1);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 480);
            if (Projectile.numHits == 0) {
                SoundStyle fire = new("CalamityMod/Sounds/Item/CursedDaggerThrow");
                SoundEngine.PlaySound(fire with { Volume = 0.5f, Pitch = 0.9f, PitchVariance = 0.2f, MaxInstances = -1 }, Owner.Center);
            }

            int trueMeleeID = CWRID.Proj_AtaraxiaBoom;
            int trueMeleeDamage = (int)Owner.GetTotalDamage<MeleeDamageClass>().ApplyTo(0.7f * Item.damage);
            Projectile.NewProjectile(Source, target.Center, Vector2.Zero, trueMeleeID, trueMeleeDamage, Item.knockBack, Owner.whoAmI, 0.0f, 0.0f);
        }
    }
}
