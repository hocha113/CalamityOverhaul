namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel
{
    /// <summary>
    /// Shepel 线 ADVSave 模块
    /// </summary>
    internal class ShepelADVData : ADVDataModule
    {
        //空闲对话变体的轮换种子，每次触发后递增
        public int IdleVariantSeed;
        //故事阶段，框架预留，当前不主动推进（0=初遇前 1=初遇后 以此类推）
        public int StoryPhase;
        //待播响应式事件的bit位集合，各位含义见ShepelReactiveEvent枚举
        public int ReactiveEventFlags;
        //上一次触发BossDefeated事件的NPC类型ID，-1表示尚未记录
        public int LastDefeatedBossNpcType = -1;
        //是否已触发首次获得SHPC场景
        public bool FirstSHPCObtained;
        //各地区情境对话的变体轮换种子，满足特殊条件时池子扩大一格
        public int UnderworldVariantSeed;
        public int DungeonVariantSeed;
        public int OceanVariantSeed;
        public int SnowVariantSeed;
        public int JungleVariantSeed;
        public int NightVariantSeed;
        //首次获得SHPC的完整初遇链是否已经收尾
        public bool FirstSHPCIntroCompleted;
    }
}
