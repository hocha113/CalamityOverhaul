using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Shaders;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Ambience
{
    /// <summary>
    /// 鬼梦痕迹微演出（KIY-P5-E）：三个独立小状态机。
    /// E7 水面无源涟漪（B 级自走）：血湖凭空一圈涟漪 + 两粒泡 + 一声弱水花，
    /// 湖底下有东西刚翻了个身；E8 泥地脚印（A 级）：侧方泥地以步行节奏
    /// 踩出一串走向雾浓侧的脚印，泥地替看不见的赶路人记账；
    /// E9 雾里木屐（A 级纯音频）：背后三四声木屐点地，转身路是空的。
    /// A 级经 <see cref="KiyumeDirector.TryClaimScare"/> 申请档期，收尾 ReleaseScare；
    /// 守田人静默区由导演门 10 统一拦（裁决 16 W4 收口，本文件不再自查）。<br/>
    /// 权威端+同步字段：无。脚印是「你看见的」，涟漪是本地水面 shader 状态，
    /// 木屐是本地声；本类 static 只是本地演出进度，非 per-player 游戏状态，
    /// netcode 静态禁令不适用（DungeonworldSnuff 同款口径）。
    /// </summary>
    internal class KiyumeTraces : ModSystem
    {
        //──E7 涟漪──
        private static int rippleTimer = -1;    //-1=首个周期未掷

        //──E8 脚印──
        private static bool footActive;
        private static int stepsLeft;
        private static int stepTimer;
        private static float stepX;
        private static float stepDir;
        private static float lastGroundY;
        private static int footTail;

        //──E9 木屐──
        private static bool getaActive;
        private static int getaHitsLeft;
        private static int getaTimer;
        private static Vector2 getaPos;

        //==================== 生命周期 ====================

        public override void OnWorldLoad() => HardReset();
        public override void ClearWorld() => HardReset();
        public override void Unload() => HardReset();

        private static void HardReset() {
            rippleTimer = -1;
            footActive = false;
            stepsLeft = 0;
            stepTimer = 0;
            footTail = 0;
            getaActive = false;
            getaHitsLeft = 0;
            getaTimer = 0;
        }

        //==================== 驱动 ====================

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            if (!KiyumeWorld.Active || Main.gameMenu || KiyumeAmbienceSystem.Presence < 0.01f) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            UpdateRipple(player);
            UpdateFootprints(player);
            UpdateGeta(player);
        }

        //==================== E7 水面无源涟漪（B 级）====================

        private static void UpdateRipple(Player player) {
            if (rippleTimer < 0) {
                //初相错开：进梦后首圈不与其它点缀同刻
                rippleTimer = (int)(NextRipplePeriod() * Main.rand.NextFloat(0.3f, 0.8f));
                return;
            }
            if (--rippleTimer > 0) {
                return;
            }
            //湖可见带之外短重试（B 级出带条款，镜像火把队列）
            if (player.Center.X >= (KiyumeMetrics.ShoalLeft + KiyumeScore.RippleZoneExtraCols) * 16f) {
                rippleTimer = KiyumeScore.RippleRetryTicks;
                return;
            }
            //落点：视野内（屏宽 ±0.4）∩ 真实水体（WaterRightPx 以西留缘），窗口空短重试
            float lo = MathF.Max(player.Center.X - Main.screenWidth * KiyumeScore.RippleViewFrac, 320f);
            float hi = MathF.Min(player.Center.X + Main.screenWidth * KiyumeScore.RippleViewFrac,
                KiyumeMetrics.WaterRightPx - 64f);
            if (lo >= hi) {
                rippleTimer = KiyumeScore.RippleRetryTicks;
                return;
            }
            rippleTimer = NextRipplePeriod();

            //血湖真水面固定行：湖里的东西偶尔上来换口气
            var pos = new Vector2(Main.rand.NextFloat(lo, hi), KiyumeMetrics.LakeSurfaceRow * 16f + 8f);
            //真水面波纹只在 WaveQuality>=2 可见（tML _useRippleWaves 对源）；低档降级泡+声照播
            if (Main.WaveQuality >= 2
                && Filters.Scene["WaterDistortion"]?.GetShader() is WaterShaderData water) {
                water.QueueRipple(pos, KiyumeScore.RippleStrength, KiyumeScore.RippleSize,
                    RippleShape.Circle);
            }
            for (int i = 0; i < KiyumeScore.RippleBubbles; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    pos + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(4f, 12f)),
                    new Vector2(Main.rand.NextFloat(-0.1f, 0.1f), Main.rand.NextFloat(-0.8f, -0.4f)),
                    new Color(74, 24, 24) * 0.6f, Main.rand.NextFloat(0.16f, 0.24f))
                    ?.Configure(Main.rand.Next(45, 71));
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Volume = KiyumeScore.CapAccent(KiyumeScore.RippleSplashVol),
                Pitch = KiyumeScore.RippleSplashPitch,
                MaxInstances = 2
            }, pos);
        }

        private static int NextRipplePeriod() {
            int period = Main.rand.Next(KiyumeScore.RipplePeriodMin, KiyumeScore.RipplePeriodMax + 1);
            //犬让位期 B 级周期 ×2（导演门 7 的 B 级条款）
            if (KiyumeDirector.HoundYieldActive) {
                period *= 2;
            }
            return period;
        }

        //==================== E8 泥地脚印（A 级）====================

        private static void UpdateFootprints(Player player) {
            if (footActive) {
                if (stepsLeft > 0) {
                    if (--stepTimer <= 0) {
                        stepTimer = KiyumeScore.FootprintStepTicks;
                        PlaceStep();
                        stepsLeft--;
                    }
                    return;
                }
                //走完了：印子留在地上自己淡，槽再压一小段防连吓贴脸
                if (--footTail <= 0) {
                    footActive = false;
                    KiyumeDirector.ReleaseScare(KiyumeScareId.Footprints);
                }
                return;
            }
            //物理门（武装也拦）：滩涂/村带的泥地；守田人静默区由导演门 10 统一拦
            int band = KiyumeMetrics.BandIndexForColumn((int)(player.Center.X / 16f));
            if (band != 1 && band != 2) {
                return;
            }
            if (!KiyumeDirector.TryClaimScare(KiyumeScareId.Footprints,
                KiyumeScore.FootprintWindowLo, KiyumeScore.FootprintWindowHi)) {
                return;
            }
            //走向雾浓侧：两侧各采一次雾密度，浓的那边就是它要去的方向（通常向西向湖）
            float west = KiyumeFogSim.DensityAt(player.Center - new Vector2(256f, 0f));
            float east = KiyumeFogSim.DensityAt(player.Center + new Vector2(256f, 0f));
            stepDir = west >= east ? -1f : 1f;
            stepX = player.Center.X + stepDir * Main.rand.NextFloat(
                KiyumeScore.FootprintStartMinPx, KiyumeScore.FootprintStartMaxPx);
            lastGroundY = player.Center.Y;
            stepsLeft = KiyumeScore.FootprintSteps;
            stepTimer = 0;
            footTail = KiyumeScore.FootprintReleaseTail;
            footActive = true;
        }

        private static void PlaceStep() {
            float x = stepX;
            stepX += stepDir * Main.rand.NextFloat(
                KiyumeScore.FootprintStrideMinPx, KiyumeScore.FootprintStrideMaxPx);
            //逐步探地：探不到这步就整个跳过，宁缺勿飘
            if (!TryFindGround(x, lastGroundY - 96f, out float ground)) {
                return;
            }
            lastGroundY = ground;
            //斜坡贴合：左右各 1 tile 地高差算倾角，压扁片随坡转
            float rot = 0f;
            if (TryFindGround(x - 16f, ground - 96f, out float gl)
                && TryFindGround(x + 16f, ground - 96f, out float gr)) {
                rot = MathF.Atan2(gr - gl, 32f);
            }
            PRTLoader.NewParticle<PRT_KiyumeFootprint>(
                new Vector2(x, ground - 2f), Vector2.Zero,
                KiyumeScore.FootprintColor * KiyumeScore.FootprintColorMul,
                Main.rand.NextFloat(0.9f, 1.1f))
                ?.Configure(KiyumeScore.FootprintLife, KiyumeScore.FootprintFadeTail, rot);
            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = KiyumeScore.CapAccent(KiyumeScore.FootprintStepVol),
                Pitch = KiyumeScore.FootprintStepPitch + Main.rand.NextFloat(-0.05f, 0.05f),
                MaxInstances = 3
            }, new Vector2(x, ground));
        }

        //==================== E9 雾里木屐（A 级纯音频）====================

        private static void UpdateGeta(Player player) {
            if (getaActive) {
                if (--getaTimer > 0) {
                    return;
                }
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = KiyumeScore.CapAccent(KiyumeScore.GetaVol),
                    Pitch = KiyumeScore.GetaPitch + Main.rand.NextFloat(-0.04f, 0.04f),
                    MaxInstances = 2
                }, getaPos);
                if (--getaHitsLeft <= 0) {
                    //声毕即完：转身什么都没有，就是设计的全部
                    getaActive = false;
                    KiyumeDirector.ReleaseScare(KiyumeScareId.Geta);
                    return;
                }
                getaTimer = KiyumeScore.GetaGapTicks;
                return;
            }
            //物理门（武装也拦）：村带 + 雾里（雾不浓这声就没处躲）；静默区由导演门 10 统一拦
            if (KiyumeMetrics.BandIndexForColumn((int)(player.Center.X / 16f)) != 2) {
                return;
            }
            if (KiyumeFogSim.DensityAt(player.Center) <= KiyumeScore.GetaFogGate) {
                return;
            }
            if (!KiyumeDirector.TryClaimScare(KiyumeScareId.Geta,
                KiyumeScore.GetaWindowLo, KiyumeScore.GetaWindowHi)) {
                return;
            }
            //背后定点：有人陪你走了一段，只是不想让你看见
            float x = player.Center.X - player.direction * Main.rand.NextFloat(
                KiyumeScore.GetaDistMinPx, KiyumeScore.GetaDistMaxPx);
            getaPos = TryFindGround(x, player.Center.Y - 240f, out float ground)
                ? new Vector2(x, ground - 8f)
                : new Vector2(x, player.Center.Y);
            getaHitsLeft = Main.rand.Next(KiyumeScore.GetaHitsMin, KiyumeScore.GetaHitsMax + 1);
            getaTimer = 1;
        }

        //从起始高度向下探地表（犬影同款）
        private static bool TryFindGround(float x, float fromY, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < 60; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 20)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }

        /// <summary>一行状态摘要（TestItem 验收用）</summary>
        internal static string StatusLine()
            => $"[痕迹] 涟漪钟{rippleTimer}"
            + $" 脚印{(footActive ? $"步余{stepsLeft}尾{footTail}" : "闲")}"
            + $" 木屐{(getaActive ? $"声余{getaHitsLeft}" : "闲")}";
    }
}
