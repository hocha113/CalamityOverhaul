using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Skeletron
{
    /// <summary>
    /// 缚魂咒：诅咒领域减益，在身期间受到的伤害提高。
    /// 由领域权威端节流施加，AddBuff 骑原版同步，各端按同步的 buff 状态一致结算
    /// </summary>
    internal sealed class SoulbindCurseDebuff : ModBuff
    {
        public override string Texture => CWRConstant.Item_BrutalRelic + "SoulbindCurseDebuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }

    /// <summary>
    /// 掌攫禁锢：幽灵巨手抓握标记，在身期间每帧清零速度。
    /// 由掌攫命中方 AddBuff，速度清零跑在 NPC 模拟端（联机即服务端），位置随原版同步。
    /// 仅普通敌人吃此禁锢，Boss 改吃缚魂重压（Boss 硬控红线）
    /// </summary>
    internal sealed class SoulGripLockDebuff : ModBuff
    {
        public override string Texture => CWRConstant.Item_BrutalRelic + "SoulGripLockDebuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }

    /// <summary>
    /// 缚魂重压：掌攫命中挂上的团队标记，在身期间受到一切来源的伤害提高。
    /// 权威端 AddBuff 骑原版同步，各端按同步 buff 一致结算
    /// </summary>
    internal sealed class SoulbindOverwhelmDebuff : ModBuff
    {
        /// <summary>持续时长（tick）＝5 秒</summary>
        public const int Duration = 300;

        public override string Texture => CWRConstant.Item_BrutalRelic + "SoulbindOverwhelmDebuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }

    /// <summary>缚魂系结算与演出：重压增伤在判伤端按同步 buff 生效，禁锢在 AI 端盖速度</summary>
    internal sealed class SoulbindingCurseGlobalNPC : GlobalNPC
    {
        /// <summary>缚魂重压受伤倍率（全来源），与 Tooltip 的 20% 同源</summary>
        internal const float OverwhelmAmp = 1.20f;

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (npc.HasBuff<SoulbindOverwhelmDebuff>()) {
                modifiers.FinalDamage *= OverwhelmAmp;
            }
        }

        /// <summary>PostAI 晚于本体 AI，盖掉当帧自驱速度（GhostGrip 同范式）</summary>
        public override void PostAI(NPC npc) {
            if (npc.HasBuff<SoulGripLockDebuff>()) {
                npc.velocity = Vector2.Zero;
            }
        }

        /// <summary>被诅咒者：微冷色浸染 + 身上稀疏舔起的小段咒焰（纯绘制端）</summary>
        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (Main.dedServ) {
                return;
            }
            bool overwhelmed = npc.HasBuff<SoulbindOverwhelmDebuff>();
            if (!overwhelmed && !npc.HasBuff<SoulbindCurseDebuff>()) {
                return;
            }
            drawColor = Color.Lerp(drawColor, new Color(150, 200, 205), overwhelmed ? 0.22f : 0.12f);
            if (Main.rand.NextBool(8)) {
                Vector2 root = npc.Center + new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f) * npc.width,
                    Main.rand.NextFloat(-0.2f, 0.5f) * npc.height);
                float sway = Main.rand.NextFloat(-0.25f, 0.25f);
                SkeletronFlameRender.Push(root, -MathHelper.PiOver2 + sway,
                    new Vector2(9f, 20f + Main.rand.NextFloat(10f)),
                    0.35f, Main.rand.NextFloat(), 0.65f, 0.6f);
            }
            //重压标记：咒焰自头顶向下压覆，密度与咒紫混比都调重（复用同一冷焰语汇，无新资产）
            if (overwhelmed && Main.rand.NextBool(4)) {
                Vector2 root = npc.Center + new Vector2(
                    Main.rand.NextFloat(-0.4f, 0.4f) * npc.width,
                    -npc.height * 0.5f - Main.rand.NextFloat(4f, 14f));
                float sway = Main.rand.NextFloat(-0.2f, 0.2f);
                SkeletronFlameRender.Push(root, MathHelper.PiOver2 + sway,
                    new Vector2(11f, 24f + Main.rand.NextFloat(12f)),
                    0.3f, Main.rand.NextFloat(), 0.85f, 0.7f);
            }
        }
    }
}
