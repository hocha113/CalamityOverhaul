using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 血柱飞沫:有物理的血团。真弹道(重力+微阻)、速度拉伸成泪滴、出生后衰减的张力抖动、
    /// 高速大团沿途甩出卫星小滴(质量守恒,本体随之缩)、三帧位置史拖出速度条痕;
    /// 落回湖面在水线上真正结束:微圈+一两粒浅色溅滴(<see cref="KikasaDomainDeco.DropletSplash"/>),
    /// 砸到地形留一小片渍。血柱与血形态三泉共用(<see cref="KikasaBloodColumnFX"/>),
    /// 只在观看端生成;色板浓血四件,新鲜期一点湿光,渐凝转暗
    /// </summary>
    internal class PRT_KikasaBloodSpray : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 700;

        private Color initialColor;
        private float lakeY;
        private float gravity;
        private float drag;
        private bool canShed;
        private float wobPhase;
        private float wobAmp;
        private float seed;
        //三帧位置史:速度条痕
        private Vector2 hist0;
        private Vector2 hist1;
        private Vector2 hist2;
        private int histCount;

        /// <summary>
        /// lakeY=落回即结束的水线(观看域),canShed=大团允许甩卫星滴(子滴关掉,防指数增殖)
        /// </summary>
        public PRT_KikasaBloodSpray Configure(int lifetime, float lakeY, bool canShed = true,
            float gravityPerFrame = 0.36f, float dragMul = 0.992f) {
            Lifetime = lifetime;
            this.lakeY = lakeY;
            this.canShed = canShed;
            gravity = gravityPerFrame;
            drag = dragMul;
            initialColor = Color;
            wobPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            wobAmp = 0.22f;
            seed = Main.rand.NextFloat(10f);
            histCount = 0;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            lakeY = 0f;
            gravity = 0f;
            drag = 1f;
            canShed = false;
            wobPhase = 0f;
            wobAmp = 0f;
            seed = 0f;
            histCount = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 40;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //位置史先记再动:条痕拖在身后
            hist2 = hist1;
            hist1 = hist0;
            hist0 = Position;
            if (histCount < 3) {
                histCount++;
            }

            Velocity.X *= drag;
            Velocity.Y = MathF.Min(Velocity.Y * drag + gravity, 16f);

            //张力抖动:出生后几帧最明显,之后衰减成滴
            wobPhase += 0.45f;
            wobAmp *= 0.955f;

            //高速大团甩卫星滴:本体缩一口,子滴不再甩(防增殖)
            float speed = Velocity.Length();
            if (canShed && Scale > 0.42f && speed > 5.5f && Main.rand.NextBool(16)) {
                Vector2 side = Velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2)
                    * (Main.rand.NextBool() ? 1f : -1f);
                PRTLoader.NewParticle<PRT_KikasaBloodSpray>(
                    Position - Velocity * 0.6f, Velocity * 0.7f + side * Main.rand.NextFloat(0.6f, 1.6f),
                    initialColor, Scale * Main.rand.NextFloat(0.32f, 0.45f))
                    ?.Configure(Math.Max(Lifetime - 4, 12), lakeY, false, gravity, drag);
                Scale *= 0.93f;
            }

            float t = LifetimeCompletion;
            //先鲜后凝:血离体越久越沉
            Color = Color.Lerp(initialColor, KikasaInk.BloodDeep, MathF.Pow(t, 1.5f) * 0.7f);
            Opacity = 1f - MathF.Pow(t, 4f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            //落回湖面:水线上结束,微圈+浅溅;湖不在了就当普通坠落
            if (lakeY != 0f && Velocity.Y > 0f && Position.Y >= lakeY - 1f) {
                KikasaDomainDeco.DropletSplash(new Vector2(Position.X, lakeY),
                    MathHelper.Clamp(Scale * 0.8f + speed * 0.03f, 0.25f, 1.2f));
                active = false;
                return;
            }
            //砸到地形:留一小片渍(限量),不穿地
            if (Collision.SolidCollision(Position - new Vector2(2f, 2f), 4, 4)) {
                if (Main.rand.NextBool(2)) {
                    KikasaInkFX.AddGroundSplat(Position, Velocity, 6f + Scale * 10f);
                }
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float speed = Velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.055f, 0f, 1.1f);
            float wob = MathF.Sin(wobPhase) * wobAmp;
            //泪滴:沿速度拉长,横向随张力鼓缩;头圆尾细由两层错位叠出
            Vector2 scale = new Vector2(0.36f * (1f - stretch * 0.35f) * (1f + wob),
                0.48f * (1f + stretch * 1.7f) * (1f - wob * 0.7f)) * Scale;

            Color body = Color * Opacity;
            Color rim = Color.Lerp(Color, KikasaInk.BloodDeep, 0.65f) * Opacity;
            Color bright = Color.Lerp(Color, KikasaInk.BloodBright, 0.5f) * Opacity;

            //速度条痕:两帧旧位上渐淡渐细的影,快时才拖
            if (histCount >= 3 && stretch > 0.35f) {
                Vector2 p1 = hist1 - Main.screenPosition;
                Vector2 p2 = hist2 - Main.screenPosition;
                spriteBatch.Draw(tex, p1, null, rim * 0.45f, Rotation, origin,
                    scale * new Vector2(0.7f, 0.8f), SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, p2, null, rim * 0.22f, Rotation, origin,
                    scale * new Vector2(0.45f, 0.6f), SpriteEffects.None, 0f);
            }

            //暗缘略宽一圈给体积;本体;尾端更细的第二层叠出泪滴头圆尾锥
            spriteBatch.Draw(tex, pos, null, rim, Rotation, origin,
                scale * new Vector2(1.32f, 1.06f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, Rotation, origin, scale, SpriteEffects.None, 0f);
            //头在前(速度方向),亮层压在头上,尾只剩暗缘与本体收细
            Vector2 headOff = (Rotation - MathHelper.PiOver2).ToRotationVector2() * (scale.Y * tex.Height * 0.18f);
            spriteBatch.Draw(tex, pos + headOff, null, bright * 0.55f, Rotation, origin,
                scale * new Vector2(0.72f, 0.5f), SpriteEffects.None, 0f);

            //新鲜期湿光:偏一侧的窄亮痕(A=0 加色),不是圆高光
            float fresh = 1f - MathHelper.Clamp(LifetimeCompletion * 1.8f, 0f, 1f);
            if (fresh > 0.05f) {
                Vector2 sideOff = (Rotation).ToRotationVector2() * (-scale.X * tex.Width * 0.22f);
                spriteBatch.Draw(tex, pos + sideOff + headOff * 0.5f, null,
                    (KikasaInk.BloodSheen with { A = 0 }) * (0.45f * fresh * Opacity), Rotation, origin,
                    scale * new Vector2(0.16f, 0.5f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
