using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //禁刷怪，白名单仅全息训练标靶
    internal class CybCourseNPC : GlobalNPC
    {
        //运行期判定，避免静态初始化时 NPC 类型未注册
        public static bool IsWhitelisted(int type) => type == ModContent.NPCType<CybTrainingDummy>();

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (!CybCourseWorld.Active)
                return;
            spawnRate = 0;
            maxSpawns = 0;
        }

        public override bool PreAI(NPC npc) {
            if (!CybCourseWorld.Active)
                return true;
            if (IsWhitelisted(npc.type))
                return true;
            npc.active = false;
            npc.netUpdate = true;
            return false;
        }
    }
}
