using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.DuneStorm
{
    /// <summary>
    /// 扬沙的屏幕层绘制（镜像 DungeonworldAmbientRender 的自开自收加色批，无 RT 槽）：
    /// 贴地风带沙线（Airflow 横向拉伸，速度与密度随风/沙暴/风堑涌起）
    /// 与远处热浪微光（竖向暖白光缕，晴昼低速上升）。
    /// 挂 EndEntityDraw：沙线要扫过玩家脚边（人走在风沙里）
    /// </summary>
    internal sealed class DuneStormRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.60（DuneStorm 专属）</summary>
        public override float Weight => 1.60f;

        private const int MaxStreaks = 14;
        private const int MaxWisps = 6;

        private struct Streak
        {
            internal bool Active;
            internal Vector2 Pos;
            internal float Speed;
            internal int Life;
            internal int MaxLife;
            internal float Len;
            internal float Height;
            internal float Alpha;
        }

        private struct Wisp
        {
            internal bool Active;
            internal Vector2 Pos;
            internal float Rise;
            internal int Life;
            internal int MaxLife;
            internal float Phase;
            internal float Len;
        }

        private static readonly Streak[] streaks = new Streak[MaxStreaks];
        private static readonly Wisp[] wisps = new Wisp[MaxWisps];
        private static int streakSpawnIn;
        private static int wispSpawnIn;

        //==================== 逻辑更新 ====================

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = DuneStormAmbience.Presence;
            if (presence < 0.02f) {
                for (int i = 0; i < streaks.Length; i++) {
                    streaks[i].Active = false;
                }
                for (int i = 0; i < wisps.Length; i++) {
                    wisps[i].Active = false;
                }
                return;
            }

            UpdateStreaks(presence);
            UpdateWisps(presence);
        }

        private static void UpdateStreaks(float presence) {
            //推进在途沙线：撞进实体地块或出屏即回收
            for (int i = 0; i < streaks.Length; i++) {
                if (!streaks[i].Active) {
                    continue;
                }
                streaks[i].Pos.X += streaks[i].Speed;
                streaks[i].Life++;
                bool expired = streaks[i].Life >= streaks[i].MaxLife
                    || streaks[i].Pos.X < Main.screenPosition.X - 400f
                    || streaks[i].Pos.X > Main.screenPosition.X + Main.screenWidth + 400f;
                if (!expired && streaks[i].Life % 5 == 0) {
                    Point tp = streaks[i].Pos.ToTileCoordinates();
                    expired = WorldGen.InWorld(tp.X, tp.Y, 10) && WorldGen.SolidTile(tp.X, tp.Y);
                }
                if (expired) {
                    streaks[i].Active = false;
                }
            }

            //密度目标随风/沙暴/风堑走
            float wind = DuneStorm.WindStrength01();
            int targetCount = (int)(presence * (3f + 7f * wind
                + 4f * DuneStormAmbience.StormPressure + 4f * DuneStormAmbience.GustSwell));
            if (--streakSpawnIn > 0) {
                return;
            }
            streakSpawnIn = 3;

            int active = 0;
            for (int i = 0; i < streaks.Length; i++) {
                if (streaks[i].Active) {
                    active++;
                }
            }
            if (active >= targetCount) {
                return;
            }

            //贴地锚定：只在沙系地表上起线
            float worldX = Main.screenPosition.X + Main.rand.NextFloat(-100f, Main.screenWidth + 100f);
            int tileX = (int)(worldX / 16f);
            int startY = (int)(Main.LocalPlayer.Bottom.Y / 16f) - 14;
            if (!DuneStorm.TryFindGround(tileX, startY, out Vector2 ground)) {
                return;
            }
            if (!DuneStorm.IsSandFamily(Framing.GetTileSafely(tileX, (int)(ground.Y / 16f)).TileType)) {
                return;
            }
            float dir = Main.windSpeedCurrent >= 0f ? 1f : -1f;
            float speed = dir * (4.5f + 7f * wind + 4f * DuneStormAmbience.StormPressure
                + 3f * DuneStormAmbience.GustSwell) * Main.rand.NextFloat(0.8f, 1.2f);
            for (int i = 0; i < streaks.Length; i++) {
                if (streaks[i].Active) {
                    continue;
                }
                streaks[i] = new Streak {
                    Active = true,
                    Pos = ground + new Vector2(0f, -Main.rand.NextFloat(4f, 20f)),
                    Speed = speed,
                    Life = 0,
                    MaxLife = Main.rand.Next(45, 85),
                    Len = Main.rand.NextFloat(130f, 230f),
                    Height = Main.rand.NextFloat(9f, 17f),
                    Alpha = Main.rand.NextFloat(0.09f, 0.16f)
                };
                return;
            }
        }

        private static void UpdateWisps(float presence) {
            for (int i = 0; i < wisps.Length; i++) {
                if (!wisps[i].Active) {
                    continue;
                }
                wisps[i].Pos.Y -= wisps[i].Rise;
                wisps[i].Life++;
                if (wisps[i].Life >= wisps[i].MaxLife) {
                    wisps[i].Active = false;
                }
            }

            //热浪只在晴昼、非沙暴主导时可见
            if (!Main.dayTime || Main.raining || DuneStormAmbience.StormPressure > 0.25f || presence < 0.35f) {
                return;
            }
            if (--wispSpawnIn > 0) {
                return;
            }
            wispSpawnIn = Main.rand.Next(14, 26);

            float worldX = Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth);
            int tileX = (int)(worldX / 16f);
            int startY = (int)(Main.LocalPlayer.Bottom.Y / 16f) - 14;
            if (!DuneStorm.TryFindGround(tileX, startY, out Vector2 ground)) {
                return;
            }
            for (int i = 0; i < wisps.Length; i++) {
                if (wisps[i].Active) {
                    continue;
                }
                wisps[i] = new Wisp {
                    Active = true,
                    Pos = ground + new Vector2(0f, -Main.rand.NextFloat(10f, 60f)),
                    Rise = Main.rand.NextFloat(0.35f, 0.8f),
                    Life = 0,
                    MaxLife = Main.rand.Next(100, 160),
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Len = Main.rand.NextFloat(55f, 100f)
                };
                return;
            }
        }

        //==================== 绘制 ====================

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = DuneStormAmbience.Presence;
            if (presence < 0.02f) {
                return;
            }
            bool anyStreak = false;
            for (int i = 0; i < streaks.Length; i++) {
                if (streaks[i].Active) {
                    anyStreak = true;
                    break;
                }
            }
            bool anyWisp = false;
            for (int i = 0; i < wisps.Length; i++) {
                if (wisps[i].Active) {
                    anyWisp = true;
                    break;
                }
            }
            if (!anyStreak && !anyWisp) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            if (anyStreak) {
                DrawStreaks(spriteBatch, presence);
            }
            if (anyWisp) {
                DrawWisps(spriteBatch, presence);
            }
            spriteBatch.End();
        }

        //贴地沙线：横向气流带。Airflow 实测 ext_w=1.00（长轴零端部衰减），
        //整条拉伸=两端一刀切（VFX.md 禁令），按截条三段透明度阶梯收口：暗-亮-暗
        private static void DrawStreaks(SpriteBatch sb, float presence) {
            Texture2D tex = CWRAsset.Airflow?.Value;
            if (tex == null || tex.IsDisposed) {
                return;
            }
            //三段源截条（沿 256 长轴）与端部收口透明度
            ReadOnlySpan<int> segX = [0, 77, 179];
            ReadOnlySpan<int> segW = [77, 102, 77];
            ReadOnlySpan<float> segA = [0.35f, 1f, 0.35f];

            for (int i = 0; i < streaks.Length; i++) {
                if (!streaks[i].Active) {
                    continue;
                }
                float t = streaks[i].Life / (float)streaks[i].MaxLife;
                float env = Math.Min(t / 0.2f, 1f) * MathHelper.Clamp((1f - t) / 0.25f, 0f, 1f);
                float alpha = streaks[i].Alpha * env * presence;
                if (alpha < 0.004f) {
                    continue;
                }
                Vector2 pos = streaks[i].Pos - Main.screenPosition;
                float scaleX = streaks[i].Len / tex.Width;
                float scaleY = streaks[i].Height / tex.Height;
                SpriteEffects flip = streaks[i].Speed < 0f
                    ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                for (int s = 0; s < 3; s++) {
                    var src = new Rectangle(segX[s], 0, segW[s], tex.Height);
                    //截条中心相对贴图中心的横向偏移（对称阶梯，翻面安全）
                    float axisOffset = (segX[s] + segW[s] * 0.5f - tex.Width * 0.5f) * scaleX;
                    sb.Draw(tex, pos + new Vector2(axisOffset, 0f), src,
                        DuneStorm.SandBright * (alpha * segA[s]), 0f,
                        new Vector2(segW[s] * 0.5f, tex.Height * 0.5f),
                        new Vector2(scaleX, scaleY), flip, 0f);
                }
            }
        }

        //热浪微光：竖向暖白光缕缓升，带轻微横摆（远处热空气的折光感）
        private static void DrawWisps(SpriteBatch sb, float presence) {
            Texture2D tex = CWRAsset.LightShot?.Value;
            if (tex == null || tex.IsDisposed) {
                return;
            }
            Color warm = new(255, 240, 205);
            for (int i = 0; i < wisps.Length; i++) {
                if (!wisps[i].Active) {
                    continue;
                }
                float t = wisps[i].Life / (float)wisps[i].MaxLife;
                float env = Math.Min(t / 0.25f, 1f) * MathHelper.Clamp((1f - t) / 0.35f, 0f, 1f);
                float alpha = 0.07f * env * presence;
                if (alpha < 0.004f) {
                    continue;
                }
                float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f + wisps[i].Phase) * 4f;
                Vector2 pos = wisps[i].Pos + new Vector2(sway, 0f) - Main.screenPosition;
                Vector2 scale = new(wisps[i].Len / tex.Width, 9f / tex.Height);
                sb.Draw(tex, pos, null, warm * alpha, -MathHelper.PiOver2,
                    tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
