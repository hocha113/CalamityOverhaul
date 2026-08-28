using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Accessory
{
    /// <summary>
    /// 【饰品·魔力】奥术护持：覆盖饰品魔力词缀（奥秘），
    /// 受击瞬间以魔力织出符文屏障，每 1 点魔力抵 2 点伤害，至多抵掉该击 15%。
    /// 受击结算在受击方本地端权威，魔力消耗天然只动自己
    /// </summary>
    internal class GodSmithArcaneWardEndow : GodSmithEndow
    {
        /// <summary>单次至多抵掉的伤害比例（顶级档）</summary>
        internal const float BaseAbsorbRatio = 0.15f;

        /// <summary>每点魔力抵挡的伤害</summary>
        internal const int DamagePerMana = 2;

        /// <summary>护持起动所需最低魔力</summary>
        internal const int ManaFloor = 10;

        public override int[] CoveredPrefixes => [PrefixID.Arcane];

        protected override string EndowNameFallback => "Arcane Aegis";

        protected override string EndowDescFallback =>
            "When struck, runes drink your mana: each point of mana blocks {0} damage, up to {1}% of the hit";

        public override object[] DescFormatArgs(Item item)
            => [DamagePerMana, (BaseAbsorbRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void ModifyHurt(Item accessory, Player player, ref Player.HurtModifiers modifiers, float tierScale) {
            if (player.statMana < ManaFloor) {
                return;
            }
            //挂终值回调拿到真实结算伤害，再按比例与魔力储量双重封顶折抵
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) => {
                int cap = (int)(info.Damage * BaseAbsorbRatio * tierScale);
                int absorb = Math.Min(Math.Min(cap, player.statMana * DamagePerMana), info.Damage - 1);
                if (absorb <= 0) {
                    return;
                }
                int manaCost = (absorb + DamagePerMana - 1) / DamagePerMana;
                player.statMana = Math.Max(0, player.statMana - manaCost);
                player.manaRegenDelay = Math.Max(player.manaRegenDelay, 30);
                info.Damage -= absorb;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = -0.2f }, player.Center);
                    for (int i = 0; i < 12; i++) {
                        float ang = MathHelper.TwoPi * i / 12f;
                        Dust dust = Dust.NewDustPerfect(player.Center + ang.ToRotationVector2() * 26f,
                            DustID.RuneWizard, ang.ToRotationVector2() * 2f, 100, default, 1.1f);
                        dust.noGravity = true;
                    }
                }
            };
        }
    }
}
