﻿using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RStarfury : CWRItemOverride
    {
        public override int TargetID => ItemID.Starfury;
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.useTime = 12;
            item.damage = 15;
            item.SetKnifeHeld<StarfuryHeld>();
        }
    }

    internal class StarfuryHeld : BaseKnife
    {
        public override int TargetID => ItemID.Starfury;
        public override string gradientTexturePath => CWRConstant.ColorBar + "Gel_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 30;
            distanceToOwner = -22;
            drawTrailBtommWidth = 10;
            SwingData.baseSwingSpeed = 3f;
            Projectile.width = Projectile.height = 46;
            Length = 42;
            SwingData.starArg = 50;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void Shoot() {
            int damage = Item.damage;
            Item.damage = (int)(damage * 0.7f);
            OrigItemShoot();
            Item.damage = damage;
        }
    }
}
