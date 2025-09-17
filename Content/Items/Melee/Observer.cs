using CalamityMod;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class Observer : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "Observer";
        [VaultLoaden(CWRConstant.Item_Melee + "ObserverGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetStaticDefaults() {
            ItemID.Sets.Yoyo[Item.type] = true;
            ItemID.Sets.GamepadExtraRange[Item.type] = 16;
            ItemID.Sets.GamepadSmartQuickReach[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 34;
            Item.DamageType = DamageClass.MeleeNoSpeed;
            Item.damage = 52;
            Item.knockBack = 4f;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item1;
            Item.channel = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<ObserverYoyo>();
            Item.shootSpeed = 15f;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 60, 5);
            Item.CWR().DeathModeItem = true;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class ObserverYoyo : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "ObserverYoyo";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<Observer>()).DisplayName;
        [VaultLoaden(CWRConstant.Item_Melee + "ObserverYoyoGlow")]
        public static Asset<Texture2D> Glow = null;
        private int Time;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Type] = 40f * 2;
            ProjectileID.Sets.YoyosMaximumRange[Type] = 430f;
            ProjectileID.Sets.YoyosTopSpeed[Type] = 40f / 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
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
            NPC target = Projectile.Center.FindClosestNPC(100);
            if (target != null) {
                Projectile.SmoothHomingBehavior(target.Center, 1);
            }
            Time++;
        }

        public override bool PreDraw(ref Color lightColor) {
            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 1.75f * Main.essScale);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);
            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Masking + "DiffusionCircle3");
            float sengs = Math.Abs(MathF.Sin(Time * 0.04f));
            float slp = sengs * 0.6f + 1.6f;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.Red * (0.2f + sengs * 0.5f)
            , Projectile.rotation, value.GetOrig(), slp, SpriteEffects.FlipHorizontally, 0);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Type], lightColor, 1);
            value = Glow.Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
            , Projectile.rotation, value.GetOrig(), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
