using CalamityMod;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Summon;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.RemakeItems.ModifyBag;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerHeadAI : NPCOverride, ICWRLoader
    {
        public override int TargetID => NPCID.TheDestroyer;
        public static bool MachineRebellion;
        private bool machineRebellion_ByNPC;
        internal static Asset<Texture2D> Head;
        internal static Asset<Texture2D> Head_Glow;
        private static int iconIndex;
        private int frame;
        private int glowFrame;
        private bool openMouth;
        private int dontOpenMouthTime;
        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Head", -1);
            iconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Head");
        }

        void ICWRLoader.LoadAsset() {
            Head = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Head");
            Head_Glow = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Head_Glow");
        }
        void ICWRLoader.UnLoadData() {
            Head = null;
            Head_Glow = null;
        }

        public override void SetProperty() {
            if (MachineRebellion) {
                npc.defDefense = npc.defense = 80;
                npc.defDamage = npc.damage *= 2;

                machineRebellion_ByNPC = true;
                netOtherWorkSend = true;
                MachineRebellion = false;
            }
        }

        public override void BossHeadSlot(ref int index) {
            if (!HeadPrimeAI.DontReform()) {
                index = iconIndex;
            }
        }

        public override void BossHeadRotation(ref float rotation) => rotation = npc.rotation + MathHelper.Pi;

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            IItemDropRuleCondition condition = new DropInDeathMode();
            LeadingConditionRule rule = new LeadingConditionRule(condition);
            rule.Add(ModContent.ItemType<DestroyersBlade>(), 4);
            rule.Add(ModContent.ItemType<StaffoftheDestroyer>(), 4);
            rule.Add(ModContent.ItemType<Observer>(), 4);
            rule.Add(ModContent.ItemType<ForgedLash>(), 4);
            npcLoot.Add(rule);
        }

        public override bool AI() {
            CWRUtils.ClockFrame(ref glowFrame, 5, 3);
            Player target = CWRUtils.GetPlayerInstance(npc.target);
            if (target.Alives()) {
                float dotProduct = Vector2.Dot(npc.velocity.UnitVector(), npc.Center.To(target.Center).UnitVector());
                float toPlayerLang = npc.Distance(target.Center);
                if (toPlayerLang < 660 && toPlayerLang > 100 && dotProduct > 0.8f) {
                    if (dontOpenMouthTime <= 0) {
                        openMouth = true;
                    }
                }
                else {
                    openMouth = false;
                }

                if (openMouth) {
                    if (frame < 3) {
                        frame++;
                    }
                    dontOpenMouthTime = 120;
                }
                else {
                    if (frame > 0) {
                        frame--;
                    }
                }
            }
            if (dontOpenMouthTime > 0) {
                dontOpenMouthTime--;
            }
            return true;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            Texture2D value = Head.Value;
            Rectangle rectangle = CWRUtils.GetRec(value, frame, 4);
            Rectangle glowRectangle = CWRUtils.GetRec(value, glowFrame, 4);
            spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , rectangle, drawColor, npc.rotation + MathHelper.Pi, rectangle.Size() / 2, npc.scale, SpriteEffects.None, 0);
            Texture2D value2 = Head_Glow.Value;
            spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , glowRectangle, Color.White, npc.rotation + MathHelper.Pi, glowRectangle.Size() / 2, npc.scale, SpriteEffects.None, 0);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
    }
}
