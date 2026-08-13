using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds
{
    /// <summary>尾节：链条末端，逻辑全继承体节，仅换贴图</summary>
    internal class EowTailAI : EowBodyAI
    {
        public override int TargetID => NPCID.EaterofWorldsTail;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.EaterofWorldsTail);
            Main.instance.LoadNPC(NPCID.EaterofWorldsHead);
            Texture2D tailTex = TextureAssets.Npc[NPCID.EaterofWorldsTail].Value;
            Texture2D headTex = TextureAssets.Npc[NPCID.EaterofWorldsHead].Value;

            //尾永不领队，morph 恒0，仍复用统一绘制取得脉冲染色
            DrawSegment(spriteBatch, screenPos, drawColor, tailTex, headTex, 0f);
            return false;
        }
    }
}
