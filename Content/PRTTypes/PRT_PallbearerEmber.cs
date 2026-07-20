using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 血色余烬：抬棺人专属迸溅火星。高速甩出后急减速、受微重力坠落，
    /// 顺速度方向拉丝 + 闪烁衰减；SoftGlow 双层核心（同色叠亮，无纯白高光），纯程序化
    /// </summary>
    internal class PRT_PallbearerEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> StreakTex = null;

        private float flickerSeed;
        private float gravity;

        public PRT_PallbearerEmber Configure(int lifetime, float gravityStrength = 0.05f) {
            Lifetime = lifetime;
            gravity = gravityStrength;
            return this;
        }

        public override void Reset() {
            base.Reset();
            flickerSeed = 0f;
            gravity = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            flickerSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(16, 28);
            }
            if (gravity == 0f) {
                gravity = 0.05f;
            }
        }

        public override void AI() {
            //急减速后余烬下坠
            Velocity *= 0.90f;
            if (Velocity.Length() < 3f) {
                Velocity.Y += gravity;
            }

            float lc = LifetimeCompletion;
            //瞬燃 → 衰减，带余烬闪烁
            float flicker = 0.78f + 0.22f * MathF.Sin(Time * 0.9f + flickerSeed);
            Opacity = MathF.Min(lc * 8f, 1f) * (1f - lc * lc) * flicker;
            Scale *= 0.965f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D core = TexValue;
            Texture2D streak = StreakTex?.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };

            //顺速度拉丝：速度快时火星呈线
            float speed = Velocity.Length();
            if (streak != null && speed > 1.5f) {
                float stretch = MathHelper.Clamp(speed * 0.14f, 0.3f, 1.4f);
                spriteBatch.Draw(streak, pos, null, col * (0.75f * Opacity)
                    , Velocity.ToRotation() + MathHelper.PiOver2, streak.Size() * 0.5f
                    , new Vector2(0.22f, stretch) * Scale, SpriteEffects.None, 0f);
            }

            Vector2 origin = core.Size() * 0.5f;
            //同色双层叠亮：小而热的芯，不引入纯白
            spriteBatch.Draw(core, pos, null, col * (0.55f * Opacity), 0f, origin, 0.3f * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(core, pos, null, col * (0.95f * Opacity), 0f, origin, 0.13f * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
