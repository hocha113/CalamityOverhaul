using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Ashreign
{
    /// <summary>
    /// 烬雪絮片：黑色灰烬缓落，横向随风轻摆，缓转缓灭。
    /// 材质=燃尽的灰烬软屑：无光、吸光、轻到被气流拨弄。
    /// Fog 真 alpha 小尺度当软片体（暗层物理上不能用加色实现），AlphaBlend 直接染暗色。
    /// 上升火星刻意不新建类型，复用既有 PRT_DefEmber（负重力+低阻尼即为上升燃屑）
    /// </summary>
    internal class PRT_AshreignFlake : BasePRT
    {
        public override int InGame_World_MaxCount => 160;
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private Color initialColor;
        private float swayAmp;
        private float swayPhase;
        private float rot;
        private float rotSpeed;
        private float fallSpeed;

        public PRT_AshreignFlake Configure(int lifetime, float fall = 0.55f, float sway = 0.28f) {
            Lifetime = lifetime;
            initialColor = Color;
            fallSpeed = fall;
            swayAmp = sway;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            rot = Main.rand.NextFloat(MathHelper.TwoPi);
            rotSpeed = Main.rand.NextFloat(-0.02f, 0.02f);
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            swayAmp = 0f;
            swayPhase = 0f;
            rot = 0f;
            rotSpeed = 0f;
            fallSpeed = 0.55f;
        }

        public override void AI() {
            //缓落：竖向趋近端速，横向=风裹挟+自摆
            Velocity.Y = MathHelper.Lerp(Velocity.Y, fallSpeed, 0.03f);
            float wind = Main.windSpeedCurrent * 1.1f;
            float sway = MathF.Sin(Time * 0.045f + swayPhase) * swayAmp;
            Velocity.X = MathHelper.Lerp(Velocity.X, wind + sway, 0.05f);
            rot += rotSpeed;

            //两端渐隐：出生 12 帧淡入，末段 25% 淡出（出生透明度有人清，防隐形粒）
            float t = LifetimeCompletion;
            float envelope = Math.Min(Time / 12f, 1f) * MathHelper.Clamp((1f - t) / 0.25f, 0f, 1f);
            Color = initialColor * envelope;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //Fog 256 画布，0.04~0.08 缩放即 10~20px 软屑；轻微扁片
            Vector2 scale = new Vector2(1f, 0.82f) * (0.052f * Scale);
            spriteBatch.Draw(tex, pos, null, Color, rot, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
