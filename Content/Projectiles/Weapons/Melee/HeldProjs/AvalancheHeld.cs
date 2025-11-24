using CalamityOverhaul.Content.MeleeModify.Core;
using System;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class AvalancheHeld : BaseKnife
    {
        public override string gradientTexturePath => CWRConstant.ColorBar + "BrinyBaron_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 44;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 4f;
            drawTrailBtommWidth = 50;
            distanceToOwner = 26;
            drawTrailTopWidth = 18;
            Incandescence = true;
            SwingAIType = SwingAITypeEnum.UpAndDown;
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if ((CWRLoad.WormBodys.Contains(target.type) || target.type == NPCID.Probe) && !Main.rand.NextBool(2)) {
                return;
            }
            if (Projectile.numHits > 0) {
                return;
            }
            Projectile.numHits++;
            int totalProjectiles = 4;
            float radians = MathHelper.TwoPi / totalProjectiles;
            int type = CWRID.Proj_IceBombFriendly;
            int bombDamage = (int)Owner.GetTotalDamage<MeleeDamageClass>().ApplyTo(Item.damage);
            float velocity = 4f;
            double angleA = radians * 0.5;
            double angleB = MathHelper.ToRadians(90f) - angleA;
            float velocityX = (float)(velocity * Math.Sin(angleA) / Math.Sin(angleB));
            Vector2 spinningPoint = Main.rand.NextBool() ? new Vector2(0f, -velocity) : new Vector2(-velocityX, -velocity);
            for (int k = 0; k < totalProjectiles; k++) {
                Vector2 projRotation = spinningPoint.RotatedBy(radians * k);
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), target.Center, projRotation, type, bombDamage, hit.Knockback, Main.myPlayer);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            int totalProjectiles = 4;
            float radians = MathHelper.TwoPi / totalProjectiles;
            int type = CWRID.Proj_IceBombFriendly;
            int bombDamage = (int)Owner.GetTotalDamage<MeleeDamageClass>().ApplyTo(Item.damage);
            float velocity = 4f;
            double angleA = radians * 0.5;
            double angleB = MathHelper.ToRadians(90f) - angleA;
            float velocityX = (float)(velocity * Math.Sin(angleA) / Math.Sin(angleB));
            Vector2 spinningPoint = Main.rand.NextBool() ? new Vector2(0f, -velocity) : new Vector2(-velocityX, -velocity);
            for (int k = 0; k < totalProjectiles; k++) {
                Vector2 projRotation = spinningPoint.RotatedBy(radians * k);
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), target.Center, projRotation, type, bombDamage, 0f, Main.myPlayer);
            }
        }
    }
}
