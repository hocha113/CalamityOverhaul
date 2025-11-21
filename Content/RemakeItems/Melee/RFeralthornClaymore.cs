using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RFeralthornClaymore : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetKnifeHeld<FeralthornClaymoreHeld>();
    }

    internal class FeralthornClaymoreHeld : BaseKnife
    {
        public override string gradientTexturePath => CWRConstant.ColorBar + "FeralthornClaymore_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 64;
            Projectile.scale = 1.24f;
            canDrawSlashTrail = true;
            overOffsetCachesRoting = MathHelper.ToRadians(6);
            SwingData.starArg = 54;
            SwingData.minClampLength = 110;
            SwingData.maxClampLength = 120;
            SwingData.baseSwingSpeed = 4f;
            distanceToOwner = 0;
            drawTrailTopWidth = 30;
            Length = 110;
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(4)) {
                Dust.NewDust(Projectile.position, Projectile.width
                    , Projectile.height, DustID.JungleSpore);
            }
        }

        public override void Shoot() {
            Vector2 spanPos = ShootSpanPos + ShootVelocity.UnitVector() * Length * Main.rand.NextFloat(0.6f, 8.2f);
            Projectile.NewProjectile(Source, spanPos, VaultUtils.RandVr(23, 35)
                , CWRID.Proj_ThornBase, (int)(Item.damage * 0.5), 0f, Main.myPlayer);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Venom, 300);
            Projectile.NewProjectile(Source, target.Center.X, target.Center.Y
                , Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(-18f, 18f)
                , CWRID.Proj_ThornBase, (int)(Item.damage * 0.5), 0f, Main.myPlayer);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Venom, 300);
            Projectile.NewProjectile(Source, target.Center.X, target.Center.Y
                , Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(-18f, 18f)
                , CWRID.Proj_ThornBase, (int)(Item.damage * 0.5), 0f, Main.myPlayer);
        }
    }
}
