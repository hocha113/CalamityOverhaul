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
    /// <summary>花岗飞刀，电弧拖尾，命中链电跳邻近，末段/撞地碎晶</summary>
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
        //链电已触发，owner 侧本地
        private ref float ChainUsed => ref Projectile.localAI[0];
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
            //直线段后延迟重力
            if (Projectile.ai[0] > 22) {
                Projectile.velocity.Y += 0.16f;
            }
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + SpriteRot;
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.55f);

            if (!VaultUtils.isServer) {
                if (Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.06f
                        , GraniteMarbleVFX.GraniteCore, 0.22f).Configure(10, 1f, 1.2f);
                }
                if (Main.rand.NextBool(14)) {
                    PRTLoader.NewParticle<PRT_GraniteVolt>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                        , Projectile.velocity * 0.1f, GraniteMarbleVFX.GraniteSpark
                        , Main.rand.NextFloat(0.16f, 0.26f)).Configure(Main.rand.Next(2, 5));
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity; //供 OnKill 反推碎裂点
            Projectile.Kill();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Pitch = 0.4f, Volume = 0.35f }, target.Center);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_GraniteVolt>(target.Center + Main.rand.NextVector2Circular(8f, 8f)
                        , Main.rand.NextVector2Unit() * 2f, GraniteMarbleVFX.GraniteCore
                        , Main.rand.NextFloat(0.22f, 0.34f)).Configure(Main.rand.Next(3, 6));
                }
            }
            //每刀至多一次链电，owner 侧
            if (ChainUsed != 0f || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            NPC jump = target.Center.FindClosestNPC(GraniteKnifeVoltArc.JumpRange, onHitNPCs: new[] { target });
            if (jump == null) {
                return;
            }
            ChainUsed = 1f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero
                , ModContent.ProjectileType<GraniteKnifeVoltArc>()
                , Math.Max((int)(Projectile.damage * 0.3f), 1), 0f, Projectile.owner, jump.whoAmI, target.whoAmI);
        }

        public override void OnKill(int timeLeft) {
            Vector2 baseDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            //碎裂点回退半刀身，防嵌墙
            Vector2 burstPos = Projectile.Center - baseDir * Projectile.width * 0.5f;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.5f, Volume = 0.6f }, burstPos);
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.05f, Volume = 0.35f }, burstPos);
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Pitch = 0.15f, Volume = 0.3f }, burstPos);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_GraniteShard>(burstPos
                        , Main.rand.NextVector2Circular(3f, 2.4f) - baseDir * Main.rand.NextFloat(0.5f, 1.5f)
                            - Vector2.UnitY * Main.rand.NextFloat(1f, 2.6f)
                        , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.5f, 0.85f))
                        .Configure(Main.rand.Next(26, 40));
                }
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_GraniteVolt>(burstPos + Main.rand.NextVector2Circular(7f, 7f)
                        , Main.rand.NextVector2Unit() * 2f, GraniteMarbleVFX.GraniteCore
                        , Main.rand.NextFloat(0.24f, 0.4f)).Configure(Main.rand.Next(3, 6));
                }
                PRTLoader.NewParticle<PRT_Light>(burstPos, Vector2.Zero
                    , GraniteMarbleVFX.GraniteSpark, 0.4f).Configure(12, 1f, 1.25f);
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                int shards = 3;
                for (int i = 0; i < shards; i++) {
                    Vector2 v = baseDir.RotatedBy(MathHelper.Lerp(-0.7f, 0.7f, i / (float)(shards - 1))) * Main.rand.NextFloat(6f, 9f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), burstPos, v
                        , ModContent.ProjectileType<GraniteCrystalShard>()
                        , (int)(Projectile.damage * 0.5f), Projectile.knockBack * 0.4f, Projectile.owner);
                }
            }
        }

        public float GetWidthFunc(float c) {
            //半宽上限8px
            float head = MathHelper.Clamp(c * 10f, 0f, 1f);
            float tail = 1f - c;
            return head * tail * tail * 8f * Projectile.scale;
        }

        public Color GetColorFunc(Vector2 uv) => Color.White * Projectile.Opacity;

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

            Texture2D line = CWRAsset.Line.Value;
            float flicker = 0.68f + 0.32f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.whoAmI * 1.7f);
            float bladeRot = Projectile.rotation - SpriteRot + MathHelper.PiOver2; //Line 竖向
            Vector2 bladePos = Projectile.Center - Main.screenPosition;
            Vector2 lineOrigin = line.Size() / 2f;
            Color edge = GraniteMarbleVFX.GraniteSpark * 0.85f * flicker; edge.A = 0;
            Color core = Color.White * 0.5f * flicker; core.A = 0;
            Main.EntitySpriteDraw(line, bladePos, null, edge, bladeRot, lineOrigin
                , new Vector2(5f / line.Width, 36f * Projectile.scale / line.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, bladePos, null, core, bladeRot, lineOrigin
                , new Vector2(2.4f / line.Width, 30f * Projectile.scale / line.Height), SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() =>
            GraniteMarbleVFX.DrawGraniteArcTrailFromOldPos(Projectile, ref Trail, GetWidthFunc, GetColorFunc);
    }

    /// <summary>
    /// 链电判定弹，ai[0]=跳跃目标 whoAmI，ai[1]=起点 whoAmI；只伤跳跃目标一次
    /// </summary>
    internal class GraniteKnifeVoltArc : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        /// <summary>链电搜索半径 px</summary>
        internal const float JumpRange = 240f;
        private const int LifeTime = 14;
        private const int BrightTime = 6; //满亮帧，其后淡出
        private const int ArcPointCount = 10;

        private ThunderTrail mainTrail;
        private ThunderTrail coreTrail;
        private Vector2 startPos;
        private Vector2 endPos;
        private ref float Timer => ref Projectile.localAI[0];
        private float Fade => MathHelper.Clamp(Projectile.timeLeft / (float)(LifeTime - BrightTime), 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //每目标一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //端点跟 NPC，失效停末位
            if (((int)Projectile.ai[1]).TryGetNPC(out NPC source) && source.Alives()) {
                startPos = source.Center;
            }
            else if (startPos == Vector2.Zero) {
                startPos = Projectile.Center;
            }
            if (((int)Projectile.ai[0]).TryGetNPC(out NPC jump) && jump.Alives()) {
                endPos = jump.Center;
            }
            else if (endPos == Vector2.Zero) {
                endPos = startPos;
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Pitch = 0.6f, Volume = 0.55f }, endPos);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_GraniteVolt>(endPos + Main.rand.NextVector2Circular(8f, 8f)
                        , Main.rand.NextVector2Unit() * 2.5f, GraniteMarbleVFX.GraniteSpark
                        , Main.rand.NextFloat(0.26f, 0.4f)).Configure(Main.rand.Next(3, 6));
                }
            }
            //亮相每2帧重掷路径，其后冻结淡出
            if (!VaultUtils.isServer && Projectile.timeLeft > LifeTime - BrightTime && (int)Timer % 2 == 0) {
                BuildArcPath();
            }
            Timer++;

            for (int i = 0; i < 3; i++) {
                Lighting.AddLight(Vector2.Lerp(startPos, endPos, i / 2f)
                    , GraniteMarbleVFX.GraniteCore.ToVector3() * 0.4f * Fade);
            }
        }

        private void BuildArcPath() {
            Vector2 dir = endPos - startPos;
            if (dir.Length() < 8f) {
                return;
            }
            Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            Vector2[] points = new Vector2[ArcPointCount];
            for (int i = 0; i < ArcPointCount; i++) {
                float t = i / (float)(ArcPointCount - 1);
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                points[i] = startPos + dir * t + perp * Main.rand.NextFloat(-9f, 9f) * envelope;
            }
            if (mainTrail == null) {
                mainTrail = new ThunderTrail(CWRAsset.ThunderTrail, GetMainWidth
                    , _ => GraniteMarbleVFX.GraniteSpark, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                mainTrail.SetRange((0f, 7f));
                mainTrail.SetExpandWidth(4f);
                coreTrail = new ThunderTrail(CWRAsset.ThunderTrail, GetCoreWidth
                    , _ => Color.White, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                coreTrail.SetRange((0f, 3f));
                coreTrail.SetExpandWidth(2f);
            }
            mainTrail.BasePositions = points;
            coreTrail.BasePositions = points;
            mainTrail.RandomThunder();
            coreTrail.RandomThunder();
        }

        private float GetMainWidth(float f) => (9f + 5f * (float)Math.Sin(f * MathHelper.Pi)) * Fade;
        private float GetCoreWidth(float f) => (3.5f + 2f * (float)Math.Sin(f * MathHelper.Pi)) * Fade;
        private float GetArcAlpha(float f) => Fade;

        public override bool? CanHitNPC(NPC target) {
            if (target.whoAmI == (int)Projectile.ai[0]) {
                return null;
            }
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (startPos == Vector2.Zero || endPos == Vector2.Zero) {
                return false;
            }
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , startPos, endPos, 24f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Fade <= 0.05f) {
                return false;
            }
            mainTrail?.DrawThunder(Main.instance.GraphicsDevice);
            coreTrail?.DrawThunder(Main.instance.GraphicsDevice);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color glowColor = GraniteMarbleVFX.GraniteSpark; glowColor.A = 0;
            Main.EntitySpriteDraw(glow, startPos - Main.screenPosition, null, glowColor * 0.7f * Fade
                , 0f, glow.Size() / 2f, 0.1f + 0.25f * Fade, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, endPos - Main.screenPosition, null, glowColor * 0.9f * Fade
                , 0f, glow.Size() / 2f, 0.12f + 0.35f * Fade, SpriteEffects.None, 0);
            return false;
        }
    }
}
