using CalamityOverhaul.Content.GameModes.BrutalMobs.JungleHell.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.JungleHell
{
    /// <summary>
    /// 残酷模式丛林+地狱小怪行为机制层，主题「伏击与弹幕幕」。
    /// 不接管原版 AI，只做叠加：齐射幕(蜂族/恶魔/血魔/龟壳)、藤蔓鞭击(食人花族)、
    /// 小鬼传送开窗、骨蛇破土预告、闻血狂暴(鱼/蝠/蛛)。
    /// 数值增强由 GameModeNPC 统一负责，此处只加行为
    /// </summary>
    internal class JungleHellNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        #region 调参常量
        /// <summary>各机制族全局并发上限（同屏量产怪的公平与性能阀门）</summary>
        internal const int MaxConcurrent = 6;
        /// <summary>档位每级冷却缩短比例</summary>
        private const float TierCooldownStep = 0.12f;

        //齐射幕族
        private const float VolleyMinRange = 170f;
        private const float VolleyMaxRange = 720f;
        private const int VolleyCooldownHornet = 300;
        private const int VolleyCooldownDemon = 340;
        private const int VolleyCooldownDevil = 320;
        private const int VolleyCooldownStagger = 60;
        /// <summary>走廊缺口中心相对瞄准线的最大随机偏移（弧度），生成瞬间锁定</summary>
        private const float CorridorOffsetMax = 0.22f;
        private const float StingerDamageFrac = 0.5f;
        private const float ScytheDamageFrac = 0.6f;
        private const float TridentDamageFrac = 0.65f;

        //龟壳崩棘（旋转冲锋起跳瞬间植下反应式齐射预兆）
        private const int TortoiseSpinCooldown = 300;
        /// <summary>识别起旋：上一帧慢于此值</summary>
        private const float TortoisePrevSpeedMax = 2.5f;
        /// <summary>识别起旋：当前帧快于此值</summary>
        private const float TortoiseLaunchSpeedMin = 5.5f;
        private const float SpikeDamageFrac = 0.55f;

        //藤蔓鞭击族
        private const float WhipMinRange = 170f;
        private const float WhipMaxRange = 430f;
        private const int WhipCooldown = 260;
        private const int WhipCooldownStagger = 50;
        private const float WhipDamageFrac = 0.65f;

        //小鬼传送开窗
        /// <summary>单帧位移超过此距离视为传送（施法者原地不动，击退远小于此）</summary>
        private const float TeleportJumpDist = 128f;
        private const int ImpWindowCooldown = 90;
        private const float ImpBoltDamageFrac = 0.5f;
        private const float ImpTargetRange = 1000f;

        //骨蛇破土预告 + 地下伏击提速
        private const float SerpentOmenRange = 900f;
        private const float SerpentAmbushBase = 0.3f;
        private const float SerpentAmbushPerTier = 0.1f;

        //闻血狂暴族
        private const float FrenzyAdvanceBase = 0.35f;
        private const float FrenzyAdvancePerTier = 0.12f;
        /// <summary>咬伤流血时长（帧），反制=处理自己的减益</summary>
        private const int BiteBleedBase = 240;
        private const int BiteBleedPerTier = 60;

        //出生冷却宽限：刚刷出的个体不许立刻放特殊攻击
        private const int InitialGraceMin = 90;
        private const int InitialGraceRand = 90;
        #endregion

        /// <summary>机制族</summary>
        private enum MechKind : byte
        {
            None,
            /// <summary>齐射幕（蜂族/恶魔/血魔）</summary>
            Volley,
            /// <summary>龟壳崩棘（反应式齐射）</summary>
            Tortoise,
            /// <summary>藤蔓鞭击</summary>
            Whip,
            /// <summary>传送开窗</summary>
            ImpWindow,
            /// <summary>破土预告</summary>
            Serpent,
            /// <summary>闻血狂暴</summary>
            Frenzy,
        }

        /// <summary>本组接管的类型表（大小写变体走 netID 映射回基类型，无需单列）</summary>
        private static readonly HashSet<int> TargetTypes = [
            //丛林
            NPCID.Piranha, NPCID.Arapaima, NPCID.JungleBat, NPCID.GiantFlyingFox,
            NPCID.JungleCreeper, NPCID.JungleCreeperWall,
            NPCID.Hornet, NPCID.HornetFatty, NPCID.HornetHoney, NPCID.HornetLeafy,
            NPCID.HornetSpikey, NPCID.HornetStingy, NPCID.MossHornet,
            NPCID.ManEater, NPCID.Snatcher, NPCID.AngryTrapper, NPCID.GiantTortoise,
            //地狱
            NPCID.Hellbat, NPCID.Lavabat, NPCID.Demon, NPCID.RedDevil,
            NPCID.FireImp, NPCID.BoneSerpentHead,
        ];

        //各机制族存活弹幕计数（服务端决策用，Counter 系统周期重计自愈）
        internal static int LiveVolleyOmens;
        internal static int LiveLashes;
        internal static int LiveAmbushOmens;
        internal static int LiveBreachOmens;

        /// <summary>出生绑定档位，0 = 未增强</summary>
        private int boundTier;
        private MechKind kind;
        /// <summary>资格缓存：0 未判 / 1 通过 / -1 排除（雕像怪等在首帧才可判）</summary>
        private int gate;
        private int cooldown;
        /// <summary>小鬼传送检测的上一帧位置</summary>
        private Vector2 lastPos;
        /// <summary>乌龟起旋检测的上一帧速度</summary>
        private float prevSpeed;
        /// <summary>破土预兆最近一次刷新的帧号（预兆实体每帧回写，各端本地一致）</summary>
        internal int lastOmenFrame = -100000;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => TargetTypes.Contains(entity.type);

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            gate = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            boundTier = tier;
            kind = ResolveKind(npc.type);
            cooldown = InitialGraceMin + Main.rand.Next(InitialGraceRand);
        }

        private static MechKind ResolveKind(int type) {
            if (type == NPCID.Hornet || type == NPCID.HornetFatty || type == NPCID.HornetHoney
                || type == NPCID.HornetLeafy || type == NPCID.HornetSpikey || type == NPCID.HornetStingy
                || type == NPCID.MossHornet || type == NPCID.Demon || type == NPCID.RedDevil) {
                return MechKind.Volley;
            }
            if (type == NPCID.GiantTortoise) {
                return MechKind.Tortoise;
            }
            if (type == NPCID.ManEater || type == NPCID.Snatcher || type == NPCID.AngryTrapper) {
                return MechKind.Whip;
            }
            if (type == NPCID.FireImp) {
                return MechKind.ImpWindow;
            }
            if (type == NPCID.BoneSerpentHead) {
                return MechKind.Serpent;
            }
            return MechKind.Frenzy;
        }

        /// <summary>机制资格：雕像怪/Boss/友方/蠕虫体节等一律排除</summary>
        private static bool MechEligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0) {
                return false;
            }
            if (npc.SpawnedFromStatue || npc.boss || npc.realLife >= 0) {
                return false;
            }
            return true;
        }

        /// <summary>档位冷却折算</summary>
        private int TierCd(int baseCd) => (int)(baseCd * (1f - TierCooldownStep * (boundTier - 1)));

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (gate == 0) {
                gate = MechEligible(npc) ? 1 : -1;
            }
            if (gate < 0) {
                return;
            }

            switch (kind) {
                case MechKind.Frenzy: FrenzyTick(npc); return;
                case MechKind.Volley: VolleyTick(npc); return;
                case MechKind.Tortoise: TortoiseTick(npc); return;
                case MechKind.Whip: WhipTick(npc); return;
                case MechKind.ImpWindow: ImpTick(npc); return;
                case MechKind.Serpent: SerpentTick(npc); return;
            }
        }

        /// <summary>目标有效且在射程内（服务端触发判定用）</summary>
        private static bool TargetInRange(NPC npc, float range, out Player target) {
            target = null;
            if (!npc.HasValidTarget) {
                return false;
            }
            Player player = Main.player[npc.target];
            if (!player.active || player.dead) {
                return false;
            }
            if (npc.Distance(player.Center) > range) {
                return false;
            }
            target = player;
            return true;
        }

        #region 闻血狂暴（鱼/蝠/蛛：目标带流血则提速，全端确定性模拟）
        private void FrenzyTick(NPC npc) {
            if (!npc.HasValidTarget) {
                return;
            }
            Player target = Main.player[npc.target];
            if (!target.active || target.dead || !target.HasBuff(BuffID.Bleeding)) {
                return;
            }
            //鱼类离水翻滚时不狂暴
            bool isFish = npc.type == NPCID.Piranha || npc.type == NPCID.Arapaima;
            if (isFish && !npc.wet) {
                return;
            }

            Vector2 advance = npc.velocity * (FrenzyAdvanceBase + FrenzyAdvancePerTier * (boundTier - 1));
            if (!npc.noTileCollide) {
                advance = Collision.TileCollision(npc.position, advance, npc.width, npc.height);
            }
            npc.position += advance;

            //嗜血余迹，纯客户端表现
            if (!Main.dedServ && Main.rand.NextBool(7)) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    DustID.Blood, 0f, 0f, 120, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.velocity = -npc.velocity * 0.2f;
                dust.noGravity = true;
            }
        }
        #endregion

        #region 齐射幕（蜂族毒刺 / 恶魔镰刃 / 血魔三叉戟）
        private void VolleyTick(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
                return;
            }
            if (LiveVolleyOmens >= MaxConcurrent || !TargetInRange(npc, VolleyMaxRange, out Player target)) {
                return;
            }
            float dist = npc.Distance(target.Center);
            if (dist < VolleyMinRange) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                return;
            }

            int mode = npc.type == NPCID.Demon ? JhVolleyOmen.ModeDemon
                : npc.type == NPCID.RedDevil ? JhVolleyOmen.ModeDevil : JhVolleyOmen.ModeHornet;
            float frac = mode == JhVolleyOmen.ModeDemon ? ScytheDamageFrac
                : mode == JhVolleyOmen.ModeDevil ? TridentDamageFrac : StingerDamageFrac;
            SpawnVolley(npc, mode, (target.Center - npc.Center).ToRotation(), frac);

            int baseCd = mode == JhVolleyOmen.ModeDemon ? VolleyCooldownDemon
                : mode == JhVolleyOmen.ModeDevil ? VolleyCooldownDevil : VolleyCooldownHornet;
            cooldown = TierCd(baseCd) + Main.rand.Next(VolleyCooldownStagger);
        }

        /// <summary>植下齐射预兆：瞄角与走廊偏移在此刻锁定（预告即承诺），参数全部随生成包同步</summary>
        private void SpawnVolley(NPC npc, int mode, float aim, float damageFrac) {
            int damage = Math.Max(1, (int)(npc.damage * damageFrac));
            float corridorOffset = Main.rand.NextFloat(-CorridorOffsetMax, CorridorOffsetMax);
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, aim.ToRotationVector2(),
                ModContent.ProjectileType<JhVolleyOmen>(), damage, 0f, Main.myPlayer,
                JhVolleyOmen.Pack(mode, boundTier), aim, corridorOffset);
            LiveVolleyOmens++;
        }
        #endregion

        #region 龟壳崩棘（起旋瞬间沿冲锋线植下延迟棘幕，双拍读招）
        private void TortoiseTick(NPC npc) {
            float speed = npc.velocity.Length();
            if (VaultUtils.isClient) {
                prevSpeed = speed;
                return;
            }
            if (cooldown > 0) {
                cooldown--;
            }
            //旋转冲锋在缩壳瞬间锁定弹道，起跳帧速度陡增；棘幕沿同一条锁定线布设
            if (cooldown <= 0 && prevSpeed < TortoisePrevSpeedMax && speed > TortoiseLaunchSpeedMin
                && LiveVolleyOmens < MaxConcurrent && npc.HasValidTarget) {
                SpawnVolley(npc, JhVolleyOmen.ModeTortoise, npc.velocity.ToRotation(), SpikeDamageFrac);
                cooldown = TierCd(TortoiseSpinCooldown);
            }
            prevSpeed = speed;
        }
        #endregion

        #region 藤蔓鞭击（食人花族：预告延伸鞭打，超出常规藤蔓够不到的距离）
        private void WhipTick(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
                return;
            }
            if (LiveLashes >= MaxConcurrent || !TargetInRange(npc, WhipMaxRange, out Player target)) {
                return;
            }
            float dist = npc.Distance(target.Center);
            if (dist < WhipMinRange) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                return;
            }

            //鞭线基点/方向/长度在此刻全部锁定
            float reach = MathHelper.Clamp(dist + 40f, 120f, WhipMaxRange);
            float aim = (target.Center - npc.Center).ToRotation();
            int buffMode = npc.type == NPCID.AngryTrapper ? 1 : 0;
            int damage = Math.Max(1, (int)(npc.damage * WhipDamageFrac));
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, aim.ToRotationVector2(),
                ModContent.ProjectileType<JhVineLash>(), damage, 0f, Main.myPlayer,
                boundTier * 4 + buffMode, reach, npc.whoAmI);
            LiveLashes++;
            cooldown = TierCd(WhipCooldown) + Main.rand.Next(WhipCooldownStagger);
        }
        #endregion

        #region 小鬼传送开窗（传送后 45 帧可见凝形，锁向后才开火）
        private void ImpTick(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
            }
            Vector2 prev = lastPos;
            lastPos = npc.position;
            if (prev == Vector2.Zero || cooldown > 0) {
                return;
            }
            //单帧大位移 = 传送落点，此处开窗
            if (Vector2.DistanceSquared(prev, npc.position) < TeleportJumpDist * TeleportJumpDist) {
                return;
            }
            if (LiveAmbushOmens >= MaxConcurrent || !TargetInRange(npc, ImpTargetRange, out Player target)) {
                return;
            }

            int damage = Math.Max(1, (int)(npc.damage * ImpBoltDamageFrac));
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<JhAmbushOmen>(), damage, 0f, Main.myPlayer,
                npc.whoAmI + boundTier * 1000, target.whoAmI, JhAmbushOmen.UnlockedAngle);
            LiveAmbushOmens++;
            cooldown = ImpWindowCooldown;
        }
        #endregion

        #region 骨蛇破土预告（地下逼近时尘柱示警，预兆在场才吃伏击提速）
        private void SerpentTick(NPC npc) {
            bool underground = Collision.SolidCollision(npc.position, npc.width, npc.height);
            bool omenLive = (int)Main.GameUpdateCount - lastOmenFrame < 4;

            //伏击提速：只在"预兆可见"时生效，读同步速度全端确定性推进
            if (underground && omenLive) {
                npc.position += npc.velocity * (SerpentAmbushBase + SerpentAmbushPerTier * (boundTier - 1));
            }

            if (VaultUtils.isClient) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
                return;
            }
            if (!underground || omenLive || LiveBreachOmens >= MaxConcurrent
                || !TargetInRange(npc, SerpentOmenRange, out _)) {
                return;
            }
            //残留渐隐中的旧预兆还在就不重复挂（低频触发路径才走这次扫描）
            if (HasLiveBreachOmen(npc.whoAmI)) {
                cooldown = 10;
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<JhBreachOmen>(), 0, 0f, Main.myPlayer, npc.whoAmI);
            LiveBreachOmens++;
            cooldown = 30;
        }

        private static bool HasLiveBreachOmen(int npcWhoAmI) {
            int type = ModContent.ProjectileType<JhBreachOmen>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == npcWhoAmI) {
                    return true;
                }
            }
            return false;
        }
        #endregion

        /// <summary>撕咬者挂流血（命中方本机结算，原生同步）；流血是闻血狂暴族的触发器</summary>
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0) {
                return;
            }
            //出生帧的接触咬合可能先于 PostAI 的懒判定，这里同口径补判（堵雕像怪首帧挂流血的窗口）
            if (gate == 0) {
                gate = MechEligible(npc) ? 1 : -1;
            }
            if (gate < 0) {
                return;
            }
            bool biter = npc.type == NPCID.Piranha || npc.type == NPCID.Arapaima
                || npc.type == NPCID.GiantFlyingFox || npc.type == NPCID.BoneSerpentHead;
            if (!biter) {
                return;
            }
            target.AddBuff(BuffID.Bleeding, BiteBleedBase + BiteBleedPerTier * (boundTier - 1));
        }
    }

    /// <summary>周期重计各机制族的存活弹幕数，自愈式并发上限（服务端决策专用）</summary>
    internal class JungleHellCounterSystem : ModSystem
    {
        private const int RecountInterval = 30;

        public override void PostUpdateProjectiles() {
            if (VaultUtils.isClient || Main.GameUpdateCount % RecountInterval != 0) {
                return;
            }
            int volley = 0, lash = 0, ambush = 0, breach = 0;
            int tVolley = ModContent.ProjectileType<JhVolleyOmen>();
            int tLash = ModContent.ProjectileType<JhVineLash>();
            int tAmbush = ModContent.ProjectileType<JhAmbushOmen>();
            int tBreach = ModContent.ProjectileType<JhBreachOmen>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == tVolley) {
                    volley++;
                }
                else if (proj.type == tLash) {
                    lash++;
                }
                else if (proj.type == tAmbush) {
                    ambush++;
                }
                else if (proj.type == tBreach) {
                    breach++;
                }
            }
            JungleHellNPC.LiveVolleyOmens = volley;
            JungleHellNPC.LiveLashes = lash;
            JungleHellNPC.LiveAmbushOmens = ambush;
            JungleHellNPC.LiveBreachOmens = breach;
        }
    }
}
