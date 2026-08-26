using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 教徒召唤的巨型天体（CultistPlanet.fx，一种星球一个 technique）<br/>
    /// ai[0]=星球种类 0星旋 1星云 2星尘 3日耀 4月明 ai[1]=宿主npc<br/>
    /// ai[2]=阶段包装(个位:0降临 1常驻 2退场 3收势待掷 4掷出 5归位;十位:幻象序号)<br/>
    /// 运动学:星旋小幅游走/星云漂移(带幻象)/星尘绕教徒公转/日耀月明钉死场心;<br/>
    /// 掷星走近大远小假纵深:收势缩到 0.55(退远)→掷出途中放大到 1.25(掠过近平面)→归位<br/>
    /// 公平阀:碰撞半径小于可见球体;掷出只在近平面(scale&gt;0.85)咬人;开火走 PlanetVolleyGate 轮流出手
    /// </summary>
    internal class CultistPlanetProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int KindVortex = 0;
        internal const int KindNebula = 1;
        internal const int KindStardust = 2;
        internal const int KindSolar = 3;
        internal const int KindMoon = 4;

        private const int ArriveFrames = 56;
        private const int DepartFrames = 46;
        /// <summary>裂解期:裂纹生长帧数</summary>
        internal const int CrackFrames = 46;
        /// <summary>裂解期:熔岩内核蓄力帧数</summary>
        internal const int CoreFrames = 54;

        private int Kind => (int)Projectile.ai[0];
        private int OwnerWho => (int)Projectile.ai[1];
        private int Stage => (int)Projectile.ai[2] % 10;
        private int PhantomIndex => (int)Projectile.ai[2] / 10;
        private bool IsPhantom => PhantomIndex > 0;

        private ref float Timer => ref Projectile.localAI[0];
        /// <summary>星尘公转角(各端本地积分,权威端位置广播兜底)</summary>
        private ref float OrbitAngle => ref Projectile.localAI[1];

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
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
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
                    //降临:假纵深从远处逼近,cubed 缓出
                    float t = MathHelper.Clamp(Timer / ArriveFrames, 0f, 1f);
                    float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                    Projectile.scale = 0.08f + 0.92f * ease;
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
                    //收势待掷:拽到教徒头顶蓄势,自转提速,体积不变(力量感来自缓慢与重量)
                    Projectile.scale = MathHelper.Lerp(Projectile.scale, 1f, 0.1f);
                    if (Timer % 9 == 0) {
                        CultistMotion.Shake(Projectile.Center, 1.8f, 5);
                    }
                    break;
                case 4: {
                    //掷出:缓慢但有力,一路震着走;撞限制圈反弹,越转越快
                    if (Main.GameUpdateCount % 7 == 0) {
                        CultistMotion.Shake(Projectile.Center, 2.4f, 6);
                    }
                    if (Timer % 6 == 0) {
                        CultistMotion.RuneBurst(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * VisRadius * 0.7f,
                            CultistMotion.PhaseCore(Kind), 2, 4f);
                    }
                    //撞墙反弹(权威端):镜面反射+震屏,localAI[1] 计反弹次数
                    if (!VaultUtils.isClient && context != null && context.ArenaSpawned) {
                        float dist = Projectile.Center.Distance(context.ArenaCenter);
                        float limit = CultistStateContext.ArenaRadius - CollisionRadius * 0.6f;
                        if (dist > limit) {
                            Vector2 normal = (context.ArenaCenter - Projectile.Center).SafeNormalize(Vector2.UnitY);
                            Projectile.Center = context.ArenaCenter - normal * limit;
                            Projectile.velocity = Vector2.Reflect(Projectile.velocity, normal) * 0.94f;
                            OrbitAngle += 1f;   //反弹计数(星尘公转角回轨时自续)
                            CultistMotion.Shake(Projectile.Center, 9f, 16);
                            CultistScreenFX.PushFlash(0.25f);
                            CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(Kind), 16, 8f);
                            CultistMotion.SigilCommitFX(Projectile.Center, CultistMotion.PhaseCore(Kind), 1.6f);
                            Projectile.netUpdate = true;
                            if (OrbitAngle >= 2f) {
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
                    //裂解爆炸(转阶段):裂纹生长→外壳炸开露熔岩内核→内核蓄力引爆
                    Projectile.velocity *= 0.9f;
                    if (Timer <= CrackFrames) {
                        //裂纹期:震感随裂纹加深
                        if (Timer % 6 == 0) {
                            CultistMotion.Shake(Projectile.Center, 1.5f + Timer / CrackFrames * 4f, 6);
                        }
                        if ((int)Timer == CrackFrames) {
                            //外壳炸开
                            CultistScreenFX.PushFlash(0.5f);
                            CultistMotion.Shake(Projectile.Center, 11f, 18);
                            CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(Kind), 36, 13f);
                            CultistMotion.ImpactBurst(Projectile.Center, CultistMotion.PhaseLegacyElement(Kind), 2.2f);
                            if (!VaultUtils.isServer) {
                                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1.1f, Pitch = -0.5f }, Projectile.Center);
                            }
                        }
                    }
                    else if (Timer >= CrackFrames + CoreFrames) {
                        //内核引爆
                        CultistScreenFX.PushFlash(0.85f);
                        CultistMotion.Shake(Projectile.Center, 13f, 22);
                        CultistMotion.RuneBurst(Projectile.Center, CultistMotion.RuneGold, 46, 15f);
                        CultistMotion.ImpactBurst(Projectile.Center, 0, 2.6f);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.2f, Pitch = -0.4f }, Projectile.Center);
                        }
                        Projectile.Kill();
                        return;
                    }
                    break;
                }
            }
            if (Projectile.timeLeft < 120 && Stage != 2) {
                Projectile.timeLeft = 120;
            }

            //运动学(权威端写位置,netImportant 广播);掷出/裂解段不回锚
            if (!VaultUtils.isClient && ownerAlive && context != null && Stage != 4 && Stage != 6) {
                Vector2 anchor = Stage == 3
                    ? owner.Center + new Vector2(0f, -430f)
                    : ComputeAnchor(context, owner);
                float stiff = Stage == 3 ? 0.075f : 0.045f;
                Projectile.velocity = (anchor - Projectile.Center) * stiff;
                if (Projectile.velocity.Length() > 16f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 16f;
                }
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
            Timer = 0;
            Projectile.netUpdate = true;
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
                    //星尘:绕教徒公转,扫过圆环的钟表指针
                    OrbitAngle += 0.011f;
                    return owner.Center + OrbitAngle.ToRotationVector2() * 560f;
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
                    if (Timer % 78 != 0) {
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
                    if (Timer % 68 != 0) {
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
                    if (Timer % 60 != 0) {
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
                    if (Timer % 64 != 0) {
                        return;
                    }
                    if ((int)(Timer / 64f) % 2 == 0) {
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
                //星云的压力是幻象本身,月明的攻击走凝视态,都不开火
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
                    float speed = (int)proj.ai[0] == KindMoon ? 7f : 9f;
                    proj.velocity = (aim - proj.Center).SafeNormalize(Vector2.UnitY) * speed;
                    proj.ai[2] = 4;
                    proj.localAI[0] = 0f;
                    proj.localAI[1] = 0f;   //反弹计数清零
                    proj.netUpdate = true;
                    return;
                }
            }
        }

        /// <summary>命令裂解爆炸(权威端,转阶段用):内核蓄力炸开</summary>
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
        /// 伤害窗=可见窗:常驻成形后咬人;掷出只在近平面(放大段)咬人,远平面(缩小)可安全穿过;<br/>
        /// 幻象永不咬人(识真线索)
        /// </summary>
        public override bool CanHitPlayer(Player target) {
            if (IsPhantom) {
                return false;
            }
            return Stage switch {
                1 => Projectile.scale > 0.95f,
                4 => true,
                _ => false,
            };
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //撞上行星:向外弹开,仁慈方向
            Vector2 push = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
            target.velocity = push * 11f;
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
            //掷星期自转越来越快:蓄势小提速,飞行随时间狂飙
            float spinMul = Stage switch {
                3 => 1.8f,
                4 => 1.8f + Timer * 0.055f,
                6 => MathHelper.Max(0.3f, 1f - Timer / CrackFrames),
                _ => 1f,
            };
            //裂解期:先画本体+裂纹覆层,外壳炸开后只剩熔岩内核
            bool moltenCore = Stage == 6 && Timer > CrackFrames;
            float crack = Stage == 6 ? MathHelper.Clamp(Timer / (float)CrackFrames, 0f, 1f) : 0f;
            //内核蓄力:体积缓涨,引爆前顶满
            float coreSwell = moltenCore
                ? 0.42f + MathHelper.Clamp((Timer - CrackFrames) / (float)CoreFrames, 0f, 1f) * 0.22f : 1f;

            effect.CurrentTechnique = effect.Techniques[moltenCore ? "TechSolar" : TechniqueName];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.identity * 0.37f);
            effect.Parameters["uAlpha"]?.SetValue(Stage == 2 ? Projectile.scale : 1f);
            effect.Parameters["uSpin"]?.SetValue(moltenCore ? 0.11f : SpinOf(Kind) * spinMul);
            effect.Parameters["uShear"]?.SetValue(Kind == KindVortex && !moltenCore ? 0.45f : 0f);
            effect.Parameters["uTilt"]?.SetValue(moltenCore ? 0f : TiltOf(Kind));
            effect.Parameters["uLightDir"]?.SetValue(new Vector3(-0.45f, -0.55f, 0.70f));
            effect.Parameters["uColDeep"]?.SetValue(moltenCore ? new Vector3(0.30f, 0.04f, 0.00f) : PaletteDeep(Kind));
            effect.Parameters["uColMid"]?.SetValue(moltenCore ? new Vector3(0.92f, 0.26f, 0.03f) : PaletteMid(Kind));
            effect.Parameters["uColBright"]?.SetValue(moltenCore ? new Vector3(1.0f, 0.62f, 0.16f) : PaletteBright(Kind));
            effect.Parameters["uColStorm"]?.SetValue(moltenCore ? new Vector3(1.0f, 0.92f, 0.60f) : PaletteStorm(Kind));
            effect.Parameters["uSolidity"]?.SetValue(IsPhantom ? 0.40f : 0.95f);
            effect.Parameters["uPupil"]?.SetValue(pupil);
            effect.Parameters["uCrack"]?.SetValue(0f);

            //球盘=画布半径 0.42,quad 按可见半径折算(与 .fx 头部契约同步)
            float quadSize = VisRadius / 0.42f * 2f * Projectile.scale * coreSwell;

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

            //裂纹覆层:同 quad 第二趟,熔岩透光
            if (crack > 0.01f && !moltenCore) {
                effect.CurrentTechnique = effect.Techniques["TechCrack"];
                effect.Parameters["uCrack"]?.SetValue(crack);
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
                vortexFx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                vortexFx.Parameters["uAlpha"]?.SetValue(surge);
                vortexFx.Parameters["uHole"]?.SetValue(0.20f);
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
