using System;
using System.Linq;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 三槽铭刻叠算出的战斗档。原铭「鬼切」与空铭恒为 <see cref="Identity"/>，
    /// 不进任何特殊分支；倍率字段由 <see cref="OniMeiDefinition.ModifyCombatProfile"/> 逐槽叠乘，
    /// 语义开关按铭点亮
    /// </summary>
    public struct OniMeiCombatProfile
    {
        //====通用倍率(恒 1 即无改动)====
        /// <summary>武器面板伤害倍率(髭切 0.90)</summary>
        public float DamageMul;
        /// <summary>连段排拍间隔倍率(狮子之子 1.10)</summary>
        public float ComboGapMul;
        /// <summary>手持期间所受最终伤害倍率(友切 1.10)；肢解反噬的固定契约除外</summary>
        public float IncomingDamageMul;
        /// <summary>疾走气力消耗倍率(风樋 0.75)</summary>
        public float DashVigorCostMul;
        /// <summary>樱流每帧耗气倍率(风樋 0.70)</summary>
        public float SakuraDrainMul;
        /// <summary>疾走墨痕伤害倍率(风樋 0.75)</summary>
        public float FlashMarkDamageMul;
        /// <summary>自然回气倍率(血樋 0.50)</summary>
        public float NaturalRegenMul;
        /// <summary>招式消耗后的额外回气延迟(帧，血樋 +24)</summary>
        public int ExtraRegenDelayTicks;
        /// <summary>连段每拍首次命中的额外回气(血樋 +2)</summary>
        public float ComboHitVigorBonus;
        /// <summary>残心首次命中的额外回气(血樋 +8)</summary>
        public float ZanshinHitVigorBonus;
        /// <summary>常规架势获取倍率(不动 0.80)</summary>
        public float StanceGainMul;
        /// <summary>气力上限倍率(倶利伽罗 0.80)</summary>
        public float VigorMaxMul;

        //====语义开关(各铭的个性化机制)====
        /// <summary>髭切「断首」：残心/灭世对斩杀线内目标终结增益，击杀返势</summary>
        public bool ExecuteLowLifeBonus;
        /// <summary>狮子之子「狮势」：完整五拍逐拍蓄势，第五拍合颚副斩</summary>
        public bool LionRoar;
        /// <summary>友切「咎影」：疾走取消连段留延迟斩影并积咎</summary>
        public bool GuiltEcho;
        /// <summary>不动「不动护」：承诺动作中受击可耗架势削减该击</summary>
        public bool StanceGuard;
        /// <summary>倶利伽罗「龙火回环」：处决后窗口内完整连段第五拍龙火副斩</summary>
        public bool DragonfireLoop;
        /// <summary>风樋「顺风」：疾走/墨痕的介质更轻更窄(纯表现)</summary>
        public bool WindGroove;
        /// <summary>血樋「回流」：命中回气的湿墨表现(纯表现)</summary>
        public bool BloodGroove;
        /// <summary>铁截「截金」：连段本拍首击对钢铁体额外加深</summary>
        public bool IronSever;
        /// <summary>滞樋「滞缚」：授权命中黏敌；疾走起步自黏</summary>
        public bool StickyBind;
        /// <summary>闲樋「闲息」：脱战自然回气加快；交战耗气税由 Roster 倍率承担</summary>
        public bool QuietBreath;
        /// <summary>镇鸣「镇弹」：受弹伤/击退削弱</summary>
        public bool QuellProjectiles;
        /// <summary>旧首「取首」：残心/灭世仅对头/非蠕虫节残血加深（无返势）</summary>
        public bool HeadHunt;
        /// <summary>默切「默杀」：疾走结束后短窗内下一记普连/残心加深</summary>
        public bool SilentKill;
        /// <summary>痺雕「痺反」：护身或穿身格挡成功反麻来手</summary>
        public bool NumbCounter;
        /// <summary>止足「止步」：立定充电后残心/灭世/第五拍加深</summary>
        public bool PlantedStep;
        /// <summary>谢樋「剪落」：击杀/了结时邻域溅小剪刃</summary>
        public bool PetalPrune;
        /// <summary>潮樋「潮拍」：合潮窗命中奖气；错拍连段略亏</summary>
        public bool TideBeat;
        /// <summary>虚吼「空鸣」：空场周期威压；远离再近一刀；贴身失焦</summary>
        public bool HollowRoar;
        /// <summary>息合「吐息刀压」：短蓄松手行进定锚断斩链</summary>
        public bool BreathWave;
        /// <summary>焦樋「焦痕」：疾走路径留短灼地</summary>
        public bool ScorchTrail;
        /// <summary>余炎「余烬场」：处决后焦点留持续灼地</summary>
        public bool EmberField;
        /// <summary>假切「假身」：疾走起步残影替真身吸一击</summary>
        public bool FalseBody;

        /// <summary>严格基准档：所有倍率恒等，所有开关关闭</summary>
        public static OniMeiCombatProfile Identity => new() {
            DamageMul = 1f,
            ComboGapMul = 1f,
            IncomingDamageMul = 1f,
            DashVigorCostMul = 1f,
            SakuraDrainMul = 1f,
            FlashMarkDamageMul = 1f,
            NaturalRegenMul = 1f,
            ExtraRegenDelayTicks = 0,
            ComboHitVigorBonus = 0f,
            ZanshinHitVigorBonus = 0f,
            StanceGainMul = 1f,
            VigorMaxMul = 1f,
        };
    }

    /// <summary>
    /// 铭刻效果层统一入口：战斗侧一律从"那把刀"的 <see cref="OnikiriData.Mei"/> 解析，
    /// 不读 UI 的 DisplayStore 缓存；铭数据随物品存档/联机同步，各端解析结果一致
    /// </summary>
    internal static class OniMeiCombat
    {
        //====髭切「断首」调参====
        /// <summary>断首线：目标(蠕虫归主体)生命比低于此值进入终结区间</summary>
        public const float ExecuteThreshold = 0.35f;
        /// <summary>非 boss 目标在斩杀线底端的最大终结加成(1→1.5)</summary>
        public const float ExecuteMaxBonus = 0.5f;
        /// <summary>boss 目标单独限幅(1→1.25)</summary>
        public const float ExecuteBossMaxBonus = 0.25f;
        /// <summary>断首击杀返还架势(每次招式至多一次)</summary>
        public const float ExecuteKillStanceRefund = 10f;

        //====L0 独特化调参====
        /// <summary>铁截：钢铁体连段首击伤害倍率</summary>
        public const float IronSeverSteelHitMul = 1.30f;
        /// <summary>滞樋：命中黏敌时长(帧)</summary>
        public const int StickyBindTargetSlowTicks = 45;
        /// <summary>滞樋：疾走起步自黏时长(帧)</summary>
        public const int StickyBindSelfSlowTicks = 20;
        /// <summary>闲樋：无命中记忆刷新视为脱战的窗口(帧)</summary>
        public const int QuietBreathColdTicks = 180;
        /// <summary>闲樋：脱战时自然回气额外倍率(叠在 NaturalRegenMul 上)</summary>
        public const float QuietBreathRegenMul = 3f;
        /// <summary>镇鸣：受弹最终伤害倍率</summary>
        public const float QuellProjectileDamageMul = 0.88f;
        /// <summary>镇鸣：受弹击退倍率</summary>
        public const float QuellProjectileKnockbackMul = 0.35f;

        //====M1 独特化调参====
        /// <summary>旧首：非 boss 头/主体在斩杀线底端的最大加成(1→1.65)</summary>
        public const float HeadHuntMaxBonus = 0.65f;
        /// <summary>旧首：boss 头单独限幅(1→1.35)</summary>
        public const float HeadHuntBossMaxBonus = 0.35f;
        /// <summary>默切：疾走结束后默杀窗(帧)</summary>
        public const int SilentKillWindowTicks = 45;
        /// <summary>默切：窗内下一记加深倍率</summary>
        public const float SilentKillHitMul = 1.35f;
        /// <summary>痺反：来手 Slow 时长(帧)</summary>
        public const int NumbCounterSlowTicks = 36;
        /// <summary>止足：低位移累计达此帧数视为立定就绪</summary>
        public const int PlantedChargeNeedTicks = 45;
        /// <summary>止足：速度平方阈(\|v\|≈1.5)</summary>
        public const float PlantedSpeedSq = 2.25f;
        /// <summary>止足：受击击退后不清充的宽容(帧)</summary>
        public const int PlantedKnockbackGraceTicks = 12;
        /// <summary>止足：大招/第五拍加深倍率</summary>
        public const float PlantedStepHitMul = 1.25f;
        /// <summary>残心同帧默杀×止足叠乘软帽</summary>
        public const float SilentPlantedSoftCap = 1.55f;

        //====M2 独特化调参====
        /// <summary>剪落：邻域溅射半径</summary>
        public const float PetalPruneRadius = 220f;
        /// <summary>剪落：断斩相对武器伤害</summary>
        public const float PetalPruneDamageMul = 0.22f;
        /// <summary>剪落：连环门闩(帧)</summary>
        public const int PetalPruneCooldownTicks = 36;
        /// <summary>剪落：空残心扣气</summary>
        public const float PetalPruneEmptyZanshinVigor = 2f;
        /// <summary>潮拍：潮汐周期(帧)</summary>
        public const int TidePeriodTicks = 40;
        /// <summary>潮拍：合潮半宽(帧)</summary>
        public const int TideWindowHalf = 10;
        /// <summary>潮拍：合潮授权命中回气</summary>
        public const float TideOnBeatVigor = 4f;
        /// <summary>潮拍：错拍连段授权首击伤</summary>
        public const float TideOffBeatHitMul = 0.90f;
        /// <summary>空鸣：冷战/空场阈(帧)</summary>
        public const int HollowRoarColdTicks = 90;
        /// <summary>空鸣：威压脉冲间隔(帧)</summary>
        public const int HollowRoarInterval = 75;
        /// <summary>空鸣：威压 Slow 半径</summary>
        public const float HollowRoarRadius = 320f;
        /// <summary>空鸣：威压 Slow 时长(帧)</summary>
        public const int HollowRoarSlowTicks = 24;
        /// <summary>空鸣：远离再近一刀加深</summary>
        public const float HollowApproachHitMul = 1.18f;
        /// <summary>空鸣：贴身失焦连砍伤</summary>
        public const float HollowFocusLossHitMul = 0.88f;
        /// <summary>空鸣：近距判定半径</summary>
        public const float HollowNearRadius = 280f;
        /// <summary>空鸣：短窗内授权命中达此次数视为失焦</summary>
        public const int HollowFocusLossHitNeed = 3;
        /// <summary>空鸣：失焦统计窗(帧)</summary>
        public const int HollowFocusLossWindowTicks = 48;

        //====H0 息合吐息刀压====
        /// <summary>息合：短蓄松手最低帧</summary>
        public const int BreathMinChargeTicks = 21;
        /// <summary>息合：首拍前摇满帧(按满则出连)</summary>
        public const int BreathMaxChargeTicks = 36;
        /// <summary>息合：吐息气耗</summary>
        public const float BreathWaveVigorCost = 22f;
        /// <summary>息合：空蓄受击白扣气</summary>
        public const float BreathCancelVigorTax = 8f;
        /// <summary>息合：蓄息速度倍率</summary>
        public const float BreathChargeSlowMul = 0.55f;
        /// <summary>息合：断斩链段数</summary>
        public const int BreathWaveSegments = 4;
        /// <summary>息合：段间距(世界单位)</summary>
        public const float BreathWaveSpacing = 88f;
        /// <summary>息合：段间隔(帧)</summary>
        public const int BreathWaveSegmentDelay = 10;
        /// <summary>息合：单段相对武器伤害</summary>
        public const float BreathWaveDamageMul = 0.16f;

        //====H1 灼地共型====
        /// <summary>焦痕：灼地寿命(帧)</summary>
        public const int ScorchLifeTicks = 90;
        /// <summary>焦痕：视觉规模</summary>
        public const float ScorchScale = 0.85f;
        /// <summary>焦痕：相对武器伤害</summary>
        public const float ScorchDamageMul = 0.18f;
        /// <summary>焦痕：路径采样间距</summary>
        public const float ScorchSampleDist = 48f;
        /// <summary>焦痕：单次疾走最多坑数</summary>
        public const int ScorchMaxPerDash = 6;
        /// <summary>余烬：灼地寿命(帧)</summary>
        public const int EmberLifeTicks = 240;
        /// <summary>余烬：视觉规模</summary>
        public const float EmberScale = 1.25f;
        /// <summary>余烬：相对武器伤害</summary>
        public const float EmberDamageMul = 0.22f;
        /// <summary>余烬场在时疾走耗气倍率</summary>
        public const float EmberFieldDashCostMul = 1.15f;
        /// <summary>同点刷新距离阈</summary>
        public const float BurnRefreshRadius = 48f;

        //====H2 假身====
        /// <summary>假身：残影寿命(帧)</summary>
        public const int FalseBodyLifeTicks = 120;
        /// <summary>假身在场：承伤额外倍率</summary>
        public const float FalseBodyIncomingMul = 1.12f;
        /// <summary>假身在场：疾走耗气倍率</summary>
        public const float FalseBodyDashCostMul = 1.12f;
        /// <summary>影破真空：持续时间(帧)</summary>
        public const int FalseBodyVacuumTicks = 45;
        /// <summary>影破真空：承伤倍率</summary>
        public const float FalseBodyVacuumIncomingMul = 1.15f;

        /// <summary>潮拍：相位是否落在合潮窗(窗心在周期中点)</summary>
        public static bool IsTideOnBeat(int tidePhase) {
            int period = TidePeriodTicks;
            if (period <= 0) {
                return false;
            }
            int phase = ((tidePhase % period) + period) % period;
            int center = period / 2;
            int dist = Math.Abs(phase - center);
            dist = Math.Min(dist, period - dist);
            return dist <= TideWindowHalf;
        }

        /// <summary>按物品解析三槽合成档；非鬼切/空数据返回 Identity</summary>
        public static OniMeiCombatProfile Resolve(Item item) {
            OniMeiCombatProfile profile = OniMeiCombatProfile.Identity;
            OnikiriData data = OnikiriData.TryGet(item);
            if (data == null) {
                return profile;
            }
            foreach (OniMeiSlotKind slot in OniMeiStore.SlotKinds) {
                OniMeiRegistry.GetEngraved(data.Mei, slot)?.ModifyCombatProfile(ref profile);
            }
            return profile;
        }

        /// <summary>按玩家手中物品解析(含鼠标项)；未持刀返回 Identity</summary>
        public static OniMeiCombatProfile ResolveHeld(Player player)
            => player == null ? OniMeiCombatProfile.Identity : Resolve(player.GetItem());

        /// <summary>蠕虫类归主体，生命池共享的节段按头结算</summary>
        private static NPC RootOf(NPC npc)
            => npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? Main.npc[npc.realLife] : npc;

        /// <summary>
        /// 髭切「断首」或旧首「取首」终结倍率：目标已入斩杀线时随已损生命递增；
        /// 未装对应铭/未入线/旧首打节体返回 false。残心/灭世的 ModifyHitNPC 调用(owner 端)
        /// </summary>
        public static bool TryGetExecuteBonus(Player owner, NPC target, out float mul) {
            mul = 1f;
            if (target == null) {
                return false;
            }
            OniMeiCombatProfile profile = ResolveHeld(owner);
            bool headHunt = profile.HeadHunt;
            bool execute = profile.ExecuteLowLifeBonus;
            if (!execute && !headHunt) {
                return false;
            }
            if (headHunt && !IsHeadHuntTarget(target)) {
                return false;
            }
            NPC root = RootOf(target);
            if (root.lifeMax <= 0) {
                return false;
            }
            float frac = MathHelper.Clamp(root.life / (float)root.lifeMax, 0f, 1f);
            if (frac >= ExecuteThreshold) {
                return false;
            }
            float depth = 1f - frac / ExecuteThreshold;
            float cap = headHunt
                ? (root.boss ? HeadHuntBossMaxBonus : HeadHuntMaxBonus)
                : (root.boss ? ExecuteBossMaxBonus : ExecuteMaxBonus);
            mul = 1f + depth * cap;
            return true;
        }

        /// <summary>旧首可取首位：命中为 Root 自身，或非蠕虫节体表内类型</summary>
        private static bool IsHeadHuntTarget(NPC target) {
            if (target.whoAmI == RootOf(target).whoAmI) {
                return true;
            }
            return !CWRLoad.WormBodys.Contains(target.type);
        }

        /// <summary>痺反：对来手叠短 Slow；无源/未装返回 false</summary>
        public static bool TryApplyNumbCounter(Player owner, NPC source) {
            if (source == null || !source.active || !ResolveHeld(owner).NumbCounter) {
                return false;
            }
            source.AddBuff(BuffID.Slow, NumbCounterSlowTicks);
            return true;
        }

        /// <summary>
        /// 断首命中收尾(owner 端 OnHitNPC 调用)：入线命中画断线；
        /// 由本招式了结目标时返还架势(refunded 保证每次招式至多一次)
        /// </summary>
        public static void OnExecuteStrikeHit(Player owner, NPC target, float cutAngle, ref bool refunded) {
            if (target == null || !ResolveHeld(owner).ExecuteLowLifeBonus) {
                return;
            }
            NPC root = RootOf(target);
            float frac = root.lifeMax > 0 ? root.life / (float)root.lifeMax : 1f;
            bool killed = !root.active || root.life <= 0;
            if (!killed && frac >= ExecuteThreshold) {
                return;
            }
            OniMeiStrikes.SpawnSeverLine(target, cutAngle);
            if (killed && !refunded) {
                refunded = true;
                if (owner.TryGetModPlayer(out OnikiriPlayer okp)) {
                    okp.GrantExecuteRefund();
                }
                OniMeiStrikes.SpawnExecuteRefundFleck(owner, target.Center);
            }
        }

        /// <summary>
        /// 铁截「截金」：钢铁/装甲体加深。由连段本拍首击门闸调用；
        /// 未装铁截或非钢体返回 false
        /// </summary>
        public static bool TryApplyIronSever(Player owner, NPC target, ref NPC.HitModifiers modifiers) {
            if (target == null || !ResolveHeld(owner).IronSever) {
                return false;
            }
            if (!CWRLoad.NPCValue.ISTheofSteel(target)) {
                return false;
            }
            modifiers.FinalDamage *= IronSeverSteelHitMul;
            return true;
        }
    }
}
