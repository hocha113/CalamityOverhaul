using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    internal class AriaofTheCosmos : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "AriaofTheCosmos";

        public override void SetDefaults() {
            Item.damage = 120;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 15;
            Item.width = 52;
            Item.height = 52;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 6f;
            Item.value = Item.buyPrice(gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item109;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<AccretionDiskEffect>();
            Item.shootSpeed = 0f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, 
            Vector2 velocity, int type, int damage, float knockback) {
            //在玩家周围生成吸积盘特效
            Vector2 spawnPos = player.Center;
            
            //创建吸积盘投射物
            int proj = Projectile.NewProjectile(source, spawnPos, Vector2.Zero, type, damage, knockback, player.whoAmI);
            
            if (Main.projectile[proj].ModProjectile is AccretionDiskEffect disk) {
                //配置吸积盘参数
                disk.RotationSpeed = 1.5f;      //旋转速度
                disk.InnerRadius = 0.2f;        //内半径
                disk.OuterRadius = 0.8f;        //外半径
                
                //可以根据需要调整大小
                Main.projectile[proj].scale = 1.5f;
            }
            
            return false;
        }
    }
}
