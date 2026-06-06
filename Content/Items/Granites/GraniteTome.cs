using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Granites
{
    /// <summary>
    /// 花岗岩魔典：召出一本浮空法书，吟咏后吐出缓慢追踪的花岗能量球，命中即碎裂为四散水晶
    /// </summary>
    internal class GraniteTome : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.damage = 17;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 7;
            Item.useTime = Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 2.5f;
            Item.UseSound = SoundID.Item43;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<GraniteTomeHeld>();
            Item.shootSpeed = 10f;
            Item.value = Item.sellPrice(0, 0, 60, 0);
            Item.rare = ItemRarityID.Orange;
        }

        //同一时刻只允许一本法书存在，配合 autoReuse 形成稳定的吟咏-发射节奏
        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<GraniteTomeHeld>()] <= 0;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.GraniteBlock, 18)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 8)
                .AddIngredient(ItemID.FallenStar, 3)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>
    /// 浮空法书持握体：只做脚手架（锚定 Owner + 吟咏节拍），逻辑与渲染全自写
    /// </summary>
    internal class GraniteTomeHeld : BaseHeldProj
    {
        public override string Texture => GraniteMarbleVFX.GraniteTex + "GraniteTome";
        private Vector2 aim = Vector2.UnitX;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 40;
            Projectile.friendly = false;
        }

        public override void OnSpawn(IEntitySource source) {
            aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.velocity = Vector2.Zero;
        }

        public override void AI() {
            SetHeld();
            int duration = Owner.itemAnimationMax;
            if (duration < 1) {
                duration = 26;
            }
            if (Projectile.timeLeft > duration) {
                Projectile.timeLeft = duration;
            }

            float life = 1f - Projectile.timeLeft / (float)duration;
            float bob = MathF.Sin(life * MathHelper.Pi) * 7f;

            Projectile.velocity = aim;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + aim * 26f
                + aim.RotatedBy(MathHelper.PiOver2) * bob + new Vector2(0f, -4f);
            Projectile.rotation = aim.ToRotation();
            SetDirection();

            //轻微吟咏脉冲粒子
            if (Projectile.ai[0] == 0f && Main.rand.NextBool(4) && !VaultUtils.isServer) {
                Vector2 ringPos = Projectile.Center + Main.rand.NextVector2CircularEdge(20f, 20f);
                PRTLoader.NewParticle<PRT_Light>(ringPos, aim.RotatedByRandom(0.6f) * 0.6f
                    , GraniteMarbleVFX.GraniteCore, 0.3f).Configure(16, 1f, 1.1f, hueShift: 0f);
            }

            //吟咏到位后吐出一枚能量球
            if (Projectile.ai[0] == 0f && life >= 0.4f) {
                Projectile.ai[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item43 with { Pitch = 0.25f }, Projectile.Center);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + aim * 22f
                        , aim * 8f, ModContent.ProjectileType<GraniteEnergyOrb>()
                        , Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 10; i++) {
                        PRTLoader.NewParticle<PRT_Light>(Projectile.Center + aim * 18f
                            , aim.RotatedByRandom(0.5f) * Main.rand.NextFloat(1f, 4f)
                            , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.3f, 0.6f)).Configure(18, 1f, 1.3f);
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.55f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            SpriteEffects fx = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Main.EntitySpriteDraw(tex, pos, null, Projectile.GetAlpha(lightColor), 0f
                , tex.Size() / 2f, Projectile.scale, fx, 0);
            return false;
        }
    }

    /// <summary>
    /// 花岗能量球：缓慢追踪，命中或撞地碎裂为水晶碎片
    /// </summary>
    internal class GraniteEnergyOrb : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private Trail Trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 260;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.ai[0]++;
            const float maxSpeed = 8.5f;
            NPC target = Projectile.Center.FindClosestNPC(820f);
            if (target != null) {
                Vector2 desired = Projectile.Center.To(target.Center).UnitVector() * maxSpeed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.045f);
            }
            if (Projectile.velocity.Length() > maxSpeed) {
                Projectile.velocity = Projectile.velocity.UnitVector() * maxSpeed;
            }

            Projectile.rotation += 0.2f;
            Projectile.scale = 1f + MathF.Sin(Projectile.ai[0] * 0.2f) * 0.08f;
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.95f);

            if (Main.rand.NextBool(3) && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.08f
                    , GraniteMarbleVFX.GraniteCore, 0.32f).Configure(16, 1f, 1.2f, hueShift: 0f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f, Volume = 0.7f }, Projectile.Center);
                for (int i = 0; i < 12; i++) {
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Main.rand.NextVector2Circular(5f, 5f)
                        , GraniteMarbleVFX.GraniteCore, Main.rand.NextFloat(0.3f, 0.6f)).Configure(20, 1f, 1.4f);
                }
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Main.rand.NextVector2Circular(4f, 4f)
                        , GraniteMarbleVFX.GraniteSpark, 0).Configure(GraniteMarbleVFX.GraniteSpark, 22, 0.2f, Main.rand.NextFloat(0.5f, 0.9f));
                }
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero
                    , GraniteMarbleVFX.GraniteDeep, 0).Configure(0.05f, 0.5f, 18);
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                int shards = 4;
                float baseRot = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < shards; i++) {
                    Vector2 v = (baseRot + MathHelper.TwoPi / shards * i).ToRotationVector2()
                        * Main.rand.NextFloat(6f, 9f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, v
                        , ModContent.ProjectileType<GraniteCrystalShard>()
                        , (int)(Projectile.damage * 0.5f), Projectile.knockBack * 0.4f, Projectile.owner);
                }
            }
        }

        public float GetWidthFunc(float completionRatio) {
            float progress = completionRatio > 0.5f ? 1f - completionRatio : completionRatio;
            return progress * 2f * Projectile.scale * Projectile.width * 1.3f;
        }

        public Color GetColorFunc(Vector2 completionRatio) {
            float t = (float)Math.Sin(completionRatio.X * 15f + Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f;
            return Color.Lerp(GraniteMarbleVFX.GraniteDeep, GraniteMarbleVFX.GraniteSpark, t) * Projectile.Opacity;
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
            GraniteMarbleVFX.ApplyGradientTrail(effect, GraniteMarbleVFX.GraniteBar, CWRConstant.Masking + "StarTexture");
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            float pulse = 1f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f);

            Color deep = GraniteMarbleVFX.GraniteDeep; deep.A = 0;
            Color core = GraniteMarbleVFX.GraniteCore; core.A = 0;

            spriteBatch.Draw(glow, pos, null, deep * 0.9f, 0f, glow.Size() / 2f, Projectile.scale * 1.5f * pulse, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, pos, null, core * 0.95f, 0f, glow.Size() / 2f, Projectile.scale * 0.85f * pulse, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, core * 0.8f, Projectile.rotation, star.Size() / 2f, Projectile.scale * 0.14f * pulse, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 碎裂水晶碎片：轻微追踪的高速能量碎片
    /// </summary>
    internal class GraniteCrystalShard : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private Trail Trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 55;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.ai[0]++;
            NPC target = Projectile.Center.FindClosestNPC(380f);
            if (target != null) {
                Vector2 desired = Projectile.Center.To(target.Center).UnitVector() * 11f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.06f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.timeLeft < 18) {
                Projectile.scale = Projectile.timeLeft / 18f;
            }
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.5f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Main.rand.NextVector2Circular(3f, 3f)
                    , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.2f, 0.4f)).Configure(14, 1f, 1.3f);
            }
        }

        public float GetWidthFunc(float completionRatio) {
            float progress = completionRatio > 0.5f ? 1f - completionRatio : completionRatio;
            return progress * 2f * Projectile.scale * Projectile.width * 1.1f;
        }

        public Color GetColorFunc(Vector2 completionRatio) => GraniteMarbleVFX.GraniteSpark * Projectile.Opacity;

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
            GraniteMarbleVFX.ApplyGradientTrail(effect, GraniteMarbleVFX.GraniteBar, CWRConstant.Masking + "ThunderTrail");
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color core = GraniteMarbleVFX.GraniteSpark; core.A = 0;
            spriteBatch.Draw(glow, pos, null, core * 0.85f * Projectile.scale, 0f, glow.Size() / 2f, Projectile.scale * 0.5f, SpriteEffects.None, 0f);
        }
    }
}
