using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 悬停压场连接段：侧翼弹簧悬停+预判血珠点射，计满即从洗牌袋抽下一招<br/>
    /// 兼作攻击间歇的呼吸拍，restFrames 控制时长
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.VeilHover, typeof(EocStateContext))]
    internal class EocVeilHoverState : EocStateBase
    {
        public override string StateName => "EocVeilHover";
        public override EocStateIndex StateIndex => EocStateIndex.VeilHover;

        private readonly int restFrames;
        private int shootTimer;

        public EocVeilHoverState() : this(52) {
        }

        public EocVeilHoverState(int restFrames) {
            this.restFrames = restFrames;
        }

        private int ShootRate => Context.IsDeathMode ? 26 : 34;
        private int MinRest => restFrames;
        private int MaxDuration => restFrames + (Context.IsDeathMode ? 54 : 76);

        private EocStateContext Context;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            shootTimer = 0;
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            DisableContactDamage(npc);

            //侧上方弹簧悬停+呼吸浮动
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hoverTarget = player.Center + new Vector2(side * 350f, -270f)
                + EocMotion.BreathingOffset(seed: 2.3f);
            EocMotion.SpringHover(npc, hoverTarget, 0.014f, 0.085f, context.IsSecondPhase ? 30f : 24f);
            FaceTarget(npc, player.Center, 0.24f);

            //低雾常驻压迫感，二阶段更浓
            EocScreenFX.PushVignette(context.IsSecondPhase ? 0.2f : 0.12f);

            Timer++;
            shootTimer++;

            //休息拍结束后开始点射
            if (Timer > MinRest && shootTimer >= ShootRate) {
                shootTimer = 0;
                bool canShoot = Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)
                    && npc.Distance(player.Center) > 220f;
                if (canShoot) {
                    float shootSpeed = context.IsDeathMode ? 13.5f : 11.5f;
                    Vector2 predicted = EocMotion.PredictTarget(player, npc.Center, shootSpeed, 0.5f);
                    Vector2 dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);

                    if (!VaultUtils.isClient) {
                        //三连微散射
                        for (int i = -1; i <= 1; i++) {
                            Vector2 vel = dir.RotatedBy(i * 0.09f) * shootSpeed;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + dir * 46f, vel,
                                ModContent.ProjectileType<EocBloodShot>(), 9, 0f, Main.myPlayer, 0f);
                        }
                    }

                    //开火后坐与飞沫
                    npc.velocity -= dir * 5.5f;
                    context.PushIris(0.6f, EocMotion.Arterial);
                    EocMotion.BloodSpray(npc.Center + dir * 40f, dir, 5, 7f, 0.4f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.6f, Pitch = 0.2f }, npc.Center);
                    }
                }
            }

            //决策仅权威端
            if (VaultUtils.isClient) {
                return null;
            }

            if (Timer >= MaxDuration) {
                return CreateAttackState(context.NextAttack());
            }

            return null;
        }

        /// <summary>按索引实例化攻击状态</summary>
        internal static IEocState CreateAttackState(EocStateIndex index) {
            return index switch {
                EocStateIndex.FeintDash => new EocFeintDashState(),
                EocStateIndex.FogAmbush => new EocFogAmbushState(),
                EocStateIndex.ServantLance => new EocServantLanceState(),
                EocStateIndex.ServantEncircle => new EocServantEncircleState(),
                EocStateIndex.BloodFountain => new EocBloodFountainState(),
                EocStateIndex.MawFrenzy => new EocMawFrenzyState(),
                EocStateIndex.BlindsideCross => new EocBlindsideCrossState(),
                EocStateIndex.Maelstrom => new EocMaelstromState(),
                _ => new EocFeintDashState(),
            };
        }
    }
}
