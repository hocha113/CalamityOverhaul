using InnoVault.PRT;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Verdant
{
    /// <summary>
    /// 「蒸郁」：残酷模式地表丛林的常态氛围层（纯本地演出）。
    /// 近地湿热薄雾+孢子浮尘，白天虫鸣加密、夜间萤火缓飘，雨时雾更浓、蛙声加入；
    /// 艳阳时偶有「花粉浪」随风飘过（纯视觉柔光）。
    /// 环境声循环镜像 OldNetAmbience 的 SlotId+回调惯例；点缀镜像 AmbienceScore 的周期一次性声。
    /// 同时充当本槽位的公共工具面（群系判定/城镇安宁/伤害基准/并发计数）
    /// </summary>
    internal class VerdantAmbience : ModSystem
    {
        /// <summary>本地屏幕在场强度 0~1（进出群系 ~1s 缓升缓降）</summary>
        internal static float Presence { get; private set; }

        /// <summary>虫鸣压停计时（沼雾伏影凝聚期逐帧续期，骤停即听觉预告）</summary>
        private static int chirpMute;

        //环境声循环槽（湿热空气的底噪）
        private static SlotId humidSlot;
        private static readonly SoundStyle HumidStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        //点缀节拍：虫鸣（连响簇）/蛙声（双连）/叶隙风
        private static int chirpTimer = 240;
        private static int chirpBurstLeft;
        private static int chirpBurstGap;
        private static int frogTimer = 300;
        private static int frogHitsLeft;
        private static int frogGap;
        private static int rustleTimer = 600;

        //粒子节拍
        private static int mistTimer;
        private static int sporeTimer;
        private static int fireflyTimer;
        private static int pollenTimer = 700;

        //==================== 公共工具（本槽位共享） ====================

        /// <summary>残酷模式下的地表丛林判定（与 Mireheart 槽以高度分界：地表层归此处）</summary>
        internal static bool InVerdant(Player player)
            => GameModeSystem.BrutalActive && player.ZoneJungle && player.ZoneOverworldHeight;

        /// <summary>城镇安宁：约 60 格内有存活城镇 NPC 时伤害机制不触发（氛围保留）</summary>
        internal static bool TownSanctuary(Vector2 pos) {
            const float RangeSq = 960f * 960f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && Vector2.DistanceSquared(npc.Center, pos) < RangeSq) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 丛林原版敌怪接触伤害锚（经典档原值，不含任何难度乘数）。
        /// 前困难 30（丛林史莱姆/捕人草一档）、困难后 70（巨飞狐/苔藓蜂一档）。
        /// 难度缩放交给引擎：hostile 弹幕命中实收 = damage × 2/×4/×6（经典/专家/大师），
        /// 消费端负责预除 ×2 结算系数，禁止再叠手动难度乘数
        /// </summary>
        internal static float JungleContactBase() => Main.hardMode ? 70f : 30f;

        /// <summary>统计某类弹幕的活动实例数（镜像 Wastes 写法；只在低频决策处调用）</summary>
        internal static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>压停虫鸣（沼雾伏影在凝聚/浓雾期逐帧调用续期）</summary>
        internal static void MuteChirps() => chirpMute = Math.Max(chirpMute, 10);

        /// <summary>从指定瓦格向下找可站立地表行，找不到返回 -1</summary>
        internal static int FindGroundTileY(int tileX, int fromTileY, int maxDown) {
            for (int dy = 0; dy < maxDown; dy++) {
                int ty = fromTileY + dy;
                if (!WorldGen.InWorld(tileX, ty, 24)) {
                    return -1;
                }
                if (WorldGen.SolidTile(tileX, ty)) {
                    return ty;
                }
            }
            return -1;
        }

        //==================== 生命周期 ====================

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            float target = !Main.gameMenu && InVerdant(Main.LocalPlayer) ? 1f : 0f;
            Presence = Math.Abs(target - Presence) < 0.004f
                ? target : MathHelper.Lerp(Presence, target, 0.045f);
            if (chirpMute > 0) {
                chirpMute--;
            }
            if (Presence < 0.02f) {
                return;
            }
            UpdateAmbientLoop();
            if (Main.gamePaused) {
                return;
            }
            UpdateAccents();
            UpdateParticles();
        }

        public override void ClearWorld() {
            Presence = 0f;
            chirpMute = 0;
            chirpBurstLeft = 0;
            frogHitsLeft = 0;
            chirpTimer = 240;
            frogTimer = 300;
            rustleTimer = 600;
            pollenTimer = 700;
        }

        //湿气压色：氛围级的绿荫沉色，雨时更浓；只染背景为主，保战斗可读性
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (Presence <= 0.002f) {
                return;
            }
            float rainBoost = Main.raining ? 1.45f : 1f;
            tileColor = Color.Lerp(tileColor, new Color(76, 92, 62), 0.10f * Presence * rainBoost);
            backgroundColor = Color.Lerp(backgroundColor, new Color(34, 48, 30), 0.22f * Presence * rainBoost);
        }

        //==================== 环境声 ====================

        //循环丢失（切场景/音量档变化）就补挂，音量在回调里逐帧走
        private static void UpdateAmbientLoop() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(humidSlot, out _)) {
                humidSlot = SoundEngine.PlaySound(HumidStyle, null, UpdateHumidLoop);
            }
        }

        //湿热空气：闷而低的气流底噪，雨时上量
        private static bool UpdateHumidLoop(ActiveSound sound) {
            if (Presence <= 0.004f || Main.gameMenu) {
                return false;
            }
            sound.Volume = 0.055f * (Main.raining ? 1.8f : 1f) * Presence;
            sound.Pitch = -0.42f;
            sound.Position = null;
            return true;
        }

        private static void UpdateAccents() {
            Player player = Main.LocalPlayer;

            //虫鸣：白天簇密夜间稀疏；沼雾凝聚时骤停（安静本身即预告的一半）
            if (chirpMute > 0) {
                chirpBurstLeft = 0;
            }
            else {
                if (chirpBurstLeft > 0 && --chirpBurstGap <= 0) {
                    chirpBurstLeft--;
                    chirpBurstGap = 5;
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.12f * Presence,
                        Pitch = 0.78f + Main.rand.NextFloat(0.16f),
                        MaxInstances = 4,
                    }, player.Center + new Vector2(Main.rand.NextFloat(-420f, 420f), Main.rand.NextFloat(-160f, 60f)));
                }
                if (--chirpTimer <= 0) {
                    bool day = Main.dayTime;
                    chirpTimer = (day ? 210 : 470) + Main.rand.Next(day ? 170 : 280);
                    chirpBurstLeft = day ? 3 : 2;
                    chirpBurstGap = 1;
                }
            }

            //蛙声：下雨时加入（低哑双连）
            if (Main.raining && chirpMute <= 0) {
                if (frogHitsLeft > 0 && --frogGap <= 0) {
                    frogHitsLeft--;
                    frogGap = 9;
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.22f * Presence,
                        Pitch = -0.78f + Main.rand.NextFloat(0.1f),
                        MaxInstances = 3,
                    }, player.Center + new Vector2(Main.rand.NextFloat(-360f, 360f), Main.rand.NextFloat(0f, 140f)));
                }
                if (--frogTimer <= 0) {
                    frogTimer = 260 + Main.rand.Next(220);
                    frogHitsLeft = 2;
                    frogGap = 1;
                }
            }

            //叶隙风：偶发的枝叶翻响
            if (--rustleTimer <= 0) {
                rustleTimer = 780 + Main.rand.Next(560);
                SoundEngine.PlaySound(SoundID.Grass with {
                    Volume = 0.14f * Presence,
                    Pitch = -0.12f,
                    MaxInstances = 2,
                }, player.Center + new Vector2(Main.rand.NextFloat(-320f, 320f), -Main.rand.NextFloat(40f, 180f)));
            }
        }

        //==================== 粒子 ====================
        //常态预算：薄雾 ~6/s + 孢子 ~5/s + 夜萤 ~3/s ≈ 14/s，雨时 ~18/s，均远低于 40/s 公约

        private static void UpdateParticles() {
            Player player = Main.LocalPlayer;
            bool rain = Main.raining;

            //近地湿热薄雾（雨时加密）
            if (--mistTimer <= 0) {
                mistTimer = rain ? 6 : 10;
                SpawnGroundMist(player, rain);
            }

            //孢子浮尘
            if (--sporeTimer <= 0) {
                sporeTimer = 12;
                Vector2 pos = new(Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth),
                    player.Center.Y + Main.rand.NextFloat(-260f, 200f));
                PRTLoader.NewParticle<PRT_VerdantSpore>(pos,
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.1f, 0.15f)),
                    new Color(168, 198, 118), Main.rand.NextFloat(0.07f, 0.13f));
            }

            //夜间萤火：贴近地表植被的高度缓飘
            if (!Main.dayTime && --fireflyTimer <= 0) {
                fireflyTimer = 22;
                int tileX = (int)((Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth)) / 16f);
                //探地起点抬高：上坡列不至于把粒子生进土里
                int groundY = FindGroundTileY(tileX, (int)(player.Center.Y / 16f) - 14, 44);
                if (groundY > 0) {
                    Vector2 pos = new(tileX * 16f + 8f, groundY * 16f - Main.rand.NextFloat(14f, 110f));
                    PRTLoader.NewParticle<PRT_VerdantFirefly>(pos,
                        Main.rand.NextVector2Circular(0.3f, 0.2f),
                        new Color(198, 228, 96), Main.rand.NextFloat(0.09f, 0.13f));
                }
            }

            //花粉浪：艳阳（白天、无雨、少云）时随风飘过的一阵柔金
            if (Main.dayTime && !rain && Main.cloudAlpha < 0.18f && --pollenTimer <= 0) {
                pollenTimer = 560 + Main.rand.Next(620);
                SpawnPollenWave(player);
            }
        }

        private static void SpawnGroundMist(Player player, bool rain) {
            int tileX = (int)((Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth)) / 16f);
            int groundY = FindGroundTileY(tileX, (int)(player.Center.Y / 16f) - 14, 44);
            if (groundY < 0) {
                return;
            }
            Vector2 pos = new(tileX * 16f + 8f, groundY * 16f - Main.rand.NextFloat(4f, 34f));
            Color tint = rain ? new Color(138, 152, 138)
                : Main.dayTime ? new Color(170, 188, 158) : new Color(122, 142, 122);
            PRTLoader.NewParticle<PRT_VerdantMist>(pos,
                new Vector2(Main.windSpeedCurrent * 0.4f + Main.rand.NextFloat(-0.15f, 0.15f),
                    -Main.rand.NextFloat(0.02f, 0.12f)),
                tint, Main.rand.NextFloat(0.38f, 0.66f))?.Configure(rain ? 200 : 160);
        }

        //一阵花粉：从上风向屏幕边飘入的松散光尘团
        private static void SpawnPollenWave(Player player) {
            float wind = Main.windSpeedCurrent;
            float edgeX = wind >= 0f ? Main.screenPosition.X - 40f
                : Main.screenPosition.X + Main.screenWidth + 40f;
            float push = wind * 3.2f + (wind >= 0f ? 0.7f : -0.7f);
            for (int i = 0; i < 9; i++) {
                Vector2 pos = new(edgeX + Main.rand.NextFloat(-60f, 60f),
                    player.Center.Y + Main.rand.NextFloat(-220f, 130f));
                PRTLoader.NewParticle<PRT_VerdantPollen>(pos,
                    new Vector2(push * Main.rand.NextFloat(0.8f, 1.15f), Main.rand.NextFloat(-0.2f, 0.2f)),
                    new Color(232, 204, 112), Main.rand.NextFloat(0.11f, 0.17f));
            }
        }
    }
}
