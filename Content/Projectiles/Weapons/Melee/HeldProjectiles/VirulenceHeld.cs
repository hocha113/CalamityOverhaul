using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class VirulenceHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Virulence>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Plague_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5f;
            distanceToOwner = 48;
            trailTopWidth = 30;
        }
        
        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity * 1.6f, ModContent.ProjectileType<VirulentWave>(), (int)(Projectile.damage * 0.85), Projectile.knockBack, Owner.whoAmI, 0f, 0f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Plague>(), 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Plague>(), 300);
        }
    }
}
