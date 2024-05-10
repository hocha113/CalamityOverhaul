using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Dusts;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Common;
using Terraria.DataStructures;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 硫火鞭
    /// </summary>
    internal class BrimlashEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Brimlash";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 72;
            Item.damage = 70;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityLimeBuyPrice;
            Item.rare = ItemRarityID.Lime;
            Item.shoot = ModContent.ProjectileType<BrimlashProj>();
            Item.shootSpeed = 10f;
            
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            Item.useTime = Item.useAnimation = 30;
            Item.noUseGraphic = Item.noMelee = false;
            if (player.altFunctionUse == 2) {
                Item.useTime = Item.useAnimation = 22;
                Item.noUseGraphic = Item.noMelee = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(SoundID.Item84, position);
                Lighting.AddLight(position, Color.Red.ToVector3());

                if (Main.rand.NextBool(16))
                    player.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 20);

                float randRot = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 5; i++) {
                    Vector2 vr = (MathHelper.TwoPi / 5f * i + randRot).ToRotationVector2() * 15;
                    Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<Brimlash2>(), damage, knockback, player.whoAmI);
                }

                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, (int)CalamityDusts.Brimstone);
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);
        }
    }
}
