using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 暗雷回响
    /// </summary>
    internal class PerfectDark : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "PerfectDark";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetDefaults() {
            Item.width = 50;
            Item.damage = 24;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 25;
            Item.useTurn = true;
            Item.knockBack = 4.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 50;
            Item.value = CalamityGlobalItem.Rarity3BuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.shoot = ModContent.ProjectileType<DarkBall>();
            Item.shootSpeed = 10f;
            
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<BrainRot>(), 300);
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(ModContent.BuffType<BrainRot>(), 300);
        }
    }
}
