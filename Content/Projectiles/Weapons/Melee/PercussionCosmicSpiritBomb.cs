﻿using CalamityMod;
using CalamityMod.Graphics.Primitives;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class PercussionCosmicSpiritBomb : ModProjectile
    {
        public static Color[] colorDates;
        public int overTextIndex = 1;
        public Player Owner => Main.player[Projectile.owner];
        [VaultLoaden("@CalamityMod/ExtraTextures/Trails/ScarletDevilStreak")]
        public static Asset<Texture2D> scarletDevilStreakAsset = null;
        public override string Texture => CWRConstant.Cay_Proj_Melee + "CosmicSpiritBomb" + overTextIndex;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(Projectile.velocity.X);
            writer.Write(Projectile.velocity.Y);
            writer.Write(Projectile.timeLeft);

        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Projectile.velocity.X = reader.ReadSingle();
            Projectile.velocity.Y = reader.ReadSingle();
            Projectile.timeLeft = reader.ReadInt32();
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 150;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void AI() {
            float scaleModd = Main.mouseTextColor / 200f - 0.35f;
            scaleModd *= 0.2f;
            Projectile.scale = scaleModd + 0.95f;

            Vector2 toOwner = Projectile.position.To(Owner.position);
            float projDistance = toOwner.Length() / 100f;
            if (projDistance > 12.3333f) {
                projDistance = 12.3333f;
            }
            if (Projectile.ai[0] == 0) {
                Projectile.velocity = toOwner.UnitVector() * projDistance;
            }

            Projectile.rotation += Projectile.velocity.X * 0.03f;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(200, 200, 200, Projectile.alpha);
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn, 180);

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14, Projectile.position);
            Projectile.Explode(75);
            for (int k = 0; k < 10; k++) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                    , DustID.MagicMirror, Projectile.oldVelocity.X * 2.5f, Projectile.oldVelocity.Y * 2.5f);
            }
        }

        internal Color ColorFunction(float completionRatio) {
            colorDates ??= CWRUtils.GetColorDate(TextureAssets.Projectile[Type].Value);
            return VaultUtils.MultiStepColorLerp((150 - Projectile.timeLeft) / 150f, colorDates);
        }

        internal float WidthFunction(float completionRatio) {
            float amount = (float)Math.Pow(1f - completionRatio, 3.0);
            return MathHelper.Lerp(0f, 22f * Projectile.scale * Projectile.Opacity, amount);
        }

        public override bool PreDraw(ref Color lightColor) {
            GameShaders.Misc["CalamityMod:TrailStreak"].SetShaderTexture(scarletDevilStreakAsset);
            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(WidthFunction, ColorFunction
                , (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:TrailStreak"]), 30);
            return base.PreDraw(ref lightColor);
        }
    }
}
