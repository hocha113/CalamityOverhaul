using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States.Hands
{
    //手部准则
    //掌根朝上，指尖指向 rotation-PiOver2
    //客户端是傀儡：位置以服务端广播为准，状态仅供本地视觉包络

    /// <summary>侧翼护卫浮游，观察头部状态自转移</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronHandStateIndex.Guard, typeof(SkeletronHandContext))]
    internal class HandGuardState : SkeletronHandStateBase
    {
        public override string StateName => "HandGuard";
        public override SkeletronHandStateIndex StateIndex => SkeletronHandStateIndex.Guard;

        public override SkeletronHandStateBase OnUpdate(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = false;

            SpringMove(ctx, GuardAnchor(ctx), 0.14f, 0.86f, 22f);
            //掌心懒散指向玩家
            AimPalm(npc, ctx.Target.Center, 0.05f);

            Timer++;

            //根据头部状态自转移（服务端决策）
            if (!VaultUtils.isClient) {
                SkeletronStateIndex headState = SkeletronHeadAI.GetStateIndex(ctx.Head);
                switch (headState) {
                    case SkeletronStateIndex.HandCrush:
                        return new HandCrushState();
                    case SkeletronStateIndex.ClapPincer:
                        return new HandClapState();
                    case SkeletronStateIndex.PalmSnatch:
                        return new HandSnatchState();
                    case SkeletronStateIndex.SpinBoneStorm:
                    case SkeletronStateIndex.DayEnrage:
                        return new HandOrbitState();
                    case SkeletronStateIndex.PhaseTransition:
                        return new HandTornState();
                }
            }
            return null;
        }
    }

    /// <summary>锁链砸击连段：左右交替两拍 + 同步合拍第三击</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronHandStateIndex.Crush, typeof(SkeletronHandContext))]
    internal class HandCrushState : SkeletronHandStateBase
    {
        public override string StateName => "HandCrush";
        public override SkeletronHandStateIndex StateIndex => SkeletronHandStateIndex.Crush;

        private int phase;          //0等待错拍 1蓄 2砸 3嵌 4收 5等待合拍 6蓄二 7砸二 8嵌二 9收尾
        private int phaseTimer;
        private Vector2 strikeAim;
        private float groundY;

        /// <summary>合拍第三击的全局起拍</summary>
        private const int SyncSlamStart = 112;

        public override void OnEnter(SkeletronHandContext ctx) {
            base.OnEnter(ctx);
            phase = 0;
            phaseTimer = 0;
        }

        public override SkeletronHandStateBase OnUpdate(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = false;

            //左手先手，右手错拍
            int waitFrames = ctx.Side < 0 ? 0 : 30;

            switch (phase) {
                case 0: //错拍等待
                    HoldGuard(ctx);
                    if (phaseTimer >= waitFrames) {
                        Advance(1);
                    }
                    break;
                case 1: //蓄势上扬
                case 6:
                    UpdateWindup(ctx, phase == 6 ? 14 : 26);
                    break;
                case 2: //俯冲砸击
                case 7:
                    UpdateStrike(ctx);
                    break;
                case 3: //嵌地
                case 8:
                    UpdateEmbed(ctx);
                    break;
                case 4: //收势回位
                    HoldGuard(ctx);
                    if (phaseTimer >= 20) {
                        Advance(5);
                    }
                    break;
                case 5: //等待合拍
                    HoldGuard(ctx);
                    ctx.PalmFlame = MathHelper.Clamp((Timer - (SyncSlamStart - 22)) / 22f, 0f, 1f);
                    if (Timer >= SyncSlamStart) {
                        Advance(6);
                    }
                    break;
                default: //收尾
                    HoldGuard(ctx);
                    if (phaseTimer >= 20 && !VaultUtils.isClient
                        && SkeletronHeadAI.GetStateIndex(ctx.Head) != SkeletronStateIndex.HandCrush) {
                        return new HandGuardState();
                    }
                    //头部状态异常提前离开时兜底
                    if (!VaultUtils.isClient && Timer > 320) {
                        return new HandGuardState();
                    }
                    break;
            }

            phaseTimer++;
            Timer++;
            return null;
        }

        private void Advance(int next) {
            phase = next;
            phaseTimer = 0;
        }

        private void HoldGuard(SkeletronHandContext ctx) {
            SpringMove(ctx, GuardAnchor(ctx), 0.14f, 0.86f, 24f);
            AimPalm(ctx.Npc, ctx.Target.Center, 0.08f);
        }

        /// <summary>蓄势：抬到玩家侧上方，提前8帧锁定砸点给读秒，末拍反向上抽</summary>
        private void UpdateWindup(SkeletronHandContext ctx, int duration) {
            NPC npc = ctx.Npc;
            Vector2 hover = ctx.Target.Center + new Vector2(ctx.Side * 190f, -360f);
            SpringMove(ctx, hover, 0.2f, 0.82f, 34f);
            ctx.ChainTension = phaseTimer / (float)duration;
            ctx.PalmFlame = phaseTimer / (float)duration;

            //提前锁定砸点（公平阀：给玩家离开落点的读秒窗口）
            int lockFrame = duration - 8;
            if (phaseTimer == lockFrame) {
                Vector2 aimPoint = ctx.Target.Center;
                groundY = SkeletronFacts.FindGroundY(ctx.Target.Bottom);
                if (groundY > 0f) {
                    aimPoint = new Vector2(ctx.Target.Center.X, groundY);
                }
                strikeAim = aimPoint;
            }
            //锁定前掌随人，锁定后掌口盯死落点
            AimPalm(npc, phaseTimer < lockFrame ? ctx.Target.Center : strikeAim, 0.2f);

            //锁定期落点预告（地表骨尘涌动）
            if (!VaultUtils.isServer && phaseTimer >= lockFrame && groundY > 0f && phaseTimer % 2 == 0) {
                Dust dust = Dust.NewDustDirect(new Vector2(strikeAim.X - 30f, strikeAim.Y - 8f), 60, 8, DustID.Bone,
                    Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-3f, -1f), 130, default, 1.3f);
                dust.noGravity = false;
            }

            //末拍反向上抽（力从蓄来）
            float t = phaseTimer / (float)duration;
            if (t > 0.72f) {
                npc.velocity -= Vector2.UnitY * MathF.Pow((t - 0.72f) / 0.28f, 2f) * 5f;
            }

            if (phaseTimer >= duration) {
                //一帧内定速：直线读得快
                npc.velocity = (strikeAim - npc.Center).SafeNormalize(Vector2.UnitY) * SkeletronDirector.SlamSpeed(ctx.Death);
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
                ctx.SpringVelocity = npc.velocity;

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Volume = 0.9f, Pitch = -0.55f }, npc.Center);
                }
                Advance(phase + 1);
            }
        }

        /// <summary>砸击：只在高速时带接触伤害</summary>
        private void UpdateStrike(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;
            ctx.ChainTension = 1f;
            npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

            //砸击拖影尘
            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                Dust dust = Dust.NewDustDirect(npc.Center, 8, 8, DustID.Bone,
                    -npc.velocity.X * 0.1f, -npc.velocity.Y * 0.1f, 120, default, 1.1f);
                dust.noGravity = true;
            }

            bool hitGround = groundY > 0f && npc.Center.Y >= groundY - 26f;
            bool hitTile = Collision.SolidCollision(npc.position, npc.width, npc.height);
            bool tooFar = phaseTimer > 26 || npc.Center.Distance(strikeAim) < 20f && groundY <= 0f;

            if (hitGround || hitTile) {
                OnImpact(ctx);
                Advance(phase + 1);
            }
            else if (tooFar) {
                //空挥：刹车泄力
                npc.velocity *= 0.72f;
                Advance(phase + 1);
            }
        }

        /// <summary>嵌地定格</summary>
        private void UpdateEmbed(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;
            npc.velocity *= 0.55f;
            ctx.ChainTension = 1f;

            if (phaseTimer >= 12) {
                npc.damage = 0;
                Advance(phase == 3 ? 4 : 9);
            }
        }

        /// <summary>落点冲击：骨刺隆起 + 冲击反馈</summary>
        private void OnImpact(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.velocity = Vector2.Zero;

            //冲击广播计数，客户端凭此播命中反馈
            npc.ai[SkeletronAiSlots.HandFree] += 1f;

            if (!VaultUtils.isClient && groundY > 0f) {
                int damage = SkeletronHeadAI.GetSkullDamage(ctx.Head);
                //骨刺自落点向两侧隆起
                for (int i = -2; i <= 2; i++) {
                    float x = npc.Center.X + i * 92f;
                    float gy = SkeletronFacts.FindGroundY(new Vector2(x, npc.Center.Y - 120f));
                    if (gy <= 0f) {
                        continue;
                    }
                    Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(x, gy), Vector2.Zero,
                        ModContent.ProjectileType<SkeletronBoneSpike>(), damage, 0f, Main.myPlayer,
                        Math.Abs(i) * 4f, 1f + Math.Abs(i) * 0.06f);
                }
            }
        }
    }

    /// <summary>双掌合拍钳杀</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronHandStateIndex.Clap, typeof(SkeletronHandContext))]
    internal class HandClapState : SkeletronHandStateBase
    {
        public override string StateName => "HandClap";
        public override SkeletronHandStateIndex StateIndex => SkeletronHandStateIndex.Clap;

        private Vector2 clapAnchor;
        private bool anchorLatched;
        private bool arrived;
        private bool burstDone;

        internal const int FlankEnd = 22;
        internal const int SnapFrame = 58;

        public override void OnEnter(SkeletronHandContext ctx) {
            base.OnEnter(ctx);
            anchorLatched = false;
            arrived = false;
            burstDone = false;
        }

        public override SkeletronHandStateBase OnUpdate(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;

            if (Timer < FlankEnd) {
                //甩到玩家两侧同高
                Vector2 flank = ctx.Target.Center + new Vector2(ctx.Side * 470f, 0f);
                SpringMove(ctx, flank, 0.24f, 0.8f, 40f);
                AimPalm(npc, ctx.Target.Center, 0.2f);
            }
            else if (Timer < SnapFrame) {
                //锁位预告：掌心幽火拉满，末6帧颤抖
                if (!anchorLatched) {
                    anchorLatched = true;
                    clapAnchor = ctx.Target.Center;
                }
                Vector2 hold = clapAnchor + new Vector2(ctx.Side * 470f, 0f);
                SpringMove(ctx, hold, 0.3f, 0.74f, 40f);
                AimPalm(npc, clapAnchor, 0.3f);
                ctx.ChainTension = (Timer - FlankEnd) / (float)(SnapFrame - FlankEnd);
                ctx.PalmFlame = ctx.ChainTension;
                if (Timer > SnapFrame - 6) {
                    npc.velocity += Main.rand.NextVector2Circular(1.6f, 1.6f);
                }
            }
            else if (!arrived) {
                //合拢
                if (Timer == SnapFrame) {
                    npc.velocity = (clapAnchor - npc.Center).SafeNormalize(Vector2.UnitX * ctx.Side) * SkeletronDirector.ClapSpeed(ctx.Death);
                    npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
                    ctx.SpringVelocity = npc.velocity;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1f, Pitch = -0.35f }, npc.Center);
                    }
                }
                npc.damage = npc.defDamage;
                ctx.ChainTension = 1f;

                bool reached = ctx.Side < 0 ? npc.Center.X >= clapAnchor.X - 34f : npc.Center.X <= clapAnchor.X + 34f;
                if (reached || Timer > SnapFrame + 26) {
                    arrived = true;
                    npc.velocity *= 0.1f;
                    npc.ai[SkeletronAiSlots.HandFree] += 1f;

                    //合拍中心迸发（由左手负责，防止双份）
                    if (!burstDone && ctx.Side < 0 && !VaultUtils.isClient) {
                        burstDone = true;
                        int damage = SkeletronHeadAI.GetSkullDamage(ctx.Head);
                        for (int i = 0; i < 6; i++) {
                            //背向双掌轴线的六向骨屑弹
                            float ang = MathHelper.TwoPi * i / 6f + MathHelper.PiOver2 * 0.5f;
                            Vector2 vel = ang.ToRotationVector2() * 4.6f;
                            //纵向分量放大，横向被掌挡住的读法
                            vel.Y *= 1.35f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), clapAnchor, vel,
                                ModContent.ProjectileType<SkeletronBoneShard>(), damage, 0f, Main.myPlayer, 0.012f, 0f);
                        }
                    }
                }
            }
            else {
                //收势
                npc.damage = 0;
                npc.velocity *= 0.86f;
                SpringMove(ctx, GuardAnchor(ctx), 0.1f, 0.88f, 26f);
                if (!VaultUtils.isClient && Timer > SnapFrame + 60
                    && SkeletronHeadAI.GetStateIndex(ctx.Head) != SkeletronStateIndex.ClapPincer) {
                    return new HandGuardState();
                }
                if (!VaultUtils.isClient && Timer > 300) {
                    return new HandGuardState();
                }
            }

            Timer++;
            return null;
        }
    }

    /// <summary>旋杀紧缩环绕（读作旋转质量的一部分）</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronHandStateIndex.Orbit, typeof(SkeletronHandContext))]
    internal class HandOrbitState : SkeletronHandStateBase
    {
        public override string StateName => "HandOrbit";
        public override SkeletronHandStateIndex StateIndex => SkeletronHandStateIndex.Orbit;

        public override SkeletronHandStateBase OnUpdate(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
            ctx.ChainTension = 1f;

            //槽位复用等异常取不到覆写时保持null，下一行已有回退时钟（精确索引缺键会抛出）
            ctx.Head.TryGetOverride(out SkeletronHeadAI headOverride);
            float clock = headOverride?.ai[SkeletronAiSlots.OverrideOrbitClock] ?? Main.GameUpdateCount;
            float rot = clock * 0.22f + (ctx.Side < 0 ? 0f : MathHelper.Pi);
            Vector2 toPoint = ctx.Head.Center + rot.ToRotationVector2() * ctx.Head.width * 1.5f;
            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.5f);
            //指尖甩向切线（离心读法）
            npc.rotation = rot + MathHelper.Pi;

            Timer++;
            if (!VaultUtils.isClient) {
                SkeletronStateIndex headState = SkeletronHeadAI.GetStateIndex(ctx.Head);
                if (headState is not SkeletronStateIndex.SpinBoneStorm and not SkeletronStateIndex.DayEnrage) {
                    return new HandGuardState();
                }
            }
            return null;
        }

        public override void OnExit(SkeletronHandContext ctx) {
            base.OnExit(ctx);
            ctx.Npc.dontTakeDamage = false;
        }
    }

    /// <summary>断手狂化：被头颅锁链绞回、痉挛、殉解</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronHandStateIndex.Torn, typeof(SkeletronHandContext))]
    internal class HandTornState : SkeletronHandStateBase
    {
        public override string StateName => "HandTorn";
        public override SkeletronHandStateIndex StateIndex => SkeletronHandStateIndex.Torn;

        public override SkeletronHandStateBase OnUpdate(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;
            ctx.ChainTension = 1f;

            //绞回头侧
            Vector2 toPoint = ctx.Head.Center + new Vector2(ctx.Side * 120f, -30f);
            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.2f);
            npc.velocity = Vector2.Zero;
            //痉挛
            npc.Center += Main.rand.NextVector2Circular(1.8f, 1.8f) * MathHelper.Clamp(Timer / 30f, 0f, 1f);
            AimPalm(npc, ctx.Head.Center, 0.2f);

            //痉挛期渗出幽火
            if (!VaultUtils.isServer && Timer % 4 == 0) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                    npc.Center + Main.rand.NextVector2Circular(20f, 20f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.8f, 2.2f),
                    SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.2f, 2f))?.Configure(Main.rand.Next(24, 40));
            }

            Timer++;

            //左手先殉，右手后殉（拍点与头部转阶段演出对齐）
            int tornFrame = ctx.Side < 0 ? 46 : 76;
            if (Timer >= tornFrame && !VaultUtils.isClient) {
                npc.life = 0;
                npc.HitEffect();
                npc.active = false;
                npc.netUpdate = true;
            }
            return null;
        }
    }
}
