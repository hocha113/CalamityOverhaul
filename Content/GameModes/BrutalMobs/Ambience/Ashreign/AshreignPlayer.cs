using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Ashreign.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Ashreign
{
    /// <summary>
    /// 烬暴逐玩家结算 + 熔泡/烬暴权威调度。
    /// 本机分支：读取在场烬暴计算暴露（带内且无上风遮蔽），
    /// 暴露时轻推 + 刷新短原版 On Fire!（持续灼伤，挂减益由受击端本机施加，原生同步），
    /// 躲入建筑/障碍（上风向 10 格内三身位全被实体瓦挡住）即免疫。
    /// 权威分支：逐玩家冷却调度熔泡与烬暴的生成（决策私产不入同步，生成的弹幕实体天然同步）。
    /// 全部状态为实例字段，禁 static 存逐玩家数据
    /// </summary>
    internal class AshreignPlayer : ModPlayer
    {
        //==== 烬暴暴露参数 ====
        /// <summary>轻推加速度（像素/帧²），刻意低于跑速，是骚扰不是控制</summary>
        private const float PushAccel = 0.16f;
        /// <summary>风推携带速度上限（同向超过则不再加力）</summary>
        private const float PushCarryCap = 4.2f;
        /// <summary>上风遮蔽扫描距离（瓦格）</summary>
        private const int ShelterScanTiles = 10;
        /// <summary>遮蔽采样间隔</summary>
        private const int ShelterScanGap = 6;
        /// <summary>城镇安宁采样间隔</summary>
        private const int TownScanGap = 30;
        /// <summary>灼伤刷新间隔与时长（帧）：离幕后 ≤45 帧自然散去</summary>
        private const int BurnRefreshGap = 15;
        private const int BurnTicks = 45;
        /// <summary>暴露超过此值才结算推力与灼伤</summary>
        private const float HazardGate = 0.3f;

        /// <summary>本机烬幕暴露 0~1（氛围层读取，已含遮蔽与平滑淡出）</summary>
        internal float StormExposure { get; private set; }

        //本机遮蔽/城镇缓存
        private bool sheltered;
        private bool townCalm;
        private int shelterTimer;
        private int townTimer;
        private int burnTimer;

        //权威端调度冷却（服务端实例私产，客户端不读不写）
        private int bubbleTimer;
        private int stormTimer;

        public override void Initialize() {
            StormExposure = 0f;
            sheltered = false;
            townCalm = false;
            shelterTimer = 0;
            townTimer = 0;
            burnTimer = 0;
            //出生错拍：同世界多人不同帧起手
            bubbleTimer = 300 + Main.rand.Next(300);
            stormTimer = 2400 + Main.rand.Next(2400);
        }

        public override void PostUpdateMiscEffects() {
            if (Player.whoAmI == Main.myPlayer && !Main.dedServ) {
                LocalStormTick();
            }
            if (!VaultUtils.isClient) {
                AuthorityTick();
            }
        }

        public override void UpdateDead() {
            StormExposure = Math.Max(StormExposure - 0.02f, 0f);
            burnTimer = 0;
        }

        //==================== 本机：烬暴暴露结算 ====================

        private void LocalStormTick() {
            if (!Ashreign.AmbienceActive(Player)) {
                StormExposure = Math.Max(StormExposure - 0.03f, 0f);
                return;
            }

            //找覆盖本机的烬暴（取包络最强者），方向用于上风遮蔽判定
            float raw = 0f;
            float windDir = 0f;
            int stormType = ModContent.ProjectileType<AshreignAshStormProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != stormType) {
                    continue;
                }
                if (Math.Abs(Player.Center.X - proj.Center.X) > AshreignAshStormProj.HalfWidth) {
                    continue;
                }
                float envelope = AshreignAshStormProj.Envelope(proj);
                if (envelope > raw) {
                    raw = envelope;
                    windDir = proj.velocity.X >= 0f ? 1f : -1f;
                }
            }

            if (raw > 0.01f && windDir != 0f) {
                SampleShelter(windDir);
                if (--townTimer <= 0) {
                    townTimer = TownScanGap;
                    townCalm = Ashreign.TownCalm(Player.Center);
                }
            }

            float target = sheltered ? 0f : raw;
            StormExposure = Math.Abs(target - StormExposure) < 0.01f
                ? target : MathHelper.Lerp(StormExposure, target, 0.09f);

            if (StormExposure < HazardGate || windDir == 0f) {
                return;
            }
            //Boss 在场/城镇安宁：伤害与位移暂停，烬幕视觉保留
            if (!Ashreign.MechanicsAllowed || townCalm) {
                return;
            }

            //轻推：同向携带速度设上限，温和不致命
            if (Player.velocity.X * windDir < PushCarryCap) {
                Player.velocity.X += windDir * PushAccel * StormExposure;
            }

            //持续灼伤：短 On Fire! 滚动刷新，离幕即自然烧完散去
            if (--burnTimer <= 0) {
                burnTimer = BurnRefreshGap;
                Player.AddBuff(BuffID.OnFire, BurnTicks);
            }
        }

        /// <summary>
        /// 上风遮蔽：自头/胸/脚三身位向风源方向逐格找实体瓦，
        /// 10 格内三行全被挡住才算入遮蔽（躲到一堵三格高的墙后即免疫，可读可学）
        /// </summary>
        private void SampleShelter(float windDir) {
            if (--shelterTimer > 0) {
                return;
            }
            shelterTimer = ShelterScanGap;

            int step = windDir >= 0f ? -1 : 1;//风自上风向吹来，向风源侧扫
            int headY = (int)(Player.Top.Y / 16f);
            int midY = (int)(Player.Center.Y / 16f);
            int feetY = (int)((Player.Bottom.Y - 4f) / 16f);
            int startX = (int)(Player.Center.X / 16f);

            sheltered = RowBlocked(startX, headY, step)
                && RowBlocked(startX, midY, step)
                && RowBlocked(startX, feetY, step);
        }

        private static bool RowBlocked(int startX, int tileY, int step) {
            for (int i = 1; i <= ShelterScanTiles; i++) {
                int tileX = startX + step * i;
                if (!WorldGen.InWorld(tileX, tileY, 10)) {
                    return true;
                }
                if (WorldGen.SolidTile(tileX, tileY)) {
                    return true;
                }
            }
            return false;
        }

        //==================== 权威端：熔泡与烬暴调度 ====================

        private void AuthorityTick() {
            if (!Ashreign.AmbienceActive(Player)) {
                return;//离开地狱冻结冷却，间隔只在辖区内计数
            }
            int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);

            if (--bubbleTimer <= 0) {
                bubbleTimer = TryScheduleBubble()
                    ? Ashreign.BubbleIntervalByTier[tier - 1] + Main.rand.Next(60)
                    : Ashreign.TriggerRetryFrames;
            }
            if (--stormTimer <= 0) {
                stormTimer = TryScheduleStorm()
                    ? Ashreign.StormIntervalByTier[tier - 1] + Main.rand.Next(900)
                    : 120;
            }
        }

        /// <summary>熔泡：在目标附近采样岩浆池液面起泡；三成再补一泡（档位不改形状）</summary>
        private bool TryScheduleBubble() {
            if (!Ashreign.MechanicsAllowed || Ashreign.TownCalm(Player.Center)) {
                return false;
            }
            int bubbleType = ModContent.ProjectileType<AshreignMagmaBubbleProj>();
            if (Ashreign.CountActive(bubbleType) >= Ashreign.BubbleCap) {
                return false;
            }
            if (!TrySampleBubbleAnchor(out Vector2 anchor)) {
                return false;
            }
            SpawnBubble(bubbleType, anchor);

            if (Main.rand.NextFloat() < 0.35f
                && Ashreign.CountActive(bubbleType) < Ashreign.BubbleCap
                && TrySampleBubbleAnchor(out Vector2 second)
                && Vector2.DistanceSquared(second, anchor) > 90f * 90f) {
                SpawnBubble(bubbleType, second);
            }
            return true;
        }

        private void SpawnBubble(int bubbleType, Vector2 anchor) {
            //体型差异是风味不是档位；伤害由泡在提交帧按锚定公式现算
            float scaleVar = Main.rand.NextFloat(0.85f, 1.25f);
            Projectile.NewProjectile(Player.GetSource_Misc("CWRAshreignBubble"), anchor,
                Vector2.Zero, bubbleType, 0, 0f, Main.myPlayer, scaleVar);
        }

        /// <summary>在玩家两侧 8~46 格内采样岩浆池液面（贴身 180px 内不起泡，离岸即安全）</summary>
        private bool TrySampleBubbleAnchor(out Vector2 anchor) {
            int startY = (int)(Player.Center.Y / 16f) - 10;
            for (int attempt = 0; attempt < 8; attempt++) {
                int dx = Main.rand.Next(8, Ashreign.BubbleSampleTiles + 1)
                    * (Main.rand.NextBool() ? 1 : -1);
                int tileX = (int)(Player.Center.X / 16f) + dx;
                if (!Ashreign.TryFindLavaSurfaceInColumn(tileX, startY, 44, out anchor)) {
                    continue;
                }
                float dist = Vector2.Distance(anchor, Player.Center);
                if (dist < Ashreign.BubbleMinDist || dist > 1050f) {
                    continue;
                }
                return true;
            }
            anchor = default;
            return false;
        }

        /// <summary>烬暴：在目标上风 1500px 起一面红霾墙横扫（逼近过程即 ≥45 帧预告）</summary>
        private bool TryScheduleStorm() {
            if (!Ashreign.MechanicsAllowed || Ashreign.TownCalm(Player.Center)) {
                return false;
            }
            int stormType = ModContent.ProjectileType<AshreignAshStormProj>();
            if (Ashreign.CountActive(stormType) >= Ashreign.StormCap) {
                return false;
            }
            //本玩家周边已有烬暴则不再点名，防叠墙
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == stormType
                    && Math.Abs(proj.Center.X - Player.Center.X) < Ashreign.StormCrowdDist) {
                    return false;
                }
            }

            float dir = Main.rand.NextBool() ? 1f : -1f;
            Vector2 spawnPos = Player.Center + new Vector2(-dir * Ashreign.StormSpawnLead, -40f);
            Projectile.NewProjectile(Player.GetSource_Misc("CWRAshreignStorm"), spawnPos,
                new Vector2(dir * Ashreign.StormSpeed, 0f),
                stormType, 0, 0f, Main.myPlayer);
            return true;
        }
    }
}
