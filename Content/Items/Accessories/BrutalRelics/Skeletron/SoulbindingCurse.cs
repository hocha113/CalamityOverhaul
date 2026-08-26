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
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }

    /// <summary>
    /// 掌攫禁锢：幽灵巨手抓握标记，在身期间每帧清零速度。
    /// 由掌攫命中方 AddBuff，速度清零跑在 NPC 模拟端（联机即服务端），位置随原版同步
    /// </summary>
    internal sealed class SoulGripLockDebuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }

    /// <summary>缚魂咒结算与演出：增伤在判伤端按同步 buff 生效，禁锢在 AI 端盖速度</summary>
    internal sealed class SoulbindingCurseGlobalNPC : GlobalNPC
    {
        /// <summary>缚魂咒受伤倍率，与 Tooltip 的 32% 同源</summary>
        internal const float CurseAmp = 1.32f;

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (npc.HasBuff<SoulbindCurseDebuff>()) {
                modifiers.FinalDamage *= CurseAmp;
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
            if (Main.dedServ || !npc.HasBuff<SoulbindCurseDebuff>()) {
                return;
            }
            drawColor = Color.Lerp(drawColor, new Color(150, 200, 205), 0.12f);
            if (Main.rand.NextBool(8)) {
                Vector2 root = npc.Center + new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f) * npc.width,
                    Main.rand.NextFloat(-0.2f, 0.5f) * npc.height);
                float sway = Main.rand.NextFloat(-0.25f, 0.25f);
                SkeletronFlameRender.Push(root, -MathHelper.PiOver2 + sway,
                    new Vector2(9f, 20f + Main.rand.NextFloat(10f)),
                    0.35f, Main.rand.NextFloat(), 0.65f, 0.6f);
            }
        }
    }
}
