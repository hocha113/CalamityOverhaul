using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Buffs
{
    /// <summary>
    /// 鬼手压制标记；在身期间每帧清零速度。<br/>
    /// <c>GhostHandProj.GrippingBehavior</c> 滚动续期 8 帧，松手后自然过期
    /// </summary>
    internal sealed class GhostGripDebuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }

    /// <summary>压制执行端，PostAI 晚于本体 AI，盖掉当帧自驱速度</summary>
    internal sealed class GhostGripGlobalNPC : GlobalNPC
    {
        public override void PostAI(NPC npc) {
            if (npc.HasBuff<GhostGripDebuff>()) {
                npc.velocity = Vector2.Zero;
            }
        }
    }
}
