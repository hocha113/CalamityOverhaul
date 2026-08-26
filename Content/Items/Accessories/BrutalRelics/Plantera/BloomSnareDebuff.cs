using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Plantera
{
    /// <summary>
    /// 荆棘反缠减益：束缚减速+持续撕裂。owner 端 AddBuff 骑原版 buff 同步，
    /// 各端从同步的 buff 逐帧派生旗标，无自定义网络包；
    /// 荆棘反伤在 BloomNovaBulbPlayer.OnHurt(受害端=判伤端)结算
    /// </summary>
    internal class BloomSnaredDebuff : ModBuff
    {
        //NPC减益图标几乎不显示，图标直接用遗物本体图
        public override string Texture => CWRConstant.Item_BrutalRelic + "BloomNovaBulb";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            npc.GetGlobalNPC<BloomSnareNPC>().Snared = true;
        }
    }

    /// <summary>缠中状态落地：减速/撕裂/藤蔓覆盖装点。旗标每帧由减益重新点亮，禁静态状态</summary>
    internal class BloomSnareNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>本帧被荆棘缠住(由 BloomSnaredDebuff.Update 逐帧点亮)</summary>
        public bool Snared;

        public override void ResetEffects(NPC npc) => Snared = false;

        public override void PostAI(NPC npc) {
            if (!Snared) {
                return;
            }
            //束缚减速：AI写完速度后乘算。Boss砍两成，杂兵近半
            bool bossLike = npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type];
            npc.velocity *= bossLike ? 0.80f : 0.55f;

            //缠身微粒(客户端装点)
            if (!VaultUtils.isServer && Main.rand.NextBool(14)) {
                PlanteraRenderHelper.SpawnAmbientMote(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f), false);
            }
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (!Snared) {
                return;
            }
            //持续撕裂：240点/秒
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }
            npc.lifeRegen -= 480;
            if (damage < 80) {
                damage = 80;
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!Snared || npc.IsABestiaryIconDummy) {
                return;
            }

            //身上两道交叉荆棘：主藤(玩家→目标)由反缠弹幕负责，这里画贴身束缚网
            float w = MathHelper.Clamp(npc.width * 0.66f, 22f, 130f);
            float h = MathHelper.Clamp(npc.height * 0.42f, 14f, 90f);
            float halfWidth = MathHelper.Clamp(npc.width * 0.09f, 3.5f, 8f);
            float seedBase = 0.11f + npc.whoAmI * 0.037f % 0.6f;

            VineParams vine = VineParams.Default;
            vine.HalfWidth = halfWidth;
            vine.Taut = 0.88f;
            vine.Pulse = 0.5f;
            vine.PulseDir = 1f;
            vine.Phase2 = false;

            Vector2 a1 = npc.Center + new Vector2(-w, h);
            Vector2 a2 = npc.Center + new Vector2(w, -h);
            vine.RestLength = Vector2.Distance(a1, a2) * 1.05f;
            vine.Seed = seedBase;
            PlanteraVineRenderer.DrawVine(spriteBatch, a1, a2, vine);

            Vector2 b1 = npc.Center + new Vector2(w, h * 0.8f);
            Vector2 b2 = npc.Center + new Vector2(-w, -h * 0.8f);
            vine.Seed = seedBase + 0.23f;
            PlanteraVineRenderer.DrawVine(spriteBatch, b1, b2, vine);
        }
    }
}
