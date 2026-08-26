using CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome.Projectiles;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome
{
    /// <summary>
    /// 邪恶生物群系小怪的残酷模式行为机制层(主题:侵蚀与汲取)。
    /// 叠加在原版 AI 之上,不接管 AI、不动数值(数值层由 GameModeNPC 统一承担)。
    /// 三个家族机制:
    /// 1. 场地侵蚀云:施放系(腐化者/灵液黏黏怪/爬藤怪/漂浮怪)释放孢核预告,绽放缓扩瘴云,带具名逃生缺口;
    /// 2. 汲取压制:接触系命中玩家偷走再生(挂原版减益),自身回血并叼食后撤(可见增益经弹幕实体承载);
    /// 3. 死亡定向溅射:肉厚系死亡先凝 34 帧无害凝核,再放三连邪液溅矛,槽位走廊即缺口
    /// </summary>
    internal class EvilBiomeMobsNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //====== 具名数值块 ======
        /// <summary>瘴云冷却(档位 1),档位每 +1 缩短一档(只调强度)</summary>
        private const int CloudCooldownBase = 560;
        private const int CloudCooldownTierStep = 60;
        /// <summary>施放距离窗口与首发错拍延迟</summary>
        private const float CloudMinDist = 200f;
        private const float CloudMaxDist = 680f;
        private const int CloudStaggerMin = 120;
        private const int CloudStaggerSpan = 240;
        /// <summary>瘴云全局并发上限(种+云合计,量产怪防刷屏)</summary>
        private const int CloudGlobalCap = 6;
        /// <summary>瘴云伤害 = npc.damage(已缩放) × 此系数</summary>
        private const float CloudDamageFrac = 0.5f;
        /// <summary>凝核全局并发上限</summary>
        private const int DeathBurstGlobalCap = 6;
        /// <summary>溅矛伤害 = npc.damage(已缩放) × 此系数</summary>
        private const float LanceDamageFrac = 0.55f;
        /// <summary>死亡溅射只对这个半径内有存活玩家的死亡生效</summary>
        private const float SplatterRelevantDist = 900f;

        /// <summary>本组接管的全部类型(蠕虫只挂头,体/尾类型不进表)</summary>
        private static readonly HashSet<int> TargetTypes = [
            //腐化
            NPCID.EaterofSouls, NPCID.Corruptor, NPCID.DevourerHead,
            //猩红
            NPCID.Crimera, NPCID.FaceMonster, NPCID.BloodCrawler, NPCID.BloodCrawlerWall,
            //困难邪地
            NPCID.Herpling, NPCID.IchorSticker, NPCID.Clinger, NPCID.SeekerHead,
            NPCID.FloatyGross, NPCID.CursedHammer, NPCID.CrimsonAxe,
            //邪化木乃伊
            NPCID.DarkMummy, NPCID.BloodMummy,
        ];

        /// <summary>本个体出生时绑定的档位,0 = 无机制(镜像 GameModeNPC.boundTier)</summary>
        private int boundTier;
        /// <summary>瘴云冷却计时(服务端决策私产);-1 = 待错拍初始化</summary>
        private int cloudTimer;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && TargetTypes.Contains(entity.type);

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            boundTier = tier;
            cloudTimer = -1;
        }

        /// <summary>机制资格(每个入口都过):排除友方/雕像怪/Boss/蠕虫体节等</summary>
        private static bool MechanicEligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage || npc.boss) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0 || npc.SpawnedFromStatue) {
                return false;
            }
            //蠕虫体节(realLife 指向他人)排除,机制只挂头
            if (npc.realLife >= 0 && npc.realLife != npc.whoAmI) {
                return false;
            }
            return true;
        }

        //====== 家族归属表(类型级参数) ======

        /// <summary>瘴云施放系的风味;-1 = 不施放</summary>
        private static int CloudFlavor(int type) => type switch {
            NPCID.Corruptor => EvilBiomeFX.FlavorCorrupt,
            NPCID.IchorSticker => EvilBiomeFX.FlavorIchor,
            NPCID.Clinger => EvilBiomeFX.FlavorCursed,
            NPCID.FloatyGross => EvilBiomeFX.FlavorCrimson,
            _ => -1,
        };

        /// <summary>汲取家族的风味(含诅咒武器);-1 = 不在家族</summary>
        internal static int SiphonFlavor(int type) => type switch {
            NPCID.EaterofSouls or NPCID.DevourerHead or NPCID.SeekerHead or NPCID.DarkMummy => EvilBiomeFX.FlavorCorrupt,
            NPCID.Crimera or NPCID.FaceMonster or NPCID.Herpling or NPCID.BloodMummy
                or NPCID.BloodCrawler or NPCID.BloodCrawlerWall => EvilBiomeFX.FlavorCrimson,
            NPCID.CursedHammer => EvilBiomeFX.FlavorCursed,
            NPCID.CrimsonAxe => EvilBiomeFX.FlavorIchor,
            _ => -1,
        };

        /// <summary>汲取回血千分比(0 = 只挂减益不回血);血爬类是家族招牌,汲取最狠</summary>
        internal static int SiphonHealPermille(int type) => type switch {
            NPCID.BloodCrawler or NPCID.BloodCrawlerWall => 80,
            NPCID.BloodMummy => 60,
            NPCID.FaceMonster or NPCID.Herpling or NPCID.DarkMummy => 50,
            NPCID.EaterofSouls or NPCID.Crimera or NPCID.DevourerHead or NPCID.SeekerHead => 40,
            _ => 0,
        };

        /// <summary>蠕虫头(不吃汲取后的位移脉冲)</summary>
        internal static bool IsWormHead(int type) => type is NPCID.DevourerHead or NPCID.SeekerHead;

        /// <summary>死亡定向溅射的风味;-1 = 不溅射</summary>
        private static int SplatterFlavor(int type) => type switch {
            NPCID.Corruptor or NPCID.DarkMummy => EvilBiomeFX.FlavorCorrupt,
            NPCID.FaceMonster or NPCID.Herpling or NPCID.FloatyGross or NPCID.BloodMummy => EvilBiomeFX.FlavorCrimson,
            _ => -1,
        };

        /// <summary>接触减益秒数:汲取系 6/8/10,诅咒锤 3/4/5,猩红斧 4/5/6</summary>
        private static int ContactDebuffSeconds(int type, int tier) {
            if (type == NPCID.CursedHammer) {
                return 2 + tier;
            }
            if (type == NPCID.CrimsonAxe) {
                return 3 + tier;
            }
            return 4 + 2 * tier;
        }

        //====== 机制入口 ======

        public override void PostAI(NPC npc) {
            //空闲路径快出:决策只在权威端
            if (boundTier <= 0 || VaultUtils.isClient) {
                return;
            }
            int flavor = CloudFlavor(npc.type);
            if (flavor < 0) {
                return;
            }
            if (cloudTimer < 0) {
                //首发错拍,避免同屏多只齐射
                cloudTimer = CloudStaggerMin + Main.rand.Next(CloudStaggerSpan);
                return;
            }
            if (cloudTimer > 0) {
                cloudTimer--;
                return;
            }
            cloudTimer = TryCastCloud(npc, flavor)
                ? CloudCooldownBase - CloudCooldownTierStep * (boundTier - 1)
                : 60;//条件不满足时短暂重试
        }

        /// <summary>施放孢核:锁定玩家当前位置为落点,预告体直线漂来,期满绽放瘴云</summary>
        private bool TryCastCloud(NPC npc, int flavor) {
            if (!MechanicEligible(npc)) {
                return false;
            }
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                return false;
            }
            Player player = Main.player[npc.target];
            if (!player.active || player.dead) {
                return false;
            }
            float dist = npc.Distance(player.Center);
            if (dist < CloudMinDist || dist > CloudMaxDist) {
                return false;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                return false;
            }
            if (CountFieldProjectiles() >= CloudGlobalCap) {
                return false;
            }
            //落点与缺口出手即锁定:缺口开在种子行进方向的远侧,顺势撤离即出
            Vector2 lockPoint = player.Center;
            float gapCenter = (lockPoint - npc.Center).ToRotation();
            Vector2 vel = (lockPoint - npc.Center) / ErosionCloudSeed.TravelFrames;
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                ModContent.ProjectileType<ErosionCloudSeed>(), (int)(npc.damage * CloudDamageFrac), 0f,
                Main.myPlayer, flavor, gapCenter, boundTier);
            return true;
        }

        /// <summary>孢核+瘴云现存数(仅触发时扫描,非每帧)</summary>
        private static int CountFieldProjectiles() {
            int seedType = ModContent.ProjectileType<ErosionCloudSeed>();
            int cloudType = ModContent.ProjectileType<ErosionCloudProj>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == seedType || proj.type == cloudType) {
                    count++;
                }
            }
            return count;
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0 || !MechanicEligible(npc)) {
                return;
            }
            int flavor = SiphonFlavor(npc.type);
            if (flavor < 0) {
                return;
            }
            //受击端本机结算减益,原生同步(猩红偷再生=流血断回复,腐化蚀体=虚弱)
            target.AddBuff(EvilBiomeFX.BuffFor(flavor), ContactDebuffSeconds(npc.type, boundTier) * 60);

            //汲取状态实体只由受击者本机生成(owner=受击者),权威端收到后结算回血与后撤
            if (target.whoAmI != Main.myPlayer) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<SiphonBurstProj>(), 0, 0f, Main.myPlayer, npc.whoAmI, npc.type);
        }

        public override void OnKill(NPC npc) {
            //OnKill 本就只在权威端触发,双保险再拦一次
            if (boundTier <= 0 || VaultUtils.isClient) {
                return;
            }
            int flavor = SplatterFlavor(npc.type);
            if (flavor < 0 || !MechanicEligible(npc)) {
                return;
            }
            if (CountActiveOmens() >= DeathBurstGlobalCap) {
                return;
            }
            //锁定死亡瞬间最近的存活玩家方向;附近无人则不放(预告即承诺,此后不再重瞄)
            Player nearest = null;
            float best = SplatterRelevantDist;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                float dist = npc.Distance(player.Center);
                if (dist < best) {
                    best = dist;
                    nearest = player;
                }
            }
            if (nearest == null) {
                return;
            }
            float lockAngle = (nearest.Center - npc.Center).ToRotation();
            Projectile.NewProjectile(npc.GetSource_Death(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<VileBurstOmen>(), (int)(npc.damage * LanceDamageFrac), 0f,
                Main.myPlayer, lockAngle, flavor, boundTier);
        }

        /// <summary>凝核现存数(仅死亡时扫描)</summary>
        private static int CountActiveOmens() {
            int omenType = ModContent.ProjectileType<VileBurstOmen>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == omenType) {
                    count++;
                }
            }
            return count;
        }
    }
}
