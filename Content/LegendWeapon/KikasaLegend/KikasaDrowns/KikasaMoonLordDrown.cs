using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 月总沉湖收尾：沉溺不走 CheckDead，必须按核心索引清整组。
    /// 禁止对手/头调 checkDead（原版会再刷真眼，#87 软锁）。
    /// </summary>
    internal static class KikasaMoonLordDrown
    {
        private const string BrutalProjNamespace =
            "CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles";

        /// <summary>核心/头/手/真眼/水蛭囊</summary>
        internal static bool IsPart(int type)
            => CWRLoad.MoonLordSegments != null && CWRLoad.MoonLordSegments.Contains(type);

        /// <summary>已击败过的月总部件：战斗无敌窗不挡沉湖抓取</summary>
        internal static bool IsDefeatedPart(NPC npc)
            => npc != null && IsPart(npc.type) && KikasaBossGate.IsDefeated(npc);

        /// <summary>归属核心槽：核心用 whoAmI，其余部位沿用原版 ai[3]</summary>
        internal static int CoreIndexOf(NPC npc)
            => npc.type == NPCID.MoonLordCore ? npc.whoAmI : (int)npc.ai[3];

        /// <summary>同一颗心的在场部件写入 output（含无敌与破损残口）</summary>
        internal static void CollectFamily(NPC anyPart, List<NPC> output) {
            output.Clear();
            if (anyPart == null || !anyPart.active || !IsPart(anyPart.type)) {
                return;
            }
            int core = CoreIndexOf(anyPart);
            if (core < 0) {
                return;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (IsPart(npc.type) && CoreIndexOf(npc) == core) {
                    output.Add(npc);
                }
            }
        }

        /// <summary>完成帧再扫：钉身窗内新刷的真眼/水蛭囊 + 幻影弹 + 残酷演出核心</summary>
        internal static void SweepRemainders(int coreWho) {
            if (coreWho < 0) {
                return;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!IsPart(npc.type) || CoreIndexOf(npc) != coreWho) {
                    continue;
                }
                npc.life = 0;
                npc.active = false;
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                }
            }

            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (IsMoonLordProjectile(proj)) {
                    proj.Kill();
                }
            }

            if (MoonLordCoreAI.ActivePerformanceCore == coreWho) {
                MoonLordCoreAI.ActivePerformanceCore = -1;
            }
        }

        private static bool IsMoonLordProjectile(Projectile proj) {
            if (proj.type == ProjectileID.PhantasmalEye
                || proj.type == ProjectileID.PhantasmalSphere
                || proj.type == ProjectileID.PhantasmalDeathray
                || proj.type == ProjectileID.MoonLeech
                || proj.type == ProjectileID.PhantasmalBolt) {
                return true;
            }
            ModProjectile modProj = proj.ModProjectile;
            return modProj != null && modProj.GetType().Namespace == BrutalProjNamespace;
        }
    }
}
