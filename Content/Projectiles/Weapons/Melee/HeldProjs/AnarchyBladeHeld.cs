using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
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
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 30;
            Length = 90;
            autoSetShoot = true;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void Shoot() {
            Item.Initialize();
            if (++Item.CWR().ai[0] > 3) {
                Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<AnarchyBeam>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI);
                Item.CWR().ai[0] = 0;
            }
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.1f, phase1SwingSpeed: 3.2f, phase2SwingSpeed: 6f);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.NewProjectile(Source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<BrimstoneBoom>(), Item.damage / 3, Item.knockBack, Owner.whoAmI);
            target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.NewProjectile(Source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<BrimstoneBoom>(), Item.damage / 3, Item.knockBack, Owner.whoAmI);
            target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);
        }
    }
}
