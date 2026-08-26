using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 唤雨符静态定义，运行期不可变；子类即注册（<see cref="KikasaTalismanRegistry"/> 反射扫描）。<br/>
    /// 三符位同质无分类，同 Key 不可重复上绳；效果经 <see cref="ModifyProfile"/>
    /// 汇入 <see cref="KikasaTalismanProfile"/>；Key 从此稳定，改名即断档。<br/>
    /// <b>状态纪律</b>：定义是全局单例，严禁在实例上放任何运行态——
    /// 每玩家会话计量走 <see cref="KikasaTalismanRainContext.StateFor"/>，
    /// 每 NPC 叠层走 <see cref="KikasaTalismanStackNPC"/>。<br/>
    /// <b>联机纪律</b>：伤害与生成投射物只在 ctx.IsOwnerClient 端做（自然同步），
    /// 纯表现各端本地跑并自行 !Main.dedServ
    /// </summary>
    public abstract class KikasaTalismanDefinition
    {
        /// <summary>稳定键，存档/网络据此挂接，默认类型名</summary>
        public virtual string Key => GetType().Name;

        /// <summary>符箧排序，越小越前</summary>
        public virtual int SortOrder => 0;

        /// <summary>符墨主色：字形/朱印/UI 读数的身份色，逐符独特</summary>
        public virtual Color InkAccent => new(120, 160, 196);

        //====本地化（由同 Key 符纸物品统一注册，本定义只保留只读视图）====
        /// <summary>符名</summary>
        public LocalizedText DisplayName { get; private set; }
        /// <summary>来历残句</summary>
        public LocalizedText Origin { get; private set; }
        /// <summary>赋效文案（真实机制说明）</summary>
        public LocalizedText Power { get; private set; }
        /// <summary>代价文案（真实负担说明）</summary>
        public LocalizedText Burden { get; private set; }
        /// <summary>悬浮短摘要（赋效;代价）</summary>
        public LocalizedText Summary { get; private set; }

        internal bool HasLocalization
            => DisplayName != null && Origin != null && Power != null && Burden != null && Summary != null;

        internal void BindLocalization(KikasaTalismanItem item) {
            DisplayName = item.DisplayName;
            Origin = item.Origin;
            Power = item.Power;
            Burden = item.Burden;
            Summary = item.Tooltip;
        }

        //====效果====
        /// <summary>
        /// 汇入三符位合成战斗档（<see cref="KikasaTalismanCombat.Resolve(Terraria.Player)"/> 逐位调用）。<br/>
        /// 倍率一律"叠乘/累加"，禁止直接赋值覆盖其他符位
        /// </summary>
        public virtual void ModifyProfile(ref KikasaTalismanProfile profile) { }

        //====字形====
        /// <summary>
        /// 构建本符字形笔画（归一 [-1,1] 空间，笔画 API 见
        /// <see cref="KikasaTalismanGlyph"/>.L/Dot/Arc/Canopy），注册期收进中央缓存；
        /// 返回 null 用伞形 fallback
        /// </summary>
        internal virtual KikasaGlyphStroke[] BuildGlyph() => null;

        //====行为挂钩（基建 A）====
        //派发次序=符位序（0→2）；除注明"仅所有者端"外均各端运行

        /// <summary>起伞第一帧（各端）。服务：霎（首拍三连预置+演出）、霁（蓄霁清零）</summary>
        internal virtual void OnRainStart(in KikasaTalismanRainContext ctx, Projectile umbrella) { }

        /// <summary>收伞瞬间（各端；持有条件破裂的谢幕不触发）。服务：霁（霁光结算）</summary>
        internal virtual void OnRecall(in KikasaTalismanRainContext ctx, Projectile umbrella) { }

        /// <summary>
        /// 节拍解算（各端逐帧、一帧可能多次调用，实现必须为纯函数式读取，禁副作用与随机）。
        /// 旁观端节拍仅表现，允许与 owner 端近似。
        /// 服务：霎（首拍三连/三连后+40%）、雹（自造齐掷拍）、霅（重霅后停一拍）、
        /// 澍（受击窗 x0.5）、雩（大雩窗 x0.5）
        /// </summary>
        internal virtual void ModifyVolleyRhythm(in KikasaTalismanRainContext ctx,
            Projectile umbrella, ref KikasaVolleyRhythm rhythm) { }

        /// <summary>出手拍事件（各端，每波一次）。服务：霅（节拍环推进/漏拍判定）、雹（齐掷重音演出）</summary>
        internal virtual void OnVolley(in KikasaTalismanRainContext ctx,
            Projectile umbrella, int volleyIndex, bool ghostVolley) { }

        /// <summary>
        /// 墨滴生成（仅所有者端）；标签/载荷随生成包同步，供落点/命中/绘制分支。
        /// 服务：霏（每第3滴雾化）、霰（大滴打标）、霄（伤+25%+打标）、霓（三色轮转）、
        /// 雹（齐掷滴巨雹化/普通拍-8%）、霎（三连滴 0.75x+直坠标）、雩（大雩洼解锁+虹墨标）
        /// </summary>
        internal virtual void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) { }

        /// <summary>墨滴弹道参数（各端首帧，须确定性）。服务：霄（高空直坠）、霎（三连直坠）</summary>
        internal virtual void ModifyDropCurve(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropCurve curve) { }

        /// <summary>墨滴绘制参数（端本地；仅派发给标签符，ctx.Slot=-1）。服务：霓、雹、霏、霞</summary>
        internal virtual void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) { }

        /// <summary>
        /// 墨滴谢幕（非服务器各端；落湖被收走不触发）。生成物仅所有者端做。
        /// 服务：霰（落地碎霰珠）、霏（雾滴滞留雾团）、霜（滴击碎霜镜）
        /// </summary>
        internal virtual void OnDropKill(in KikasaTalismanRainContext ctx,
            Projectile drop, bool onTile) { }

        /// <summary>
        /// 雨系命中修正（仅所有者端，滴/瀑/泉/洼四源统一入口）。
        /// 服务：渍（逐层易伤）、沆（夜+15%/昼-5%）、霞（晨昏+18%）、霸（月窗）、
        /// 霁（撑伞期-8%）、雩（大雩+15%/平时-10%）、霓（紫滴易伤）、霜（镜面免伤）
        /// </summary>
        internal virtual void ModifyRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, ref NPC.HitModifiers modifiers) { }

        /// <summary>
        /// 雨系命中事件（仅所有者端，四源统一）。
        /// 服务：洇（洇痕叠层/满层爆）、露（凝露珠）、霉（霉蚀挂层）、霹（窗内召雷）、
        /// 霓（三色触发）、霅（拍内命中记账）、雯（符星充能）、雩（蓄祭）、
        /// 澍（窗内回血）、霁（蓄霁）、雹（破甲）、霆（泉击链电）
        /// </summary>
        internal virtual void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) { }

        /// <summary>墨瀑生成（仅所有者端）；标签经 ai[1] 量化同步。服务：霸（满月月瀑打标）</summary>
        internal virtual void ModifyPourSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaPourSpawnContext pour) { }

        /// <summary>墨瀑首帧（各端）。服务：泷（冲刷推移伴生体）、霸（月瀑材质旋钮）</summary>
        internal virtual void OnPourStart(in KikasaTalismanRainContext ctx, Projectile pour) { }

        /// <summary>墨瀑谢幕（各端）。服务：虹（落点拱虹桥）、霹（开 10s 天雷窗）</summary>
        internal virtual void OnPourEnd(in KikasaTalismanRainContext ctx, Projectile pour) { }

        /// <summary>
        /// 墨泉齐发决策（仅所有者端，一瀑一次；基础条件不满足也会派发）。
        /// 服务：霆（雷泉打标/柱高 x1.5/非满蓄 25% 小雷泉）、雩（大雩泉档解锁）
        /// </summary>
        internal virtual void ModifyGeyserVolley(in KikasaTalismanRainContext ctx,
            Projectile pour, ref KikasaGeyserVolleyContext geysers) { }

        /// <summary>墨泉喷发拍（各端；仅派发给标签符，ctx.Slot=-1）。服务：霆（雷冠演出）</summary>
        internal virtual void OnGeyserErupt(in KikasaTalismanRainContext ctx, Projectile geyser) { }

        /// <summary>
        /// 墨洼逐帧（各端）。旋钮类（宽度波动/判定关断）在此逐帧写。
        /// 服务：汐（潮性涨落/涌潮位移）、沆（夜蒸瘴雾柱）、霜（霜镜材质）
        /// </summary>
        internal virtual void OnPuddleUpdate(in KikasaTalismanRainContext ctx, Projectile puddle) { }

        /// <summary>墨洼接触（仅所有者端，约 10 帧一轮节流扫描）。服务：渍（浸渍叠层）、霜（踏镜减速）</summary>
        internal virtual void OnPuddleContact(in KikasaTalismanRainContext ctx,
            Projectile puddle, NPC npc) { }

        /// <summary>墨洼配色（端本地逐帧）。服务：霜（霜银）、沆（瘴绿）、霞（霞纹）</summary>
        internal virtual void ModifyPuddleDraw(in KikasaTalismanRainContext ctx,
            Projectile puddle, ref KikasaPuddleDrawParams draw) { }

        /// <summary>
        /// 持伞逐帧（各端、死亡帧也在跑；ctx.Owner 为被派发的持伞人）。
        /// 服务：霅（节拍环 UI）、雯（符星轨道/满充自掷）、霸（月相跟踪）、
        /// 雩（大雩入场/终了三泉）、澍（冷却计时）、沆/霞（时段窗演出）、霁（计量 UI）
        /// </summary>
        internal virtual void UpdateWhileHeld(in KikasaTalismanRainContext ctx) { }

        /// <summary>持伞人受击（掉血前一刻）。服务：澍（开 3s 及时雨窗）</summary>
        internal virtual void OnOwnerHurt(in KikasaTalismanRainContext ctx, in Player.HurtInfo info) { }

        //====NPC 叠层挂钩（由 KikasaTalismanStackNPC 按叠层 Kind 派发，不走三符位）====

        /// <summary>本符叠层的持续伤害（lifeRegen 权威端生效；实现直接改 npc.lifeRegen）。服务：洇（微 DoT）、霉（霉蚀 DoT）</summary>
        internal virtual void ModifyStackLifeRegen(NPC npc, int stacks, ref int damage) { }

        /// <summary>带本符叠层的 NPC 死亡（服务端/单机）。服务：霉（孢子雾感染周围）</summary>
        internal virtual void OnStackNPCKill(NPC npc, int stacks) { }

        /// <summary>本符叠层的 NPC 表现层（客户端 PostDraw 批内，简单精灵绘制）。服务：洇、渍、霉</summary>
        internal virtual void DrawNPCStack(SpriteBatch spriteBatch, NPC npc,
            int stacks, int timerFrames, Vector2 screenPos, Color drawColor) { }
    }
}
