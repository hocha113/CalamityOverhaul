using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Hollowdeep.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Hollowdeep
{
    /// <summary>
    /// 「惊岩」震动计量（以玩家为中心）：爆炸、连续挖掘、战斗喧闹会惊动岩层，
    /// 超过阈值后在头顶洞壁挂出落石预告（声响驱动坠物，区别于 Rimehollow 的时间驱动冰锥）。
    /// 计量与触发只在权威端推进；档位只调阈值（越高档越敏感），落石形状与伤害不随档位变
    /// </summary>
    internal class HollowdeepPlayer : ModPlayer
    {
        /// <summary>触发阈值（档位递减 = 越高档越敏感）</summary>
        private static readonly int[] QuakeThresholdByTier = [560, 440, 330];
        /// <summary>震动值每帧线性衰减</summary>
        private const float VibrationDecay = 0.45f;
        /// <summary>挖掘工具（镐/斧/锤）每次挥动的震动</summary>
        private const float MiningSwingAdd = 22f;
        /// <summary>普通武器每次挥动的震动（战斗喧闹的慢积累）</summary>
        private const float CombatSwingAdd = 8f;
        /// <summary>受击一次的震动</summary>
        private const float HurtAdd = 40f;
        /// <summary>震动值上限 = 阈值 × 此系数（防爆炸囤积连环触发）</summary>
        private const float VibrationCapMul = 1.6f;
        /// <summary>触发后的本人冷却</summary>
        private const int QuakeCooldownFrames = 420;
        /// <summary>条件不满足（并发满/无洞顶）时的复查间隔</summary>
        private const int RetryFrames = 30;
        /// <summary>落石全局并发上限（严格）</summary>
        private const int RockCap = 4;
        /// <summary>落石伤害 = 洞穴代表怪接触伤害 × 此值（镜像 DamageFrac 写法；档位不加伤）</summary>
        private const float RockDamageFrac = 0.55f;
        /// <summary>向上找洞顶的最大瓦格数（更高的穹顶视为无岩可落）</summary>
        private const int CeilingSearchTiles = 40;

        /// <summary>震动值（权威端私产，客户端不得用它驱动画面）</summary>
        private float vibration;
        private int quakeCooldown;
        private int retryDelay;
        private int prevItemAnimation;

        public override void PostUpdate() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;//计量与触发只在权威端
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                vibration = 0f;
                return;
            }
            if (quakeCooldown > 0) {
                quakeCooldown--;
            }
            if (retryDelay > 0) {
                retryDelay--;
            }
            vibration = Math.Max(0f, vibration - VibrationDecay);

            bool inCave = HollowdeepAmbience.InPureCave(Player);

            //挥动侦测：itemAnimation 回跳即新一次挥动（自动挥舞与电钻同样命中）
            if (inCave && Player.itemAnimation > prevItemAnimation) {
                Item held = Player.HeldItem;
                if (held.pick > 0 || held.axe > 0 || held.hammer > 0) {
                    vibration += MiningSwingAdd;
                }
                else if (held.damage > 0) {
                    vibration += CombatSwingAdd;
                }
            }
            prevItemAnimation = Player.itemAnimation;

            float threshold = QuakeThresholdByTier[tier - 1];
            vibration = Math.Min(vibration, threshold * VibrationCapMul);

            if (!inCave || vibration < threshold) {
                return;
            }
            if (quakeCooldown > 0 || retryDelay > 0) {
                return;
            }
            //公平门：Boss 战与城镇安宁期间伤害机制一律暂停
            if (CWRWorld.HasBoss || HollowdeepAmbience.TownNearby(Player)) {
                retryDelay = RetryFrames;
                return;
            }
            TryTriggerRockfall();
        }

        //死亡期计量缓释，复活不继承满值
        public override void UpdateDead() {
            vibration *= 0.95f;
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (GameModeSystem.EffectiveTier <= 0 || !HollowdeepAmbience.InPureCave(Player)) {
                return;
            }
            vibration += HurtAdd;
        }

        /// <summary>外部震源注入（爆炸扫描），带上限钳制</summary>
        internal void AddVibration(float amount) {
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            vibration = Math.Min(vibration + amount,
                QuakeThresholdByTier[tier - 1] * VibrationCapMul);
        }

        /// <summary>头顶锁点挂预告：预告即承诺，落点在生成帧锁死，此后可走位躲开</summary>
        private void TryTriggerRockfall() {
            if (HollowdeepAmbience.CountActive(ModContent.ProjectileType<HollowdeepRockfallProj>()) >= RockCap) {
                retryDelay = RetryFrames;
                return;
            }
            if (!HollowdeepAmbience.TryFindCeiling(Player.Center, CeilingSearchTiles, out Vector2 anchor)) {
                retryDelay = RetryFrames;
                return;
            }

            //洞穴代表怪接触伤害基准（骷髅 20/40/60），只随原版难度走，档位不加伤
            int reference = Main.masterMode ? 60 : Main.expertMode ? 40 : 20;
            int damage = (int)(reference * RockDamageFrac);
            float scale = 0.9f + Main.rand.NextFloat(0.25f);
            Projectile.NewProjectile(Player.GetSource_Misc("HollowdeepQuake"), anchor, Vector2.Zero,
                ModContent.ProjectileType<HollowdeepRockfallProj>(), damage, 1.5f, Main.myPlayer,
                scale);

            vibration *= 0.35f;
            quakeCooldown = QuakeCooldownFrames;
        }
    }

    /// <summary>
    /// 爆炸震源扫描：每帧一趟找到正在起爆的爆炸物（原版爆炸 AI 在 timeLeft==3 进入爆窗），
    /// 按距离衰减把震动记到附近洞穴玩家头上。只在权威端跑
    /// </summary>
    internal class HollowdeepQuakeScan : ModSystem
    {
        /// <summary>单次爆炸的最大震动（贴脸起爆）</summary>
        private const float ExplosionAddMax = 130f;
        /// <summary>震动波及半径（像素）</summary>
        private const float ExplosionRange = 900f;

        public override void PostUpdateProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (GameModeSystem.EffectiveTier <= 0) {
                return;
            }
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.aiStyle != ProjAIStyleID.Explosive || proj.timeLeft != 3) {
                    continue;
                }
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (!player.active || player.dead) {
                        continue;
                    }
                    float dist = player.Distance(proj.Center);
                    if (dist > ExplosionRange || !HollowdeepAmbience.InPureCave(player)) {
                        continue;
                    }
                    player.GetModPlayer<HollowdeepPlayer>()
                        .AddVibration(ExplosionAddMax * (1f - dist / ExplosionRange));
                }
            }
        }
    }
}
