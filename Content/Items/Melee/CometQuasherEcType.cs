using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 彗星陨刃
    /// </summary>
    internal class CometQuasherEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "CometQuasher";

        public override void SetDefaults() {
            Item.width = 46;
            Item.height = 62;
            Item.scale = 1.5f;
            Item.damage = 80;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 15;
            Item.useStyle = 1;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.knockBack = 2.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = 8;
            Item.shoot = ModContent.ProjectileType<CometQuasherMeteor>();
            Item.shootSpeed = 9f;
            
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int proj = Projectile.NewProjectile(source, position, velocity
                    , ModContent.ProjectileType<CometQuasherMeteor>(), (int)(Item.damage * 0.5f), Item.knockBack, player.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
            return false;
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.CritDamage *= 0.5f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            Vector2 offsetVr = CWRUtils.GetRandomVevtor(-75, -105, Main.rand.Next(500, 600));
            Vector2 spanPos = target.Center + offsetVr;
            Vector2 vr = offsetVr.UnitVector() * -17;
            int proj = Projectile.NewProjectile(CWRUtils.parent(player), spanPos, vr, ModContent.ProjectileType<CometQuasherMeteor>(), Item.damage, Item.knockBack, player.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            Vector2 offsetVr = CWRUtils.GetRandomVevtor(-75, -105, Main.rand.Next(500, 600));
            Vector2 spanPos = target.Center + offsetVr;
            Vector2 vr = offsetVr.UnitVector() * -17;
            int proj = Projectile.NewProjectile(CWRUtils.parent(player), spanPos, vr, ModContent.ProjectileType<CometQuasherMeteor>(), Item.damage, Item.knockBack, player.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, 6);
            }
        }
    }
}
