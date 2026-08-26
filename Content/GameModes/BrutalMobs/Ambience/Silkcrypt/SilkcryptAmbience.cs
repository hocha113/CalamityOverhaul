using InnoVault.PRT;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Silkcrypt
{
    /// <summary>
    /// 丝幕：残酷模式蛛巢的常态氛围主控（纯客户端表现，服务端早退）。
    /// 一条洞风底噪循环（镜像 OldNetAmbience 的 SlotId 补挂制）+ 暗处多足窸窣与
    /// 丝线绷紧的一次性声调度 + 丝絮粒子密度预算 + 背景掠影蛛形 + 茧动恐吓。
    /// 所有 static 均为屏幕级演出量，非逐玩家数据
    /// </summary>
    internal class SilkcryptAmbience : ModSystem
    {
        /// <summary>本地在场强度 0~1（进出蛛巢缓升缓降，离场淡出不硬切）</summary>
        internal static float Presence { get; private set; }

        //环境声循环槽（丢失补挂，音量在回调里逐帧走）
        private static SlotId draftSlot;
        private static readonly SoundStyle DraftStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        //一次性声与低频事件的调度计时
        private static int skitterTimer;
        private static int creakTimer;
        private static int silhouetteTimer;
        private static int twitchTimer;

        //丝絮预算累加器
        private static float lintAcc;
        /// <summary>丝絮每秒预算上限</summary>
        private const float LintPerSecCap = 6f;

        /// <summary>茧动的蛛网簇判定：5×5 内蛛网瓦数下限</summary>
        private const int CobwebClusterMin = 5;

        public override void ClearWorld() => HardReset();
        public override void Unload() => HardReset();

        private static void HardReset() {
            Presence = 0f;
            skitterTimer = 0;
            creakTimer = 0;
            silhouetteTimer = 0;
            twitchTimer = 0;
            lintAcc = 0f;
        }

        /// <summary>约 60 瓦格内有存活城镇 NPC（城镇安宁公约）</summary>
        internal static bool NearTown(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.townNPC) {
                    continue;
                }
                if (Vector2.DistanceSquared(npc.Center, pos) < 960f * 960f) {
                    return true;
                }
            }
            return false;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            Player player = Main.LocalPlayer;
            bool inNest = !Main.gameMenu && player != null && player.active
                && player.GetModPlayer<SilkcryptPlayer>().InNest;
            Presence = MathHelper.Lerp(Presence, inNest ? 1f : 0f, 0.03f);
            if (!inNest && Presence < 0.003f) {
                Presence = 0f;
            }

            if (Presence < 0.02f || Main.gamePaused) {
                return;
            }
            UpdateLoop();
            UpdateOneShots(player);
            SpawnLint(player);
            UpdateSilhouette(player);
            UpdateCocoonTwitch(player);
        }

        //==================== 声底噪 ====================

        private static void UpdateLoop() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(draftSlot, out _)) {
                draftSlot = SoundEngine.PlaySound(DraftStyle, null, UpdateDraft);
            }
        }

        /// <summary>洞风过丝的低鸣：极低音量，只做"这里不安静"的底</summary>
        private static bool UpdateDraft(ActiveSound sound) {
            if (Main.gameMenu || Presence < 0.005f) {
                return false;
            }
            sound.Volume = 0.10f * Presence;
            sound.Pitch = -0.55f;
            sound.Position = null;
            return true;
        }

        /// <summary>暗处多足窸窣 + 丝线绷紧：定位声在玩家周围的暗点随机响起</summary>
        private static void UpdateOneShots(Player player) {
            if (--skitterTimer <= 0) {
                skitterTimer = Main.rand.Next(130, 320);
                if (Presence > 0.25f && TryFindDarkSpot(player, 180f, 460f, out Vector2 pos)) {
                    SoundEngine.PlaySound(SoundID.Grass with {
                        Volume = Main.rand.NextFloat(0.15f, 0.26f) * Presence,
                        Pitch = Main.rand.NextFloat(-0.7f, -0.35f),
                        MaxInstances = 3,
                    }, pos);
                }
            }
            if (--creakTimer <= 0) {
                creakTimer = Main.rand.Next(340, 800);
                if (Presence > 0.25f) {
                    //丝线绷紧：原版黑隐士喷网同源音，压低作丝弦
                    Vector2 pos = player.Center + Main.rand.NextVector2CircularEdge(220f, 160f);
                    SoundEngine.PlaySound(SoundID.Item17 with {
                        Volume = Main.rand.NextFloat(0.12f, 0.2f) * Presence,
                        Pitch = Main.rand.NextFloat(-0.6f, -0.25f),
                        MaxInstances = 3,
                    }, pos);
                }
            }
        }

        /// <summary>在玩家周围找一处暗气格（窸窣与掠影只发生在暗处）</summary>
        private static bool TryFindDarkSpot(Player player, float minDist, float maxDist, out Vector2 pos) {
            for (int attempt = 0; attempt < 4; attempt++) {
                Vector2 candidate = player.Center
                    + Main.rand.NextVector2Unit() * Main.rand.NextFloat(minDist, maxDist);
                Point tile = candidate.ToTileCoordinates();
                if (!WorldGen.InWorld(tile.X, tile.Y, 10) || WorldGen.SolidTile(tile.X, tile.Y)) {
                    continue;
                }
                if (Lighting.Brightness(tile.X, tile.Y) < 0.25f) {
                    pos = candidate;
                    return true;
                }
            }
            pos = default;
            return false;
        }

        //==================== 丝絮 ====================

        private static void SpawnLint(Player player) {
            float wallFrac = player.GetModPlayer<SilkcryptPlayer>().WallFrac;
            float perSec = Math.Min(2.5f + 5f * wallFrac, LintPerSecCap) * Presence;
            lintAcc += perSec / 60f;
            while (lintAcc >= 1f) {
                lintAcc -= 1f;
                SpawnOneLint();
            }
        }

        /// <summary>丝絮只从蛛巢墙前落下：随机屏内点要求背后是蛛巢墙</summary>
        private static void SpawnOneLint() {
            for (int attempt = 0; attempt < 3; attempt++) {
                Vector2 pos = new(
                    Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth),
                    Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight));
                Point tile = pos.ToTileCoordinates();
                if (!WorldGen.InWorld(tile.X, tile.Y, 10) || WorldGen.SolidTile(tile.X, tile.Y)) {
                    continue;
                }
                if (Main.tile[tile.X, tile.Y].WallType != WallID.SpiderUnsafe) {
                    continue;
                }
                PRTLoader.NewParticle<PRT_SilkcryptLint>(pos,
                    new Vector2(0f, Main.rand.NextFloat(0.05f, 0.2f)),
                    new Color(208, 206, 218) * Main.rand.NextFloat(0.28f, 0.45f),
                    Main.rand.NextFloat(0.5f, 1.1f))
                    ?.Configure(Main.rand.Next(240, 420), Main.rand.NextFloat(120f));
                return;
            }
        }

        //==================== 背景掠影 ====================

        /// <summary>低频背景蛛影：起点与中途都要求暗处，掠过即散，绝不生成敌怪</summary>
        private static void UpdateSilhouette(Player player) {
            if (--silhouetteTimer > 0) {
                return;
            }
            silhouetteTimer = Main.rand.Next(420, 900);
            if (Presence < 0.5f) {
                return;
            }
            if (!TryFindDarkSpot(player, 260f, 560f, out Vector2 start)) {
                return;
            }
            float dir = Main.rand.NextBool() ? 1f : -1f;
            Vector2 vel = new Vector2(dir, Main.rand.NextFloat(-0.35f, 0.35f));
            vel.Normalize();
            vel *= Main.rand.NextFloat(9f, 13f);
            int life = Main.rand.Next(26, 44);

            //中途点也要在暗处，否则影子会穿过亮区穿帮
            Point mid = (start + vel * (life * 0.5f)).ToTileCoordinates();
            if (!WorldGen.InWorld(mid.X, mid.Y, 10)
                || Lighting.Brightness(mid.X, mid.Y) > 0.28f) {
                return;
            }
            PRTLoader.NewParticle<PRT_SilkcryptSkitter>(start, vel,
                new Color(14, 10, 18) * 0.62f, Main.rand.NextFloat(0.72f, 0.98f))
                ?.Configure(life);
        }

        //==================== 茧动 ====================

        /// <summary>
        /// 茧动：玩家附近的浓密蛛网簇偶发蠕动一下（网屑抖落 + 闷响），
        /// 纯氛围恐吓，无任何判定
        /// </summary>
        private static void UpdateCocoonTwitch(Player player) {
            if (--twitchTimer > 0) {
                return;
            }
            twitchTimer = Main.rand.Next(600, 1500);
            if (Presence < 0.4f) {
                return;
            }

            //随机点找蛛网簇：5×5 内蛛网瓦够密才算"茧"
            Point center = player.Center.ToTileCoordinates();
            for (int attempt = 0; attempt < 6; attempt++) {
                int x = center.X + Main.rand.Next(-20, 21);
                int y = center.Y + Main.rand.Next(-14, 15);
                if (!WorldGen.InWorld(x, y, 10)) {
                    continue;
                }
                int webs = 0;
                for (int dx = -2; dx <= 2; dx++) {
                    for (int dy = -2; dy <= 2; dy++) {
                        Tile tile = Main.tile[x + dx, y + dy];
                        if (tile.HasTile && tile.TileType == TileID.Cobweb) {
                            webs++;
                        }
                    }
                }
                if (webs < CobwebClusterMin) {
                    continue;
                }

                Vector2 pos = new(x * 16f + 8f, y * 16f + 8f);
                //蠕动：网屑向外抖一圈 + 湿闷的一声（里面有东西动了一下）
                for (int i = 0; i < 9; i++) {
                    Dust dust = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(30f, 30f),
                        DustID.Web, Main.rand.NextVector2Circular(1.2f, 0.9f),
                        120, default, Main.rand.NextFloat(0.7f, 1.1f));
                    dust.noGravity = Main.rand.NextBool();
                }
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.22f * Presence,
                    Pitch = -0.86f,
                    MaxInstances = 2,
                }, pos);
                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.1f * Presence,
                    Pitch = -0.5f,
                    MaxInstances = 2,
                }, pos);
                return;
            }
        }

        //==================== 光 ====================

        /// <summary>蛛巢压光：极轻的一层昏沉，档位不参与</summary>
        public override void ModifyLightingBrightness(ref float scale) {
            if (!Main.dedServ && Presence > 0.001f) {
                scale *= 1f - 0.05f * Presence;
            }
        }
    }
}
