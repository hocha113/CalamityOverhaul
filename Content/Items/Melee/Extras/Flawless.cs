using CalamityMod;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
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
        private int Time2;
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
            SwingData.starArg = 30;
            SwingData.ler1_UpLengthSengs = 0.05f;
            SwingData.minClampLength = 200;
            SwingData.maxClampLength = 210;
            SwingData.ler1_UpSizeSengs = 0.016f;
            SwingData.baseSwingSpeed = 4.2f;
            autoSetShoot = true;
        }

        private void stab() {
            if (Time == 0) {
                Length = 80;
                startVector = RodingToVer(1, Projectile.velocity.ToRotation());
                speed = 1 + 0.6f / updateCount / SwingMultiplication;
            }

            if (Time < 6 * updateCount) {
                Vector2 spanSparkPos = Projectile.Center + Projectile.velocity.UnitVector() * Length;
                BasePRT spark = new PRT_Spark(spanSparkPos, Projectile.velocity, false, 4, 2.26f, Color.AliceBlue, Owner);
                PRTLoader.AddParticle(spark);
            }

            Length *= speed;
            vector = startVector * Length * SwingMultiplication;
            speed -= 0.015f / updateCount;

            if (Time >= 26 * updateCount * SwingMultiplication) {
                Projectile.Kill();
            }
            float toTargetSengs = Projectile.Center.To(Owner.Center).Length();
            Projectile.scale = 0.8f + toTargetSengs / 520f;
            if (Time % updateCount == updateCount - 1) {
                Length = MathHelper.Clamp(Length, 60, 160);
            }
        }

        public override bool PreSwingAI() {
            Projectile.ai[0] = 1;
            if (Projectile.ai[0] == 0) {
                SwingAIType = SwingAITypeEnum.None;
                ExecuteAdaptiveSwing(
                phase0SwingSpeed: -0.6f,
                phase1SwingSpeed: 12.2f,
                phase2SwingSpeed: 2f,
                swingSound: SoundID.Item1 with { Pitch = -0.6f });
            }
            else if (Projectile.ai[0] == 1) {
                stab();
                return false;
            }
            else if (Projectile.ai[0] == 2) {
                SwingAIType = SwingAITypeEnum.UpAndDown;
                ExecuteAdaptiveSwing(
                phase0SwingSpeed: -0.6f,
                phase1SwingSpeed: 12.2f,
                phase2SwingSpeed: 2f,
                swingSound: SoundID.Item1 with { Pitch = -0.6f });
            }
            else if (Projectile.ai[0] == 3) {
                stab();
                return false;
            }
            return true;
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.KnifeHitNPC(target, hit, damageDone);
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
