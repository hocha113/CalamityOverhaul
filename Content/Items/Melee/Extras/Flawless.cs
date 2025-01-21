using CalamityMod;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    /// <summary>
    /// 化境
    /// </summary>
    internal class Flawless : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Melee + "Flawless";
        private int Level;
        internal Asset<Texture2D> flawlessDownAsset;
        void ICWRLoader.LoadAsset() => flawlessDownAsset = CWRUtils.GetT2DAsset(Texture + "2");
        void ICWRLoader.UnLoadData() => flawlessDownAsset = null;
        public override void SetDefaults() {
            Item.width = 74;
            Item.height = 74;
            Item.value = Terraria.Item.sellPrice(gold: 75);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useAnimation = 32;
            Item.useTime = 32;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.damage = 920;
            Item.crit = 16;
            Item.knockBack = 7.5f;
            Item.noUseGraphic = true;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.noMelee = true;
            Item.channel = true;
            Item.shootSpeed = 10f;
            Item.shoot = ModContent.ProjectileType<FlawlessHeld>();
            Item.rare = ModContent.RarityType<Violet>();
            Item.CWR().GetMeleePrefix = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int newdmg = damage;
            if (Level == 1) {
                newdmg = (int)(damage * 1.15f);
            }
            else if (Level == 2) {
                newdmg = (int)(damage * 1.25f);
            }
            else if (Level == 3) {
                newdmg = (int)(damage * 1.55f);
            }
            Projectile.NewProjectile(source, position, velocity, type, newdmg, knockback, player.whoAmI, Level);
            if (++Level > 3) {
                Level = 0;
            }
            return false;
        }
    }

    internal class FlawlessHeld : BaseKnife
    {
        public override string Texture => CWRConstant.Item_Melee + "FlawlessHeld";
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Flawless_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = -20;
            drawTrailBtommWidth = 80;
            drawTrailTopWidth = 80;
            drawTrailCount = 6;
            Length = 200;
            unitOffsetDrawZkMode = 0;
            Projectile.width = Projectile.height = 186;
            distanceToOwner = -60;
            SwingData.starArg = -30;
            SwingData.ler1_UpLengthSengs = 0.05f;
            SwingData.minClampLength = 200;
            SwingData.maxClampLength = 210;
            SwingData.ler1_UpSizeSengs = 0.016f;
            SwingData.baseSwingSpeed = 4.2f;
            autoSetShoot = true;
        }

        public override bool PreSwingAI() {
            if (Projectile.ai[0] == 1) {
                StabBehavior(initialLength: 100, lifetime: 26, scaleFactorDenominator: 520f, minLength: 100, maxLength: 220);
                if (Time < 6 * updateCount) {
                    Vector2 spanSparkPos = Projectile.Center + Projectile.velocity.UnitVector() * Length / 2;
                    BasePRT spark = new PRT_Spark(spanSparkPos, Projectile.velocity, false, 4, 2.26f, Color.AliceBlue, Owner);
                    PRTLoader.AddParticle(spark);
                }
                return false;
            }
            else if (Projectile.ai[0] == 2) {
                ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: -1.3f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 4f
                , phase0MeleeSizeIncrement: 0.002f, phase2MeleeSizeIncrement: -0.008f);
                SwingBehavior(SwingData);
                return false;
            }
            else if (Projectile.ai[0] == 3) {
                StabBehavior(initialLength: 160, lifetime: 16, scaleFactorDenominator: 320f, minLength: 160, maxLength: 220);
                if (Time < 6 * updateCount) {
                    Vector2 spanSparkPos = Projectile.Center + Projectile.velocity.UnitVector() * Length / 2;
                    BasePRT spark = new PRT_Spark(spanSparkPos, Projectile.velocity, false, 4, 2.26f, Color.AliceBlue, Owner);
                    PRTLoader.AddParticle(spark);
                }
                return false;
            }
            SwingAIType = SwingAITypeEnum.UpAndDown;
            SwingIndex = 1;
            SwingData.starArg = -160;
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: -2.3f
                , phase1SwingSpeed: 12.2f, phase2SwingSpeed: 4f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0.004f);
            SwingBehavior(SwingData);
            return false;
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {

        }

        public override void SwingModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {

        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return null;
        }
    }
}
