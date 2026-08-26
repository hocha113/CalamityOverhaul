using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mournfog
{
    /// <summary>
    /// 残酷模式墓地环境氛围（纯本机演出层，服务端不跑）。三个具名特色 + 一个可选惊吓：
    /// 「霭祭」常态氛围：原版墓地雾之上叠一层缓慢脉动的浓雾（9s 呼吸），
    /// 远鸦双啼 + 远钟 + 风过墓碑的呜咽声床，零星暗绿鬼火漫飘；
    /// 「碑语」：靠近墓碑时偶发碑面幽光亮起 + 一句低语（纯氛围，无文本 UI）；
    /// 「掘响」：地面低频冒出骨手抓挠的尘效 + 闷响（纯视觉惊吓，不伤害不生成敌怪）。
    /// 「怨聚」的累积在 <see cref="MournfogPlayer"/>，环体在 Projectiles 文件夹。
    /// 环境音循环镜像 OldNetAmbience 的槽位管理；Boss 在场氛围减半保留
    /// </summary>
    internal class MournfogAmbience : ModSystem
    {
        /// <summary>本机在场强度 0~1（进出墓地缓升缓降，不硬切）</summary>
        internal static float Presence { get; private set; }

        /// <summary>浓雾脉动 0~1：周期 9s 整除 GlobalTimeWrappedHourly 的 3600s，回绕无跳变</summary>
        internal static float FogPulse =>
            0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * (MathHelper.TwoPi / 9f));

        //==== 声床（呜咽风循环 + 一次性点缀） ====
        private static SlotId windSlot;
        private static readonly SoundStyle WindMoanStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };
        /// <summary>远鸦：Roar 拉高变薄双连（镜像 Dungeonworld 逆唱诗的既有手法）</summary>
        private static int cawIn = 900;
        private static int cawHitsLeft;
        private static int cawHitIn;
        private static float cawPitch;
        private static int bellIn = 1800;
        private static int foreshadowIn = 120;

        //==== 霭祭粒子预算 ====
        private static int mistIn = 30;
        private static int wispIn = 60;

        //==== 掘响（一次一处，低频） ====
        private static int scratchIn = 2400;
        private static int scratchTick = -1;
        private static Vector2 scratchPos;
        /// <summary>掘响总时长</summary>
        private const int ScratchLife = 52;

        //==== 碑语 ====
        private static int stoneScanIn = 10;
        private static int stoneWhisperIn = 1200;
        private static readonly List<Point> stones = new(12);

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            UpdatePresence();
            if (Main.gamePaused || Main.gameMenu || Presence < 0.02f) {
                return;
            }
            Player lp = Main.LocalPlayer;
            if (lp == null || !lp.active) {
                return;
            }
            float grudge = lp.GetModPlayer<MournfogPlayer>().RedShift;

            UpdateWindLoop();
            UpdateAccents(lp, grudge);
            UpdateMistAndWisps(lp, grudge);
            UpdateScratch(lp);
            UpdateEpitaph(lp);
        }

        private static void UpdatePresence() {
            Player lp = Main.LocalPlayer;
            bool inScene = !Main.gameMenu && lp != null && lp.active
                && lp.ZoneGraveyard && GameModeSystem.BrutalActive;
            //Boss 在场：纯视觉氛围保留但减弱（伤害机制的暂停在 Player/环体处各自把关）
            float target = inScene ? (CWRWorld.HasBoss ? 0.5f : 1f) : 0f;
            Presence = MathHelper.Lerp(Presence, target, 0.03f);
            if (Presence < 0.003f && target <= 0f) {
                Presence = 0f;
            }
        }

        //循环丢失（切场景/音量档变化）就补挂，音量在回调里逐帧走
        private static void UpdateWindLoop() {
            if (!SoundEngine.TryGetActiveSound(windSlot, out _)) {
                windSlot = SoundEngine.PlaySound(WindMoanStyle, null, UpdateWindMoan);
            }
        }

        //风过墓碑的呜咽：随浓雾脉动同呼吸，压低不与原版音乐打架
        private static bool UpdateWindMoan(ActiveSound sound) {
            if (Presence < 0.02f || Main.gameMenu) {
                return false;
            }
            float pulse = FogPulse;
            sound.Volume = Presence * (0.055f + 0.115f * (0.55f + 0.45f * pulse));
            sound.Pitch = -0.62f + 0.06f * pulse;
            sound.Position = null;
            return true;
        }

        /// <summary>一次性点缀：远鸦双啼、远钟、怨聚前兆低语</summary>
        private static void UpdateAccents(Player lp, float grudge) {
            //远鸦：双连短啼，音源挂在高处远处
            if (cawHitsLeft > 0 && --cawHitIn <= 0) {
                cawHitsLeft--;
                cawHitIn = 8;
                Vector2 perch = lp.Center + new Vector2(
                    Main.rand.NextFloat(500f, 900f) * (Main.rand.NextBool() ? 1f : -1f),
                    -Main.rand.NextFloat(250f, 450f));
                SoundEngine.PlaySound(SoundID.Roar with {
                    Volume = 0.14f * Presence,
                    Pitch = cawPitch - (cawHitsLeft == 0 ? 0.06f : 0f),
                    MaxInstances = 3,
                }, perch);
            }
            if (--cawIn <= 0) {
                cawIn = Main.rand.Next(840, 1680);
                cawHitsLeft = 2;
                cawHitIn = 1;
                cawPitch = Main.rand.NextFloat(0.82f, 0.95f);
            }

            //远钟：低沉一声，间隔长
            if (--bellIn <= 0) {
                bellIn = Main.rand.Next(1560, 2880);
                Vector2 far = lp.Center + new Vector2(
                    Main.rand.NextFloat(700f, 1000f) * (Main.rand.NextBool() ? 1f : -1f), -300f);
                SoundEngine.PlaySound(SoundID.Item35 with {
                    Volume = 0.28f * Presence, Pitch = -0.4f, MaxInstances = 2,
                }, far);
            }

            //怨聚前兆：累积过四成后，耳边低语渐清（听觉通道先于环体出现）
            if (grudge > 0.4f && --foreshadowIn <= 0) {
                foreshadowIn = Main.rand.Next(80, 170);
                Vector2 near = lp.Center + Main.rand.NextVector2CircularEdge(1f, 1f)
                    * Main.rand.NextFloat(120f, 260f);
                SoundEngine.PlaySound(SoundID.NPCHit36 with {
                    Volume = (0.05f + 0.2f * grudge) * Presence,
                    Pitch = -0.5f + 0.4f * grudge,
                    MaxInstances = 3,
                }, near);
            }
        }

        /// <summary>霭祭：脉动浓雾（复用 PRT_GhostRainMist）+ 漫飘鬼火（专属 PRT）</summary>
        private static void UpdateMistAndWisps(Player lp, float grudge) {
            float pulse = FogPulse;
            //浓雾：~1.8 团/s，大而慢，透明度随脉动呼吸
            if (--mistIn <= 0) {
                mistIn = Main.rand.Next(26, 44);
                Vector2 pos = lp.Center + new Vector2(
                    Main.rand.NextFloat(-950f, 950f), Main.rand.NextFloat(-160f, 320f));
                Color mist = new Color(96, 110, 100) * ((0.62f + 0.45f * pulse) * Presence);
                PRTLoader.NewParticle<PRT_GhostRainMist>(pos,
                    new Vector2(Main.rand.NextFloat(-0.28f, 0.28f), -0.03f),
                    mist, Main.rand.NextFloat(1f, 1.6f))
                    ?.Configure(Main.rand.Next(170, 250));
            }

            //漫飘鬼火：~0.9 只/s，暗绿；怨聚累积时随 grudge 渐红（预告的预告）
            if (--wispIn <= 0) {
                wispIn = Main.rand.Next(56, 96);
                Vector2 pos = lp.Center + Main.rand.NextVector2CircularEdge(1f, 1f)
                    * Main.rand.NextFloat(240f, 680f);
                Point tile = pos.ToTileCoordinates();
                if (WorldGen.InWorld(tile.X, tile.Y, 10) && !WorldGen.SolidTile(tile.X, tile.Y)) {
                    PRTLoader.NewParticle<PRT_MournfogWisp>(pos,
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -0.1f),
                        Color.White, Main.rand.NextFloat(0.45f, 0.7f))
                        ?.Configure(Main.rand.Next(240, 360), grudge);
                }
            }
        }

        /// <summary>掘响：骨手抓挠尘效 + 闷响，纯视觉惊吓（不伤害、不生成敌怪、无判定）</summary>
        private static void UpdateScratch(Player lp) {
            if (scratchTick >= 0) {
                scratchTick++;
                if (scratchTick >= ScratchLife) {
                    scratchTick = -1;
                    return;
                }
                //抓挠期粉尘：≤2 粒/3 帧的土屑 + 低频骨屑
                if (scratchTick % 3 == 0) {
                    for (int i = 0; i < 2; i++) {
                        Dust dust = Dust.NewDustPerfect(
                            scratchPos + new Vector2(Main.rand.NextFloat(-14f, 14f), 2f),
                            DustID.Dirt, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f),
                                -Main.rand.NextFloat(1f, 2.6f)), 60, default,
                            Main.rand.NextFloat(0.9f, 1.3f));
                        dust.noGravity = false;
                    }
                }
                if (scratchTick % 9 == 0) {
                    Dust bone = Dust.NewDustPerfect(
                        scratchPos + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                        DustID.Bone, new Vector2(Main.rand.NextFloat(-0.8f, 0.8f),
                            -Main.rand.NextFloat(1.4f, 2.4f)), 80, default, 0.75f);
                    bone.noGravity = false;
                }
                //抓挠节拍：两记闷刨
                if (scratchTick == 18 || scratchTick == 36) {
                    SoundEngine.PlaySound(SoundID.Dig with {
                        Volume = 0.3f, Pitch = -0.35f, MaxInstances = 3,
                    }, scratchPos);
                }
                return;
            }

            //惊吓要克制：氛围未满/Boss 在场不排
            if (--scratchIn > 0 || Presence < 0.6f || CWRWorld.HasBoss) {
                return;
            }
            scratchIn = Main.rand.Next(1500, 3300);
            if (TryFindScratchSpot(lp, out Vector2 spot)) {
                scratchPos = spot;
                scratchTick = 0;
                SoundEngine.PlaySound(SoundID.WormDigQuiet with {
                    Volume = 0.55f, Pitch = -0.45f, MaxInstances = 2,
                }, spot);
            }
        }

        /// <summary>在玩家附近找一块可见的裸地表（离脚下有点距离，惊吓不糊脸）</summary>
        private static bool TryFindScratchSpot(Player lp, out Vector2 spot) {
            spot = default;
            Point feet = lp.Bottom.ToTileCoordinates();
            for (int attempt = 0; attempt < 8; attempt++) {
                int sign = Main.rand.NextBool() ? 1 : -1;
                int x = feet.X + sign * Main.rand.Next(9, 33);
                for (int dy = -2; dy < 13; dy++) {
                    int y = feet.Y + dy;
                    if (!WorldGen.InWorld(x, y, 10)) {
                        break;
                    }
                    if (!WorldGen.SolidTile(x, y) || WorldGen.SolidTile(x, y - 1)) {
                        continue;
                    }
                    spot = new Vector2(x * 16f + 8f, y * 16f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>碑语：低频扫描附近墓碑，偶发碑面幽光 + 一句低语（无文本 UI）</summary>
        private static void UpdateEpitaph(Player lp) {
            if (--stoneScanIn <= 0) {
                stoneScanIn = 45;
                ScanTombstones(lp);
            }
            if (--stoneWhisperIn > 0) {
                return;
            }
            if (stones.Count == 0) {
                stoneWhisperIn = 240;//附近没碑，短周期复查
                return;
            }
            stoneWhisperIn = Main.rand.Next(720, 1800);
            Point pick = stones[Main.rand.Next(stones.Count)];
            //墓碑 2x2：锚点取左上角，中心即碑面
            Vector2 center = new(pick.X * 16f + 16f, pick.Y * 16f + 16f);
            if (lp.Center.Distance(center) > 860f) {
                return;
            }
            PRTLoader.NewParticle<PRT_MournfogStoneGlow>(center, Vector2.Zero,
                Color.White, Main.rand.NextFloat(0.9f, 1.2f))?.Configure(140);
            SoundEngine.PlaySound(SoundID.NPCHit36 with {
                Volume = 0.3f, Pitch = -0.32f, MaxInstances = 2,
            }, center);
        }

        /// <summary>屏幕范围墓碑扫描（45 帧一次，锚点=多格结构左上角防重复计数）</summary>
        private static void ScanTombstones(Player lp) {
            stones.Clear();
            Point c = lp.Center.ToTileCoordinates();
            int x0 = Math.Max(10, c.X - 62);
            int x1 = Math.Min(Main.maxTilesX - 10, c.X + 62);
            int y0 = Math.Max(10, c.Y - 36);
            int y1 = Math.Min(Main.maxTilesY - 10, c.Y + 36);
            for (int x = x0; x <= x1; x++) {
                for (int y = y0; y <= y1; y++) {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile || tile.TileType != TileID.Tombstones
                        || tile.TileFrameX % 36 != 0 || tile.TileFrameY != 0) {
                        continue;
                    }
                    stones.Add(new Point(x, y));
                    if (stones.Count >= 12) {
                        return;
                    }
                }
            }
        }

        //光色：在原版墓地灰暗之上再压一层湿冷灰绿（力度克制，不与原版雾滤镜打架）
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (Presence <= 0.001f) {
                return;
            }
            Color duskTile = new(84, 92, 86);
            Color duskBg = new(56, 66, 60);
            tileColor = Color.Lerp(tileColor, duskTile, 0.10f * Presence);
            backgroundColor = Color.Lerp(backgroundColor, duskBg, 0.16f * Presence);
        }

        public override void ClearWorld() {
            Presence = 0f;
            scratchTick = -1;
            stones.Clear();
            cawHitsLeft = 0;
        }
    }
}
