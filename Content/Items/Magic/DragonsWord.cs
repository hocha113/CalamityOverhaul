using CalamityOverhaul.Content.Projectiles.Weapons.Magic.DragonsWordProj;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class DragonsWord : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "DragonsWord";
        [VaultLoaden(CWRConstant.Item_Magic + "DragonsWordGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.width = 70;
            Item.height = 84;
            Item.damage = 682;
            Item.mana = 80;
            Item.shootSpeed = 6;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.DamageType = DamageClass.Magic;
            Item.useTime = Item.useAnimation = 60;
            Item.rare = ItemRarityID.Red;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item92;
            Item.value = Item.buyPrice(0, 85, 5, 5);
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<DragonsWordProj>();
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_DragonsWord;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<DragonsWordMouse>(), damage, knockback, player.whoAmI, 0f, 0.03f);
                return false;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 vr = (MathHelper.TwoPi / 3f * i + Main.GameUpdateCount * 0.1f).ToRotationVector2();
                Projectile.NewProjectile(source, player.Center + vr * Main.rand.Next(22, 38), vr.RotatedByRandom(0.32f) * 3
                , type, damage, knockback, player.whoAmI, 0f, 0.03f);
            }
            return false;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<DragonsWordMouse>()] <= 0;
    }
}
