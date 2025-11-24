using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.RemakeItems.ModifyBag;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 管理头部AI的数据、属性和资源加载。
    /// </summary>
    internal partial class HeadPrimeAI : CWRNPCOverride
    {
        void ICWRLoader.LoadData() {
            string path = CWRConstant.NPC + "BSP/";
            CWRMod.Instance.AddBossHeadTexture(path + "Skeletron_Head", -1);
            iconIndex = ModContent.GetModBossHeadSlot(path + "Skeletron_Head");
        }

        void ICWRLoader.LoadAsset() {
            //先缓存原版的纹理
            Vanilla_TwinsBossBag = TextureAssets.Item[ItemID.TwinsBossBag];
            Vanilla_DestroyerBossBag = TextureAssets.Item[ItemID.DestroyerBossBag];
            Vanilla_SkeletronPrimeBossBag = TextureAssets.Item[ItemID.SkeletronPrimeBossBag];
            if (CWRServerConfig.Instance.BiologyOverhaul) {
                TextureAssets.Item[ItemID.TwinsBossBag] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Bag/TwinBag");
                TextureAssets.Item[ItemID.DestroyerBossBag] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Bag/DestroyerBag");
                TextureAssets.Item[ItemID.SkeletronPrimeBossBag] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Bag/PrimeBag");
            }
            else {//无论在什么情况下，修改了原版纹理都需要恢复它，这里考虑的是中途关闭了生物大修后的需要的恢复操作
                TextureAssets.Item[ItemID.TwinsBossBag] = Vanilla_TwinsBossBag;
                TextureAssets.Item[ItemID.DestroyerBossBag] = Vanilla_DestroyerBossBag;
                TextureAssets.Item[ItemID.SkeletronPrimeBossBag] = Vanilla_SkeletronPrimeBossBag;
            }
        }

        void ICWRLoader.UnLoadData() {
            if (VaultUtils.isServer) {//下面的操作不能在服务器上运行
                return;
            }

            //无论在什么情况下，修改了原版纹理都需要恢复它
            if (Vanilla_TwinsBossBag != null) {
                TextureAssets.Item[ItemID.TwinsBossBag] = Vanilla_TwinsBossBag;
            }
            if (Vanilla_TwinsBossBag != null) {
                TextureAssets.Item[ItemID.DestroyerBossBag] = Vanilla_DestroyerBossBag;
            }
            if (Vanilla_TwinsBossBag != null) {
                TextureAssets.Item[ItemID.SkeletronPrimeBossBag] = Vanilla_SkeletronPrimeBossBag;
            }
        }

        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            LeadingConditionRule rule = new LeadingConditionRule(new DropInDeathMode());
            rule.Add(ModContent.ItemType<CommandersChainsaw>(), 4);
            rule.Add(ModContent.ItemType<HyperionBarrage>(), 4);
            rule.Add(ModContent.ItemType<CommandersStaff>(), 4);
            rule.Add(ModContent.ItemType<CommandersClaw>(), 4);
            rule.Add(ModContent.ItemType<RaiderGun>(), 4);
            npcLoot.Add(rule);
            LeadingConditionRule rule2 = new LeadingConditionRule(new DropInMachineRebellion());
            rule2.Add(ModContent.ItemType<SoulofFrightEX>());
            rule2.Add(ModContent.ItemType<SoulofMightEX>());
            rule2.Add(ModContent.ItemType<SoulofSightEX>());
            rule2.Add(ModContent.ItemType<MetalMusicBox>(), dropRateInt: 5);
            npcLoot.Add(rule2);
        }

        public override void BossHeadSlot(ref int index) {
            if (!DontReform()) {
                index = iconIndex;
            }
        }

        public override bool CanLoad() => true;

        public override bool? CanCWROverride() {
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return null;
        }
    }
}
