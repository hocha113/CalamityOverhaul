using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>是否身处鬼雨世界，按玩家独立存档</summary>
    internal sealed class OniRainWorldPlayer : ModPlayer
    {
        public bool InOniRainWorld;

        public override void SaveData(TagCompound tag) {
            if (InOniRainWorld) {
                tag["inOniRainWorld"] = true;
            }
        }

        public override void LoadData(TagCompound tag) {
            InOniRainWorld = tag.ContainsKey("inOniRainWorld");
        }
    }

    /// <summary>
    /// 鬼雨世界的常驻状态：氛围目标喂给 <see cref="Content.Wraiths.Abilities.GhostRains.GhostRainAmbience"/>
    /// （压顶/天幕/滤镜全套复用），并自带满幕雨帘、潮雾与远雷的本地表现。
    /// </summary>
    internal static class OniRainWorldState
    {
        //沿用鬼雨既定湿墨色板：灰白尸雨/尸斑青/潮雾沉青
        private static readonly Color RainPale = new(170, 185, 190);
        private static readonly Color RainCorpse = new(140, 170, 165);
        private static readonly Color MistDamp = new(58, 66, 70);

        private static float dropCarry;
        private static int thunderTimer;

        /// <summary>本地玩家是否身处鬼雨世界</summary>
        public static bool LocalIn => !Main.dedServ && !Main.gameMenu
            && Main.LocalPlayer?.active == true
            && Main.LocalPlayer.TryGetModPlayer(out OniRainWorldPlayer orp)
            && orp.InOniRainWorld;

        /// <summary>给鬼雨氛围控制器的目标强度：在雨世界恒满，演出结算前给预压顶</summary>
        public static float GlobalAmbientTarget
            => LocalIn ? 1f : OniRainWorldTransition.AmbientPreGloom;

        internal static void EnterLocal(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            player.GetModPlayer<OniRainWorldPlayer>().InOniRainWorld = true;
            thunderTimer = 300;
        }

        internal static void ExitLocal(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            player.GetModPlayer<OniRainWorldPlayer>().InOniRainWorld = false;
        }

        /// <summary>调试出口：D3 直接退雨，氛围沿控制器包络自行排干</summary>
        internal static void DebugExit() {
            Player player = Main.LocalPlayer;
            if (player?.active != true || !LocalIn) {
                return;
            }
            ExitLocal(player);
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Pitch = -0.4f, Volume = 0.7f, MaxInstances = 3,
            }, player.Center);
        }

        /// <summary>
        /// 常驻表现：满幕雨帘 + 贴地潮雾 + 稀有脸痕 + 远雷，密度吃氛围强度。<br/>
        /// 演出结算前也承担前兆稀雨——两个世界开始互相渗透的零星雨丝。
        /// </summary>
        internal static void UpdateFx() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            bool inWorld = LocalIn;
            float preRain = OniRainWorldTransition.PreRainDensity;
            if (!inWorld && preRain <= 0f) {
                return;
            }

            Player player = Main.LocalPlayer;
            float density = inWorld
                ? Content.Wraiths.Abilities.GhostRains.GhostRainAmbience.Intensity
                : preRain;
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

            if (density > 0.8f && Main.rand.NextBool(70)) {
                SpawnFaceStreak();
            }

            //远雷，稳态下的低频心跳；前兆雨阶段不抢演出节拍的雷声
            if (inWorld && --thunderTimer <= 0) {
                thunderTimer = Main.rand.Next(480, 960);
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Pitch = Main.rand.NextFloat(-1f, -0.75f),
                    Volume = Main.rand.NextFloat(0.22f, 0.4f),
                    MaxInstances = 3,
                }, player.Center + new Vector2(Main.rand.NextFloat(-900f, 900f), -400f));
            }
        }

        internal static void ResetLocal() {
            dropCarry = 0f;
            thunderTimer = 0;
        }

        /// <summary>满幕雨帘：约 0.02 滴/像素宽/帧 @密度1，风向按世界种子定相</summary>
        private static void SpawnRainBand(float density) {
            float left = Main.screenPosition.X - 160f;
            float right = Main.screenPosition.X + Main.screenWidth + 160f;

            dropCarry += density * 0.02f * (right - left);
            int count = Math.Min((int)dropCarry, 56);
            dropCarry -= count;
            if (count <= 0) {
                return;
            }

            float wind = MathF.Sin(Main.worldID % 255 * 0.37f) * 2.2f * density;
            for (int i = 0; i < count; i++) {
                Vector2 pos = new(Main.rand.NextFloat(left, right),
                    Main.screenPosition.Y - Main.rand.NextFloat(10f, 220f));
                Vector2 vel = new(wind + Main.rand.NextFloat(-0.35f, 0.35f),
                    Main.rand.NextFloat(11f, 17f));
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
    }

    /// <summary>演出与常驻表现的驱动泵，兼本地化载体</summary>
    internal class OniRainWorldSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "OniRainWorld";

        public static LocalizedText InteractHint { get; private set; }

        public override void SetStaticDefaults() {
            InteractHint = this.GetLocalization(nameof(InteractHint), () => "[右键] 撑伞入雨");
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            OniRainWorldTransition.Update();
            OniRainWorldState.UpdateFx();
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                OniRainWorldTransition.HardReset();
                OniRainWorldState.ResetLocal();
            }
        }
    }
}
