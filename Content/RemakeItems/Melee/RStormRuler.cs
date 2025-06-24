﻿using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RStormRuler : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<StormRuler>();

        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<StormRulerHeld>();
        }
    }

    internal class StormRulerHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<StormRuler>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "AbsoluteZero_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 82;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 3.65f;
            ShootSpeed = 20;
            SwingAIType = SwingAITypeEnum.UpAndDown;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<StormRulerProj>()
                , Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override bool PreInOwner() {
            if (Main.rand.NextBool(5 * UpdateRate)) {
                int swingDust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.Flare_Blue, Owner.direction * 2, 0f, 150, default, 1.3f);
                Main.dust[swingDust].velocity *= 0.2f;
            }
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1SwingSpeed: 6.2f, phase2SwingSpeed: 4f);
            return base.PreInOwner();
        }
    }
}
