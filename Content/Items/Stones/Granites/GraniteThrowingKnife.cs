using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Granites
{
    /// <summary>
    /// 花岗飞刀：穿透并拖出蓝色能量缎带，末段 / 撞地碎裂为水晶碎片
    /// </summary>
    internal class GraniteThrowingKnife : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 30;
            Item.damage = 15;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 17;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 3f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<GraniteThrowingKnifeProj>();
            Item.shootSpeed = 12f;
            Item.value = Item.sellPrice(0, 0, 45, 0);
            Item.rare = ItemRarityID.Green;
        }

        public override void AddRecipes() {
            CreateRecipe(50)
                .AddIngredient(ItemID.Granite, 10)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 4)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class GraniteThrowingKnifeProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => GraniteMarbleVFX.GraniteTex + "GraniteThrowingKnife";
        private const float SpriteRot = MathHelper.PiOver4;
        private Trail Trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.ai[0]++;
            if (Projectile.ai[0] > 22) {
                Projectile.velocity.Y += 0.16f;
            }
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + SpriteRot;
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.55f);

            if (Main.rand.NextBool(3) && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.06f
                    , GraniteMarbleVFX.GraniteCore, 0.28f).Configure(12, 1f, 1.2f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.5f, Volume = 0.6f }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Main.rand.NextVector2Circular(4f, 4f)
                        , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.3f, 0.5f)).Configure(16, 1f, 1.3f);
                }
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                int shards = 3;
                Vector2 baseDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < shards; i++) {
                    Vector2 v = baseDir.RotatedBy(MathHelper.Lerp(-0.7f, 0.7f, i / (float)(shards - 1))) * Main.rand.NextFloat(6f, 9f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, v
                        , ModContent.ProjectileType<GraniteCrystalShard>()
                        , (int)(Projectile.damage * 0.5f), Projectile.knockBack * 0.4f, Projectile.owner);
                }
            }
        }

        public float GetWidthFunc(float c) {
            float p = c > 0.5f ? 1f - c : c;
            return p * 2f * Projectile.scale * Projectile.width * 1.2f;
        }

        public Color GetColorFunc(Vector2 c) {
            float t = (float)Math.Sin(c.X * 12f + Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f;
            return Color.Lerp(GraniteMarbleVFX.GraniteDeep, GraniteMarbleVFX.GraniteCore, t) * Projectile.Opacity;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 dpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Color c = GraniteMarbleVFX.GraniteCore * fade * 0.35f; c.A = 0;
                Main.EntitySpriteDraw(tex, dpos, null, c, Projectile.oldRot[i], origin, Projectile.scale * fade, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor)
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos == null || Projectile.oldPos.Length == 0) {
                return;
            }
            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    Projectile.oldPos[i] = Projectile.Center;
                }
                positions[i] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            Trail ??= new Trail(positions, GetWidthFunc, GetColorFunc);
            Trail.TrailPositions = positions;

            Effect effect = EffectLoader.GradientTrail.Value;
            GraniteMarbleVFX.ApplyGradientTrail(effect, GraniteMarbleVFX.GraniteBar, CWRAsset.StarTexture.Value);
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}
