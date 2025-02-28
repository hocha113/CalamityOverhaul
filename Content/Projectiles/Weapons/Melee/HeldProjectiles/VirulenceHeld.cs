using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class VirulenceHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Virulence>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Plague_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 36;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5f;
            drawTrailBtommWidth = 20;
            distanceToOwner = 8;
            drawTrailTopWidth = 18;
            Length = 40;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity * 1.6f
                , ModContent.ProjectileType<VirulentWave>(), (int)(Projectile.damage * 0.85)
                , Projectile.knockBack, Owner.whoAmI, 0f, 0f);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Plague>(), 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Plague>(), 300);
        }
    }
}
