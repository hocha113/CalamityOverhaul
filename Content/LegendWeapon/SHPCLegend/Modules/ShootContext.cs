namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules
{
    /// <summary>
    /// SHPC 射击行为聚合上下文，所有改件通过 <see cref="SHPCModuleItem.Apply"/> 修改这些字段
    /// 浮点字段默认 1f（中性倍率），整型/布尔默认 0/false（中性加成）
    /// 对浮点倍率字段使用加算叠加：每个改件贡献增量（+0.5 = +50%），最终值 = 1 + 各改件增量之和
    /// 行为标志（Beam*/Orb* 部分）由弹幕在生成、命中、消亡等钩子处直接消费
    /// </summary>
    internal struct ShootContext
    {
        //═════ 基础数值（所有改件均可调） ═════

        /// <summary>
        /// 左键攻速倍率，作用于 useTime/useAnimation（>1 更快，<1 更慢）
        /// </summary>
        public float AttackSpeedMul;
        /// <summary>
        /// 通用伤害倍率
        /// </summary>
        public float DamageMul;
        /// <summary>
        /// 左键散布角度倍率，0 表示散布归零
        /// </summary>
        public float SpreadMul;
        /// <summary>
        /// 左键基础发射数量加成（最终发数 = max(1, BeamCount + BeamCountAdd)，MergeBeams 启用则强制为 1）
        /// </summary>
        public int BeamCountAdd;
        /// <summary>
        /// 左键弹丸初速度倍率
        /// </summary>
        public float BeamSpeedMul;
        /// <summary>
        /// 左键弹丸追踪强度倍率（>1 更强）
        /// </summary>
        public float HomingMul;
        /// <summary>
        /// 是否将多发弹幕合并为一发（聚束枪管）
        /// </summary>
        public bool MergeBeams;
        /// <summary>
        /// 合并模式下的伤害加成倍率（额外）
        /// </summary>
        public float MergedDamageBonus;
        /// <summary>
        /// 法力消耗倍率
        /// </summary>
        public float ManaCostMul;
        /// <summary>
        /// 右键蓄力时间倍率（<1 更快蓄满）
        /// </summary>
        public float ChargeTimeMul;
        /// <summary>
        /// 右键能量球飞行速度倍率
        /// </summary>
        public float OrbSpeedMul;
        /// <summary>
        /// 暴击率加成（百分点直接相加）
        /// </summary>
        public int CritAdd;

        //═════ 光束行为钩子（CyberTraceBeamProj 消费） ═════

        /// <summary>额外穿透次数，叠加在弹幕基础穿透之上</summary>
        public int BeamExtraPierce;
        /// <summary>光束生命周期倍率，影响最大飞行距离</summary>
        public float BeamLifeMul;
        /// <summary>命中时是否在该处引爆一次微型脉冲爆炸</summary>
        public bool BeamExplodeOnHit;
        /// <summary>微型爆炸半径（像素）</summary>
        public float BeamExplodeRadius;
        /// <summary>命中时链式跳跃次数，每次跳跃消耗 1</summary>
        public int BeamChainCount;
        /// <summary>链式跳跃搜索半径（像素）</summary>
        public float BeamChainRange;
        /// <summary>消亡时分裂出的副光束数量，向四周散射</summary>
        public int BeamSplitOnDeath;
        /// <summary>新星枪管特判：每多一发弹幕，后续爆炸伤害的衰减乘数；0表示无衰减</summary>
        public float BeamExplodeDecayPerBeam;

        //═════ 能量球行为钩子（CyberChargeOrbProj 消费） ═════

        /// <summary>蓄力时是否在球周持续吸引附近敌人</summary>
        public bool OrbDrainAura;
        /// <summary>爆炸半径倍率，最终半径 = 基础 × 此值</summary>
        public float OrbExplosionRadiusMul;
        /// <summary>爆炸时生成的自动追踪迷你光球数量</summary>
        public int OrbDetonationMinions;
        /// <summary>爆炸时是否将玩家反推弹射</summary>
        public bool OrbExplosionPropels;

        //═════ 攻击模式覆写 ═════

        /// <summary>左键变为发射持续跟随光标的棱镜光柱</summary>
        public bool LaserMode;

        //═════ 激光行为钩子（CyberPrismLaserProj 消费） ═════

        /// <summary>激光脉冲爆炸帧间隔，0=关闭；每隔此帧数在光束终点引爆一次脉冲</summary>
        public int LaserPulseInterval;
        /// <summary>激光脉冲爆炸半径（像素）</summary>
        public float LaserPulseRadius;
        /// <summary>激光命中NPC时是否施加灼烧类debuff</summary>
        public bool LaserScorchOnHit;
        /// <summary>灼烧持续帧数</summary>
        public int LaserScorchDuration;

        //═════ 能量球飞行行为（CyberChargeOrbProj 消费） ═════

        /// <summary>能量球飞行阶段是否持续向最近敌人偏转追踪</summary>
        public bool OrbFlyingAttract;

        /// <summary>
        /// 中性默认值
        /// </summary>
        public static ShootContext Default => new() {
            AttackSpeedMul = 1f,
            DamageMul = 1f,
            SpreadMul = 1f,
            BeamCountAdd = 0,
            BeamSpeedMul = 1f,
            HomingMul = 1f,
            MergeBeams = false,
            MergedDamageBonus = 1f,
            ManaCostMul = 1f,
            ChargeTimeMul = 1f,
            OrbSpeedMul = 1f,
            CritAdd = 0,

            BeamExtraPierce = 0,
            BeamLifeMul = 1f,
            BeamExplodeOnHit = false,
            BeamExplodeRadius = 0f,
            BeamChainCount = 0,
            BeamChainRange = 240f,
            BeamSplitOnDeath = 0,
            BeamExplodeDecayPerBeam = 0f,

            OrbDrainAura = false,
            OrbExplosionRadiusMul = 1f,
            OrbDetonationMinions = 0,
            OrbExplosionPropels = false,

            LaserMode = false,
            LaserPulseInterval = 0,
            LaserPulseRadius = 80f,
            LaserScorchOnHit = false,
            LaserScorchDuration = 0,

            OrbFlyingAttract = false,
        };
    }
}
