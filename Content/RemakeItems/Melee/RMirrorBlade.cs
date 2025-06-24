﻿using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RMirrorBlade : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<MirrorBlade>();
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<MirrorBladeHeld>();
        }
    }

    internal class MirrorBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<MirrorBlade>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "BrinyBaron_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 30;
            distanceToOwner = 12;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = 50;
            Projectile.height = 50;
            Length = 54;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<MirrorBlast>()
                , Projectile.damage, Projectile.knockBack, Owner.whoAmI, 0f, 0f);
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }
    }
}
