using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>雨系伤害来源类别，命中挂钩以此分辨滴/瀑/泉/洼/柱</summary>
    internal enum KikasaRainSourceKind : byte
    {
        Drop,
        Pour,
        Geyser,
        Puddle,
        /// <summary>血湖形态血珠入水起的血柱（<see cref="KikasaRains.KikasaBloodColumn"/>），不是三泉</summary>
        Column,
    }

    /// <summary>
    /// 挂钩通用上下文。多数挂钩在所有端运行（弹幕 AI 各端自走），
    /// 实现纪律：伤害与生成投射物只在 <see cref="IsOwnerClient"/> 端做，
    /// 纯表现自行加 !Main.dedServ；会话计量经 <see cref="StateFor"/> 取本符私仓
    /// </summary>
    internal readonly struct KikasaTalismanRainContext(Player owner, KikasaTalismanPlayer session, int slot)
    {
        /// <summary>效果归属玩家（伞主）</summary>
        public readonly Player Owner = owner;

        /// <summary>归属玩家的会话宿主，可空（默认上下文）</summary>
        public readonly KikasaTalismanPlayer Session = session;

        /// <summary>本次派发的符位下标；标签驱动的派发（滴绘制/泉喷发）为 -1</summary>
        public readonly int Slot = slot;

        /// <summary>本端是否为归属端（伤害/生成只在这里做）</summary>
        public bool IsOwnerClient => Owner != null && Owner.whoAmI == Main.myPlayer;

        /// <summary>取该符的会话计量仓（字段语义由符自定，端本地不落盘）</summary>
        public KikasaTalismanSessionState StateFor(KikasaTalismanDefinition definition)
            => definition == null ? null : Session?.GetTalismanState(definition.Key);
    }

    /// <summary>一波墨雨节拍解；基准值（含霖/沛倍率与档位护栏）已算好，挂钩只做增删改</summary>
    internal struct KikasaVolleyRhythm
    {
        /// <summary>节拍周期（帧）</summary>
        public int Period;
        /// <summary>本波滴数</summary>
        public int DropCount;
        /// <summary>错拍间隔（帧）</summary>
        public int Stagger;
        /// <summary>是否齐掷拍（全鬼同帧各掷一滴）</summary>
        public bool GhostVolley;
    }

    /// <summary>墨滴生成上下文（仅所有者端派发）；标签与载荷随生成包免费同步到各端</summary>
    internal struct KikasaDropSpawnContext
    {
        /// <summary>出手点</summary>
        public Vector2 Position;
        /// <summary>初速</summary>
        public Vector2 Velocity;
        /// <summary>体积（1=常规）</summary>
        public float Scale;
        /// <summary>伤害倍率（基准乘区已折入，挂钩继续叠乘）</summary>
        public float DamageMul;
        /// <summary>穿透数（1=命中即灭；仅归属端判伤，无需同步）</summary>
        public int Penetrate;
        /// <summary>锁定目标 whoAmI，-1 无</summary>
        public int TargetWho;
        /// <summary>无目标时的坠落列 X</summary>
        public float FallbackX;
        /// <summary>鬼滴（伞下鬼侧掷）</summary>
        public bool Ghost;
        /// <summary>落地积洼</summary>
        public bool Puddle;
        /// <summary>本滴出自齐掷拍</summary>
        public bool GhostVolley;
        /// <summary>本滴出自墨瀑散射（非伞缘甩出）</summary>
        public bool FromPourScatter;
        /// <summary>波内滴序（散射滴为散射序号）</summary>
        public int DropIndex;
        /// <summary>符标签（0=无；先到先得，已有标签的滴不建议覆盖）</summary>
        public int TagId;
        /// <summary>标签载荷（0..16383，语义由标签符自定）</summary>
        public int TagPayload;
    }

    /// <summary>墨滴弹道参数；各端首帧同参解算，实现必须确定性（禁 Main.rand）</summary>
    internal struct KikasaDropCurve
    {
        /// <summary>上抛力度与头顶偏置的口径（原顶点高度，抛洒追踪制沿用字段名）</summary>
        public float ApexAboveTarget;
        /// <summary>坠落加速度，兼追踪段加速度的基准</summary>
        public float PlungeGravity;
        /// <summary>坠落终速，兼追踪段极速</summary>
        public float PlungeMaxSpeed;
        /// <summary>抛洒段时长口径（帧，实际取其半）</summary>
        public float ArcDur;
    }

    /// <summary>墨滴绘制参数（端本地，仅带符标签的滴派发）</summary>
    internal struct KikasaDropDrawParams
    {
        /// <summary>墨体色</summary>
        public Color Body;
        /// <summary>暗缘色</summary>
        public Color Deep;
        /// <summary>芯色</summary>
        public Color Core;
        /// <summary>画布尺寸倍率（纯视觉，不动判定）</summary>
        public float SizeMul;
        /// <summary>追击穿透态 0~1（缘一线鬼青、体略透）；由滴自填，符一般不动</summary>
        public float Ghost;
    }

    /// <summary>墨洼绘制配色（端本地；宽度走 <see cref="KikasaRains.KikasaInkPuddle"/> 的判定同源旋钮，不在此改）</summary>
    internal struct KikasaPuddleDrawParams
    {
        /// <summary>暗缘色</summary>
        public Color Deep;
        /// <summary>墨体色</summary>
        public Color Body;
        /// <summary>芯线色</summary>
        public Color Core;
        /// <summary>湿反光色</summary>
        public Color Sheen;
    }

    /// <summary>墨瀑生成上下文（仅所有者端派发）；标签打进 ai[1] 量化编码随生成包同步</summary>
    internal struct KikasaPourSpawnContext
    {
        /// <summary>倾泻角（弧度）</summary>
        public float Aim;
        /// <summary>蓄力档 0~1（只读参考，打包时量化到 0.001）</summary>
        public float Fill;
        /// <summary>伤害倍率</summary>
        public float DamageMul;
        /// <summary>符标签（0=无）</summary>
        public int TagId;
    }

    /// <summary>墨泉齐发决策（所有者端，一瀑只派发一次）</summary>
    internal struct KikasaGeyserVolleyContext
    {
        /// <summary>是否喷发（基础条件=湖倾档+满蓄，挂钩可强开/强关）</summary>
        public bool Fire;
        /// <summary>基础条件是否达成（信息位，供挂钩分辨满蓄泉与符泉）</summary>
        public bool FromFullCharge;
        /// <summary>泉数</summary>
        public int Count;
        /// <summary>伤害倍率（沛符乘区之上继续叠乘）</summary>
        public float DamageMul;
        /// <summary>柱高倍率（随泉的 ai[2] 同步到各端）</summary>
        public float HeightMul;
        /// <summary>逐泉错拍延迟（帧）</summary>
        public int DelayStepFrames;
        /// <summary>符标签（0=无，随泉的 ai[1] 同步）</summary>
        public int TagId;
        /// <summary>标签载荷</summary>
        public int TagPayload;
    }

    /// <summary>
    /// 唤雨符挂钩派发器：按归属玩家的 <see cref="KikasaTalismanPlayer.Talismans"/> 解析三符位
    /// （持伞时生效），空绳/未持伞 <see cref="IsEmpty"/>，所有派发短路零开销。
    /// 每 AI 帧解析一次快照即可复用；派发次序=符位序（0→2），先到先得
    /// </summary>
    internal readonly struct KikasaTalismanHookRunner
    {
        private readonly Player owner;
        private readonly KikasaTalismanStore store;
        private readonly KikasaTalismanPlayer session;

        internal KikasaTalismanHookRunner(Player owner, KikasaTalismanStore store, KikasaTalismanPlayer session) {
            this.owner = owner;
            this.store = store;
            this.session = session;
        }

        /// <summary>空绳/未持伞：真时所有派发直接短路</summary>
        public bool IsEmpty => store == null;

        private KikasaTalismanRainContext Ctx(int slot) => new(owner, session, slot);

        public void OnRainStart(Projectile umbrella) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.OnRainStart(Ctx(slot), umbrella);
            }
        }

        public void OnRecall(Projectile umbrella) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.OnRecall(Ctx(slot), umbrella);
            }
        }

        public void ModifyVolleyRhythm(Projectile umbrella, ref KikasaVolleyRhythm rhythm) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.ModifyVolleyRhythm(Ctx(slot), umbrella, ref rhythm);
            }
        }

        public void OnVolley(Projectile umbrella, int volleyIndex, bool ghostVolley) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.OnVolley(Ctx(slot), umbrella, volleyIndex, ghostVolley);
            }
        }

        public void ModifyDropSpawn(ref KikasaDropSpawnContext drop) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.ModifyDropSpawn(Ctx(slot), ref drop);
            }
        }

        public void ModifyDropCurve(Projectile drop, ref KikasaDropCurve curve) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.ModifyDropCurve(Ctx(slot), drop, ref curve);
            }
        }

        public void OnDropKill(Projectile drop, bool onTile) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.OnDropKill(Ctx(slot), drop, onTile);
            }
        }

        public void ModifyRainHitNPC(Projectile source, KikasaRainSourceKind kind,
            NPC npc, ref NPC.HitModifiers modifiers) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)
                    ?.ModifyRainHitNPC(Ctx(slot), source, kind, npc, ref modifiers);
            }
        }

        public void OnRainHitNPC(Projectile source, KikasaRainSourceKind kind,
            NPC npc, in NPC.HitInfo hit, int damageDone) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)
                    ?.OnRainHitNPC(Ctx(slot), source, kind, npc, in hit, damageDone);
            }
        }

        public void ModifyPourSpawn(ref KikasaPourSpawnContext pour) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.ModifyPourSpawn(Ctx(slot), ref pour);
            }
        }

        public void OnPourStart(Projectile pour) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.OnPourStart(Ctx(slot), pour);
            }
        }

        public void OnPourEnd(Projectile pour) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.OnPourEnd(Ctx(slot), pour);
            }
        }

        public void ModifyGeyserVolley(Projectile pour, ref KikasaGeyserVolleyContext geysers) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.ModifyGeyserVolley(Ctx(slot), pour, ref geysers);
            }
        }

        public void OnPuddleUpdate(Projectile puddle) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.OnPuddleUpdate(Ctx(slot), puddle);
            }
        }

        public void OnPuddleContact(Projectile puddle, NPC npc) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.OnPuddleContact(Ctx(slot), puddle, npc);
            }
        }

        public void ModifyPuddleDraw(Projectile puddle, ref KikasaPuddleDrawParams draw) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.ModifyPuddleDraw(Ctx(slot), puddle, ref draw);
            }
        }

        public void UpdateWhileHeld() {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.UpdateWhileHeld(Ctx(slot));
            }
        }

        public void OnOwnerHurt(in Player.HurtInfo info) {
            if (store == null) {
                return;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(store, slot)?.OnOwnerHurt(Ctx(slot), in info);
            }
        }
    }

    /// <summary>
    /// 唤雨符挂钩入口：派发器解析 + 符标签位打包。
    /// 标签编码（滴 ai[2] 高位 / 泉 ai[1]）：bit3..9 标签（符网络 id+1，0=无），
    /// bit10..23 载荷；整数域 &lt; 2^24，float 精确表示，随生成包免费同步
    /// </summary>
    internal static class KikasaTalismanHooks
    {
        //====标签位段====
        private const int TagShift = 3;
        private const int TagMask = 0x7F;
        private const int PayloadShift = 10;
        private const int PayloadMask = 0x3FFF;

        /// <summary>标签+载荷打包为位段（不含 bit0..2，调用方自 OR 低位标志）</summary>
        public static int PackTag(int tagId, int payload)
            => ((tagId & TagMask) << TagShift) | ((payload & PayloadMask) << PayloadShift);

        /// <summary>自 ai 值读标签 id（0=无符）</summary>
        public static int ReadTagId(float aiValue) => ((int)aiValue >> TagShift) & TagMask;

        /// <summary>自 ai 值读标签载荷</summary>
        public static int ReadTagPayload(float aiValue) => ((int)aiValue >> PayloadShift) & PayloadMask;

        /// <summary>本符的标签 id（网络 id+1）；超出位段容量返回 0（不打标）</summary>
        public static int TagIdFor(KikasaTalismanDefinition definition) {
            if (definition == null
                || !KikasaTalismanRegistry.TryGetNetworkId(definition.Key, out ushort id)
                || id + 1 > TagMask) {
                return 0;
            }
            return id + 1;
        }

        /// <summary>标签 id 反查定义，0/未注册返回 false</summary>
        public static bool TryGetTagDefinition(int tagId, out KikasaTalismanDefinition definition) {
            definition = null;
            return tagId > 0
                && KikasaTalismanRegistry.TryGetByNetworkId((ushort)(tagId - 1), out definition);
        }

        //====派发器解析====

        /// <summary>
        /// 解析归属玩家的挂钩派发器（符位表在玩家身上，持伞时生效）；
        /// 未持伞/空绳返回空派发器。廉价判定（类型比对+三位判空），可逐帧调用
        /// </summary>
        public static KikasaTalismanHookRunner For(Player owner) {
            if (owner == null || owner.HeldItem?.type != ModContent.ItemType<KikasaItem>()
                || !owner.TryGetModPlayer(out KikasaTalismanPlayer session)) {
                return default;
            }
            KikasaTalismanStore store = session.Talismans;
            if (store.HungCount == 0) {
                return default;
            }
            return new KikasaTalismanHookRunner(owner, store, session);
        }

        /// <summary>按弹幕 owner 下标解析</summary>
        public static KikasaTalismanHookRunner ForOwner(int playerIndex)
            => playerIndex >= 0 && playerIndex < Main.maxPlayers ? For(Main.player[playerIndex]) : default;

        //====标签驱动的单符派发（不走三符位，未标签零开销）====

        private static KikasaTalismanRainContext TagCtx(int ownerIndex) {
            Player owner = ownerIndex >= 0 && ownerIndex < Main.maxPlayers ? Main.player[ownerIndex] : null;
            KikasaTalismanPlayer session = null;
            owner?.TryGetModPlayer(out session);
            return new KikasaTalismanRainContext(owner, session, -1);
        }

        /// <summary>滴绘制参数派发：只找标签符（读滴 ai[2]），绘制线程逐帧调用</summary>
        public static void ModifyDropDraw(Projectile drop, ref KikasaDropDrawParams draw) {
            if (!TryGetTagDefinition(ReadTagId(drop.ai[2]), out KikasaTalismanDefinition definition)) {
                return;
            }
            definition.ModifyDropDraw(TagCtx(drop.owner), drop, ref draw);
        }

        /// <summary>墨泉喷发事件派发：只找标签符（读泉 ai[1]），各端喷发帧各调一次</summary>
        public static void OnGeyserErupt(Projectile geyser) {
            if (!TryGetTagDefinition(ReadTagId(geyser.ai[1]), out KikasaTalismanDefinition definition)) {
                return;
            }
            definition.OnGeyserErupt(TagCtx(geyser.owner), geyser);
        }

        /// <summary>
        /// 血柱起柱事件派发：只找标签符（读柱 ai[1]），各端起柱帧各调一次。
        /// 与 <see cref="OnGeyserErupt"/> 分开：按"一次右键三泉"调校的符（霆雷冠等）若每颗珠子都触发会失衡
        /// </summary>
        public static void OnColumnErupt(Projectile column) {
            if (!TryGetTagDefinition(ReadTagId(column.ai[1]), out KikasaTalismanDefinition definition)) {
                return;
            }
            definition.OnColumnErupt(TagCtx(column.owner), column);
        }
    }
}
