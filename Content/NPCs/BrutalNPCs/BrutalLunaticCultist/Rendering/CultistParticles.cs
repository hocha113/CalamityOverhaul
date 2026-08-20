using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering
{
    /// <summary>符文残片：竖排刻痕缓浮渐熄，教徒身份粒子（挪移/假身破碎/死亡崩解）</summary>
    internal class PRT_CultistRune : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float flickerSeed;

        public PRT_CultistRune Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            flickerSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Main.rand.NextFloat(-0.35f, 0.35f);
            flickerSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(24, 40);
            }
        }

        public override void AI() {
            //浮升减速，符文缓缓向上飘散
            Velocity *= 0.94f;
            Velocity.Y -= 0.05f;
            Rotation += Velocity.X * 0.01f;

            float flicker = 0.78f + 0.22f * (float)Math.Sin(Time * 0.55f + flickerSeed);
            Opacity = MathHelper.Clamp(Time / 4f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 2.6f, 0f, 1f) * flicker;
            Lighting.AddLight(Position, Color.ToVector3() * 0.2f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            Color edge = Color; edge.A = 0;

            //底晕
            spriteBatch.Draw(glow, pos, null, edge * 0.3f * Opacity, 0f, glow.Size() / 2f, Scale * 0.3f, SpriteEffects.None, 0f);
            //主刻痕：细竖条 + 两侧短刻，拼出符文式样
            Vector2 mainScale = new Vector2(0.16f, 0.85f) * Scale;
            spriteBatch.Draw(tex, pos, null, edge * Opacity, Rotation, origin, mainScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos + Rotation.ToRotationVector2() * 5f * Scale, null, edge * 0.8f * Opacity,
                Rotation + MathHelper.PiOver2, origin, mainScale * new Vector2(0.8f, 0.42f), SpriteEffects.None, 0f);
            //亮芯
            spriteBatch.Draw(tex, pos, null, Color.White * 0.6f * Opacity, Rotation, origin,
                mainScale * new Vector2(0.5f, 0.72f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>焚焰余烬：湍流上浮的火点，收缩渐熄</summary>
    internal class PRT_CultistEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private float rise;
        private float wobbleSeed;

        public PRT_CultistEmber Configure(int lifetime, float riseAccel = 0.1f) {
            Lifetime = lifetime;
            rise = riseAccel;
            return this;
        }

        public override void Reset() {
            base.Reset();
            rise = 0f;
            wobbleSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            wobbleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(20, 34);
                rise = 0.1f;
            }
        }

        public override void AI() {
            Velocity *= 0.95f;
            Velocity.Y -= rise;
            Velocity.X += (float)Math.Sin(Time * 0.3f + wobbleSeed) * 0.06f;
            Scale *= 0.972f;

            Opacity = MathHelper.Clamp(Time / 3f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 2.2f, 0f, 1f);
            Lighting.AddLight(Position, Color.ToVector3() * 0.3f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D glow = TexValue;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color edge = Color; edge.A = 0;

            spriteBatch.Draw(glow, pos, null, edge * 0.85f * Opacity, 0f, glow.Size() / 2f, Scale * 0.22f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, Color.White * 0.5f * Opacity, Time * 0.05f, star.Size() / 2f, Scale * 0.02f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>霜辉冰屑：锐利小晶片缓落，冷光渐隐</summary>
    internal class PRT_CultistFrostMote : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float spin;

        public PRT_CultistFrostMote Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.08f, 0.2f) * (Main.rand.NextBool() ? 1f : -1f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(22, 36);
            }
        }

        public override void AI() {
            Velocity *= 0.96f;
            Velocity.Y += 0.06f;
            Rotation += spin;

            Opacity = MathHelper.Clamp(Time / 4f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 3f, 0f, 1f);
            Lighting.AddLight(Position, Color.ToVector3() * 0.18f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            Color edge = Color; edge.A = 0;
            Vector2 shard = new Vector2(0.2f, 0.6f) * Scale;

            spriteBatch.Draw(tex, pos, null, edge * Opacity, Rotation, origin, shard, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color.White * 0.65f * Opacity, Rotation, origin,
                shard * new Vector2(0.45f, 0.75f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>咒文闪辉：施法瞬间的四芒星涨缩，launch/commit 顿音</summary>
    internal class PRT_CultistGlyphFlash : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarTexture";
        public override bool CanPool => true;

        public PRT_CultistGlyphFlash Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 12;
            }
        }

        public override void AI() {
            //快涨慢缩，一次呼吸
            float t = LifetimeCompletion;
            Opacity = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
            Rotation += 0.02f;
            Lighting.AddLight(Position, Color.ToVector3() * 0.5f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D star = TexValue;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color edge = Color; edge.A = 0;
            float t = LifetimeCompletion;
            float size = Scale * (0.5f + t * 0.5f);

            spriteBatch.Draw(glow, pos, null, edge * 0.7f * Opacity, 0f, glow.Size() / 2f, size * 1.4f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, edge * Opacity, Rotation, star.Size() / 2f, size * 0.16f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, Color.White * 0.7f * Opacity, Rotation, star.Size() / 2f, size * 0.09f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
