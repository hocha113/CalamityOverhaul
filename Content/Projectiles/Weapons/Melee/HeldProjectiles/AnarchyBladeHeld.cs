using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.VulcaniteProj;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class AnarchyBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<AnarchyBlade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BloodRed_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 86;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommMode = 50;
            trailTopWidth = 30;
            Length = 90;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            Projectile.localNPCHitCooldown = 16;
        }

        public override void Shoot() {
            Item.initialize();
            if (++Item.CWR().ai[0] > 2) {
                Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<AnarchyBeam>(), Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI);
                Item.CWR().ai[0] = 0;
            }
        }

        public override bool PreInOwnerUpdate() {
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
