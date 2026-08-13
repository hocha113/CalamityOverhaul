using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 过载熔断连接段（20% 一次性）：警报音逐拍加急、焊缝热光拉满、
    /// 全身火星喷涌——之后攻速解锁、每次出招后自落零件、头锤升级三连摆
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.OverloadConnector, typeof(ScrapStateContext))]
    internal class ScrapOverloadConnectorState : ScrapStateBase
    {
        public override string StateName => "OverloadConnector";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.OverloadConnector;

        private const int StateEnd = 50;

        private bool roared;
        /// <summary>警报拍计数（间隔逐拍缩短）</summary>
        private int alarmCount;
        private int nextAlarm = 4;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            npc.velocity *= 0.9f;
            ctx.WeldHeat = MathHelper.Clamp(t / (float)StateEnd, 0f, 1f);
            ctx.EyeScan = (t % 8) / 8f;

            //警报加急：间隔 12→6 逐拍缩短，音高逐拍抬升
            if (t >= nextAlarm) {
                alarmCount++;
                nextAlarm = t + Math.Max(12 - alarmCount, 6);
                SoundEngine.PlaySound(SoundID.Item15 with {
                    Volume = 0.4f,
                    Pitch = -0.4f + alarmCount * 0.12f,
                    MaxInstances = 2
                }, npc.Center);
            }

            //全身火星喷涌
            if (!Main.dedServ && t % 3 == 0) {
                int arm = Main.rand.Next(ScrapCommander.ArmCount);
                PRTLoader.NewParticle<PRT_Spark>(
                    owner.ShoulderWorld(arm) + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(1f, 4f)),
                    Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(10, 16));
            }

            if (t == 30 && !roared) {
                roared = true;
                ctx.Phase = 3;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.85f, Pitch = -0.15f, MaxInstances = 1 }, npc.Center);
                ShakeNearby(npc.Center, 3f);
                //四臂猛地绷正：过载的那口劲
                for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                    owner.ImpulseArm(i, (owner.RestTarget(i) - owner.GetArmPos(i)) * 0.2f + new Vector2(0f, -2.2f));
                }
                owner.TautVibe = 12;
            }

            Timer++;
            if (t >= StateEnd) {
                ctx.AttackCooldown = 25;
                if (!VaultUtils.isClient) {
                    return new ScrapHubState();
                }
            }
            return null;
        }
    }
}
