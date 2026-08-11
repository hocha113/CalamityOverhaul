using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Tiles.BloodAltars
{
    /// <summary>
    /// 浓血团。比刻心者的细液滴大一号：出膛沿速度各向异性拉长，飞行中体积摆动，
    /// 落回液面时压扁铺开并把涟漪推回池子。<br/>
    /// Extra_98 是真 alpha 软团，必须 AlphaBlend
    /// </summary>
    internal class PRT_BloodGout : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 200;

        private Color initialColor;
        private float gravity;
        private float wobblePhase;
        private float wobbleAmp;
        private float splatY;
        private float splatHalfX;
        private float splatCenterX;
        private BloodAltarRite rite;
        private bool splatted;

        /// <summary>
        /// <paramref name="surfaceY"/> 给出液面世界 Y；血团越过它就算落回池中。
        /// 传 <see cref="float.MaxValue"/> 表示这团不回池（例如喷向空中的溅出）
        /// </summary>
        public PRT_BloodGout Configure(int lifetime, float gravityPerFrame = 0.34f
            , float surfaceY = float.MaxValue, float surfaceCenterX = 0f, float surfaceHalfWidth = 0f
            , BloodAltarRite owner = null) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            splatY = surfaceY;
            splatCenterX = surfaceCenterX;
            splatHalfX = surfaceHalfWidth;
            rite = owner;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            wobblePhase = 0f;
            wobbleAmp = 0f;
            splatY = float.MaxValue;
            splatHalfX = 0f;
            splatCenterX = 0f;
            rite = null;
            splatted = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Opacity = 1f;
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            wobbleAmp = Main.rand.NextFloat(0.10f, 0.22f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(30, 46);
            }
            if (gravity <= 0f) {
                gravity = 0.34f;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity.X *= 0.988f;
            Velocity.Y += gravity;
            if (Velocity.Y > 15f) {
                Velocity.Y = 15f;
            }

            wobblePhase += 0.28f;
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            //空中只轻淡，色量留给落面那一下
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(LifetimeCompletion, 3.1f) * 0.5f);

            TrySplat();
        }

        private void TrySplat() {
            if (splatted || Velocity.Y <= 0.2f || Position.Y < splatY) {
                return;
            }
            if (splatHalfX > 0f && MathF.Abs(Position.X - splatCenterX) > splatHalfX) {
                return;
            }

            splatted = true;
            Position.Y = splatY;
            float force = MathHelper.Clamp(Velocity.Length() * 0.11f, 0.25f, 1f);
            Velocity = Vector2.Zero;
            rite?.PushRipple(Position, force);
            //铺开后很快没入液面
            Lifetime = Time + Main.rand.Next(6, 11);
            Scale *= 1.25f;
        }

        public override bool ShouldUpdatePosition() => !splatted;

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            Color draw = Color.MultiplyRGB(light) * Opacity;

            if (splatted) {
                //落面：切向铺开、法向压扁，两层错位读出摊开的湿感
                float spread = 1f + (Time - (Lifetime - 10)) * 0.06f;
                Vector2 flat = new Vector2(0.72f * spread, 0.16f) * Scale;
                spriteBatch.Draw(tex, pos, null, draw, 0f, origin, flat, SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, pos, null, draw * 0.65f, 0.18f, origin
                    , flat * new Vector2(0.7f, 0.85f), SpriteEffects.None, 0f);
                return false;
            }

            //体积摆动：一团血在空中不是刚体
            float wob = MathF.Sin(wobblePhase) * wobbleAmp;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.038f, 0f, 0.95f);
            Vector2 scale = new Vector2(0.52f * (1f - stretch * 0.28f) * (1f - wob)
                , 0.74f * (1f + stretch * 1.35f) * (1f + wob)) * Scale;

            spriteBatch.Draw(tex, pos, null, draw, Rotation, origin, scale, SpriteEffects.None, 0f);
            //内芯更浓，压出"厚"
            spriteBatch.Draw(tex, pos, null, draw, Rotation, origin
                , scale * new Vector2(0.52f, 0.86f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>祭品碎壳：半透明膜片被撑破后翻滚下坠，边缘先干涸发暗</summary>
    internal class PRT_BloodShell : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "HitJagged01";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 120;

        private Color initialColor;
        private float spin;
        private float flatten;

        public PRT_BloodShell Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spin = 0f;
            flatten = 1f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Opacity = 1f;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.09f, 0.21f) * (Main.rand.NextBool() ? 1f : -1f);
            flatten = Main.rand.NextFloat(0.34f, 0.62f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 40);
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Velocity.X *= 0.965f;
            Velocity.Y += 0.24f;
            Rotation += spin;
            spin *= 0.985f;

            //先干涸转暗，再淡出，膜片不做发光
            Color = Color.Lerp(initialColor, BloodAltarFx.ColDry, MathF.Min(1f, t * 1.6f));
            Opacity = 1f - MathF.Pow(t, 2.6f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            Color draw = Color.MultiplyRGB(light) * Opacity;
            //翻滚中的膜片：靠单轴压扁读出"片"，对称贴图用 abs 收零不给负 scale
            float fold = MathF.Abs(MathF.Cos(Rotation * 1.7f)) * 0.7f + 0.3f;
            Vector2 scale = new Vector2(0.30f, 0.30f * flatten * fold) * Scale;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, draw, Rotation
                , tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
