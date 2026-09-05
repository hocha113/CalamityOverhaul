using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 甲虫鳞甲（进攻）：单件沿用原版；套装奖励为连续命中积攒甲虫之力，每次命中 +1 层（最多 20 层），
    /// 每层伤害 +1%、所有武器攻速 +0.5%，2 秒未命中清零；满层时命中有 20% 概率引发武器面板 1.5 倍的甲壳冲击
    /// </summary>
    internal class GsBeetleScaleArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.BeetleHelmet];
        public override int BodyID => ItemID.BeetleScaleMail;
        public override int LegsID => ItemID.BeetleLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Consecutive hits build Beetle Might, +1 stack per hit up to 20; each stack grants 1% damage and 0.5% attack speed, and 2 seconds without hitting resets it; at full stacks hits have a 20% chance to unleash a shell shock for 1.5x your weapon's damage";

        private const int MaxStacks = 20;

        /// <summary>断层判定：多少帧没命中就清零</summary>
        private const int DecayFrames = 120;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            int stacks = Math.Clamp(state.EndowCharge, 0, MaxStacks);
            if (stacks <= 0) {
                return;
            }
            player.GetDamage(DamageClass.Generic) += 0.01f * stacks;
            player.GetAttackSpeed(DamageClass.Generic) += 0.005f * stacks;
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (state.EndowCharge > 0 && Main.GameUpdateCount - state.EndowTimer > DecayFrames) {
                state.EndowCharge = 0;
            }
        }

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsArmorBlastProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            state.EndowTimer = Main.GameUpdateCount;
            if (state.EndowCharge < MaxStacks) {
                state.EndowCharge++;
                if (state.EndowCharge == MaxStacks && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.2f }, player.Center);
                }
                return;
            }
            if (Main.rand.Next(100) < 20) {
                SpawnBlast(player, target.Center, (int)(WeaponPanelDamage(player, hit) * 1.5f), 130f,
                    "GodSmithBeetleScaleEndow", GsArmorBlastProj.Style.Shell);
            }
        }
    }

    /// <summary>
    /// 甲虫外壳（防御）：单件沿用原版；套装奖励为防御 +10%，受到伤害时获得一层甲壳（最多 3 层），
    /// 每层防御 +6、受到的伤害降低 6%，10 秒未受伤全部消散；满 3 层时再受伤会震碎甲壳，
    /// 向周围释放防御力 4 倍伤害的冲击波
    /// </summary>
    internal class GsBeetleShellArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.BeetleHelmet];
        public override int BodyID => ItemID.BeetleShell;
        public override int LegsID => ItemID.BeetleLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "10% more defense; taking damage grants a shell layer (up to 3), each layer giving 6 defense and 6% damage reduction, all fading after 10 seconds without being hit; being hit at 3 layers shatters them into a shockwave dealing 4x your defense";

        private const int MaxLayers = 3;

        /// <summary>甲壳保持帧数</summary>
        private const int LayerFrames = 600;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.statDefense *= 1.10f;
            int layers = Math.Clamp(state.EndowCharge, 0, MaxLayers);
            if (layers <= 0) {
                return;
            }
            player.statDefense += 6 * layers;
            player.endurance += 0.06f * layers;
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (state.EndowCharge > 0 && Main.GameUpdateCount - state.EndowTimer > LayerFrames) {
                state.EndowCharge = 0;
            }
        }

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsArmorBlastProj>();

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            state.EndowTimer = Main.GameUpdateCount;
            if (state.EndowCharge < MaxLayers) {
                state.EndowCharge++;
                return;
            }
            //满层再受伤：震碎甲壳，冲击波伤害按防御力折算
            state.EndowCharge = 0;
            int defense = player.statDefense;
            SpawnBlast(player, player.Center, Math.Max(10, defense * 4), 200f, "GodSmithBeetleShellEndow", GsArmorBlastProj.Style.Shell);
        }
    }
}
