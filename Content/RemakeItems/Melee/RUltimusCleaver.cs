using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RUltimusCleaver : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<UltimusCleaver>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 72;
            Item.height = 62;
            Item.damage = 130;
            Item.DamageType = DamageClass.Melee;
            Item.useTurn = true;
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = null;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8f;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.autoReuse = true;
            Item.SetKnifeHeld<UltimusCleaverHeld>();
        }
    }

    internal class UltimusCleaverHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<UltimusCleaver>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "UltimusCleaver_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 46;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 36;
            Length = 70;
            unitOffsetDrawZkMode = -8;
            overOffsetCachesRoting = MathHelper.ToRadians(8);
            SwingData.starArg = 68;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 80;
            SwingData.maxClampLength = 90;
            SwingData.ler1_UpSizeSengs = 0.056f;
        }

        public override void Shoot() {
            for (int i = 0; i < 6; i++) {
                Projectile.NewProjectile(Source, Owner.Center, new Vector2(Projectile.spriteDirection * (7 + i * 0.2f), Main.rand.Next(-13, 0))
                , ModContent.ProjectileType<UltimusCleaverDust>(), Projectile.damage / 4, Projectile.knockBack, Owner.whoAmI);
            }
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 6.2f, phase2SwingSpeed: 4f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                target.AddBuff(BuffID.OnFire3, 360);
                int onHitDamage = Projectile.damage / 6;
                Owner.ApplyDamageToNPC(target, onHitDamage, 0f, 0, false);
                float firstDustScale = 1.7f;
                float secondDustScale = 0.8f;
                float thirdDustScale = 2f;
                Vector2 dustRotation = (target.rotation - 1.57079637f).ToRotationVector2();
                Vector2 dustVelocity = dustRotation * target.velocity.Length();
                SoundEngine.PlaySound(SoundID.Item14, target.Center);
                int increment;
                for (int i = 0; i < 40; i = increment + 1) {
                    int swingDust = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, DustID.InfernoFork, 0f, 0f, 200, default, firstDustScale);
                    Dust dust = Main.dust[swingDust];
                    dust.position = target.Center + Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * target.width / 2f;
                    dust.noGravity = true;
                    dust.velocity.Y -= 6f;
                    dust.velocity *= 3f;
                    dust.velocity += dustVelocity * Main.rand.NextFloat();
                    swingDust = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, DustID.InfernoFork, 0f, 0f, 100, default, secondDustScale);
                    dust.position = target.Center + Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * target.width / 2f;
                    dust.velocity.Y -= 6f;
                    dust.velocity *= 2f;
                    dust.noGravity = true;
                    dust.fadeIn = 1f;
                    dust.color = Color.Crimson * 0.5f;
                    dust.velocity += dustVelocity * Main.rand.NextFloat();
                    increment = i;
                }
                for (int j = 0; j < 20; j = increment + 1) {
                    int swingDust2 = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, DustID.InfernoFork, 0f, 0f, 0, default, thirdDustScale);
                    Dust dust = Main.dust[swingDust2];
                    dust.position = target.Center + Vector2.UnitX.RotatedByRandom(3.1415927410125732).RotatedBy((double)target.velocity.ToRotation(), default) * target.width / 3f;
                    dust.noGravity = true;
                    dust.velocity.Y -= 6f;
                    dust.velocity *= 0.5f;
                    dust.velocity += dustVelocity * (0.6f + 0.6f * Main.rand.NextFloat());
                    increment = j;
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 360);
            SoundEngine.PlaySound(SoundID.Item14, target.Center);
        }
    }
}
