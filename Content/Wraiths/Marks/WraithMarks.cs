using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Marks
{
    /// <summary>印记读写的唯一门面；各鬼不直接摸 <see cref="WraithMarkNPC"/>。</summary>
    internal static class WraithMarks
    {
        /// <summary>湿：雨蚀一跳管这么久，掉出雨域后还挂一会儿</summary>
        internal const int SoakedTicks = 90;
        /// <summary>攥：随抓取滚动续期，松手后余一小段</summary>
        internal const int GrippedTicks = 40;
        /// <summary>断：刀口敞着的窗口</summary>
        internal const int SeveredTicks = 180;
        /// <summary>照：灯照过的余亮</summary>
        internal const int LitTicks = 150;
        /// <summary>缚：喜堂圈住的时长</summary>
        internal const int BetrothedTicks = 300;

        private static WraithMarkNPC Of(NPC npc)
            => npc?.active == true && npc.TryGetGlobalNPC(out WraithMarkNPC marks) ? marks : null;

        /// <summary><paramref name="key"/> 为施加鬼的 Key，三印崩按它付费。</summary>
        internal static void Apply(NPC npc, WraithMark mark, int ticks, float power,
            int owner, string key)
            => Of(npc)?.Apply(mark, ticks, power, owner, key);

        internal static bool Has(NPC npc, WraithMark mark, int owner)
            => Of(npc)?.Has(mark, owner) == true;

        internal static float PowerOf(NPC npc, WraithMark mark, int owner)
            => Of(npc)?.PowerOf(mark, owner) ?? 0f;

        internal static WraithMark Active(NPC npc, int owner)
            => Of(npc)?.Active(owner) ?? WraithMark.None;

        /// <summary>身上来自同一施加者的不同印记数量，三印崩读它。</summary>
        internal static int CountActive(NPC npc, int owner) {
            WraithMark active = Active(npc, owner);
            int count = 0;
            for (int i = 0; i < WraithMarkExtensions.Count; i++) {
                if ((active & WraithMarkExtensions.FromIndex(i)) != 0) {
                    count++;
                }
            }
            return count;
        }
    }
}
