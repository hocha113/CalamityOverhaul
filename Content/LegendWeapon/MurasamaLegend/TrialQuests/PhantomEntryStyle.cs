using CalamityOverhaul.Content.ADV.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.TrialQuests
{
    /// <summary>
    /// 鬼妖村正委托条目，MGR:R赤红血狂主题<br/>
    /// GPU shader背景+CPU锐利HUD矢量，黑曜底猩红扫光血雾
    /// </summary>
    internal class PhantomEntryStyle : IEntrustEntryStyle
    {
        #region 色板

        //黑曜+血红基调(纯CPU降级路径用)
        private static readonly Color BgVoid = new(7, 3, 4);
        private static readonly Color BgDeep = new(18, 6, 9);
        private static readonly Color BgMid = new(32, 10, 14);
        private static readonly Color BgHover = new(48, 14, 20);
        private static readonly Color BgSelected = new(64, 18, 24);

        //MGR:R赤红色板
        private static readonly Color CrimsonDeep = new(140, 18, 26);
        private static readonly Color CrimsonMid = new(195, 35, 45);
        private static readonly Color CrimsonBright = new(240, 70, 80);
        private static readonly Color BloodFlash = new(255, 130, 110);
        private static readonly Color ScanlineRed = new(180, 40, 50);

        //状态色
        private static readonly Color BladeMode = new(255, 60, 70);  //追踪态:Blade Mode红
        private static readonly Color CompletedSilver = new(220, 215, 220);  //完成态:刀刃银白
        private static readonly Color FailedDark = new(80, 8, 12);
        private static readonly Color SuspendedAsh = new(110, 90, 95);

        //文字
        private static readonly Color TitleBlade = new(245, 220, 220);
        private static readonly Color TitleComplete = new(220, 220, 225);
        private static readonly Color TitleFailed = new(180, 60, 65);

        #endregion

        private float scanTime;
        private float pulseTime;
        private float dataFlowTime;
        private float shaderTime;

        public void Update() {
            scanTime += 0.038f;
            pulseTime += 0.022f;
            dataFlowTime += 0.06f;
            shaderTime += 0.016f;
            const float wrap = MathHelper.TwoPi * 4f;
            if (scanTime > wrap) scanTime -= wrap;
            if (pulseTime > wrap) pulseTime -= wrap;
            if (dataFlowTime > 100f) dataFlowTime -= 100f;
            if (shaderTime > 10000f) shaderTime -= 10000f;
        }

        public bool DrawEntryBackground(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha) {
            float intensity = 0.35f;
            if (isHovered) intensity = 0.65f;
            if (isSelected) intensity = 0.85f;
            if (entry.Status == QuestEntryStatus.Tracked) intensity = MathF.Max(intensity, 0.95f);
            if (entry.Status == QuestEntryStatus.Failed) intensity *= 0.55f;
            if (entry.Status == QuestEntryStatus.Suspended) intensity *= 0.40f;

            Color accent = GetAccentColor(entry.Status, 1f);
            float pulse01 = MathF.Sin(pulseTime * 2.2f) * 0.5f + 0.5f;

            //GPU着色器路径
            if (MurasamaPhantomShaderPanel.Available) {
                MurasamaPhantomShaderPanel.Draw(sb, entryRect, alpha, pulse01,
                    shaderTime, 6, 0f, intensity, accent);
            }
            else {
                DrawFallbackBackground(sb, entryRect, entry, isSelected, isHovered, alpha, accent);
            }

            //CPU硬HUD元素叠加(锐利、不依赖shader)
            DrawCpuOverlay(sb, entryRect, entry, alpha, intensity, accent);

            return true;
        }

        //无shader环境下的CPU降级背景
        private void DrawFallbackBackground(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha, Color accent) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            //纵向渐变,模拟血气从底部上涌
            int segs = 14;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = entryRect.Y + (int)(t * entryRect.Height);
                int y2 = entryRect.Y + (int)(t2 * entryRect.Height);
                if (y2 <= y1) continue;

                Color baseC = isSelected ? BgSelected
                    : isHovered ? BgHover
                    : Color.Lerp(BgVoid, BgDeep, t * 0.5f);
                //底部染血
                Color tinted = Color.Lerp(baseC, BgMid, MathF.Pow(t, 1.4f) * 0.55f) * (alpha * 0.95f);

                sb.Draw(px, new Rectangle(entryRect.X, y1, entryRect.Width, y2 - y1), uv, tinted);
            }

            //CRT水平扫描线
            for (int y = entryRect.Y; y < entryRect.Bottom; y += 3) {
                sb.Draw(px, new Rectangle(entryRect.X, y, entryRect.Width, 1),
                    uv, ScanlineRed * (alpha * 0.05f));
            }

            //斩击扫光(对角)
            float bladePos = (scanTime * 0.18f) % 1f;
            int bladeX = entryRect.X + (int)(bladePos * (entryRect.Width + 40)) - 20;
            int bladeW = (int)(entryRect.Width * 0.22f);
            for (int dx = 0; dx < bladeW; dx++) {
                int x = bladeX + dx;
                if (x < entryRect.X || x >= entryRect.Right) continue;
                float fade = 1f - (float)dx / bladeW;
                fade *= fade;
                sb.Draw(px, new Rectangle(x, entryRect.Y, 1, entryRect.Height),
                    uv, CrimsonBright * (alpha * fade * 0.10f));
            }
        }

        //CPU锐利HUD元素叠加层
        private void DrawCpuOverlay(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            float alpha, float intensity, Color accent) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            float barPulse = MathF.Sin(pulseTime * 2.5f) * 0.20f + 0.80f;

            //左侧粗色带(主)
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y + 1, 3, entryRect.Height - 2),
                uv, accent * (alpha * barPulse));
            //左侧高光细线
            sb.Draw(px, new Rectangle(entryRect.X + 3, entryRect.Y + 2, 1, entryRect.Height - 4),
                uv, BloodFlash * (alpha * barPulse * 0.55f));

            //上边框:实线左段 + 锯齿断口 + 虚线段(战术HUD)
            int breakX = entryRect.X + (int)(entryRect.Width * 0.32f);
            Color borderC = CrimsonMid * (alpha * 0.65f);
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y, breakX - entryRect.X, 1), uv, borderC);
            //断口三角缺角
            sb.Draw(px, new Vector2(breakX, entryRect.Y + 1), null, borderC * 0.85f,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(2.4f), SpriteEffects.None, 0f);
            //虚线段
            for (int x = breakX + 8; x < entryRect.Right - 4; x += 6) {
                int w = Math.Min(3, entryRect.Right - 4 - x);
                if (w > 0) sb.Draw(px, new Rectangle(x, entryRect.Y, w, 1), uv, borderC * 0.5f);
            }

            //下边框淡化
            sb.Draw(px, new Rectangle(entryRect.X + 5, entryRect.Bottom - 1,
                entryRect.Width - 10, 1), uv, borderC * 0.30f);

            //右上威胁V字斜杠，MGR ID
            DrawThreatChevrons(sb, entryRect, entry, alpha, intensity);

            //追踪态周身电浆抖动外光
            if (entry.Status == QuestEntryStatus.Tracked && intensity > 0.5f) {
                float bladePulse = MathF.Sin(pulseTime * 4f) * 0.5f + 0.5f;
                Color halo = BladeMode * (alpha * 0.20f * bladePulse);
                sb.Draw(px, new Rectangle(entryRect.X - 1, entryRect.Y - 1, entryRect.Width + 2, 1), uv, halo);
                sb.Draw(px, new Rectangle(entryRect.X - 1, entryRect.Bottom, entryRect.Width + 2, 1), uv, halo);
                sb.Draw(px, new Rectangle(entryRect.X - 1, entryRect.Y, 1, entryRect.Height), uv, halo);
                sb.Draw(px, new Rectangle(entryRect.Right, entryRect.Y, 1, entryRect.Height), uv, halo);
            }
        }

        private void DrawThreatChevrons(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            float alpha, float intensity) {
            var px = VaultAsset.placeholder2.Value;
            int chevrons = 3;
            float baseX = entryRect.Right - 11f;
            float baseY = entryRect.Y + 6f;

            Color chevC = entry.Status == QuestEntryStatus.Completed
                ? CompletedSilver
                : entry.Status == QuestEntryStatus.Failed
                    ? FailedDark
                    : CrimsonMid;

            for (int c = 0; c < chevrons; c++) {
                float pulse = MathF.Sin(scanTime * 1.4f + c * 0.7f) * 0.30f + 0.55f;
                float x = baseX - c * 5f;
                //斜杠由两条短矩形组成(/\形成V字)
                sb.Draw(px, new Vector2(x, baseY), null, chevC * (alpha * pulse * intensity),
                    -MathHelper.PiOver4, new Vector2(0f, 0.5f), new Vector2(5f, 1f), SpriteEffects.None, 0f);
                sb.Draw(px, new Vector2(x, baseY), null, chevC * (alpha * pulse * intensity),
                    MathHelper.PiOver4, new Vector2(0f, 0.5f), new Vector2(5f, 1f), SpriteEffects.None, 0f);
            }
        }

        public float DrawEntryIcon(SpriteBatch sb, Vector2 titlePos, EntrustEntryData entry, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            float cx = titlePos.X + 8f;
            float cy = titlePos.Y + 9f;

            float pulse = MathF.Sin(scanTime * 1.8f) * 0.25f + 0.75f;
            Color iconC = entry.Status switch {
                QuestEntryStatus.Completed => CompletedSilver,
                QuestEntryStatus.Failed => CrimsonDeep,
                QuestEntryStatus.Suspended => SuspendedAsh,
                _ => CrimsonBright,
            };

            //刀刃细长菱形，替大菱形
            //外框
            sb.Draw(px, new Vector2(cx, cy), null, iconC * (alpha * pulse),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(7f, 7f), SpriteEffects.None, 0f);
            //中央留空(贴近本底色)
            sb.Draw(px, new Vector2(cx, cy), null, BgVoid * (alpha * 0.95f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(4.5f, 4.5f), SpriteEffects.None, 0f);
            //内核亮点
            sb.Draw(px, new Vector2(cx, cy), null, BloodFlash * (alpha * pulse * 0.85f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(1.6f, 1.6f), SpriteEffects.None, 0f);

            //血气扩散环(周期性)
            float ringPhase = (scanTime * 0.55f) % MathHelper.TwoPi;
            float ringSize = 4f + ringPhase / MathHelper.TwoPi * 11f;
            float ringAlpha = 1f - ringPhase / MathHelper.TwoPi;
            sb.Draw(px, new Vector2(cx, cy), null, BladeMode * (alpha * ringAlpha * 0.18f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(ringSize), SpriteEffects.None, 0f);

            //追踪态附加旋转十字光
            if (entry.Status == QuestEntryStatus.Tracked) {
                float spin = scanTime * 1.6f;
                for (int i = 0; i < 2; i++) {
                    float a = spin + i * MathHelper.PiOver2;
                    sb.Draw(px, new Vector2(cx, cy), null, BloodFlash * (alpha * 0.45f * pulse),
                        a, new Vector2(0.5f), new Vector2(13f, 0.6f), SpriteEffects.None, 0f);
                }
            }

            return 22f;
        }

        public void DrawEntryOverlay(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            //失败态:全屏血红覆盖+对角斜杠红线
            if (entry.Status == QuestEntryStatus.Failed) {
                sb.Draw(px, entryRect, uv, FailedDark * (alpha * 0.18f));
                //从左下到右上的对角斩痕
                Vector2 start = new(entryRect.X, entryRect.Bottom);
                Vector2 end = new(entryRect.Right, entryRect.Y);
                Vector2 dir = end - start;
                float len = dir.Length();
                dir = Vector2.Normalize(dir);
                float rot = MathF.Atan2(dir.Y, dir.X);
                sb.Draw(px, start, uv, CrimsonDeep * (alpha * 0.55f),
                    rot, new Vector2(0, 0.5f), new Vector2(len, 1.2f), SpriteEffects.None, 0f);
            }

            //偶发CRT故障闪烁(稀疏)
            float glitch = MathF.Sin(dataFlowTime * 3.7f);
            if (glitch > 0.94f) {
                float gIntensity = (glitch - 0.94f) / 0.06f * 0.05f;
                sb.Draw(px, entryRect, uv, CrimsonBright * (alpha * gIntensity));
            }

            //顶部细密血滴(随机出现的小红点)
            int dropSeed = (int)(dataFlowTime * 0.6f) ^ entry.Key.GetHashCode();
            for (int i = 0; i < 3; i++) {
                int s = dropSeed + i * 73;
                float dropX = (s & 0xFF) / 255f;
                float dropPhase = ((dataFlowTime * 0.4f) + i * 0.33f) % 1f;
                if (dropPhase > 0.7f) continue;
                int x = entryRect.X + (int)(dropX * entryRect.Width);
                int y = entryRect.Y + (int)(dropPhase * 8f);
                float dropAlpha = (1f - dropPhase / 0.7f) * 0.55f;
                sb.Draw(px, new Rectangle(x, y, 1, 2), uv, CrimsonMid * (alpha * dropAlpha));
            }
        }

        public Color GetAccentColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => CompletedSilver * alpha,
                QuestEntryStatus.Failed => CrimsonDeep * alpha,
                QuestEntryStatus.Suspended => SuspendedAsh * alpha,
                QuestEntryStatus.Tracked => BladeMode * alpha,
                _ => CrimsonMid * alpha,
            };
        }

        public Color GetTitleColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => TitleComplete * (alpha * 0.92f),
                QuestEntryStatus.Failed => TitleFailed * alpha,
                QuestEntryStatus.Suspended => SuspendedAsh * (alpha * 0.85f),
                _ => TitleBlade * alpha,
            };
        }

        public int? GetCustomEntryHeight() => null;

        public void Reset() {
            scanTime = 0f;
            pulseTime = 0f;
            dataFlowTime = 0f;
            shaderTime = 0f;
        }
    }
}
