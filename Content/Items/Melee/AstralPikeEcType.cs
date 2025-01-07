using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Dusts;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.AstralProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 幻星长矛
    /// </summary>
    internal class AstralPikeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AstralPike";
        public const int InTargetProjToLang = 1220;
        public const int ShootPeriod = 2;
        internal static int index;
        public override void SetStaticDefaults() => ItemID.Sets.Spears[Item.type] = true;
        public override void SetDefaults() {
            Item.width = 44;
            Item.damage = 90;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 13;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 13;
            Item.knockBack = 8.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 50;
            Item.value = CalamityGlobalItem.RarityCyanBuyPrice;
            Item.rare = ItemRarityID.Cyan;
            Item.shoot = ModContent.ProjectileType<AstralPikeHeld>();
            Item.shootSpeed = 13f;

        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 25;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        public static bool ShootFunc(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (++index > 3) {
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

    internal class RAstralPike : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AstralPike>();
        public override int ProtogenesisID => ModContent.ItemType<AstralPikeEcType>();
        public override string TargetToolTipItemName => "AstralPikeEcType";
        public override void SetDefaults(Item item) {
            item.width = 44;
            item.damage = 90;
            item.DamageType = DamageClass.Melee;
            item.noMelee = true;
            item.useTurn = true;
            item.noUseGraphic = true;
            item.useAnimation = 13;
            item.useStyle = ItemUseStyleID.Shoot;
            item.useTime = 13;
            item.knockBack = 8.5f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 50;
            item.value = CalamityGlobalItem.RarityCyanBuyPrice;
            item.rare = ItemRarityID.Cyan;
            item.shoot = ModContent.ProjectileType<AstralPikeHeld>();
            item.shootSpeed = 13f;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return BansheeHookEcType.ShootFunc(item, player, source, position, velocity, type, damage, knockback);
        }
    }

    internal class AstralPikeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<AstralPike>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Astral_Bar";
        public override Texture2D TextureValue => CWRUtils.GetT2DValue(CWRConstant.Cay_Proj_Melee + "Spears/AstralPikeProj");
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 64;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 52;
            SwingData.baseSwingSpeed = 6;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            SwingDrawRotingOffset = MathHelper.PiOver2;
            ShootSpeed = 13;
        }

        public override bool PreSwingAI() {
            if (Projectile.ai[0] == 3) {
                if (Time == 0) {
                    OtherMeleeSize = 1.4f;
                }

                SwingData.baseSwingSpeed = 10;
                SwingAIType = SwingAITypeEnum.Down;

                if (Time < maxSwingTime / 3) {
                    OtherMeleeSize += 0.025f / SwingMultiplication;
                }
                else {
                    OtherMeleeSize -= 0.005f / SwingMultiplication;
                }
                return true;
            }

            if (Projectile.ai[0] == 0) {
                StabBehavior(scaleFactorDenominator: 520f, minLength: 20, maxLength: 120);
                return false;
            }

            return true;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 3) {
                return;
            }
            if (Projectile.ai[0] == 1 || Projectile.ai[0] == 2) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.2f }, Projectile.Center);
            const float sengs = 0.25f;
            Vector2 spanPos = Projectile.Center + AbsolutelyShootVelocity * 0.5f;
            Vector2 targetPos = AbsolutelyShootVelocity.UnitVector() * AstralPikeEcType.InTargetProjToLang + Projectile.Center;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, spanPos, AbsolutelyShootVelocity.RotatedBy(sengs - sengs * i)
                    , ModContent.ProjectileType<AstralPikeBeam>(), Projectile.damage, Projectile.knockBack * 0.25f, Projectile.owner, targetPos.X, targetPos.Y);
            }   
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(5)) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                    , ModContent.DustType<AstralOrange>(), Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 300);
            if (hit.Crit) {
                for (int i = 0; i < 3; i++) {
                    if (Projectile.owner == Main.myPlayer) {
                        Projectile star = CalamityUtils.ProjectileBarrage(Source, Projectile.Center, target.Center, Main.rand.NextBool()
                            , 800f, 800f, 800f, 800f, 10f, ModContent.ProjectileType<AstralStar>(), (int)(Projectile.damage * 0.4), 1f, Projectile.owner, true);
                        if (star.whoAmI.WithinBounds(Main.maxProjectiles)) {
                            star.DamageType = DamageClass.Melee;
                            star.ai[0] = 3f;
                        }
                    }
                }
            }
        }
    }
}
