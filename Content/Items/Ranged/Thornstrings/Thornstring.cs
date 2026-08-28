using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Thornstrings
{
    /// <summary>
    /// 棘弦，荒花长弓。左键把任意箭矢化为棘箭，命中向两侧崩出短针；
    /// 右键蓄力，拉满松手射出贯穿重棘箭，落点绽放花瓣圈。
    /// 拉弦与发射在 <see cref="ThornstringHeld"/>
    /// </summary>
    internal class Thornstring : BssModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Thornstring";

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 76;
            Item.damage = 15;
            Item.useAnimation = Item.useTime = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.autoReuse = true;
            Item.knockBack = 2.5f;
            Item.shootSpeed = 13.5f;
            Item.useAmmo = AmmoID.Arrow;
            Item.rare = ItemRarityID.Green;
            Item.DamageType = DamageClass.Ranged;
            Item.value = Item.sellPrice(0, 1, 20);
            Item.crit = 6;
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<ThornstringHeld>();
        }

        //右键蓄力
        public override bool AltFunctionUse(Player player) => true;

        //放箭时由手持弹幕拾取弹药
        public override bool CanConsumeAmmo(Item ammo, Player player) => ThornstringHeld.AmmoConsumeContext;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //生成手持弹幕接管左右键，全松键后自毁
            int heldType = ModContent.ProjectileType<ThornstringHeld>();
            if (player.CountProjectilesOfID(heldType) <= 0) {
                Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, heldType, 0, 0, player.whoAmI);
            }
            return false;
        }
    }
}
