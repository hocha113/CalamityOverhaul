using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class BrinyBaronHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<BrinyBaron>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BrinyBaron_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            trailCount = 8;
            distanceToOwner = 60;
            ownerOrientationLock = true;
            SwingData.baseSwingSpeed = 4.55f;
        }

        public override bool PreInOwnerUpdate() {
            if (Projectile.ai[0] == 1) {
                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item71, Owner.position);
                }
                distanceToOwner = 100;
                SwingAIType = SwingAITypeEnum.None;
                SwingData.starArg = 63;
                SwingData.baseSwingSpeed = 6;
                SwingData.ler1_UpLengthSengs = 0.11f;
                SwingData.ler1_UpSizeSengs = 0.06f;
                SwingData.minClampLength = 90;
                SwingData.maxClampLength = 130;
            }
            else if (Projectile.ai[0] == 2) {
                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item88, Owner.position);
                }
                SwingAIType = SwingAITypeEnum.None;
                shootSengs = 0.95f;
                maxSwingTime = 70;
                canDrawSlashTrail = false;
                SwingData.starArg = 13;
                SwingData.baseSwingSpeed = 2.2f;
                SwingData.ler1_UpLengthSengs = 0.1f;
                SwingData.ler1_UpSpeedSengs = 0.09f;
                SwingData.ler1_UpSizeSengs = 0.012f;
                SwingData.ler2_DownLengthSengs = 0.1f;
                SwingData.ler2_DownSpeedSengs = 0.22f;
                SwingData.maxSwingTime = 40;
            }
            return true;
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<Razorwind>();
            if (Projectile.ai[0] == 1) {
                type = ProjectileID.Bubble;
                for (int i = 0; i < 23; i++) {
                    int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center
                    , UnitToMouseV.RotatedByRandom(1.9f) * Item.shootSpeed * Main.rand.NextFloat(1.6f, 2.1f)
                    , type, Projectile.damage / 2, 2, Owner.whoAmI);
                    Main.projectile[proj].DamageType = DamageClass.Melee;
                    Main.projectile[proj].extraUpdates = 2;
                }
                return;
            }
            if (Projectile.ai[0] == 2) {
                SoundEngine.PlaySound(SoundID.Item20 with { MaxInstances = 6 }, Owner.Center);
                type = ModContent.ProjectileType<BrinyBaronOrb>();
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center
                    , safeInSwingUnit.RotatedBy((-1 + i) * 0.1f) * 12
                , type, Projectile.damage, 2, Owner.whoAmI);
                }
                int constant = 36;
                for (int j = 0; j < constant; j++) {
                    Vector2 vr = (MathHelper.TwoPi / constant * j).ToRotationVector2() * 13;
                    int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.DungeonWater, 0, 0, 100, default, 1.4f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].noLight = true;
                    Main.dust[dust].velocity = vr;
                }
                return;
            }
            SoundEngine.PlaySound(SoundID.Item84, Owner.Center);
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center, UnitToMouseV * Item.shootSpeed
                , type, (int)(Projectile.damage * 0.5f), (float)(Projectile.knockBack * 0.5), Owner.whoAmI);
        }
    }
}
