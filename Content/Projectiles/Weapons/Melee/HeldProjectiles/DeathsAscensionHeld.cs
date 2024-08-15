﻿using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee.Extras;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class DeathsAscensionHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<DeathsAscension>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail4";
        public override string gradientTexturePath => CWRConstant.ColorBar + "ExaltedOathblade_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 122;
            overOffsetCachesRoting = MathHelper.ToRadians(16);
            canDrawSlashTrail = true;
            drawTrailTopWidth = 52;
            drawTrailHighlight = false;
            drawTrailBtommWidth = 10;
            distanceToOwner = 50;
            Length = 70;
            OtherMeleeSize = 1.64f;
            unitOffsetDrawZkMode = 0;
            IgnoreImpactBoxSize = true;
            Incandescence = true;
        }

        public override void Shoot() {
            if (Projectile.ai[2] == 0) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 vr = ((-1 + i) * 0.2f).ToRotationVector2().RotatedBy(ToMouseA);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center + vr * Length, vr * 16,
                ModContent.ProjectileType<DeathsAscensionProjectile>(), (int)(Projectile.damage * 0.15f)
                , Projectile.knockBack / 2, Projectile.owner);
            }
        }

        public override bool PreInOwnerUpdate() {
            if (Time == 0) {
                SoundEngine.PlaySound(SoundID.Item71, Owner.Center);
            }
            
            if (Projectile.ai[1] == 8) {
                if (Time == 0) {
                    OtherMeleeSize = 2.24f;
                }
                SwingData.baseSwingSpeed = 12;
                OtherMeleeSize -= 0.012f;
                return true;
            }

            if (Projectile.ai[2] == 0) {
                if (Time == 0) {
                    OtherMeleeSize = 2.24f;
                }
                if (Projectile.ai[0] == 1) {
                    SwingData.baseSwingSpeed = 5;
                    SwingAIType = SwingAITypeEnum.Down;
                }
                OtherMeleeSize -= 0.01f;
            }
            else {
                overOffsetCachesRoting = MathHelper.ToRadians(6);
                SwingData.starArg = 60;
                SwingData.baseSwingSpeed = 5;
                OtherMeleeSize -= 0.006f;
            }

            return base.PreInOwnerUpdate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.ActiveItem().type == ModContent.ItemType<GuardianTerra>() && Projectile.numHits == 0) {
                int proj = Projectile.NewProjectile(new EntitySource_ItemUse(Owner, Owner.ActiveItem())
                    , Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TerratomereSlashCreator>(),
                Projectile.damage, 0, Projectile.owner, target.whoAmI, Main.rand.NextFloat(MathHelper.TwoPi));
                Main.projectile[proj].timeLeft = 130;
            }
        }
    }
}
