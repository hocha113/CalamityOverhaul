using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>空降回压：足尖上提→顶点全静→贯穿俯冲→落点尖塔波+凝胶垂直羽流→优雅收势</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.CrystalDiveStomp, typeof(QueenSlimeStateContext))]
    internal class QueenCrystalDiveStompState : QueenSlimeStateBase
    {
        public override string StateName => "CrystalDiveStomp";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.CrystalDiveStomp;

        private const int RiseTime = 38;
        private const int PoiseTime = 13;
        private const int MaxDiveTime = 90;
        private const int RecoverTime = 24;

        /// <summary>0上提 1顶点定身 2俯冲 3落地收势</summary>
        private int stage;
        private int stageTimer;

        public QueenCrystalDiveStompState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
            stage = 0;
            stageTimer = 0;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            stageTimer++;

            switch (stage) {
                case 0: {//足尖上提
                    DisableContactDamage(npc);
                    Vector2 anchor = player.Center + new Vector2(player.velocity.X * 8f, -400f);
                    QueenMotion.SpringHover(npc, anchor, 0.022f, 0.12f, 28f);
                    context.PoseCommand = 1;
                    context.WingFlapBoost = 1.2f;
                    if (stageTimer >= RiseTime) {
                        stage = 1;
                        stageTimer = 0;
                    }
                    break;
                }
                case 1: {//顶点全静(威压来自静止)
                    DisableContactDamage(npc);
                    npc.velocity *= 0.72f;
                    context.PoseCommand = 3;
                    context.PushSquash(0.32f * QueenMotion.LateSnap(stageTimer / (float)PoiseTime, 5));
                    context.SetChargeState(1, stageTimer / (float)PoiseTime);
                    FaceTarget(npc, player.Center);

                    if (stageTimer == 2) {
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.7f }, npc.Center);
                    }
                    if (stageTimer >= PoiseTime) {
                        //一帧释放俯冲：预判落点
                        Vector2 aim = player.Bottom + new Vector2(player.velocity.X * 11f, 0f);
                        Vector2 dir = (aim - npc.Center).SafeNormalize(Vector2.UnitY);
                        //保证向下贯穿
                        if (dir.Y < 0.55f) {
                            dir = new Vector2(MathHelper.Clamp(dir.X, -0.8f, 0.8f), 1f).SafeNormalize(Vector2.UnitY);
                        }
                        npc.velocity = dir * (context.IsAsuraMode ? 37f : 33f);
                        if (!VaultUtils.isClient) {
                            npc.netUpdate = true;
                        }
                        context.PushSquash(0.6f);
                        SoundEngine.PlaySound(SoundID.Item160 with { Volume = 0.9f, Pitch = -0.1f }, npc.Center);
                        stage = 2;
                        stageTimer = 0;
                    }
                    break;
                }
                case 2: {//贯穿俯冲
                    EnableContactDamageIfFast(npc, 16f);
                    context.PoseCommand = 2;
                    context.AfterimageBoost = Math.Max(context.AfterimageBoost, 1f);
                    context.WingFlapBoost = 1.6f;

                    //低于玩家脚底后恢复碰撞，砸在地面
                    if (npc.Bottom.Y >= player.Top.Y) {
                        npc.noTileCollide = false;
                        npc.noGravity = false;
                    }

                    //足尖流光
                    if (!VaultUtils.isServer && stageTimer % 2 == 0) {
                        Dust d = Dust.NewDustPerfect(npc.Bottom + Main.rand.NextVector2Circular(20f, 8f),
                            DustID.TintableDust, -npc.velocity * 0.1f, 120, QueenMotion.GetQueenDustColor(), 1.6f);
                        d.noGravity = true;
                    }

                    bool grounded = npc.velocity.Y == 0f && !npc.noGravity;
                    if (grounded || stageTimer >= MaxDiveTime) {
                        if (grounded) {
                            DoImpact(context);
                        }
                        stage = 3;
                        stageTimer = 0;
                    }
                    break;
                }
                case 3: {//收势屈膝礼
                    DisableContactDamage(npc);
                    npc.velocity.X *= 0.75f;
                    if (stageTimer < 16) {
                        context.PoseCommand = 3;
                    }
                    if (stageTimer >= RecoverTime && !VaultUtils.isClient) {
                        return new QueenAerialBalletState();
                    }
                    break;
                }
            }

            return null;
        }

        /// <summary>落点：尖塔行进波+凝胶垂直羽流</summary>
        private void DoImpact(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            context.PushSquash(-0.62f);

            QueenMotion.Shake(npc.Center, 8f, 18, "QueenDiveImpact");
            QueenMotion.LandingRingFX(npc.Bottom, 1.6f, 0.4f);
            QueenMotion.GelSplashBurst(npc.Bottom, 1.6f, 12);
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 1f, Pitch = 0f }, npc.Center);

            if (VaultUtils.isClient) {
                return;
            }

            //左右各三座尖塔行进波(留出落点身侧的近身安全角)
            int spires = context.IsAsuraMode ? 4 : 3;
            for (int side = -1; side <= 1; side += 2) {
                for (int i = 0; i < spires; i++) {
                    Vector2 basePos = npc.Bottom + new Vector2(side * (150f + i * 128f), 0f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), basePos, Vector2.Zero,
                        ModContent.ProjectileType<QueenCrystalSpireProj>(), QueenCrystalSpireProj.SpireDamage, 0f, Main.myPlayer,
                        i * 7, 0f, (i + (side + 1) * 2) * 0.16f);
                }
            }

            //垂直凝胶羽流(慢速上抛回落)
            for (int i = 0; i < 5; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(9f, 13f));
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Top, vel,
                    ModContent.ProjectileType<QueenGelPearlProj>(), QueenGelPearlProj.PearlDamage, 0f, Main.myPlayer,
                    0f, 0f, i * 0.2f);
            }
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
        }
    }
}
