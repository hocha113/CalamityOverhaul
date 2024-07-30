using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using Mono.Cecil;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class EutrophicScimitarHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<EutrophicScimitar>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "BrinyBaron_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 3f;
            distanceToOwner = 76;
            trailTopWidth = 30;
        }

        public override void Shoot() {
            Vector2 origVr = ShootVelocity * 3f;
            for (int i = 0; i <= 21; i++) {
                Dust dust;
                dust = Main.dust[Dust.NewDust(new Vector2(Owner.Center.X - 58 / 2, Owner.Center.Y - 58 / 2), 58, 58, 226, 0f, 0f, 0, new Color(255, 255, 255), 0.4605263f)];
                dust.noGravity = true;
                dust.fadeIn = 0.9473684f;
            }

            for (int projectiles = 0; projectiles < 2; projectiles++) {
                Projectile.NewProjectile(Source, Owner.Center, origVr.RotatedBy(Main.rand.NextFloat(-0.05f, 0.05f)), ModContent.ProjectileType<EutrophicScimitarProj>(), (int)(Projectile.damage * 0.7), Projectile.knockBack, Owner.whoAmI);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            
        }
    }
}
