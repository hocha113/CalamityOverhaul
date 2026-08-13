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
        //占位贴图：本条不使用自定义条形贴图，走原版 DrawFancyBar
        public override string Texture => CWRConstant.VaultPlaceholder;

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
                return false;
            }

            life = Utils.Clamp(head.life, 0f, head.lifeMax);
            lifeMax = head.lifeMax;
            shield = 0f;
            shieldMax = 0f;
            return true;
        }
    }
}
