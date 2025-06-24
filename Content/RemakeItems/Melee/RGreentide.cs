using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RGreentide : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Greentide>();
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(player, source, position, velocity, type, damage, knockback);
        }

        public static void SetDefaultsFunc(Item Item) {
            Item.damage = 95;
            Item.DamageType = DamageClass.Melee;
            Item.width = 62;
            Item.height = 62;
            Item.scale = 1.2f;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7;
            Item.value = CalamityGlobalItem.RarityLimeBuyPrice;
            Item.rare = ItemRarityID.Lime;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<GreenWater>();
            Item.shootSpeed = 18f;
            Item.SetKnifeHeld<GreentideHeld>();
        }

        public static bool ShootFunc(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 1);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class GreentideHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Greentide>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Greentide_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 46;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 52;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            ShootSpeed = 32f;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 1) {
                return;
            }
            Projectile.NewProjectile(Source, InMousePos + new Vector2(0, 600), new Vector2(0, -22).RotatedByRandom(0.3f)
                    , ModContent.ProjectileType<GreenWater>(), Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI);
        }

        public override void MeleeEffect() {
            int randomDust = Main.rand.Next(2);
            randomDust = randomDust == 0 ? 33 : 89;
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, randomDust);
        }

        public override bool PreInOwner() {
            if (Projectile.ai[0] == 0 && Projectile.IsOwnedByLocalPlayer() && Time % (12 * UpdateRate) == 0) {
                Projectile.NewProjectileDirect(Source, InMousePos, ShootVelocity.RotatedByRandom(0.3f)
                    , ModContent.ProjectileType<GreenWater>(), Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI);

            }

            if (Projectile.ai[0] == 1) {
                distanceToOwner = 50;
                SwingData.baseSwingSpeed = 5;
                SwingData.ler1_UpSizeSengs = 0.036f;
                SwingData.ler1_UpLengthSengs = 0.1f;
                SwingData.minClampLength = 90;
                SwingData.maxClampLength = 100;
            }

            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.1f, phase1SwingSpeed: 3.2f, phase2SwingSpeed: 6f);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0 && Projectile.ai[0] != 0) {
                Vector2 destination = target.Center;

                Vector2 initialPosition = destination - (Vector2.UnitY * (destination.Y - Main.screenPosition.Y + 80f));
                Vector2 initialCachedPosition = initialPosition;
                Vector2 secondaryPosition = initialCachedPosition + (Vector2.UnitY * (Main.screenHeight + 160f));
                Vector2 secondaryCachedPosition = secondaryPosition;

                Vector2 initialVelocity = (destination - initialPosition).SafeNormalize(Vector2.UnitY) * ShootSpeed;
                Vector2 initialCachedVelocity = initialVelocity;
                Vector2 secondaryVelocity = (destination - secondaryPosition).SafeNormalize(Vector2.UnitY) * ShootSpeed;
                Vector2 secondaryCachedVelocity = secondaryVelocity;

                int teethDamage = Projectile.damage / 2;
                float teethKnockback = Item.knockBack * 0.2f;
                bool evenProjectiles = 5 % 2 == 0;
                float offsetAmount = evenProjectiles ? 0.5f : 0f;
                int centralIndex = 5 / 2;
                float minVelAdj = 0.8f;
                float maxVelAdj = 1f;
                float xVelocityReduction = 0.9f;

                for (int i = 0; i < 2; i++) {
                    bool isTop = i == 0;
                    Vector2 currentPos = isTop ? initialPosition : secondaryPosition;
                    Vector2 cachedPos = isTop ? initialCachedPosition : secondaryCachedPosition;
                    Vector2 currentVelocity = isTop ? initialVelocity : secondaryVelocity;
                    Vector2 cachedVelocity = isTop ? initialCachedVelocity : secondaryCachedVelocity;

                    for (int j = 0; j < 5; j++) {
                        float velAdj = ((j == centralIndex || j == centralIndex - 1) && evenProjectiles) ? minVelAdj
                            : MathHelper.Lerp(minVelAdj, maxVelAdj, Math.Abs((j + offsetAmount) - centralIndex) / centralIndex);

                        currentPos.X += MathHelper.Lerp(-480, 480, j / (float)(5 - 1));
                        currentVelocity = CalamityUtils.CalculatePredictiveAimToTargetMaxUpdates(currentPos, target, ShootSpeed, 1) * velAdj;
                        currentVelocity.X *= xVelocityReduction;

                        Projectile.NewProjectile(Source, currentPos, currentVelocity, ModContent.ProjectileType<GreenWater>()
                            , teethDamage, teethKnockback, Owner.whoAmI, 0f, i, target.Center.Y);

                        currentPos = cachedPos;
                        currentVelocity = cachedVelocity;
                    }
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.numHits == 0 && Projectile.ai[0] != 0) {
                Vector2 destination = target.Center;

                Vector2 initialPosition = destination - (Vector2.UnitY * (destination.Y - Main.screenPosition.Y + 80f));
                Vector2 initialCachedPosition = initialPosition;
                Vector2 secondaryPosition = initialCachedPosition + (Vector2.UnitY * (Main.screenHeight + 160f));
                Vector2 secondaryCachedPosition = secondaryPosition;

                Vector2 initialVelocity = (destination - initialPosition).SafeNormalize(Vector2.UnitY) * ShootSpeed;
                Vector2 initialCachedVelocity = initialVelocity;
                Vector2 secondaryVelocity = (destination - secondaryPosition).SafeNormalize(Vector2.UnitY) * ShootSpeed;
                Vector2 secondaryCachedVelocity = secondaryVelocity;

                int teethDamage = Projectile.damage / 2;
                float teethKnockback = Item.knockBack * 0.2f;
                bool evenProjectiles = 5 % 2 == 0;
                float offsetAmount = evenProjectiles ? 0.5f : 0f;
                int centralIndex = 5 / 2;
                float minVelAdj = 0.8f;
                float maxVelAdj = 1f;
                float xVelocityReduction = 0.9f;

                for (int i = 0; i < 2; i++) {
                    bool isTop = i == 0;
                    Vector2 currentPos = isTop ? initialPosition : secondaryPosition;
                    Vector2 cachedPos = isTop ? initialCachedPosition : secondaryCachedPosition;
                    Vector2 currentVelocity = isTop ? initialVelocity : secondaryVelocity;
                    Vector2 cachedVelocity = isTop ? initialCachedVelocity : secondaryCachedVelocity;

                    for (int j = 0; j < 5; j++) {
                        float velAdj = ((j == centralIndex || j == centralIndex - 1) && evenProjectiles) ? minVelAdj
                            : MathHelper.Lerp(minVelAdj, maxVelAdj, Math.Abs((j + offsetAmount) - centralIndex) / centralIndex);

                        currentPos.X += MathHelper.Lerp(-480, 480, j / (float)(5 - 1));
                        currentVelocity = CalamityUtils.CalculatePredictiveAimToTargetMaxUpdates(currentPos, target, ShootSpeed, 1) * velAdj;
                        currentVelocity.X *= xVelocityReduction;

                        Projectile.NewProjectile(Source, currentPos, currentVelocity, ModContent.ProjectileType<GreenWater>()
                            , teethDamage, teethKnockback, Owner.whoAmI, 0f, i, target.Center.Y);

                        currentPos = cachedPos;
                        currentVelocity = cachedVelocity;
                    }
                }
            }
        }
    }
}
