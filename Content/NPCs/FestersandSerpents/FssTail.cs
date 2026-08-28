using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>尾节：复用体节跟链逻辑，单帧贴图、更小判定</summary>
    internal class FssTail : FssBody
    {
        public override string Texture => CWRConstant.NPC + "BSS/Tail";

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Ichor] = true;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 30;
            NPC.height = 30;
            NPC.damage = FssDirector.TailContact;
            NPC.defense = FssDirector.TailDefense;
            NPC.lifeMax = FssDirector.TailLife;
        }

        protected override bool IsCyst => false;

        public override void FindFrame(int frameHeight) {
            //尾节单帧：链序落在囊肿位也不换帧
            NPC.frame = new Rectangle(0, 0, TextureAssets.Npc[Type].Width(), frameHeight);
        }
    }
}
