using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 岩鱼锤石尘团，Fog 染哑光暖灰的实体尘（AlphaBlend 非光效）<br/>
    /// front 模式作砸点尘环波前，贴地压扁、强水平初速快速衰减、拖两级残影
    /// </summary>
    internal class PRT_FishRockDust : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private bool groundFront;
        private float grow;
        private float baseOpacity;
        private float spin;

        public PRT_FishRockDust Configure(int lifetime, float opacity = 0.5f, bool front = false, float growth = 0.011f) {
            Lifetime = lifetime;
            baseOpacity = opacity;
            groundFront = front;
            grow = growth;
            //波前贴地压扁，倾角只允许微偏；普通尘团自由取向慢滚
            if (front) {
                Rotation = Main.rand.NextFloat(-0.16f, 0.16f);
                spin = 0f;
            }
            else {
                spin = Main.rand.NextFloat(0.006f, 0.02f) * (Main.rand.NextBool() ? 1f : -1f);
            }
            return this;
        }

        public override void Reset() {
            base.Reset();
            groundFront = false;
            grow = 0f;
            baseOpacity = 0f;
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            //防漏 Configure 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(20, 32);
                baseOpacity = 0.5f;
                grow = 0.011f;
            }
        }

        public override void AI() {
            if (groundFront) {
                //波前，强水平速度指数衰减，微升腾
                Velocity.X *= 0.90f;
                Velocity.Y = Velocity.Y * 0.9f - 0.03f;
            }
            else {
                Velocity *= 0.93f;
                Velocity.Y -= 0.012f;
            }
            Scale += grow;
            Rotation += spin;

            float fadeIn = MathHelper.Clamp(Time / 3f, 0f, 1f);
            float fadeOut = 1f - MathF.Pow(LifetimeCompletion, 1.6f);
            Opacity = baseOpacity * fadeIn * fadeOut;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.015f) {
                return false;
            }
            Texture2D tex = TexValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 pos = Position - Main.screenPosition;
            //哑光暖灰随生命偏冷偏暗，乘环境光贴地面明度
            Color body = Color.Lerp(new Color(126, 114, 98), new Color(84, 78, 70), LifetimeCompletion);
            body = body.MultiplyRGB(Lighting.GetColor(Position.ToTileCoordinates()));
            Vector2 squish = groundFront ? new Vector2(1.35f, 0.58f) : Vector2.One;

            if (groundFront) {
                //横扫残影
                for (int i = 2; i >= 1; i--) {
                    spriteBatch.Draw(tex, pos - Velocity * (i * 2.2f), null, body * (Opacity * (0.34f - i * 0.11f))
                        , Rotation, origin, Scale * 0.6f * squish * (1f - i * 0.08f), SpriteEffects.None, 0f);
                }
            }
            spriteBatch.Draw(tex, pos, null, body * Opacity, Rotation, origin, Scale * 0.6f * squish, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 岩鱼锤瓦砾，白像素三矩形拼出的硬边角砾，哑光乘环境光零发光；<br/>
    /// 受重力抛物翻滚、落地弹跳一次、二次触地落定并快速收尾
    /// </summary>
    internal class PRT_FishRockRubble : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private float gravity;
        private float spin;
        private bool bounced;
        private float bright;//块面明度随机，让群体有深浅

        public PRT_FishRockRubble Configure(int lifetime, float gravityStrength = 0.34f) {
            Lifetime = lifetime;
            gravity = gravityStrength;
            return this;
        }

        public override void Reset() {
            base.Reset();
            gravity = 0f;
            spin = 0f;
            bounced = false;
            bright = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.09f, 0.26f) * (Main.rand.NextBool() ? 1f : -1f);
            bright = Main.rand.NextFloat(0.72f, 1.08f);
            //防漏 Configure 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(30, 48);
            }
            if (gravity == 0f) {
                gravity = 0.34f;
            }
        }

        public override void AI() {
            if (Velocity.Y < 16f) {
                Velocity.Y += gravity;
            }
            Velocity.X *= 0.99f;
            //翻滚速率挂水平速度，滚得快转得快
            Rotation += spin * (0.55f + Math.Abs(Velocity.X) * 0.07f);

            if (Velocity.Y > 0f && Collision.SolidCollision(Position - new Vector2(4f), 8, 8)) {
                if (!bounced) {
                    bounced = true;
                    Velocity.Y = -Math.Abs(Velocity.Y) * 0.42f;
                    Velocity.X *= 0.55f;
                    spin *= 1.5f;
                }
                else {
                    //二次触地落定，停转停移，提前进入收尾
                    Velocity *= 0.2f;
                    spin *= 0.5f;
                    if (Lifetime - Time > 8) {
                        Time = Lifetime - 8;
                    }
                }
            }

            Opacity = MathHelper.Clamp(Time / 2f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 3.2f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f) {
                return false;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = new(0.5f, 0.5f);

            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            Color face = new Color(115, 108, 100).MultiplyRGB(light) * bright;
            Color under = new Color(52, 48, 45).MultiplyRGB(light);
            Color edge = new Color(158, 150, 138).MultiplyRGB(light) * bright;

            float w = 10f * Scale;
            float h = 7f * Scale;
            Vector2 rotDown = (Rotation + MathHelper.PiOver2).ToRotationVector2();

            //暗底面错位在下，给块体厚度
            spriteBatch.Draw(pixel, pos + rotDown * (h * 0.22f), src, under * Opacity, Rotation
                , origin, new Vector2(w, h), SpriteEffects.None, 0f);
            //主面
            spriteBatch.Draw(pixel, pos, src, face * Opacity, Rotation
                , origin, new Vector2(w * 0.94f, h * 0.86f), SpriteEffects.None, 0f);
            //受光小棱面，偏转错位打破矩形轮廓
            spriteBatch.Draw(pixel, pos - rotDown * (h * 0.24f), src, edge * (Opacity * 0.9f), Rotation + 0.5f
                , origin, new Vector2(w * 0.42f, h * 0.4f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 反重力石屑，蓄力预告用的小哑光碎屑，向上渐加速漂浮，微水平游移，顶端淡出
    /// </summary>
    internal class PRT_FishRockMote : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private float drift;
        private float bright;

        public PRT_FishRockMote Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            drift = 0f;
            bright = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            drift = Main.rand.NextFloat(MathHelper.TwoPi);
            bright = Main.rand.NextFloat(0.75f, 1.05f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(14, 22);
            }
        }

        public override void AI() {
            //反重力
            if (Velocity.Y > -3.4f) {
                Velocity.Y -= 0.1f;
            }
            Velocity.X = Velocity.X * 0.95f + MathF.Sin(Time * 0.35f + drift) * 0.05f;
            Rotation += 0.08f;

            Opacity = MathHelper.Clamp(Time / 3f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 2.6f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f) {
                return false;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = new(0.5f, 0.5f);
            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            Color face = new Color(128, 120, 110).MultiplyRGB(light) * bright;
            Color under = new Color(60, 55, 51).MultiplyRGB(light);

            Vector2 size = new Vector2(3.4f, 2.3f) * Scale;
            spriteBatch.Draw(pixel, pos + new Vector2(0.8f), src, under * Opacity, Rotation
                , origin, size, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, src, face * Opacity, Rotation
                , origin, size * 0.9f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
