using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.EyekiteStaffs
{
    /// <summary>缚瞳风筝，克眼掉落的召唤杖</summary>
    internal class EyekiteStaff : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "EyekiteStaff";

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 40;
            Item.damage = 14;
            Item.mana = 10;
            Item.knockBack = 3.2f;
            Item.useTime = Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item44;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<EyekiteMinion>();
            Item.shootSpeed = 8f;
            Item.buffType = ModContent.BuffType<EyekiteBuff>();
            Item.DamageType = DamageClass.Summon;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 1, 50);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);
            Vector2 spawn = player.Center + new Vector2(player.direction * 12f, -18f);
            Projectile.NewProjectile(source, spawn, velocity * 0.35f + new Vector2(0f, -2f)
                , type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class EyekiteBuff : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "EyekiteBuff";

        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<EyekiteMinion>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}
