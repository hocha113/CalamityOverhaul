using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 香肠矛
    /// </summary>
    internal class SausageMakerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "SausageMaker";
        public override void SetStaticDefaults() => ItemID.Sets.Spears[Item.type] = true;
        internal static int index;
        public override void SetDefaults() {
            Item.SetItemCopySD<SausageMaker>();
            Item.SetKnifeHeld<SausageMakerHeld>();
        }

        public static bool ShootFunc(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (++index > 4) {
                index = 0;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, index);
            return false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(Item, player, source, position, velocity, type, damage, knockback);
        }
    }

    internal class RSausageMaker : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<SausageMaker>();
        public override int ProtogenesisID => ModContent.ItemType<SausageMakerEcType>();
        public override string TargetToolTipItemName => "SausageMakerEcType";
        public override void SetDefaults(Item item) => item.SetKnifeHeld<SausageMakerHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return SausageMakerEcType.ShootFunc(item, player, source, position, velocity, type, damage, knockback);
        }
    }

    internal class SausageMakerHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<SausageMaker>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BloodRed_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 64;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 0;
            drawTrailBtommWidth = 20;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 52;
            SwingData.baseSwingSpeed = 6;
            autoSetShoot = true;
        }

        public override bool PreSwingAI() {
            if (Projectile.ai[0] == 0 || Projectile.ai[0] == 1) {
                if (Time == 0) {
                    OtherMeleeSize = 1.2f;
                }

                SwingData.baseSwingSpeed = 14;
                SwingAIType = SwingAITypeEnum.UpAndDown;

                if (Time < maxSwingTime / 3) {
                    OtherMeleeSize += 0.015f / SwingMultiplication;
                }
                else {
                    OtherMeleeSize -= 0.005f / SwingMultiplication;
                }
                return true;
            }

            StabBehavior(initialLength: 40, scaleFactorDenominator: 220f, minLength: 40, maxLength: 80, canDrawSlashTrail: true);
            return false;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 0 || Projectile.ai[0] == 1) {
                return;
            }
            Projectile.NewProjectile(Source, ShootSpanPos, AbsolutelyShootVelocity,
                ModContent.ProjectileType<BloodBall>(), (int)(Projectile.damage * 0.8), Projectile.knockBack * 0.85f, Projectile.owner);
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(5)) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
               , DustID.Blood, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<BurningBlood>(), 240);
            if (Projectile.ai[0] == 0 || Projectile.ai[0] == 1) {
                int heal = (int)MathHelper.Clamp(hit.Damage, 2, 16);
                if (Main.player[Main.myPlayer].lifeSteal <= 0f || heal <= 0 || target.lifeMax <= 5) {
                    return;
                }

                CalamityGlobalProjectile.SpawnLifeStealProjectile(Projectile, Owner, heal, ProjectileID.VampireHeal, 3000f);
                if (Projectile.ai[1] == 0) {
                    Projectile.NewProjectile(Source, Projectile.Center, AbsolutelyShootVelocity, Type, 0, 0f, Projectile.owner, 0, 1);
                }
            }
        }
    }
}
