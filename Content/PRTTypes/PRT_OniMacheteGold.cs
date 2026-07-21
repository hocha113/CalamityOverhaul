using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼砍刀熔金迸屑，白金→熔金→硫火→暗，重力抛物拉长
    /// </summary>
    internal class PRT_OniMacheteGold : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 4000;

        private bool affectedByGravity;
        private float coolRate;
        private float baseScale;

        private static readonly Color ColHot = new(255, 244, 205);
        private static readonly Color ColGold = new(255, 190, 60);
        private static readonly Color ColBrim = new(220, 70, 18);
        private static readonly Color ColDark = new(60, 18, 10);

        public PRT_OniMacheteGold Configure(int lifetime, bool gravity = true, float cooling = 1f) {
            Lifetime = lifetime;
            affectedByGravity = gravity;
            coolRate = cooling;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            affectedByGravity = false;
            coolRate = 1f;
            baseScale = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            float t = MathHelper.Clamp(LifetimeCompletion * coolRate, 0f, 1f);

            Velocity *= 0.96f;
            if (affectedByGravity) {
                Velocity = new Vector2(Velocity.X * 0.985f, Velocity.Y + 0.30f);
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            Scale = baseScale * (1f - t * 0.45f);

            //热白金→熔金→硫火→暗
            Color = t < 0.30f
                ? Color.Lerp(ColHot, ColGold, t / 0.30f)
                : t < 0.70f
                    ? Color.Lerp(ColGold, ColBrim, (t - 0.30f) / 0.40f)
                    : Color.Lerp(ColBrim, ColDark, (t - 0.70f) / 0.30f);

            Opacity = 1f - MathF.Pow(t, 3f);
            if (t < 0.5f && Main.rand.NextBool(6)) {
                Lighting.AddLight(Position, 0.35f, 0.24f, 0.06f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D streak = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;

            float speedStretch = MathHelper.Clamp(Velocity.Length() * 0.10f, 0.6f, 2.2f);
            Vector2 scale = new Vector2(0.42f, speedStretch) * Scale;
            spriteBatch.Draw(streak, drawPos, null, Color * Opacity, Rotation
                , streak.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            Texture2D glow = SoftGlow?.Value;
            if (glow != null) {
                spriteBatch.Draw(glow, drawPos, null, Color * (Opacity * 0.55f), 0f
                    , glow.Size() * 0.5f, Scale * 0.5f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
