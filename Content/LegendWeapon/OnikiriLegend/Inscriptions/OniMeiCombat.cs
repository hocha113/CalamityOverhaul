using System;
using Terraria;

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
        /// 髭切「断首」终结倍率：目标已入斩杀线时随已损生命递增；
        /// 未装髭切/未入线返回 false。残心/灭世的 ModifyHitNPC 调用(owner 端，随命中包同步)
        /// </summary>
        public static bool TryGetExecuteBonus(Player owner, NPC target, out float mul) {
            mul = 1f;
            if (target == null || !ResolveHeld(owner).ExecuteLowLifeBonus) {
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
            float cap = root.boss ? ExecuteBossMaxBonus : ExecuteMaxBonus;
            mul = 1f + depth * cap;
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
    }
}
