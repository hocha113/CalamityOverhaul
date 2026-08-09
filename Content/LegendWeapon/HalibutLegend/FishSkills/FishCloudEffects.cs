using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>腾鱼驾雾专属 shader 资源（域内加载器，不动 EffectLoader）</summary>
    internal class FishCloudAssets
    {
        /// <summary>程序化多瓣积云体</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishCloudPuff { get; private set; }
    }

    /// <summary>云絮</summary>
    internal class PRT_FishCloudWisp : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float spin;
        private float growRate;

        //SetProperty 先于 Configure 执行，缺省参数不覆盖已随机化的默认值
        public PRT_FishCloudWisp Configure(int lifetime, float spinSpeed = 0f, float grow = 0f) {
            Lifetime = lifetime;
            if (spinSpeed != 0f) {
                spin = spinSpeed;
            }
            if (grow > 0f) {
                growRate = grow;
            }
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            growRate = 1.004f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend; //漫反射碎云，禁加色
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(40, 65);
            }
            if (spin == 0f) {
                spin = Main.rand.NextFloat(0.004f, 0.012f);
            }
            if (growRate <= 0f) {
                growRate = 1.004f;
            }
        }

        public override void AI() {
            Velocity *= 0.955f;
            Velocity.Y -= 0.006f; //碎云微浮
            Rotation += spin * (Velocity.X >= 0f ? 1f : -1f);
            Scale *= growRate;
            float lc = LifetimeCompletion;
            //峰值压 0.62
            Opacity = MathF.Min(Time / 6f, 1f) * (1f - lc * lc) * 0.62f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //外圈虚影 + 主体，错转错缩做柔边（AlphaBlend 不叠亮）
            spriteBatch.Draw(tex, pos, null, Color * (Opacity * 0.32f), Rotation - 0.6f, origin, Scale * 0.81f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, Scale * 0.6f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>雨滴溅斑</summary>
    internal class PRT_FishCloudSplash : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override bool CanPool => true;

        private float endScale;

        public PRT_FishCloudSplash Configure(int lifetime, float finalScale = 0.13f) {
            Lifetime = lifetime;
            endScale = finalScale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            endScale = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 14;
            }
            if (endScale <= 0f) {
                endScale = 0.13f;
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //快张缓收，水环冲出后减速消散
            Scale = MathHelper.Lerp(0.02f, endScale, 1f - MathF.Pow(1f - t, 2.4f));
            Opacity = (1f - t) * (1f - t);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            Color c = Color with { A = 0 };
            //扁平水环
            spriteBatch.Draw(tex, pos, null, c * (Opacity * 0.5f), 0f, origin, new Vector2(Scale, Scale * 0.32f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, c * (Opacity * 0.24f), 0f, origin, new Vector2(Scale * 0.7f, Scale * 0.24f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
