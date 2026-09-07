using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 黑曜石套 · 曜岩鞭印（召唤·鞭）。单件沿用原版（头 +8% 召唤、衣 +1 栏、裤 +8% 召唤）。<br/>
    /// 原版旗标清点：+15% 召唤伤害 / 鞭范围 +30% / 鞭速 +15% → 原样补回；无删除项。
    /// 另加黑曜石骷髅头的 fireWalk 作材质注脚（踩狱石/陨石不受伤）。<br/>
    /// 签名：鞭子命中烙下曜岩印（3 秒），带印敌人每次被仆从命中额外受 4 点固定伤害并迸出黑曜石屑
    /// </summary>
    internal class GsObsidianArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.ObsidianHelm];
        public override int BodyID => ItemID.ObsidianShirt;
        public override int LegsID => ItemID.ObsidianPants;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "15% increased summon damage, 30% increased whip range and 15% increased whip speed; you can walk on hellstone and meteorite; whip hits brand enemies with an Obsidian Mark for 3 seconds, and minions deal 4 extra damage to marked enemies";

        /// <summary>曜岩印持续帧数</summary>
        private const int MarkFrames = 180;

        /// <summary>仆从命中带印敌人的额外固定伤害</summary>
        private const float MarkBonusDamage = 4f;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetDamage(DamageClass.Summon) += 0.15f;
            player.whipRangeMultiplier += 0.30f;
            player.GetAttackSpeed(DamageClass.SummonMeleeSpeed) += 0.15f;
            player.fireWalk = true;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (!hit.DamageType.CountsAsClass(DamageClass.SummonMeleeSpeed)) {
                return;
            }
            target.AddBuff(ModContent.BuffType<GsObsidianMarkBuff>(), MarkFrames);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Obsidian, 0f, -1f, 80, default, 1.1f);
                dust.noGravity = true;
            }
        }

        public override void ModifyEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            ref NPC.HitModifiers modifiers, Projectile sourceProj) {
            //只有仆从/哨兵类命中吃印，鞭子自身不吃（鞭是烙印者）
            if (sourceProj == null || !target.GetGlobalNPC<GsArmorMarkNPC>().ObsidianMarked
                || modifiers.DamageType.CountsAsClass(DamageClass.SummonMeleeSpeed)
                || !modifiers.DamageType.CountsAsClass(DamageClass.Summon)) {
                return;
            }
            modifiers.FlatBonusDamage += MarkBonusDamage;
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Obsidian,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2.5f, 0.5f), 60, default, 1.2f);
                dust.noGravity = false;
            }
        }
    }
}
