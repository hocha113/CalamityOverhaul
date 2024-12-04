using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    /// <summary>
    /// 种子弯刀
    /// </summary>
    internal class RSeedler : BaseRItem
    {
        public override int TargetID => ItemID.Seedler;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<SeedlerHeld>();
        }
    }

    internal class SeedlerHeld : BaseKnife
    {
        public override int TargetID => ItemID.Seedler;
        public override string gradientTexturePath => CWRConstant.ColorBar + "Seedler_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 48;
            distanceToOwner = -20;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            unitOffsetDrawZkMode = 6;
            Length = 54;
            autoSetShoot = true;
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 6.2f, phase2SwingSpeed: 4f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ProjectileID.SeedlerNut, Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }
    }
}
