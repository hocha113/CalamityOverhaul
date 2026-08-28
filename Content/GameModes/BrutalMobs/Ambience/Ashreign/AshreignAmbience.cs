using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Ashreign.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Ashreign
{
    /// <summary>
    /// 「烬雪」常态氛围与「恶魔号角」（纯客户端演出）：
    /// 黑灰絮片缓落+橙红火星上升的双向粒子流、岩浆咕嘟与深处轰鸣底噪循环
    /// （镜像 GhostRainAmbience/OldNetAmbience 的槽位管理）、烬暴风声随临近度转烈
    /// （听觉预告通道）、极低频远处号角回响+轻屏震。
    /// 另持岩浆液面扫描缓存，喂热浪扭曲层与咕嘟声定位。
    /// 进入淡入、离开淡出，Main.gamePaused 时 PostUpdateEverything 天然不推进
    /// </summary>
    internal class AshreignAmbience : ModSystem
    {
        /// <summary>本地玩家的烬雪在场强度 0~1（Boss 在场压至 0.55，纯视觉减弱）</summary>
        internal static float Presence { get; private set; }

        /// <summary>最近烬暴的临近度 0~1（远霾逼近的听觉预告通道，入带即 1）</summary>
        internal static float StormLoom { get; private set; }

        /// <summary>本机玩家的烬幕强度 0~1（转读 AshreignPlayer 暴露，喂压光与粒子加密）</summary>
        internal static float StormVeil { get; private set; }

        //==== 岩浆液面扫描缓存（本机屏幕级视觉缓存，非逐玩家游戏状态）====
        /// <summary>热源上限（与 ThermalHeatHaze.fx MAX_SOURCES=8 留裕量）</summary>
        internal const int MaxHeatSources = 6;
        /// <summary>热源表：xy=世界坐标 z=强度0~1 w=半径px</summary>
        internal static readonly Vector4[] HeatSources = new Vector4[MaxHeatSources];
        internal static int HeatSourceCount { get; private set; }
        /// <summary>距本机玩家最近的岩浆液面点（咕嘟声定位）</summary>
        internal static Vector2 NearestLavaPos { get; private set; }
        /// <summary>岩浆贴近度 0~1（700px 内线性走满）</summary>
        internal static float LavaNearness { get; private set; }

        //液面扫描分频与分桶暂存（热路径零分配）
        private const int LavaScanGap = 12;
        private const int BucketCols = 12;
        private const int MaxBuckets = 32;
        private static readonly int[] bucketCount = new int[MaxBuckets];
        private static readonly float[] bucketSumX = new float[MaxBuckets];
        private static readonly float[] bucketSumY = new float[MaxBuckets];
        private static int lavaScanTimer;

        //==== 环境声循环槽（镜像 OldNetAmbience 的 SlotId+回调惯例）====
        private static SlotId gurgleSlot;
        private static SlotId rumbleSlot;
        private static SlotId stormWindSlot;
        private static readonly SoundStyle GurgleStyle =
            SoundID.Lavafall with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle RumbleStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle StormWindStyle =
            SoundID.BlizzardStrongLoop with { IsLooped = true, MaxInstances = 1 };

        //==== 点缀声与号角计时 ====
        private static int bloopTimer;
        private static int hornTimer = 4800;
        private static int hornEchoTimer;
        private static Vector2 hornEchoPos;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            //在场包络：残酷模式 + 地狱高度带；Boss 在场只保留减弱的纯视觉氛围
            Player player = Main.LocalPlayer;
            bool inBiome = !Main.gameMenu && player != null && player.active
                && Ashreign.AmbienceActive(player);
            float target = inBiome ? (CWRWorld.HasBoss ? 0.55f : 1f) : 0f;
            Presence = Math.Abs(target - Presence) < 0.004f
                ? target : MathHelper.Lerp(Presence, target, 0.045f);

            //烬幕强度转读本机玩家暴露（AshreignPlayer 已含遮蔽与淡出）
            StormVeil = player != null && player.TryGetModPlayer(out AshreignPlayer ap)
                ? ap.StormExposure : 0f;

            UpdateStormLoom(player);

            if (Presence <= 0.004f) {
                HeatSourceCount = 0;
                LavaNearness = 0f;
                return;
            }

            UpdateLavaScan(player);
            UpdateSoundLoops();
            UpdateAshfall();
            UpdateBloops();
            UpdateHorn(player);
        }

        /// <summary>最近烬暴临近度：带内即 1，带外 2200px 线性衰减（听觉预告通道）</summary>
        private static void UpdateStormLoom(Player player) {
            float loom = 0f;
            //残值未归零或近两帧有烬暴盖戳才扫表；空世界零成本，时停中靠残值闩锁继续找到冻结的烬暴
            if (player != null && player.active
                && (StormLoom > 0.005f || AshreignAshStormProj.PresenceStamp.ActiveWithin())) {
                int stormType = ModContent.ProjectileType<AshreignAshStormProj>();
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type != stormType) {
                        continue;
                    }
                    float outside = Math.Abs(player.Center.X - proj.Center.X) - AshreignAshStormProj.HalfWidth;
                    float near = MathHelper.Clamp(1f - outside / 2200f, 0f, 1f)
                        * AshreignAshStormProj.Envelope(proj);
                    if (near > loom) {
                        loom = near;
                    }
                }
            }
            StormLoom = Math.Abs(loom - StormLoom) < 0.005f
                ? loom : MathHelper.Lerp(StormLoom, loom, 0.06f);
        }

        //==================== 岩浆液面扫描（分频，喂热浪与咕嘟）====================

        private static void UpdateLavaScan(Player player) {
            if (--lavaScanTimer > 0) {
                return;
            }
            lavaScanTimer = LavaScanGap;

            Array.Clear(bucketCount, 0, MaxBuckets);
            Array.Clear(bucketSumX, 0, MaxBuckets);
            Array.Clear(bucketSumY, 0, MaxBuckets);

            int left = (int)(Main.screenPosition.X / 16f) - 8;
            int right = (int)((Main.screenPosition.X + Main.screenWidth) / 16f) + 8;
            int top = (int)(Main.screenPosition.Y / 16f) - 4;
            int bottom = (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + 6;

            float nearestSq = float.MaxValue;
            Vector2 nearest = default;

            //隔列采样，每列取第一处液面
            for (int x = left; x <= right; x += 2) {
                for (int y = top; y <= bottom; y++) {
                    if (!Ashreign.IsLavaSurface(x, y)) {
                        continue;
                    }
                    int bucket = Math.Clamp((x - left) / BucketCols, 0, MaxBuckets - 1);
                    bucketCount[bucket]++;
                    bucketSumX[bucket] += x * 16f + 8f;
                    bucketSumY[bucket] += y * 16f;

                    Vector2 pos = new(x * 16f + 8f, y * 16f);
                    float distSq = Vector2.DistanceSquared(pos, player.Center);
                    if (distSq < nearestSq) {
                        nearestSq = distSq;
                        nearest = pos;
                    }
                    break;
                }
            }

            //聚簇成热源：宽度不足 3 个采样列的浅坑不成源
            HeatSourceCount = 0;
            for (int i = 0; i < MaxBuckets && HeatSourceCount < MaxHeatSources; i++) {
                if (bucketCount[i] < 3) {
                    continue;
                }
                float cx = bucketSumX[i] / bucketCount[i];
                float cy = bucketSumY[i] / bucketCount[i];
                float intensity = MathHelper.Clamp(bucketCount[i] / 9f, 0.3f, 0.85f);
                float radius = MathHelper.Clamp(bucketCount[i] * 34f, 110f, 330f);
                HeatSources[HeatSourceCount++] = new Vector4(cx, cy, intensity, radius);
            }

            if (nearestSq < float.MaxValue) {
                NearestLavaPos = nearest;
                LavaNearness = MathHelper.Clamp(1f - MathF.Sqrt(nearestSq) / 700f, 0f, 1f);
            }
            else {
                LavaNearness = 0f;
            }
        }

        //==================== 环境声循环 ====================

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateSoundLoops() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(gurgleSlot, out _)) {
                gurgleSlot = SoundEngine.PlaySound(GurgleStyle, null, UpdateGurgle);
            }
            if (!SoundEngine.TryGetActiveSound(rumbleSlot, out _)) {
                rumbleSlot = SoundEngine.PlaySound(RumbleStyle, null, UpdateRumble);
            }
            if (StormLoom > 0.02f && !SoundEngine.TryGetActiveSound(stormWindSlot, out _)) {
                stormWindSlot = SoundEngine.PlaySound(StormWindStyle, null, UpdateStormWind);
            }
        }

        //岩浆咕嘟：音量随岩浆贴近度，声源钉在最近液面上
        private static bool UpdateGurgle(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0.004f) {
                return false;
            }
            sound.Volume = MathHelper.Clamp(Presence * LavaNearness * 0.42f, 0f, 0.42f);
            sound.Pitch = -0.25f;
            sound.Position = LavaNearness > 0.01f ? NearestLavaPos : null;
            return true;
        }

        //深处轰鸣：极低频闷响底噪，不定位；烬暴临近时整体抬一档
        private static bool UpdateRumble(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0.004f) {
                return false;
            }
            sound.Volume = MathHelper.Clamp(Presence * (0.15f + 0.10f * StormLoom), 0f, 0.3f);
            sound.Pitch = -0.85f;
            sound.Position = null;
            return true;
        }

        //烬暴风声：远霾逼近渐强（预告听觉通道），入幕转烈转沉
        private static bool UpdateStormWind(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0.004f || StormLoom <= 0.01f) {
                return false;
            }
            sound.Volume = MathHelper.Clamp(Presence * (0.55f * StormLoom + 0.30f * StormVeil), 0f, 0.8f);
            sound.Pitch = -0.55f + 0.22f * StormVeil;
            sound.Position = null;
            return true;
        }

        //==================== 烬雪双向粒子流 ====================

        /// <summary>
        /// 黑灰絮片缓落 + 橙红火星上升。常态合计约 ≤23 粒/秒，
        /// 烬幕内絮片加密，Boss 在场随 Presence 整体减半
        /// </summary>
        private static void UpdateAshfall() {
            if (Main.gamePaused) {
                return;
            }

            //絮片：自屏顶缓落（≈10/s，烬幕内上探）
            float flakeChance = Presence * 0.16f * (1f + 1.4f * StormVeil);
            if (Main.rand.NextFloat() < flakeChance) {
                Vector2 pos = new(
                    Main.screenPosition.X + Main.rand.NextFloat(-60f, Main.screenWidth + 60f),
                    Main.screenPosition.Y - 40f);
                float shade = Main.rand.NextFloat(0.75f, 1.05f);
                PRTLoader.NewParticle<PRT_AshreignFlake>(pos,
                    new Vector2(Main.windSpeedCurrent, Main.rand.NextFloat(0.2f, 0.5f)),
                    Ashreign.AshDark * (0.72f * shade),
                    Main.rand.NextFloat(0.7f, 1.5f))
                    ?.Configure(Main.rand.Next(160, 240),
                        Main.rand.NextFloat(0.4f, 0.75f), Main.rand.NextFloat(0.18f, 0.4f));
            }

            //火星：六成自岩浆热源升起，其余自屏内下半随机（≈13/s）
            float emberChance = Presence * 0.22f;
            if (Main.rand.NextFloat() < emberChance) {
                Vector2 pos;
                if (HeatSourceCount > 0 && Main.rand.NextFloat() < 0.6f) {
                    Vector4 src = HeatSources[Main.rand.Next(HeatSourceCount)];
                    pos = new Vector2(src.X + Main.rand.NextFloat(-src.W, src.W) * 0.8f,
                        src.Y - Main.rand.NextFloat(0f, 10f));
                }
                else {
                    pos = new Vector2(
                        Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth),
                        Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.45f, 1f));
                }
                Color warm = Main.rand.NextBool(3)
                    ? new Color(255, 108, 40) : Ashreign.EmberWarm;
                PRTLoader.NewParticle<PRT_DefEmber>(pos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.7f, 1.7f)),
                    warm, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(70, 120), -0.006f, 0.995f);
            }
        }

        //==================== 点缀声与号角 ====================

        /// <summary>岩浆泡破的零星咕噜（贴近岩浆才有，声源在液面）</summary>
        private static void UpdateBloops() {
            if (--bloopTimer > 0) {
                return;
            }
            bloopTimer = Main.rand.Next(90, 220);
            if (LavaNearness < 0.12f) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Drip with {
                Volume = 0.34f * LavaNearness,
                Pitch = Main.rand.NextFloat(-0.62f, -0.3f),
                MaxInstances = 3,
            }, NearestLavaPos);
            if (Main.rand.NextBool(4)) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with {
                    Volume = 0.16f * LavaNearness,
                    Pitch = -0.55f,
                    MaxInstances = 2,
                }, NearestLavaPos);
            }
        }

        /// <summary>
        /// 恶魔号角（可选第四项，纯氛围）：极低频远处号角回响 + 轻屏震，
        /// 26 帧后补一记更远的回声；Boss 在场静默
        /// </summary>
        private static void UpdateHorn(Player player) {
            if (hornEchoTimer > 0 && --hornEchoTimer == 0) {
                SoundEngine.PlaySound(SoundID.Roar with {
                    Volume = 0.12f, Pitch = -0.98f, MaxInstances = 2,
                }, hornEchoPos);
            }

            if (--hornTimer > 0) {
                return;
            }
            if (Presence < 0.55f || CWRWorld.HasBoss) {
                hornTimer = 600;
                return;
            }
            hornTimer = 5400 + Main.rand.Next(4500);

            Vector2 far = player.Center + new Vector2(
                Main.rand.NextFloat(900f, 1500f) * (Main.rand.NextBool() ? 1f : -1f),
                -Main.rand.NextFloat(180f, 480f));
            SoundEngine.PlaySound(SoundID.Roar with {
                Volume = 0.26f, Pitch = -0.92f, MaxInstances = 2,
            }, far);
            player.CWR()?.GetScreenShake(1.3f);
            hornEchoTimer = 26;
            hornEchoPos = far + new Vector2(Main.rand.NextFloat(-300f, 300f), -160f);
        }

        /// <summary>烬幕压光：氛围级视野压迫，禁真黑屏（地狱无日光，压光是烬幕的主力杠杆）</summary>
        public override void ModifyLightingBrightness(ref float scale) {
            if (StormVeil > 0.002f) {
                scale *= 1f - 0.28f * StormVeil;
            }
        }

        public override void ClearWorld() {
            Presence = 0f;
            StormLoom = 0f;
            StormVeil = 0f;
            HeatSourceCount = 0;
            LavaNearness = 0f;
            lavaScanTimer = 0;
            bloopTimer = 0;
            hornTimer = 4800;
            hornEchoTimer = 0;
        }
    }
}
