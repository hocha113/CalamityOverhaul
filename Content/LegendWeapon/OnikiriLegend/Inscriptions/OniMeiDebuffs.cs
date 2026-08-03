using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 滞樋「滞缚」/虚吼威压共用的墨锚黏脚。香草 Slow(32) 对 NPC 没有任何效果处理，
    /// 故自实现：位移阻尼在 <see cref="OniMeiNPCEffects.PostAI"/>(AI 写完速度后再拖)，
    /// 本类只挂旗标与介质；AddBuff 走香草联机同步
    /// </summary>
    internal class OniBindDebuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            //介质：垂落墨丝+偶发贴地墨渍，读作被墨黏住
            if (Main.dedServ || !Main.rand.NextBool(5)) {
                return;
            }
            Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
            PRTLoader.NewParticle<PRT_OniInkDrop>(pos, Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.6f)
                , new Color(56, 14, 20), Main.rand.NextFloat(0.16f, 0.28f))
                ?.Configure(Main.rand.Next(14, 22));
        }
    }

    /// <summary>
    /// 痺雕「痺反」：来手发麻。接触伤削减在 <see cref="OniMeiNPCEffects.ModifyHitPlayer"/>，
    /// 轻度阻尼共用 <see cref="OniMeiNPCEffects.PostAI"/>；本类只挂旗标与介质
    /// </summary>
    internal class OniNumbDebuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            //介质：纸白麻痹微火花，短促细碎
            if (Main.dedServ || !Main.rand.NextBool(4)) {
                return;
            }
            Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.55f, npc.height * 0.55f);
            PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, Main.rand.NextVector2Circular(1.2f, 1.2f)
                , new Color(255, 243, 226), Main.rand.NextFloat(0.12f, 0.22f))
                ?.Configure(Main.rand.Next(6, 11), affectedByGravity: false);
        }
    }

    /// <summary>
    /// 滞缚/痺的效果层：AI 之后阻尼位移(boss 减效)，蠕虫节体跳过(跟头走不重复拖)；
    /// 痺中的 NPC 接触伤打折——"麻了的手打不疼"
    /// </summary>
    internal class OniMeiNPCEffects : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private Vector2 previousVelocityDelta;

        public override bool PreAI(NPC npc) {
            if (previousVelocityDelta != Vector2.Zero) {
                npc.velocity -= previousVelocityDelta;
                previousVelocityDelta = Vector2.Zero;
            }
            return true;
        }

        public override void PostAI(NPC npc) {
            NPC root = OniMeiCombat.ResolveEffectRoot(npc);
            if (root == null || root.whoAmI != npc.whoAmI) {
                return;
            }
            bool bind = root.HasBuff<OniBindDebuff>();
            bool numb = root.HasBuff<OniNumbDebuff>();
            if (!bind && !numb) {
                return;
            }
            bool boss = NpcGroupHelper.IsBossTier(root);
            float damp = 1f;
            if (bind) {
                damp = Math.Min(damp, boss ? OniMeiCombat.BindBossDampMul : OniMeiCombat.BindDampMul);
            }
            if (numb) {
                damp = Math.Min(damp, boss ? OniMeiCombat.NumbBossDampMul : OniMeiCombat.NumbDampMul);
            }
            Vector2 before = root.velocity;
            root.velocity = before * damp;
            previousVelocityDelta = root.velocity - before;
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers) {
            NPC root = OniMeiCombat.ResolveEffectRoot(npc);
            if (root?.HasBuff<OniNumbDebuff>() == true) {
                modifiers.FinalDamage *= OniMeiCombat.NumbContactDamageMul;
            }
        }
    }
}
