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
    internal class GeliticBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<GeliticBlade>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Gel_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5f;
            distanceToOwner = 48;
            trailTopWidth = 30;
        }

        public override void Shoot() {
            SoundEngine.PlaySound(SoundID.Item1, Owner.Center);
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity * 1.5f
                , ModContent.ProjectileType<GelWave>(), Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slimed, 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slimed, 300);
        }
    }
}
