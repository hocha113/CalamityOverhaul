using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityMod.Particles;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 绝对零度
    /// </summary>
    internal class AbsoluteZero : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AbsoluteZero";
        public override void SetDefaults() {
            Item.damage = 75;
            Item.DamageType = DamageClass.Melee;
            Item.scale = 1.25f;
            Item.width = 58;
            Item.height = 58;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = false;
            Item.knockBack = 2f;
            Item.value = CalamityGlobalItem.Rarity8BuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DarkIceZeros>();
            Item.shootSpeed = 5f;
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.CritDamage *= 0.5f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(ModContent.BuffType<GlacialState>(), 60);
            var source = player.GetSource_ItemUse(Item);
            int p = Projectile.NewProjectile(source, target.Center, Vector2.Zero, ModContent.ProjectileType<DarkIceZeros>(), Item.damage, 12f, player.whoAmI);
            Main.projectile[p].timeLeft = 12;
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(ModContent.BuffType<GlacialState>(), 60);
            var source = player.GetSource_ItemUse(Item);
            int p = Projectile.NewProjectile(source, target.Center, Vector2.Zero, ModContent.ProjectileType<DarkIceZeros>(), Item.damage, 12f, player.whoAmI);
            Main.projectile[p].timeLeft = 12;
        }
    }
}
