using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 湖畔村图的前景实绘。村落/鸟居/窗火全部由 KikasaScene.fx 的 villageRow 程序化承担,
    /// CPU 侧只画两件:恶犬(原版狼贴图 + KikasaHound.fx 湿墨材质,岸上实体 + 湖中倒影,
    /// 与世界侧 <see cref="KikasaHoundReflection"/> 同一副皮)和装裱卷杆。
    /// </summary>
    internal static class KikasaSceneRenderer
    {
        //缘光双色:血湖暗血 ⇄ 鬼雨冷青,与 KikasaHoundReflection 的 CoolTint 输入同源
        private static readonly Vector3 EdgeWarm = new Color(112, 26, 26).ToVector3();
        private static readonly Vector3 EdgeCool = new Color(42, 58, 66).ToVector3();

        /// <summary>
        /// 画中恶犬:站立帧承载垂首/昂首两态(差别在烬目),鬼梦换跃起帧仰头立嚎,权重交叠渐变;
        /// reflGate&gt;0 时在水线下画垂直镜像倒影(uMode=0,湿缝+折射+深处蚀散)。
        /// 烬目由着色器内建,着色器缺编回退近黑剪影
        /// </summary>
        /// <param name="pos">岸上犬中心(UI 空间)</param>
        /// <param name="height">目标身高(像素)</param>
        /// <param name="reflGate">倒影可见度 0~1(水位不够高时收 0,免得镜像探出画底)</param>
        /// <param name="waterPixY">水面屏幕 y,倒影镜轴</param>
        public static void DrawInkHound(SpriteBatch sb, Vector2 pos, float height,
            float idleA, float alertA, float howlA, float hoverLerp,
            float rain, float stir, float boil, float waterPixY, float reflGate,
            float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            Main.instance.LoadNPC(NPCID.Wolf);
            Texture2D tex = TextureAssets.Npc[NPCID.Wolf].Value;
            if (tex == null) {
                return;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.Wolf];
            //呼吸=极小幅整体缩放,沿用旧犬的读感
            float scale = height / (frameH - 2) * (1f + MathF.Sin(time * 1.1f) * 0.01f);

            //垂首/昂首共用站立身
            float standA = MathHelper.Clamp(idleA + alertA, 0f, 1f);
            float feetY = pos.Y + height * 0.5f;
            //犬背贴水线才有湿缝,离得远自然没有
            float seamGate = MathHelper.Clamp(1f - (waterPixY - feetY) / (height * 0.45f), 0f, 1f);

            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (hound == null || noise == null) {
                DrawFallback(sb, tex, frameH, pos, scale, standA, howlA,
                    waterPixY, feetY, height, reflGate, alpha);
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            hound.CurrentTechnique = hound.Techniques["TechHound"];
            hound.Parameters["uTime"]?.SetValue(time);
            hound.Parameters["uSeed"]?.SetValue(0.77f);
            hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            hound.Parameters["uEyeAnchor"]?.SetValue(KikasaHoundReflection.EyeAnchor);
            hound.Parameters["uDissolve"]?.SetValue(0f);
            hound.Parameters["uEdgeTint"]?.SetValue(Vector3.Lerp(EdgeWarm, EdgeCool, rain));

            float shoreWobble = 0.006f + boil * 0.03f;
            //岸上实体:烬目常留一点余光,被注视时转亮,鬼梦燃透
            DrawPose(sb, hound, tex, frameH, 3, pos, scale, 0f, standA * alpha,
                0.12f + alertA * 0.35f + hoverLerp * 0.25f, refl: false, seamGate: 0f, shoreWobble);
            DrawPose(sb, hound, tex, frameH, 10, pos, scale, 0.18f, howlA * alpha, 0.95f,
                refl: false, seamGate: 0f, shoreWobble);

            //湖中倒影:倒影醒着时,燃起来的是水里那双眼睛
            if (reflGate > 0.02f) {
                Vector2 reflPos = new(pos.X, 2f * waterPixY - feetY + height * 0.5f);
                float reflWobble = 0.012f + 0.020f * stir + 0.05f * boil;
                float reflA = alpha * reflGate * 0.92f;
                DrawPose(sb, hound, tex, frameH, 3, reflPos, scale, 0f, standA * reflA,
                    0.20f + alertA * 0.80f + hoverLerp * 0.20f, refl: true, seamGate, reflWobble);
                DrawPose(sb, hound, tex, frameH, 10, reflPos, scale, -0.18f, howlA * reflA, 0.85f,
                    refl: true, seamGate, reflWobble);
            }

            KikasaVaultRenderer.RestoreUIBatch(sb);
        }

        private static void DrawPose(SpriteBatch sb, Effect hound, Texture2D tex, int frameH,
            int frame, Vector2 pos, float scale, float rot, float alpha, float eyeGlow,
            bool refl, float seamGate, float wobble) {
            if (alpha <= 0.01f) {
                return;
            }
            //源矩形上下各内缩 1px + shader 帧界钳制,双通道防帧表渗色
            Rectangle src = new(0, frame * frameH + 1, tex.Width, frameH - 2);
            hound.Parameters["uUvRect"]?.SetValue(new Vector4(
                0f, src.Y / (float)tex.Height, 1f, src.Height / (float)tex.Height));
            hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)src.Height);
            hound.Parameters["uFlipH"]?.SetValue(0f);
            hound.Parameters["uFlipV"]?.SetValue(refl ? 1f : 0f);
            hound.Parameters["uMode"]?.SetValue(refl ? 0f : 1f);
            hound.Parameters["uSeamGate"]?.SetValue(refl ? seamGate : 0f);
            hound.Parameters["uWobble"]?.SetValue(wobble);
            hound.Parameters["uEyeGlow"]?.SetValue(eyeGlow);
            hound.CurrentTechnique.Passes[0].Apply();
            sb.Draw(tex, pos, src, Color.White * alpha, rot, src.Size() * 0.5f, scale,
                refl ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
        }

        //着色器缺编:近黑剪影 + 半透倒影

        private static void DrawFallback(SpriteBatch sb, Texture2D tex, int frameH, Vector2 pos,
            float scale, float standA, float howlA, float waterPixY, float feetY,
            float height, float reflGate, float alpha) {
            Color ink = new(12, 6, 9);
            void DrawOne(int frame, float w, float rot, Vector2 at, SpriteEffects fx, float mul) {
                if (w <= 0.01f) {
                    return;
                }
                Rectangle src = new(0, frame * frameH + 1, tex.Width, frameH - 2);
                sb.Draw(tex, at, src, ink * (alpha * w * mul), rot,
                    src.Size() * 0.5f, scale, fx, 0f);
            }
            DrawOne(3, standA, 0f, pos, SpriteEffects.None, 0.92f);
            DrawOne(10, howlA, 0.18f, pos, SpriteEffects.None, 0.92f);
            if (reflGate > 0.02f) {
                Vector2 reflPos = new(pos.X, 2f * waterPixY - feetY + height * 0.5f);
                DrawOne(3, standA, 0f, reflPos, SpriteEffects.FlipVertically, 0.45f * reflGate);
                DrawOne(10, howlA, -0.18f, reflPos, SpriteEffects.FlipVertically, 0.45f * reflGate);
            }
        }

        /// <summary>
        /// 装裱轴:横卷左右两根卷杆(暗杆 + 亮芯 + 上下轴头),
        /// 画开合时轴杆贴着画心两缘走
        /// </summary>
        public static void DrawRollers(SpriteBatch sb, Rectangle canvas,
            Color bar, Color core, float alpha) {
            foreach (float x in (Span<float>)[canvas.Left - 7f, canvas.Right + 7f]) {
                Vector2 top = new(x, canvas.Top - 9f);
                Vector2 bottom = new(x, canvas.Bottom + 9f);
                KikasaVaultRenderer.DrawLine(sb, top, bottom, 4.6f, bar * alpha);
                KikasaVaultRenderer.DrawLine(sb, top, bottom, 1.4f, core * (alpha * 0.55f));
                //轴头:两端一节短粗杆
                KikasaVaultRenderer.DrawLine(sb, top - new Vector2(0f, 6f), top,
                    7f, bar * alpha);
                KikasaVaultRenderer.DrawLine(sb, bottom, bottom + new Vector2(0f, 6f),
                    7f, bar * alpha);
            }
        }
    }
}
