using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

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
            //加色批 (SrcAlpha,One)：A 必须满值携带强度，A=0 即整层隐形
            Color edge = Color with { A = 255 };

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
        /// <summary>随风系数 0~1:环境余烬跟场上风漂,爆点余烬不跟(0)</summary>
        private float windFollow;

        public PRT_CultistEmber Configure(int lifetime, float riseAccel = 0.1f, float windFollow = 0f) {
            Lifetime = lifetime;
            rise = riseAccel;
            this.windFollow = windFollow;
            return this;
        }

        public override void Reset() {
            base.Reset();
            rise = 0f;
            wobbleSeed = 0f;
            windFollow = 0f;
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
            if (windFollow > 0f) {
                Velocity.X += (CultistScreenFX.Wind * 0.9f - Velocity.X) * 0.05f * windFollow;
            }
            Scale *= 0.972f;

            Opacity = MathHelper.Clamp(Time / 3f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 2.2f, 0f, 1f);
            Lighting.AddLight(Position, Color.ToVector3() * 0.3f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D glow = TexValue;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 pos = Position - Main.screenPosition;
            //加色批合同：A 携带强度
            Color edge = Color with { A = 255 };

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
        /// <summary>随风系数 0~1:环境晶尘跟风斜落,撞击碎屑不跟(0)</summary>
        private float windFollow;

        public PRT_CultistFrostMote Configure(int lifetime, float windFollow = 0f) {
            Lifetime = lifetime;
            this.windFollow = windFollow;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            windFollow = 0f;
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
            if (windFollow > 0f) {
                //环境晶尘:横向缓追风,纵向保持缓落(不吃 0.96 阻尼,否则长寿粒子会停在半空)
                Velocity.X += (CultistScreenFX.Wind * 0.7f - Velocity.X) * 0.04f * windFollow;
                Velocity.Y = MathHelper.Min(Velocity.Y + 0.02f, 2.2f);
            }
            else {
                Velocity *= 0.96f;
                Velocity.Y += 0.06f;
            }
            Rotation += spin;

            Opacity = MathHelper.Clamp(Time / 4f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 3f, 0f, 1f);
            Lighting.AddLight(Position, Color.ToVector3() * 0.18f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //加色批合同：A 携带强度
            Color edge = Color with { A = 255 };
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
            //加色批合同：A 携带强度
            Color edge = Color with { A = 255 };
            float t = LifetimeCompletion;
            float size = Scale * (0.5f + t * 0.5f);

            spriteBatch.Draw(glow, pos, null, edge * 0.7f * Opacity, 0f, glow.Size() / 2f, size * 1.4f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, edge * Opacity, Rotation, star.Size() / 2f, size * 0.16f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, Color.White * 0.7f * Opacity, Rotation, star.Size() / 2f, size * 0.09f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 灰烬片:环境粒子,翻滚缓落随风漂<br/>
    /// 体用原版尘埃图的灰烬帧(真 alpha 小片,能压出暗色),AlphaBlend 实色染相位灰;可选炽缘(日耀灰烬边上还烧着)<br/>
    /// 翻面靠横向缩放正弦起伏,不是位置抖
    /// </summary>
    internal class PRT_CultistAsh : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        /// <summary>原版尘埃图灰烬帧列(DustID.Ash=54 → x=540),三行变体</summary>
        private const int AshSheetX = 540;

        private float tumbleSeed;
        private float spin;
        private int frameRow;
        private float fallSpeed;
        /// <summary>炽缘色,A=0 即无缘</summary>
        private Color rim;

        /// <param name="fallSpeed">目标落速(px/帧),风只改横向</param>
        /// <param name="emberRim">炽缘色(日耀灰烬),null 无缘</param>
        public PRT_CultistAsh Configure(int lifetime, float fallSpeed, Color? emberRim = null) {
            Lifetime = lifetime;
            this.fallSpeed = fallSpeed;
            rim = emberRim ?? Color.Transparent;
            return this;
        }

        public override void Reset() {
            base.Reset();
            tumbleSeed = 0f;
            spin = 0f;
            frameRow = 0;
            fallSpeed = 0f;
            rim = Color.Transparent;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.02f, 0.06f) * (Main.rand.NextBool() ? 1f : -1f);
            tumbleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            frameRow = Main.rand.Next(3);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(90, 160);
                fallSpeed = 1.1f;
            }
        }

        public override void AI() {
            //片轻:横向紧跟风再叠自身摆,纵向缓追目标落速带微起伏
            float wind = CultistScreenFX.Wind;
            float sway = MathF.Sin(Time * 0.07f + tumbleSeed) * 0.45f;
            Velocity.X = MathHelper.Lerp(Velocity.X, wind * 0.9f + sway, 0.04f);
            float bob = MathF.Sin(Time * 0.11f + tumbleSeed * 1.7f) * 0.25f;
            Velocity.Y = MathHelper.Lerp(Velocity.Y, fallSpeed + bob, 0.03f);
            Rotation += spin + Velocity.X * 0.004f;

            //屏内起生,显隐都要慢(骤现骤消会被读成闪点)
            Opacity = MathHelper.Clamp(Time / 22f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 3f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D dust = TextureAssets.Dust.Value;
            Rectangle frame = new(AshSheetX, 10 * frameRow, 8, 8);
            Vector2 origin = new(4f, 4f);
            Vector2 pos = Position - Main.screenPosition;
            //翻面:横向缩放随正弦收放,片在空中翻转
            float flip = 0.35f + 0.65f * MathF.Abs(MathF.Sin(Time * 0.09f + tumbleSeed));
            Vector2 scale = new(Scale * flip, Scale);

            if (rim.A > 0) {
                spriteBatch.Draw(dust, pos, frame, (rim with { A = 255 }) * (0.6f * Opacity), Rotation, origin,
                    scale * 1.35f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(dust, pos, frame, (Color with { A = 255 }) * Opacity, Rotation, origin,
                scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 风暴流线:被风扯长的细亮丝,横掠屏幕(雨丝/沙线/火线的共用语法)<br/>
    /// 加色批,沿速度方向拉伸(速度即长度),阵风时更长更亮;短寿快过
    /// </summary>
    internal class PRT_CultistWindStreak : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float driftSeed;

        public PRT_CultistWindStreak Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            driftSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            driftSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(30, 60);
            }
        }

        public override void AI() {
            //横向紧咬风速(丝没有质量),纵向微飘
            float wind = CultistScreenFX.Wind;
            Velocity.X = MathHelper.Lerp(Velocity.X, wind * 4.5f, 0.08f);
            Velocity.Y += MathF.Sin(Time * 0.2f + driftSeed) * 0.04f;
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            float t = LifetimeCompletion;
            Opacity = MathHelper.Clamp(Time / 5f, 0f, 1f) * MathHelper.Clamp((1f - t) * 3f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //加色批合同:A 携带强度;长度随速度拉(速度即长度)
            Color edge = Color with { A = 255 };
            float speed = Velocity.Length();
            Vector2 stretch = new Vector2(0.14f, 0.45f + speed * 0.075f) * Scale;

            spriteBatch.Draw(tex, pos, null, edge * (0.7f * Opacity), Rotation, origin, stretch, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color.White * (0.35f * Opacity), Rotation, origin,
                stretch * new Vector2(0.5f, 0.85f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 风暴云絮:大团半透暗雾随风奔流(真 alpha 烟羽 Fog,AlphaBlend 可压暗)<br/>
    /// 星旋相是墨云,日耀相是热烟,月明相是死雾;慢旋,长寿,缓显缓隐
    /// </summary>
    internal class PRT_CultistStormWisp : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float spin;
        private float peakAlpha;

        /// <param name="peakAlpha">峰值透明度(暗雾压屏的量,0.12~0.3)</param>
        public PRT_CultistStormWisp Configure(int lifetime, float peakAlpha) {
            Lifetime = lifetime;
            this.peakAlpha = peakAlpha;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            peakAlpha = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.002f, 0.006f) * (Main.rand.NextBool() ? 1f : -1f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(120, 200);
                peakAlpha = 0.18f;
            }
        }

        public override void AI() {
            //云絮比片重:追风慢一拍,阵风里被拉快
            float wind = CultistScreenFX.Wind;
            Velocity.X = MathHelper.Lerp(Velocity.X, wind * 1.6f, 0.02f);
            Velocity.Y *= 0.98f;
            Rotation += spin;

            float t = LifetimeCompletion;
            float env = t < 0.25f ? t / 0.25f : t > 0.7f ? (1f - t) / 0.3f : 1f;
            Opacity = env * peakAlpha;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //真 alpha 烟羽实色染:A=255 才能压暗背景(加色/A=0 出不了暗)
            Color body = Color with { A = 255 };
            spriteBatch.Draw(tex, pos, null, body * Opacity, Rotation, origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
