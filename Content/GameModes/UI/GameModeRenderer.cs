using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.GameModes.UI
{
    /// <summary>
    /// 模式标签绘制：shader 旗身（<see cref="EffectLoader.GameModeTab"/>）+ CPU 矢量回退 + 切换演出大字。
    /// 另持余烬微粒池与越身扩张环、首见信标等亮色辉光件（暗层禁 magic-pixel 假羽化，亮线多 pass 合法）。
    /// 批处理契约照 TBUGRenderer.ShaderQuad：End → Immediate+effect → 画 quad → 恢复 Deferred
    /// </summary>
    internal static class GameModeRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle One = new(0, 0, 1, 1);

        internal static void DrawTab(SpriteBatch sb, Rectangle rect, GameModeFace face,
            float lit, float hover, float burst, bool burstOn, float disabled, float guide, float alpha) {
            if (alpha <= 0.01f || rect.Width < 4 || rect.Height < 4) {
                return;
            }

            Effect effect = EffectLoader.GameModeTab?.Value;
            if (effect == null) {
                DrawTabFallback(sb, rect, face, lit, disabled, alpha);
                return;
            }

            Color accent = GameModeTheme.Accent(face);
            Color ember = GameModeTheme.Ember(face);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uMode"]?.SetValue((float)face);
            effect.Parameters["uLit"]?.SetValue(lit);
            effect.Parameters["uHover"]?.SetValue(hover);
            effect.Parameters["uBurst"]?.SetValue(burst);
            effect.Parameters["uBurstOn"]?.SetValue(burstOn ? 1f : 0f);
            effect.Parameters["uDisabled"]?.SetValue(disabled);
            effect.Parameters["uGuide"]?.SetValue(guide);
            effect.Parameters["uAccent"]?.SetValue(accent.ToVector3());
            effect.Parameters["uEmber"]?.SetValue(ember.ToVector3());
            ShaderQuad(sb, effect, rect);
        }

        /// <summary>shader 缺编时的诚实矢量回退：漆底 + 边线 + 模式线稿</summary>
        private static void DrawTabFallback(SpriteBatch sb, Rectangle rect, GameModeFace face,
            float lit, float disabled, float alpha) {
            Color baseCol = GameModeTheme.NightBase * (0.94f * alpha);
            sb.Draw(Pixel, rect, One, baseCol);

            Color iconCol = Color.Lerp(GameModeTheme.BoneDim, GameModeTheme.Accent(face), lit);
            iconCol = Color.Lerp(iconCol, Color.Gray * 0.6f, disabled) * alpha;
            Color rim = iconCol * 0.8f;

            //1px 边
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), One, rim);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), One, rim * 0.6f);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), One, rim * 0.7f);
            sb.Draw(Pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), One, rim * 0.7f);

            Vector2 c = rect.Center.ToVector2();
            float s = rect.Width * 0.30f;
            if (face == GameModeFace.Brutal) {
                //三道斜痕
                Vector2 dir = new Vector2(-0.46f, 0.89f) * s;
                Vector2 perp = new Vector2(-dir.Y, dir.X) / s * (s * 0.42f);
                for (int i = -1; i <= 1; i++) {
                    DrawLine(sb, c - dir + perp * i, c + dir + perp * i, 2f, iconCol);
                }
            }
            else if (face == GameModeFace.Asura) {
                //环 + 三棱的线稿近似
                const int seg = 20;
                float r = s * 0.9f;
                Vector2 prev = c + new Vector2(r, 0f);
                for (int i = 1; i <= seg; i++) {
                    float ang = MathHelper.TwoPi * i / seg;
                    Vector2 next = c + ang.ToRotationVector2() * r;
                    DrawLine(sb, prev, next, 2f, iconCol);
                    prev = next;
                }
                for (int i = 0; i < 3; i++) {
                    float ang = -MathHelper.PiOver2 + MathHelper.TwoPi * i / 3f;
                    Vector2 d = ang.ToRotationVector2();
                    DrawLine(sb, c + d * r, c + d * (r + s * 0.55f), 2f, iconCol);
                }
            }
            else if (face == GameModeFace.Annihilation) {
                //镰月线稿近似：外弧 + 一粒坠星
                const int seg = 14;
                float r = s * 0.95f;
                Vector2 prev = c + (-MathHelper.PiOver2 * 1.4f).ToRotationVector2() * r;
                for (int i = 1; i <= seg; i++) {
                    float ang = MathHelper.Lerp(-MathHelper.PiOver2 * 1.4f, MathHelper.PiOver2 * 1.4f, i / (float)seg);
                    Vector2 next = c + ang.ToRotationVector2() * r;
                    DrawLine(sb, prev, next, 2.5f, iconCol);
                    prev = next;
                }
                sb.Draw(Pixel, new Rectangle((int)(c.X + s * 0.55f) - 2, (int)(c.Y - s * 0.8f) - 2, 4, 4), One, iconCol);
            }
            else {
                //神匠线稿近似：砧台横线 + 砧脚 + 斜握锤（锤柄一线、锤头一短粗线）
                float aw = s * 1.2f;
                Vector2 anvilY = new(0f, s * 0.55f);
                DrawLine(sb, c + anvilY - new Vector2(aw, 0f), c + anvilY + new Vector2(aw, 0f), 3f, iconCol);
                DrawLine(sb, c + anvilY + new Vector2(-aw * 0.4f, 0f), c + anvilY + new Vector2(-aw * 0.25f, s * 0.5f), 2f, iconCol * 0.8f);
                DrawLine(sb, c + anvilY + new Vector2(aw * 0.4f, 0f), c + anvilY + new Vector2(aw * 0.25f, s * 0.5f), 2f, iconCol * 0.8f);
                Vector2 grip = c + new Vector2(s * 0.9f, -s * 0.1f);
                Vector2 head = c + new Vector2(-s * 0.35f, -s * 0.75f);
                DrawLine(sb, grip, head, 2f, iconCol);
                Vector2 headDir = (head - grip).SafeNormalize(Vector2.UnitX);
                Vector2 headPerp = new(-headDir.Y, headDir.X);
                DrawLine(sb, head - headPerp * (s * 0.42f), head + headPerp * (s * 0.42f), 4.5f, iconCol);
            }
        }

        private static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 delta = end - start;
            float len = delta.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(Pixel, start, One, color, delta.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(len, thickness), SpriteEffects.None, 0f);
        }

        private static void ShaderQuad(SpriteBatch sb, Effect effect, Rectangle dest) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(Pixel, dest, One, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        //——余烬微粒池：点亮标签的顶缘上浮 + 切换爆发喷洒 + 引导确认撒粒（全亮色，A=0 加法光晕）——

        private struct Mote
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Scale;
            public Color Col;
        }

        private static readonly Mote[] motes = new Mote[96];
        private static int moteCursor;

        internal static void ClearMotes() {
            for (int i = 0; i < motes.Length; i++) {
                motes[i].Life = 0f;
            }
        }

        private static void Emit(Vector2 pos, Vector2 vel, float life, float scale, Color col) {
            ref Mote m = ref motes[moteCursor];
            moteCursor = (moteCursor + 1) % motes.Length;
            m.Pos = pos;
            m.Vel = vel;
            m.MaxLife = m.Life = life;
            m.Scale = scale;
            m.Col = col;
        }

        /// <summary>点亮标签顶缘偶发的一粒余烬，缓缓上浮</summary>
        internal static void EmitIdleMote(Rectangle tab, GameModeFace face) {
            Vector2 pos = new(tab.X + Main.rand.NextFloat(4f, tab.Width - 4f),
                tab.Y + Main.rand.NextFloat(2f, 10f));
            Vector2 vel = new(Main.rand.NextFloat(-0.10f, 0.10f), -Main.rand.NextFloat(0.16f, 0.40f));
            Color col = Color.Lerp(GameModeTheme.Accent(face), GameModeTheme.Ember(face), Main.rand.NextFloat());
            Emit(pos, vel, Main.rand.NextFloat(46f, 84f), Main.rand.NextFloat(0.5f, 1f), col);
        }

        /// <summary>切换爆发的径向喷洒：开启烈而亮，关闭少而沉（余烬泄落）</summary>
        internal static void EmitBurst(Rectangle tab, GameModeFace face, bool on) {
            Vector2 c = tab.Center.ToVector2();
            int count = on ? 16 : 9;
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.25f, 0.25f);
                float spd = on ? Main.rand.NextFloat(1.6f, 3.4f) : Main.rand.NextFloat(0.8f, 1.8f);
                Vector2 vel = ang.ToRotationVector2() * spd;
                if (!on) {
                    vel.Y += 0.6f;
                }
                Color col = Color.Lerp(GameModeTheme.Accent(face), GameModeTheme.Ember(face),
                    on ? Main.rand.NextFloat(0.35f, 1f) : Main.rand.NextFloat(0.4f));
                Emit(c, vel, Main.rand.NextFloat(26f, 52f), Main.rand.NextFloat(0.6f, 1.15f), col);
            }
        }

        /// <summary>首见确认时自旗心散出的一小撮暖粒</summary>
        internal static void EmitAckMotes(Rectangle tab) {
            Vector2 c = tab.Center.ToVector2();
            Color warm = Color.Lerp(GameModeTheme.BrutalEmber, Color.White, 0.4f);
            for (int i = 0; i < 10; i++) {
                float ang = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(-0.3f, 0.3f);
                Emit(c, ang.ToRotationVector2() * Main.rand.NextFloat(0.9f, 2.0f),
                    Main.rand.NextFloat(28f, 46f), Main.rand.NextFloat(0.45f, 0.85f), warm);
            }
        }

        /// <summary>推进微粒池：标签不可见时清空（微粒锚定在背包面板上）</summary>
        internal static void UpdateMotes(bool visible) {
            if (!visible) {
                ClearMotes();
                return;
            }
            for (int i = 0; i < motes.Length; i++) {
                ref Mote m = ref motes[i];
                if (m.Life <= 0f) {
                    continue;
                }
                m.Life--;
                m.Pos += m.Vel;
                m.Vel *= 0.955f;
                m.Vel.Y -= 0.012f;
            }
        }

        internal static void DrawMotes(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            for (int i = 0; i < motes.Length; i++) {
                ref Mote m = ref motes[i];
                if (m.Life <= 0f) {
                    continue;
                }
                float age = 1f - m.Life / m.MaxLife;
                float a = MathF.Sin(age * MathF.PI);
                //亮芯
                sb.Draw(Pixel, m.Pos, One, m.Col * (a * 0.9f), 0f,
                    new Vector2(0.5f), new Vector2(2f * m.Scale), SpriteEffects.None, 0f);
                //光晕（A=0 加法）
                if (glow != null) {
                    Color halo = new Color(m.Col.R, m.Col.G, m.Col.B, (byte)0) * (a * 0.5f);
                    float scale = 15f * m.Scale / glow.Width;
                    sb.Draw(glow, m.Pos, null, halo, 0f, glow.Size() / 2f, scale, SpriteEffects.None, 0f);
                }
            }
        }

        //——越身扩张环与首见信标（细亮环双 pass 叠辉光；亮色合法，暗层禁羽化）——

        /// <summary>切换爆发的越身扩张环：起拍中心闪 + 冲出旗身的环</summary>
        internal static void DrawBurstRing(SpriteBatch sb, Rectangle tab, GameModeFace face, float burst, bool on) {
            float t = MathHelper.Clamp(burst, 0f, 1f);
            if (t >= 1f) {
                return;
            }
            Vector2 c = tab.Center.ToVector2();
            float inv = 1f - t;
            float ease = 1f - inv * inv * inv;
            float radius = MathHelper.Lerp(tab.Width * 0.42f, tab.Width * 2.1f, ease);
            float fade = inv * (on ? 0.85f : 0.5f);
            Color col = Color.Lerp(GameModeTheme.Accent(face), GameModeTheme.Ember(face), 0.5f);
            DrawRingPasses(sb, c, radius, col, fade);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && t < 0.35f) {
                float flash = 1f - t / 0.35f;
                Color halo = new Color(col.R, col.G, col.B, (byte)0) * (flash * (on ? 0.8f : 0.45f));
                float scale = tab.Width * 2.4f / glow.Width * (0.6f + 0.4f * ease);
                sb.Draw(glow, c, null, halo, 0f, glow.Size() / 2f, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>首见信标：呼吸暖光环 + 周期扩散细环（ringT 小于 0 表示本拍无环）</summary>
        internal static void DrawGuideBeacon(SpriteBatch sb, Rectangle tab, float ringT, float level) {
            Vector2 c = tab.Center.ToVector2();
            Color warm = Color.Lerp(GameModeTheme.BrutalEmber, Color.White, 0.30f);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color halo = new Color(warm.R, warm.G, warm.B, (byte)0) * (0.20f + 0.28f * level);
                float scale = tab.Height * 2.6f / glow.Height;
                sb.Draw(glow, c, null, halo, 0f, glow.Size() / 2f, scale, SpriteEffects.None, 0f);
            }

            if (ringT >= 0f && ringT <= 1f) {
                float ease = 1f - (1f - ringT) * (1f - ringT);
                float halfDiag = new Vector2(tab.Width, tab.Height).Length() * 0.5f;
                float radius = MathHelper.Lerp(halfDiag * 0.85f, halfDiag * 2.3f, ease);
                DrawRingPasses(sb, c, radius, warm, (1f - ringT) * 0.6f);
            }
        }

        /// <summary>首见确认收束：光环向旗身收拢 + 中心一记暖闪，ack 1→0</summary>
        internal static void DrawGuideAck(SpriteBatch sb, Rectangle tab, float ack) {
            Vector2 c = tab.Center.ToVector2();
            Color warm = Color.Lerp(GameModeTheme.BrutalEmber, Color.White, 0.45f);
            float halfDiag = new Vector2(tab.Width, tab.Height).Length() * 0.5f;
            float radius = MathHelper.Lerp(halfDiag * 0.7f, halfDiag * 1.9f, ack * ack);
            DrawRingPasses(sb, c, radius, warm, ack * 0.8f);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color halo = new Color(warm.R, warm.G, warm.B, (byte)0) * (ack * 0.55f);
                float scale = tab.Height * 2.2f / glow.Height * (0.5f + 0.5f * ack);
                sb.Draw(glow, c, null, halo, 0f, glow.Size() / 2f, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>细亮环 + 宽淡环两 pass 叠出辉光（亮色专用配方）</summary>
        private static void DrawRingPasses(SpriteBatch sb, Vector2 c, float radius, Color col, float alpha) {
            if (alpha <= 0.01f || radius < 2f) {
                return;
            }
            DrawRing(sb, c, radius, 2f, col * (alpha * 0.95f));
            DrawRing(sb, c, radius, 5f, col * (alpha * 0.28f));
        }

        private static void DrawRing(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color) {
            const int seg = 40;
            Vector2 prev = center + new Vector2(radius, 0f);
            for (int i = 1; i <= seg; i++) {
                float ang = MathHelper.TwoPi * i / seg;
                Vector2 next = center + ang.ToRotationVector2() * radius;
                DrawLine(sb, prev, next, thickness, color);
                prev = next;
            }
        }

        /// <summary>
        /// 切换演出：屏幕上三分之一处的大字 + 背景横幅（<see cref="EffectLoader.GameModeBanner"/>）
        /// + 起拍加法重影 + 字下扫开的主色细线。横幅缺编时退回纯文字演出
        /// </summary>
        internal static void DrawCeremonyLine(SpriteBatch sb) {
            if (!GameModeCeremony.LineActive) {
                return;
            }

            float t = GameModeCeremony.LineProgress;
            float aIn = MathHelper.SmoothStep(0f, 1f, Math.Clamp(t / 0.10f, 0f, 1f));
            float aOut = MathHelper.SmoothStep(0f, 1f, Math.Clamp((1f - t) / 0.22f, 0f, 1f));
            float a = aIn * aOut;
            if (a <= 0.01f) {
                return;
            }

            GameModeFace face = GameModeCeremony.LineFace;
            bool enabled = GameModeCeremony.LineEnabled;
            string text = GameModeText.ToggleLine(face, enabled).Value;
            var font = FontAssets.DeathText.Value;
            Vector2 size = font.MeasureString(text);

            //入场落座：轻微过冲的 scale punch，随后钳到屏宽
            float settle = Math.Clamp(t / 0.18f, 0f, 1f);
            float scale = 0.80f + 0.20f * EaseOutBack(settle);
            float maxW = GameModeTheme.UIScreenW * 0.86f;
            if (size.X * scale > maxW) {
                scale = maxW / size.X;
            }

            Vector2 pos = new(GameModeTheme.UIScreenW * 0.5f, GameModeTheme.UIScreenH * 0.30f - t * 16f);
            Color accent = GameModeTheme.Accent(face);
            Color textCol = Color.Lerp(accent, GameModeTheme.BoneDim, enabled ? 0f : 0.4f);

            //背景横幅先落，大字压上
            float bandH = Math.Max(130f, size.Y * scale + 76f);
            var band = new Rectangle(0, (int)(pos.Y - bandH * 0.5f),
                (int)GameModeTheme.UIScreenW, (int)bandH);
            DrawCeremonyBanner(sb, band, face, enabled, t, a);

            Utils.DrawBorderStringBig(sb, text, pos, textCol * a, scale, 0.5f, 0.5f);

            //起拍加法重影：黑描边在 A=0 下归零，只剩主色光字浮出
            float ghost = 1f - Math.Clamp(t / 0.22f, 0f, 1f);
            if (ghost > 0.01f) {
                Color ghostCol = new Color(accent.R, accent.G, accent.B, (byte)0) * (a * ghost * 0.45f);
                Utils.DrawBorderStringBig(sb, text, pos, ghostCol, scale * (1f + ghost * 0.10f), 0.5f, 0.5f);
            }

            //字下细线随进度扫开
            float ruleT = MathHelper.SmoothStep(0f, 1f, Math.Clamp(t / 0.32f, 0f, 1f));
            int ruleW = (int)(size.X * scale * ruleT);
            if (ruleW > 2) {
                var rule = new Rectangle((int)(pos.X - ruleW / 2f),
                    (int)(pos.Y + size.Y * scale * 0.5f + 6f), ruleW, 2);
                sb.Draw(Pixel, rule, One, accent * (a * 0.8f));
            }
        }

        /// <summary>演出横幅 quad：s1 绑 Perlin 噪声；shader 或噪声缺编时静默跳过</summary>
        private static void DrawCeremonyBanner(SpriteBatch sb, Rectangle band, GameModeFace face,
            bool enabled, float t, float alpha) {
            Effect effect = EffectLoader.GameModeBanner?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uReveal"]?.SetValue(MathHelper.SmoothStep(0f, 1f, Math.Clamp(t / 0.26f, 0f, 1f)));
            effect.Parameters["uMode"]?.SetValue((float)face);
            effect.Parameters["uEnabled"]?.SetValue(enabled ? 1f : 0f);
            effect.Parameters["uAccent"]?.SetValue(GameModeTheme.Accent(face).ToVector3());
            effect.Parameters["uEmber"]?.SetValue(GameModeTheme.Ember(face).ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(Pixel, band, One, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>轻微过冲落座的缓动</summary>
        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
