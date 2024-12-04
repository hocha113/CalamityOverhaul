using CalamityMod.Graphics.Primitives;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee.Extras;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class DivineSourceBladeProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "DivineSourceBeam";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 25;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Player player = Main.player[Projectile.owner];
            Item item = player.GetItem();
            if (Projectile.numHits == 0 && item.type == ModContent.ItemType<DivineSourceBlade>()) {
                int proj = Projectile.NewProjectile(new EntitySource_ItemUse(player, item), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TerratomereSlashCreator>(),
                Projectile.damage, 0, Projectile.owner, target.whoAmI, Main.rand.NextFloat(MathHelper.TwoPi));
                Main.projectile[proj].timeLeft = 30;
            }
        }

        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
        }

        internal Color ColorFunction(float completionRatio) {
            float amount = MathHelper.Lerp(0.65f, 1f, (float)Math.Cos((0f - Main.GlobalTimeWrappedHourly) * 3f) * 0.5f + 0.5f);
            float num = Utils.GetLerpValue(1f, 0.64f, completionRatio, clamped: true) * Projectile.Opacity;

            Color value = Color.Lerp(new Color(255, 223, 186), new Color(255, 218, 185), (float)Math.Sin(completionRatio * MathF.PI * 1.6f - Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);

            return Color.Lerp(new Color(255, 248, 220), value, amount) * num;
        }

        internal float WidthFunction(float completionRatio) {
            float amount = (float)Math.Pow(1f - completionRatio, 3.0);
            return MathHelper.Lerp(0f, 22f * Projectile.scale * Projectile.Opacity, amount);
        }

        public override bool PreDraw(ref Color lightColor) {
            GameShaders.Misc["CalamityMod:TrailStreak"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(WidthFunction, ColorFunction
                , (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:TrailStreak"]), 30);

            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White,
                Projectile.rotation + MathHelper.PiOver2,
                CWRUtils.GetOrig(mainValue),
                Projectile.scale,
                Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally,
                0
                );
            return false;
        }
    }
}
