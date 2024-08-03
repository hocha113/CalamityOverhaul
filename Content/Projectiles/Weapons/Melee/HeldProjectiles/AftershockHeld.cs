using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class AftershockHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Aftershock>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Aftershock_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 36;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 44;
            SwingAIType = SwingAITypeEnum.UpAndDown;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 1) {
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Source, ShootSpanPos + new Vector2(Main.rand.Next(-160, 160), -300), ShootVelocity
                    , ModContent.ProjectileType<MeleeFossilShard>(), Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI);
                }
                return;
            }
            for (int i = 0; i < 4; i++) {
                Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity + new Vector2(0, -Main.rand.Next(0, 6))
                , ModContent.ProjectileType<MeleeFossilShard>(), Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI);
            }
        }

        public override bool PreInOwnerUpdate() {
            if (Projectile.ai[0] == 1) {
                distanceToOwner = 40;
                SwingData.ler1_UpLengthSengs = 0.018f;
                SwingData.ler1_UpSizeSengs = 0.036f;
                SwingData.minClampLength = 70;
                SwingData.maxClampLength = 90;
            }
            return base.PreInOwnerUpdate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            base.OnHitPlayer(target, info);
        }
    }
}
