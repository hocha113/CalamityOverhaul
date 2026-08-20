using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>白昼狂暴：晨光撕开诅咒的缰绳，头颅化作不可抗拒的旋杀死神<br/>
    /// 惩罚态豁免（契约3.4）：无预警无缺口是本状态的设计目标——白昼拖延的代价即不可躲</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.DayEnrage, typeof(SkeletronStateContext))]
    internal class SkeletronDayEnrageState : SkeletronStateBase
    {
        public override string StateName => "DayEnrage";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.DayEnrage;

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;

            //守护者规格：碾杀伤害 + 铁壁
            npc.damage = 1000;
            npc.defense = 9999;
            context.EyeFlame = 1.6f;
            context.SpinVortex = MathHelper.Clamp(Timer / 40f, 0f, 1f);
            SkeletronScreenEffects.RequestDomain(0.35f);

            if (Timer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.2f, Pitch = 0.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.9f, Pitch = -0.2f }, npc.Center);
                SkeletronScreenEffects.PushShake(npc.Center, 8f);
            }

            //加速追猎：转向受限的持续逼近，速度随时间攀升
            float speed = MathF.Min(11f + Timer * 0.055f, 34f);
            Vector2 want = (context.Target.Center - npc.Center).SafeNormalize(Vector2.UnitY) * speed;
            npc.velocity = Vector2.Lerp(npc.velocity, want, 0.045f);
            SpinRotation(npc, 0.4f);

            //幽火剥落
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(npc.Center + Main.rand.NextVector2Circular(34f, 34f),
                    -npc.velocity * 0.1f + Main.rand.NextVector2Circular(1.5f, 1.5f),
                    SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.5f, 2.4f))?.Configure(Main.rand.Next(18, 30));
            }

            Timer++;
            //白昼狂暴不主动退出：夜幕重临才回到循环
            if (!Main.IsItDay() && !VaultUtils.isClient) {
                return new SkeletronHubState();
            }
            return null;
        }

        public override void OnExit(SkeletronStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            npc.defense = npc.defDefense;
            npc.damage = npc.defDamage;
            SettleRotation(npc, 1f);
        }
    }
}
