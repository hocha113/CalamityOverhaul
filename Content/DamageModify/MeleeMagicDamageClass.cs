using Terraria.ModLoader;

namespace CalamityOverhaul.Content.DamageModify
{
    /// <summary>
    /// 近战与魔法双系伤害，两侧的增伤词条、暴击与触发效果都能吃满
    /// </summary>
    internal class MeleeMagicDamageClass : DamageClass
    {
        internal static MeleeMagicDamageClass Instance;

        public override void Load() => Instance = this;
        public override void Unload() => Instance = null;

        public override string LocalizationCategory => "MeleeMagicDamageClassTextContent";

        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) {
            if (damageClass == Generic || damageClass == Melee || damageClass == Magic) {
                return StatInheritanceData.Full;
            }
            return StatInheritanceData.None;
        }

        public override bool GetEffectInheritance(DamageClass damageClass)
            => damageClass == Melee || damageClass == Magic;
    }
}
