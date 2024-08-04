using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using System;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class AvalancheHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Avalanche>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "BrinyBaron_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 44;
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 4f;
            drawTrailBtommWidth = 50;
            distanceToOwner = 26;
            drawTrailTopWidth = 20;
            Incandescence = true;
            SwingAIType = SwingAITypeEnum.UpAndDown;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (CWRLoad.WormBodys.Contains(target.type) && !Main.rand.NextBool(3)) {
                return;
            }
            var source = Owner.GetSource_ItemUse(Item);
            int totalProjectiles = 4;
            float radians = MathHelper.TwoPi / totalProjectiles;
            int type = ModContent.ProjectileType<IceBombFriendly>();
            int bombDamage = Owner.CalcIntDamage<MeleeDamageClass>(Item.damage);
            float velocity = 4f;
            double angleA = radians * 0.5;
            double angleB = MathHelper.ToRadians(90f) - angleA;
            float velocityX = (float)(velocity * Math.Sin(angleA) / Math.Sin(angleB));
            Vector2 spinningPoint = Main.rand.NextBool() ? new Vector2(0f, -velocity) : new Vector2(-velocityX, -velocity);
            for (int k = 0; k < totalProjectiles; k++) {
                Vector2 projRotation = spinningPoint.RotatedBy(radians * k);
                Projectile.NewProjectile(source, target.Center, projRotation, type, bombDamage, hit.Knockback, Main.myPlayer);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            var source = Owner.GetSource_ItemUse(Item);
            int totalProjectiles = 4;
            float radians = MathHelper.TwoPi / totalProjectiles;
            int type = ModContent.ProjectileType<IceBombFriendly>();
            int bombDamage = Owner.CalcIntDamage<MeleeDamageClass>(Item.damage);
            float velocity = 4f;
            double angleA = radians * 0.5;
            double angleB = MathHelper.ToRadians(90f) - angleA;
            float velocityX = (float)(velocity * Math.Sin(angleA) / Math.Sin(angleB));
            Vector2 spinningPoint = Main.rand.NextBool() ? new Vector2(0f, -velocity) : new Vector2(-velocityX, -velocity);
            for (int k = 0; k < totalProjectiles; k++) {
                Vector2 projRotation = spinningPoint.RotatedBy(radians * k);
                Projectile.NewProjectile(source, target.Center, projRotation, type, bombDamage, 0f, Main.myPlayer);
            }
        }
    }
}
