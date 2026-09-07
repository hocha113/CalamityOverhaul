using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 暗影套 · 暗影焰（通用，含远古暗影三件可混搭）。单件沿用原版（三件各 +5% 暴击）。<br/>
    /// 原版旗标清点：暗影疾行（shadowArmor：跑速 ×1.15、加速 ×1.75）→ 原样补回；无删除项。<br/>
    /// 签名：暴击使敌人附带暗影焰 2 秒（魔矿与腐化同源的紫焰）
    /// </summary>
    internal class GsShadowArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.ShadowHelmet, ItemID.AncientShadowHelmet];
        public override int BodyID => ItemID.ShadowScalemail;
        public override int LegsID => ItemID.ShadowGreaves;
        public override int[] BodyIDs => [ItemID.ShadowScalemail, ItemID.AncientShadowScalemail];
        public override int[] LegsIDs => [ItemID.ShadowGreaves, ItemID.AncientShadowGreaves];
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Greatly increased running speed and acceleration; critical strikes inflict Shadowflame for 2 seconds";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.shadowArmor = true;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (!hit.Crit) {
                return;
            }
            target.AddBuff(BuffID.ShadowFlame, 120);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Shadowflame,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2f, 0f), 100, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }
}
