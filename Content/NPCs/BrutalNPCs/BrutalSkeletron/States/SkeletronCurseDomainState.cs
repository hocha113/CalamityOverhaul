using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>诅咒黑暗领域·猎杀（二阶段签名）：视界压缩，诅咒烛环收拢，
    /// 黑暗中只剩眼火与冠火——头颅三次自环缘凝形，沿预警线直贯扑杀</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.CurseDomain, typeof(SkeletronStateContext))]
    internal class SkeletronCurseDomainState : SkeletronStateBase
    {
        public override string StateName => "CurseDomain";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.CurseDomain;

        internal const int RingSpawn = 34;
        internal const int RingLife = SkeletronCurseWisp.OrbitFrames;   //与烛灵寿命对齐
        internal const int Duration = RingSpawn + RingLife + 26;

        /// <summary>缺口（契约3）：烛环每 RingGapStride 槽永空一位——三条固定穿行走廊，布环循环直接读取</summary>
        private const int RingGapStride = 4;

        //猎杀节拍：散形→环缘凝形（预警线读秒）→直贯扑杀→刹车
        private const int HuntCycle = 80;
        private const int HuntDissolveEnd = 14;
        private const int HuntCondenseEnd = 30;
        private const int HuntDashEnd = 58;
        /// <summary>凝形环缘半径（扑杀出发距离，锁定后不再追踪）</summary>
        private const float HuntRingRadius = 470f;

        private Vector2 anchor;
        private bool anchorSet;

        public override void OnEnter(SkeletronStateContext context) {
            base.OnEnter(context);
            anchorSet = false;
        }

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;

            //视界压缩
            float domain = MathHelper.Clamp(Timer / 46f, 0f, 0.88f);
            if (Timer > RingSpawn + RingLife) {
                domain = MathHelper.Lerp(0.88f, 0f, (Timer - RingSpawn - RingLife) / (float)(Duration - RingSpawn - RingLife));
            }
            SkeletronScreenEffects.RequestDomain(domain);

            if (Timer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.9f, Pitch = -0.8f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.7f, Pitch = -0.9f }, npc.Center);
            }

            //布环（服务端）
            if (Timer == RingSpawn) {
                anchor = context.Target.Center;
                anchorSet = true;
                if (!VaultUtils.isClient) {
                    int damage = SkullDamage(context);
                    int slots = 13;
                    float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int i = 0; i < slots; i++) {
                        //周期缺口给穿行留路
                        if (i % RingGapStride == RingGapStride - 1) {
                            continue;
                        }
                        float angle = baseAngle + MathHelper.TwoPi * i / slots;
                        Vector2 pos = anchor + angle.ToRotationVector2() * SkeletronCurseWisp.StartRadius;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                            ModContent.ProjectileType<SkeletronCurseWisp>(), damage, 0f, Main.myPlayer,
                            angle, anchor.X, anchor.Y);
                    }
                    npc.netUpdate = true;
                }
            }

            UpdateStalk(context, npc);

            Timer++;
            if (Timer >= Duration && !VaultUtils.isClient) {
                npc.alpha = 0;
                return new SkeletronHubState();
            }
            return null;
        }

        /// <summary>黑暗猎杀：散形→环缘凝形（黑暗里只亮眼火+预警线）→直贯扑杀→刹车，共三轮</summary>
        private void UpdateStalk(SkeletronStateContext context, NPC npc) {
            if (Timer < RingSpawn) {
                //先撤到高位
                Vector2 rise = context.Target.Center + new Vector2(0f, -420f);
                npc.velocity = (rise - npc.Center) * 0.04f;
                SettleRotation(npc, 0.2f);
                return;
            }
            if (Timer > RingSpawn + RingLife) {
                //领域消散段：缓浮回稳
                npc.damage = npc.defDamage;
                npc.alpha = 0;
                npc.velocity *= 0.9f;
                SettleRotation(npc, 0.12f);
                context.EyeFlame = MathHelper.Lerp(context.EyeFlame, 1f, 0.08f);
                return;
            }

            int stalkPhase = (Timer - RingSpawn) % HuntCycle;

            if (stalkPhase < HuntDissolveEnd) {
                //散形没入黑暗
                npc.damage = 0;
                npc.velocity *= 0.85f;
                npc.alpha = (int)MathHelper.Lerp(0f, 255f, stalkPhase / (float)HuntDissolveEnd);
                context.EyeFlame = 1f - stalkPhase / (float)HuntDissolveEnd;
                if (!VaultUtils.isServer && stalkPhase % 3 == 0) {
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(npc.Center + Main.rand.NextVector2Circular(36f, 36f),
                        Main.rand.NextVector2CircularEdge(3f, 3f),
                        SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1.3f, 2f))?.Configure(Main.rand.Next(18, 30));
                }
                if (stalkPhase == HuntDissolveEnd - 1 && !VaultUtils.isClient && anchorSet) {
                    //瞬移到环缘随机方位，扑杀角当场锁死（此后不追踪）
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    npc.Center = anchor + a.ToRotationVector2() * HuntRingRadius;
                    npc.velocity = Vector2.Zero;
                    npc.ai[SkeletronAiSlots.HeadParamB] = (context.Target.Center - npc.Center).ToRotation();
                    npc.netUpdate = true;
                }
            }
            else if (stalkPhase < HuntCondenseEnd) {
                //凝形读秒：黑暗里浮出眼火与沿锁死角的预警线
                npc.damage = 0;
                npc.velocity = Vector2.Zero;
                float t = (stalkPhase - HuntDissolveEnd) / (float)(HuntCondenseEnd - HuntDissolveEnd);
                npc.alpha = (int)MathHelper.Lerp(255f, 0f, t);
                context.EyeFlame = t * 1.5f;
                context.DashTelegraph = t;
                npc.rotation = npc.rotation.AngleLerp(0f, 0.2f);
            }
            else if (stalkPhase < HuntDashEnd) {
                //直贯扑杀：锁死角一往无前，旋杀之躯
                if (stalkPhase == HuntCondenseEnd) {
                    npc.velocity = npc.ai[SkeletronAiSlots.HeadParamB].ToRotationVector2()
                        * (context.DeathMode ? 26f : 23.5f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 1f, Pitch = -0.45f }, npc.Center);
                        SkeletronScreenEffects.PushShake(npc.Center, 5f);
                    }
                }
                npc.alpha = 0;
                npc.damage = (int)(npc.defDamage * SkeletronDirector.SpinDamageMult);
                SpinRotation(npc, 0.34f);
                context.SpinVortex = 0.8f;
                context.EyeFlame = 1.5f;
            }
            else {
                //刹车定格
                npc.damage = npc.defDamage;
                npc.velocity *= 0.82f;
                context.SpinVortex *= 0.85f;
                SettleRotation(npc, 0.1f);
                context.EyeFlame = MathHelper.Lerp(context.EyeFlame, 1.1f, 0.08f);
            }
        }

        public override void OnExit(SkeletronStateContext context) {
            base.OnExit(context);
            context.Npc.alpha = 0;
        }
    }
}
