using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 登场演出：以 1 点生命自玩家脚下深处升起，悬至高空注能回血，
    /// 嗡鸣声中再生四条机械臂后正式参战。
    /// <para>位置推进全程两端确定性 Lerp，不依赖 netUpdate 强同步；
    /// 生成类副作用（机械臂）仅服务端执行。</para>
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.Intro, typeof(PrimeStateContext))]
    internal class PrimeIntroState : PrimeStateBase
    {
        public override string StateName => "Intro";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.Intro;

        internal static int RiseEnd => 60;          //自下方升起
        internal static int HealStart => 90;        //开始注能回血
        internal static int ArmSpawnTick => 180;    //再生机械臂
        internal static int IntroEnd => 220;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            if (Timer == 0) {
                npc.ai[PrimeAiSlots.HeadPhase] = PrimePhase.Intro;
                npc.life = 1;
                npc.Center = target.Center + new Vector2(0, 1200);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;//仅首次定位同步一次精确坐标
                }
            }

            npc.damage = 0;
            npc.dontTakeDamage = true;
            LeanTowards(npc, target.Center);
            context.FrameMode = 0;

            Vector2 toPoint;
            if (Timer < RiseEnd) {
                toPoint = target.Center + new Vector2(0, 500);
            }
            else {
                toPoint = target.Center + new Vector2(0, -500);

                if (Timer == HealStart && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                }
                if (Timer > HealStart) {
                    //注能回血窗口挂出充能状态：登场演出叠加汇聚涡（仅视觉，热感滤镜在 Intro 期不生效）
                    context.SetChargeState(3, MathHelper.Clamp(
                        (Timer - HealStart) / (float)(IntroEnd - HealStart), 0f, 1f));
                    int addNum = (int)(npc.lifeMax / 80f);
                    if (npc.life >= npc.lifeMax) {
                        npc.life = npc.lifeMax;
                    }
                    else {
                        Lighting.AddLight(npc.Center, Color.White.ToVector3());
                        npc.life += addNum;
                        if (Timer % 4 == 0) {
                            CombatText.NewText(npc.Hitbox, CombatText.HealLife, addNum);
                        }
                    }
                }
            }

            if (Timer == ArmSpawnTick - 8 && !VaultUtils.isServer) {
                context.Owner.SpawnHouengEffect();
                SoundEngine.PlaySound(CWRSound.SpawnArmMgs, Main.LocalPlayer.Center);
            }
            if (Timer == ArmSpawnTick && !VaultUtils.isClient) {
                context.Owner.SpawnArm();
            }

            //以速度表达升空Lerp(等效 Center=Lerp(Center,toPoint,0.065))，让客户端按同步速度平滑外推而非每帧追本地玩家
            npc.velocity = (toPoint - npc.Center) * 0.065f;

            Timer++;
            if (Timer > IntroEnd) {
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                npc.ai[PrimeAiSlots.HeadPhase] = PrimePhase.Armed;
                if (!VaultUtils.isClient) {
                    return new PrimeCommandSequenceState();
                }
            }
            return null;
        }
    }
}
