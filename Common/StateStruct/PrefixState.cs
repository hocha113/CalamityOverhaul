/// <summary>
/// 表示物品前缀附加属性的结构体
/// 该结构体用于存储与物品前缀相关的所有属性加成，包括伤害倍率、击退倍率、使用时间倍率、尺寸倍率、射速倍率、法力消耗倍率、
/// 暴击加成以及前缀的总体强度结构体还包括前缀ID和是否为模组前缀的标志
/// </summary>
public struct PrefixState
{
    /// <summary>
    /// 前缀的ID
    /// </summary>
    public int prefixID;
    /// <summary>
    /// 标志前缀是否来自模组
    /// 若为<see langword="true"/>，则表示该前缀来自模组；若为<see langword="false"/>，则表示该前缀为原版前缀
    /// </summary>
    public bool isModPreFix;
    /// <summary>
    /// 伤害倍率，默认为1f
    /// 表示该前缀对物品伤害的增减幅度
    /// </summary>
    public float damageMult;
    /// <summary>
    /// 击退倍率，默认为1f
    /// 表示该前缀对物品击退效果的增减幅度
    /// </summary>
    public float knockbackMult;
    /// <summary>
    /// 使用时间倍率，默认为1f
    /// 表示该前缀对物品使用时间的增减幅度，值越小，物品使用速度越快
    /// </summary>
    public float useTimeMult;
    /// <summary>
    /// 尺寸倍率，默认为1f
    /// 表示该前缀对物品尺寸的增减幅度
    /// </summary>
    public float scaleMult;
    /// <summary>
    /// 射速倍率，默认为1f
    /// 表示该前缀对物品射击速度的增减幅度
    /// </summary>
    public float shootSpeedMult;
    /// <summary>
    /// 法力消耗倍率，默认为1f
    /// 表示该前缀对物品法力消耗的增减幅度，值越小，消耗法力越少
    /// </summary>
    public float manaMult;
    /// <summary>
    /// 暴击加成，默认为0
    /// 表示该前缀为物品增加的暴击几率
    /// </summary>
    public int critBonus;
    /// <summary>
    /// 前缀的总体强度，默认值为0
    /// 该值由各种属性倍率和加成综合计算得出，用于衡量前缀对物品的总体影响
    /// </summary>
    public float strength;
    /// <summary>
    /// 默认构造函数
    /// 初始化时不设置任何字段，保留默认值
    /// </summary>
    public PrefixState() {
        strength = 0;
        damageMult = 1f;
        knockbackMult = 1f;
        useTimeMult = 1f;
        scaleMult = 1f;
        shootSpeedMult = 1f;
        manaMult = 1f;
        critBonus = 0;
    }
    public override string ToString() {
        string content = "PrefixAddition:";
        content += "\nprefixID:" + prefixID;
        content += "\nisModPreFix:" + isModPreFix;
        content += "\nstrength:" + strength;
        content += "\ndamageMult:" + damageMult;
        content += "\nknockbackMult:" + knockbackMult;
        content += "\nuseTimeMult:" + useTimeMult;
        content += "\nscaleMult:" + scaleMult;
        content += "\nshootSpeedMult:" + shootSpeedMult;
        content += "\nmanaMult:" + manaMult;
        content += "\ncritBonus:" + critBonus;
        return content;
    }
}