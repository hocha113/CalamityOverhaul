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

    /// <summary>侧翼护卫浮游：8字懒游+腕部小动作+贴脸缩手，Hub 期间弹指点射颅火；观察头部状态自转移</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronHandStateIndex.Guard, typeof(SkeletronHandContext))]
    internal class HandGuardState : SkeletronHandStateBase
    {
        public override string StateName => "HandGuard";
        public override SkeletronHandStateIndex StateIndex => SkeletronHandStateIndex.Guard;

        //弹指周期快照（周期首帧锁定；客户端凭同款时钟摆位形，权威端出弹）
        private bool flickArmed;
        private Vector2 flickAim;

        public override SkeletronHandStateBase OnUpdate(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = false;

            SpringMove(ctx, GuardAnchor(ctx), 0.14f, 0.86f, 22f);

            //玩家贴脸时本能避让（手会怕：灵动源于反应）
            if (npc.Center.Distance(ctx.Target.Center) < 150f) {
                npc.velocity += (npc.Center - ctx.Target.Center).SafeNormalize(Vector2.UnitX * ctx.Side) * 0.55f;
            }

            //Hub 期间弹指点射；手势窗内手势接管腕部；否则掌心懒散指人
            if (!UpdateFlick(ctx) && !UpdateIdleGesture(ctx)) {
                AimPalm(npc, ctx.Target.Center, 0.05f);
            }

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
                    case SkeletronStateIndex.Applause:
                        return new HandApplaudState();
                    case SkeletronStateIndex.SpinBoneStorm:
                    case SkeletronStateIndex.DayEnrage:
                        return new HandOrbitState();
                    case SkeletronStateIndex.PhaseTransition:
                        return new HandTornState();
                }
            }
            return null;
        }

        /// <summary>Hub 期间的弹指点射：卷腕拉弓→甩腕出弹→后坐收势；共享编队时钟对齐各端姿态，左右手错半拍</summary>
        private bool UpdateFlick(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            if (SkeletronHeadAI.GetStateIndex(ctx.Head) != SkeletronStateIndex.Hub
                || (int)ctx.Head.ai[SkeletronAiSlots.HeadPhase] != SkeletronPhase.Bound) {
                return false;
            }

            int period = SkeletronDirector.FlickPeriod;
            int windup = SkeletronDirector.FlickWindup;
            int phase = (int)(FormationClock(ctx) + (ctx.Side < 0 ? 0 : period / 2)) % period;

            //周期首帧锁定本轮意图：远距+视线才拉弓（缺口契约：FlickMinDistance 内不弹指，贴身是安全窗）
            if (phase == 0) {
                flickArmed = npc.Center.Distance(ctx.Target.Center) > SkeletronDirector.FlickMinDistance
                    && Collision.CanHitLine(npc.Center, 1, 1, ctx.Target.position, ctx.Target.width, ctx.Target.height);
            }
            if (!flickArmed || phase > windup + 18) {
                return false;
            }

            if (phase < windup) {
                //卷腕拉弓：掌口咬瞄，腕部向后卷，掌心幽火渐燃，末拍反向抽身
                float t = phase / (float)windup;
                flickAim = ctx.Target.Center;
                AimPalmOffset(npc, flickAim, ctx.Side * MathF.Pow(t, 1.6f) * 0.62f, 0.3f);
                ctx.PalmFlame = Math.Max(ctx.PalmFlame, 0.25f + 0.75f * t);
                if (t > 0.55f) {
                    Vector2 back = (npc.Center - flickAim).SafeNormalize(-Vector2.UnitX * ctx.Side);
                    npc.velocity += back * MathF.Pow((t - 0.55f) / 0.45f, 2f) * 1.4f;
                }
            }
            else if (phase == windup) {
                //出手帧：掌口甩正，颅火出膛（权威端），后坐+拉伸回弹
                Vector2 tipDir = (flickAim == Vector2.Zero ? ctx.Target.Center - npc.Center : flickAim - npc.Center)
                    .SafeNormalize(Vector2.UnitY);
                npc.rotation = tipDir.ToRotation() + MathHelper.PiOver2;
                ctx.TriggerSquash(-0.34f);
                ctx.SpringVelocity -= tipDir * 6.5f;
                npc.velocity -= tipDir * 6.5f;
                ctx.PalmFlame = 1f;

                if (!VaultUtils.isClient) {
                    Vector2 muzzle = npc.Center + tipDir * 30f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle,
                        tipDir * SkeletronDirector.FlickSkullSpeed(ctx.Asura),
                        ModContent.ProjectileType<SkeletronCursedSkull>(),
                        SkeletronHeadAI.GetSkullDamage(ctx.Head), 0f, Main.myPlayer, 0f, 0f);
                    npc.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.15f }, npc.Center);
                }
            }
            else {
                //收腕
                AimPalm(npc, ctx.Target.Center, 0.14f);
                ctx.PalmFlame = Math.Max(ctx.PalmFlame, 0.3f * (1f - (phase - windup) / 18f));
            }
            return true;
        }

        /// <summary>
        /// 闲置小动作轮盘：腕转→摆摆手→挥挥手 轮换（确定性时钟，各端一致）<br/>
        /// 返回 true = 本帧手势接管腕部姿态（调用方跳过懒散指人）
        /// </summary>
        private static bool UpdateIdleGesture(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            int period = 300 + npc.whoAmI * 37 % 120;
            float clock = Main.GameUpdateCount + npc.whoAmI * 71f;
            float t = clock % period;
            int kind = (int)(clock / period) % 3;

            switch (kind) {
                case 0: { //腕转：懒散指人之上叠两摆腕，掌心火苗把玩
                    if (t >= 44f) {
                        return false;
                    }
                    float w = t / 44f;
                    float env = (float)Math.Sin(w * MathHelper.Pi);
                    AimPalm(npc, ctx.Target.Center, 0.05f);
                    npc.rotation += env * (float)Math.Sin(w * MathHelper.TwoPi * 2f) * 0.2f;
                    ctx.PalmFlame = Math.Max(ctx.PalmFlame, env * 0.35f);
                    return true;
                }
                case 1: { //摆摆手：掌竖起左右慢摆两拍，身体随摆轻晃
                    if (t >= 78f) {
                        return false;
                    }
                    float w = t / 78f;
                    float env = (float)Math.Sin(w * MathHelper.Pi);
                    float pose = (float)Math.Sin(w * MathHelper.TwoPi * 2f) * 0.34f * ctx.Side;
                    npc.rotation = npc.rotation.AngleLerp(pose, 0.1f + 0.16f * env);
                    npc.velocity += Vector2.UnitX * ((float)Math.Cos(w * MathHelper.TwoPi * 2f) * 0.3f * env * ctx.Side);
                    return true;
                }
                default: { //挥挥手：起手轻抬，掌竖起快挥三拍
                    if (t >= 60f) {
                        return false;
                    }
                    float w = t / 60f;
                    float env = (float)Math.Sin(w * MathHelper.Pi);
                    float pose = (float)Math.Sin(w * MathHelper.TwoPi * 3f) * 0.5f;
                    npc.rotation = npc.rotation.AngleLerp(pose, 0.12f + 0.2f * env);
                    if (t < 10f) {
                        npc.velocity -= Vector2.UnitY * 0.45f;
                    }
                    ctx.PalmFlame = Math.Max(ctx.PalmFlame, env * 0.3f);
                    return true;
                }
            }
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

        /// <summary>蓄势：抬到玩家侧上方卷腕拉弓，提前8帧锁定砸点给读秒，末拍反向上抽</summary>
        private void UpdateWindup(SkeletronHandContext ctx, int duration) {
            NPC npc = ctx.Npc;
            float t = phaseTimer / (float)duration;
            Vector2 hover = ctx.Target.Center + new Vector2(ctx.Side * 190f, -360f);
            SpringMove(ctx, hover, 0.2f, 0.82f, 34f);
            ctx.ChainTension = t;
            ctx.PalmFlame = t;

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
            //锁定前掌随人，锁定后掌口盯死落点；蓄势全程腕部向后卷（力从蓄来的可读前摇）
            AimPalmOffset(npc, phaseTimer < lockFrame ? ctx.Target.Center : strikeAim,
                ctx.Side * MathF.Pow(t, 1.4f) * 0.55f, 0.2f);

            //锁定期落点预告（地表骨尘涌动）
            if (!VaultUtils.isServer && phaseTimer >= lockFrame && groundY > 0f && phaseTimer % 2 == 0) {
                Dust dust = Dust.NewDustDirect(new Vector2(strikeAim.X - 30f, strikeAim.Y - 8f), 60, 8, DustID.Bone,
                    Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-3f, -1f), 130, default, 1.3f);
                dust.noGravity = false;
            }

            //末拍反向上抽（力从蓄来）
            if (t > 0.72f) {
                npc.velocity -= Vector2.UnitY * MathF.Pow((t - 0.72f) / 0.28f, 2f) * 5f;
            }

            if (phaseTimer >= duration) {
                //一帧内定速：直线读得快；出手瞬间指轴拉伸（弹性形变）
                npc.velocity = (strikeAim - npc.Center).SafeNormalize(Vector2.UnitY) * SkeletronDirector.SlamSpeed(ctx.Asura);
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
                ctx.SpringVelocity = npc.velocity;
                ctx.TriggerSquash(-0.3f);

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Volume = 0.9f, Pitch = -0.55f }, npc.Center);
                }
                Advance(phase + 1);
            }
        }

        /// <summary>砸击：只在高速时带接触伤害，俯冲复利增速（越砸越狠）</summary>
        private void UpdateStrike(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;
            ctx.ChainTension = 1f;
            ctx.PalmFlame = 1f;
            npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

            if (npc.velocity.Length() < SkeletronDirector.SlamSpeed(ctx.Asura) * 1.28f) {
                npc.velocity *= 1.02f;
            }

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

        /// <summary>嵌地定格：落掌一帧回跳（重量=反作用），随后迅速嵌死</summary>
        private void UpdateEmbed(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = npc.defDamage;
            if (phaseTimer == 0 && npc.velocity.Length() < 1f) {
                npc.velocity = new Vector2(0f, -2.4f);
            }
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
                    npc.velocity = (clapAnchor - npc.Center).SafeNormalize(Vector2.UnitX * ctx.Side) * SkeletronDirector.ClapSpeed(ctx.Asura);
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
                    //撞掌反弹（质量对撞的读法），挤压回弹走冲击广播
                    npc.velocity *= -0.16f;
                    ctx.SpringVelocity = npc.velocity;
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

            float clock = FormationClock(ctx);
            float rot = clock * 0.22f + (ctx.Side < 0 ? 0f : MathHelper.Pi);
            Vector2 toPoint = ctx.Head.Center + rot.ToRotationVector2() * ctx.Head.width * 1.5f;
            //渐入编队（甩入而非贴入），随后咬死轨道
            float grip = MathHelper.Clamp(0.16f + Timer * 0.017f, 0.16f, 0.5f);
            npc.Center = Vector2.Lerp(npc.Center, toPoint, grip);
            //指尖甩向切线（离心读法），离心焰随行
            npc.rotation = rot + MathHelper.Pi;
            ctx.PalmFlame = Math.Max(ctx.PalmFlame, 0.55f);

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
