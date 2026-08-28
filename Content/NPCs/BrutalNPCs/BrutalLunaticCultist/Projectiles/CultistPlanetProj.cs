using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.PRTTypes;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 教徒召唤的巨型天体（CultistPlanet.fx，一种星球一个 technique）<br/>
    /// ai[0]=星球种类 0星旋 1星云 2星尘 3日耀 4月明 ai[1]=宿主npc<br/>
    /// ai[2]=阶段包装(个位:0降临 1常驻 2退场 3被举起 4砸出 5归位 6裂解 7聚阵;十位:幻象序号)<br/>
    /// 运动学:星旋小幅游走/星云漂移(带幻象)/星尘绕滞后圆心公转(缓追教徒,带转向惯性)/日耀月明钉死场心;<br/>
    /// 举星砸掷:主星被拽到教徒头顶举持(跟随本体的下沉/举升身体语言)→26(月明20)爆发砸出,高速段缓泄保贯穿→归位;<br/>
    /// 砸上黄道结界:全屏等强震+穹膜受击脉冲,反弹计数走 SendExtraAI 广播(远端也看得到撞穹)<br/>
    /// 裂解(转阶段):裂纹生长→坍缩吸气→单帧引爆散尽,残星不留(旧版熔核小球被读成"凭空冒出小星球",已废)<br/>
    /// 聚阵(幻星祭仪):真伪三星滑向品字槽位,实体度蒙面同貌,预瞄锁定拍揭示真容后逐星轮掷<br/>
    /// 公平阀:碰撞半径小于可见球体;生成 3 秒内不造成接触伤害(星球可能罩在玩家身上,留足脱身时间);<br/>
    /// 被举起段不咬人;砸中玩家强力弹飞(重物拍击);开火走 PlanetVolleyGate 轮流出手
    /// </summary>
    internal class CultistPlanetProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int KindVortex = 0;
        internal const int KindNebula = 1;
        internal const int KindStardust = 2;
        internal const int KindSolar = 3;
        internal const int KindMoon = 4;

        private const int ArriveFrames = 42;
        private const int DepartFrames = 46;
        /// <summary>裂解期:裂纹生长帧数</summary>
        internal const int CrackFrames = 40;
        /// <summary>裂解期:坍缩吸气帧数(结束帧即引爆帧)</summary>
        internal const int ImplodeFrames = 14;
        /// <summary>聚阵环半径(px):三星品字彼此擦肩,槽位角走 localAI[1]</summary>
        private const float MusterRadius = 500f;
        /// <summary>实体度真值:真身近实,幻象空壳(识真线索)</summary>
        private const float SolidityTrue = 0.95f;
        private const float SolidityPhantom = 0.40f;
        /// <summary>聚阵蒙面实体度:真伪同貌的一张脸</summary>
        private const float SolidityMasked = 0.58f;
        /// <summary>生成宽限帧数:生成 3 秒内不造成接触伤害(公平阀)</summary>
        private const int SpawnGraceFrames = 180;

        private int Kind => (int)Projectile.ai[0];
        private int OwnerWho => (int)Projectile.ai[1];
        private int Stage => (int)Projectile.ai[2] % 10;
        private int PhantomIndex => (int)Projectile.ai[2] / 10;
        private bool IsPhantom => PhantomIndex > 0;

        private ref float Timer => ref Projectile.localAI[0];
        /// <summary>星尘公转角(各端本地积分,权威端位置广播兜底)</summary>
        private ref float OrbitAngle => ref Projectile.localAI[1];

        /// <summary>上帧段号:远端靠 ai[2] 同步换段,看门复位本地 Timer(localAI 不入网)</summary>
        private int lastSeenStage = -1;

        /// <summary>生成年龄(各端本地积分,换段不清零):生成宽限窗专用</summary>
        private int spawnAge;

        /// <summary>撞穹反弹计数(权威端累加,SendExtraAI 广播;≥2 归位)</summary>
        private int bounceCount;
        /// <summary>远端已见反弹数:-1=未初始化;收包见涨才补撞穹演出,首包/清零静默对齐</summary>
        private int lastBounceSeen = -1;

        /// <summary>身份揭示保持帧:预瞄锁定拍触发,纯本地视觉(各端锁定拍由 timeLeft 同步推得)</summary>
        private int revealHold;
        /// <summary>揭示缓动 0~1:向真值实体度回摆</summary>
        private float revealLerp;
        /// <summary>聚阵蒙面缓动 0~1:入阵渐蒙,离阵渐褪</summary>
        private float maskLerp;

        /// <summary>星尘公转圆心:缓追教徒的滞后锚(权威端专用,客户端吃位置广播),巨物不贴身逐人</summary>
        private Vector2 lazyOrbitCenter;
        private bool lazyOrbitCenterInit;

        /// <summary>可见球体半径(px),shader 球盘=画布 0.42,quad 按此折算;体量=压迫感</summary>
        internal float VisRadius => Kind switch {
            KindNebula => 400f,
            KindStardust => 300f,
            KindSolar => 460f,
            KindMoon => 620f,
            _ => 420f,
        };

        /// <summary>碰撞半径:小于可见体(对玩家宽容);星云是气,判定更松</summary>
        private float CollisionRadius => VisRadius * (Kind == KindNebula ? 0.70f : 0.88f) * Projectile.scale;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.netImportant = true;
            //配合 DrawBehind 设 hide:星球只画在 NPC 身后的舞台层,
            //否则普通弹幕层再画一遍巨型球盘,把低槽位弹幕整块盖没
            Projectile.hide = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            //换段看门:远端由 ai[2] 同步得知换段,本地 Timer 归零对齐节拍(裂解/掷出的演出都按 Timer 走)
            if (Stage != lastSeenStage) {
                lastSeenStage = Stage;
                Timer = 0;
                //砸出/掷出入段清反弹计数(远端 lastBounceSeen 靠收包对齐,不在此清)
                if (Stage == 4) {
                    bounceCount = 0;
                }
            }

            Timer++;
            spawnAge++;

            //身份视觉计时(纯本地):聚阵期真伪同貌,预瞄锁定拍揭示真容
            if (revealHold > 0) {
                revealHold--;
                revealLerp = MathHelper.Min(1f, revealLerp + 0.16f);
            }
            else {
                revealLerp = MathHelper.Max(0f, revealLerp - 0.06f);
            }
            maskLerp = Stage == 7
                ? MathHelper.Min(1f, maskLerp + 0.08f)
                : MathHelper.Max(0f, maskLerp - 0.08f);

            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            bool ownerAlive = owner != null && owner.active && owner.type == NPCID.CultistBoss;

            //宿主没了:直接进退场
            if (!ownerAlive && Stage != 2) {
                SetStage(2);
            }

            CultistStateContext context = null;
            if (ownerAlive && owner.TryGetOverride(out CultistBossAI overrideAI)) {
                context = overrideAI.Context;
            }

            //生命阶段
            switch (Stage) {
                case 0: {
                    //降临:金门先开,星体穿门而出;起始 0.26 快速涨满(不给"凭空小星球"停留感)
                    if ((int)Timer == 1) {
                        CultistMotion.SigilCommitFX(Projectile.Center, CultistMotion.RuneGold, 1.5f);
                        CultistMotion.RuneBurst(Projectile.Center, CultistMotion.RuneGold, 14, 8f);
                        CultistScreenFX.PushFlash(0.30f);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item78 with { Volume = 0.9f, Pitch = -0.5f }, Projectile.Center);
                        }
                    }
                    float t = MathHelper.Clamp(Timer / ArriveFrames, 0f, 1f);
                    float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                    Projectile.scale = 0.26f + 0.74f * ease;
                    //门框余辉:成长期门口持续洒金符
                    if (Timer % 7 == 0 && t < 0.7f) {
                        CultistMotion.RuneBurst(Projectile.Center + Main.rand.NextVector2Circular(60f, 60f) * Projectile.scale,
                            CultistMotion.RuneGold, 1, 4f);
                    }
                    //星旋:裹挟风暴而来,降临全程天幕涌激拉满
                    if (Kind == KindVortex) {
                        CultistScreenFX.StormSurge = 1f;
                    }
                    if (Timer >= ArriveFrames) {
                        SetStage(1);
                        //落位一击
                        CultistMotion.Shake(Projectile.Center, 7f, 14);
                        CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(Kind), 18, 9f);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.1f, Pitch = -0.6f }, Projectile.Center);
                        }
                        //日耀:落位即耀斑,灼热炙烤宣告
                        if (Kind == KindSolar) {
                            CultistScreenFX.PushFlash(0.55f);
                            CultistMotion.Shake(Projectile.Center, 9f, 18);
                            if (!VaultUtils.isClient) {
                                EmitSolarFlare(12);
                            }
                        }
                    }
                    break;
                }
                case 1:
                    Projectile.scale = MathHelper.Lerp(Projectile.scale, 1f, 0.1f);
                    break;
                case 2: {
                    //退场:收缩渐隐,散成符文
                    Projectile.scale *= 0.965f;
                    if (Timer % 5 == 0) {
                        CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(Kind), 2, 6f);
                    }
                    if (Projectile.scale < 0.1f) {
                        Projectile.Kill();
                        return;
                    }
                    break;
                }
                case 3:
                    //被举起:拽到教徒头顶举持,自转提速,体积不变(力量感来自缓慢与重量)
                    Projectile.scale = MathHelper.Lerp(Projectile.scale, 1f, 0.1f);
                    if (Timer % 9 == 0) {
                        CultistMotion.Shake(Projectile.Center, 1.8f, 5);
                    }
                    break;
                case 4: {
                    //砸出:高速段(举星砸的爆发)只缓慢泄速,保住整段进场的贯穿力——反应慢的玩家躲不掉;
                    //低速段急衰到巡航,祭仪掷(13/11 出手)的离手爆发感维持原样
                    float cruise = Kind == KindMoon ? 7f : 9f;
                    float speedNow = Projectile.velocity.Length();
                    if (speedNow > cruise) {
                        Projectile.velocity *= speedNow > 14f ? 0.988f : 0.972f;
                    }
                    if (Main.GameUpdateCount % 7 == 0) {
                        CultistMotion.Shake(Projectile.Center, 2.4f, 6);
                    }
                    if (Timer % 6 == 0) {
                        CultistMotion.RuneBurst(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * VisRadius * 0.7f,
                            CultistMotion.PhaseCore(Kind), 2, 4f);
                    }
                    //撞结界反弹(权威端写物理):镜面反射;演出经 bounceCount 广播,各端 PlayWallImpactFX
                    if (!VaultUtils.isClient && context != null && context.ArenaSpawned) {
                        float dist = Projectile.Center.Distance(context.ArenaCenter);
                        float limit = CultistStateContext.ArenaRadius - CollisionRadius * 0.6f;
                        if (dist > limit) {
                            Vector2 normal = (context.ArenaCenter - Projectile.Center).SafeNormalize(Vector2.UnitY);
                            Projectile.Center = context.ArenaCenter - normal * limit;
                            Projectile.velocity = Vector2.Reflect(Projectile.velocity, normal) * 0.94f;
                            bounceCount++;
                            PlayWallImpactFX();
                            Projectile.netUpdate = true;
                            if (bounceCount >= 2) {
                                SetStage(5);
                            }
                        }
                    }
                    if (Timer >= 320) {
                        SetStage(5);
                    }
                    break;
                }
                default: {
                    //归位:回轨,贴近锚点后转常驻
                    Projectile.scale = MathHelper.Lerp(Projectile.scale, 1f, 0.06f);
                    if (!VaultUtils.isClient && context != null && ownerAlive
                        && Projectile.Center.Distance(ComputeAnchor(context, owner)) < 90f) {
                        SetStage(1);
                    }
                    break;
                }
                case 6: {
                    //裂解爆炸(转阶段/死亡):裂纹超压→坍缩吸气→单帧引爆,星体当帧散尽,不留残球
                    Projectile.velocity *= 0.9f;
                    if (Timer <= CrackFrames) {
                        //超压期:裂纹加深,体积微涨,缝里喷出星质细流,震感与音调同步爬升
                        float t = Timer / CrackFrames;
                        Projectile.scale = MathHelper.Lerp(Projectile.scale, 1.07f, 0.08f);
                        if (Timer % 6 == 0) {
                            CultistMotion.Shake(Projectile.Center, 1.5f + t * 4.5f, 6);
                        }
                        if (Timer % 5 == 0 && !VaultUtils.isServer) {
                            //裂缝喷流:自星面随机方位外喷
                            Vector2 dir = Main.rand.NextVector2Unit();
                            CultistMotion.RuneBurst(Projectile.Center + dir * VisRadius * Projectile.scale * 0.9f,
                                Color.Lerp(CultistMotion.PhaseCore(Kind), Color.White, 0.3f), 2, 7f + t * 5f);
                        }
                        if ((Timer == 10 || Timer == 24 || Timer == 36) && !VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item101 with {
                                Volume = 0.7f,
                                Pitch = -0.6f + t * 0.9f
                            }, Projectile.Center);
                        }
                    }
                    else if (Timer < CrackFrames + ImplodeFrames) {
                        //坍缩吸气:体积急缩,光被吸回去,场上粒子倒卷向星心(爆前的静默拍)
                        Projectile.scale *= 0.985f;
                        if (Timer % 3 == 0) {
                            CultistMotion.RuneBurst(Projectile.Center + Main.rand.NextVector2Unit() * VisRadius * 1.3f,
                                CultistMotion.PhaseCore(Kind), 1, -13f);
                        }
                        if ((int)Timer == CrackFrames + 1 && !VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.8f }, Projectile.Center);
                        }
                    }
                    else {
                        //引爆帧:星体当帧消失,冲击波+全向星屑+余晖帷幕都交给活得比弹体久的粒子
                        DetonationBurst();
                        Projectile.Kill();
                        return;
                    }
                    break;
                }
                case 7: {
                    //聚阵(真伪三星):滑向品字槽位同貌待命;此段不咬人,识真交给锁定拍的实体度揭示
                    Projectile.scale = MathHelper.Lerp(Projectile.scale, 1f, 0.1f);
                    if (Timer % 11 == 0) {
                        CultistMotion.RuneBurst(
                            Projectile.Center + Main.rand.NextVector2Circular(VisRadius * 0.5f, VisRadius * 0.5f),
                            CultistMotion.PhaseCore(Kind), 1, 3f);
                    }
                    //超时保险:出手令缺席也不困死在阵位(真身回常驻,幻象散场)
                    if (Timer >= 420 && !VaultUtils.isClient) {
                        SetStage(IsPhantom ? 2 : 1);
                    }
                    break;
                }
            }
            if (Projectile.timeLeft < 120 && Stage != 2) {
                Projectile.timeLeft = 120;
            }

            //运动学(权威端写位置,netImportant 广播);掷出/裂解段不回锚
            if (!VaultUtils.isClient && ownerAlive && context != null && Stage != 4 && Stage != 6) {
                //聚阵段 localAI[1] 分时借作槽位角(星云不用公转角,出手令会清零)
                //被举起段锚在头顶正上方(星底贴着举起的手),紧弹簧跟随本体的下沉/举升身体语言
                Vector2 anchor = Stage switch {
                    3 => owner.Center + new Vector2(0f, -(VisRadius * Projectile.scale + 92f)),
                    7 => context.ArenaCenter + OrbitAngle.ToRotationVector2() * MusterRadius,
                    _ => ComputeAnchor(context, owner),
                };
                float stiff = Stage switch { 3 => 0.11f, 7 => 0.06f, _ => 0.045f };
                //拽取限速:举星要赶在举升拍前到手,巨物快拽;其余段温和巡移
                float maxPace = Stage == 3 ? 34f : 16f;
                Vector2 desired = (anchor - Projectile.Center) * stiff;
                if (desired.Length() > maxPace) {
                    desired = desired.SafeNormalize(Vector2.Zero) * maxPace;
                }
                //星尘:速度缓插出转向惯性,巨物调头要时间(收势段除外,掷星节拍要脆);其余星球即时跟锚
                Projectile.velocity = Kind == KindStardust && Stage != 3
                    ? Vector2.Lerp(Projectile.velocity, desired, 0.05f)
                    : desired;
                if (Main.GameUpdateCount % 45 == 0) {
                    Projectile.netUpdate = true;
                }
            }
            else if (VaultUtils.isClient && Stage != 4) {
                //客户端沿广播速度自走,权威端周期兜底
                Projectile.velocity *= 0.995f;
            }

            //星球自身的弹幕:与本体轮流出手(公平阀),幻象不开火
            if (!VaultUtils.isClient && context != null && Stage == 1 && !IsPhantom && context.PlanetVolleyGate) {
                EmitVolley(context, owner);
            }

            //压迫感:临星震颤+引力拉扯(本机玩家,纯本地)
            if (!Main.dedServ && Stage == 1 && !IsPhantom) {
                Player lp = Main.LocalPlayer;
                if (lp.Alives()) {
                    float dist = lp.Center.Distance(Projectile.Center);
                    float nearBand = VisRadius * Projectile.scale + 420f;
                    if (dist < nearBand) {
                        float near = 1f - dist / nearBand;
                        if (Main.GameUpdateCount % 22 == 0) {
                            CultistMotion.Shake(lp.Center, 0.8f + near * 1.8f, 8);
                        }
                        //星旋吸卷/月明重力:轻微向星球拉,压迫感的体感层
                        if (Kind == KindVortex || Kind == KindMoon) {
                            lp.velocity += (Projectile.Center - lp.Center).SafeNormalize(Vector2.Zero)
                                * (0.045f + near * 0.075f);
                        }
                    }
                }
            }

            //体光
            float glow = Kind == KindSolar ? 1.4f : 0.85f;
            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(Kind).ToVector3() * glow * Projectile.scale);
        }

        private void SetStage(int stage) {
            Projectile.ai[2] = PhantomIndex * 10 + stage;
            lastSeenStage = stage;
            Timer = 0;
            Projectile.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)bounceCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int received = reader.ReadByte();
            //见涨才演出:撞穹反弹只在权威端发生,远端靠这里补齐撞击的震与光;首包/清零静默对齐
            if (lastBounceSeen >= 0 && received > lastBounceSeen) {
                PlayWallImpactFX();
            }
            lastBounceSeen = received;
            bounceCount = received;
        }

        /// <summary>撞穹演出(各端本地):全屏等强震+穹膜受击脉冲+白闪帷幕,巨物撞界的分量</summary>
        private void PlayWallImpactFX() {
            CultistMotion.Shake(Projectile.Center, 12f, 24, null, 1_000_000f);
            CultistScreenFX.PushFlash(0.40f);
            CultistScreenFX.SetVeil(0.45f, Projectile.Center, CultistMotion.PhaseCore(Kind), 900f);
            CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(Kind), 16, 8f);
            CultistMotion.SigilCommitFX(Projectile.Center, CultistMotion.PhaseCore(Kind), 1.6f);
            CultistZodiacRing.PulseWall(OwnerWho, 1f);
        }

        /// <summary>
        /// 引爆演出(各端本地,引爆帧一次):三重冲击环+全向星屑扇+余晖帷幕,全部活得比弹体久;<br/>
        /// 星体当帧散尽,场上不留任何残球;临星的本机玩家被冲击波小幅弹开(十格上下)
        /// </summary>
        private void DetonationBurst() {
            CultistScreenFX.PushFlash(0.9f);
            CultistScreenFX.SetVeil(0.8f, Projectile.Center, CultistMotion.PhaseCore(Kind), 1000f);
            CultistMotion.Shake(Projectile.Center, 14f, 24);
            if (VaultUtils.isServer) {
                return;
            }

            //引爆冲击:贴星玩家向外轻弹一段(十格上下),有界不叠加;本机玩家本地写速度,各端各推各的
            Player lp = Main.LocalPlayer;
            if (lp.Alives()) {
                float blastBand = VisRadius * Projectile.scale + 260f;
                float lpDist = lp.Center.Distance(Projectile.Center);
                if (lpDist < blastBand) {
                    Vector2 outward = (lp.Center - Projectile.Center).SafeNormalize(-Vector2.UnitY);
                    lp.velocity = outward * MathHelper.Lerp(6.5f, 4.5f, lpDist / blastBand);
                }
            }

            Color core = CultistMotion.PhaseCore(Kind);
            Color edge = CultistMotion.PhaseEdge(Kind);
            float sizeMul = VisRadius / 420f;

            //三重冲击环:星色双环+符金外环,错拍扩散
            InnoVault.PRT.PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, core, 0.30f * sizeMul)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1.9f * sizeMul, 22);
            InnoVault.PRT.PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                Color.Lerp(core, Color.White, 0.5f), 0.20f * sizeMul)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1.35f * sizeMul, 18);
            InnoVault.PRT.PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                CultistMotion.RuneGold, 0.42f * sizeMul)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 2.5f * sizeMul, 26);

            //星屑扇:外壳碎成高速星火,速度分层拉开景深
            int sparks = (int)(30 * sizeMul);
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 19f) * sizeMul;
                InnoVault.PRT.PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + vel.SafeNormalize(Vector2.Zero) * 30f,
                    vel, Color.Lerp(core, edge, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.5f))?
                    .Configure(true, Main.rand.Next(14, 30));
            }
            //星质余烬:慢速上浮,爆点余温
            for (int i = 0; i < 14; i++) {
                InnoVault.PRT.PRTLoader.NewParticle<Rendering.PRT_CultistEmber>(
                    Projectile.Center + Main.rand.NextVector2Circular(70f, 70f) * sizeMul,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f),
                    Color.Lerp(core, Color.White, Main.rand.NextFloat(0.3f)), Main.rand.NextFloat(1.0f, 1.8f))?
                    .Configure(Main.rand.Next(50, 90), 0.05f);
            }
            //符文剥落:星的名字散回天上
            CultistMotion.RuneBurst(Projectile.Center, core, 18, 12f);
            CultistMotion.RuneBurst(Projectile.Center, CultistMotion.RuneGold, 12, 9f);

            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.25f, Pitch = -0.45f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1.0f, Pitch = -0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 0.9f, Pitch = -0.7f }, Projectile.Center);
        }

        /// <summary>各星球的运动学锚点</summary>
        private Vector2 ComputeAnchor(CultistStateContext context, NPC owner) {
            Vector2 center = context.ArenaCenter;
            float t = Main.GlobalTimeWrappedHourly;
            switch (Kind) {
                case KindNebula: {
                    //星云:缓慢漂移;幻象各占相位角
                    float phase = PhantomIndex * MathHelper.TwoPi / 3f;
                    return center + new Vector2(
                        (float)Math.Sin(t * 0.21f + phase) * 260f,
                        (float)Math.Cos(t * 0.16f + phase) * 170f);
                }
                case KindStardust: {
                    //星尘:绕滞后圆心公转,扫过圆环的钟表指针;圆心缓追教徒(疾走瞬移时星球拖在后面),不贴身逐人
                    if (!lazyOrbitCenterInit) {
                        lazyOrbitCenter = owner.Center;
                        lazyOrbitCenterInit = true;
                    }
                    //蚀祭期公转冻结:本影楔锚在星心,星不动楔才立得住(星尘相蚀祭因此可入池)
                    if (!CultistUmbraShade.ShadeActiveFor(OwnerWho)) {
                        lazyOrbitCenter = Vector2.Lerp(lazyOrbitCenter, owner.Center, 0.02f);
                        OrbitAngle += 0.0085f;
                    }
                    return lazyOrbitCenter + OrbitAngle.ToRotationVector2() * 560f;
                }
                case KindSolar:
                case KindMoon:
                    //日耀/月明:钉死场心炙烤
                    return center;
                default:
                    //星旋:小幅利萨茹游走,环宽有呼吸
                    return center + new Vector2(
                        (float)Math.Sin(t * 0.30f) * 130f,
                        (float)Math.Sin(t * 0.47f + 1.3f) * 90f);
            }
        }

        /// <summary>星球弹幕(权威端):每种天体一种语言,缺口是声明常量</summary>
        private void EmitVolley(CultistStateContext context, NPC owner) {
            Player target = context.Target;
            if (target == null || !target.Alives()) {
                return;
            }
            switch (Kind) {
                case KindNebula: {
                    //星云:6 槽星珠环,朝玩家扇区跳 2 槽(GapSlots=2,公平阀)+一对直指珠
                    if (Timer % 62 != 0) {
                        return;
                    }
                    const int NSlots = 6;
                    const int NGap = 2;
                    float pAngle = (target.Center - Projectile.Center).ToRotation();
                    int nGapCenter = (int)MathF.Round(pAngle / MathHelper.TwoPi * NSlots);
                    for (int i = 0; i < NSlots; i++) {
                        int nd = Math.Abs(((i - nGapCenter) % NSlots + NSlots + NSlots / 2) % NSlots - NSlots / 2);
                        if (nd <= NGap / 2) {
                            continue;
                        }
                        Vector2 dir = (MathHelper.TwoPi * i / NSlots).ToRotationVector2();
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center + dir * VisRadius * 0.85f, dir * 4.6f,
                            ModContent.ProjectileType<CultistStarBead>(), 38, 0f, Main.myPlayer, KindNebula);
                    }
                    Vector2 aimDir = pAngle.ToRotationVector2();
                    for (int i = -1; i <= 1; i += 2) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center + aimDir * VisRadius * 0.85f, aimDir.RotatedBy(i * 0.14f) * 6.8f,
                            ModContent.ProjectileType<CultistStarBead>(), 38, 0f, Main.myPlayer, KindNebula);
                    }
                    CultistMotion.CastFlash(Projectile.Center, CultistMotion.NebulaCore, 1.1f);
                    break;
                }
                case KindVortex: {
                    //星旋:10 槽缓速星珠环,朝玩家扇区跳 3 槽(GapSlots=3,公平阀)
                    if (Timer % 54 != 0) {
                        return;
                    }
                    const int Slots = 10;
                    const int GapSlots = 3;
                    float playerAngle = (target.Center - Projectile.Center).ToRotation();
                    int gapCenter = (int)MathF.Round(playerAngle / MathHelper.TwoPi * Slots);
                    for (int i = 0; i < Slots; i++) {
                        int delta = Math.Abs(((i - gapCenter) % Slots + Slots + Slots / 2) % Slots - Slots / 2);
                        if (delta <= GapSlots / 2) {
                            continue;
                        }
                        Vector2 dir = (MathHelper.TwoPi * i / Slots).ToRotationVector2();
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center + dir * VisRadius * 0.9f, dir * 4.2f,
                            ModContent.ProjectileType<CultistStarBead>(), 38, 0f, Main.myPlayer, KindVortex);
                    }
                    CultistMotion.CastFlash(Projectile.Center, CultistMotion.VortexCore, 1.2f);
                    break;
                }
                case KindStardust: {
                    //星尘:公转切向甩晶珠,轨迹可由公转方向预读
                    if (Timer % 48 != 0) {
                        return;
                    }
                    Vector2 tangent = (OrbitAngle + MathHelper.PiOver2).ToRotationVector2();
                    for (int i = 0; i < 2; i++) {
                        Vector2 vel = tangent.RotatedBy((i - 0.5f) * 0.24f) * 5.6f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center + vel.SafeNormalize(Vector2.Zero) * VisRadius * 0.9f, vel,
                            ModContent.ProjectileType<CultistStarBead>(), 38, 0f, Main.myPlayer, KindStardust);
                    }
                    break;
                }
                case KindSolar: {
                    //日耀:日珥抛珠与耀斑辐射交替
                    if (Timer % 52 != 0) {
                        return;
                    }
                    if ((int)(Timer / 52f) % 2 == 0) {
                        Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        for (int i = 0; i < 2; i++) {
                            Vector2 vel = dir.RotatedBy((i - 0.5f) * 0.5f) * 7.5f - Vector2.UnitY * 4.5f;
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                                Projectile.Center + vel.SafeNormalize(Vector2.Zero) * VisRadius * 0.95f, vel,
                                ModContent.ProjectileType<CultistStarBead>(), 40, 0f, Main.myPlayer, KindSolar, 2f);
                        }
                        CultistMotion.CastFlash(Projectile.Center + dir * VisRadius, CultistMotion.SolarCore, 1f);
                    }
                    else {
                        //太阳耀斑:径向直射珠环,朝玩家扇区留缺口
                        EmitSolarFlare(10);
                        CultistScreenFX.PushFlash(0.25f);
                    }
                    break;
                }
                //月明的攻击走凝视态,不开火
            }
        }

        /// <summary>太阳耀斑:自日面径向直射星珠,朝玩家扇区跳 3 槽(GapSlots=3,公平阀);权威端</summary>
        private void EmitSolarFlare(int slots) {
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            const int GapSlots = 3;
            float playerAngle = target.Alives()
                ? (target.Center - Projectile.Center).ToRotation() : 0f;
            int gapCenter = (int)MathF.Round(playerAngle / MathHelper.TwoPi * slots);
            for (int i = 0; i < slots; i++) {
                int delta = Math.Abs(((i - gapCenter) % slots + slots + slots / 2) % slots - slots / 2);
                if (delta <= GapSlots / 2) {
                    continue;
                }
                Vector2 dir = (MathHelper.TwoPi * i / slots).ToRotationVector2();
                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    Projectile.Center + dir * VisRadius * 0.95f, dir * 8.5f,
                    ModContent.ProjectileType<CultistStarBead>(), 40, 0f, Main.myPlayer, KindSolar);
            }
        }

        /// <summary>命令收势待掷(权威端,取一颗常驻非幻象星球)</summary>
        internal static bool CommandRecede(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho
                    && (int)proj.ai[2] % 10 == 1 && (int)proj.ai[2] / 10 == 0) {
                    proj.ai[2] = 3;
                    proj.localAI[0] = 0f;
                    proj.netUpdate = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>命令掷出(权威端):方向即刻锁死,预告即承诺;缓慢但有力量感</summary>
        internal static void CommandLaunch(int ownerWho, Vector2 aim) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho && (int)proj.ai[2] % 10 == 3) {
                    CommandLaunchPlanet(proj, aim);
                    return;
                }
            }
        }

        /// <summary>命令指定星球掷出(权威端,幻星祭仪逐星出手):保幻象序号;出手快于巡航,掷出段自然衰到巡航=离手爆发感</summary>
        internal static void CommandLaunchPlanet(Projectile planet, Vector2 aim) {
            float speed = (int)planet.ai[0] == KindMoon ? 11f : 13f;
            planet.velocity = (aim - planet.Center).SafeNormalize(Vector2.UnitY) * speed;
            planet.ai[2] = (int)planet.ai[2] / 10 * 10 + 4;
            planet.localAI[0] = 0f;
            planet.localAI[1] = 0f;   //槽位角用毕清零
            planet.netUpdate = true;
        }

        /// <summary>命令举星砸出(权威端,举星砸掷):从头顶举持位爆发掷下,高速段缓泄保贯穿(来不及躲=被砸);方向即刻锁死</summary>
        internal static void CommandSmash(int ownerWho, Vector2 aim) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho && (int)proj.ai[2] % 10 == 3) {
                    float speed = (int)proj.ai[0] == KindMoon ? 20f : 26f;
                    proj.velocity = (aim - proj.Center).SafeNormalize(Vector2.UnitY) * speed;
                    proj.ai[2] = (int)proj.ai[2] / 10 * 10 + 4;
                    proj.localAI[0] = 0f;
                    proj.netUpdate = true;
                    return;
                }
            }
        }

        /// <summary>命令入聚阵(权威端,幻星祭仪):槽位角写 localAI[1],三星滑向品字位</summary>
        internal static void CommandMuster(Projectile planet, float slotAngle) {
            planet.ai[2] = (int)planet.ai[2] / 10 * 10 + 7;
            planet.localAI[0] = 0f;
            planet.localAI[1] = slotAngle;
            planet.netUpdate = true;
        }

        /// <summary>预瞄锁定拍揭示真容(各端本地,预瞄线调用):蒙面实体度回摆真值</summary>
        internal void RevealIdentity() => revealHold = 90;

        /// <summary>命令裂解爆炸(权威端,转阶段/死亡用):裂纹→坍缩→单帧引爆散尽</summary>
        internal static void CommandExplode(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho && (int)proj.ai[2] % 10 != 2) {
                    //幻象直接散,真身走裂解
                    if ((int)proj.ai[2] / 10 > 0) {
                        proj.ai[2] = (int)proj.ai[2] / 10 * 10 + 2;
                    }
                    else {
                        proj.ai[2] = 6;
                    }
                    proj.localAI[0] = 0f;
                    proj.netUpdate = true;
                }
            }
        }

        /// <summary>圆形碰撞,可见即危险</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = CollisionRadius;
            Vector2 center = Projectile.Center;
            Vector2 closest = new(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(center, closest) < radius * radius;
        }

        /// <summary>
        /// 伤害窗=可见窗:常驻成形后咬人;砸出段全程咬人;被举起段安全(危险从离手起算);<br/>
        /// 幻象永不咬人(识真线索);生成 3 秒内不咬人(生成点可能罩着玩家,留足脱身时间)
        /// </summary>
        public override bool CanHitPlayer(Player target) {
            if (IsPhantom || spawnAge < SpawnGraceFrames) {
                return false;
            }
            return Stage switch {
                1 => Projectile.scale > 0.95f,
                4 => true,
                _ => false,
            };
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Vector2 push = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
            if (Stage == 4) {
                //被砸出的星球撞上:像被巨物拍飞,半径向外半顺行进向(按令:砸到玩家弹开)
                Vector2 fling = (push * 0.4f + Projectile.velocity.SafeNormalize(Vector2.Zero) * 0.6f)
                    .SafeNormalize(push);
                target.velocity = fling * 19f;
                //重击体感(OnHitPlayer 在被击玩家本机端运行,演出恰好只给挨砸的人)
                CultistMotion.Shake(target.Center, 10f, 18);
                CultistScreenFX.PushFlash(0.35f);
            }
            else {
                //撞上行星:向外弹开,仁慈方向
                target.velocity = push * 11f;
            }
            if (Kind == KindSolar) {
                target.AddBuff(BuffID.OnFire3, 180);
            }
        }

        /// <summary>命令幻象散场(权威端,真身不动)</summary>
        internal static void DismissPhantoms(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho
                    && (int)proj.ai[2] / 10 > 0 && (int)proj.ai[2] % 10 != 2) {
                    proj.ai[2] = (int)proj.ai[2] / 10 * 10 + 2;
                    proj.localAI[0] = 0f;
                    proj.netUpdate = true;
                }
            }
        }

        /// <summary>命令某宿主的所有星球退场(权威端)</summary>
        internal static void BeginDeparture(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho && (int)proj.ai[2] % 10 != 2) {
                    proj.ai[2] = (int)proj.ai[2] / 10 * 10 + 2;
                    proj.localAI[0] = 0f;
                    proj.netUpdate = true;
                }
            }
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            //压在 NPC 身后:星球是舞台,弹幕和本体都读得清
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!CultistMotion.OnScreen(Projectile.Center, VisRadius * 2.4f)) {
                return false;
            }
            Effect effect = EffectLoader.CultistPlanet?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            SpriteBatch sb = Main.spriteBatch;
            if (effect == null || canvas == null || noise == null) {
                DrawFallback(sb);
                return false;
            }

            //瞳孔开度:月明专属,各端从本地 context 读
            float pupil = 0f;
            if (Kind == KindMoon && OwnerWho >= 0 && OwnerWho < Main.maxNPCs
                && Main.npc[OwnerWho].active && Main.npc[OwnerWho].TryGetOverride(out CultistBossAI ai)) {
                pupil = ai.Context?.PupilOpen ?? 0f;
            }

            //uniform 全参数重设(共享 shader 的设备全局残留陷阱)
            //掷星期自转:蓄势小提速,飞行渐加速但饱和封顶 3.2x(无界线性会在反弹时转成陀螺)
            float spinMul = Stage switch {
                3 => 1.8f,
                4 => 1.8f + 1.4f * (1f - MathF.Exp(-Timer / 110f)),
                6 => MathHelper.Max(0.3f, 1f - Timer / CrackFrames),
                _ => 1f,
            };
            //裂解期:裂纹覆层全程压在本体上;坍缩段裂纹顶满+本体渐暗(光被吸进星心)
            float crack = Stage == 6 ? MathHelper.Clamp(Timer / CrackFrames, 0f, 1f) : 0f;
            float implodeDim = Stage == 6 && Timer > CrackFrames
                ? 1f - MathHelper.Clamp((Timer - CrackFrames) / ImplodeFrames, 0f, 1f) * 0.45f : 1f;

            effect.CurrentTechnique = effect.Techniques[TechniqueName];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.identity * 0.37f);
            effect.Parameters["uAlpha"]?.SetValue((Stage == 2 ? Projectile.scale : 1f) * implodeDim);
            effect.Parameters["uSpin"]?.SetValue(SpinOf(Kind) * spinMul);
            effect.Parameters["uShear"]?.SetValue(Kind == KindVortex ? 0.45f : 0f);
            effect.Parameters["uTilt"]?.SetValue(TiltOf(Kind));
            effect.Parameters["uLightDir"]?.SetValue(new Vector3(-0.45f, -0.55f, 0.70f));
            effect.Parameters["uColDeep"]?.SetValue(PaletteDeep(Kind));
            effect.Parameters["uColMid"]?.SetValue(PaletteMid(Kind));
            effect.Parameters["uColBright"]?.SetValue(PaletteBright(Kind));
            effect.Parameters["uColStorm"]?.SetValue(PaletteStorm(Kind));
            //实体度=识真线索:聚阵蒙面期真伪同一张脸,锁定揭示/离阵后回真值
            float trueSolidity = IsPhantom ? SolidityPhantom : SolidityTrue;
            float solidity = MathHelper.Lerp(trueSolidity,
                MathHelper.Lerp(SolidityMasked, trueSolidity, revealLerp), maskLerp);
            effect.Parameters["uSolidity"]?.SetValue(solidity);
            effect.Parameters["uPupil"]?.SetValue(pupil);
            effect.Parameters["uCrack"]?.SetValue(0f);

            //球盘=画布半径 0.42,quad 按可见半径折算(与 .fx 头部契约同步)
            float quadSize = VisRadius / 0.42f * 2f * Projectile.scale;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            sb.Draw(canvas, drawPos, null, Color.White, 0f,
                canvas.Size() * 0.5f, quadSize / canvas.Width, SpriteEffects.None, 0f);

            //裂纹覆层:同 quad 第二趟,缝里透星光;坍缩段缝光顶满(引爆前最后的亮)
            if (crack > 0.01f) {
                effect.CurrentTechnique = effect.Techniques["TechCrack"];
                effect.Parameters["uCrack"]?.SetValue(MathHelper.Min(crack + (1f - implodeDim) * 0.8f, 1f));
                effect.CurrentTechnique.Passes[0].Apply();
                sb.Draw(canvas, drawPos, null, Color.White, 0f,
                    canvas.Size() * 0.5f, quadSize / canvas.Width, SpriteEffects.None, 0f);
            }

            sb.End();

            //星旋:头顶乌云漩涡缠绕(独立批:FNA Immediate 每次 Draw 会重 Apply Begin 绑定的 effect,跨 Effect 必须换批)
            Effect vortexFx = Kind == KindVortex && Stage != 6 ? EffectLoader.CultistCloudVortex?.Value : null;
            if (vortexFx != null) {
                float surge = MathHelper.Clamp(0.45f + CultistScreenFX.StormSurge * 0.55f
                    + (Stage == 4 ? 0.35f : 0f), 0f, 1f);
                //云环与星球自转解耦:反向慢转,掷星提速也不跟(同转会读成一体贴片)
                float cloudSwirl = -Main.GlobalTimeWrappedHourly * 0.09f;
                vortexFx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                vortexFx.Parameters["uAlpha"]?.SetValue(surge);
                vortexFx.Parameters["uHole"]?.SetValue(0.20f);
                vortexFx.Parameters["uSwirl"]?.SetValue(cloudSwirl);
                vortexFx.Parameters["uColDark"]?.SetValue(new Vector3(0.030f, 0.045f, 0.060f));
                vortexFx.Parameters["uColLit"]?.SetValue(new Vector3(0.30f, 0.45f, 0.55f));
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, vortexFx, Main.GameViewMatrix.TransformationMatrix);
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                float cloudQuad = VisRadius * 5.2f * Projectile.scale;
                sb.Draw(canvas, drawPos, null, Color.White, 0f,
                    canvas.Size() * 0.5f, cloudQuad / canvas.Width, SpriteEffects.None, 0f);
                sb.End();
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺席回退:软光球剪影,至少占位可见</summary>
        private void DrawFallback(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            Color core = CultistMotion.PhaseCore(Kind) with { A = 255 };
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, core * 0.85f, 0f,
                glow.Size() * 0.5f, VisRadius * 2f / glow.Width * Projectile.scale, SpriteEffects.None, 0f);
        }

        private string TechniqueName => Kind switch {
            KindNebula => "TechNebula",
            KindStardust => "TechStardust",
            KindSolar => "TechSolar",
            KindMoon => "TechMoon",
            _ => "TechVortex",
        };

        private static float SpinOf(int kind) => kind switch {
            KindNebula => 0.018f,
            KindStardust => 0.055f,
            KindSolar => 0.036f,
            KindMoon => 0.004f,
            _ => 0.050f,
        };

        private static float TiltOf(int kind) => kind switch {
            KindStardust => -0.35f,
            KindVortex => -0.16f,
            _ => 0f,
        };

        private static Vector3 PaletteDeep(int kind) => kind switch {
            KindNebula => new(0.10f, 0.02f, 0.15f),
            KindStardust => new(0.02f, 0.05f, 0.10f),
            KindSolar => new(0.28f, 0.05f, 0.01f),
            KindMoon => new(0.10f, 0.10f, 0.13f),
            _ => new(0.012f, 0.035f, 0.075f),
        };

        private static Vector3 PaletteMid(int kind) => kind switch {
            KindNebula => new(0.46f, 0.10f, 0.46f),
            KindStardust => new(0.16f, 0.38f, 0.48f),
            KindSolar => new(0.85f, 0.32f, 0.05f),
            KindMoon => new(0.32f, 0.33f, 0.38f),
            _ => new(0.055f, 0.21f, 0.30f),
        };

        private static Vector3 PaletteBright(int kind) => kind switch {
            KindNebula => new(0.95f, 0.52f, 0.85f),
            KindStardust => new(0.62f, 0.90f, 0.95f),
            KindSolar => new(1.0f, 0.72f, 0.25f),
            KindMoon => new(0.62f, 0.64f, 0.70f),
            _ => new(0.40f, 0.78f, 0.86f),
        };

        private static Vector3 PaletteStorm(int kind) => kind switch {
            KindNebula => new(1.0f, 0.86f, 1.0f),
            KindStardust => new(0.95f, 1.0f, 1.0f),
            KindSolar => new(1.0f, 0.95f, 0.80f),
            KindMoon => new(0.55f, 1.0f, 0.85f),
            _ => new(0.72f, 0.94f, 1.0f),
        };
    }
}
