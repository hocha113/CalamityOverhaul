using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 投技·整体吞没：双段深蹲蓄势→超级砸落→落点正压命中把玩家裹进凝胶体内→
    /// 带人弹跳三次消化挤压→高压深蹲蓄压→把玩家喷出→脱力恢复。
    /// 空振则陷入更长的硬直惩罚窗。P2解锁，连接拍注入，长冷却
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.Engulf, typeof(KingSlimeStateContext))]
    internal class KingSlimeEngulfState : KingSlimeStateBase
    {
        public override string StateName => "Engulf";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.Engulf;

        #region 节拍与同步槽约定
        /// <summary>蓄势帧数(公平阀：≥40可读前摇)</summary>
        internal const int ChargeTime = 46;
        /// <summary>消化弹跳次数=挤压拍数</summary>
        internal const int DigestBounces = 3;
        /// <summary>高压蓄压帧数</summary>
        internal const int PressureTime = 34;
        /// <summary>命中后脱力恢复帧数</summary>
        internal const int RecoverTime = 42;
        /// <summary>空振硬直惩罚帧数(比命中恢复更长，奖励躲避方)</summary>
        internal const int WhiffTime = 54;
        /// <summary>出手一次(无论命中)压满的冷却</summary>
        internal const int CooldownAfterUse = 1500;
        /// <summary>正压判定区高度(px，自底部向上)</summary>
        private const float PressZoneHeight = 116f;

        //重制ai槽约定(服务端写，随npc.netUpdate搭车同步)：
        //ai[6]=被吞玩家whoAmI+1(0无) ai[7]=抓取相位(0无 1消化 2高压 3已喷出) ai[8]=挤压拍计数
        internal const int SlotVictim = 6;
        internal const int SlotGrabPhase = 7;
        internal const int SlotSqueeze = 8;
        #endregion

        /// <summary>0蓄势 1腾空砸落 2消化弹跳 3高压 4恢复 5空振惩罚</summary>
        private int phase;
        private int phaseTimer;
        /// <summary>已完成的消化弹跳落地数(服务端推进)</summary>
        private int bouncesDone;
        /// <summary>本端已放过特效的挤压拍序号，防同拍重放</summary>
        private int fxBeatPlayed;
        /// <summary>本端已放过喷出帧特效</summary>
        private bool ejectFxPlayed;
        /// <summary>消化期弹跳的水平摆向</summary>
        private int bounceDir;
        /// <summary>落地后等待抓取判定包的本地计帧(客户端)</summary>
        private int landResolveTimer;
        /// <summary>本端已放过落地冲击特效(慢端由相位自愈补放)</summary>
        private bool slamFxPlayed;
        /// <summary>本端见过持人相位(客户端区分"断投释放"与"从未抓到")</summary>
        private bool sawHold;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
            bouncesDone = 0;
            fxBeatPlayed = 0;
            ejectFxPlayed = false;
            bounceDir = 1;
            landResolveTimer = 0;
            slamFxPlayed = false;

            sawHold = false;

            //中途加入的客户端：服务端已在持人阶段，直接快进到对应相位，防止重播前摇与旧节拍
            if (VaultUtils.isClient && context.Host != null) {
                int netPhase = (int)context.Host.ai[SlotGrabPhase];
                if (netPhase == 1) {
                    phase = 2;
                    sawHold = true;
                }
                else if (netPhase >= 2) {
                    phase = 3;
                    sawHold = true;
                    //喷出已发生的不再补放喷出特效
                    ejectFxPlayed = netPhase == 3;
                }
                fxBeatPlayed = (int)context.Host.ai[SlotSqueeze];
            }
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            KingSlimeAI host = context.Host;
            Timer++;
            phaseTimer++;

            if (host == null) {
                //异常：宿主缺失，直接收招
                return VaultUtils.isClient ? null : BackToHop(context);
            }

            //全程接触伤害关闭：命中=吞没剧本伤，躲开=完全无伤+反打窗口
            context.ContactDamageScale = 0f;

            //客户端相位自愈：以同步的抓取相位为准前向修正
            if (VaultUtils.isClient) {
                SyncClientPhase(context, host);
            }

            //挤压拍特效：各端按同步计数各放一次(只前向，防回卷重放)
            int beat = (int)host.ai[SlotSqueeze];
            if (beat > fxBeatPlayed && phase >= 2) {
                fxBeatPlayed = beat;
                SqueezeBeatFX(context);
            }

            //喷出帧特效：各端观察相位3的首帧
            if (!ejectFxPlayed && (int)host.ai[SlotGrabPhase] == 3) {
                ejectFxPlayed = true;
                EjectFX(context);
            }

            switch (phase) {
                case 0: UpdateCharge(context); break;
                case 1: UpdateAirborne(context); break;
                case 2: UpdateDigest(context); break;
                case 3: UpdatePressure(context); break;
                case 4: {
                    IKingSlimeState next = UpdateRecover(context);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
                case 5: {
                    IKingSlimeState next = UpdateWhiff(context);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            //持人期目标校验：死亡/离场/被传送出安全距离→立即断投(服务端)
            if (!VaultUtils.isClient && phase is 2 or 3) {
                Player victim = ResolveVictim(host);
                if (victim == null || !victim.Alives() || victim.Distance(npc.Center) > 700f) {
                    AbortGrab(context);
                }
            }

            //看门狗：任何异常卡死都强制释放收招
            if (Timer > 560 && !VaultUtils.isClient) {
                AbortGrab(context);
                return BackToHop(context);
            }

            return null;
        }

        #region 幕推进

        /// <summary>幕0 蓄势：先鼓胀吸气再深压蓄满，全场最深的蹲缩(与普通跳/迫击炮区分)</summary>
        private void UpdateCharge(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.X *= 0.7f;
            npc.direction = npc.spriteDirection = DirToTarget(context);

            float t = phaseTimer / (float)ChargeTime;
            if (phaseTimer <= 18) {
                //吸气鼓胀
                float swell = phaseTimer / 18f;
                context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f + 0.16f * swell, 0.3f);
            }
            else {
                //深压：pow末段猛缩到全招式最低点
                float press = (phaseTimer - 18) / (float)(ChargeTime - 18);
                context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f - 0.6f * MathF.Pow(press, 2.4f), 0.42f);
            }
            context.AuraMode = 1;
            context.AuraProgress = t;

            //专属前摇音画：金冠鸣响起手，低吼渐起，体表汇聚珠
            if (phaseTimer == 2) {
                KingSlimeGelFX.CrownChime(npc.Top, -0.3f, 1f);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 0.85f }, npc.Center);
            }
            if (phaseTimer == ChargeTime - 10) {
                SoundEngine.PlaySound(SoundID.Item95 with { Pitch = -0.5f, Volume = 0.85f, MaxInstances = 3 }, npc.Center);
            }
            if (!VaultUtils.isServer) {
                if (phaseTimer % 3 == 0) {
                    KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.5f, 2);
                }
                //汇聚珠：四周凝胶被吸向体心(吞噬意象的预告)
                if (phaseTimer > 14 && phaseTimer % 2 == 0 && KingSlimeGelFX.OnScreen(npc.Center)) {
                    Vector2 from = npc.Center + Main.rand.NextVector2CircularEdge(150f, 90f);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGelBead>(from, (npc.Center - from) * 0.08f,
                        KingSlimeGelFX.GelMid * 0.75f, Main.rand.NextFloat(0.5f, 1f))?.Configure(16, 0.05f, 0.99f);
                }
            }

            if (phaseTimer >= ChargeTime) {
                Player player = context.Target;
                if (!player.Alives()) {
                    //目标失效：不出手，直接回连接器
                    if (!VaultUtils.isClient) {
                        phase = 4;
                        phaseTimer = 0;
                    }
                    return;
                }
                LaunchSuperSlam(context);
                phase = 1;
                phaseTimer = 0;
            }
        }

        /// <summary>超级砸落起跳：高抛+提前量锁定，空中几乎不再修正(公平阀：落点在起跳即定)</summary>
        private void LaunchSuperSlam(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            float dx = player.Center.X + player.velocity.X * 26f - npc.Center.X;
            float vx = MathHelper.Clamp(dx / 46f, -14.5f, 14.5f);
            float vy = -15.5f;
            //目标在上方补跳高
            float dy = player.Center.Y - npc.Center.Y;
            if (dy < -120f) {
                vy -= MathHelper.Clamp(-dy * 0.012f, 0f, 5f);
            }
            LaunchHop(npc, vx, vy);
            context.StretchImpulse(0.55f);
            KingSlimeGelFX.SquishSound(npc.Bottom, -0.4f, 1f);
            SoundEngine.PlaySound(SoundID.QueenSlime with { Pitch = -0.5f, Volume = 0.9f, MaxInstances = 2 }, npc.Center);
            KingSlimeGelFX.CameraPunch(npc.Bottom, 4f, 10, "BKSEngulfJump", -Vector2.UnitY);
        }

        /// <summary>幕1 腾空砸落：下坠追加重力更快更狠，落地帧做正压判定</summary>
        private void UpdateAirborne(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //空中转向极弱：路线在起跳时已承诺
            if (player.Alives()) {
                npc.velocity.X += MathHelper.Clamp((player.Center.X - npc.Center.X) * 0.0003f, -0.03f, 0.03f);
            }
            //下坠段追加重力：超级砸落比普通跳更沉
            if (npc.velocity.Y > 0f) {
                npc.velocity.Y += 0.32f;
                context.AuraMode = 3;
                context.AuraProgress = 1f;
                context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1.3f, 0.2f);

                //落点预告：沿当前弹道在地表画尘柱(纯本地表现)
                if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                    Vector2 ground = KingSlimeGelFX.FindGroundBelow(npc.Center + new Vector2(npc.velocity.X * 10f, 0f), 60);
                    if (KingSlimeGelFX.OnScreen(ground)) {
                        for (int i = 0; i < 3; i++) {
                            Dust d = Dust.NewDustDirect(ground - new Vector2(npc.width * 0.4f, 6f),
                                (int)(npc.width * 0.8f), 8, DustID.TintableDust, 0, 0, 130,
                                KingSlimeGelFX.DustBlue, Main.rand.NextFloat(1f, 1.7f));
                            d.noGravity = true;
                            d.velocity = new Vector2(0f, -Main.rand.NextFloat(1.5f, 3.5f));
                        }
                    }
                }
            }
            //上升段滴胶尾迹
            else if (!VaultUtils.isServer && phaseTimer % 4 == 0 && KingSlimeGelFX.OnScreen(npc.Center)) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGelBead>(
                    npc.Bottom + new Vector2(Main.rand.NextFloat(-0.3f, 0.3f) * npc.width, 0f),
                    new Vector2(0f, Main.rand.NextFloat(1f, 2.5f)),
                    KingSlimeGelFX.GelMid * 0.7f, Main.rand.NextFloat(0.5f, 1f))?.Configure(18);
            }

            //落地帧：大冲击视觉(命中与否同享)，服务端做正压判定
            if (context.JustLanded || (phaseTimer > 16 && Grounded(npc))) {
                SlamImpactFX(context);
                if (!VaultUtils.isClient) {
                    Player caught = FindPressedPlayer(npc);
                    if (caught != null) {
                        BeginGrab(context, caught);
                        phase = 2;
                    }
                    else {
                        phase = 5;
                    }
                    phaseTimer = 0;
                }
                else {
                    //客户端：等抓取判定包到达再定相位(SyncClientPhase接管)，超时按空振走
                    landResolveTimer = 0;
                    phase = 9;
                    phaseTimer = 0;
                }
            }

            //保险：长时间未落地(被击入深坑等)交给看门狗，这里只防负相位
            if (phaseTimer > 240 && !VaultUtils.isClient) {
                phase = 5;
                phaseTimer = 0;
            }
        }

        /// <summary>落地大冲击：命中与空振同享的演出成本</summary>
        private void SlamImpactFX(KingSlimeStateContext context) {
            if (slamFxPlayed) {
                return;
            }
            slamFxPlayed = true;
            NPC npc = context.Npc;
            context.SquashVelocity -= 0.28f;
            KingSlimeGelFX.ThudSound(npc.Bottom, 26f);
            KingSlimeGelFX.CameraPunch(npc.Bottom, 9f, 18, "BKSEngulfSlam", Vector2.UnitY);
            if (!VaultUtils.isServer) {
                KingSlimeGelFX.LandingBurst(npc.Bottom, 22f, 1.6f);
            }
            if (!VaultUtils.isClient) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom, Vector2.Zero,
                    ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Main.myPlayer, 2f);
            }
        }

        /// <summary>正压判定(服务端)：落点脚下矩形内的第一个存活玩家被吞</summary>
        private static Player FindPressedPlayer(NPC npc) {
            Rectangle pressZone = new Rectangle(
                (int)(npc.Center.X - npc.width * 0.5f),
                (int)(npc.Bottom.Y - PressZoneHeight),
                npc.width,
                (int)PressZoneHeight + 12);
            foreach (Player player in Main.ActivePlayers) {
                if (player.Alives() && !player.ghost && player.Hitbox.Intersects(pressZone)) {
                    return player;
                }
            }
            return null;
        }

        /// <summary>命中开吞(服务端)：写同步槽，吞没音画</summary>
        private void BeginGrab(KingSlimeStateContext context, Player victim) {
            KingSlimeAI host = context.Host;
            host.ai[SlotVictim] = victim.whoAmI + 1;
            host.ai[SlotGrabPhase] = 1f;
            host.ai[SlotSqueeze] = 0f;
            context.Npc.netUpdate = true;
            GulpFX(context.Npc);
        }

        /// <summary>吞没音画：各端在自然路径上各自触发(服务端由BeginGrab、客户端由相位自愈)</summary>
        private static void GulpFX(NPC npc) {
            SoundEngine.PlaySound(SoundID.Drown with { Pitch = -0.7f, Volume = 1.1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.5f, Volume = 1f, MaxInstances = 3 }, npc.Center);
            if (!VaultUtils.isServer) {
                KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.55f, 8);
                KingSlimeGelFX.GelSplatter(npc.Center - new Vector2(0f, npc.height * 0.3f), -Vector2.UnitY, 8, 5f, 1.1f);
            }
        }

        /// <summary>幕2 消化弹跳：带人小跳三次，每次落地=一记挤压拍(服务端推进计数)</summary>
        private void UpdateDigest(KingSlimeStateContext context) {
            NPC npc = context.Npc;

            //吃撑体态：微胀+沸腾光环；朝向由同步速度推导(各端一致)
            context.ScaleMul = 1.06f;
            context.AuraMode = 2;
            context.AuraProgress = 0.85f;
            if (Math.Abs(npc.velocity.X) > 0.5f) {
                npc.direction = npc.spriteDirection = Math.Sign(npc.velocity.X);
            }

            if (!VaultUtils.isServer && (int)Timer % 5 == 0) {
                KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.45f, 2);
            }

            if (VaultUtils.isClient) {
                return;
            }

            //服务端：落地驱动挤压拍与下一跳
            if (Grounded(npc)) {
                npc.velocity.X *= 0.8f;
                if (context.JustLanded && phaseTimer > 6) {
                    //落地=挤压拍：推进同步计数(特效与受害者伤害都由计数驱动)
                    bouncesDone++;
                    context.Host.ai[SlotSqueeze] = bouncesDone;
                    npc.netUpdate = true;
                    phaseTimer = 0;
                    if (bouncesDone >= DigestBounces) {
                        //进入高压
                        context.Host.ai[SlotGrabPhase] = 2f;
                        phase = 3;
                        return;
                    }
                }
                //落地滞留片刻再起跳(每拍有自己的前摇-爆发-余韵)；
                //用>=防轻触地未触发JustLanded复位时错过等值帧卡死
                if (phaseTimer >= 14 && bouncesDone < DigestBounces) {
                    bounceDir = -bounceDir;
                    float vy = bouncesDone switch { 0 => -9.5f, 1 => -11f, _ => -8.5f };
                    LaunchHop(npc, bounceDir * 2.4f, vy);
                    context.StretchImpulse(0.3f);
                    KingSlimeGelFX.SquishSound(npc.Bottom, -0.2f, 0.8f);
                    phaseTimer = 0;
                }
            }
        }

        /// <summary>挤压拍特效(各端按同步计数触发)：深陷形变+飞溅+闷响+震屏</summary>
        private void SqueezeBeatFX(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            context.ImpactSquash(0.3f);
            KingSlimeGelFX.ThudSound(npc.Bottom, 15f);
            SoundEngine.PlaySound(SoundID.Drown with { Pitch = -0.3f + fxBeatPlayed * 0.12f, Volume = 0.8f, MaxInstances = 3 }, npc.Center);
            KingSlimeGelFX.CameraPunch(npc.Bottom, 5f, 12, "BKSEngulfSqueeze", Vector2.UnitY);
            context.AuraMode = 3;
            context.AuraProgress = 1f;
            if (!VaultUtils.isServer) {
                KingSlimeGelFX.LandingBurst(npc.Bottom, 12f, 1.1f);
                KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.5f, 5);
            }
        }

        /// <summary>幕3 高压：定身深压蓄满，末6帧收声(爆发前的静默)，然后喷出</summary>
        private void UpdatePressure(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.X *= 0.7f;

            float t = MathHelper.Clamp(phaseTimer / (float)PressureTime, 0f, 1f);
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f - 0.5f * t, 0.22f);
            context.WobbleAmp = MathHelper.Clamp(context.WobbleAmp + 0.004f, 0f, 0.14f);
            context.AuraMode = 1;
            context.AuraProgress = t;

            bool quietTail = phaseTimer > PressureTime - 6;
            if (!VaultUtils.isServer && !quietTail) {
                //气泡随压力上涌加密，末段静默
                if (phaseTimer % 2 == 0) {
                    KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * (0.55f - 0.25f * t), 3);
                }
                if (phaseTimer % 10 == 0) {
                    SoundEngine.PlaySound(SoundID.Drown with { Pitch = -0.1f + t * 0.6f, Volume = 0.6f, MaxInstances = 3 }, npc.Center);
                    KingSlimeGelFX.CameraPunch(npc.Center, 1.5f + t * 2.5f, 10, "BKSEngulfPressure");
                }
            }

            if (phaseTimer >= PressureTime && !VaultUtils.isClient) {
                //喷出帧：相位3写包，受害者端读到后自行施加弹射与终伤
                context.Host.ai[SlotGrabPhase] = 3f;
                npc.netUpdate = true;
                phase = 4;
                phaseTimer = 0;
            }
        }

        /// <summary>喷出帧特效(各端观察相位3首帧)：猛拉伸+反冲+大锥飞溅+金环</summary>
        private void EjectFX(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player victim = ResolveVictim(context.Host);
            int dir = victim != null ? Math.Sign(victim.Center.X - npc.Center.X) : npc.direction;
            if (dir == 0) {
                dir = 1;
            }

            context.StretchImpulse(0.62f);
            //反冲：身体向喷出反方向坐倒(质量即反作用)
            npc.velocity = new Vector2(-dir * 3.6f, -2.6f);

            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.25f, Volume = 0.95f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = 0.2f, Volume = 1.1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item167 with { Pitch = -0.8f, Volume = 0.5f, MaxInstances = 2 }, npc.Center);
            KingSlimeGelFX.CameraPunch(npc.Center, 8f, 16, "BKSEngulfEject", new Vector2(dir, -0.5f));
            if (!VaultUtils.isServer) {
                KingSlimeGelFX.GelSplatter(npc.Center, new Vector2(dir * 0.8f, -1f), 18, 10f, 1.3f);
                KingSlimeGelFX.LandingBurst(npc.Bottom, 14f, 1.2f);
            }
            if (!VaultUtils.isClient) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Main.myPlayer, 1f, 1f);
            }
        }

        /// <summary>幕4 恢复：软瘫脱力，体积回落，清同步槽，回连接器</summary>
        private IKingSlimeState UpdateRecover(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.X *= 0.85f;
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 0.7f, 0.12f);
            context.ScaleMul = MathHelper.Lerp(context.ScaleMul, 1f, 0.1f);
            context.AuraMode = 0;
            context.AuraProgress = 0f;

            if (!VaultUtils.isServer && (int)Timer % 9 == 0) {
                KingSlimeGelFX.BubbleFizz(npc.Bottom - new Vector2(0f, 10f), npc.width * 0.4f, 1);
            }

            //喷出包已飞出一段时间后清槽(给受害者端富余的读取窗口)
            if (!VaultUtils.isClient && phaseTimer == 18 && context.Host.ai[SlotVictim] != 0f) {
                ClearGrabSlots(context);
            }

            if (phaseTimer >= RecoverTime && !VaultUtils.isClient) {
                return BackToHop(context);
            }
            return null;
        }

        /// <summary>幕5 空振惩罚：深陷地面缓慢回弹，全程可被集火的输出窗</summary>
        private IKingSlimeState UpdateWhiff(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.X *= 0.8f;

            float t = MathHelper.Clamp(phaseTimer / (float)WhiffTime, 0f, 1f);
            //先深陷再缓慢回弹：狼狈感
            float squashTarget = t < 0.45f ? 0.42f : MathHelper.Lerp(0.42f, 0.95f, (t - 0.45f) / 0.55f);
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, squashTarget, 0.15f);
            context.AuraMode = 0;
            context.AuraProgress = 0f;

            if (!VaultUtils.isServer && (int)Timer % 7 == 0) {
                KingSlimeGelFX.BubbleFizz(npc.Bottom - new Vector2(0f, 8f), npc.width * 0.45f, 1);
            }

            if (phaseTimer >= WhiffTime && !VaultUtils.isClient) {
                return BackToHop(context);
            }
            return null;
        }

        #endregion

        #region 同步与出口

        /// <summary>客户端相位自愈：以同步的抓取相位为准前向修正本地演出相位</summary>
        private void SyncClientPhase(KingSlimeStateContext context, KingSlimeAI host) {
            int netPhase = (int)host.ai[SlotGrabPhase];
            if (netPhase >= 1) {
                sawHold = true;
            }

            if (netPhase == 1 && phase != 2) {
                //吞没确认：落地等待或任何早期相位都推进到消化；慢端补放落地与吞没音画
                if (phase is 9 or 1 or 0) {
                    SlamImpactFX(context);
                    GulpFX(context.Npc);
                }
                phase = 2;
                phaseTimer = 0;
            }
            else if (netPhase == 2 && phase < 3) {
                phase = 3;
                phaseTimer = 0;
            }
            else if (netPhase == 3 && phase < 4) {
                phase = 4;
                phaseTimer = 0;
            }
            else if (netPhase == 0) {
                if (phase == 9) {
                    //落地已过但服务端没给抓取标记→按空振表演
                    landResolveTimer++;
                    if (landResolveTimer > 10) {
                        phase = 5;
                        phaseTimer = 0;
                    }
                }
                else if (sawHold && phase is 2 or 3) {
                    //持人期槽被清=服务端异常断投，跟进释放姿态
                    phase = 4;
                    phaseTimer = 0;
                }
            }
        }

        /// <summary>解析被吞玩家，无效返回null</summary>
        internal static Player ResolveVictim(KingSlimeAI host) {
            if (host == null) {
                return null;
            }
            int idx = (int)host.ai[SlotVictim] - 1;
            if (idx < 0 || idx >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[idx];
            return player != null && player.active ? player : null;
        }

        /// <summary>异常断投(服务端)：清槽即释放，受害者端读到空槽做软释放</summary>
        private void AbortGrab(KingSlimeStateContext context) {
            ClearGrabSlots(context);
            if (phase < 4) {
                phase = 4;
                phaseTimer = 0;
            }
        }

        /// <summary>清空抓取同步槽(服务端)</summary>
        private static void ClearGrabSlots(KingSlimeStateContext context) {
            KingSlimeAI host = context.Host;
            if (host == null || VaultUtils.isClient) {
                return;
            }
            host.ai[SlotVictim] = 0f;
            host.ai[SlotGrabPhase] = 0f;
            host.ai[SlotSqueeze] = 0f;
            context.Npc.netUpdate = true;
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            //无论从哪条路径退出(死亡演出打断/看门狗/正常收招)都保证槽清空、速度残留清理
            ClearGrabSlots(context);
            context.Npc.velocity.X *= 0.5f;
            //出手一次(无论命中)压满冷却，防连续吞没
            context.EngulfCooldown = Math.Max(context.EngulfCooldown, CooldownAfterUse);
        }

        #endregion
    }
}
