using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Aetherglim.Projectiles;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Aetherglim
{
    /// <summary>
    /// 微光地带环境氛围导演。
    /// 客户端侧「珠光」常态氛围：珠光泡从湖面缓升、空灵风铃与倒放感回响点缀、幻光蝶低频掠过，
    /// 全部纯本机演出，进出群系有淡入淡出；Boss 在场时减弱视觉、静默铃声。
    /// 权威端侧「引力泡」生成：逐玩家冷却（档位只调频率）、全局并发上限、
    /// 泡从玩家侧方远处的湖面上空出生缓飘而来（漂近的过程就是预告）
    /// </summary>
    internal class AetherglimAmbience : ModSystem
    {
        //==== 引力泡生成（权威端）====
        /// <summary>逐玩家生成冷却，档位只调频率不换机制</summary>
        private static readonly int[] BubbleCooldownByTier = [1620, 1260, 960];
        /// <summary>引力泡全局并发上限</summary>
        private const int BubbleCap = 3;
        /// <summary>泡可见半径基准（形状不随档位改变）</summary>
        private const float BubbleRadius = 84f;
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int RetryFrames = 60;

        //==== 珠光氛围（客户端）====
        /// <summary>本机屏幕的微光在场强度 0~1（进出群系缓升缓降）</summary>
        public static float Presence { get; private set; }

        /// <summary>风铃音阶（空灵五声位）</summary>
        private static readonly float[] ChimeNotes = [0.05f, 0.25f, 0.45f, 0.65f];

        private static readonly List<Vector2> surfacePoints = [];
        private static int surfaceProbeTimer;
        private static int pearlTimer;
        private static int chimeTimer = 300;
        private static int bellTimer = 1800;
        private static int butterflyTimer = 600;
        //风铃回响：延迟第二声（正放=亮后暗尾，倒放感=暗先亮后）
        private static int echoDelay;
        private static float echoPitch;
        private static float echoVolume;
        private static Vector2 echoPos;

        public override void ClearWorld() {
            Presence = 0f;
            surfacePoints.Clear();
            surfaceProbeTimer = 0;
            pearlTimer = 0;
            chimeTimer = 300;
            bellTimer = 1800;
            butterflyTimer = 600;
            echoDelay = 0;
        }

        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                UpdateClientAmbience();
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                UpdateBubbleDirector();
            }
        }

        //==================== 「珠光」客户端氛围 ====================

        private static void UpdateClientAmbience() {
            if (Main.gameMenu) {
                Presence = 0f;
                return;
            }
            Player player = Main.LocalPlayer;
            float target = 0f;
            if (GameModeSystem.EffectiveTier > 0 && player.active && !player.dead && player.ZoneShimmer) {
                //Boss 在场：纯视觉保留但减弱
                target = CWRWorld.HasBoss ? 0.5f : 1f;
            }
            Presence = Math.Abs(target - Presence) < 0.01f
                ? target : MathHelper.Lerp(Presence, target, 0.045f);
            if (Presence < 0.05f) {
                return;
            }

            UpdateSurfaceCache(player);
            UpdatePearls();
            if (!CWRWorld.HasBoss) {
                UpdateChimes(player);
            }
            UpdateButterflies(player);
        }

        /// <summary>低频扫描视野内的微光液面，缓存珠光泡的出生点（每 45 帧 14 列探针）</summary>
        private static void UpdateSurfaceCache(Player player) {
            if (--surfaceProbeTimer > 0) {
                return;
            }
            surfaceProbeTimer = 45;
            surfacePoints.Clear();
            int centerX = (int)(player.Center.X / 16f);
            int topY = (int)(Main.screenPosition.Y / 16f) - 6;
            int bottomY = (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + 8;
            for (int i = 0; i < 14; i++) {
                int tileX = centerX + Main.rand.Next(-48, 49);
                if (AetherglimFX.TryFindShimmerSurface(tileX, topY, bottomY, out Vector2 surface)) {
                    surfacePoints.Add(surface);
                }
            }
        }

        /// <summary>珠光泡从湖面缓升（常态 ≤5/s，Boss 在场减半）</summary>
        private static void UpdatePearls() {
            if (--pearlTimer > 0 || surfacePoints.Count == 0 || Presence < 0.4f) {
                return;
            }
            pearlTimer = CWRWorld.HasBoss ? 16 + Main.rand.Next(14) : 8 + Main.rand.Next(9);
            Vector2 point = surfacePoints[Main.rand.Next(surfacePoints.Count)];
            //DiffusionCircle4 实测 156×156：Scale 0.10~0.22 → 泡缘半径约 7~16 像素
            PRTLoader.NewParticle<PRT_AetherglimPearl>(
                point + new Vector2(Main.rand.NextFloat(-10f, 10f), 2f),
                new Vector2(Main.rand.NextFloat(-0.1f, 0.1f), -Main.rand.NextFloat(0.4f, 0.85f)),
                Color.White, Main.rand.NextFloat(0.10f, 0.22f))
                .Configure(Main.rand.Next(90, 160), Main.rand.NextFloat(6f));
        }

        /// <summary>
        /// 空灵风铃：偶发的单音+一声延迟回响。四成概率回响在前（先暗后亮），
        /// 像时间倒着放了一小段——微光的时空异质感全在这半秒里
        /// </summary>
        private static void UpdateChimes(Player player) {
            //回响落拍
            if (echoDelay > 0 && --echoDelay == 0) {
                SoundEngine.PlaySound(SoundID.Item26 with {
                    Volume = echoVolume * Presence,
                    Pitch = echoPitch,
                    MaxInstances = 3
                }, echoPos);
            }

            if (--chimeTimer > 0) {
                //远方偶来一记钟点缀
                if (--bellTimer <= 0) {
                    bellTimer = 1500 + Main.rand.Next(1500);
                    Vector2 bellPos = player.Center + new Vector2(
                        Main.rand.NextFloat(-520f, 520f), Main.rand.NextFloat(-260f, 120f));
                    SoundEngine.PlaySound(SoundID.Item35 with {
                        Volume = 0.13f * Presence,
                        Pitch = 0.42f,
                        MaxInstances = 2
                    }, bellPos);
                }
                return;
            }
            chimeTimer = 420 + Main.rand.Next(420);

            float note = ChimeNotes[Main.rand.Next(ChimeNotes.Length)];
            Vector2 pos = player.Center + new Vector2(
                Main.rand.NextFloat(-340f, 340f), Main.rand.NextFloat(-180f, 100f));
            bool reversedFeel = Main.rand.NextFloat() < 0.4f;
            if (reversedFeel) {
                //倒放感：先来一声低哑的"尾巴"，亮音随后才落
                SoundEngine.PlaySound(SoundID.Item26 with {
                    Volume = 0.10f * Presence,
                    Pitch = note - 0.9f,
                    MaxInstances = 3
                }, pos);
                echoDelay = 13;
                echoPitch = note;
                echoVolume = 0.22f;
            }
            else {
                SoundEngine.PlaySound(SoundID.Item26 with {
                    Volume = 0.22f * Presence,
                    Pitch = note,
                    MaxInstances = 3
                }, pos);
                echoDelay = 13;
                echoPitch = note - 0.9f;
                echoVolume = 0.12f;
            }
            echoPos = pos;
        }

        /// <summary>幻光蝶：低频从屏缘飘入横掠视野（Boss 在场不再来新蝶）</summary>
        private static void UpdateButterflies(Player player) {
            if (--butterflyTimer > 0 || Presence < 0.55f || CWRWorld.HasBoss) {
                return;
            }
            butterflyTimer = 480 + Main.rand.Next(720);
            int side = Main.rand.NextBool() ? 1 : -1;
            float speed = Main.rand.NextFloat(1.05f, 1.5f);
            Vector2 spawn = new(
                side < 0 ? Main.screenPosition.X - 70f : Main.screenPosition.X + Main.screenWidth + 70f,
                player.Center.Y - Main.rand.NextFloat(-60f, 200f));
            int life = (int)((Main.screenWidth + 260f) / speed);
            PRTLoader.NewParticle<PRT_AetherglimButterfly>(spawn,
                new Vector2(-side * speed, 0f), Color.White, Main.rand.NextFloat(0.9f, 1.3f))
                .Configure(life, Main.rand.NextFloat(6f));
        }

        //==================== 「引力泡」权威端生成 ====================

        private static void UpdateBubbleDirector() {
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            int bubbleType = ModContent.ProjectileType<AetherglimGravityBubbleProj>();
            foreach (Player player in Main.ActivePlayers) {
                AetherglimPlayer glim = player.GetModPlayer<AetherglimPlayer>();
                if (glim.BubbleSpawnTimer > 0) {
                    glim.BubbleSpawnTimer--;
                    continue;
                }
                glim.BubbleSpawnTimer = RetryFrames;
                if (player.dead || !player.ZoneShimmer || player.shimmering) {
                    continue;
                }
                if (CWRWorld.HasBoss || AetherglimFX.NearTownNPC(player.Center)) {
                    continue;
                }
                if (AetherglimFX.CountActive(bubbleType) >= BubbleCap) {
                    continue;
                }
                if (!TryPickLakeAnchor(player, out Vector2 anchor)) {
                    continue;
                }

                //从侧方远处出生、缓飘而来：漂近本身就是 ≥10 秒的可见预告
                int dir = Main.rand.NextBool() ? 1 : -1;
                Vector2 spawnPos = anchor + new Vector2(
                    -dir * Main.rand.NextFloat(680f, 900f), -Main.rand.NextFloat(70f, 150f));
                if (Collision.SolidCollision(spawnPos - new Vector2(46f), 92, 92)) {
                    continue;//出生点埋在崖体里，本轮放弃
                }
                float radius = BubbleRadius + Main.rand.NextFloat(-8f, 10f);
                Projectile.NewProjectile(new EntitySource_Misc("CWR_AetherglimBubble"),
                    spawnPos, new Vector2(dir * Main.rand.NextFloat(0.5f, 0.78f), 0f),
                    bubbleType, 0, 0f, Main.myPlayer, radius);
                glim.BubbleSpawnTimer = BubbleCooldownByTier[tier - 1] + Main.rand.Next(-90, 121);
            }
        }

        /// <summary>在玩家附近找一处微光液面作为泡的漂行锚点（10 列随机探针）</summary>
        private static bool TryPickLakeAnchor(Player player, out Vector2 anchor) {
            anchor = default;
            int centerX = (int)(player.Center.X / 16f);
            int topY = (int)(player.Center.Y / 16f) - 30;
            int bottomY = (int)(player.Center.Y / 16f) + 34;
            for (int i = 0; i < 10; i++) {
                int tileX = centerX + Main.rand.Next(-44, 45);
                if (AetherglimFX.TryFindShimmerSurface(tileX, topY, bottomY, out anchor)) {
                    return true;
                }
            }
            return false;
        }
    }
}
