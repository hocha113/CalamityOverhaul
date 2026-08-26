using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 火焰塔火舌:温度梯度载体,白金核→橙红→暗红烟尾,拒绝均匀橙色贴片。
    /// TearFlame01 黑底撕裂火形走加色批;沿速度拉伸,逐帧高频闪变,尖端天然撕裂
    /// </summary>
    internal class PRT_DefFlameTongue : BasePRT
    {
        public override int InGame_World_MaxCount => 240;
        public override string Texture => CWRConstant.Masking + "TearFlame01";
        public override bool CanPool => true;

        //温度梯度三站:白热→炽橙→暗红
        private static readonly Color HotWhite = new(255, 242, 205);
        private static readonly Color BlazeOrange = new(255, 158, 66);
        private static readonly Color DeepRed = new(198, 58, 24);

        private float flip;
        private float jitter = 1f;

        public PRT_DefFlameTongue Configure(int lifetime) {
            Lifetime = lifetime;
            flip = Main.rand.NextBool() ? 1f : -1f;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void Reset() {
            base.Reset();
            flip = 1f;
            jitter = 1f;
        }

        public override void AI() {
            //火舌减速+末段热浮升
            Velocity *= 0.94f;
            float t = LifetimeCompletion;
            Velocity.Y -= 0.05f * t;
            //逐帧高频闪变:火的瞬态签名
            jitter = Main.rand.NextFloat(0.88f, 1.12f);

            //温度梯度:寿命即冷却曲线
            Color ramp = t < 0.30f
                ? Color.Lerp(HotWhite, BlazeOrange, t / 0.30f)
                : Color.Lerp(BlazeOrange, DeepRed, (t - 0.30f) / 0.70f);
            //加色批 A 必须非零
            ramp.A = 255;
            Color = ramp * MathF.Pow(1f - t, 0.85f);

            Rotation = Velocity.ToRotation();
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float t = LifetimeCompletion;

            //沿速度拉伸,横向随寿命撑开(锥形扩散);贴图256px,系数按实测折算(火舌约40~80px)
            float speed = Velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.09f, 0.15f, 1.5f);
            Vector2 scale = new Vector2(0.15f * (1f + stretch), 0.14f * (1f + t * 0.8f)) * Scale * jitter;
            SpriteEffects fx = flip > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            //体层:温度梯度色
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, fx, 0f);
            //核层:更白更小,先于体层熄灭
            float coreA = MathF.Pow(MathHelper.Clamp(1f - t * 1.6f, 0f, 1f), 1.3f);
            Color core = new Color(255, 246, 224, 255) * (coreA * 0.75f);
            spriteBatch.Draw(tex, pos, null, core, Rotation, origin, scale * 0.55f, fx, 0f);
            return false;
        }
    }
}
