using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RFloodtide : CWRItemOverride
    {
        public override void SetDefaults(Item item) => item.SetKnifeHeld<FloodtideHeld>();
    }

    internal class FloodtideHeld : BaseKnife
    {
        public override string gradientTexturePath => CWRConstant.ColorBar + "Floodtide_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            drawTrailTopWidth = 30;
            distanceToOwner = 30;
            drawTrailBtommWidth = 50;
            SwingData.starArg = 66;
            SwingData.baseSwingSpeed = 3.5f;
            Projectile.width = Projectile.height = 66;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Length = 86;
            ShootSpeed = 18;
        }

        public override void PostInOwner() {
            if (Main.rand.NextBool(5 * UpdateRate)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.FishronWings);
            }
        }

        public override void Shoot() {
            Vector2 velocity = ShootVelocity;
            for (int i = 0; i < 2; i++) {
                float SpeedX = velocity.X + Main.rand.Next(-20, 21) * 0.05f;
                float SpeedY = velocity.Y + Main.rand.Next(-20, 21) * 0.05f;
                Projectile.NewProjectile(Source, ShootSpanPos, new Vector2(SpeedX, SpeedY)
                    , CWRID.Proj_FloodtideShark, Projectile.damage
                    , Projectile.knockBack, Owner.whoAmI, 0f, 0f);
            }
        }
    }
}
