using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Fleshfen
{
    /// <summary>
    /// 猩红之地「血雾」「脉搏地动」「血沼泡」的客户端氛围核心（地表与地下同管，检测 ZoneCrimson）。
    /// 全部状态是本机屏幕的视图量（镜像 GhostRainAmbience 的在场强度惯例），不含任何逐玩家游戏数据。
    /// 心跳节拍随本机玩家血量比例降低而加快（与 Nyxdepth 深渊的随深度心跳分界：这里只看血量）；
    /// 音量随深入地下加深。环境音循环镜像 OldNetAmbience 的 SlotId+回调生命周期
    /// </summary>
    internal class FleshfenAmbience : ModSystem
    {
        //==== 在场与节拍（本机视图量）====
        /// <summary>血雾在场强度 0~1（进出群系缓升缓降；Boss 在场压半）</summary>
        internal static float Presence { get; private set; }
        /// <summary>拍点包络：每拍置 1 后指数衰减，供音量/微光/雾层呼吸共用</summary>
        internal static float BeatEnvelope { get; private set; }
        /// <summary>深入度 0~1：地表 0，岩石层及以下 1（音量随深入）</summary>
        internal static float DepthFactor { get; private set; }
        /// <summary>恐惧度 0~1：血量越低越高（平滑过的）</summary>
        internal static float FearEased { get; private set; }

        /// <summary>心跳间隔（帧）：满血 66（约 55bpm）→ 濒死 28（约 128bpm）</summary>
        private const int BeatIntervalFull = 66;
        private const int BeatIntervalPanic = 28;
        /// <summary>次拍（dub）滞后帧数，lub-dub 双响才读得出心跳</summary>
        private const int DubDelayFrames = 9;

        private static int beatTimer;
        private static int dubTimer;

        //==== 环境音循环槽（镜像 OldNetAmbience 惯例）====
        private static SlotId heartDroneSlot;
        private static SlotId mistWindSlot;
        private static readonly SoundStyle HeartDroneStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle MistWindStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        //==== 肉块地形脉动微光 ====
        internal struct PulseSpot
        {
            internal bool Active;
            internal Vector2 Pos;
            internal int Life;
            internal int MaxLife;
            internal float Strength;
        }
        internal static readonly PulseSpot[] PulseSpots = new PulseSpot[6];

        //==== 血沼泡（血水面纯视觉）====
        internal struct MireBubble
        {
            internal bool Active;
            internal Vector2 Pos;
            internal int Life;
            internal int MaxLife;
            internal float Size;
        }
        internal static readonly MireBubble[] MireBubbles = new MireBubble[4];
        private static int bubbleScanTimer;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            UpdatePresence();
            if (Presence <= 0.003f) {
                return;
            }

            Player player = Main.LocalPlayer;
            UpdateDepthAndFear(player);
            UpdateHeartbeat(player);
            UpdateAmbientLoops();
            UpdatePulseSpots();
            UpdateMireBubbles();
        }

        /// <summary>在场强度：进出群系约 1s 缓升缓降；Boss 在场纯视觉减弱（伤害机制由血露那头自行停摆）</summary>
        private static void UpdatePresence() {
            bool inZone = GameModeSystem.BrutalActive && !Main.gameMenu
                && Main.LocalPlayer.active && Main.LocalPlayer.ZoneCrimson;
            float target = inZone ? (CWRWorld.HasBoss ? 0.55f : 1f) : 0f;
            Presence = MathHelper.Lerp(Presence, target, 0.035f);
            if (Presence < 0.003f && target <= 0f) {
                Presence = 0f;
            }
            BeatEnvelope *= 0.90f;
        }

        private static void UpdateDepthAndFear(Player player) {
            float py = player.Center.Y / 16f;
            float surface = (float)Main.worldSurface;
            float rock = (float)Main.rockLayer;
            DepthFactor = MathHelper.Clamp((py - surface) / Math.Max(1f, rock - surface), 0f, 1f);

            //血量越低心越急；死亡时归零，别让尸体旁的心跳狂奔
            float fear = 0f;
            if (!player.dead) {
                float hpRatio = MathHelper.Clamp(player.statLife / (float)Math.Max(1, player.statLifeMax2), 0f, 1f);
                fear = 1f - hpRatio;
            }
            float eased = fear * fear * (3f - 2f * fear);
            FearEased = MathHelper.Lerp(FearEased, eased, 0.1f);
        }

        /// <summary>「脉搏地动」：全群系统一节拍，每拍 lub-dub 双响 + 极轻屏震 + 地面粒子涟漪（纯氛围无伤害）</summary>
        private static void UpdateHeartbeat(Player player) {
            if (dubTimer > 0 && --dubTimer == 0) {
                PlayThump(0.68f, -0.78f);
            }
            if (--beatTimer > 0) {
                return;
            }
            beatTimer = BeatIntervalFull - (int)((BeatIntervalFull - BeatIntervalPanic) * FearEased);
            if (Presence < 0.15f || player.dead) {
                return;
            }

            BeatEnvelope = 1f;
            PlayThump(1f, -0.92f);
            dubTimer = DubDelayFrames;
            //极轻屏震：血少时更沉（GetScreenShake 自带用户配置门与快速衰减）
            player.CWR()?.GetScreenShake((0.42f + 0.85f * FearEased) * Presence);
            SpawnGroundRipple(player);
            TrySpawnPulseSpot();
        }

        /// <summary>心跳闷响：非定位环境声，音量随深入与在场强度</summary>
        private static void PlayThump(float volumeScale, float pitch) {
            if (Presence < 0.1f) {
                return;
            }
            float volume = Presence * (0.24f + 0.22f * DepthFactor) * volumeScale;
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = volume,
                Pitch = pitch,
                MaxInstances = 4,
            });
        }

        /// <summary>拍点地面涟漪：脚下地面向两侧荡开一圈血尘（6 粒/拍）</summary>
        private static void SpawnGroundRipple(Player player) {
            if (Presence < 0.3f) {
                return;
            }
            //只认脚下 8 格内的地面，悬空/深井上方不放
            int tileX = (int)(player.Bottom.X / 16f);
            int startY = (int)(player.Bottom.Y / 16f);
            float groundYPx = -1f;
            for (int dy = 0; dy < 8; dy++) {
                if (!WorldGen.InWorld(tileX, startY + dy, 10)) {
                    return;
                }
                if (WorldGen.SolidTile(tileX, startY + dy)) {
                    groundYPx = (startY + dy) * 16f;
                    break;
                }
            }
            if (groundYPx < 0f) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                float offset = Main.rand.NextFloat(14f, 64f) * (i % 2 == 0 ? 1f : -1f);
                Vector2 pos = new(player.Bottom.X + offset, groundYPx - 2f);
                if (i < 4) {
                    Dust glow = Dust.NewDustPerfect(pos, DustID.CrimsonTorch,
                        new Vector2(Math.Sign(offset) * Main.rand.NextFloat(0.6f, 1.3f), -Main.rand.NextFloat(0.2f, 0.6f)),
                        130, default, 0.95f);
                    glow.noGravity = true;
                }
                else {
                    Dust.NewDustPerfect(pos, DustID.Blood,
                        new Vector2(Math.Sign(offset) * Main.rand.NextFloat(0.3f, 0.9f), -Main.rand.NextFloat(0.8f, 1.6f)),
                        90, default, Main.rand.NextFloat(0.9f, 1.2f));
                }
            }
        }

        //==== 环境音循环：丢失即补挂，音量在回调里逐帧走（进入淡入、离开淡出由 Presence 承担）====

        private static void UpdateAmbientLoops() {
            if (Main.gameMenu || Presence < 0.05f) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(heartDroneSlot, out _)) {
                heartDroneSlot = SoundEngine.PlaySound(HeartDroneStyle, null, UpdateHeartDrone);
            }
            if (!SoundEngine.TryGetActiveSound(mistWindSlot, out _)) {
                mistWindSlot = SoundEngine.PlaySound(MistWindStyle, null, UpdateMistWind);
            }
        }

        /// <summary>心跳低鸣：肉壁腔体的低频嗡鸣，地下更响，并随拍点轻微起伏（泵血感）</summary>
        private static bool UpdateHeartDrone(ActiveSound sound) {
            if (Presence < 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = Presence * (0.10f + 0.15f * DepthFactor) * (0.85f + 0.30f * BeatEnvelope);
            sound.Pitch = -0.86f;
            sound.Position = null;
            return true;
        }

        /// <summary>湿风底噪：血雾在流动的证据，很轻</summary>
        private static bool UpdateMistWind(ActiveSound sound) {
            if (Presence < 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = Presence * 0.055f;
            sound.Pitch = -0.45f;
            sound.Position = null;
            return true;
        }

        //==== 肉块地形脉动微光：拍点上偶发点一处猩红瓦面，与心跳同呼吸 ====

        /// <summary>猩红家族瓦（自然分布的肉壤面）</summary>
        private static bool IsFleshTile(int type)
            => type == TileID.Crimstone || type == TileID.CrimsonGrass
            || type == TileID.Crimsand || type == TileID.CrimsonHardenedSand
            || type == TileID.CrimsonJungleGrass;

        private static void TrySpawnPulseSpot() {
            if (Presence < 0.2f || Main.rand.Next(5) >= 3) {
                return;
            }
            int slot = -1;
            for (int i = 0; i < PulseSpots.Length; i++) {
                if (!PulseSpots[i].Active) {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) {
                return;
            }
            //屏内随机采样：命中裸露的猩红瓦面才点亮
            int baseX = (int)(Main.screenPosition.X / 16f);
            int baseY = (int)(Main.screenPosition.Y / 16f);
            int spanX = Main.screenWidth / 16 + 1;
            int spanY = Main.screenHeight / 16 + 1;
            for (int attempt = 0; attempt < 14; attempt++) {
                int x = baseX + Main.rand.Next(spanX);
                int y = baseY + Main.rand.Next(spanY);
                if (!WorldGen.InWorld(x, y, 10) || !WorldGen.SolidTile(x, y)) {
                    continue;
                }
                if (!IsFleshTile(Main.tile[x, y].TileType)) {
                    continue;
                }
                bool exposed = !WorldGen.SolidTile(x, y - 1) || !WorldGen.SolidTile(x, y + 1)
                    || !WorldGen.SolidTile(x - 1, y) || !WorldGen.SolidTile(x + 1, y);
                if (!exposed) {
                    continue;
                }
                Vector2 pos = new(x * 16f + 8f, y * 16f + 8f);
                PulseSpots[slot] = new PulseSpot {
                    Active = true,
                    Pos = pos,
                    Life = 0,
                    MaxLife = 44 + Main.rand.Next(20),
                    Strength = Main.rand.NextFloat(0.7f, 1.1f),
                };
                for (int d = 0; d < 2; d++) {
                    Dust crawl = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(6f, 6f),
                        DustID.CrimsonTorch, Main.rand.NextVector2Circular(0.4f, 0.4f), 140, default, 0.8f);
                    crawl.noGravity = true;
                }
                return;
            }
        }

        private static void UpdatePulseSpots() {
            for (int i = 0; i < PulseSpots.Length; i++) {
                if (!PulseSpots[i].Active) {
                    continue;
                }
                PulseSpots[i].Life++;
                if (PulseSpots[i].Life >= PulseSpots[i].MaxLife) {
                    PulseSpots[i].Active = false;
                    continue;
                }
                float env = SpotEnv(in PulseSpots[i]);
                Lighting.AddLight(PulseSpots[i].Pos, new Vector3(0.34f, 0.055f, 0.065f) * env);
            }
        }

        /// <summary>脉动包络：寿命正弦拱 × 心跳呼吸（渲染层画微光时取同一条）</summary>
        internal static float SpotEnv(in PulseSpot spot) {
            float arc = MathF.Sin(MathF.PI * spot.Life / spot.MaxLife);
            return arc * (0.55f + 0.45f * BeatEnvelope) * spot.Strength * Presence;
        }

        //==== 血沼泡：血水面冒泡爆裂（纯视觉，低频）====

        private static void UpdateMireBubbles() {
            //推进在途泡
            for (int i = 0; i < MireBubbles.Length; i++) {
                if (!MireBubbles[i].Active) {
                    continue;
                }
                MireBubbles[i].Life++;
                if (MireBubbles[i].Life < MireBubbles[i].MaxLife) {
                    continue;
                }
                MireBubbles[i].Active = false;
                PopBubble(MireBubbles[i].Pos, MireBubbles[i].Size);
            }

            if (Presence < 0.3f || --bubbleScanTimer > 0) {
                return;
            }
            bubbleScanTimer = 26;
            int slot = -1;
            for (int i = 0; i < MireBubbles.Length; i++) {
                if (!MireBubbles[i].Active) {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) {
                return;
            }
            //屏内找血水面（液面上方无液体的水瓦）
            int baseX = (int)(Main.screenPosition.X / 16f);
            int baseY = (int)(Main.screenPosition.Y / 16f);
            int spanX = Main.screenWidth / 16 + 1;
            int spanY = Main.screenHeight / 16 + 1;
            for (int attempt = 0; attempt < 10; attempt++) {
                int x = baseX + Main.rand.Next(spanX);
                int y = baseY + Main.rand.Next(spanY);
                if (!WorldGen.InWorld(x, y, 10)) {
                    continue;
                }
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.LiquidAmount < 64 || tile.LiquidType != LiquidID.Water) {
                    continue;
                }
                if (Framing.GetTileSafely(x, y - 1).LiquidAmount > 0) {
                    continue;
                }
                float surfaceY = y * 16f + (255 - tile.LiquidAmount) * 16f / 255f;
                MireBubbles[slot] = new MireBubble {
                    Active = true,
                    Pos = new Vector2(x * 16f + Main.rand.NextFloat(3f, 13f), surfaceY + 2f),
                    Life = 0,
                    MaxLife = 34 + Main.rand.Next(20),
                    Size = Main.rand.NextFloat(0.7f, 1.2f),
                };
                return;
            }
        }

        /// <summary>泡破：溅起几粒血滴 + 黏稠滴响（5 粒/次）</summary>
        private static void PopBubble(Vector2 pos, float size) {
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.28f, Pitch = -0.15f, MaxInstances = 4 }, pos);
            for (int i = 0; i < 4; i++) {
                Dust.NewDustPerfect(pos, DustID.Blood,
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(1.6f, 3.2f)) * size,
                    70, default, Main.rand.NextFloat(1f, 1.3f));
            }
            Dust mist = Dust.NewDustPerfect(pos, DustID.CrimsonTorch,
                new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.8f)), 150, default, 0.8f);
            mist.noGravity = true;
        }

        //==== 全局染色：血雾压顶（地表日光染出干血色，幅度克制）====

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (Presence <= 0.001f) {
                return;
            }
            Color hazeTile = new(168, 108, 100);
            Color hazeBg = new(96, 44, 48);
            tileColor = Color.Lerp(tileColor, hazeTile, Presence * 0.16f);
            backgroundColor = Color.Lerp(backgroundColor, hazeBg, Presence * 0.26f);
        }

        public override void ClearWorld() {
            Presence = 0f;
            BeatEnvelope = 0f;
            FearEased = 0f;
            beatTimer = 0;
            dubTimer = 0;
            bubbleScanTimer = 0;
            for (int i = 0; i < PulseSpots.Length; i++) {
                PulseSpots[i].Active = false;
            }
            for (int i = 0; i < MireBubbles.Length; i++) {
                MireBubbles[i].Active = false;
            }
        }
    }
}
