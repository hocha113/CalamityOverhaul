using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>黑曜石鱼专属演出资产</summary>
    internal class FishObsidianAssets
    {
        /// <summary>鱼体火山玻璃单趟着色：剪影压暗 + 轮廓窄镜面随光向扫动 + 紫黑偏光 + 余温矿脉</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishObsidianGloss { get; private set; }
    }

    /// <summary>
    /// 黑曜石玻璃碎片：暗色近剪影的锐利薄片，贝壳状断口迸射后受重力翻滚下坠。<br/>
    /// 自旋用旋转拖影表达（位置残影表达不了自旋），翻滚中棱面周期性正对视线迸出紫白爆闪。<br/>
    /// <see cref="Configure"/> 的 slowMoFrames 给爆裂慢放帧：先全速出膛再强阻尼急停，读作碎裂瞬间的时间凝滞。<br/>
    /// 贴图 Extra_98 带真 alpha，默认 AlphaBlend 直绘安全；爆闪层用 A=0 颜色在同批次内做加色
    /// </summary>
    internal class PRT_FishObsidianShard : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 2000;

        private static readonly Color GlintCol = new(226, 208, 255);

        private Color bodyColor;
        private float gravity;
        private float drag;
        private int slowFrames;
        private float spin;
        private float baseScale;
        private float glintPhase;
        private float glintSpeed;
        private float aspect;
        private float glint;

        public PRT_FishObsidianShard Configure(int lifetime, float gravityPerFrame = 0.26f
            , float dragMul = 0.988f, int slowMoFrames = 0) {
            Lifetime = lifetime;
            gravity = gravityPerFrame;
            drag = dragMul;
            slowFrames = slowMoFrames;
            bodyColor = Color;
            baseScale = Scale;
            spin = Main.rand.NextFloat(0.16f, 0.42f) * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            aspect = Main.rand.NextFloat(0.20f, 0.34f);
            glintPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            glintSpeed = Main.rand.NextFloat(0.35f, 0.75f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            bodyColor = default;
            gravity = 0.26f;
            drag = 0.988f;
            slowFrames = 0;
            spin = 0f;
            baseScale = 0f;
            glintPhase = 0f;
            glintSpeed = 0.5f;
            aspect = 0.26f;
            glint = 0f;
        }

        public override void AI() {
            if (slowFrames > 0) {
                //慢放帧：强阻尼把爆速立刻压下来，之后才交还给重力
                slowFrames--;
                Velocity *= 0.82f;
            }
            else {
                Velocity.X *= drag;
                Velocity.Y += gravity;
                if (Velocity.Y > 15f) {
                    Velocity.Y = 15f;
                }
            }

            spin *= 0.985f;
            Rotation += spin;

            float t = LifetimeCompletion;
            Scale = baseScale * (1f - MathF.Pow(t, 2.2f) * 0.55f);
            Opacity = 1f - MathF.Pow(t, 3f);
            glint = MathF.Pow(MathF.Abs(MathF.Sin(Time * glintSpeed + glintPhase)), 9f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.05f || Opacity < 0.02f) {
                return false;
            }

            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //快速飞掠时沿长轴略拉伸，读作切割空气的薄片
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.8f);
            Vector2 sc = new Vector2(aspect, 0.85f + stretch) * (Scale * 0.6f);

            //旋转拖影两帧
            spriteBatch.Draw(tex, pos, null, bodyColor * (Opacity * 0.32f), Rotation - spin * 2.6f
                , origin, sc * 0.96f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, bodyColor * (Opacity * 0.15f), Rotation - spin * 5.2f
                , origin, sc * 0.92f, SpriteEffects.None, 0f);
            //暗玻璃本体
            spriteBatch.Draw(tex, pos, null, bodyColor * Opacity, Rotation, origin, sc, SpriteEffects.None, 0f);
            //棱面爆闪
            if (glint > 0.05f) {
                spriteBatch.Draw(tex, pos, null, (GlintCol with { A = 0 }) * (glint * Opacity), Rotation
                    , origin, sc * new Vector2(0.6f, 1.05f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
