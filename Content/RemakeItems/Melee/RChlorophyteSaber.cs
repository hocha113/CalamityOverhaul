using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RChlorophyteSaber : ItemOverride
    {
        public override int TargetID => ItemID.ChlorophyteSaber;
        public override bool CanLoadLocalization => false;
        public override void SetDefaults(Item item) {
            item.useTime = 10;
            item.SetKnifeHeld<ChlorophyteSaberHeld>();
            item.shootSpeed = 0;
        }
    }

    internal class RChlorophyteClaymore : ItemOverride
    {
        public override int TargetID => ItemID.ChlorophyteClaymore;
        public override bool CanLoadLocalization => false;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<ChlorophyteClaymoreHeld>();
    }

    internal class ChlorophyteSaberHeld : BaseKnife
    {
        public override int TargetID => ItemID.ChlorophyteSaber;
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 24;
            Length = 20;
            ShootSpeed = 26;
        }

        public override bool PreSwingAI() {
            StabBehavior(initialLength: 10, lifetime: maxSwingTime, scaleFactorDenominator: 420f, minLength: 10, maxLength: 60, ignoreUpdateCount:true);
            return false;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, AbsolutelyShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.6f, 1)
                    , ProjectileID.SporeCloud, Projectile.damage, Projectile.knockBack, Main.myPlayer);
            Projectile.NewProjectile(Source, ShootSpanPos, AbsolutelyShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.6f, 1)
                    , ProjectileID.SporeCloud, Projectile.damage / 2, Projectile.knockBack, Main.myPlayer);
        }
    }

    internal class ChlorophyteClaymoreHeld : BaseKnife
    {
        public override int TargetID => ItemID.ChlorophyteClaymore;
        public override string gradientTexturePath => CWRConstant.ColorBar + "Plague_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 36;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            SwingData.starArg = 50;
            SwingData.baseSwingSpeed = 3f;
            drawTrailBtommWidth = 20;
            distanceToOwner = 30;
            drawTrailTopWidth = 40;
            Length = 60;
            ShootSpeed = 13;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ProjectileID.ChlorophyteOrb, Projectile.damage, Projectile.knockBack, Owner.whoAmI, 0f, 0f);
        }
    }
}
