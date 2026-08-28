using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs;
using CalamityOverhaul.Content.UIs.HudStack;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.Blessings.UI
{
    /// <summary>
    /// 引魂灯 HUD：修罗（含死神永生态）开启时常驻左下角堆叠。
    /// 三层构成：BlessingLantern.fx 氛围层（光晕/漏光/地光/魂雾/微尘）、
    /// 魂焰 shader（受悬停气流倾斜）、SVG 多层线稿（主骨架/细部/窗拱/巡行亮笔/吊铃）。
    /// 新讨伐时魂灵自檐外螺旋入灯，落灯瞬间灯焰腾起、余烬迸散、吊铃受激；
    /// 有未看过的祝福时宝顶栖一缕新焰苗。
    /// 点击或按 <see cref="CWRKeySystem.Blessing_Key"/> 打开往生轮；异域全屏开启时淡出让位
    /// </summary>
    internal class BlessingHud : UIHandle, IBottomLeftHud
    {
        public static BlessingHud Instance => UIHandleLoader.GetUIHandleOfType<BlessingHud>();

        /// <summary>解锁演出总长（帧）：前段魂灵入灯，后段灯焰腾起</summary>
        private const int UnlockTotalFrames = 130;

        /// <summary>魂灵入灯段长（帧），此刻落灯</summary>
        private const int WispFrames = 62;

        /// <summary>余烬粒子上限</summary>
        private const int EmberCap = 26;

        /// <summary>本盏灯的魂焰相位种子</summary>
        private const float LampSeed = 17.3f;

        private float hover;
        private bool wasHovering;
        private float flameLean;      //悬停气流的持续倾斜（平滑）
        private float leanKick;       //落灯瞬间的一记涌动（衰减）
        private float bellAngle;      //吊铃摆角
        private float bellVel;
        private float pressDip;       //按下沉降 1→0
        private float prevUnlockSince = -1f;
        private Rectangle lanternRect;
        private readonly List<FlameCell> flameScratch = [];
        private readonly List<Ember> embers = [];
        private int emberTimer;

        /// <summary>一粒上浮余烬</summary>
        private struct Ember
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Mix;   //0=余烬金 1=accent 紫
            public float Seed;
        }

#if DEBUG
        /// <summary>VisLab 视觉联排：真值时无视模式门并伪装燃焰数与解锁循环（仅影响显示）</summary>
        public bool mockActive;
#endif

        public override bool Active {
            get {
                bool gate = BlessingPlayer.SystemActive;
#if DEBUG
                gate |= mockActive;
#endif
                return gate && !Main.gameMenu;
            }
        }

        //——左下角堆叠契约——

        public bool HudStackActive => Active;
        public int HudStackOrder => 40;
        public Vector2 HudStackAnchor => BlessingTheme.LanternAnchor;
        public float HudStackTopExtent => BlessingTheme.LanternSize.Y + 30f;
        public float HudStackBottomExtent => 12f;

        /// <summary>让位遮蔽：异域全屏展开度与自家往生轮展开度取大</summary>
        private static float Occlusion {
            get {
                float foreign = FullScreenUIHub.ForeignOcclusion01(FullScreenUIDomain.Asura);
                float own = BlessingWheelUI.Instance?.OpenProgress.Current ?? 0f;
                return Math.Max(foreign, own);
            }
        }

        /// <summary>距最近一次解锁的帧数；-1=无演出。DEBUG 联排下循环播放</summary>
        private float UnlockSince {
            get {
#if DEBUG
                if (mockActive) {
                    return Main.GameUpdateCount % 300u;
                }
#endif
                if (BlessingWorld.RecentUnlock == null) {
                    return -1f;
                }
                uint since = Main.GameUpdateCount - BlessingWorld.RecentUnlockTick;
                return since < UnlockTotalFrames ? since : -1f;
            }
        }

        /// <summary>魂灵入灯进度 0..1；不在入灯段时为 -1</summary>
        private static float WispT(float since)
            => since >= 0f && since < WispFrames ? since / WispFrames : -1f;

        /// <summary>灯焰腾起包络 1→0；入灯段与演出外为 0</summary>
        private static float PulseEnv(float since) {
            if (since < WispFrames || since >= UnlockTotalFrames) {
                return 0f;
            }
            return 1f - (since - WispFrames) / (UnlockTotalFrames - WispFrames);
        }

        /// <summary>本帧显示口径（燃焰数/槽上限/新焰苗），DEBUG 联排时伪装</summary>
        private (int burning, int cap, bool hasNew) ResolveCounts() {
            BlessingPlayer bp = Main.LocalPlayer.GetModPlayer<BlessingPlayer>();
            int burning = bp.BurningCount;
            int cap = BlessingPlayer.SlotCap;
            bool hasNew = bp.HasUnwitnessed;
#if DEBUG
            if (mockActive) {
                burning = 2;
                cap = 3;
                hasNew = true;
            }
#endif
            return (burning, cap, hasNew);
        }

        public override void Update() {
            Vector2 anchor = BottomLeftHudStack.ResolveAnchor(this);
            lanternRect = BlessingTheme.LanternRect(anchor);
            Vector2 flameCenter = BlessingRenderer.LanternFlameRect(lanternRect).Center.ToVector2();

            float since = UnlockSince;
            bool interactive = Occlusion < 0.1f;
            bool hovering = interactive && lanternRect.Contains(Main.MouseScreen.ToPoint());
            hover = MathHelper.Lerp(hover, hovering ? 1f : 0f, 0.2f);

            //悬停沿：一声轻响 + 吊铃受一缕风
            if (hovering && !wasHovering) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = 0.3f });
                bellVel += 0.045f;
            }
            wasHovering = hovering;

            //悬停气流：焰体缓缓倒向背离光标的一侧，光标离开后回正
            float leanTarget = 0f;
            if (hovering) {
                leanTarget = MathHelper.Clamp((flameCenter.X - Main.MouseScreen.X) / 40f, -1f, 1f) * 0.35f;
            }
            flameLean = MathHelper.Lerp(flameLean, leanTarget, 0.12f);
            leanKick *= 0.90f;

            //吊铃摆动：弹簧 + 阻尼 + 常吹微风
            bellVel += -bellAngle * 0.025f - bellVel * 0.05f;
            bellVel += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + 2.1f) * 0.0012f;
            bellAngle = MathHelper.Clamp(bellAngle + bellVel, -0.6f, 0.6f);

            pressDip *= 0.85f;

            //落灯瞬间：余烬迸散 + 焰体涌动 + 吊铃受激 + 一声入灯
            if (prevUnlockSince >= 0f && prevUnlockSince < WispFrames && since >= WispFrames) {
                BurstEmbers(flameCenter, 14);
                leanKick = Main.rand.NextFloat(-0.5f, 0.5f);
                bellVel += Main.rand.NextFloat(-0.10f, 0.10f);
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = -0.25f });
            }
            prevUnlockSince = since;

            UpdateEmbers(flameCenter, since, interactive);

            if (!hovering) {
                return;
            }

            player.mouseInterface = true;
            BlessingPlayer bp = Main.LocalPlayer.GetModPlayer<BlessingPlayer>();
            Main.hoverItemName = BlessingSystemText.HudName.Value + " · "
                + BlessingSystemText.BurningCounter.Format(bp.BurningCount, BlessingPlayer.SlotCap)
                + "\n" + BlessingSystemText.HudOpenHint.Value;

            if (keyLeftPressState == KeyPressState.Pressed) {
                pressDip = 1f;
                bellVel -= 0.06f;
                BlessingWheelUI.Instance?.Toggle();
            }
        }

        /// <summary>余烬粒子推进与孵化：燃焰越多吐得越勤，零燃焰只偶发一粒</summary>
        private void UpdateEmbers(Vector2 flameCenter, float since, bool interactive) {
            for (int i = embers.Count - 1; i >= 0; i--) {
                Ember e = embers[i];
                e.Life--;
                if (e.Life <= 0f) {
                    embers.RemoveAt(i);
                    continue;
                }
                e.Vel.Y -= 0.0045f;    //浮力缓增
                e.Vel.X += MathF.Sin(Main.GameUpdateCount * 0.11f + e.Seed) * 0.006f;
                e.Vel *= 0.985f;
                e.Pos += e.Vel;
                embers[i] = e;
            }

            //遮蔽中不再孵化，存量自然烧尽
            if (!interactive && Occlusion > 0.5f) {
                return;
            }
            (int burning, _, _) = ResolveCounts();
            int interval = burning > 0 ? Math.Max(9, 32 - burning * 5) : 70;
            if (PulseEnv(since) > 0.5f) {
                interval = Math.Max(4, interval / 3);
            }
            if (++emberTimer >= interval && embers.Count < EmberCap) {
                emberTimer = 0;
                SpawnEmber(flameCenter, false);
            }
        }

        private void SpawnEmber(Vector2 flameCenter, bool burst) {
            float spread = burst ? 7f : 3.5f;
            float maxLife = Main.rand.NextFloat(42f, 84f);
            embers.Add(new Ember {
                Pos = flameCenter + new Vector2(Main.rand.NextFloat(-spread, spread), Main.rand.NextFloat(-4f, 2f)),
                Vel = new Vector2(Main.rand.NextFloat(-0.25f, 0.25f) * (burst ? 2.2f : 1f),
                    -Main.rand.NextFloat(0.35f, 0.85f) * (burst ? 1.8f : 1f)),
                MaxLife = maxLife,
                Life = maxLife,
                Size = Main.rand.NextFloat(1.3f, 2.5f),
                Mix = Main.rand.NextFloat(0.45f),
                Seed = Main.rand.NextFloat(MathHelper.TwoPi),
            });
        }

        private void BurstEmbers(Vector2 flameCenter, int count) {
            for (int i = 0; i < count && embers.Count < EmberCap + 8; i++) {
                SpawnEmber(flameCenter, true);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            float alpha = 1f - Occlusion;
            if (alpha <= 0.01f) {
                return;
            }

            (int burning, int cap, bool hasNew) = ResolveCounts();
            float since = UnlockSince;
            float wispT = WispT(since);
            float pulse = PulseEnv(since);

            //燃焰亮度：零燃焰留长明微焰，腾起时冲顶
            float litRatio = cap > 0 ? burning / (float)cap : 0f;
            float lit = burning > 0 ? 0.5f + 0.5f * litRatio : 0.18f;
            lit = Math.Min(1f, lit + pulse * 0.8f);

            //1 氛围层：魂灵接近时灯先渐亮（预感），落灯后走腾起包络
            float ambientPulse = Math.Max(pulse, wispT >= 0f ? wispT * 0.30f : 0f);
            BlessingRenderer.DrawLanternAmbient(spriteBatch, lanternRect, LampSeed,
                burning > 0 ? litRatio : 0.12f, hover, ambientPulse, alpha);

            //2 魂焰批：焰室主焰 + 宝顶新焰苗
            Rectangle flameRect = BlessingRenderer.LanternFlameRect(lanternRect);
            if (pulse > 0f) {
                int grow = (int)(pulse * 9f);
                flameRect.Inflate(grow, grow + 4);
                flameRect.Y -= grow;
            }
            flameScratch.Clear();
            if (lit > 0.01f) {
                flameScratch.Add(new FlameCell {
                    Rect = flameRect,
                    Seed = LampSeed,
                    Lit = lit,
                    Alpha = alpha,
                    Lean = MathHelper.Clamp(flameLean + leanKick, -1f, 1f),
                });
            }
            if (hasNew) {
                //宝顶栖一缕新焰苗，呼吸明灭，随微风轻摆
                float breath = 0.5f + 0.5f * MathF.Sin(Main.GameUpdateCount * 0.045f);
                int sw = (int)(lanternRect.Width * 0.26f);
                int sh = (int)(lanternRect.Height * 0.24f);
                int rootY = (int)(lanternRect.Y + lanternRect.Height * 0.05f);
                flameScratch.Add(new FlameCell {
                    Rect = new Rectangle(lanternRect.Center.X - sw / 2, rootY - (int)(sh * 0.75f), sw, sh),
                    Seed = 91.7f,
                    Lit = 0.30f + 0.35f * breath,
                    Alpha = alpha * 0.95f,
                    Lean = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.3f) * 0.25f,
                });
            }
            BlessingRenderer.DrawFlames(spriteBatch, flameScratch);

            //3 线稿：主骨架（燃焰越多越染 accent）→ 细部次笔 → 窗拱亮描 → 巡行亮笔
            Vector2 center = lanternRect.Center.ToVector2() + new Vector2(0f, pressDip * 1.5f);
            float halfSize = lanternRect.Height * 0.5f;
            float litness = Math.Min(1f, burning / 3f);
            Color frame = Color.Lerp(BlessingTheme.BoneDim, BlessingTheme.Accent,
                0.25f + litness * 0.45f + hover * 0.30f) * alpha;
            SvgPath lantern = SvgPathPen.Path(BlessingRenderer.LanternPath);
            BlessingRenderer.StrokePath100(spriteBatch, lantern, center, halfSize,
                frame, 1.7f, alpha, hover > 0.4f ? BlessingTheme.Ember * (hover * 0.6f) : null);

            SvgPath detail = SvgPathPen.Path(BlessingRenderer.LanternDetailPath);
            BlessingRenderer.StrokePath100(spriteBatch, detail, center, halfSize,
                frame * 0.55f, 1.0f, alpha * 0.9f);

            Color arch = Color.Lerp(frame, BlessingTheme.Ember, 0.30f + 0.35f * lit) * alpha;
            SvgPath window = SvgPathPen.Path(BlessingRenderer.LanternWindowPath);
            BlessingRenderer.StrokePath100(spriteBatch, window, center, halfSize,
                arch, 1.4f, alpha, lit > 0.5f ? BlessingTheme.Ember * (0.35f * lit) : null);

            //巡行亮笔：一段光沿灯骨缓行，悬停提速增亮——"通着魂气"的活性
            float runnerHead = (Main.GlobalTimeWrappedHourly * (0.045f + hover * 0.05f)) % 1f;
            BlessingRenderer.RunnerPath100(spriteBatch, lantern, center, halfSize,
                BlessingTheme.Ember, 1.0f, (0.30f + 0.45f * hover) * alpha, runnerHead, 0.05f,
                Color.White * (0.3f * hover));

            //4 吊铃：檐角垂铃随物理摆动
            Vector2 hook = BlessingRenderer.LanternBellHook(lanternRect);
            SvgPathPen.Stroke(spriteBatch, SvgPathPen.Path(BlessingRenderer.LanternBellPath),
                hook, lanternRect.Height / 74f, bellAngle, frame * 0.9f, 1.1f, alpha);

            //5 灯座刻度：槽上限一排珠位，燃焰者亮金并带一点辉
            DrawSlotNotches(spriteBatch, burning, cap, alpha);

            //6 余烬粒子：菱形亮屑 + 大粒的软辉
            DrawEmbers(spriteBatch, alpha);

            //7 魂灵入灯：贝塞尔螺旋接近，带拖影
            if (wispT >= 0f) {
                DrawWisp(spriteBatch, wispT, alpha);
            }

            //8 落灯扩散环 + 辉闪
            if (pulse > 0f) {
                Vector2 c = flameRect.Center.ToVector2();
                float ease = 1f - pulse;
                BlessingRenderer.DrawRingPasses(spriteBatch, c,
                    MathHelper.Lerp(lanternRect.Width * 0.4f, lanternRect.Width * 2.4f, ease * ease),
                    Color.Lerp(BlessingTheme.Accent, BlessingTheme.Ember, 0.5f), pulse * 0.8f * alpha);
                BlessingRenderer.DrawGlow(spriteBatch, c, lanternRect.Width * 2.4f,
                    BlessingTheme.Ember, pulse * 0.45f * alpha);
            }
        }

        /// <summary>灯座下的槽位刻度：亮珠=燃焰，暗珠=空槽</summary>
        private void DrawSlotNotches(SpriteBatch sb, int burning, int cap, float alpha) {
            if (cap <= 0) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float spacing = 7f;
            float y = lanternRect.Bottom + 7f;
            float x0 = lanternRect.Center.X - (cap - 1) * spacing * 0.5f;
            for (int i = 0; i < cap; i++) {
                bool on = i < burning;
                Vector2 pos = new(x0 + i * spacing, y);
                if (on) {
                    Color c = BlessingTheme.Ember;
                    SvgPathPen.SoftDot(sb, pos, 5f, c, (0.30f + 0.25f * hover) * alpha);
                    sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), c * alpha, MathHelper.PiOver4,
                        new Vector2(0.5f), new Vector2(2.5f), SpriteEffects.None, 0f);
                }
                else {
                    sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                        BlessingTheme.BoneDim * (0.45f * alpha), MathHelper.PiOver4,
                        new Vector2(0.5f), new Vector2(1.8f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>余烬粒子：出生迸亮、终末熄暗，大粒带软辉</summary>
        private void DrawEmbers(SpriteBatch sb, float alpha) {
            if (embers.Count == 0) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            foreach (Ember e in embers) {
                float t = e.Life / e.MaxLife;             //1→0
                float a = MathF.Sin(MathF.Min(1f, (1f - t) * 4f) * MathHelper.PiOver2) * t * alpha;
                if (a <= 0.02f) {
                    continue;
                }
                Color c = Color.Lerp(BlessingTheme.Ember, BlessingTheme.Accent, e.Mix);
                float s = e.Size * (0.6f + 0.4f * t);
                sb.Draw(pixel, e.Pos, new Rectangle(0, 0, 1, 1), new Color(c.R, c.G, c.B, (byte)0) * a,
                    MathHelper.PiOver4 + e.Seed, new Vector2(0.5f), new Vector2(s), SpriteEffects.None, 0f);
                if (e.Size > 2f) {
                    SvgPathPen.SoftDot(sb, e.Pos, s * 2.6f, c, a * 0.45f);
                }
            }
        }

        /// <summary>魂灵入灯：自檐外沿贝塞尔曲线螺旋落向焰室，头亮尾散</summary>
        private void DrawWisp(SpriteBatch sb, float wispT, float alpha) {
            Vector2 end = BlessingRenderer.LanternFlameRect(lanternRect).Center.ToVector2();
            Vector2 p0 = end + new Vector2(lanternRect.Width * 1.9f, -lanternRect.Height * 1.35f);
            Vector2 p1 = end + new Vector2(lanternRect.Width * 0.55f, -lanternRect.Height * 1.85f);
            float te = 1f - (1f - wispT) * (1f - wispT);  //ease-out：末段减速落灯

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color body = Color.Lerp(BlessingTheme.Accent, BlessingTheme.Ember, 0.35f);
            for (int k = 4; k >= 0; k--) {
                float tk = te - k * 0.045f;
                if (tk <= 0f) {
                    continue;
                }
                Vector2 pos = WispPoint(p0, p1, end, tk);
                float ka = (0.75f - k * 0.13f) * alpha;
                float size = 3.2f - k * 0.45f;
                if (k == 0) {
                    BlessingRenderer.DrawGlow(sb, pos, 22f + 6f * MathF.Sin(Main.GameUpdateCount * 0.25f),
                        body, 0.8f * alpha);
                    sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), Color.White * (0.9f * alpha),
                        MathHelper.PiOver4, new Vector2(0.5f), new Vector2(2.2f), SpriteEffects.None, 0f);
                }
                else {
                    SvgPathPen.SoftDot(sb, pos, size * 2.2f, body, ka * 0.5f);
                }
            }
        }

        /// <summary>魂灵航迹：二次贝塞尔 + 递减的垂向螺旋抖动</summary>
        private static Vector2 WispPoint(Vector2 p0, Vector2 p1, Vector2 end, float t) {
            float u = 1f - t;
            Vector2 pos = u * u * p0 + 2f * u * t * p1 + t * t * end;
            Vector2 tangent = 2f * u * (p1 - p0) + 2f * t * (end - p1);
            Vector2 normal = tangent.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            pos += normal * MathF.Sin(t * 9.4f) * 7f * (1f - t);
            return pos;
        }
    }

    /// <summary>祝福键：修罗开启时开合往生轮</summary>
    internal class BlessingKeySystem : ModSystem
    {
        public override void UpdateUI(GameTime gameTime) {
            if (!BlessingPlayer.SystemActive || Main.gameMenu) {
                return;
            }
            if (CWRKeySystem.Blessing_Key != null && CWRKeySystem.Blessing_Key.JustReleased) {
                BlessingWheelUI.Instance?.Toggle();
            }
        }
    }
}
