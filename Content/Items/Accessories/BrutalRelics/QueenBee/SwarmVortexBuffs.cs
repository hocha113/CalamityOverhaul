using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenBee
{
    /// <summary>
    /// 蜜蜡甲buff：存在=甲在身，跨端真相靠原版玩家buff同步。<br/>
    /// 池量与吸收逻辑全在 <see cref="SwarmVortexPlayer"/>，buff本身不携带数值
    /// </summary>
    internal class WaxWardBuff : ModBuff
    {
        public override string Texture => CWRConstant.Item_BrutalRelic + "WaxWardBuff";

        //本地化默认值(zh正典)；Buffs.hjson 条目归属协调者，报告已注明
        public override LocalizedText DisplayName => this.GetLocalization("DisplayName", () => "蜜蜡甲");
        public override LocalizedText Description => this.GetLocalization("Description", () => "蜂群结成的蜜蜡甲正在替你承伤");

        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            //owner每帧续短时；远端由同步的buff列表自然维持
            player.buffTime[buffIndex] = System.Math.Max(player.buffTime[buffIndex], 2);
        }
    }

    /// <summary>蜂涡缠身减益：被蜂群绞杀，行动迟缓。挂减速旗，实际减速在 <see cref="SwarmVortexGlobalNPC"/></summary>
    internal class SwarmVortexDebuff : ModBuff
    {
        public override string Texture => CWRConstant.Item_BrutalRelic + "SwarmVortexDebuff";

        public override LocalizedText DisplayName => this.GetLocalization("DisplayName", () => "蜂涡缠身");
        public override LocalizedText Description => this.GetLocalization("Description", () => "被蜂群旋涡绞杀，行动迟缓");

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex)
            => npc.GetGlobalNPC<SwarmVortexGlobalNPC>().VortexSlowed = true;
    }

    /// <summary>
    /// 蜂涡减速施加：buff经原版同步落在各端，此处每帧按同一规则回滚位移，全端确定性一致。<br/>
    /// 普通敌怪拖得狠、Boss只拖一小口(公平阀)；8层启动成本背书重档
    /// </summary>
    internal class SwarmVortexGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>本帧被蜂涡缠住，buff逐帧点亮</summary>
        public bool VortexSlowed;

        /// <summary>
        /// Boss级判定：Boss本体/计名Boss/蠕虫链。蠕虫整链按Boss档折扣，
        /// 避免分段回滚不齐拉伸链体(镜像巨鹿白盲先例)
        /// </summary>
        private static bool IsBossLike(NPC npc) {
            return npc.boss || Terraria.ID.NPCID.Sets.ShouldBeCountedAsBoss[npc.type]
                || npc.aiStyle == Terraria.ID.NPCAIStyleID.Worm;
        }

        public override void ResetEffects(NPC npc) => VortexSlowed = false;

        public override void PostAI(NPC npc) {
            if (!VortexSlowed) {
                return;
            }
            //位移回滚式减速：不碰velocity，AI节奏不乱，等效减速稳定不复利
            float hold = IsBossLike(npc) ? 0.15f : 0.50f;
            npc.position -= npc.velocity * hold;
        }
    }
}
