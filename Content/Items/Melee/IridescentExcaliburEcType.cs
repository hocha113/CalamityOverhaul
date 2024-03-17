using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.IridescentExcaliburpProj;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 光辉圣剑
    /// </summary>
    internal class IridescentExcaliburEcType : EctypeItem
    {
        public static Color[] richColors = new Color[]{
            new Color(255, 0, 0),
            new Color(0, 255, 0),
            new Color(0, 0, 255),
            new Color(255, 255, 0),
            new Color(255, 0, 255),
            new Color(0, 255, 255),
            new Color(255, 165, 0),
            new Color(128, 0, 128),
            new Color(255, 192, 203),
            new Color(0, 128, 0),
            new Color(128, 128, 0),
            new Color(0, 128, 128),
            new Color(128, 0, 0),
            new Color(139, 69, 19),
            new Color(0, 255, 127),
            new Color(255, 215, 0)
        };
        public override string Texture => CWRConstant.Cay_Wap_Melee + "IridescentExcalibur";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        private static readonly int BaseDamage = 2000;
        private int BeamType = 0;
        private const int alpha = 50;

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.damage = BaseDamage;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 14;
            Item.useTurn = true;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 8f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.width = 112;
            Item.height = 112;
            Item.value = CalamityGlobalItem.Rarity16BuyPrice;
            Item.shoot = ModContent.ProjectileType<IridescentExcaliburBeam>();
            Item.shootSpeed = 6f;
            Item.rare = ModContent.RarityType<Rainbow>();
            
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit += 10;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            _ = Projectile.NewProjectile(source, position.X, position.Y, velocity.X, velocity.Y, type, damage, knockback, player.whoAmI, BeamType, 0f);

            BeamType++;
            if (BeamType > 11) {
                BeamType = 0;
            }

            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                Item.shoot = ProjectileID.None;
                Item.shootSpeed = 0f;
            }
            else {
                Item.shoot = ModContent.ProjectileType<IridescentExcaliburBeam>();
                Item.shootSpeed = 12f;
            }

            return base.CanUseItem(player);
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            if (player.altFunctionUse == 2) {
                modifiers.SourceDamage *= 2;
            }
        }

        public override void ModifyHitPvp(Player player, Player target, ref Player.HurtModifiers modifiers) {
            if (player.altFunctionUse == 2) {
                modifiers.SourceDamage *= 2;
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(4)) {
                Color color = CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), richColors);
                Dust swingDust = Main.dust[Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.RainbowMk2, 0f, 0f, alpha, color, 1.2f)];
                swingDust.noGravity = true;
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<MiracleBlight>(), 600);

            if (!target.canGhostHeal || player.moonLeech) {
                return;
            }

            int healAmount = Main.rand.Next(3) + 10;
            player.statLife += healAmount;
            player.HealEffect(healAmount);
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(ModContent.BuffType<MiracleBlight>(), 600);

            if (player.moonLeech) {
                return;
            }

            int healAmount = Main.rand.Next(3) + 10;
            player.statLife += healAmount;
            player.HealEffect(healAmount);
        }
    }
}
