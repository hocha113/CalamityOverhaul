using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>月露枪管破珠露滴，重力弧线+速度拉伸，青白微光</summary>
    internal class PRT_SHPCMoondewDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        public PRT_SHPCMoondewDrop Configure(int lifeTime) {
            Lifetime = lifeTime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) Lifetime = 28;
        }

        public override void AI() {
            Velocity = new Vector2(Velocity.X * 0.995f, MathF.Min(Velocity.Y + 0.14f, 7f));
            Opacity = 1f - LifetimeCompletion;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            float speed = Velocity.Length();
            //坠速拉伸成滴形，体积守恒收细
            float stretch = 1f + MathHelper.Clamp(speed * 0.16f, 0f, 1.6f);
            Vector2 scale = new Vector2(stretch, 1f / MathF.Sqrt(stretch)) * (Scale * 0.11f);
            float rot = Velocity.ToRotation();
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            spriteBatch.Draw(tex, pos, null, Color * Opacity, rot, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, new Color(240, 250, 255) * (Opacity * 0.7f), rot, origin, scale * 0.5f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
