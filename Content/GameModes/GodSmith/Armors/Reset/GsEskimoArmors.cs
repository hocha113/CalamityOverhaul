using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 防雪套两色共用层 · 雪衣（通用）：单件沿用原版（无属性）。<br/>
    /// 原版旗标清点：免疫寒冷、冰冻 → 原样补回；无删除项。<br/>
    /// 签名：在雪原防御 +3；受击时抖落雪屑，使近身敌人寒冷 2 秒（冷却 3 秒）
    /// </summary>
    internal abstract class GsEskimoArmorScheme : GsResetArmorScheme
    {
        public sealed override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Immune to Chilled and Frozen; 3 more defense in the snow biome; taking damage shakes off snow that chills nearby enemies for 2 seconds, once every 3 seconds";

        private const float ShakeRange = 90f;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.buffImmune[BuffID.Frozen] = true;
            player.buffImmune[BuffID.Chilled] = true;
            if (player.ZoneSnow) {
                player.statDefense += 3;
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (player.whoAmI != Main.myPlayer || !state.TryUseCooldown(this, 180)) {
                return;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || npc.dontTakeDamage || npc.boss || npc.Center.Distance(player.Center) > ShakeRange) {
                    continue;
                }
                npc.AddBuff(BuffID.Chilled, 120);
            }
            SoundEngine.PlaySound(SoundID.Item48 with { Volume = 0.35f, Pitch = 0.5f }, player.Center);
            for (int i = 0; i < 16; i++) {
                Dust dust = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Snow,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 2f), 60, default, 1.2f);
                dust.noGravity = false;
            }
        }
    }

    /// <summary>防雪套</summary>
    internal class GsEskimoArmor : GsEskimoArmorScheme
    {
        public override int[] HeadIDs => [ItemID.EskimoHood];
        public override int BodyID => ItemID.EskimoCoat;
        public override int LegsID => ItemID.EskimoPants;
    }

    /// <summary>粉色防雪套</summary>
    internal class GsPinkEskimoArmor : GsEskimoArmorScheme
    {
        public override int[] HeadIDs => [ItemID.PinkEskimoHood];
        public override int BodyID => ItemID.PinkEskimoCoat;
        public override int LegsID => ItemID.PinkEskimoPants;
    }
}
