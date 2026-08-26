using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Hollowdeep
{
    /// <summary>
    /// 残酷模式通用洞穴（土层/岩层纯净地带）环境氛围控制器。
    /// 「空聆」滴水与深洞低鸣、尘埃光斑、蝙蝠振翅掠过；
    /// 「暗涌」全黑久待时的屏边黑雾与耳鸣（纯压迫演出，不碰光照引擎）；
    /// 「萤缀」洞顶低频萤火飘带。
    /// 本类只跑客户端演出与本地强度量；「惊岩」的权威决策在 <see cref="HollowdeepPlayer"/>
    /// </summary>
    internal class HollowdeepAmbience : ModSystem
    {
        /// <summary>本地玩家的在场强度 0~1（进出洞穴缓升缓降）</summary>
        public static float Presence { get; private set; }

        /// <summary>「暗涌」黑雾强度 0~1（全黑久待渐升，见光快速消散）</summary>
        public static float DarkVeil { get; private set; }

        //==== 暗涌参数 ====
        /// <summary>全黑判定亮度阈值</summary>
        private const float DarkBrightness = 0.06f;
        /// <summary>黑雾起雾前的宽限帧数（约 4 秒）</summary>
        private const float DarkGraceTicks = 240f;
        /// <summary>宽限后拉满黑雾所需帧数（约 8 秒）</summary>
        private const float DarkRampTicks = 480f;
        /// <summary>见光时暗时长的坍缩系数（约 15 帧内归零，"点亮即消散"）</summary>
        private const float DarkCollapse = 0.78f;

        /// <summary>暗时长累计（帧）</summary>
        private static float darkTicks;

        //==== 环境声循环槽（镜像 OldNetAmbience 的 SlotId+回调惯例） ====
        private static SlotId droneSlot;
        private static SlotId tinnitusSlot;
        /// <summary>深洞低鸣：门户闲置环压低八度当洞腔共鸣</summary>
        private static readonly SoundStyle DroneStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };
        /// <summary>耳鸣：室内风雪环拔高当高频嘶鸣</summary>
        private static readonly SoundStyle TinnitusStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        //==== 空聆调度计时（本地屏幕演出量，静态仅存本机） ====
        private static int dripTimer;
        private static int groanTimer;
        private static int moteTimer;
        //蝙蝠掠过：一串短促振翅声沿轨迹划过
        private static int batTimer;
        private static int batChirpsLeft;
        private static int batChirpGap;
        private static Vector2 batPos;
        private static Vector2 batVel;

        //==== 萤缀（洞顶萤火飘带）====
        private static int glowTimer;
        private static int glowRun;
        private static int glowEmitCd;
        private static int glowAnchorCd;
        private static float glowDir;
        private static Vector2 glowEmitter;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            bool active = !Main.gameMenu && GameModeSystem.EffectiveTier > 0
                && Main.LocalPlayer.active && InPureCave(Main.LocalPlayer);
            float target = active ? 1f : 0f;
            Presence = Math.Abs(target - Presence) < 0.005f
                ? target : MathHelper.Lerp(Presence, target, 0.035f);

            UpdateDarkVeil();

            if (Presence > 0.02f && !Main.gameMenu) {
                UpdateAmbientLoops();
            }
            if (Presence > 0.35f && !Main.gameMenu && !Main.gamePaused) {
                UpdateHollowListen();
                UpdateGlowRibbon();
            }
        }

        public override void ClearWorld() {
            if (Main.dedServ) {
                return;
            }
            Presence = 0f;
            DarkVeil = 0f;
            darkTicks = 0f;
            batChirpsLeft = 0;
            glowRun = 0;
        }

        //==================== 检测 ====================

        /// <summary>
        /// 纯净洞穴判定：土层/岩层高度，且不属任何特殊群系。
        /// 各端可用（Zone 旗标随原版同步；灾厄群系经 CWRRef 守门，世界边缘一刀切兜底深渊/硫磺海）
        /// </summary>
        public static bool InPureCave(Player player) {
            if (!player.ZoneDirtLayerHeight && !player.ZoneRockLayerHeight) {
                return false;
            }
            if (player.ZoneDesert || player.ZoneUndergroundDesert || player.ZoneSnow
                || player.ZoneJungle || player.ZoneCorrupt || player.ZoneCrimson
                || player.ZoneHallow || player.ZoneGlowshroom || player.ZoneGranite
                || player.ZoneMarble || player.ZoneHive || player.ZoneLihzhardTemple
                || player.ZoneDungeon || player.ZoneShimmer || player.ZoneGraveyard
                || player.ZoneMeteor || player.ZoneBeach) {
                return false;
            }

            Point center = player.Center.ToTileCoordinates();
            if (!WorldGen.InWorld(center.X, center.Y, 10)) {
                return false;
            }
            //世界边缘带（深渊/硫磺海地界）不算纯净洞穴，服务端无需灾厄旗标也能排除
            if (center.X < 350 || center.X > Main.maxTilesX - 350) {
                return false;
            }
            //蛛巢无 Zone 旗标：中心点蛛网墙即视为出界（Silkcrypt 槽自有整片扫描）
            if (Framing.GetTileSafely(center).WallType == WallID.SpiderUnsafe) {
                return false;
            }
            //灾厄地下群系排除（星辉瘟疫可延伸到地下；旗标缺失时由上面的边缘带兜底）
            if (CWRRef.Has && (player.GetPlayerZoneAbyss() || player.GetPlayerZoneSunkenSea()
                || player.GetPlayerZoneAstral() || player.GetPlayerZoneSulphur()
                || player.GetPlayerZoneCalamity())) {
                return false;
            }
            return true;
        }

        /// <summary>玩家约 60 格内有存活城镇 NPC（城镇安宁：伤害机制不触发）</summary>
        internal static bool TownNearby(Player player) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(player.Center) < 960f) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>统计某类弹幕的活动实例数（镜像 Wastes 的并发上限口径，只在触发时调用）</summary>
        internal static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>
        /// 从空腔点向上找第一块实心洞顶，返回其下沿悬挂锚点（下探 29px，落石 24px 判定箱
        /// 在此完全离墙，起坠帧不会撞死在顶砖里）；起点实心或搜完未见顶皆失败
        /// </summary>
        internal static bool TryFindCeiling(Vector2 worldPos, int maxUpTiles, out Vector2 anchor) {
            anchor = default;
            Point start = worldPos.ToTileCoordinates();
            if (!WorldGen.InWorld(start.X, start.Y, 10) || WorldGen.SolidTile(start.X, start.Y)) {
                return false;
            }
            for (int dy = 1; dy <= maxUpTiles; dy++) {
                int tileY = start.Y - dy;
                if (!WorldGen.InWorld(start.X, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(start.X, tileY)) {
                    anchor = new Vector2(start.X * 16f + 8f, tileY * 16f + 29f);
                    return true;
                }
            }
            return false;
        }

        //==================== 暗涌 ====================

        private static void UpdateDarkVeil() {
            Player player = Main.LocalPlayer;
            bool dark = false;
            if (Presence > 0.5f && !Main.gameMenu && player.active && !player.dead) {
                Point tile = player.Center.ToTileCoordinates();
                if (WorldGen.InWorld(tile.X, tile.Y, 10)) {
                    dark = Lighting.Brightness(tile.X, tile.Y) < DarkBrightness;
                }
            }

            if (dark && !Main.gamePaused) {
                darkTicks = Math.Min(darkTicks + 1f, DarkGraceTicks + DarkRampTicks + 90f);
            }
            else if (!dark) {
                darkTicks *= DarkCollapse;
            }

            float veil = MathHelper.Clamp((darkTicks - DarkGraceTicks) / DarkRampTicks, 0f, 1f);
            if (CWRWorld.HasBoss) {
                veil *= 0.4f;//Boss 战不抢戏
            }
            DarkVeil = veil;
        }

        //==================== 空聆 ====================

        private static void UpdateHollowListen() {
            Player player = Main.LocalPlayer;

            //滴水：洞顶随机点位一声轻滴 + 一粒坠水尘（有空间感的定位声）
            if (--dripTimer <= 0) {
                dripTimer = Main.rand.Next(100, 320);
                Vector2 probe = player.Center + new Vector2(Main.rand.NextFloat(-700f, 700f),
                    -Main.rand.NextFloat(0f, 150f));
                if (TryFindCeiling(probe, 24, out Vector2 dripPos)) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.32f + Main.rand.NextFloat(0.16f),
                        Pitch = -0.55f + Main.rand.NextFloat(0.5f),
                        MaxInstances = 3,
                    }, dripPos);
                    Dust drop = Dust.NewDustPerfect(dripPos + new Vector2(0f, 2f), DustID.Water,
                        new Vector2(0f, 1.6f), 60, default, 0.9f);
                    drop.noGravity = false;
                }
            }

            //深洞远吟：远处传来的低沉岩层挪响
            if (--groanTimer <= 0) {
                groanTimer = Main.rand.Next(1400, 3200);
                Vector2 offset = Main.rand.NextVector2Unit() * Main.rand.NextFloat(500f, 900f);
                SoundEngine.PlaySound(SoundID.WormDig with {
                    Volume = 0.22f, Pitch = -0.85f, MaxInstances = 2,
                }, player.Center + offset);
            }

            //蝙蝠掠过：一侧起飞、贴着玩家上方划过的一串短振翅
            if (batChirpsLeft > 0) {
                batPos += batVel;
                if (--batChirpGap <= 0) {
                    batChirpGap = 4;
                    batChirpsLeft--;
                    SoundEngine.PlaySound(SoundID.Item1 with {
                        Volume = 0.2f,
                        Pitch = 0.5f + Main.rand.NextFloat(0.25f),
                        MaxInstances = 4,
                    }, batPos);
                }
            }
            else if (--batTimer <= 0) {
                batTimer = Main.rand.Next(1100, 2600);
                if (Presence > 0.6f) {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    batPos = player.Center + new Vector2(side * Main.rand.NextFloat(340f, 520f),
                        -Main.rand.NextFloat(40f, 160f));
                    batVel = new Vector2(-side * Main.rand.NextFloat(9f, 13f), Main.rand.NextFloat(-1f, 1f));
                    batChirpsLeft = 5;
                    batChirpGap = 1;
                }
            }

            //尘埃光斑：只在亮处缓浮（≤7/s，屏外不采样）
            if (--moteTimer <= 0) {
                moteTimer = 9;
                Vector2 sample = Main.screenPosition + new Vector2(
                    Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight));
                Point tile = sample.ToTileCoordinates();
                if (WorldGen.InWorld(tile.X, tile.Y, 10) && !WorldGen.SolidTile(tile.X, tile.Y)
                    && Lighting.Brightness(tile.X, tile.Y) >= 0.4f) {
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_HollowdeepMote>(sample,
                        new Vector2(Main.rand.NextFloat(-0.14f, 0.14f), Main.rand.NextFloat(0.03f, 0.14f)),
                        new Color(214, 202, 178), Main.rand.NextFloat(0.06f, 0.11f))
                        ?.Configure(Main.rand.Next(150, 240));
                }
            }
        }

        //==================== 萤缀 ====================

        private static void UpdateGlowRibbon() {
            Player player = Main.LocalPlayer;

            if (glowRun > 0) {
                glowRun--;
                //贴顶横漂 + 轻微垂摆
                glowEmitter.X += glowDir * (0.7f + 0.3f * MathF.Sin(glowRun * 0.045f));
                glowEmitter.Y += MathF.Sin(glowRun * 0.05f) * 0.35f;

                //周期重贴洞顶：跟随地形起伏，顶没了就收尾
                if (--glowAnchorCd <= 0) {
                    glowAnchorCd = 20;
                    if (TryFindCeiling(glowEmitter + new Vector2(0f, 44f), 12, out Vector2 anchor)) {
                        glowEmitter.Y = MathHelper.Lerp(glowEmitter.Y, anchor.Y + 14f, 0.4f);
                    }
                    else if (glowRun > 30) {
                        glowRun = 30;
                    }
                }

                if (--glowEmitCd <= 0) {
                    glowEmitCd = 3;
                    Vector2 jitter = new(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-8f, 10f));
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_HollowdeepFirefly>(glowEmitter + jitter,
                        new Vector2(glowDir * Main.rand.NextFloat(0.15f, 0.4f), Main.rand.NextFloat(-0.08f, 0.1f)),
                        new Color(255, 192, 96), Main.rand.NextFloat(0.09f, 0.15f))
                        ?.Configure(Main.rand.Next(180, 300));
                }
                return;
            }

            if (--glowTimer > 0) {
                return;
            }
            glowTimer = Main.rand.Next(1200, 2700);
            if (Presence <= 0.55f) {
                return;
            }
            //在玩家附近上方找一段洞顶作为飘带起点
            Vector2 probe = player.Center + new Vector2(Main.rand.NextFloat(-500f, 500f),
                -Main.rand.NextFloat(60f, 200f));
            if (!TryFindCeiling(probe, 30, out Vector2 start)) {
                return;
            }
            glowEmitter = start + new Vector2(0f, 16f);
            glowDir = Main.rand.NextBool() ? 1f : -1f;
            glowRun = Main.rand.Next(320, 460);
            glowEmitCd = 0;
            glowAnchorCd = 20;
        }

        //==================== 环境声循环 ====================

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateAmbientLoops() {
            if (!SoundEngine.TryGetActiveSound(droneSlot, out _)) {
                droneSlot = SoundEngine.PlaySound(DroneStyle, null, UpdateDrone);
            }
            if (!SoundEngine.TryGetActiveSound(tinnitusSlot, out _)) {
                tinnitusSlot = SoundEngine.PlaySound(TinnitusStyle, null, UpdateTinnitus);
            }
        }

        //深洞低鸣：稀薄的洞腔共鸣底，Boss 在场再压低
        private static bool UpdateDrone(ActiveSound sound) {
            if (Presence <= 0.004f || Main.gameMenu) {
                return false;
            }
            sound.Volume = 0.16f * Presence * (CWRWorld.HasBoss ? 0.6f : 1f);
            sound.Pitch = -0.72f;
            sound.Position = null;
            return true;
        }

        //耳鸣：随暗涌平方渐强，见光随黑雾一起塌掉
        private static bool UpdateTinnitus(ActiveSound sound) {
            if (Presence <= 0.004f || Main.gameMenu) {
                return false;
            }
            sound.Volume = 0.3f * DarkVeil * DarkVeil;
            sound.Pitch = 0.9f;
            sound.Position = null;
            return true;
        }
    }
}
