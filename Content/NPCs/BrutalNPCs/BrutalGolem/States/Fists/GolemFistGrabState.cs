using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States.Fists
{
    /// <summary>投技抓取：顿帧 → 拖拽钉墙 → 钉压连段 → 沿面研磨 → 终结掷出
    /// 抓取数据写 Override ai[8..11] 同步，被抓玩家位移由其本端 GolemGrabPlayer 施加</summary>
    [InnoVault.StateMachines.VaultState((int)GolemFistStateIndex.Grab, typeof(GolemFistStateContext))]
    internal class GolemFistGrabState : GolemFistStateBase
    {
        public override string StateName => "FistGrab";
        public override GolemFistStateIndex StateIndex => GolemFistStateIndex.Grab;

        /// <summary>顿帧结束（抓住瞬间的凝滞）</summary>
        internal const int HitStopEnd = 10;
        /// <summary>拖拽撞墙结束</summary>
        internal const int DragEnd = 26;
        /// <summary>钉压连段结束（眼激光横扫 + 胸口束点烙窗口）</summary>
        internal const int PinEnd = 121;
        /// <summary>研磨结束 = 终结掷出帧</summary>
        internal const int GrindEnd = 166;
        /// <summary>保底超时</summary>
        internal const int MaxLife = 200;
        /// <summary>拳与被钉玩家的贴合间距</summary>
        internal const float PinGap = 34f;

        //LaserScan 采样暂存（主线程复用）
        private static readonly float[] probeSamples = new float[3];

        //抓取点（各端在 OnEnter 各自捕获，服务端权威）
        private Vector2 connectPoint;
        //研磨行程（服务端在研磨起始帧解算）
        private float grindLen;
        private bool fxSlamDone;

        public override void OnEnter(GolemFistStateContext ctx) {
            base.OnEnter(ctx);
            NPC npc = ctx.Npc;
            connectPoint = npc.Center;
            grindLen = 0f;
            fxSlamDone = false;

            if (!VaultUtils.isClient) {
                //按飞行方向探钉面，失败时降级地面，再失败放弃投掷
                Vector2 dir = npc.velocity.SafeNormalize(Vector2.UnitX * ctx.Side);
                ProbePin(ctx, dir);
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;
            }

            if (!VaultUtils.isServer) {
                //抓住瞬间：重击闷响 + 石屑爆
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.55f, Volume = 1.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f, Volume = 0.6f }, npc.Center);
                GolemScreenEffects.Shake(4f);
                for (int i = 0; i < 12; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(npc.Center + Main.rand.NextVector2Circular(20f, 20f),
                        VaultUtils.RandVr(1.5f, 5f), new Color(122, 104, 78), Main.rand.NextFloat(0.7f, 1.2f)).Configure(36);
                }
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(npc.Center, VaultUtils.RandVr(2f, 6f),
                        new Color(255, 190, 80), Main.rand.NextFloat(0.7f, 1f)).Configure(true, 14);
                }
            }
        }

        public override IGolemFistState OnUpdate(GolemFistStateContext ctx) {
            NPC npc = ctx.Npc;

            //演出保护：抓取期拳不可打、不被弹、不吸仇恨
            npc.dontTakeDamage = true;
            npc.chaseable = false;
            npc.damage = 0;
            npc.noTileCollide = true;
            npc.noGravity = true;

            GolemPinKind kind = (GolemPinKind)(int)ctx.Owner.ai[GolemAiSlots.FistPinKind];
            int targetIdx = (int)ctx.Owner.ai[GolemAiSlots.FistGrabTarget] - 1;
            Player victim = targetIdx >= 0 && targetIdx < Main.maxPlayers ? Main.player[targetIdx] : null;

            //异常出口（服务端裁决）：无钉面/目标失效/被拉远/躯干不在投技状态/超时
            if (!VaultUtils.isClient) {
                bool bodyOk = GolemFacts.BodyValid(ctx.Body)
                    && GolemFacts.GetStateIndex(ctx.Body) == GolemStateIndex.WallSlam;
                bool victimOk = victim != null && victim.active && !victim.dead;
                //拖拽期玩家同步位置滞后，距离断投只在钉压后生效
                bool tooFar = victimOk && Timer > DragEnd + 8 && victim.Distance(npc.Center) > 480f;
                if (kind == GolemPinKind.None || !bodyOk || !victimOk || tooFar || Timer >= MaxLife) {
                    return Release(ctx, thrown: false);
                }
            }

            Vector2 normal = GolemFacts.PinNormal(kind);
            Vector2 pin = new(ctx.Owner.ai[GolemAiSlots.FistPinX], ctx.Owner.ai[GolemAiSlots.FistPinY]);
            Vector2 holdPos = pin + normal * PinGap;

            //拳朝钉面按压（贴图镜像同 Punch 的取向规则）
            Vector2 press = -normal;
            if (press != Vector2.Zero) {
                npc.rotation = ctx.Side < 0 ? (-press).ToRotation() : press.ToRotation();
            }
            npc.velocity = Vector2.Zero;

            //研磨期拳压着玩家碾，接触伤害减额生效（各端一致，判伤在受害端）
            if (Timer >= PinEnd && Timer < GrindEnd) {
                npc.damage = (int)(npc.defDamage * GolemDirector.GrabGrindDamageMul);
            }

            //相位运动只在权威端解算（研磨行程仅服务端知晓），客户端全程跟同步位置
            if (!VaultUtils.isClient) {
                if (Timer < HitStopEnd) {
                    npc.Center = connectPoint;
                }
                else if (Timer < DragEnd) {
                    float t = (Timer - HitStopEnd) / (float)(DragEnd - HitStopEnd);
                    //二次缓入：加速撞进墙里
                    npc.Center = Vector2.Lerp(connectPoint, holdPos, t * t);
                }
                else if (Timer < PinEnd) {
                    npc.Center = holdPos;
                }
                else if (Timer < GrindEnd) {
                    if (Timer == PinEnd) {
                        grindLen = ComputeGrindLen(pin, kind);
                        npc.netUpdate = true;
                    }
                    float t = (Timer - PinEnd) / (float)(GrindEnd - PinEnd);
                    //二次缓入研磨：越磨越快，末段最重
                    npc.Center = holdPos + GolemFacts.GrindTangent(kind) * (t * t) * grindLen;
                }
                else {
                    //终结掷出
                    return Release(ctx, thrown: true);
                }
            }

            UpdateFx(ctx, kind, pin, normal);

            Timer++;
            return null;
        }

        public override void OnExit(GolemFistStateContext ctx) {
            base.OnExit(ctx);
            ctx.Npc.chaseable = true;
        }

        /// <summary>各端本地演出节拍</summary>
        private void UpdateFx(GolemFistStateContext ctx, GolemPinKind kind, Vector2 pin, Vector2 normal) {
            if (VaultUtils.isServer || kind == GolemPinKind.None) {
                return;
            }
            NPC npc = ctx.Npc;

            //撞墙拍：环波 + 石屑帘
            if (!fxSlamDone && Timer >= DragEnd) {
                fxSlamDone = true;
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.8f, Volume = 1.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.5f, Volume = 0.9f }, npc.Center);
                GolemScreenEffects.Shake(6f);
                GolemScreenEffects.PushShockRing(pin, 0.8f, 460f);
                Vector2 wallTangent = GolemFacts.GrindTangent(kind);
                for (int i = 0; i < 16; i++) {
                    Vector2 pos = pin + wallTangent * Main.rand.NextFloat(-46f, 46f) - normal * 6f;
                    PRTLoader.NewParticle<PRT_MarbleChip>(pos, normal * Main.rand.NextFloat(1f, 4f) + VaultUtils.RandVr(0f, 2f),
                        new Color(122, 104, 78), Main.rand.NextFloat(0.8f, 1.3f)).Configure(44);
                }
            }

            //钉压期：接触缝隙渗尘，压迫感
            if (Timer >= DragEnd && Timer < PinEnd && Timer % 9 == 0) {
                Dust dust = Dust.NewDustPerfect(pin + VaultUtils.RandVr(0f, 14f), DustID.Stone,
                    normal * Main.rand.NextFloat(0.5f, 1.5f), 60, default, 1.1f);
                dust.velocity *= 0.5f;
            }

            //研磨期：火花流 + 石屑 + 刮擦声
            if (Timer >= PinEnd && Timer < GrindEnd) {
                Vector2 contact = npc.Center - normal * (PinGap * 0.55f);
                if (Timer % 2 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(contact + VaultUtils.RandVr(0f, 8f),
                        normal * Main.rand.NextFloat(1f, 3f) + GolemFacts.GrindTangent(kind) * Main.rand.NextFloat(-1f, 3f),
                        new Color(255, 200, 90), Main.rand.NextFloat(0.6f, 1f)).Configure(true, 12);
                }
                if (Timer % 5 == 0) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(contact,
                        VaultUtils.RandVr(1f, 3f) - normal * 1.5f,
                        new Color(122, 104, 78), Main.rand.NextFloat(0.6f, 1f)).Configure(30);
                }
                if (Timer % 12 == 0) {
                    SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.7f, Volume = 0.8f }, npc.Center);
                    GolemScreenEffects.Shake(1.5f);
                }
            }
        }

        /// <summary>释放（服务端）：终结掷出附石浪与碎石，异常断投则静默松手</summary>
        private IGolemFistState Release(GolemFistStateContext ctx, bool thrown) {
            NPC npc = ctx.Npc;

            if (thrown) {
                //终结重砸：双向石浪 + 上抛碎石扇（弹幕承载跨端表现）
                int waveDamage = GolemDirector.ScaleDamage(GolemDirector.ShockwaveDamage, ctx.AsuraMode, ctx.Enraged);
                for (int dir = -1; dir <= 1; dir += 2) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom + new Vector2(dir * 30f, -12f),
                        new Vector2(dir * 10f, 0f), ModContent.ProjectileType<GolemShockWave>(),
                        waveDamage, 0f, Main.myPlayer);
                }
                int shrapnelDamage = GolemDirector.ScaleDamage(GolemDirector.ShrapnelDamage, ctx.AsuraMode, ctx.Enraged);
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = (-Vector2.UnitY).RotatedBy(MathHelper.Lerp(-0.9f, 0.9f, i / 5f))
                        * Main.rand.NextFloat(6f, 10f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                        ModContent.ProjectileType<GolemStoneShrapnel>(), shrapnelDamage, 0f, Main.myPlayer);
                }
            }

            //清抓取契约，被抓端读到即解锁
            ctx.Owner.ai[GolemAiSlots.FistGrabTarget] = 0f;
            ctx.Owner.ai[GolemAiSlots.FistPinX] = 0f;
            ctx.Owner.ai[GolemAiSlots.FistPinY] = 0f;
            ctx.Owner.ai[GolemAiSlots.FistPinKind] = 0f;
            npc.netUpdate = true;
            return new GolemFistReturnState();
        }

        /// <summary>探钉面（服务端）：先横向探墙，降级向下探地，全空则放弃投掷</summary>
        private void ProbePin(GolemFistStateContext ctx, Vector2 dir) {
            NPC npc = ctx.Npc;
            float sx = dir.X >= 0f ? 1f : -1f;

            //横向探墙（纯水平轴，钉面几何干净）
            float wallDist = ScanSolid(npc.Center, new Vector2(sx, 0f), 500f);
            if (wallDist < 500f) {
                ctx.Owner.ai[GolemAiSlots.FistPinX] = npc.Center.X + sx * wallDist - sx * 24f;
                ctx.Owner.ai[GolemAiSlots.FistPinY] = npc.Center.Y;
                ctx.Owner.ai[GolemAiSlots.FistPinKind] =
                    (int)(sx > 0f ? GolemPinKind.WallRight : GolemPinKind.WallLeft);
                return;
            }

            //降级：无墙可钉时按在地上碾（神庙外空旷场地）
            float floorDist = ScanSolid(npc.Center, Vector2.UnitY, 440f);
            if (floorDist < 440f) {
                ctx.Owner.ai[GolemAiSlots.FistPinX] = npc.Center.X;
                ctx.Owner.ai[GolemAiSlots.FistPinY] = npc.Center.Y + floorDist - 24f;
                ctx.Owner.ai[GolemAiSlots.FistPinKind] =
                    (int)(sx > 0f ? GolemPinKind.FloorRight : GolemPinKind.FloorLeft);
                return;
            }

            //纯空中：放弃投掷，仅保留重拳击退
            ctx.Owner.ai[GolemAiSlots.FistPinKind] = (int)GolemPinKind.None;
        }

        /// <summary>研磨行程：墙面磨到地板，地面碾到前方障碍，行程收束不穿模</summary>
        private static float ComputeGrindLen(Vector2 pin, GolemPinKind kind) {
            if (kind is GolemPinKind.WallLeft or GolemPinKind.WallRight) {
                float floorDist = ScanSolid(pin, Vector2.UnitY, 520f);
                return Math.Max(floorDist - 26f, 24f);
            }
            Vector2 tangent = GolemFacts.GrindTangent(kind);
            float aheadDist = ScanSolid(pin, tangent, 260f);
            return Math.Max(Math.Min(aheadDist - 26f, 240f), 24f);
        }

        /// <summary>激光扫描取最近固体距离，未命中返回上限</summary>
        private static float ScanSolid(Vector2 from, Vector2 dir, float max) {
            Collision.LaserScan(from, dir, 8f, max, probeSamples);
            float min = max;
            foreach (float s in probeSamples) {
                min = Math.Min(min, s);
            }
            return min;
        }
    }
}
