using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class AbsoluteZeroHeld : BaseKnife
    {
        public override string gradientTexturePath => CWRConstant.ColorBar + "AbsoluteZero_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 60;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            Length = 56;
            autoSetShoot = true;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), ShootSpanPos
                , ShootVelocity, ModContent.ProjectileType<DarkIceBomb>()
                , (int)(Projectile.damage * 0.8f), Projectile.knockBack * 0.8f, Owner.whoAmI);
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1Ratio: 0.2f, phase1SwingSpeed: 6.2f
                    , phase2SwingSpeed: 2f, phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(CWRID.Buff_GlacialState, 60);
            var source = Owner.GetSource_ItemUse(Item);
            int p = Projectile.NewProjectile(source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<DarkIceBomb>(), (int)(Item.damage * 1.25f), 12f, Owner.whoAmI);
            Main.projectile[p].timeLeft = 12;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(CWRID.Buff_GlacialState, 60);
            var source = Owner.GetSource_ItemUse(Item);
            int p = Projectile.NewProjectile(source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<DarkIceBomb>(), (int)(Item.damage * 1.25f), 12f, Owner.whoAmI);
            Main.projectile[p].timeLeft = 12;
        }
    }
}
