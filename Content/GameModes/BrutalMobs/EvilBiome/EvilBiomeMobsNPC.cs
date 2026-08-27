using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome
{
    /// <summary>
    /// 邪恶生物群系小怪的残酷模式行为机制层(主题:侵蚀与汲取)。
    /// 叠加在原版 AI 之上,不接管 AI、不动数值(数值层由 GameModeNPC 统一承担)。
    /// 家族机制:
    /// 1. 场地侵蚀云:施放系(腐化者/灵液黏黏怪/爬藤怪/漂浮怪)释放孢核预告,绽放缓扩瘴云,带具名逃生缺口;
    /// 2. 汲取压制:接触系命中玩家偷走再生(挂原版减益),自身回血并叼食后撤(可见增益经弹幕实体承载);
    /// 3. 扑咬三型:接触系的战斗内主动招——飞行型(噬魂怪/猩红蝇)绕后错位再弧线扑咬,
    ///    地面型(脸怪/跳跳兽)蹲身蓄力低平快扑落地滑步,爬墙型(血腥爬行者两形态)贴面定身弹射;
    /// 4. 木乃伊裹布缠掷(暗黑/血腥木乃伊):抬臂 34 帧直线标记预告,掷出裹布卷,命中挂原版缓速;
    /// 5. 死亡定向溅射:肉厚系死亡先凝 34 帧无害凝核,再放三连邪液溅矛,槽位走廊即缺口。
    /// 具名排除(M6):蠕虫头(吞噬者/搜寻者)不上扑咬——蠕虫压迫感靠原版钻地缠绕,体链注入直线突进会
    /// 拉散体节(镜像 SiphonBurstProj 对蠕虫头免位移脉冲的同一判断),仅保留汲取接触;
    /// 诅咒锤/血肉之斧不上扑咬——原版 AI 本就是蓄力冲锋循环,再叠扑咬属重复编排,保留接触减益时长差异
    /// </summary>
    internal class EvilBiomeMobsNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //====== 具名数值块 ======
        /// <summary>瘴云冷却(档位 1),档位每 +1 缩短一档(只调强度)</summary>
        private const int CloudCooldownBase = 560;
        private const int CloudCooldownTierStep = 60;
        /// <summary>施放距离窗口</summary>
        private const float CloudMinDist = 200f;
        private const float CloudMaxDist = 680f;
        /// <summary>全包首发错拍窗收进 60~180 帧(M7),瘴云/扑咬/缠掷三系共用</summary>
        private const int FirstStaggerMin = 60;
        private const int FirstStaggerSpan = 120;
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

        //====== 扑咬三型(接触系战斗内主动招) ======
        /// <summary>扑咬冷却(档位 1/2/3),任何档位不低于 300 帧;另加随机抖动</summary>
        private static readonly int[] PounceCooldownByTier = [440, 370, 300];
        private const int PounceCooldownJitter = 40;
        /// <summary>条件不满足/预兆缺位/目标失效的重试间隔</summary>
        private const int MoveRetryDelay = 45;
        /// <summary>资格不符(雕像怪等)的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>接触系主动招全局并发闸(扑咬+缠掷合计,仅触发时扫描)</summary>
        private const int ContactEngageCap = 6;
        /// <summary>飞行型:绕后错位帧数(~30)与侧后 45° 锚点距离/缓移速度/触发窗</summary>
        private const int FlyFlankFrames = 30;
        private const float FlyFlankDist = 150f;
        private const float FlyFlankDrift = 2.6f;
        private const float FlyMinDist = 120f;
        private const float FlyMaxDist = 460f;
        /// <summary>飞行型扑咬峰速(档位 1/2/3)、包络分段、弧线初速偏置与漂移后摇帧</summary>
        private static readonly float[] FlyPeakByTier = [10.5f, 11.5f, 12.5f];
        private const int FlyRise = 6;
        private const int FlyHold = 9;
        private const int FlyDecay = 15;
        private const float FlyArcBias = 3.4f;
        private const int FlyRecoverFrames = 14;
        /// <summary>地面型:蹲身蓄力帧数(≥24)与触发窗</summary>
        private const int GroundCrouchFrames = 26;
        private const float GroundMinDistX = 70f;
        private const float GroundMaxDistX = 330f;
        private const float GroundMaxDistY = 150f;
        /// <summary>地面型低平快扑峰速(档位 1/2/3)、包络分段、起跳小抬升与滑步后摇帧</summary>
        private static readonly float[] GroundPeakByTier = [9f, 10f, 11f];
        private const int GroundRise = 4;
        private const int GroundHold = 8;
        private const int GroundDecay = 12;
        private const float GroundHopVy = -2.4f;
        private const int GroundRecoverFrames = 12;
        /// <summary>爬墙型:贴面定身蓄力帧数(≥24)与触发窗</summary>
        private const int WallClingFrames = 26;
        private const float WallMinDist = 90f;
        private const float WallMaxDist = 380f;
        /// <summary>爬墙型弹射峰速(档位 1/2/3)与包络分段(爬升最陡,读作弹射)</summary>
        private static readonly float[] WallPeakByTier = [11f, 12f, 13f];
        private const int WallRise = 3;
        private const int WallHold = 7;
        private const int WallDecay = 10;
        private const int WallRecoverFrames = 12;

        //====== 木乃伊裹布缠掷 ======
        /// <summary>缠掷冷却(档位 1/2/3)与抖动</summary>
        private static readonly int[] WrapCooldownByTier = [520, 460, 400];
        private const int WrapCooldownJitter = 50;
        /// <summary>缠掷触发距离窗(上限略小于裹布射程)</summary>
        private const float WrapMinDist = 140f;
        private const float WrapMaxDist = 420f;
        /// <summary>裹布伤害 = npc.damage(已缩放) × 此系数</summary>
        private const float WrapDamageFrac = 0.5f;
        /// <summary>掷后收臂帧</summary>
        private const int WrapRecoverFrames = 12;

        //====== 扑咬运动三型与主动招相位 ======
        internal const int StyleFly = 0;
        internal const int StyleGround = 1;
        internal const int StyleWall = 2;
        private const byte PhaseIdle = 0;
        private const byte PhaseWindup = 1;
        private const byte PhaseStrike = 2;
        private const byte PhaseRecover = 3;

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
        /// <summary>接触系主动招相位(扑咬/缠掷共用;服务端决策私产)</summary>
        private byte movePhase;
        /// <summary>相位计时:前摇/后摇倒数,扑咬执行段正数走包络</summary>
        private int moveTimer;
        /// <summary>主动招冷却;-1 = 待错拍初始化</summary>
        private int moveCooldown;
        /// <summary>出手瞬间锁定的扑咬方向(此后不再重瞄)</summary>
        private Vector2 lockDir;
        /// <summary>飞行型弧线偏置侧(起手时定死)</summary>
        private float arcSign;
        /// <summary>本次主动招绑定的预兆槽位(服务端私产)</summary>
        private int omenIndex = -1;

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
            moveCooldown = -1;
            omenIndex = -1;
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

        /// <summary>扑咬运动型;-1 = 不上扑咬(排除名单与理由见包头)</summary>
        internal static int PounceStyle(int type) => type switch {
            NPCID.EaterofSouls or NPCID.Crimera => StyleFly,
            NPCID.FaceMonster or NPCID.Herpling => StyleGround,
            NPCID.BloodCrawler or NPCID.BloodCrawlerWall => StyleWall,
            _ => -1,
        };

        /// <summary>各型前摇帧数(蓄势预兆实体与本机相位共用同一时长)</summary>
        internal static int PounceWindupFrames(int style) => style switch {
            StyleGround => GroundCrouchFrames,
            StyleWall => WallClingFrames,
            _ => FlyFlankFrames,
        };

        /// <summary>裹布缠掷家族</summary>
        private static bool IsMummy(int type) => type is NPCID.DarkMummy or NPCID.BloodMummy;

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
            int cloudFlavor = CloudFlavor(npc.type);
            if (cloudFlavor >= 0) {
                CloudStep(npc, cloudFlavor);
                return;
            }
            int style = PounceStyle(npc.type);
            if (style >= 0) {
                PounceStep(npc, style);
                return;
            }
            if (IsMummy(npc.type)) {
                MummyStep(npc);
            }
            //蠕虫头与诅咒锤/血肉之斧无主动招(见包头排除说明),只保留接触结算
        }

        /// <summary>瘴云施放系逐帧:错拍→冷却→施放(逻辑原样,仅首发错拍窗收进 60~180)</summary>
        private void CloudStep(NPC npc, int flavor) {
            if (cloudTimer < 0) {
                //首发错拍,避免同屏多只齐射
                cloudTimer = FirstStaggerMin + Main.rand.Next(FirstStaggerSpan);
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

        //====== 主动招共用助手 ======

        /// <summary>
        /// 提速位移补偿:GameModeNPC.PostAI 对非 Boss 怪按 velocity×SpeedBonus 追加位置推进,
        /// 本层注入的承诺性速度一律除回该系数(位移项除回、重力项不除),
        /// 口径镜像 PumpkinMoonNPC.MoveGain:boss 旗标个体与体节不吃提速层,系数为 1
        /// </summary>
        private float MoveGain(NPC npc) => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        /// <summary>来源打包(槽位+1|类型&lt;&lt;8),预兆实体与 NPC 侧回读共用(镜像沙锥)</summary>
        private static int SrcPack(NPC npc) => (npc.whoAmI + 1) | (npc.type << 8);

        /// <summary>回读绑定预兆:索引+类型+来源三重校验,缺位即中止(失败方向=安全方向)</summary>
        private bool OmenBoundValid(NPC npc, int projType, int srcAiSlot) {
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[omenIndex];
            return proj.active && proj.type == projType && (int)proj.ai[srcAiSlot] == SrcPack(npc);
        }

        /// <summary>接触系主动招现存数(扑咬+缠掷合计,仅触发时扫描)</summary>
        private static int CountEngagedContact() {
            int count = 0;
            foreach (NPC other in Main.ActiveNPCs) {
                if (!TargetTypes.Contains(other.type)) {
                    continue;
                }
                if (other.TryGetGlobalNPC(out EvilBiomeMobsNPC inst) && inst.movePhase != PhaseIdle) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>目标合法性(触发与扑咬前摇逐帧都过)</summary>
        private static bool TargetAlive(NPC npc, out Player player) {
            player = null;
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                return false;
            }
            Player candidate = Main.player[npc.target];
            if (!candidate.active || candidate.dead) {
                return false;
            }
            player = candidate;
            return true;
        }

        private static bool HasLine(NPC npc, Player player)
            => Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1);

        /// <summary>中止主动招:回短冷却;必要时击杀蓄势预兆(提前击杀不播出手爆点)</summary>
        private void AbortMove(NPC npc, bool killOmen) {
            if (killOmen && omenIndex >= 0 && omenIndex < Main.maxProjectiles) {
                Projectile omen = Main.projectile[omenIndex];
                if (omen.active && omen.type == ModContent.ProjectileType<SiphonPounceOmen>()
                    && (int)omen.ai[0] == SrcPack(npc)) {
                    omen.Kill();
                }
            }
            movePhase = PhaseIdle;
            moveTimer = 0;
            omenIndex = -1;
            moveCooldown = MoveRetryDelay;
            npc.netUpdate = true;
        }

        //====== 扑咬三型相位机 ======

        private void PounceStep(NPC npc, int style) {
            switch (movePhase) {
                case PhaseIdle:
                    if (moveCooldown < 0) {
                        //首发错拍(60~180 帧),避免同屏齐扑
                        moveCooldown = FirstStaggerMin + Main.rand.Next(FirstStaggerSpan);
                        return;
                    }
                    if (--moveCooldown > 0) {
                        return;
                    }
                    TryStartPounce(npc, style);
                    return;
                case PhaseWindup:
                    PounceWindupTick(npc, style);
                    return;
                case PhaseStrike:
                    PounceStrikeTick(npc, style);
                    return;
                default:
                    PounceRecoverTick(npc, style);
                    return;
            }
        }

        private static bool PounceGroundReady(NPC npc, Player player) {
            if (npc.velocity.Y != 0f) {
                return false;
            }
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Bottom.Y - npc.Bottom.Y);
            return dx >= GroundMinDistX && dx <= GroundMaxDistX && dy <= GroundMaxDistY && HasLine(npc, player);
        }

        private static bool PounceAirReady(NPC npc, Player player, float min, float max) {
            float dist = npc.Distance(player.Center);
            return dist >= min && dist <= max && HasLine(npc, player);
        }

        private void TryStartPounce(NPC npc, int style) {
            if (!MechanicEligible(npc)) {
                moveCooldown = IneligibleDelay;
                return;
            }
            if (!TargetAlive(npc, out Player player)) {
                moveCooldown = MoveRetryDelay;
                return;
            }
            bool ready = style switch {
                StyleGround => PounceGroundReady(npc, player),
                StyleWall => PounceAirReady(npc, player, WallMinDist, WallMaxDist),
                _ => PounceAirReady(npc, player, FlyMinDist, FlyMaxDist),
            };
            if (!ready || CountEngagedContact() >= ContactEngageCap) {
                moveCooldown = MoveRetryDelay;
                return;
            }
            //蓄势预兆实体:全端可见的前摇信号(尘+辉),生成失败则整次进攻作废
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<SiphonPounceOmen>(), 0, 0f, Main.myPlayer,
                SrcPack(npc), style, boundTier);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                moveCooldown = MoveRetryDelay;
                return;
            }
            omenIndex = omen;
            //弧线偏置侧起手定死(出手不重瞄的一部分)
            arcSign = player.direction;
            movePhase = PhaseWindup;
            moveTimer = PounceWindupFrames(style);
            //刹车脉冲:压速即前摇开始的可见信号
            npc.velocity *= 0.5f;
            npc.netUpdate = true;
        }

        private void PounceWindupTick(NPC npc, int style) {
            //预兆缺位→中止回冷却(失败方向=安全方向);目标失效→连预兆一起撤
            if (!OmenBoundValid(npc, ModContent.ProjectileType<SiphonPounceOmen>(), 0)) {
                AbortMove(npc, killOmen: false);
                return;
            }
            if (!TargetAlive(npc, out Player player)) {
                AbortMove(npc, killOmen: true);
                return;
            }
            switch (style) {
                case StyleGround:
                    //蹲身蓄力:横向压速,纵向交给原版重力
                    npc.velocity.X *= 0.72f;
                    break;
                case StyleWall:
                    //贴面定身
                    npc.velocity *= 0.6f;
                    break;
                default: {
                    //绕后错位:向目标侧后 45° 锚点缓移(压速慢漂,承诺速度除回提速)
                    Vector2 anchor = player.Center + new Vector2(-player.direction * 0.707f, -0.707f) * FlyFlankDist;
                    Vector2 desired = (anchor - npc.Center).SafeNormalize(Vector2.Zero) * (FlyFlankDrift / MoveGain(npc));
                    npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.12f);
                    break;
                }
            }
            if (moveTimer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (--moveTimer > 0) {
                return;
            }
            CommitPounce(npc, style, player);
        }

        /// <summary>出手锁向:此后不再重瞄;扑咬命中沿用 OnHitPlayer 的既有汲取结算</summary>
        private void CommitPounce(NPC npc, int style, Player player) {
            Vector2 to = player.Center - npc.Center;
            if (style == StyleGround && Math.Abs(to.X) < 24f) {
                //目标几乎在正上方,低平快扑无从谈起,安全放弃
                AbortMove(npc, killOmen: false);
                return;
            }
            lockDir = to.SafeNormalize(Vector2.UnitX * npc.direction);
            if (style == StyleGround) {
                //起跳小抬升(位移承诺除回提速,此后纵向交给原版重力)
                npc.velocity.Y = GroundHopVy / MoveGain(npc);
            }
            movePhase = PhaseStrike;
            moveTimer = 0;
            npc.netUpdate = true;
        }

        private void PounceStrikeTick(NPC npc, int style) {
            moveTimer++;
            float gain = MoveGain(npc);
            switch (style) {
                case StyleGround: {
                    //低平快扑:只塑形横向,纵向交给原版重力,落地自然转滑步
                    float env = MobDash.Envelope(moveTimer, GroundRise, GroundHold, GroundDecay);
                    npc.velocity.X = Math.Sign(lockDir.X) * GroundPeakByTier[boundTier - 1] * env / gain;
                    if (moveTimer >= GroundRise + GroundHold + GroundDecay) {
                        EnterRecover(npc, GroundRecoverFrames);
                        return;
                    }
                    break;
                }
                case StyleWall: {
                    //弹射扑咬:全向直线,爬升段最陡
                    float env = MobDash.Envelope(moveTimer, WallRise, WallHold, WallDecay);
                    npc.velocity = lockDir * (WallPeakByTier[boundTier - 1] * env / gain);
                    if (moveTimer >= WallRise + WallHold + WallDecay) {
                        EnterRecover(npc, WallRecoverFrames);
                        return;
                    }
                    break;
                }
                default: {
                    //弧线扑咬:垂向初速偏置随包络前段渐消,路径向锁定线收拢(不重瞄)
                    float env = MobDash.Envelope(moveTimer, FlyRise, FlyHold, FlyDecay);
                    float bias = FlyArcBias * MathHelper.Clamp(1f - moveTimer / (float)(FlyRise + FlyHold), 0f, 1f);
                    Vector2 perp = new(-lockDir.Y, lockDir.X);
                    npc.velocity = (lockDir * FlyPeakByTier[boundTier - 1] + perp * (arcSign * bias)) * (env / gain);
                    if (moveTimer >= FlyRise + FlyHold + FlyDecay) {
                        EnterRecover(npc, FlyRecoverFrames);
                        return;
                    }
                    break;
                }
            }
            if (moveTimer % 6 == 0) {
                //执行段低频重推,矫正客户端原版 AI 的速度漂移
                npc.netUpdate = true;
            }
        }

        private void EnterRecover(NPC npc, int frames) {
            movePhase = PhaseRecover;
            moveTimer = frames;
            npc.netUpdate = true;
        }

        private void PounceRecoverTick(NPC npc, int style) {
            switch (style) {
                case StyleGround:
                    //落地滑步:横向残速缓释,纵向交还重力
                    npc.velocity.X *= 0.86f;
                    break;
                case StyleWall:
                    npc.velocity *= 0.8f;
                    break;
                default:
                    //漂移后摇
                    npc.velocity *= 0.88f;
                    break;
            }
            if (moveTimer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (--moveTimer > 0) {
                return;
            }
            //残速已衰,控制权干净还给原版 AI
            movePhase = PhaseIdle;
            omenIndex = -1;
            moveCooldown = PounceCooldownByTier[boundTier - 1] + Main.rand.Next(PounceCooldownJitter + 1);
            npc.netUpdate = true;
        }

        //====== 木乃伊裹布缠掷 ======

        private void MummyStep(NPC npc) {
            switch (movePhase) {
                case PhaseIdle:
                    if (moveCooldown < 0) {
                        //首发错拍(60~180 帧)
                        moveCooldown = FirstStaggerMin + Main.rand.Next(FirstStaggerSpan);
                        return;
                    }
                    if (--moveCooldown > 0) {
                        return;
                    }
                    TryStartWrap(npc);
                    return;
                case PhaseWindup:
                    WrapWindupTick(npc);
                    return;
                default:
                    //收臂后摇:纯计时,不注速度
                    if (--moveTimer <= 0) {
                        movePhase = PhaseIdle;
                        omenIndex = -1;
                        moveCooldown = WrapCooldownByTier[boundTier - 1] + Main.rand.Next(WrapCooldownJitter + 1);
                    }
                    return;
            }
        }

        private void TryStartWrap(NPC npc) {
            if (!MechanicEligible(npc)) {
                moveCooldown = IneligibleDelay;
                return;
            }
            if (!TargetAlive(npc, out Player player)) {
                moveCooldown = MoveRetryDelay;
                return;
            }
            float dist = npc.Distance(player.Center);
            if (dist < WrapMinDist || dist > WrapMaxDist || !HasLine(npc, player)
                || CountEngagedContact() >= ContactEngageCap) {
                moveCooldown = MoveRetryDelay;
                return;
            }
            //掷向出手即锁定(预告即承诺),直线标记预兆承载并在提交帧自持发射
            float lockAngle = (player.Center - npc.Center).ToRotation();
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<MummyWrapOmen>(), Math.Max(1, (int)(npc.damage * WrapDamageFrac)), 0f,
                Main.myPlayer, lockAngle, boundTier, SrcPack(npc));
            if (omen < 0 || omen >= Main.maxProjectiles) {
                moveCooldown = MoveRetryDelay;
                return;
            }
            omenIndex = omen;
            movePhase = PhaseWindup;
            moveTimer = MummyWrapOmen.TelegraphFrames;
            //抬臂定身:压速与标记线共同构成前摇信号
            npc.velocity.X *= 0.4f;
            npc.netUpdate = true;
        }

        private void WrapWindupTick(NPC npc) {
            //标记被移走(槽位被顶)→中止;木乃伊自死时标记自会取消(镜像沙锥语义,不必在此收拾)
            if (!OmenBoundValid(npc, ModContent.ProjectileType<MummyWrapOmen>(), 2)) {
                AbortMove(npc, killOmen: false);
                return;
            }
            npc.velocity.X *= 0.7f;
            if (moveTimer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (--moveTimer <= 0) {
                //发射由预兆在提交帧自持完成,这里只转入收臂
                movePhase = PhaseRecover;
                moveTimer = WrapRecoverFrames;
                npc.netUpdate = true;
            }
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
