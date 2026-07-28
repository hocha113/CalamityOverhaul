using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Buffs
{
    /// <summary>
    /// 替死后遗症：12秒内移速降至72%，屏幕显示血丝暗角。<br/>
    /// 不可右键清除，与侵蚀累积独立。
    /// </summary>
    internal sealed class ScapeAfterburn : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        /// <summary>供 WraithOmenRender 读取，决定暗角强度（0=无，1=全强）</summary>
        public static float LocalAfterburn;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;        //不可右键清除
            Main.pvpBuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.moveSpeed *= 0.72f;
            player.maxRunSpeed *= 0.72f;

            if (player.whoAmI == Main.myPlayer && !Main.dedServ) {
                LocalAfterburn = 1f;
            }
        }
    }
}