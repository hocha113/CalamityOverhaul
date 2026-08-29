using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Deerclops
{
    /// <summary>
    /// 白盲减益：重度冰冻减速+冻伤DoT。致盲的"仇恨混乱"半边由原版混乱buff承担，
    /// 本减益经原版NPC buff同步各端一致，状态旗标挂逐实体GlobalNPC
    /// </summary>
    internal class WhiteblindDebuff : ModBuff
    {
        public override string Texture => CWRConstant.Item_BrutalRelic + "WhiteblindDebuff";

        private LocalizedText displayNameCache;
        private LocalizedText descriptionCache;
        public override LocalizedText DisplayName
            => displayNameCache ??= this.GetLocalization(nameof(DisplayName), () => "白盲");
        public override LocalizedText Description
            => descriptionCache ??= this.GetLocalization(nameof(Description), () => "风雪蒙眼，行动迟滞，每秒10点冻伤");

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            npc.GetGlobalNPC<WhiteoutStormGlobalNPC>().Whiteblind = true;
        }
    }

    /// <summary>白盲的逐实体载体：减速回滚、冻伤DoT、蒙雪表现</summary>
    internal class WhiteoutStormGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>本帧处于白盲(buff逐帧点亮)</summary>
        public bool Whiteblind;

        /// <summary>
        /// Boss级判定：Boss本体/计名Boss/蠕虫链(整链折扣，避免分段回滚不齐拉伸链体)。
        /// Boss级不吃混乱、减速大打折扣，冻伤照吃
        /// </summary>
        internal static bool IsBossLike(NPC npc) {
            return npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || npc.aiStyle == NPCAIStyleID.Worm;
        }

        public override void ResetEffects(NPC npc) => Whiteblind = false;

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (!Whiteblind) {
                return;
            }
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }
            //10dps冻伤(lifeRegen单位=0.5HP/s)；正回复归零保留(对Boss再生的长线价值)
            npc.lifeRegen -= 20;
            if (damage < 5) {
                damage = 5;
            }
        }

        public override void PostAI(NPC npc) {
            if (!Whiteblind) {
                return;
            }
            //位移回滚式减速：不碰velocity，AI节奏不乱，各端按同步buff一致回滚。
            //非Boss回滚35%(减速中档)，Boss级12%(折扣声明见报告)
            float hold = IsBossLike(npc) ? 0.12f : 0.35f;
            npc.position -= npc.velocity * hold;
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (!Whiteblind) {
                return;
            }
            //霜蒙冷调
            drawColor = Color.Lerp(drawColor, new Color(200, 228, 255), 0.35f);

            //头顶蒙雪：雪粒自头顶缓落
            if (Main.rand.NextBool(3)) {
                Dust snow = Dust.NewDustDirect(new Vector2(npc.position.X, npc.position.Y - 12f),
                    npc.width, 10, DustID.Snow, npc.velocity.X * 0.2f, 0.5f, 120, default, Main.rand.NextFloat(0.7f, 1.1f));
                snow.velocity = new Vector2(snow.velocity.X * 0.4f, Main.rand.NextFloat(0.4f, 1.2f));
                snow.noGravity = false;
            }
            //偶发晶面反光
            if (Main.rand.NextBool(22)) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f),
                    Vector2.Zero, DeerclopsMotion.ColdWhite, Main.rand.NextFloat(1.6f, 2.6f))
                    .Configure(Main.rand.Next(16, 26));
            }
        }
    }
}
