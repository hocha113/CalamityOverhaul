using CalamityOverhaul.Common;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Starfall
{
    /// <summary>
    /// 残酷模式陨石坑环境层「烬息」的本地枢纽：在场强度、陨石瓦片锚点缓存、
    /// 灼热余烬粒子、金属低鸣循环、偶发滋滋电火花、「磁暴弧」与「天外残响」的排程。
    /// 全部为客户端演出量，权威端不进此类；余爆机制的决策在 <see cref="StarfallPlayer"/>
    /// </summary>
    internal static class StarfallAmbience
    {
        /// <summary>本地屏幕的陨石坑氛围在场强度 0~1（Boss 在场时目标值压低）</summary>
        public static float Presence { get; private set; }

        /// <summary>Boss 在场时的氛围保留系数（纯视觉减弱，不清零）</summary>
        private const float BossDim = 0.45f;

        /// <summary>可见陨石瓦片顶面锚点缓存（烬息/热浪/电火花/磁暴弧共用采样源）</summary>
        private static readonly Vector2[] anchors = new Vector2[64];
        private static int anchorCount;
        private static int scanIn;

        /// <summary>余烬产率积分器（0.20/tick 上限 ≈ 12 粒/秒，随在场强度与锚点密度缩放）</summary>
        private static float emberCarry;

        //金属质感低鸣循环槽（镜像 OldNetAmbience 的 SlotId+回调惯例）
        private static SlotId humSlot;
        private static readonly SoundStyle HumStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };

        //偶发事件计时器
        private static int sparkIn;
        private static int arcIn;
        private static int echoIn;
        /// <summary>天外残响的第二声回声延迟（&gt;0 表示在途）</summary>
        private static int echoTail;
        private static bool wasActive;

        internal static void Update() {
            if (Main.dedServ || Main.gameMenu) {
                Presence = 0f;
                wasActive = false;
                return;
            }
            if (Main.gamePaused) {
                return;
            }

            Player player = Main.LocalPlayer;
            bool on = GameModeSystem.BrutalActive && player.active && player.ZoneMeteor;
            float target = on ? (CWRWorld.HasBoss ? BossDim : 1f) : 0f;
            Presence = Math.Abs(target - Presence) < 0.01f
                ? target : MathHelper.Lerp(Presence, target, 0.05f);

            if (Presence <= 0.004f) {
                anchorCount = 0;
                wasActive = false;
                return;
            }
            //初次激活：事件计时器全部随机错拍，避免入场齐响
            if (!wasActive) {
                wasActive = true;
                sparkIn = Main.rand.Next(180, 420);
                arcIn = Main.rand.Next(300, 720);
                echoIn = Main.rand.Next(1800, 3600);
                echoTail = 0;
            }

            ScanAnchors();
            SpawnEmbers();
            UpdateHumLoop();
            UpdateSparkTicker();
            UpdateArcTicker();
            UpdateEchoTicker(player);
        }

        internal static void Reset() {
            Presence = 0f;
            anchorCount = 0;
            emberCarry = 0f;
            echoTail = 0;
            wasActive = false;
        }

        /// <summary>随机取一个可见陨石瓦片顶面锚点</summary>
        internal static bool TryPickAnchor(out Vector2 pos) {
            if (anchorCount <= 0) {
                pos = default;
                return false;
            }
            pos = anchors[Main.rand.Next(anchorCount)];
            return true;
        }

        /// <summary>每 12 tick 重扫屏幕内的陨石瓦片顶面（步进 2 瓦格 + 随机相位，零分配）</summary>
        private static void ScanAnchors() {
            if (--scanIn > 0) {
                return;
            }
            scanIn = 12;
            anchorCount = 0;
            int x0 = (int)(Main.screenPosition.X / 16f) - 2;
            int x1 = x0 + Main.screenWidth / 16 + 4;
            int y0 = (int)(Main.screenPosition.Y / 16f) - 2;
            int y1 = y0 + Main.screenHeight / 16 + 4;
            int xPhase = Main.rand.Next(2);
            int yPhase = Main.rand.Next(2);
            for (int x = x0 + xPhase; x <= x1; x += 2) {
                for (int y = y0 + yPhase; y <= y1; y += 2) {
                    if (!WorldGen.InWorld(x, y, 10)) {
                        continue;
                    }
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile || tile.TileType != TileID.Meteorite) {
                        continue;
                    }
                    if (WorldGen.SolidTile(x, y - 1)) {
                        continue;//只要顶面暴露的瓦片，余烬要向上冒
                    }
                    anchors[anchorCount++] = new Vector2(x * 16f + 8f, y * 16f);
                    if (anchorCount >= anchors.Length) {
                        return;
                    }
                }
            }
        }

        /// <summary>烬息主体：灼热余烬自陨石地块上升，掺少量暗烟尘（常态预算 ≤12 粒/秒）</summary>
        private static void SpawnEmbers() {
            if (anchorCount <= 0) {
                return;
            }
            emberCarry += 0.20f * Presence * Math.Min(anchorCount, 16) / 16f;
            while (emberCarry >= 1f) {
                emberCarry -= 1f;
                Vector2 pos = anchors[Main.rand.Next(anchorCount)]
                    + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f);
                if (Main.rand.NextBool(7)) {
                    //暗烟尘：余烬熄灭后的碳灰，慢慢飘起
                    Dust smoke = Dust.NewDustPerfect(pos, DustID.Smoke,
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.4f, 0.8f)),
                        170, default, Main.rand.NextFloat(0.7f, 1f));
                    smoke.noGravity = true;
                }
                else {
                    //灼热余烬：上升途中被热流左右吹摆
                    Dust ember = Dust.NewDustPerfect(pos, DustID.Torch,
                        new Vector2(Main.rand.NextFloat(-0.45f, 0.45f), -Main.rand.NextFloat(0.9f, 2.2f)),
                        0, default, Main.rand.NextFloat(0.8f, 1.3f));
                    ember.noGravity = true;
                }
            }
        }

        /// <summary>金属低鸣：外星残骸的哼鸣底噪，音量随在场强度，循环丢失即补挂</summary>
        private static void UpdateHumLoop() {
            if (Presence < 0.05f) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(humSlot, out _)) {
                humSlot = SoundEngine.PlaySound(HumStyle, null, UpdateHum);
            }
        }

        //与 Hollowdeep 深洞低鸣同源（DD2_EtherianPortalIdleLoop），刻意拉开：
        //音高 -0.45（对方 -0.72 洞腔共鸣，这里偏机械哼鸣）、音量加慢呼吸包络（对方恒值）
        private static bool UpdateHum(ActiveSound sound) {
            if (Presence <= 0.01f || Main.gameMenu) {
                return false;
            }
            float breath = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 0.8f);
            sound.Volume = 0.26f * Presence * breath;
            sound.Pitch = -0.45f;
            sound.Position = null;
            return true;
        }

        /// <summary>偶发滋滋电火花：随机陨石瓦片上炸一小撮火花并配电噪，声源定位在瓦片处</summary>
        private static void UpdateSparkTicker() {
            if (--sparkIn > 0) {
                return;
            }
            sparkIn = Main.rand.Next(240, 540);
            if (anchorCount <= 0 || !TryPickAnchor(out Vector2 pos)) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                Volume = 0.38f * Presence,
                Pitch = Main.rand.NextFloat(-0.05f, 0.40f),
                MaxInstances = 3,
            }, pos);
            for (int i = 0; i < 5; i++) {
                bool violet = i >= 3;
                Dust spark = Dust.NewDustPerfect(pos + new Vector2(Main.rand.NextFloat(-4f, 4f), -2f),
                    violet ? DustID.PurpleTorch : DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(1.2f, 3.2f)),
                    0, default, Main.rand.NextFloat(0.7f, 1.15f));
                spark.noGravity = true;
            }
        }

        /// <summary>磁暴弧排程：空中偶发紫弧，纯氛围，频率不随档位走</summary>
        private static void UpdateArcTicker() {
            if (--arcIn > 0) {
                return;
            }
            arcIn = Main.rand.Next(420, 900);
            if (!TryPickAnchor(out Vector2 anchor)) {
                return;
            }
            Vector2 start = anchor + new Vector2(Main.rand.NextFloat(-90f, 90f),
                -Main.rand.NextFloat(260f, 520f));
            StarfallAmbientRender.SpawnArc(start);
        }

        /// <summary>天外残响：遥远陨石坠落的双声回响 + 极轻屏震（受屏震配置门控）</summary>
        private static void UpdateEchoTicker(Player player) {
            if (echoTail > 0 && --echoTail == 0) {
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Volume = 0.16f * Presence,
                    Pitch = -0.85f,
                    MaxInstances = 2,
                });
            }
            if (--echoIn > 0) {
                return;
            }
            echoIn = Main.rand.Next(2700, 5400);
            SoundEngine.PlaySound(SoundID.Thunder with {
                Volume = 0.32f * Presence,
                Pitch = -0.70f,
                MaxInstances = 2,
            });
            echoTail = 26;
            if (Presence > 0.5f && CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(player.Center,
                    Main.rand.NextVector2Unit(), 1.6f, 2.5f, 22, 800f, "CWRStarfallEcho"));
            }
        }
    }

    internal class StarfallAmbienceSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                StarfallAmbience.Update();
            }
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                StarfallAmbience.Reset();
                StarfallAmbientRender.Clear();
            }
        }
    }
}
