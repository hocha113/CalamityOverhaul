using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 武装阶段大招：四臂飞散四角，头-臂之间拉起带伤害判定的电弧链锁
    /// （<see cref="PrimeArcChainProj"/>），十字结构旋转收紧逼玩家穿缝，
    /// 结束时中心脉冲弹环 + 屏幕冲击波。
    /// <para>收紧半径经头部 <c>npc.ai[1]</c>（<see cref="PrimeAiSlots.HeadCommandSlot"/>）广播，
    /// 机械臂编队代码读取后同步收拢；退出时必须清零防止污染指令通道。</para>
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.TetherSpin, typeof(PrimeStateContext))]
    internal class PrimeTetherSpinState : PrimeStateBase
    {
        public override string StateName => "TetherSpin";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.TetherSpin;

        private const int Telegraph = 36;
        internal const int SpinDuration = 180;
        private const int Total = Telegraph + SpinDuration + 24;

        /// <summary>链锁初始半径（与预警环一致）</summary>
        private const float ChainRadiusStart = 300f;
        /// <summary>收紧终点半径</summary>
        private const float ChainRadiusEnd = 175f;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            //十字中心缓慢压向玩家：玩家必须持续移动穿缝，但低跟随系数保证可以拉开
            Vector2 anchor = context.Target.Center + new Vector2(0, -120);
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * 0.035f, 0.12f);

            float progress = MathHelper.Clamp((Timer - Telegraph) / (float)SpinDuration, 0f, 1f);
            //十字旋转随收紧加速
            npc.rotation += 0.02f + progress * 0.055f;

            if (Timer < Telegraph) {
                context.SetChargeState(1, Timer / (float)Telegraph);
                if (!VaultUtils.isClient && Timer == 1) {
                    PrimeTelegraphLine.SpawnRing(npc, npc.Center, ChainRadiusStart, Telegraph);
                }
            }
            else if (Timer == Telegraph) {
                if (!VaultUtils.isClient) {
                    SpawnChains(context);
                }
            }
            else if (Timer < Telegraph + SpinDuration) {
                context.SetChargeState(1, 0.4f + progress * 0.5f);
                //向机械臂广播收紧半径
                npc.ai[PrimeAiSlots.HeadCommandSlot] = MathHelper.Lerp(ChainRadiusStart, ChainRadiusEnd, progress);

                if (!VaultUtils.isServer && Timer % 9 == 0) {
                    Vector2 sparkPos = npc.Center + Main.rand.NextVector2CircularEdge(
                        npc.ai[PrimeAiSlots.HeadCommandSlot], npc.ai[PrimeAiSlots.HeadCommandSlot]);
                    Dust dust = Dust.NewDustDirect(sparkPos, 1, 1, DustID.Electric, 0, 0, 100, Color.Gold, 1.3f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - sparkPos) * 0.06f;
                }
            }
            else if (Timer == Telegraph + SpinDuration) {
                //终结脉冲：链锁同时熄灭（弹幕侧按 timeLeft 自然到期），中心炸出一圈制导弹
                npc.ai[PrimeAiSlots.HeadCommandSlot] = 0f;
                if (!VaultUtils.isClient) {
                    FirePulseRing(context);
                }
                if (!VaultUtils.isServer) {
                    PrimeScreenEffects.PushShockRing(npc.Center, 1f, 700f);
                    PrimeDeathPerformancePlayer.RequestShake(12f, 18);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.1f, Pitch = -0.5f }, npc.Center);
                }
            }

            Timer++;
            if (Timer >= Total && !VaultUtils.isClient) {
                return new PrimeCommandSequenceState();
            }
            return null;
        }

        public override void OnExit(PrimeStateContext context) {
            base.OnExit(context);
            //无论以何种路径离开（转阶段/狂怒打断），都不能把收紧半径残留在指令槽里
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = 0f;
        }

        /// <summary>对每条存活机械臂拉起一道电弧链锁（仅服务端）</summary>
        private static void SpawnChains(PrimeStateContext context) {
            NPC npc = context.Npc;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.DeathLaser));
            int[] armIndices = [CWRWorld.primeCannon, CWRWorld.primeVice, CWRWorld.primeSaw, CWRWorld.primeLaser];

            foreach (int armIndex in armIndices) {
                if (armIndex < 0 || armIndex >= Main.maxNPCs || !Main.npc[armIndex].active) {
                    continue;
                }
                Vector2 mid = (npc.Center + Main.npc[armIndex].Center) / 2f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), mid, Vector2.Zero,
                    ModContent.ProjectileType<PrimeArcChainProj>(), damage, 0f, Main.myPlayer,
                    armIndex, npc.whoAmI, SpinDuration);
            }
            npc.netUpdate = true;
        }

        /// <summary>终结脉冲：八向制导弹环（带预警线，玩家有反应余量）</summary>
        private static void FirePulseRing(PrimeStateContext context) {
            NPC npc = context.Npc;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));
            int count = context.MasterMode ? 10 : 8;

            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi / count * i;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, ang.ToRotationVector2() * 9f,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                    Main.myPlayer, npc.whoAmI, npc.target, ang);
            }
        }
    }
}
