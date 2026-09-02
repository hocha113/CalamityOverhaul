using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>尾节：复用体节跟链逻辑，单帧贴图、更小判定</summary>
    internal class BssTail : BssBody
    {
        public override string Texture => CWRConstant.NPC + "BSS/Tail";

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 36;
            NPC.height = 36;
            NPC.damage = BssDirector.TailContact;
            NPC.defense = BssDirector.TailDefense;
            NPC.lifeMax = BssDirector.TailLife;
        }

        protected override bool IsFlower => false;

        protected override float DrawOriginShift => BssDirector.TailOriginShift;

        public override void FindFrame(int frameHeight) {
            //尾节单帧：链序落在红花位也不换帧
            NPC.frame = new Rectangle(0, 0, TextureAssets.Npc[Type].Width(), frameHeight);
        }
    }
}
