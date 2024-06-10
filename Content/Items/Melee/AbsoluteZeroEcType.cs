using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Items;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 绝对零度
    /// </summary>
    internal class AbsoluteZeroEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AbsoluteZero";
        public override void SetDefaults() {
            Item.damage = 75;
            Item.DamageType = DamageClass.Melee;
            Item.scale = 1.25f;
            Item.width = 58;
            Item.height = 58;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = false;
            Item.knockBack = 2f;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DarkIceZeros>();
            Item.shootSpeed = 7f;
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.CritDamage *= 0.5f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(ModContent.BuffType<GlacialState>(), 60);
            var source = player.GetSource_ItemUse(Item);
            int p = Projectile.NewProjectile(source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<DarkIceZeros>(), (int)(Item.damage * 1.25f), 12f, player.whoAmI);
            Main.projectile[p].timeLeft = 12;
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(ModContent.BuffType<GlacialState>(), 60);
            var source = player.GetSource_ItemUse(Item);
            int p = Projectile.NewProjectile(source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<DarkIceZeros>(), (int)(Item.damage * 1.25f), 12f, player.whoAmI);
            Main.projectile[p].timeLeft = 12;
        }
    }
}
