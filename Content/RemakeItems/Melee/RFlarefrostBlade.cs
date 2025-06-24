﻿using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RFlarefrostBlade : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<FlarefrostBlade>();
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<FlarefrostBladeHeld>();
        }
    }

    internal class FlarefrostBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<FlarefrostBlade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "FlarefrostBlade_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 56;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 30;
            drawTrailCount = 8;
            Length = 68;
            SwingData.baseSwingSpeed = 3.65f;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            ShootSpeed = 11;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<Flarefrost>(), Projectile.damage
                , Projectile.knockBack, Owner.whoAmI, 0f, 0);
        }

        public override void MeleeEffect() {
            int dustChoice = Main.rand.Next(2);
            dustChoice = dustChoice == 0 ? 67 : 6;
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustChoice);
            }
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(phase1Ratio: 0.2f, phase0SwingSpeed: -0.1f, phase1SwingSpeed: 4.2f, phase2SwingSpeed: 3f, phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 180);
            target.AddBuff(BuffID.Frostburn2, 180);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 180);
            target.AddBuff(BuffID.Frostburn2, 180);
        }
    }
}
