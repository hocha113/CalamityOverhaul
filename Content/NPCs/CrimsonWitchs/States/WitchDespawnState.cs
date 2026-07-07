using CalamityOverhaul.Content.NPCs.CrimsonWitchs.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.CrimsonWitchs.States
{
    /// <summary>屈膝礼离场：目标失效或远遁触发。
    /// 减速驻足 → 微屈身行礼 → 火光内收熄灭消失（M7 补专属粒子与音效打磨）</summary>
    [InnoVault.StateMachines.VaultState((int)WitchStateIndex.Despawn, typeof(WitchStateContext))]
    internal class WitchDespawnState : WitchStateBase
    {
        public override string StateName => "Despawn";
        public override WitchStateIndex StateIndex => WitchStateIndex.Despawn;

        //====演出节奏（帧）====
        private const int SettleTime = 45;   //减速驻足
        private const int CurtsyTime = 50;   //屈膝礼
        private const int FadeTime = 40;     //熄灭消失
        private const int CurtsyEnd = SettleTime + CurtsyTime;
        private const int TotalTime = CurtsyEnd + FadeTime;

        public override void OnEnter(WitchStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.dontTakeDamage = true;
        }

        public override IWitchState OnUpdate(WitchStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            npc.dontTakeDamage = true;

            if (Timer <= SettleTime) {
                //驻足：指数刹车归于静止
                npc.velocity *= 0.9f;
                npc.rotation = npc.velocity.X * 0.02f;
            }
            else if (Timer <= CurtsyEnd) {
                //屈膝礼：轻微下沉再回正，姿态由渲染层随进度演绎
                npc.velocity = Vector2.Zero;
                float t = (Timer - SettleTime) / (float)CurtsyTime;
                float bow = (float)System.Math.Sin(t * MathHelper.Pi);
                npc.rotation = npc.spriteDirection * bow * 0.14f;

                if (Timer == SettleTime + CurtsyTime / 2) {
                    SoundEngine.PlaySound(SoundID.Item45 with { Pitch = 0.4f, Volume = 0.5f }, npc.Center);
                }
            }
            else {
                //熄灭：光度渐弱，本体淡出
                npc.velocity = Vector2.Zero;
                float t = (Timer - CurtsyEnd) / (float)FadeTime;
                npc.Opacity = 1f - t;
                Lighting.AddLight(npc.Center, new Vector3(0.9f, 0.3f, 0.2f) * (1f - t));
            }

            if (Timer >= TotalTime && !VaultUtils.isClient) {
                npc.active = false;
                npc.netUpdate = true;
            }

            return null;
        }
    }
}
