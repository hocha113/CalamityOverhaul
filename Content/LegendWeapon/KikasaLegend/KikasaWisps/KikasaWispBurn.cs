using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaWisps
{
    /// <summary>
    /// 鬼火灼烧 debuff：owner 端湖带扫描施加（AddBuff 骑原版 buff 同步）。
    /// DoT 走 <see cref="CWRNpc.KikasaWispFire"/> 标志 → UpdateLifeRegen；
    /// 灼身重绘在 <see cref="KikasaWispBurnNPC"/>，这里只置标志与限频洒火粒
    /// </summary>
    internal class KikasaWispBurn : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "KikasaWispBurn";
        private int time;

        public override void SetStaticDefaults() => Main.debuff[Type] = true;

        public override void Update(NPC npc, ref int buffIndex) {
            npc.CWR().KikasaWispFire = true;
            if (++time % 5 == 0) {
                Vector2 pos = npc.Center + new Vector2(
                    Main.rand.NextFloat(-npc.width * 0.5f, npc.width * 0.5f),
                    Main.rand.NextFloat(-npc.height * 0.3f, npc.height * 0.5f));
                PRTLoader.NewParticle<PRT_KikasaWispFlame>(pos,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(1.0f, 2.0f)),
                    KikasaWisp.Tint(KikasaWisp.GoldBody), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(20, 34));
            }
            //偶尔一盏离体的小游珠脱身而去
            if (time % 46 == 0) {
                PRTLoader.NewParticle<PRT_KikasaWispOrb>(
                    npc.Top + new Vector2(Main.rand.NextFloat(-npc.width * 0.3f, npc.width * 0.3f), -4f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.2f)),
                    KikasaWisp.Tint(KikasaWisp.GoldBody), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(50, 90), Main.rand.NextFloat(0.4f, 0.9f));
            }
        }
    }
}
