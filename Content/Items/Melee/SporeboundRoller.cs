using CalamityMod;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 牵滚菌
    /// </summary>
    internal class SporeboundRoller : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "SporeboundRoller";
        [VaultLoaden(CWRConstant.Item_Melee + "SporeboundRollerGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetStaticDefaults() {
            ItemID.Sets.Yoyo[Item.type] = true;
            ItemID.Sets.GamepadExtraRange[Item.type] = 8;
        }

        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 40;
            Item.DamageType = DamageClass.MeleeNoSpeed;
            Item.damage = 12;
            Item.knockBack = 2f;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item1;
            Item.channel = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<SporeboundRollerYoyo>();
            Item.shootSpeed = 15f;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 0, 50, 5);
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class SporeboundRollerYoyo : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "SporeboundRollerYoyo";
        [VaultLoaden(CWRConstant.Item_Melee + "SporeboundRollerYoyoGlow")]
        public static Asset<Texture2D> Glow = null;
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<SporeboundRoller>()).DisplayName;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Type] = 20f * 2;
            ProjectileID.Sets.YoyosMaximumRange[Type] = 230f;
            ProjectileID.Sets.YoyosTopSpeed[Type] = 12f;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
        }

        public override void AI() {
            if (Projectile.Distance(Owner.Center) > 3200f) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, lightColor
            , Projectile.rotation, value.GetOrig(), Projectile.scale, SpriteEffects.None, 0);
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Type], lightColor, 1);
            value = Glow.Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
            , Projectile.rotation, value.GetOrig(), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
