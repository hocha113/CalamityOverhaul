using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.PvP
{
    /// <summary>玩家效果结束的方式，HUD 按它区分退场演出（到期淡出 / 被拔碎裂）</summary>
    internal enum PlayerHackRemoveReason : byte
    {
        /// <summary>自然到期</summary>
        Expired,
        /// <summary>被强制卸载协议拔除</summary>
        Uninstalled,
        /// <summary>防守方掉线/死亡/换世界，服务端清账</summary>
        DefenderLost,
        /// <summary>服务端看门狗强制移除（对账失联）</summary>
        Watchdog,
    }

    /// <summary>
    /// 玩家目标（PvP）协议的中间基类。<b>写第二波协议先读完这段。</b><br/><br/>
    ///
    /// <b>权威模型（为什么这些钩子长这样）</b>：玩家目标的效果管线与 NPC 协议是倒过来的。
    /// NPC 协议的 <c>OnApply/OnTick/OnRemove</c> 跑在权威端（服务端/单人），因为 NPC 归服务端所有；
    /// 玩家的生命/buff/背包/ModPlayer 全部只有防守方自己的客户端能写（原版信任模型），
    /// 所以玩家协议的结算跑在<b>防守方客户端</b>：<br/>
    /// 攻击方请求 → 服务端校验与记账（<see cref="PlayerHackAuthority"/> 授予账）→
    /// 转发 <c>DefenderApply</c> 给防守方 → 防守方本机施加并回执 → 全员广播表现。<br/>
    /// 这条管线由 <see cref="PlayerHackNet"/> 驱动，协议作者不碰包，只填下面的钩子。<br/><br/>
    ///
    /// <b>三条通道，别写错端</b>：<br/>
    /// · <b>防守方通道</b>（<see cref="OnDefenderApply"/> / <see cref="OnDefenderTick"/> /
    ///   <see cref="OnDefenderRemove"/>）——只在防守方本机跑，落点必须是防守方自己的资源：
    ///   ModPlayer 字段、属性乘区、本机 <c>AddBuff</c>（自己给自己上 buff，msg 50 自动扩散图标）、
    ///   本机 <c>Hurt(pvp:true)</c>。<br/>
    /// · <b>权威通道</b>（<see cref="OnAuthorityGranted"/> / <see cref="OnAuthorityRevoked"/>）
    ///   ——在服务端（单人=本机）跑，落点必须是服务端拥有的资源：RAM、授予账载荷（额度/计数）。
    ///   例：内存烧蚀在这里烧 RAM，战术榨取在这里记回流额度。<br/>
    /// · <b>表现通道</b>（<see cref="OnSpectatorTick"/> / <see cref="DrawDefenderOverlay"/>）
    ///   ——前者在每个客户端跑（数据源是 PlayerEffectState 广播镜像，含攻击方与旁观者），
    ///   后者只在防守方本机 HUD 层跑。表现不读防守方本机数值（观众读不到，也不该读）。<br/><br/>
    ///
    /// <b>per-effect 状态放哪</b>：写进 <see cref="PlayerHackEffect.ProtocolState"/>
    /// （防守方侧）或 <see cref="PlayerHackGrant.AuthorityState"/>（服务端侧）——
    /// 帐本条目是实例化的，生命周期随效果自清，<b>不要开协议侧静态字典</b>
    /// （这是与 NPC 协议不同的一点：NPC 侧追踪器条目不可挂载荷才被迫外挂静态账）。<br/><br/>
    ///
    /// <b>载荷</b>：施加参数走 <see cref="WriteApplyPayload"/> → <see cref="ReadApplyPayload"/>
    /// （服务端写、防守方读，前置 1 字节长度由管线负责）；防守方要回传真值
    /// （如增益抽取的 buff 型号与真实剩余时长）走 <see cref="WriteReceiptPayload"/> →
    /// <see cref="HandleReceiptPayload"/>（防守方写、服务端读并转交攻击方结算）。
    /// 读侧不用自己守流对齐——管线按长度前缀切好子流才调你。<br/><br/>
    ///
    /// <b>红线（框架层强制）</b>：完全失控 0 帧——基类不提供任何输入劫持入口；
    /// 减速/迟滞/烧蚀/生命伤害一律经 <see cref="HackPvPRules"/> 的 Clamp* 落地，
    /// 数值超限会被静默压回上限，别试。每个可感知效果必须有 HUD 条目——
    /// 这由框架保证（帐本条目即 HUD 条目），协议作者无需也无法自行开关。<br/><br/>
    ///
    /// <b>SetDefaults 写法</b>（与 NPC 协议一致，但目标位被基类钉死为 Player）：
    /// <code>
    /// public override void SetDefaults() {
    ///     UploadTime = 90;             //上传帧数
    ///     RamCost = 3;                 //RAM 基础价（HackCostEvaluator 另乘目标倍率）
    ///     Category = QuickHackCategory.Covert;
    ///     UnlockedByDefault = false;   //芯片档协议设 false + 一行芯片子类
    /// }
    /// public override int GetDuration() => 480;  //持续帧数，0 = 即时
    /// </code>
    /// </summary>
    internal abstract class PlayerHackDef : QuickHackDef
    {
        #region 权威通道密封（玩家目标没有"权威端施加"这一步）

        /// <summary>
        /// 密封为空转。玩家效果不经 <c>HackEffectTracker</c> 的权威施加通道
        /// （防守方客户端才是合法写入者，见类注释的权威模型）。
        /// 上传完成后的分发走 <see cref="PlayerHackNet"/> 的 DefenderApply 管线
        /// </summary>
        public sealed override bool OnApply(IHackTarget target, Player caster) => false;

        /// <summary>密封为终止。玩家效果不进追踪器，不存在权威 Tick</summary>
        public sealed override bool OnTick(IHackTarget target, int elapsed) => false;

        /// <summary>密封为空。玩家效果的移除走 <see cref="OnDefenderRemove"/></summary>
        public sealed override void OnRemove(IHackTarget target) { }

        /// <summary>密封为空。表现广播走 <see cref="OnSpectatorTick"/>（镜像驱动）</summary>
        public sealed override void OnReplicatedApply(IHackTarget target, int elapsed) { }

        /// <summary>密封为空，同上</summary>
        public sealed override void OnReplicatedTick(IHackTarget target, int elapsed) { }

        /// <summary>密封为空，同上</summary>
        public sealed override void OnReplicatedRemove(IHackTarget target) { }

        /// <summary>
        /// 目标位在类型层钉死为 Player：先跑子类 <see cref="QuickHackDef.SetDefaults"/>，
        /// 再强制覆写 <c>SupportedTargets</c>——权威通道与防守通道在类型层分开，
        /// 双目标协议在结构上不可表达（用户裁决：玩家是另一个系统，不做 NPC 协议适配）
        /// </summary>
        public sealed override void VaultSetup() {
            base.VaultSetup();
            SupportedTargets = HackTargetKind.Player;
        }

        #endregion

        #region 防守方通道（防守方本机跑，落点=防守方自己的资源）

        /// <summary>
        /// 防守方本机施加。返回 false = 本机终审拒绝（回执 Rejected，服务端撤销授予并退攻击方 RAM）。<br/>
        /// per-effect 状态在这里 new 出来挂到 <paramref name="effect"/>.ProtocolState
        /// </summary>
        public virtual bool OnDefenderApply(Player defender, PlayerHackEffect effect) => true;

        /// <summary>
        /// 防守方本机逐帧。返回 false = 提前结束（走 <see cref="OnDefenderRemove"/>，
        /// reason=Expired，并随对账上报服务端）。elapsed 从帐本条目读，不用自己数
        /// </summary>
        public virtual bool OnDefenderTick(Player defender, PlayerHackEffect effect) => true;

        /// <summary>防守方本机移除/到期清理。改了属性的协议在这里还回去</summary>
        public virtual void OnDefenderRemove(Player defender, PlayerHackEffect effect,
            PlayerHackRemoveReason reason) { }

        #endregion

        #region 权威通道（服务端/单人跑，落点=服务端拥有的资源）

        /// <summary>
        /// 服务端授予时（发出 DefenderApply 的同一帧）。落点只许是服务端拥有的资源：
        /// RAM 直写、<paramref name="grant"/>.AuthorityState 记额度。
        /// 不要在这里碰防守方的生命/buff/背包——服务端写不进（tml-netcode-pitfalls §6.2）
        /// </summary>
        public virtual void OnAuthorityGranted(Player caster, Player defender,
            PlayerHackGrant grant) { }

        /// <summary>服务端撤销/到期/看门狗清账时。对称清理 AuthorityState 记的账</summary>
        public virtual void OnAuthorityRevoked(PlayerHackGrant grant,
            PlayerHackRemoveReason reason) { }

        #endregion

        #region 载荷（变长小载荷，管线负责长度前缀与子流切割）

        /// <summary>服务端 → 防守方的施加参数（如熔断标记的引信帧数掷骰）。默认空载荷</summary>
        public virtual void WriteApplyPayload(BinaryWriter writer, Player caster,
            Player defender) { }

        /// <summary>
        /// 防守方读施加参数，在 <see cref="OnDefenderApply"/> 之前调用。
        /// reader 是按长度前缀切好的子流，读不完/读超了都不会污染共用流，
        /// 但仍应与写侧字段一一对应
        /// </summary>
        public virtual void ReadApplyPayload(BinaryReader reader, PlayerHackEffect effect) { }

        /// <summary>
        /// 防守方 → 服务端的回执真值（如增益抽取：被抽 buff 型号与真实剩余时长——
        /// msg 50 给远端的 buffTime 全是 60 占位，全世界只有防守方知道真值）。默认空载荷
        /// </summary>
        public virtual void WriteReceiptPayload(BinaryWriter writer,
            PlayerHackEffect effect) { }

        /// <summary>
        /// 服务端读回执载荷（Applied 回执随附）。要转授攻击方资源的协议在这里
        /// 把真值转发给攻击方本机结算（攻击方资源归攻击方客户端，服务端只转发）
        /// </summary>
        public virtual void HandleReceiptPayload(BinaryReader reader, Player caster,
            Player defender, PlayerHackGrant grant) { }

        #endregion

        #region 表现通道（攻击方/旁观者读广播镜像，防守方读本机帐本）

        /// <summary>
        /// 每客户端逐帧，数据源是 PlayerEffectState 广播镜像（elapsed 为服务端影子时钟，
        /// 60f 刷新粒度 + 本机补间）。攻击方与旁观者的世界表现（描边光、粒子）写这里。
        /// 防守方本机也会收到自己的镜像——想只对旁观者生效就查
        /// <c>defender.whoAmI != Main.myPlayer</c>
        /// </summary>
        public virtual void OnSpectatorTick(Player defender, int casterIndex,
            int elapsed, int duration) { }

        /// <summary>
        /// 防守方本机 HUD 覆盖层（UI 空间，UIScale 批已就位）。
        /// 读数污染的仪表加扰、地图熄灭的雪花层这类"污染防守方屏幕"的表现写这里；
        /// 帐本条目卡片本身由框架画，协议只画自己的附加层
        /// </summary>
        public virtual void DrawDefenderOverlay(SpriteBatch spriteBatch, Player defender,
            PlayerHackEffect effect) { }

        /// <summary>
        /// HUD 条目图标的晶粒纹（SvgPathPen 路径，M/L/Q/C 指令，归一 [-1,1] 空间）。
        /// 返回 null 走 <c>HackChipGlyph.FallbackDie</c> 通用电路纹——不配纹样也能上线
        /// </summary>
        public virtual string GlyphDiePath => null;

        #endregion
    }
}
