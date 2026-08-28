using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiame.Backgrounds;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame
{
    /// <summary>
    /// 鬼雨场景包络与氛围泵：<see cref="Presence"/> 淡入淡出、湿墨冷灰青光照改写、
    /// 满幕黑雨/贴地潮雾/稀有脸痕/远雷，全部吃同一条风暴脉动 <see cref="StormPulse"/>。<br/>
    /// 风暴脉动是 uGust 语义：一条慢包络同源驱动雨密度与斜度、天幕雷闪节奏、水面溅环，
    /// 整个世界作为一场风暴同呼吸，不做各自为政的随机数。<br/>
    /// 色板纪律：冷灰青/尸斑青/灰白，禁红禁暖（与鬼湖夜雨菜单同律）；纯客户端表现
    /// </summary>
    internal class KiameAmbience : ModSystem
    {
        //湿墨色板：物块压向冷灰青，背景沉进近黑的湿夜
        internal static Color RainTile = new(96, 114, 120);
        internal static Color RainBackground = new(24, 31, 35);
        //雨滴：灰白尸雨与尸斑青，与鬼雨叠加层同源
        private static readonly Color RainPale = new(170, 185, 190);
        private static readonly Color RainCorpse = new(140, 170, 165);
        private static readonly Color MistDamp = new(58, 66, 70);

        /// <summary>物块/背景染色强度（1=完全接管原版日光）</summary>
        internal static float TileTintStrength = 0.92f;
        internal static float BackgroundTintStrength = 0.95f;
        /// <summary>整体压沉幅度：这世界没有灯，雷闪是唯一的亮</summary>
        internal static float Dim = 0.32f;

        //基准雨密度与雷隔（帧）：脉动在其上加减
        private const float RainDensityBase = 0.9f;
        private const int ThunderMinBase = 420;
        private const int ThunderMaxBase = 860;

        private static float presence;
        private static float stormPulse;
        private static float dropCarry;
        private static int thunderTimer;
        //雷声相对闪光的延迟帧数，光先于声的距离感
        private static int thunderSoundDelay;

        /// <summary>0~1 场景在场包络；天幕、雨、氛围粒子共用一条</summary>
        internal static float Presence => presence;

        /// <summary>0~1 风暴脉动（uGust 语义）：雨帘/雷闪/水面溅环同源呼吸</summary>
        internal static float StormPulse => stormPulse;

        /// <summary>0~1 当前雨密度：雨帘泵与水面溅环共用同一口径</summary>
        internal static float RainDensity01 => MathHelper.Clamp(
            presence * RainDensityBase * (0.7f + stormPulse * 0.6f), 0f, 1f);

        public override void OnWorldLoad() {
            presence = 0f;
            dropCarry = 0f;
            thunderTimer = 240;
            thunderSoundDelay = 0;
            if (!Main.dedServ && KiameWorld.Active) {
                UI.KiameEntryReveal.Arm();
            }
        }

        public override void OnWorldUnload() {
            presence = 0f;
            stormPulse = 0f;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            bool want = KiameWorld.Active;
            presence = MathHelper.Lerp(presence, want ? 1f : 0f, want ? 0.06f : 0.12f);
            if (!want && presence < 0.004f) {
                presence = 0f;
                return;
            }
            UpdateStormPulse();
            UpdateRainFx();
            UpdateThunder();
        }

        //风暴脉动：双周期正弦叠一层慢噪，永远在 0.15~1 间游走，不读作节拍器
        private static void UpdateStormPulse() {
            float t = (float)Main.timeForVisualEffects * 0.016f;
            float wave = MathF.Sin(t * 0.117f) * 0.5f + MathF.Sin(t * 0.043f + 1.7f) * 0.5f;
            float target = MathHelper.Clamp(0.575f + wave * 0.425f, 0.15f, 1f);
            stormPulse = MathHelper.Lerp(stormPulse, target, 0.02f);
        }

        /// <summary>满幕黑雨 + 贴地潮雾 + 稀有脸痕，密度吃在场包络与风暴脉动</summary>
        private static void UpdateRainFx() {
            if (!KiameWorld.Active || Main.gameMenu) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }

            float density = RainDensity01;
            if (density < 0.02f) {
                return;
            }

            SpawnRainBand(density);

            if (density > 0.2f && Main.GameUpdateCount % 4 == 0) {
                SpawnMist(player);
                if (density > 0.55f) {
                    SpawnMist(player);
                }
            }

            //雨幕稀有脸痕：暴雨压顶时才偶露一面
            if (density > 0.85f && Main.rand.NextBool(60)) {
                SpawnFaceStreak();
            }
        }

        /// <summary>满幕雨帘：约 0.02 滴/像素宽/帧 @密度1，风向按宏观种子定相、斜度吃脉动</summary>
        private static void SpawnRainBand(float density) {
            float left = Main.screenPosition.X - 160f;
            float right = Main.screenPosition.X + Main.screenWidth + 160f;

            dropCarry += density * 0.02f * (right - left);
            int count = Math.Min((int)dropCarry, 72);
            dropCarry -= count;
            //进量超帧上限时截断积欠，防高脉动下无限攒债
            dropCarry = Math.Min(dropCarry, 30f);
            if (count <= 0) {
                return;
            }

            float wind = MathF.Sin(Gen.KiameMetrics.MacroSeed % 255 * 0.37f) * 1.4f
                * (0.5f + stormPulse);
            for (int i = 0; i < count; i++) {
                Vector2 pos = new(Main.rand.NextFloat(left, right),
                    Main.screenPosition.Y - Main.rand.NextFloat(10f, 220f));
                Vector2 vel = new(wind + Main.rand.NextFloat(-0.35f, 0.35f),
                    Main.rand.NextFloat(11.5f, 17.5f));
                Color color = (Main.rand.NextBool(7) ? RainCorpse : RainPale)
                    * Main.rand.NextFloat(0.42f, 0.65f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos, vel, color,
                    Main.rand.NextFloat(0.8f, 1.25f))
                    ?.Configure(Main.rand.Next(70, 110), vel.X);
            }
        }

        /// <summary>贴地潮雾，探不到地面不生</summary>
        private static void SpawnMist(Player player) {
            float x = player.Center.X + Main.rand.NextFloat(
                -Main.screenWidth * 0.55f - 200f, Main.screenWidth * 0.55f + 200f);
            if (!TryFindGround(x, player.Center.Y - 60f, out float groundY)) {
                return;
            }
            Vector2 pos = new(x, groundY - Main.rand.NextFloat(6f, 40f));
            Vector2 vel = new(Main.rand.NextFloat(-0.35f, 0.35f), Main.rand.NextFloat(-0.08f, 0f));
            PRTLoader.NewParticle<PRT_GhostRainMist>(pos, vel,
                MistDamp * Main.rand.NextFloat(0.75f, 1f),
                Main.rand.NextFloat(0.7f, 1.25f))
                ?.Configure(Main.rand.Next(90, 160));
        }

        /// <summary>雨幕稀有的脸痕竖丝</summary>
        private static void SpawnFaceStreak() {
            Vector2 pos = new(
                Main.rand.NextFloat(Main.screenPosition.X + 60f,
                    Main.screenPosition.X + Main.screenWidth - 60f),
                Main.screenPosition.Y + Main.rand.NextFloat(60f, 280f));
            PRTLoader.NewParticle<PRT_GhostRainFaceStreak>(pos,
                new Vector2(0f, Main.rand.NextFloat(1.6f, 2.4f)),
                RainPale * 0.5f, Main.rand.NextFloat(0.85f, 1.15f))
                ?.Configure(Main.rand.Next(50, 74));
        }

        //远雷：天幕云底先闪惨白，雷声隔十几到四十帧才到；脉动越高雷越频越沉
        private static void UpdateThunder() {
            if (!KiameWorld.Active || Main.gameMenu) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }

            if (--thunderTimer <= 0) {
                float squeeze = 1f - stormPulse * 0.45f;
                thunderTimer = Main.rand.Next(
                    (int)(ThunderMinBase * squeeze), (int)(ThunderMaxBase * squeeze));
                KiameSky.NotifyThunder();
                thunderSoundDelay = Main.rand.Next(15, 40);
            }
            if (thunderSoundDelay > 0 && --thunderSoundDelay == 0) {
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Pitch = Main.rand.NextFloat(-1f, -0.75f),
                    Volume = Main.rand.NextFloat(0.24f, 0.42f) * (0.8f + stormPulse * 0.4f),
                    MaxInstances = 3,
                }, player.Center + new Vector2(Main.rand.NextFloat(-900f, 900f), -400f));
            }
        }

        /// <summary>从起始高度向下探地表</summary>
        private static bool TryFindGround(float x, float fromY, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < 46; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 40)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }

        public override void ModifyLightingBrightness(ref float scale) {
            if (presence > 0.001f) {
                scale *= 1f - Dim * presence;
            }
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (presence <= 0.001f) {
                return;
            }
            //时间冻在夜里，原版底色近黑；这里几乎整条接管，雨夜没有别的光源解释
            tileColor = Color.Lerp(tileColor, RainTile, TileTintStrength * presence);
            backgroundColor = Color.Lerp(backgroundColor, RainBackground, BackgroundTintStrength * presence);
        }
    }
}
