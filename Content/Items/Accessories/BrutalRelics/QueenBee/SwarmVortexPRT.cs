using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenBee
{
    /// <summary>
    /// 涡群单蜂：真速度场驱动(切向环绕+径向归位+逐蜂抖动的合力逐帧转向)，
    /// 不是贴图旋转。锚定实体存活时逐帧继承其位移，蜂群随目标走。<br/>
    /// 三模式：收拢(远处螺旋卷入)/环绕(稳态涡)/散逸(外抛淡出)。
    /// 材质=蜂：短纺锤体沿速度取向 + 高频翅闪(横向宽度振荡) + 琥珀色相差
    /// </summary>
    internal class PRT_VortexBee : BasePRT
    {
        internal const byte ModeOrbit = 0;
        internal const byte ModeConverge = 1;
        internal const byte ModeScatter = 2;

        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        //蜂群量大，单独限池防涌爆
        public override int InGame_World_MaxCount => 600;

        private Color initialColor;
        private Entity anchor;
        private Vector2 lastCenter;
        private float orbitRadius;
        private float spinDir;
        private float startRadius;
        private byte mode;
        private float wingPhase;
        private float jitterSeed;

        /// <summary>anchor=锚定实体(可null=定点)；radius=稳态轨道半径；spin=±1旋向</summary>
        public PRT_VortexBee Configure(Entity anchor, float radius, float spin, int lifetime, byte flightMode) {
            this.anchor = anchor;
            orbitRadius = radius;
            spinDir = spin >= 0f ? 1f : -1f;
            Lifetime = lifetime;
            mode = flightMode;
            initialColor = Color;
            lastCenter = anchor?.Center ?? Position;
            startRadius = Vector2.Distance(Position, lastCenter);
            wingPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            jitterSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            anchor = null;
            lastCenter = default;
            orbitRadius = 0f;
            spinDir = 1f;
            startRadius = 0f;
            mode = ModeOrbit;
            wingPhase = 0f;
            jitterSeed = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AlphaBlend;

        public override void AI() {
            if (anchor != null && anchor.active) {
                lastCenter = anchor.Center;
                //跟着锚点平移，蜂群黏在目标身上
                Position += anchor.velocity;
            }

            float t = LifetimeCompletion;
            Vector2 rel = Position - lastCenter;
            float r = Math.Max(rel.Length(), 4f);
            Vector2 outward = rel / r;
            Vector2 tangent = new(-outward.Y * spinDir, outward.X * spinDir);

            if (mode == ModeScatter) {
                //散逸：沿切向外抛，逐渐失速下坠
                Velocity *= 0.965f;
                Velocity.Y += 0.06f;
            }
            else {
                //目标轨道半径：收拢模式从出生半径螺旋收进，稳态带呼吸
                float targetR = mode == ModeConverge
                    ? MathHelper.Lerp(startRadius, orbitRadius, (float)Math.Pow(t, 0.8))
                    : orbitRadius * (1f + 0.22f * (float)Math.Sin(Time * 0.13f + jitterSeed));

                float orbitSpeed = mode == ModeConverge
                    ? MathHelper.Lerp(3.2f, 7.5f, t)   //越收越急
                    : 6.2f;

                //速度场合成：切向环绕 + 径向归位 + 逐蜂高频抖动
                Vector2 radialPull = outward * (targetR - r) * 0.085f;
                Vector2 jitter = (Time * 0.55f + jitterSeed).ToRotationVector2()
                    * (float)Math.Sin(Time * 0.9f + jitterSeed * 3f) * 0.9f;
                Vector2 desired = tangent * orbitSpeed + radialPull + jitter;
                Velocity = Vector2.Lerp(Velocity, desired, 0.24f);
            }

            wingPhase += 1.55f;

            //淡入淡出：首15%浮现、尾25%消散
            float fade = Math.Min(t / 0.15f, 1f) * Math.Min((1f - t) / 0.25f, 1f);
            Color = initialColor * MathHelper.Clamp(fade, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float rot = Velocity.ToRotation() + MathHelper.PiOver2;

            //蜂体：沿速度拉伸的短纺锤
            float speedStretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.6f);
            Vector2 bodyScale = new Vector2(0.16f * (1f - speedStretch * 0.3f),
                0.26f * (1f + speedStretch)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, rot, origin, bodyScale, SpriteEffects.None, 0f);

            //翅闪：横向宽度高频振荡的淡色横片，读作扑翼
            float wing = 0.5f + 0.5f * (float)Math.Sin(wingPhase);
            Color wingColor = Color * (0.35f + 0.4f * wing);
            Vector2 wingScale = new Vector2(0.3f * (0.5f + wing * 0.8f), 0.1f) * Scale;
            spriteBatch.Draw(tex, pos, null, wingColor, rot + MathHelper.PiOver2, origin, wingScale, SpriteEffects.None, 0f);

            //头点微亮
            spriteBatch.Draw(tex, pos + Velocity.SafeNormalize(Vector2.Zero) * 2.5f * Scale, null,
                Color * 0.8f, rot, origin, bodyScale * 0.45f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
