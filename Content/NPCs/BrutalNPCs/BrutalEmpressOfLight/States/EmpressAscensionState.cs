using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 三阶段变身（15%）：护盾碎裂 → 悬停头顶 → 七句台词 → 日舞姿势里回血至 35% → 相位姿势 → 终章。
    /// 600f 全程无敌，是全场最长的一次呼吸，用回血宣布"后面才是正戏"
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Ascension, typeof(EmpressStateContext))]
    internal class EmpressAscensionState : EmpressStateBase
    {
        public override string StateName => "EmpressAscension";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Ascension;

        internal const int TotalTime = 600;
        private const int HealStart = 330;
        private const int HealEnd = 450;
        internal const float HealFraction = 0.35f;
        private static readonly int[] LineTicks = [30, 120, 210, 300, 390, 450, 540];

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            EmpressCast.ClearHostileProjectiles(npc);
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.velocity *= 0.3f;
            //护盾碎裂拍
            PlayLocal(SoundID.DD2_ExplosiveTrapExplode with { Volume = 1f, Pitch = -0.5f }, npc.Center);
            PlayLocal(SoundID.DD2_CrystalCartImpact with { Volume = 1.2f, Pitch = -0.5f }, npc.Center);
            EmpressMotion.Shake(npc.Center, 14f, 26);
            if (!VaultUtils.isServer) {
                EmpressScreenFX.PushPrismPulse(npc.Center, 1f, 44);
                for (int i = 0; i < 90; i++) {
                    float r = Main.rand.NextFloat();
                    r = MathF.Sqrt(r) + r * r;
                    Vector2 dir = Main.rand.NextVector2Unit();
                    float hue = Main.rand.NextFloat();
                    PRTLoader.NewParticle<PRT_EmpressSpark>(npc.Center, dir * (r * 20f + 4f),
                        EmpressMotion.FormColor(hue, context.DayFormBlend, 0.72f), Main.rand.NextFloat(0.9f, 1.8f) * MathF.Sqrt(r))?
                        .Configure(Main.rand.Next(24, 44), hue, context.DayFormBlend);
                }
            }
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            npc.dontTakeDamage = true;
            npc.damage = 0;

            //悬停头顶，随呼吸
            if (Timer > 90 && target.Alives()) {
                Vector2 dest = target.Center - new Vector2(0f, 225f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.05f) * 15f);
                npc.velocity = Vector2.Lerp(npc.velocity, (dest - npc.Center) * 0.05f, 0.2f);
            }
            else {
                npc.velocity *= 0.95f;
            }

            //台词（权威端广播）
            for (int i = 0; i < LineTicks.Length; i++) {
                if (Timer == LineTicks[i]) {
                    EmpressOfLightAI.SayAscension(i);
                }
            }

            if (Timer < HealStart) {
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareAmbient(0.4f + 0.3f * Timer / HealStart);
                }
            }
            else if (Timer < HealEnd) {
                //日舞姿势里回血：越到后面越快，上限 35%
                context.Pose = EmpressPose.Dance;
                context.PoseTimer = 39f;
                float k = MathHelper.Clamp((Timer - HealStart) / 60f, 0f, 1f);
                int cap = (int)(npc.lifeMax * HealFraction);
                if (npc.life < cap) {
                    int add = (int)Math.Ceiling((cap - npc.life) * 0.05f * k) + npc.lifeMax / 2000;
                    //血量只在权威端写，客户端靠原版 NPC 同步拿到；治疗数字各端本地估算显示
                    if (!VaultUtils.isClient) {
                        npc.life = Math.Min(npc.life + add, cap);
                    }
                    if (!VaultUtils.isServer && Timer % 4 == 0) {
                        CombatText.NewText(npc.Hitbox, CombatText.HealLife, add);
                    }
                }
                context.SetChargeState(3, k);
                EmpressMotion.Shake(npc.Center, 0.6f, 4);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareAmbient(0.7f + 0.2f * k);
                    if (Main.rand.NextBool(2)) {
                        float hue = Main.rand.NextFloat();
                        Vector2 spawn = npc.Center + Main.rand.NextVector2CircularEdge(360f, 380f);
                        PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (npc.Center - spawn) * 0.05f,
                            EmpressMotion.FormColor(hue, context.DayFormBlend, 0.7f), Main.rand.NextFloat(0.7f, 1.2f))?.Configure(20, hue, context.DayFormBlend);
                    }
                }
            }
            else {
                //相位姿势：原版变身绘制的白闪与幻影；上限 58 避开 ai[1]≥60 的本体隐没窗
                context.Pose = EmpressPose.Transform;
                context.PoseTimer = MathHelper.Clamp((Timer - HealEnd) / (float)(TotalTime - HealEnd) * 58f, 0f, 58f);
                float k = (Timer - HealEnd) / (float)(TotalTime - HealEnd);
                context.SetChargeState(3, k);
                EmpressMotion.Shake(npc.Center, 0.25f + k * 1.2f, 4);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareAmbient(0.9f);
                    if (Timer == TotalTime - 60) {
                        EmpressScreenFX.PushPhaseGlow();
                    }
                }
            }

            if (Timer >= TotalTime) {
                if (!VaultUtils.isClient) {
                    npc.ai[3] = (int)npc.ai[3] | 4;
                    npc.netUpdate = true;
                    context.AttackCounter = 0;
                }
                EmpressMotion.Shake(npc.Center, 10f, 20);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.PushPrismPulse(npc.Center, 0.9f, 36);
                    EmpressScreenFX.PushPhaseFlash(0.6f);
                }
                PlayLocal(SoundID.Item163 with { Volume = 1f, Pitch = 0.1f }, npc.Center);
                npc.dontTakeDamage = false;
                return new EmpressFinaleState();
            }
            return null;
        }

        public override void OnExit(EmpressStateContext context) {
            base.OnExit(context);
            context.Npc.dontTakeDamage = false;
        }
    }
}
