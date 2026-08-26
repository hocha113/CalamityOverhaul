using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds
{
    /// <summary>统一血池进度条：任意体节聚焦都显示头部血池，替代原版逐节求和条</summary>
    internal class EowBossBar : ModBossBar
    {
        //贴图契约（BossBarLoader.DrawFancyBar_TML）：Texture 会被直接当作 516x348 六帧条体图集使用；
        //占位图会让条体塌缩成不可见。这里故意不给贴图路径，BossBarLoader.GetTexture 在
        //RequestIfExists 失败时回落到原版 UI_BossBar 图集，得到标准框+填充+背景
        public override string Texture => "CalamityOverhaul/UseVanillaBossBarSheet";

        public override Asset<Texture2D> GetIconTexture(ref Rectangle? iconFrame) {
            int headSlot = NPCID.Sets.BossHeadTextures[NPCID.EaterofWorldsHead];
            if (headSlot >= 0) {
                return TextureAssets.NpcHeadBoss[headSlot];
            }
            return null;
        }

        public override bool? ModifyInfo(ref BigProgressBarInfo info, ref float life, ref float lifeMax,
            ref float shield, ref float shieldMax) {
            //聚焦NPC可能是任意节，一律折算到头
            NPC focus = Main.npc[info.npcIndexToAimAt];
            NPC head = null;

            if (focus.active && focus.type == NPCID.EaterofWorldsHead) {
                head = focus;
            }
            else if (focus.active && (focus.type == NPCID.EaterofWorldsBody || focus.type == NPCID.EaterofWorldsTail)) {
                int headIdx = focus.realLife;
                if (headIdx >= 0 && headIdx < Main.maxNPCs && Main.npc[headIdx].active
                    && Main.npc[headIdx].type == NPCID.EaterofWorldsHead) {
                    head = Main.npc[headIdx];
                }
            }

            if (head == null) {
                //兜底：找场上任意头
                foreach (var n in Main.ActiveNPCs) {
                    if (n.type == NPCID.EaterofWorldsHead) {
                        head = n;
                        break;
                    }
                }
            }

            if (head == null) {
                //分裂期兜底:头节丢失时按"池主"聚合场上全部世吞节段的生命,
                //血条不再整根消失(反馈十三·#101)。有 realLife 的节只数池主一次,独立节直接计
                float sumLife = 0f;
                float sumMax = 0f;
                foreach (var n in Main.ActiveNPCs) {
                    if (n.type != NPCID.EaterofWorldsHead && n.type != NPCID.EaterofWorldsBody
                        && n.type != NPCID.EaterofWorldsTail) {
                        continue;
                    }
                    if (n.realLife >= 0 && n.realLife < Main.maxNPCs && n.realLife != n.whoAmI
                        && Main.npc[n.realLife].active) {
                        continue;
                    }
                    sumLife += Utils.Clamp(n.life, 0f, n.lifeMax);
                    sumMax += n.lifeMax;
                }
                if (sumMax <= 0f) {
                    return false;
                }
                life = sumLife;
                lifeMax = sumMax;
                shield = 0f;
                shieldMax = 0f;
                return true;
            }

            life = Utils.Clamp(head.life, 0f, head.lifeMax);
            lifeMax = head.lifeMax;
            shield = 0f;
            shieldMax = 0f;
            return true;
        }
    }
}
