using Terraria.ModLoader;

namespace CalamityOverhaul.Content.DamageModify
{
    /// <summary>
    /// 远程与魔法双系伤害，两侧的增伤词条、暴击与触发效果都能吃满
    /// </summary>
    internal class RangedMagicDamageClass : DamageClass
    {
        internal static RangedMagicDamageClass Instance;

        public override void Load() => Instance = this;
        public override void Unload() => Instance = null;

        public override string LocalizationCategory => "RangedMagicDamageClassTextContent";

        public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) {
            if (damageClass == Generic || damageClass == Ranged || damageClass == Magic) {
                return StatInheritanceData.Full;
            }
            return StatInheritanceData.None;
        }

        public override bool GetEffectInheritance(DamageClass damageClass)
            => damageClass == Ranged || damageClass == Magic;
    }
}
