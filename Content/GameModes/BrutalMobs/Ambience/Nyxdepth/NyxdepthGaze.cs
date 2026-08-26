using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Nyxdepth
{
    /// <summary>
    /// 「深渊凝视」：黑暗远处低频浮现一对发光眼睛盯视玩家，缓慢眨动；
    /// 玩家靠近或该处被照亮即散，纯恐吓，不生成敌怪、不造成伤害、不发光照。<br/>
    /// 完全本机客户端演出（每名玩家看到自己的凝视），状态走静态本地量；
    /// 绘制由 <see cref="NyxdepthAmbientRender"/> 在其批次内调用 <see cref="Draw"/>
    /// </summary>
    internal static class NyxdepthGaze
    {
        private enum Phase : byte
        {
            /// <summary>无凝视，倒计时下一次浮现</summary>
            Idle,
            /// <summary>缓慢浮现</summary>
            FadeIn,
            /// <summary>驻留盯视（带眨眼）</summary>
            Hold,
            /// <summary>自然隐去</summary>
            FadeOut,
            /// <summary>被惊散（靠近/照亮）</summary>
            Disperse,
        }

        private const int FadeInFrames = 75;
        private const int FadeOutFrames = 45;
        private const int DisperseFrames = 18;
        /// <summary>玩家靠近到这个距离即散</summary>
        private const float ApproachDistPx = 300f;
        /// <summary>该处亮度超过此值视作被照亮</summary>
        private const float LitThreshold = 0.24f;
        /// <summary>浮现点要求的黑暗程度</summary>
        private const float DarkThreshold = 0.13f;

        private static Phase phase;
        private static int timer;
        private static int spawnIn = 700;
        private static Vector2 anchor;
        private static float fade;
        private static int blinkIn = 110;
        /// <summary>眨眼进度帧，-1=睁眼稳态；0..17 走 闭6/停4/睁8</summary>
        private static int blinkT = -1;
        private static Vector2 pupil;

        /// <summary>仍需绘制（含渐出尾巴）</summary>
        public static bool Visible => phase != Phase.Idle && fade > 0.01f;

        public static void Update() {
            if (phase == Phase.Idle) {
                if (NyxdepthAmbience.Pressure < 0.20f) {
                    return;//浅层不闹鬼，计时也不走
                }
                if (--spawnIn > 0) {
                    return;
                }
                if (TrySpawn()) {
                    phase = Phase.FadeIn;
                    timer = FadeInFrames;
                    fade = 0f;
                    blinkT = -1;
                    blinkIn = Main.rand.Next(80, 150);
                    pupil = Vector2.Zero;
                }
                else {
                    spawnIn = 240;//找不到合适的黑暗水域，稍后再试
                }
                return;
            }

            //追视：瞳孔缓慢滑向玩家方向
            Player player = Main.LocalPlayer;
            pupil = Vector2.Lerp(pupil, (player.Center - anchor).SafeNormalize(Vector2.Zero) * 3f, 0.08f);

            //眨眼推进（只在驻留期起新眨，眨到一半进了别的相位就眨完）
            if (blinkT >= 0) {
                if (++blinkT >= 18) {
                    blinkT = -1;
                }
            }
            else if (phase == Phase.Hold && --blinkIn <= 0) {
                blinkT = 0;
                blinkIn = Main.rand.Next(80, 150);
            }

            //惊散判定：浮现与驻留期都会被靠近/照亮打断
            if (phase == Phase.FadeIn || phase == Phase.Hold) {
                Point tilePos = anchor.ToTileCoordinates();
                bool tooClose = Vector2.Distance(player.Center, anchor) < ApproachDistPx;
                bool lit = Lighting.Brightness(tilePos.X, tilePos.Y) > LitThreshold;
                bool gone = NyxdepthAmbience.Pressure < 0.05f;
                if (tooClose || lit || gone) {
                    EnterDisperse();
                    return;
                }
            }

            switch (phase) {
                case Phase.FadeIn:
                    fade = 1f - timer / (float)FadeInFrames;
                    if (--timer <= 0) {
                        phase = Phase.Hold;
                        timer = Main.rand.Next(300, 480);
                        fade = 1f;
                    }
                    break;
                case Phase.Hold:
                    if (--timer <= 0) {
                        phase = Phase.FadeOut;
                        timer = FadeOutFrames;
                    }
                    break;
                case Phase.FadeOut:
                    fade = timer / (float)FadeOutFrames;
                    if (--timer <= 0) {
                        BackToIdle();
                    }
                    break;
                case Phase.Disperse:
                    fade *= 0.82f;
                    if (--timer <= 0) {
                        BackToIdle();
                    }
                    break;
            }
        }

        /// <summary>在屏内边缘带找一处黑暗的开阔水域（不贴脸、不出屏、够黑、有水）</summary>
        private static bool TrySpawn() {
            Player player = Main.LocalPlayer;
            for (int i = 0; i < 12; i++) {
                Vector2 pos = new(
                    Main.screenPosition.X + Main.rand.NextFloat(90f, Main.screenWidth - 90f),
                    Main.screenPosition.Y + Main.rand.NextFloat(90f, Main.screenHeight - 90f));
                float dist = Vector2.Distance(pos, player.Center);
                if (dist < 430f || dist > 920f) {
                    continue;
                }
                Point tilePos = pos.ToTileCoordinates();
                if (!WorldGen.InWorld(tilePos.X, tilePos.Y, 40) || WorldGen.SolidTile(tilePos.X, tilePos.Y)) {
                    continue;
                }
                Tile tile = Framing.GetTileSafely(tilePos.X, tilePos.Y);
                if (tile.LiquidAmount < 60 || tile.LiquidType != LiquidID.Water) {
                    continue;
                }
                if (Lighting.Brightness(tilePos.X, tilePos.Y) > DarkThreshold) {
                    continue;
                }
                anchor = pos;
                return true;
            }
            return false;
        }

        /// <summary>惊散：一声弱水响+几缕暗尘，眼睛快速没入黑暗</summary>
        private static void EnterDisperse() {
            phase = Phase.Disperse;
            timer = DisperseFrames;
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Volume = 0.28f, Pitch = -0.7f, MaxInstances = 2
            }, anchor);
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(anchor + Main.rand.NextVector2Circular(14f, 8f),
                    DustID.DungeonWater, Main.rand.NextVector2Circular(1.6f, 1.2f),
                    150, new Color(30, 50, 60), 1.1f);
                dust.noGravity = true;
            }
        }

        private static void BackToIdle() {
            phase = Phase.Idle;
            fade = 0f;
            blinkT = -1;
            spawnIn = Main.rand.Next(900, 1800);
        }

        public static void Reset() {
            phase = Phase.Idle;
            fade = 0f;
            timer = 0;
            blinkT = -1;
            spawnIn = 700;
            pupil = Vector2.Zero;
        }

        /// <summary>眨眼包络：0=全睁 1=全闭</summary>
        private static float BlinkEnv() {
            if (blinkT < 0) {
                return 0f;
            }
            if (blinkT < 6) {
                return blinkT / 6f;
            }
            return blinkT < 10 ? 1f : 1f - (blinkT - 10) / 8f;
        }

        /// <summary>
        /// 由渲染句柄在 AlphaBlend+GameView 批内调用。
        /// 暗窝/瞳孔用真 alpha 暗色承载，幽光走 A=0 加色敷料；不 AddLight，免得自己触发照亮驱散
        /// </summary>
        public static void Draw(SpriteBatch sb) {
            if (!Visible) {
                return;
            }
            Texture2D fog = CWRAsset.Fog?.Value;
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (fog == null || spindle == null || glow == null) {
                return;
            }
            float alpha = fade * 0.62f * NyxdepthAmbience.Presence;
            if (alpha <= 0.01f) {
                return;
            }
            //睁闭程度：留 0.08 底防退化为零面积
            float open = 1f - BlinkEnv() * 0.92f;

            //水中生物的极缓漂移
            Vector2 sway = new(
                MathF.Sin(Main.GlobalTimeWrappedHourly * 0.7f) * 2.5f,
                MathF.Sin(Main.GlobalTimeWrappedHourly * 0.53f + 1.7f) * 2f);
            Vector2 center = anchor + sway - Main.screenPosition;

            //暗窝：真 alpha 暗层，让眼光读作嵌在一团更深的黑里
            sb.Draw(fog, center, null, new Color(3, 5, 9) * (0.55f * alpha), 0.4f,
                fog.Size() * 0.5f, 0.62f, SpriteEffects.None, 0f);

            for (int i = -1; i <= 1; i += 2) {
                Vector2 eye = center + new Vector2(i * 13f, 0f);
                float lean = i * 0.055f;//双眼各自向内微倾，读作聚焦
                //幽光底（A=0 加色）
                sb.Draw(glow, eye, null, new Color(30, 88, 82, 0) * (0.55f * alpha), 0f,
                    glow.Size() * 0.5f, 0.55f, SpriteEffects.None, 0f);
                //眼睑透镜：竖梭形横置成杏眼，眨眼压贴图 X 轴（旋转后即眼高）
                sb.Draw(spindle, eye, null, new Color(140, 220, 205) * (0.85f * alpha),
                    MathHelper.PiOver2 + lean, spindle.Size() * 0.5f,
                    new Vector2(0.30f * open, 0.44f), SpriteEffects.None, 0f);
                //热芯（SoftGlow 黑底 A=0 加色；自 Extra_98 换来按 VFX.md 约 ×0.5 尺度折算）
                sb.Draw(glow, eye, null, new Color(190, 255, 235, 0) * (0.8f * alpha),
                    MathHelper.PiOver2 + lean, glow.Size() * 0.5f,
                    new Vector2(0.08f * open, 0.15f), SpriteEffects.None, 0f);
                //竖瞳：追视玩家，眼睑将合时先没入
                float pupilVis = MathHelper.Clamp((open - 0.25f) / 0.75f, 0f, 1f);
                if (pupilVis > 0.05f) {
                    sb.Draw(spindle, eye + pupil, null, new Color(2, 3, 5) * (0.9f * alpha * pupilVis),
                        lean, spindle.Size() * 0.5f,
                        new Vector2(0.085f, 0.15f * open), SpriteEffects.None, 0f);
                }
            }
        }
    }
}
