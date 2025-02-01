using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTrueCausticEdge : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<TrueCausticEdge>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<TrueCausticEdgeHeld>();
    }

    internal class TrueCausticEdgeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<TrueCausticEdge>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BloodRed_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 46;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 30;
            Length = 70;
            unitOffsetDrawZkMode = -4;
            overOffsetCachesRoting = MathHelper.ToRadians(8);
            SwingData.starArg = 80;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 80;
            SwingData.maxClampLength = 90;
            SwingData.ler1_UpSizeSengs = 0.056f;
            ShootSpeed = 12;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<CausticEdgeProjectile>()
                , Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(3 * UpdateRate)) {
                int dustType = Main.rand.NextBool() ? DustID.GreenFairy : DustID.Venom;
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType, 0f, 0f, 100, default, Main.rand.NextFloat(1.8f, 2.4f));
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0f;
                if (dustType == DustID.Venom)
                    Main.dust[dust].fadeIn = 1.5f;
            }
            return base.PreInOwnerUpdate();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Venom, 180);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Venom, 180);
        }
    }
}
