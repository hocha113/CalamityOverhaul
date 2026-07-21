using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>大理石飞盘，链击回旋，弹射尽后归手，可并存两枚</summary>
    internal class MarbleDisc : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.damage = 16;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 4f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MarbleDiscProj>();
            Item.shootSpeed = 14f;
            Item.value = Item.sellPrice(0, 0, 65, 0);
            Item.rare = ItemRarityID.Orange;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<MarbleDiscProj>()] < 2;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 18)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 8)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>自由飞回旋镖（非 BaseHeldProj），链击层驱动伤害/金光</summary>
    internal class MarbleDiscProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleDisc";

        //弹射预算(墙+敌)，尽则回手
        private const int MaxBounce = 5;
        //链击层上限，每层+10%伤
        private const int MaxChainLevel = 3;
        //链击搜索半径
        private const float ChainRange = 400f;
        //回手吸附半径
        private const float CatchRange = 34f;

        private Player Owner => Main.player[Projectile.owner];

        //owner 端已命中表，换向排除防回锁
        private readonly List<NPC> hitNPCs = new();

        private int ChainLevel => (int)Projectile.ai[2];

        //ai[0] 0掷出/1回手；ai[1]飞行计时(链击清零)；ai[2]链击层0~3；localAI[0]弹射计数
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            if (!Owner.Alives()) {
                Projectile.Kill();
                return;
            }

            //转速跟飞行速度
            Projectile.rotation += 0.28f + Projectile.velocity.Length() * 0.015f;
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleGold.ToVector3() * (0.4f + 0.14f * ChainLevel));

            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(9f, 9f)
                    , -Projectile.velocity * 0.12f
                    , Main.rand.NextBool() ? GraniteMarbleVFX.MarbleCore : GraniteMarbleVFX.MarbleGold
                    , 0.3f + 0.05f * ChainLevel).Configure(GraniteMarbleVFX.MarbleGold, 10, 0.2f, 0.4f);
            }

            if ((int)Projectile.ai[0] == 0) {
                Projectile.ai[1]++;
                Projectile.velocity *= 0.987f;
                if (Projectile.ai[1] > 28f || Projectile.velocity.Length() < 4.5f) {
                    Projectile.ai[0] = 1f;
                    Projectile.netUpdate = true;
                }
                return;
            }

            //回手段穿墙
            Projectile.tileCollide = false;
            Vector2 toOwner = Projectile.Center.To(Owner.Center);
            if (toOwner.Length() < CatchRange) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                }
                return;
            }
            Vector2 desired = toOwner.UnitVector() * 16f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.16f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.localAI[0]++;
            if (Projectile.localAI[0] >= MaxBounce) {
                Projectile.ai[0] = 1f;
            }
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.1f) {
                Projectile.velocity.X = -oldVelocity.X;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.1f) {
                Projectile.velocity.Y = -oldVelocity.Y;
            }

            if (!VaultUtils.isServer) {
                //石响双层
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.22f, Pitch = 0.55f }, Projectile.Center);
                Vector2 outDir = Projectile.velocity.UnitVector();
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                        , outDir.RotatedByRandom(0.65f) * Main.rand.NextFloat(2f, 5.5f)
                        , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.35f, 0.65f))
                        .Configure(Main.rand.Next(16, 26));
                }
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, outDir * 0.8f
                    , GraniteMarbleVFX.MarbleDust, 0.4f).Configure(20, 0.6f, 0.05f);
            }
            return false;
        }

        //链击走乘区，不改 Projectile.damage
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.SourceDamage *= 1f + 0.1f * ChainLevel;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int chain = ChainLevel;
            if (!VaultUtils.isServer) {
                //凿击随层抬调
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.62f, Pitch = -0.05f + 0.16f * chain }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.3f, Pitch = 0.35f }, Projectile.Center);
                Vector2 back = -Projectile.velocity.UnitVector();
                for (int i = 0; i < 4 + chain; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                        , back.RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 5f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f)
                        , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f))
                        .Configure(Main.rand.Next(18, 30));
                }
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero
                    , GraniteMarbleVFX.MarbleGold, 0.16f + 0.03f * chain).Configure(10, 0.85f);
            }

            if ((int)Projectile.ai[0] != 0) {
                return;
            }

            if (!hitNPCs.Contains(target)) {
                hitNPCs.Add(target);
            }

            Projectile.localAI[0]++;
            NPC next = Projectile.localAI[0] < MaxBounce
                ? Projectile.Center.FindClosestNPC(ChainRange, ignoreTiles: false, onHitNPCs: hitNPCs)
                : null;
            if (next != null) {
                Projectile.velocity = Projectile.Center.To(next.Center).UnitVector() * 15f;
                Projectile.ai[1] = 0f;//链击续航
                if (Projectile.ai[2] < MaxChainLevel) {
                    Projectile.ai[2]++;
                }
            }
            else {
                Projectile.ai[0] = 1f;
            }
            Projectile.netUpdate = true;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            Player owner = Owner;
            //吸附致死=接住，否则碎屑
            if (owner.Alives() && Projectile.Center.To(owner.Center).Length() < CatchRange + 26f) {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.24f, Pitch = 0.72f }, Projectile.Center);
                Vector2 handPos = Vector2.Lerp(owner.Center, Projectile.Center, 0.35f);
                PRTLoader.NewParticle<PRT_Light>(handPos, Vector2.Zero, GraniteMarbleVFX.MarbleGold, 0.22f)
                    .Configure(12, 0.9f, _entity: owner, _followingRateRatio: 1f);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(handPos, Main.rand.NextVector2Circular(1.6f, 1.6f) + owner.velocity
                        , GraniteMarbleVFX.MarbleGold, 0.35f).Configure(GraniteMarbleVFX.MarbleCore, 12, 0.25f, 0.5f);
                }
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.45f, Pitch = -0.1f }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                    , Main.rand.NextVector2Circular(3f, 2f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f)
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f))
                    .Configure(Main.rand.Next(18, 28));
            }
            PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Vector2.Zero
                , GraniteMarbleVFX.MarbleDust, 0.45f).Configure(22, 0.65f, 0.05f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            //旋转残影，避急弯顶点崩
            float ghostAlpha = 0.3f + 0.07f * ChainLevel;
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 dpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                //近端暖白、尾端鎏金
                Color c = Color.Lerp(GraniteMarbleVFX.MarbleGold, GraniteMarbleVFX.MarbleCore, fade) * fade * ghostAlpha;
                c.A = 0;
                Main.EntitySpriteDraw(tex, dpos, null, c, Projectile.oldRot[i], origin
                    , Projectile.scale * fade, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor)
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Color gold = GraniteMarbleVFX.MarbleGold;
            gold.A = 0;
            Color core = GraniteMarbleVFX.MarbleCore;
            core.A = 0;

            int chain = ChainLevel;
            //闪烁挂旋转角
            float flick = 0.5f + 0.5f * MathF.Sin(Projectile.rotation * 3f);

            //历史位柔光残影
            float trailBoost = 0.35f + 0.11f * chain;
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 dpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                spriteBatch.Draw(glow, dpos, null, gold * fade * trailBoost, 0f, glow.Size() / 2f
                    , 0.35f * fade, SpriteEffects.None, 0f);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;

            //盘体辉光随链击
            spriteBatch.Draw(glow, pos, null, gold * (0.5f + 0.16f * chain), 0f, glow.Size() / 2f
                , 0.48f + 0.05f * chain, SpriteEffects.None, 0f);

            //旋转金边
            spriteBatch.Draw(tex, pos, null, gold * (0.3f + 0.32f * flick) * (0.75f + 0.25f * chain), Projectile.rotation
                , tex.Size() / 2f, Projectile.scale * 1.12f, SpriteEffects.None, 0f);

            //盘缘双 glint
            Vector2 rim = Projectile.rotation.ToRotationVector2() * 13f * Projectile.scale;
            spriteBatch.Draw(star, pos + rim, null, core * (0.45f + 0.45f * flick), Projectile.rotation
                , star.Size() / 2f, 0.07f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos - rim, null, gold * (0.3f + 0.35f * (1f - flick)), Projectile.rotation
                , star.Size() / 2f, 0.055f, SpriteEffects.None, 0f);

            spriteBatch.Draw(star, pos, null, core * 0.75f, -Projectile.rotation * 0.5f, star.Size() / 2f
                , 0.1f + 0.012f * chain, SpriteEffects.None, 0f);
        }
    }
}
