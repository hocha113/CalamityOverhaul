using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>
    /// 大理石卵石：无限使用的极快投石，对生命上限越高的敌人造成越多额外伤害，撞地弹射一次
    /// </summary>
    internal class MarblePebble : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 22;
            Item.damage = 11;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 2f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MarblePebbleProj>();
            Item.shootSpeed = 14f;
            Item.value = Item.sellPrice(0, 0, 30, 0);
            Item.rare = ItemRarityID.Green;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 10f;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 8)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 3)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarblePebbleProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarblePebble";
        private Trail Trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 220;
            Projectile.tileCollide = true;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.ai[0]++;
            if (Projectile.ai[0] > 26) {
                Projectile.velocity.Y += 0.18f;
            }
            if (Projectile.velocity.Y > 17f) {
                Projectile.velocity.Y = 17f;
            }
            Projectile.rotation += 0.3f * Math.Sign(Projectile.velocity.X == 0 ? 1 : Projectile.velocity.X);
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleCore.ToVector3() * 0.3f);
        }

        //大卫投石：生命上限越高，额外伤害越多
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            float bonus = MathHelper.Clamp(target.lifeMax / 4000f, 0f, 1f) * 0.85f;
            modifiers.FinalDamage *= 1f + bonus;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.5f) {
                    Projectile.velocity.X = -oldVelocity.X * 0.7f;
                }
                if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.5f) {
                    Projectile.velocity.Y = -oldVelocity.Y * 0.55f;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.4f, Volume = 0.6f }, Projectile.Center);
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Main.rand.NextVector2Circular(2f, 2f)
                            , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.25f, 0.4f)).Configure(18, 0.6f, 0.05f);
                    }
                }
                return false;
            }
            Projectile.Kill();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Main.rand.NextVector2Circular(2f, 2f)
                    , GraniteMarbleVFX.MarbleGold, 0.4f).Configure(GraniteMarbleVFX.MarbleGold, 12, 0.2f, 0.5f);
            }
        }

        public float GetWidthFunc(float c) => (1f - c) * Projectile.scale * 14f;

        public Color GetColorFunc(Vector2 _) => GraniteMarbleVFX.MarbleCore * Projectile.Opacity;

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
            GraniteMarbleVFX.ApplyGradientTrail(effect, GraniteMarbleVFX.MarbleBar, CWRConstant.Masking + "Line");
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}
