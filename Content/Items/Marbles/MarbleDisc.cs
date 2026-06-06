using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Marbles
{
    /// <summary>
    /// 大理石飞盘：旋转掷出，在敌人 / 墙壁间弹射数次后回旋归手，可同时存在两枚
    /// </summary>
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
                .AddIngredient(ItemID.MarbleBlock, 18)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 8)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarbleDiscProj : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleDisc";
        private const int MaxBounce = 3;
        private Trail Trail;

        //ai[0]: 0=掷出, 1=回旋；localAI[0]=弹射计数；ai[1]=飞行计时
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
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
            Projectile.rotation += 0.45f;
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleGold.ToVector3() * 0.45f);

            if (Main.rand.NextBool(4) && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Vector2.Zero
                    , GraniteMarbleVFX.MarbleCore, 0.4f).Configure(GraniteMarbleVFX.MarbleCore, 10, 0.2f, 0.4f);
            }

            if ((int)Projectile.ai[0] == 0) {
                Projectile.ai[1]++;
                Projectile.velocity *= 0.987f;
                if (Projectile.ai[1] > 28f || Projectile.velocity.Length() < 4.5f) {
                    Projectile.ai[0] = 1f;
                }
            }
            else {
                Vector2 toOwner = Projectile.Center.To(Owner.Center);
                if (toOwner.Length() < 34f) {
                    Projectile.Kill();
                    return;
                }
                Vector2 desired = toOwner.UnitVector() * 16f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.16f);
            }
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
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f, Volume = 0.6f }, Projectile.Center);
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f, Volume = 0.6f }, Projectile.Center);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Main.rand.NextVector2Circular(2f, 2f)
                        , GraniteMarbleVFX.MarbleGold, 0.5f).Configure(GraniteMarbleVFX.MarbleGold, 12, 0.2f, 0.6f);
                }
            }

            if ((int)Projectile.ai[0] != 0) {
                return;
            }
            Projectile.localAI[0]++;
            NPC next = FindNextTarget(target);
            if (Projectile.localAI[0] < MaxBounce && next != null) {
                Projectile.velocity = Projectile.Center.To(next.Center).UnitVector() * 13f;
            }
            else {
                Projectile.ai[0] = 1f;
            }
        }

        private NPC FindNextTarget(NPC exclude) {
            NPC best = null;
            float bestDist = 620f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == exclude.whoAmI || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float d = Projectile.Center.To(npc.Center).Length();
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        public float GetWidthFunc(float c) {
            float p = c > 0.5f ? 1f - c : c;
            return p * 2f * Projectile.scale * Projectile.width * 0.9f;
        }

        public Color GetColorFunc(Vector2 _) => GraniteMarbleVFX.MarbleGold * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 dpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Color c = GraniteMarbleVFX.MarbleCore * fade * 0.3f; c.A = 0;
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
            GraniteMarbleVFX.ApplyGradientTrail(effect, GraniteMarbleVFX.MarbleBar, CWRConstant.Masking + "MotionTrail3");
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}
