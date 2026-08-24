using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaWisps;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI.Panorama
{
    /// <summary>
    /// 湖心景过程化绘制：全屏背景走 KikasaPanorama.fx（缺编时 CPU 三段平涂回退），
    /// 恶犬沿用 KikasaHound.fx 湿墨材质（与世界侧倒影同一副皮），
    /// 金焰全程序化（鬼火金三色），水脉/湖力条/虚座是共用小笔
    /// </summary>
    internal static class KikasaPanoramaRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;

        //缘光双色:血湖暗血 ⇄ 鬼雨冷青,与 KikasaHoundReflection 的 CoolTint 输入同源
        private static readonly Vector3 EdgeWarm = new Color(112, 26, 26).ToVector3();
        private static readonly Vector3 EdgeCool = new Color(42, 58, 66).ToVector3();

        //==================== 全屏背景 ====================

        /// <summary>
        /// 背景：夜空雨幕 + 左岸礁 + 血湖水体。waterUv 是当前水线（涨落即状态），
        /// dry 驱动干湖龟裂，vigor 喂水面辉光（湖力裁撤后调用端恒传 1），
        /// wisp 是鬼火燃势（水线金焰带），lightGate 是湖藏填充率（水中烬萤稠度）
        /// </summary>
        public static void DrawBackdrop(SpriteBatch sb, Rectangle rect, float alpha,
            float rain, float waterUv, float dry, float stir, float vigor,
            float wisp, float lightGate) {
            Effect effect = EffectLoader.KikasaPanorama?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                DrawBackdropCPU(sb, rect, alpha, rain, waterUv, dry);
                return;
            }
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uWaterY"]?.SetValue(waterUv);
            effect.Parameters["uDry"]?.SetValue(MathHelper.Clamp(dry, 0f, 1f));
            effect.Parameters["uRain"]?.SetValue(MathHelper.Clamp(rain, 0f, 1f));
            effect.Parameters["uStir"]?.SetValue(MathHelper.Clamp(stir, 0f, 1f));
            effect.Parameters["uVigor"]?.SetValue(MathHelper.Clamp(vigor, 0f, 1f));
            effect.Parameters["uWisp"]?.SetValue(MathHelper.Clamp(wisp, 0f, 1f));
            effect.Parameters["uLightGate"]?.SetValue(MathHelper.Clamp(lightGate, 0f, 1f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(Pixel, rect, Color.White);
            KikasaVaultRenderer.RestoreUIBatch(sb);
        }

        //CPU 回退：天/水（或干床）三段平涂 + 岸线水线各一划，不做假羽化

        private static void DrawBackdropCPU(SpriteBatch sb, Rectangle rect, float alpha,
            float rain, float waterUv, float dry) {
            Texture2D px = Pixel;
            int waterY = rect.Y + (int)(rect.Height * MathHelper.Clamp(waterUv, 0f, 1f));

            //天空双带：上深下浅的手糊渐变
            int skyMid = rect.Y + (int)(rect.Height * 0.22f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, skyMid - rect.Y),
                KikasaHudTheme.Void(rain) * (0.96f * alpha));
            sb.Draw(px, new Rectangle(rect.X, skyMid, rect.Width, waterY - skyMid),
                KikasaHudTheme.Mid(rain) * (0.88f * alpha));

            //水体或干床
            Color body = Color.Lerp(KikasaHudTheme.Deep(rain), KikasaHudTheme.Void(rain), dry);
            sb.Draw(px, new Rectangle(rect.X, waterY, rect.Width, rect.Bottom - waterY),
                body * (0.94f * alpha));
            KikasaVaultRenderer.DrawLine(sb, new Vector2(rect.X + 3, waterY),
                new Vector2(rect.Right - 3, waterY), 1.5f,
                KikasaHudTheme.Glow(rain) * ((0.5f - dry * 0.3f) * alpha));

            //左岸礁：恶犬的立足处
            float shoreY = rect.Y + rect.Height * KikasaPanoramaTheme.WaterFullUv;
            sb.Draw(px, new Rectangle(rect.X, (int)(shoreY - 6f),
                (int)(rect.Width * 0.24f), 16), KikasaHudTheme.Void(rain) * (0.92f * alpha));
        }

        //==================== 恶犬（鬼梦之鬼，湿墨实绘） ====================

        /// <summary>
        /// 恶犬：站立帧承载垂首/昂首两态(差别在烬目)，鬼梦换跃起帧仰头立嚎，权重交叠渐变；
        /// reflGate&gt;0 时在水线下画垂直镜像倒影(uMode=0)。烬目着色器内建，缺编回退近黑剪影
        /// </summary>
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
            //呼吸=极小幅整体缩放
            float scale = height / (frameH - 2) * (1f + MathF.Sin(time * 1.1f) * 0.01f);

            float standA = MathHelper.Clamp(idleA + alertA, 0f, 1f);
            float feetY = pos.Y + height * 0.5f;
            //犬背贴水线才有湿缝，离得远自然没有
            float seamGate = MathHelper.Clamp(1f - (waterPixY - feetY) / (height * 0.45f), 0f, 1f);

            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (hound == null || noise == null) {
                DrawHoundFallback(sb, tex, frameH, pos, scale, standA, howlA,
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
            //岸上实体：烬目常留一点余光，被注视时转亮，鬼梦燃透
            DrawHoundPose(sb, hound, tex, frameH, 3, pos, scale, 0f, standA * alpha,
                0.12f + alertA * 0.35f + hoverLerp * 0.25f, refl: false, seamGate: 0f, shoreWobble);
            DrawHoundPose(sb, hound, tex, frameH, 10, pos, scale, 0.18f, howlA * alpha, 0.95f,
                refl: false, seamGate: 0f, shoreWobble);

            //湖中倒影：倒影醒着时，燃起来的是水里那双眼睛
            if (reflGate > 0.02f) {
                Vector2 reflPos = new(pos.X, 2f * waterPixY - feetY + height * 0.5f);
                float reflWobble = 0.012f + 0.020f * stir + 0.05f * boil;
                float reflA = alpha * reflGate * 0.92f;
                DrawHoundPose(sb, hound, tex, frameH, 3, reflPos, scale, 0f, standA * reflA,
                    0.20f + alertA * 0.80f + hoverLerp * 0.20f, refl: true, seamGate, reflWobble);
                DrawHoundPose(sb, hound, tex, frameH, 10, reflPos, scale, -0.18f, howlA * reflA, 0.85f,
                    refl: true, seamGate, reflWobble);
            }

            KikasaVaultRenderer.RestoreUIBatch(sb);
        }

        private static void DrawHoundPose(SpriteBatch sb, Effect hound, Texture2D tex, int frameH,
            int frame, Vector2 pos, float scale, float rot, float alpha, float eyeGlow,
            bool refl, float seamGate, float wobble) {
            if (alpha <= 0.01f) {
                return;
            }
            //源矩形上下各内缩 1px + shader 帧界钳制，双通道防帧表渗色
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

        //着色器缺编：近黑剪影 + 半透倒影

        private static void DrawHoundFallback(SpriteBatch sb, Texture2D tex, int frameH, Vector2 pos,
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
                DrawOne(10, howlA, 0.18f, reflPos, SpriteEffects.FlipVertically, 0.45f * reflGate);
            }
        }

        //==================== 金焰（鬼火之鬼，全程序化） ====================

        /// <summary>
        /// 金焰：燃着时是一柱摇曳的金火（芯白金/身金/舌琥珀），熄着只剩一粒苍金余烬；
        /// 被雨压制向苍金失温。加色批自管，画完复原
        /// </summary>
        public static void DrawWispGhost(SpriteBatch sb, Vector2 pos, float burn,
            float quench, float rain, float hover, float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            float t = MathHelper.Clamp(burn, 0f, 1f);
            //压制中失温：三色一起拉向苍金
            Color core = Color.Lerp(KikasaWisp.GoldCore, KikasaWisp.PaleDying, quench);
            Color body = Color.Lerp(KikasaWisp.GoldBody, KikasaWisp.PaleDying, quench);
            Color tip = Color.Lerp(KikasaWisp.AmberTip, KikasaWisp.PaleDying, quench);
            core = KikasaWisp.Tint(core);
            body = KikasaWisp.Tint(body);
            tip = KikasaWisp.Tint(tip);

            KikasaVaultRenderer.BeginAdditive(sb);

            if (t < 0.05f) {
                //熄着：一粒余烬压着呼吸，悬停亮一拍，它还在等湖力回满
                float breath = KikasaPanoramaTheme.Breath(time, 4.1f, 1.8f);
                KikasaVaultRenderer.DrawGlowDot(sb, pos + new Vector2(0f, 22f), 7f,
                    KikasaWisp.Tint(KikasaWisp.PaleDying) * ((0.30f + breath * 0.20f + hover * 0.25f) * alpha));
                KikasaVaultRenderer.RestoreUIBatch(sb);
                return;
            }

            //火柱：三节glow自下而上收窄，横摆相位错开
            float height = 64f * t;
            for (int i = 0; i < 3; i++) {
                float k = i / 2f;
                float sway = MathF.Sin(time * (3.1f + i * 0.9f) + i * 1.7f) * (4.5f - i * 1.2f) * t;
                Vector2 at = pos + new Vector2(sway, 22f - height * (0.22f + k * 0.62f));
                float r = MathHelper.Lerp(20f, 8f, k) * (0.8f + t * 0.3f);
                Color c = Color.Lerp(body, tip, k);
                KikasaVaultRenderer.DrawGlowDot(sb, at, r, c * ((0.42f - k * 0.10f + hover * 0.10f) * t * alpha));
            }
            //焰芯
            float coreFlick = 0.85f + 0.15f * MathF.Sin(time * 7.3f);
            KikasaVaultRenderer.DrawGlowDot(sb, pos + new Vector2(0f, 22f - height * 0.28f),
                9f * coreFlick, core * (0.75f * t * alpha));
            //火舌上飘的两粒烬星
            for (int i = 0; i < 2; i++) {
                float p = (time * (0.5f + i * 0.23f) + i * 0.5f) % 1f;
                Vector2 at = pos + new Vector2(
                    MathF.Sin(time * 2.2f + i * 3.1f) * 7f, 22f - height * 0.6f - p * 34f);
                KikasaVaultRenderer.DrawGlowDot(sb, at, 2.6f, tip * (0.5f * (1f - p) * t * alpha));
            }
            //水面上的映光
            KikasaVaultRenderer.DrawGlowDot(sb, pos + new Vector2(0f, 44f), 16f,
                body * (0.16f * t * alpha));

            KikasaVaultRenderer.RestoreUIBatch(sb);
        }

        //==================== 湿纸卡底 ====================

        /// <summary>
        /// 湿纸卡底（KikasaScene.fx TechCard）；缺编回退平底 + 边线。
        /// 引导卡走这个入口（自湖畔村图退役时迁来，配方不变）
        /// </summary>
        public static void DrawCardBg(SpriteBatch sb, Rectangle card, float alpha, float rain) {
            Effect effect = EffectLoader.KikasaScene?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect != null && noise != null && effect.Techniques["TechCard"] != null) {
                Rectangle ext = card;
                ext.Inflate(8, 8);
                effect.CurrentTechnique = effect.Techniques["TechCard"];
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uAlpha"]?.SetValue(alpha * 0.96f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(ext.Width, ext.Height));
                effect.Parameters["uTear"]?.SetValue(alpha);
                effect.Parameters["uRain"]?.SetValue(rain);
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                sb.Draw(Pixel, ext, Color.White);
                KikasaVaultRenderer.RestoreUIBatch(sb);
            }
            else {
                sb.Draw(Pixel, card, KikasaHudTheme.Void(rain) * (0.9f * alpha));
                Color edgeC = KikasaHudTheme.Accent(rain) * (0.5f * alpha);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Left, card.Top),
                    new Vector2(card.Right, card.Top), 1f, edgeC);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Left, card.Bottom),
                    new Vector2(card.Right, card.Bottom), 1f, edgeC * 0.7f);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Left, card.Top),
                    new Vector2(card.Left, card.Bottom), 1f, edgeC * 0.85f);
                KikasaVaultRenderer.DrawLine(sb, new Vector2(card.Right, card.Top),
                    new Vector2(card.Right, card.Bottom), 1f, edgeC * 0.85f);
            }
        }

        //==================== 水脉与虚座 ====================

        /// <summary>
        /// 水脉：两点之间随水晃的亮线（组合边的语言）。dashed 时走断续虚线（将成边预演）
        /// </summary>
        public static void DrawWaterVein(SpriteBatch sb, Vector2 from, Vector2 to,
            float width, Color color, float time, float sag = 14f, bool dashed = false) {
            Vector2 d = to - from;
            float len = d.Length();
            if (len < 4f) {
                return;
            }
            int segs = Math.Max(8, (int)(len / 14f));
            Vector2 prev = from;
            for (int i = 1; i <= segs; i++) {
                float t = i / (float)segs;
                //中段下垂 + 波动：水里的线不会绷直
                float sagY = MathF.Sin(t * MathHelper.Pi) * sag;
                float wob = MathF.Sin(t * 9f + time * 2.1f) * 1.8f;
                Vector2 cur = from + d * t + new Vector2(0f, sagY + wob);
                if (!dashed || i % 3 != 0) {
                    KikasaVaultRenderer.DrawLine(sb, prev, cur, width, color);
                }
                prev = cur;
            }
        }

        /// <summary>虚座：断续的小圆环（空席/未沉之影），悬停亮起</summary>
        public static void DrawDashedSocket(SpriteBatch sb, Vector2 pos, float radius,
            Color color, float time, float spin = 0.3f) {
            const int dashes = 9;
            for (int d = 0; d < dashes; d++) {
                float a0 = MathHelper.TwoPi * d / dashes + time * spin;
                float a1 = a0 + MathHelper.TwoPi / dashes * 0.55f;
                Vector2 prev = pos + a0.ToRotationVector2() * radius;
                const int segs = 3;
                for (int i = 1; i <= segs; i++) {
                    float t = MathHelper.Lerp(a0, a1, i / (float)segs);
                    Vector2 cur = pos + t.ToRotationVector2() * radius;
                    KikasaVaultRenderer.DrawLine(sb, prev, cur, 1.1f, color);
                    prev = cur;
                }
            }
        }

        //==================== 祈雨绳 ====================

        //麻绳双色:血湖暗褐 ⇄ 鬼雨湿灰,上缘一线受月光
        private static readonly Color RopeWarm = new(74, 46, 44);
        private static readonly Color RopeCool = new(52, 62, 68);

        /// <summary>
        /// 祈雨绳：两锚点间的下垂麻绳，随夜风微摆；雨态绳身浸湿微亮。
        /// 静态垂弧几何与 <see cref="KikasaPanoramaTheme.TalisRopePoint"/> 同源，
        /// 风摆只加在绘制上不进命中
        /// </summary>
        public static void DrawTalisRope(SpriteBatch sb, float rain, float alpha, float time) {
            Vector2 l = KikasaPanoramaTheme.TalisRopeLeft;
            Vector2 r = KikasaPanoramaTheme.TalisRopeRight;
            Color cord = Color.Lerp(RopeWarm, RopeCool, rain);
            Color lit = Color.Lerp(cord, Color.White, 0.22f + rain * 0.12f);

            const int segs = 26;
            Vector2 prev = KikasaPanoramaTheme.TalisRopePoint(0f);
            for (int i = 1; i <= segs; i++) {
                float u = i / (float)segs;
                float wind = MathF.Sin(u * 5.2f + time * 1.15f) * (1.5f + rain * 1.2f)
                    * MathF.Sin(u * MathHelper.Pi);
                Vector2 cur = KikasaPanoramaTheme.TalisRopePoint(u) + new Vector2(0f, wind);
                KikasaVaultRenderer.DrawLine(sb, prev, cur, 2.1f, cord * (0.85f * alpha));
                KikasaVaultRenderer.DrawLine(sb, prev + new Vector2(0f, -0.9f),
                    cur + new Vector2(0f, -0.9f), 0.9f, lit * (0.5f * alpha));
                prev = cur;
            }
            //两端绳结:一粒沉色小方
            Rectangle src = new(0, 0, 1, 1);
            foreach (Vector2 anchor in stackalloc[] { l, r }) {
                sb.Draw(Pixel, anchor, src, cord * alpha, MathHelper.PiOver4,
                    new Vector2(0.5f), new Vector2(4.6f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>符位吊线：绳点垂到符顶，随符同摆</summary>
        public static void DrawTalisCord(SpriteBatch sb, Vector2 ropePoint, Vector2 stripTop,
            float rain, float alpha) {
            Color cord = Color.Lerp(RopeWarm, RopeCool, rain);
            KikasaVaultRenderer.DrawLine(sb, ropePoint, stripTop, 1.3f, cord * (0.9f * alpha));
        }

        /// <summary>空绳位：吊线短垂 + 一枚断续绳环，等符来挂</summary>
        public static void DrawEmptyTalisSlot(SpriteBatch sb, Vector2 ropePoint,
            Color color, float alpha, float time) {
            Vector2 knot = ropePoint + new Vector2(0f, KikasaPanoramaTheme.TalisCordLen * 0.7f);
            DrawTalisCord(sb, ropePoint, knot, 0f, alpha * 0.7f);
            DrawDashedSocket(sb, knot + new Vector2(0f, 7f), 6.5f, color * alpha, time, 0.22f);
        }
    }
}
