using CalamityOverhaul.Content.Items.Materials;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DestroyersBladeEXs
{
    /// <summary>
    /// 毁灭者之刃EX。左键沉重三拍挥砍(红白光束+影子弹幕),
    /// 停手片刻进入潜猎协议(移速上升),贴近猎物后右键发动毁灭者之撕咬,
    /// 咬中进入歼灭协议:弹幕全面强化获得追踪,终结斩额外吐出毁灭者头颅
    /// </summary>
    internal class DestroyersBladeEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBladeEX";
        [VaultLoaden(CWRConstant.Item_Melee + "DestroyersBladeEXGlow")]
        public static Asset<Texture2D> Glow = null;

        public override void SetDefaults() {
            Item.height = 132;
            Item.width = 134;
            Item.damage = 1090;
            Item.knockBack = 8;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = null;
            Item.useTime = Item.useAnimation = 18;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 60, 5);
            Item.shoot = ModContent.ProjectileType<DestroyersBladeEXHeld>();
            Item.shootSpeed = 15;
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override bool AltFunctionUse(Player player)
            => player.GetModPlayer<DestroyerEXPlayer>().BiteReady;

        public override bool CanUseItem(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<DestroyerBiteProj>()] > 0) {
                return false;
            }
            if (player.altFunctionUse == 2) {
                return player.GetModPlayer<DestroyerEXPlayer>().BiteReady;
            }
            return player.ownedProjectileCounts[Item.shoot] == 0;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            //歼灭协议窗口的全武器增伤
            if (player.GetModPlayer<DestroyerEXPlayer>().Empowered) {
                damage *= DestroyerEXPlayer.FrenzyDamageMul;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            DestroyerEXPlayer mp = player.GetModPlayer<DestroyerEXPlayer>();

            if (player.altFunctionUse == 2) {
                //毁灭者之撕咬:锁定潜猎目标,化身头颅扑咬
                int target = mp.FindBiteTarget();
                if (target < 0) {
                    return false;
                }
                Vector2 toTarget = (Main.npc[target].Center - player.Center).SafeNormalize(velocity);
                Projectile.NewProjectile(source, player.Center, toTarget
                    , ModContent.ProjectileType<DestroyerBiteProj>(), (int)(damage * 6f), 12f
                    , player.whoAmI, target);
                return false;
            }

            //沉重三拍:拍号与挥向挂玩家,物品实例会被重克隆
            int combo = mp.ComboStage % 3;
            float swingDir = mp.ComboStage % 2 == 0 ? 1f : -1f;
            mp.ComboStage = (mp.ComboStage + 1) % 6;
            mp.LastSwingTick = Main.GameUpdateCount;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
           , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition, null, Color.White
                , rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<DestroyersBlade>().
                AddIngredient<SoulofMightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }
}
