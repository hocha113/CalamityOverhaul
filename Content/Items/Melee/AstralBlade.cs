using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Dusts;
using CalamityMod.Items;
using CalamityMod;
using Microsoft.Xna.Framework;
using System;
using Terraria.Audio;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.AstralProj;
using Terraria.DataStructures;
using Terraria.WorldBuilding;
using CalamityMod.Projectiles.Typeless;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 幻星刃
    /// </summary>
    internal class AstralBlade : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AstralBlade";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetDefaults() {
            Item.damage = 85;
            Item.DamageType = DamageClass.Melee;
            Item.width = 80;
            Item.height = 80;
            Item.scale = 1;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4f;
            Item.value = CalamityGlobalItem.Rarity9BuyPrice;
            Item.rare = ItemRarityID.Cyan;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<AstralBall>();
            Item.shootSpeed = 11;
            
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 25;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity.RotatedBy(Main.rand.NextFloat(-0.15f, 0.15f)), type, (int)(damage * 0.75f), knockback, player.whoAmI);
            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 300);

            if (hit.Crit) {
                var source = Item.GetSource_FromThis();
                for (int i = 0; i < 3; i++) {
                    Projectile star = CalamityUtils.ProjectileBarrage(source, player.Center, target.Center
                        , Main.rand.NextBool(), 800f, 800f, 800f, 800f, 10f, ModContent.ProjectileType<AstralStar>(), (int)(hit.Damage * 0.4), 1f, player.whoAmI, true);
                    if (star.whoAmI.WithinBounds(Main.maxProjectiles)) {
                        star.DamageType = DamageClass.Melee;
                        star.ai[0] = 3f;
                    }
                }
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 300);
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            for (int i = 0; i < 3; i++) {
                Dust d = CalamityUtils.MeleeDustHelper(player, Main.rand.NextBool() ? ModContent.DustType<AstralOrange>() : ModContent.DustType<AstralBlue>(), 0.7f, 55, 110, -0.07f, 0.07f);
                if (d != null) {
                    d.customData = 0.03f;
                }
            }
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            //TODO: 这里的功能未完成，总的来讲，它就应该空着，而不是调用HitOnDamageIfyFunc
        }

        public static void HitOnDamageIfyFunc(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers) {
            if (target.lifeMax <= 0)
                return;

            float lifeRatio = MathHelper.Clamp(target.life / (float)target.lifeMax, 0f, 1f);
            float multiplier = MathHelper.Lerp(1f, 2f, lifeRatio);

            modifiers.SourceDamage *= multiplier;
            modifiers.Knockback *= multiplier;

            if (Main.rand.NextBool((int)MathHelper.Clamp((item.crit + player.GetCritChance<MeleeDamageClass>()) * multiplier, 0f, 99f), 100))
                modifiers.SetCrit();

            if (multiplier > 1.5f) {
                SoundEngine.PlaySound(SoundID.Item105, player.Center);
                bool blue = Main.rand.NextBool();
                float angleStart = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                float var = 0.05f + (2f - multiplier);
                for (float angle = 0f; angle < MathHelper.TwoPi; angle += var) {
                    blue = !blue;
                    Vector2 velocity = angle.ToRotationVector2() * (2f + (float)(Math.Sin(angleStart + angle * 3f) + 1) * 2.5f) * Main.rand.NextFloat(0.95f, 1.05f);
                    Dust d = Dust.NewDustPerfect(target.Center, blue ? ModContent.DustType<AstralBlue>() : ModContent.DustType<AstralOrange>(), velocity);
                    d.customData = 0.025f;
                    d.scale = multiplier - 0.75f;
                    d.noLight = false;
                }
            }
        }
    }
}
