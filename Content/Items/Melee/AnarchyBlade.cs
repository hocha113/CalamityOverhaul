using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.CalPlayer;
using CalamityMod.Dusts;
using CalamityMod.Items;
using CalamityMod.NPCs;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.VulcaniteProj;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 混乱之刃
    /// </summary>
    internal class AnarchyBlade : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AnarchyBlade";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }
        public override bool AltFunctionUse(Player player) => true;

        private static int BaseDamage = 150;
        public const int ShootPeriod = 3;
        public int ShootCount;

        public override void SetDefaults() {
            Item.width = 114;
            Item.damage = BaseDamage;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 19;
            Item.useTime = 19;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 122;
            Item.value = CalamityGlobalItem.Rarity8BuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.shoot = ModContent.ProjectileType<AnarchyBeam>();
            Item.shootSpeed = 15;
            
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            Item.scale = 1;
            Item.useTime = Item.useAnimation = 17;
            if (player.altFunctionUse == 2) {
                Item.scale = 1.2f;
                Item.useTime = Item.useAnimation = 19;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                return false;
            }
            ShootCount++;
            if (ShootCount < ShootPeriod) {//这里使用简单的流程控制语句进行周期发射
                return false;
            }
            ShootCount = 0;
            SoundEngine.PlaySound(SoundID.Item20, position);
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3))
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, (int)CalamityDusts.Brimstone);
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            int lifeAmount = player.statLifeMax2 - player.statLife;
            damage.Base += lifeAmount * 0.1f;
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.CritDamage *= 0.5f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.altFunctionUse == 2) {
                var source = player.GetSource_ItemUse(Item);
                Projectile.NewProjectile(source, target.Center, Vector2.Zero, ModContent.ProjectileType<BrimstoneBoom>(), Item.damage, Item.knockBack, Main.myPlayer);
                target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);

                if (player.statLife <= (player.statLifeMax2 * 0.5f) && Main.rand.NextBool(5)) {
                    if (!CalamityPlayer.areThereAnyDamnBosses && CalamityGlobalNPC.ShouldAffectNPC(target)) {
                        target.life = 0;
                        target.HitEffect(0, 10.0);
                        target.active = false;
                        target.NPCLoot();
                    }
                }
            }
            
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            if (player.altFunctionUse == 2) {
                var source = player.GetSource_ItemUse(Item);
                Projectile.NewProjectile(source, target.Center, Vector2.Zero, ModContent.ProjectileType<BrimstoneBoom>(), Item.damage, Item.knockBack, Main.myPlayer);
                target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);
            }
        }
    }
}
