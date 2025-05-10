﻿using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Light : BasePRT
    {
        public override string Texture => "CalamityMod/Particles/Light";
        public float SquishStrenght;
        public float MaxSquish;
        public float HueShift;
        public float followingRateRatio;
        public Entity entity;
        [VaultLoaden("@CalamityMod/Particles/BloomCircle")]
        internal static Asset<Texture2D> BloomTex = null;
        public PRT_Light(Vector2 position, Vector2 velocity, float scale, Color color, int lifetime, float opacity = 1f
            , float squishStrenght = 1f, float maxSquish = 3f, float hueShift = 0f, Entity _entity = null, float _followingRateRatio = 0.9f) {
            Position = position;
            Velocity = velocity;
            Scale = scale;
            Color = color;
            Opacity = opacity;
            Rotation = 0;
            Lifetime = lifetime;
            SquishStrenght = squishStrenght;
            MaxSquish = maxSquish;
            HueShift = hueShift;
            entity = _entity;
            followingRateRatio = _followingRateRatio;
        }
        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        public override void AI() {
            Velocity *= (LifetimeCompletion >= 0.34f) ? 0.93f : 1.02f;

            Opacity = LifetimeCompletion > 0.5f ? ((float)Math.Sin(LifetimeCompletion * MathHelper.Pi) * 0.2f) + 0.8f : (float)Math.Sin(LifetimeCompletion * MathHelper.Pi);
            Scale *= 0.95f;

            Color = Main.hslToRgb(Main.rgbToHsl(Color).X + HueShift, Main.rgbToHsl(Color).Y, Main.rgbToHsl(Color).Z);

            if (entity != null && entity.active) {
                Position += entity.velocity * followingRateRatio;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Texture2D bloomTex = BloomTex.Value;

            float squish = MathHelper.Clamp(Velocity.Length() / 10f * SquishStrenght, 1f, MaxSquish);

            float rot = Velocity.ToRotation() + MathHelper.PiOver2;
            Vector2 origin = tex.Size() / 2f;
            Vector2 scale = new(Scale - (Scale * squish * 0.3f), Scale * squish);
            float properBloomSize = tex.Height / (float)bloomTex.Height;

            Vector2 drawPosition = Position - Main.screenPosition;

            Main.spriteBatch.Draw(bloomTex, drawPosition, null, Color * Opacity * 0.8f, rot, bloomTex.Size() / 2f, scale * 2 * properBloomSize, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPosition, null, Color * Opacity * 0.8f, rot, origin, scale * 1.1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPosition, null, Color.White * Opacity * 0.9f, rot, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
