using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 碧水权杖重铸：潮汐节拍（持续流变体）。连续喷洒 45 帧蓄潮，随后开 20 帧
    /// 「涨潮窗」：窗内水压增幅（伤害 1.3 倍、击退 1.5 倍、蓝耗 0.8 倍），
    /// 三息一涌。完整渡过一个涨潮窗积 1 层（上限 3），满层后下一次施法拍出「浪破」：
    /// 扇形五道重浪（合计约 2.5 倍）并清层。材质身份：流体（水压）。<br/>
    /// 节拍窗语义由喷洒时长驱动，不走标准就绪窗（UsesStandardBeat = false）
    /// </summary>
    internal class GsAquaScepter : GsChantScheme
    {
        public override int TargetItemID => ItemID.AquaScepter;

        protected override string GsDescFallback =>
            "Reforged: sustained spraying swells into a surge window of crushing water pressure;\nride three full surges and the next cast breaks into a fan of tidal slams";
        protected override float BaseDamageMult => 1.08f;

        protected override int MaxResonance => 3;

        protected override bool UsesStandardBeat => false;

        /// <summary>形态：浪破重浪</summary>
        private const float FormWaveBreak = 10f;

        /// <summary>蓄潮所需连续喷洒帧数</summary>
        private const int ChargeTicks = 45;
        /// <summary>涨潮窗时长</summary>
        private const int SurgeTicks = 20;

        /// <summary>涨潮窗是否在期（CounterA = 喷洒帧计数，TimerA = 窗关闭时刻）</summary>
        private static bool InSurge(GsChantPlayer chant) => Main.GameUpdateCount < chant.TimerA;

        protected override void ChantHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GsChantPlayer chant = Chant(player);
            uint now = Main.GameUpdateCount;
            bool spraying = player.itemAnimation > 0;

            if (spraying) {
                chant.TimerB = now;
                if (!InSurge(chant)) {
                    chant.CounterA++;
                    if (chant.CounterA >= ChargeTicks) {
                        //涨潮：开窗并压一声潮涌
                        chant.CounterA = 0;
                        chant.TimerA = now + SurgeTicks;
                        SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.7f, Pitch = -0.2f }, player.Center);
                    }
                }
            }
            else if (now - chant.TimerB > 8) {
                //断喷：蓄潮清零，层数保留
                chant.CounterA = 0;
            }

            //窗口自然走完且仍在喷：完整涌拍积 1 层
            if (chant.TimerA > 0 && now == chant.TimerA) {
                if (spraying) {
                    if (chant.Resonance < MaxResonance) {
                        chant.Resonance++;
                    }
                    if (chant.Resonance >= MaxResonance && !chant.EmpowerArmed) {
                        chant.EmpowerArmed = true;
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.85f, Pitch = 0.1f }, player.Center);
                    }
                    SoundEngine.PlaySound(SoundID.Item4 with {
                        Volume = 0.35f,
                        Pitch = 0.1f + 0.1f * chant.Resonance
                    }, player.Center);
                }
                chant.TimerA = 0;
            }
        }

        protected override void ChantModifyShootStats(Item item, Player player, GsChantPlayer chant,
            ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //把窗态写进拍型：窗内弹带正拍标（各端按标加密水花），层数快照供打标
            chant.CurrentBeat = InSurge(chant) ? ChantBeat.OnBeat : ChantBeat.Straight;
            chant.ResonanceAtCast = chant.Resonance;
        }

        protected override bool? ChantShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            if (!chant.EmpowerArmed) {
                return null;
            }
            //浪破：满层后的这一发拍出扇形五道重浪并清层
            chant.EmpowerArmed = false;
            chant.Resonance = 0;
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.75f, Pitch = -0.3f }, position);
            int slamDamage = Math.Max(1, (int)(damage * 0.5f));
            for (int i = 0; i < 5; i++) {
                float off = MathHelper.ToRadians(-22f + 44f * i / 4f);
                Vector2 vel = velocity.SafeNormalize(Vector2.UnitX).RotatedBy(off) * 10f;
                QueueForm(player, FormWaveBreak);
                int idx = Projectile.NewProjectile(source, position, vel, type,
                    slamDamage, 8f, player.whoAmI);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].scale *= 1.6f;
                    Main.projectile[idx].timeLeft = 18;
                    Main.projectile[idx].netUpdate = true;
                }
            }
            return null;
        }

        protected override void ChantModifyWeaponDamage(Item item, Player player, GsChantPlayer chant,
            ref StatModifier damage) {
            if (chant.BoundItemType == item.type && InSurge(chant)) {
                damage *= 1.3f;
            }
        }

        public override void GsModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback) {
            if (player.whoAmI == Main.myPlayer) {
                GsChantPlayer chant = Chant(player);
                if (chant.BoundItemType == item.type && InSurge(chant)) {
                    knockback *= 1.5f;
                }
            }
        }

        protected override void ChantModifyManaCost(Item item, Player player, GsChantPlayer chant,
            ref float reduce, ref float mult) {
            if (chant.BoundItemType == item.type && InSurge(chant)) {
                mult *= 0.8f;
            }
        }
    }
}
