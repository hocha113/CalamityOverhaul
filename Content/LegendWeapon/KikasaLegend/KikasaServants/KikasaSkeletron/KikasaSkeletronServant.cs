using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaSkeletron
{
    /// <summary>
    /// 鬼奴·湖水版骷髅王。单弹幕内部模拟"头 + 左右手"三件套：
    /// 头位权威同步（Projectile.Center），双手位置各端本地重建（弹簧追锚 + 拍击窗内
    /// 沿弧线运动学摆位），手无骨臂、只垂一条若断若续的血水腕链——被湖缚住的手。
    /// 出水四拍走"双手先挣出水面、头颅随后顶开浪冠"的挣脱叙事；
    /// 战斗循环：双手交替扇形拍击（第二下更快）→ 追踪血颅间奏 → 经典旋颅
    /// （收手护体、达速才开伤害窗、收势踉跄）→ 血颅间奏。
    /// 联机同基准契约：owner 裁决转场盖 netUpdate 章，节拍闩防快照回卷，
    /// 生命线只有 owner 判；命中由 owner 端的手位/头位结算，远端手位仅演出
    /// </summary>
    internal class KikasaSkeletronServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>旋颅与拍击的接触基伤（召唤加成前）</summary>
        internal const int MeleeDamage = 500;

        /// <summary>追踪血颅基伤（召唤加成前），由血颅弹幕消费</summary>
        internal const int SkullDamage = 270;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateSlam = 2;
        private const int StateSpin = 3;
        private const int StateSkulls = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>
        /// 状态内子参数。拍击期编码为 首手位 + 已拍次数×2（bit0=首手，由 owner
        /// 在进场时按目标方位定下并随同步包分发）；旋颅/吐颅为相位号；溶解为过水线闩
        /// </summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：湖下三点血光预兆→双手挣出→头颅顶浪→升起凝实→落定→觉醒握拳
        private const int HandsBreachFrame = 26;
        private const int HeadBreachFrame = 40;
        private const int RiseEnd = 70;
        private const int AwakenFrame = 78;
        private const int EmergeTotal = 92;

        //拍击：三记交替，第二下比第一下快（压迫感），第三下回到中速（错拍）
        private const int SlamCount = 3;
        private static readonly int[] SlapWindups = [28, 16, 22];
        private const int StrikeFrames = 8;
        /// <summary>冲击拍在挥击第几帧落地</summary>
        private const int ImpactAt = 6;
        private const int SlapRecover = 16;

        //旋颅：收手→加速自旋→达速漂移逼近（伤害窗）→衰减踉跄→回正
        private const int TuckFrames = 14;
        private const int SpinupFrames = 26;
        private const int ChaseFrames = 88;
        private const int SpindownFrames = 26;
        private const int SpinRecoverFrames = 16;
        private const float MaxSpinSpeed = 0.55f;

        //吐颅间奏：定身昂首→蓄力（72% 后静默）→三发→回摆
        private const int SkullAimFrames = 12;
        private const int SkullChargeFrames = 16;
        private const int SkullGap = 11;
        private const int SkullCount = 3;
        private const int SkullRecoverFrames = 14;

        private const int DissolveFrames = 52;

        //==================== 双手（各端本地重建，不入同步；owner 端手位裁决命中）====================

        private const float HeadDrawScale = 0.94f;
        private const float HandDrawScale = 0.92f;

        /// <summary>0=左手（原贴图朝向），1=右手（水平翻转）</summary>
        private readonly Vector2[] handPos = new Vector2[2];
        /// <summary>本帧位移差，喂拖影拉伸与拍击扫掠碰撞</summary>
        private readonly Vector2[] handVel = new Vector2[2];
        /// <summary>手的贴图旋转（指尖朝向角 + PiOver2，原版约定）</summary>
        private readonly float[] handRot = new float[2];
        private readonly bool[] handBelowWater = new bool[2];
        private bool handsInit;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int attackCooldown;
        private int attackIndex;
        private int cachedTarget = -1;
        private int lastSeenState = -1;
        //出水演出闩（Emerge 是首状态，不需要跨场清理）
        private bool handsBreached;
        private bool headBreached;
        private bool settleDipped;
        private bool awakenDone;
        //拍击节拍闩
        private int lastSlapLaunched = -1;
        private int lastSlapImpacted = -1;
        //旋颅与吐颅节拍闩
        private bool spinRoared;
        private int lastSkullFired = -1;
        private bool dissolveSplashed;
        /// <summary>觉醒握拳余韵帧数，纯绘制用</summary>
        private int clenchTimer;

        //本次挥击的弧线参数（launch 闩帧从当前手位/目标定参，各端自算，远端仅演出）
        private float slamStartAng;
        private float slamEndDelta;
        private float slamR0;
        private float slamR1;

        //==================== 色板（血系主体随域冷化；幽蓝骨火只做次要点缀层）====================

        private static Color BloodTint => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        private static Color BoneFire => KikasaSkeletronBloodSkull.SkullGlow;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面（破水点）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(MeleeDamage);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 58f), Vector2.Zero,
                ModContent.ProjectileType<KikasaSkeletronServant>(), damage, 8f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //双手甩出去比 hitbox 远得多，头出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 180;
        }

        public override bool MinionContactDamage() => true;

        /// <summary>接触伤害窗与可见的攻击严格对齐：旋颅只在达速漂移段，拍击只在挥击窗</summary>
        public override bool? CanDamage() {
            if (State == StateSpin) {
                return (int)StateParam == 2 ? null : false;
            }
            if (State == StateSlam && SlapIndex < SlamCount) {
                int windup = SlapWindups[SlapIndex];
                int t = (int)StateTimer;
                return t > windup && t <= windup + StrikeFrames + 4 ? null : false;
            }
            return false;
        }

        /// <summary>旋颅=头身圆域+护体双手；拍击=活动手本帧扫掠线段（防高速穿隧）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!handsInit) {
                return false;
            }
            if (State == StateSpin) {
                if (targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(96f, 102f)))) {
                    return true;
                }
                for (int i = 0; i < 2; i++) {
                    if (targetHitbox.Intersects(Utils.CenteredRectangle(handPos[i], new Vector2(52f, 52f)))) {
                        return true;
                    }
                }
                return false;
            }
            if (State == StateSlam) {
                int hand = ActiveHand;
                float _ = 0f;
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    handPos[hand] - handVel[hand], handPos[hand], 46f, ref _);
            }
            return false;
        }

        public override bool? CanCutTiles() => false;

        //==================== 拍击编码 ====================

        private int SlapIndex => (int)StateParam / 2;
        private int SlapFirstHand => (int)StateParam % 2;
        /// <summary>本记的出手：首手起、左右交替</summary>
        private int ActiveHand => (SlapFirstHand + SlapIndex) % 2;

        /// <summary>手的横向符号：左=-1 右=+1</summary>
        private static float HandDir(int i) => i == 0 ? -1f : 1f;

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //双手还没挣出水面就要收场：什么都没露出来，不演谢幕
            if (State == StateEmerge && StateTimer < HandsBreachFrame) {
                Projectile.Kill();
                return;
            }
            State = StateDissolve;
            StateTimer = 0;
            StateParam = 0;
            Projectile.netUpdate = Main.myPlayer == Projectile.owner;
        }

        //==================== 推进 ====================

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }
            bool authority = Main.myPlayer == Projectile.owner;
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();

            //生命线：只有 owner 裁决——服务器无领域状态（既定契约），
            //迟入场客户端首份快照前也会误判；其余端只跟包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(MeleeDamage);

            //换场清闩：远端可能靠收包切状态而非本地同拍转场，
            //上一场残闩会吞掉新场节拍（挥击弧、吼声、过水线拍）
            if (State != lastSeenState) {
                lastSeenState = State;
                lastSlapLaunched = -1;
                lastSlapImpacted = -1;
                lastSkullFired = -1;
                spinRoared = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                }
            }

            if (!handsInit) {
                RebuildHands(domain);
            }

            cachedTarget = FindTarget(owner);

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateSlam: UpdateSlam(owner, domain, authority); break;
                case StateSpin: UpdateSpin(authority); break;
                case StateSkulls: UpdateSkulls(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateHands(domain);
            UpdateDrips(domain);
            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (clenchTimer > 0) {
                clenchTimer--;
            }

            float glow = HeadAlpha() * 0.5f;
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.40f * glow, 0.10f * glow, 0.09f * glow);
                for (int i = 0; i < 2; i++) {
                    Lighting.AddLight(handPos[i], 0.2f * glow, 0.05f * glow, 0.05f * glow);
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：被缚的手先挣出来 ====================

        private void UpdateEmerge(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < HandsBreachFrame) {
                //预兆：湖下三点血光（绘制层），涟漪从两翼向两只手的出水点收拢
                Projectile.velocity = Vector2.Zero;
                if (viewed) {
                    if (t % 5 == 2) {
                        float converge = 1f - t / (float)HandsBreachFrame;
                        float side = t / 5 % 2 == 0 ? 1f : -1f;
                        KikasaDomainDeco.RippleAt(
                            new Vector2(Projectile.Center.X + side * (56f + converge * 60f), lakeY),
                            0.35f + (1f - converge) * 0.5f);
                    }
                    if (t == 6 || t == 18) {
                        SoundEngine.PlaySound(SoundID.Drip with {
                            Volume = 0.45f,
                            Pitch = t == 6 ? -0.5f : -0.2f,
                            MaxInstances = 2
                        }, new Vector2(Projectile.Center.X, lakeY));
                    }
                }
                return;
            }

            if (!handsBreached) {
                //第二拍：双手猛地挣出水面——两处水花、两声骨响错半拍
                handsBreached = true;
                for (int i = 0; i < 2; i++) {
                    float side = HandDir(i);
                    handPos[i] = new Vector2(Projectile.Center.X + side * 56f, lakeY + 4f);
                    handVel[i] = new Vector2(side * 0.6f, -9f);
                    handBelowWater[i] = false;
                    SoundEngine.PlaySound(SoundID.NPCHit2 with {
                        Volume = 0.55f,
                        Pitch = -0.35f + i * 0.12f,
                        MaxInstances = 2
                    }, handPos[i]);
                    if (viewed) {
                        Vector2 hit = new(handPos[i].X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 8);
                        KikasaDomainDeco.RippleAt(hit, 1.2f);
                    }
                }
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    ShakeViewer(2.5f);
                }
            }

            if (t < HeadBreachFrame) {
                //双手已出、头还压在水下：水下血光继续憋压（绘制层）
                return;
            }

            if (!headBreached) {
                //第三拍：头颅顶开浪冠，一帧起速 + 闷吼
                headBreached = true;
                Projectile.velocity = new Vector2(0f, -10.2f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.55f, Pitch = -0.65f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            //升起：起速后指数衰减，前快后慢，禁匀速
            Projectile.velocity.Y *= 0.955f;
            Projectile.velocity.X = 0f;

            if (viewed && t < RiseEnd && t % 2 == 0) {
                //颅顶血水成帘往下淌
                Vector2 dropPos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-36f, 36f), Main.rand.NextFloat(0f, 34f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2.2f, 3.6f)),
                    BloodTint * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 26), 0f);
            }

            if (!settleDipped && t >= RiseEnd + 2) {
                //落定拍：颅骨下沉半口再顶住——重量先答话
                settleDipped = true;
                Projectile.velocity.Y = 1.4f;
            }

            if (!awakenDone && t >= AwakenFrame) {
                //觉醒拍：双手同时握拳、眼窝骨火亮起
                awakenDone = true;
                clenchTimer = 14;
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.6f, Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 2; i++) {
                        for (int k = 0; k < 6; k++) {
                            //拳心挤出的血珠
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                handPos[i] + Main.rand.NextVector2Circular(12f, 12f),
                                new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.4f, 2.2f)),
                                Main.rand.NextBool(3) ? BloodDeep : BloodTint,
                                Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(14, 24), 0.3f);
                        }
                    }
                }
                if (viewed) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 0.6f);
                    ShakeViewer(1.5f);
                }
            }

            //升起期头微仰，觉醒后回正盯猎物方向
            Projectile.rotation = Projectile.rotation.AngleLerp(t < AwakenFrame ? -0.08f : 0f, 0.15f);

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），各端同拍；owner 盖章纠偏
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>破水浪冠：头颅顶开的主浪，量级介于克眼与毁灭者之间</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.6f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(44f, 0f), 1.0f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(40f, 0f), 1.0f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-16f, 0f), 12);
            KikasaDomainDeco.SplashAt(hit + new Vector2(16f, 0f), 12);

            //浪冠扇 + 垂直水柱束
            for (int i = 0; i < 24; i++) {
                float angle = -MathHelper.Pi * (0.12f + 0.76f * i / 23f);
                float speed = Main.rand.NextFloat(3.4f, 7.8f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-28f, 28f), -4f),
                    angle.ToRotationVector2() * speed,
                    Main.rand.NextBool(3) ? BloodDeep : BloodTint,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(24, 38));
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-9f, 9f), -6f),
                    new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), -Main.rand.NextFloat(8.5f, 13.5f)),
                    BloodTint * 0.9f, Main.rand.NextFloat(0.55f, 0.95f))
                    ?.Configure(Main.rand.Next(34, 52));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-34f, 34f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.35f, 0.8f)),
                    MistBlood * 0.85f, Main.rand.NextFloat(0.75f, 1.05f))
                    ?.Configure(Main.rand.Next(65, 105));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.09f)
                ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.36f, 11);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.35f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 1 }, hit);
            ShakeViewer(6f);
        }

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            //悬在主人侧上方，呼吸浮动
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 68f, -178f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 2.0f + Seed) * 7f;
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.2f + Seed * 2f) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢就贴回来，双手一并重建防抽搐
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildHands(owner.GetModPlayer<KikasaDomainPlayer>());
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.08f;
            const float maxSpeed = 16f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.13f);
            //颅骨随横移轻摆
            Projectile.rotation = Projectile.rotation.AngleLerp(
                MathHelper.Clamp(Projectile.velocity.X * 0.045f, -0.3f, 0.3f), 0.12f);

            //出手裁决：拍击→血颅→旋颅→血颅 轮转；规则各端一致，owner 盖章
            if (cachedTarget >= 0 && attackCooldown <= 0 && StateTimer > 26) {
                attackIndex++;
                int pick = attackIndex % 4;
                if (pick == 1) {
                    State = StateSlam;
                    //首手＝目标所在侧的手，掌风顺着劈过去
                    StateParam = Main.npc[cachedTarget].Center.X < Projectile.Center.X ? 0 : 1;
                }
                else if (pick == 3) {
                    State = StateSpin;
                    StateParam = 0;
                }
                else {
                    State = StateSkulls;
                    StateParam = 0;
                }
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 双手交替扇形拍击 ====================

        private void UpdateSlam(Player owner, KikasaDomainPlayer domain, bool authority) {
            int slapIdx = SlapIndex;
            if (slapIdx >= SlamCount) {
                EndAttack(authority, 110);
                return;
            }
            int windup = SlapWindups[slapIdx];
            int t = (int)StateTimer;

            //蓄力半途目标没了就收势
            if (cachedTarget < 0 && t <= windup) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 aimPos = cachedTarget >= 0
                ? Main.npc[cachedTarget].Center + Main.npc[cachedTarget].velocity * 6f
                : Projectile.Center + new Vector2(0f, 260f);

            //头押阵位：比跟随更贴近目标一侧，身子先探过去
            Vector2 lean = (aimPos - owner.Center).SafeNormalize(Vector2.UnitX) * 24f;
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 52f, -186f) + lean;
            Vector2 desired = (anchor - Projectile.Center) * 0.06f;
            if (desired.Length() > 10f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 10f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.1f);
            //颅骨朝出手侧微倾
            Projectile.rotation = Projectile.rotation.AngleLerp(
                MathHelper.Clamp((aimPos.X - Projectile.Center.X) * 0.0004f, -0.14f, 0.14f), 0.1f);

            if (t <= windup) {
                //蓄力：掌心汇聚血珠，72% 后静默——爆发前的吸气
                if (!Main.dedServ && t < windup * 0.72f && t % 3 == 1) {
                    Vector2 palm = handPos[ActiveHand];
                    Vector2 from = palm + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 80f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from, (palm - from) * 0.16f,
                        BloodTint * 0.5f, Main.rand.NextFloat(0.26f, 0.45f))?.Configure(8, 0f);
                }
                if (t == 2) {
                    //抬手骨响
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.3f, Pitch = -0.85f, MaxInstances = 3 }, handPos[ActiveHand]);
                }
                return;
            }

            if (lastSlapLaunched < slapIdx) {
                //launch 一帧定弧：从举起位劈向目标并跟出半程；头吃反冲后仰
                lastSlapLaunched = slapIdx;
                ComputeSlamArc(aimPos);
                Vector2 aimDir = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity -= aimDir * 2.6f;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.8f, Pitch = -0.3f + slapIdx * 0.08f, MaxInstances = 3 }, handPos[ActiveHand]);
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.35f, Pitch = -0.5f, MaxInstances = 3 }, handPos[ActiveHand]);
                if (ViewedOwner) {
                    ShakeViewer(1.2f);
                }
                Projectile.netUpdate = authority;
            }

            //挥击窗内的掌风血帘：沿手甩出速度拉伸的血珠
            if (!Main.dedServ && t <= windup + StrikeFrames) {
                int hand = ActiveHand;
                for (int k = 0; k < 2; k++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        handPos[hand] + Main.rand.NextVector2Circular(16f, 16f),
                        handVel[hand] * 0.28f + Main.rand.NextVector2Circular(1.4f, 1.4f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodTint,
                        Main.rand.NextFloat(0.38f, 0.62f))?.Configure(Main.rand.Next(16, 28), 0.34f);
                }
            }

            if (t >= windup + ImpactAt && lastSlapImpacted < slapIdx) {
                //冲击拍：掌风落地——拍到水面掀一线横推水花，凌空则甩出弧形血帘
                lastSlapImpacted = slapIdx;
                SlamImpact(domain);
            }

            if (t >= windup + StrikeFrames + SlapRecover) {
                //本记结束，换手
                StateParam += 2;
                StateTimer = 0;
                if (SlapIndex >= SlamCount) {
                    EndAttack(authority, 110);
                }
                else {
                    Projectile.netUpdate = authority;
                }
            }
        }

        /// <summary>launch 帧定弧线：起角=当前手位，终角=目标向再跟出 0.55rad 的顺劈</summary>
        private void ComputeSlamArc(Vector2 aimPos) {
            int hand = ActiveHand;
            Vector2 head = Projectile.Center;
            slamStartAng = (handPos[hand] - head).ToRotation();
            float aimAng = (aimPos - head).ToRotation();
            float delta = MathHelper.WrapAngle(aimAng - slamStartAng);
            float side = MathF.Sign(delta);
            if (side == 0f) {
                side = HandDir(hand);
            }
            slamEndDelta = delta + side * 0.55f;
            slamR0 = MathF.Max(Vector2.Distance(handPos[hand], head), 120f);
            slamR1 = MathHelper.Clamp(Vector2.Distance(aimPos, head), 170f, 340f);
        }

        /// <summary>冲击拍分层：震屏 + 重拍双声 + 弧形血珠帘幕；触水加横推水花线</summary>
        private void SlamImpact(KikasaDomainPlayer domain) {
            int hand = ActiveHand;
            Vector2 palm = handPos[hand];
            float lakeY = domain.LakeWorldY;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            bool onWater = lakeAlive && palm.Y >= lakeY - 16f;

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.45f, Pitch = -0.35f, MaxInstances = 2 }, palm);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.65f, Pitch = -0.3f, MaxInstances = 3 }, palm);
            if (ViewedOwner) {
                ShakeViewer(onWater ? 4f : 3f);
            }

            if (Main.dedServ) {
                return;
            }
            //弧形血珠帘幕：沿刚扫过的弧铺一排，向外下方甩出后挂帘坠落
            Vector2 head = Projectile.Center;
            for (int k = 0; k < 10; k++) {
                float ang = slamStartAng + slamEndDelta * (0.35f + 0.65f * k / 9f);
                float r = MathHelper.Lerp(slamR0, slamR1, 0.4f + 0.6f * k / 9f);
                Vector2 pos = head + ang.ToRotationVector2() * r;
                Vector2 fling = ang.ToRotationVector2() * Main.rand.NextFloat(1.5f, 3.5f)
                    + new Vector2(0f, Main.rand.NextFloat(-0.5f, 1.2f));
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos, fling,
                    Main.rand.NextBool(3) ? BloodDeep : BloodTint,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(20, 34), 0.3f);
            }
            PRTLoader.NewParticle<PRT_DWave>(palm, Vector2.Zero, BloodDeep, 0.07f)
                ?.Configure(new Vector2(0.6f, 1f), handVel[hand].ToRotation(), 0.26f, 9);

            if (onWater && ViewedOwner) {
                //一线横推水花：沿掌风方向逐级远去的溅点
                float dir = MathF.Sign(handVel[hand].X);
                if (dir == 0f) {
                    dir = HandDir(hand);
                }
                for (int k = 0; k < 4; k++) {
                    Vector2 hit = new(palm.X + dir * k * 36f, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 7 - k);
                    KikasaDomainDeco.RippleAt(hit, 1.5f - k * 0.28f);
                }
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 2 }, palm);
            }
        }

        //==================== 经典旋颅 ====================

        /// <summary>旋颅角速度：加速平方爬升、达速恒定、收势指数衰减——全由相位计时确定</summary>
        private float SpinOmega() {
            if (State != StateSpin) {
                return 0f;
            }
            int t = (int)StateTimer;
            return (int)StateParam switch {
                1 => MaxSpinSpeed * MathF.Pow(MathHelper.Clamp(t / (float)SpinupFrames, 0f, 1f), 2f),
                2 => MaxSpinSpeed,
                3 => MaxSpinSpeed * MathF.Pow(0.88f, t),
                _ => 0f,
            };
        }

        private void UpdateSpin(bool authority) {
            int t = (int)StateTimer;
            int phase = (int)StateParam;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            Projectile.rotation += SpinOmega();

            if (phase == 0) {
                //收手护体：手向颅侧收拢（UpdateHands），头后拉半步蓄势
                Vector2 back = cachedTarget >= 0
                    ? (Projectile.Center - Main.npc[cachedTarget].Center).SafeNormalize(-Vector2.UnitY)
                    : -Vector2.UnitY;
                float k = MathF.Pow(t / (float)TuckFrames, 3f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, back * (1.5f + 5f * k), 0.2f);
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                }
                if (t >= TuckFrames) {
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //加速自旋：原地憋转，骨节咔咔声随转速爬调；72% 后粒子静默
                Projectile.velocity *= 0.86f;
                float charge = t / (float)SpinupFrames;
                if (t % 6 == 0) {
                    SoundEngine.PlaySound(SoundID.NPCHit2 with {
                        Volume = 0.28f,
                        Pitch = -0.8f + charge * 0.7f,
                        MaxInstances = 3
                    }, Projectile.Center);
                }
                if (!Main.dedServ && charge < 0.72f && t % 2 == 0) {
                    //血珠被转速卷进来
                    Vector2 from = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 130f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                        (Projectile.Center - from) * 0.12f,
                        BloodTint * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(9, 0f);
                }
                if (t >= SpinupFrames) {
                    NextPhase(2);
                }
                return;
            }

            if (phase == 2) {
                //达速漂移逼近：伤害窗开启，缓慢压向目标——威压来自"躲不开的慢"
                if (!spinRoared) {
                    spinRoared = true;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.45f, Pitch = 0.15f, MaxInstances = 2 }, Projectile.Center);
                    if (ViewedOwner) {
                        ShakeViewer(2f);
                    }
                }
                if (cachedTarget >= 0) {
                    Vector2 aim = (Main.npc[cachedTarget].Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, aim * 8.2f, 0.05f);
                }
                else {
                    Projectile.velocity *= 0.97f;
                    if (t > 30) {
                        NextPhase(3);
                        return;
                    }
                }
                //离心甩血：从颅缘沿切线飞出
                if (!Main.dedServ && t % 2 == 0) {
                    float ang = Projectile.rotation + Seed;
                    Vector2 rim = Projectile.Center + ang.ToRotationVector2() * 46f;
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(rim,
                        (ang + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(3f, 6f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodTint,
                        Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(12, 22), 0.3f);
                }
                if (t % 12 == 0) {
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.3f, Pitch = 0.1f, MaxInstances = 2 }, Projectile.Center);
                }
                if (t % 10 == 0 && ViewedOwner) {
                    ShakeViewer(0.6f);
                }
                if (t >= ChaseFrames) {
                    NextPhase(3);
                }
                return;
            }

            if (phase == 3) {
                //收势踉跄：转速衰减、身位左摇右晃再下沉半口——重量在刹车里
                float dir = (int)(Seed * 13f) % 2 == 0 ? 1f : -1f;
                if (t == 2) {
                    Projectile.velocity.X += dir * 3.2f;
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.5f, Pitch = -0.55f, MaxInstances = 2 }, Projectile.Center);
                    if (ViewedOwner) {
                        ShakeViewer(2f);
                    }
                }
                if (t == 10) {
                    Projectile.velocity.X -= dir * 2.1f;
                    Projectile.velocity.Y += 1.8f;
                }
                Projectile.velocity *= 0.9f;
                if (t >= SpindownFrames) {
                    NextPhase(4);
                }
                return;
            }

            //回正：踉跄后把头摆回来
            Projectile.rotation = MathHelper.WrapAngle(Projectile.rotation).AngleLerp(0f, 0.14f);
            Projectile.velocity *= 0.92f;
            if (t >= SpinRecoverFrames) {
                EndAttack(authority, 130);
            }
        }

        //==================== 追踪血颅间奏 ====================

        private void UpdateSkulls(Player owner, bool authority) {
            int t = (int)StateTimer;
            int phase = (int)StateParam;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            Vector2 aimPos = cachedTarget >= 0
                ? Main.npc[cachedTarget].Center
                : Projectile.Center + new Vector2(0f, 300f);
            Vector2 aim = (aimPos - MouthPos()).SafeNormalize(Vector2.UnitY);

            if (phase == 0) {
                //定身昂首：刹车、下颚朝目标抬起
                if (cachedTarget < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                Projectile.velocity *= 0.85f;
                Projectile.rotation = Projectile.rotation.AngleLerp(
                    MathHelper.Clamp(aim.X * 0.2f, -0.3f, 0.3f) - 0.1f, 0.2f);
                if (t >= SkullAimFrames) {
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //蓄力：口中冷火积光（绘制层），血珠向口汇聚，72% 静默截断
                Projectile.velocity *= 0.9f;
                float charge = t / (float)SkullChargeFrames;
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                }
                if (!Main.dedServ && charge < 0.72f && t % 2 == 0) {
                    Vector2 mouth = MouthPos();
                    Vector2 from = mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(46f, 100f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from, (mouth - from) * 0.14f,
                        BoneFire * (0.35f + charge * 0.3f), Main.rand.NextFloat(0.26f, 0.5f))
                        ?.Configure(9, 0f);
                }
                if (t >= SkullChargeFrames) {
                    NextPhase(2);
                }
                return;
            }

            if (phase == 2) {
                //连吐三发，每发后坐上仰；窗口闩出手，远端迟到换场也补得上节拍
                int shotIndex = (t - 1) / SkullGap;
                if (shotIndex < SkullCount && lastSkullFired < shotIndex) {
                    lastSkullFired = shotIndex;
                    FireSkull(owner, aim, shotIndex, authority);
                }
                Projectile.velocity *= 0.9f;
                if (t >= SkullGap * SkullCount) {
                    NextPhase(3);
                }
                return;
            }

            //回摆
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.15f);
            Projectile.velocity *= 0.92f;
            if (t >= SkullRecoverFrames) {
                EndAttack(authority, 70);
            }
        }

        private void FireSkull(Player owner, Vector2 aim, int shotIndex, bool authority) {
            //每发后坐：颅骨上仰退半步
            Projectile.velocity -= aim * 3.0f;
            Projectile.velocity.Y -= 1.1f;
            Projectile.rotation -= 0.07f;

            Vector2 mouth = MouthPos();
            //湿噗 + 骨点双层
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 3 }, mouth);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 3 }, mouth);
            if (!Main.dedServ) {
                //出膛喷吐：锥形冷血珠 + 一圈扩散环
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(mouth + Main.rand.NextVector2Circular(3f, 3f),
                        aim.RotatedByRandom(0.3f) * Main.rand.NextFloat(2.5f, 7f),
                        Main.rand.NextBool(3) ? KikasaSkeletronBloodSkull.SkullDeep : BoneFire,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
                }
                PRTLoader.NewParticle<PRT_DWave>(mouth + aim * 8f, Vector2.Zero, BoneFire, 0.06f)
                    ?.Configure(new Vector2(0.55f, 1f), aim.ToRotation(), 0.2f, 8);
            }
            if (ViewedOwner) {
                ShakeViewer(0.8f);
            }

            //血颅只在 owner 端生成，spawn 包自带全部初值（目标与蛇摆符号走 ai 槽）
            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SkullDamage);
                Vector2 vel = aim.RotatedBy(Main.rand.NextFloat(-0.08f, 0.08f)) * 11.5f;
                //吐是抛出去的：上抛偏置配合弹体前段微重力走弧线
                vel.Y -= 1.6f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), mouth, vel,
                    ModContent.ProjectileType<KikasaSkeletronBloodSkull>(), damage, 2f,
                    Projectile.owner, cachedTarget, shotIndex % 2 == 0 ? 1f : -1f);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解回湖 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;

            if (lakeAlive) {
                //头颅坠回湖里
                Projectile.velocity.X *= 0.92f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.26f, 9f);
            }
            else {
                //湖已不在：原地化水
                Projectile.velocity *= 0.9f;
            }
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.08f);

            //过水线拍（一次）
            if (lakeAlive && !dissolveSplashed && Projectile.Center.Y >= lakeY) {
                dissolveSplashed = true;
                StateParam = 1f;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    Vector2 hit = new(Projectile.Center.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 10);
                    KikasaDomainDeco.RippleAt(hit, 1.4f);
                    ShakeViewer(2f);
                }
            }

            //双手先化、头骨后化：残珠错拍
            if (!Main.dedServ && t % 2 == 0) {
                for (int i = 0; i < 2; i++) {
                    if (HandDissolveT(i) is > 0.05f and < 0.95f && Main.rand.NextBool(2)) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            handPos[i] + Main.rand.NextVector2Circular(16f, 16f),
                            new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.4f, 2.8f)),
                            BloodTint * 0.55f, Main.rand.NextFloat(0.3f, 0.55f))
                            ?.Configure(Main.rand.Next(12, 22));
                    }
                }
                if (HeadDissolveT() is > 0.05f and < 0.95f) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.5f, 3f)),
                        BloodTint * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(12, 22));
                }
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
            }
        }

        //==================== 双手推进（各端本地重建）====================

        /// <summary>头位硬纠或初始化时按状态归位，防手位抽搐</summary>
        private void RebuildHands(KikasaDomainPlayer domain) {
            handsInit = true;
            for (int i = 0; i < 2; i++) {
                float side = HandDir(i);
                if (State == StateEmerge && StateTimer < HandsBreachFrame) {
                    handPos[i] = new Vector2(Projectile.Center.X + side * 56f, domain.LakeWorldY + 22f);
                    handBelowWater[i] = true;
                }
                else {
                    handPos[i] = HoverPost(i);
                    handBelowWater[i] = false;
                }
                handVel[i] = Vector2.Zero;
                handRot[i] = MathHelper.Pi;
            }
        }

        /// <summary>跟随态手位锚：颅侧偏下，呼吸浮动错相位</summary>
        private Vector2 HoverPost(int i) {
            float side = HandDir(i);
            Vector2 post = Projectile.Center + Projectile.velocity + new Vector2(side * 96f, 46f);
            post.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f + Seed + i * 2.1f) * 7f;
            post.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.5f + Seed * 2f + i * 1.7f) * 4f;
            return post;
        }

        /// <summary>指尖指向 dir 时的贴图旋转（原版约定：texture 指尖朝上）</summary>
        private static float FingersRot(Vector2 dir) => dir.ToRotation() + MathHelper.PiOver2;

        private void UpdateHands(KikasaDomainPlayer domain) {
            //本帧渲染位 = Center + velocity（AI 在位移积分前跑）
            Vector2 head = Projectile.Center + Projectile.velocity;
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;

            //硬纠检测：同步包把头拽走半屏，双手直接归位
            if (Vector2.Distance(handPos[0], head) > 700f || Vector2.Distance(handPos[1], head) > 700f) {
                RebuildHands(domain);
                return;
            }

            Vector2 aimPos = cachedTarget >= 0 ? Main.npc[cachedTarget].Center : head + new Vector2(0f, 300f);

            for (int i = 0; i < 2; i++) {
                Vector2 prev = handPos[i];
                float side = HandDir(i);
                Vector2 anchor;
                float wantRot;
                float chase = 0.14f;
                float lerpV = 0.3f;
                float maxSpd = 26f;
                bool kinematic = false;

                switch (State) {
                    case StateEmerge: {
                        if (t < HandsBreachFrame) {
                            //水下待命：钉住不动
                            handPos[i] = new Vector2(head.X + side * 56f, lakeY + 22f);
                            handVel[i] = Vector2.Zero;
                            handRot[i] = 0f;
                            continue;
                        }
                        //挣出后先扑到湖面上方的临时位，头出来了再退让到颅侧
                        anchor = t < HeadBreachFrame
                            ? new Vector2(head.X + side * 64f, lakeY - 44f)
                            : HoverPost(i);
                        wantRot = 0f;
                        if (t >= AwakenFrame) {
                            wantRot = FingersRot((aimPos - handPos[i]).SafeNormalize(Vector2.UnitY));
                        }
                        chase = 0.2f;
                        lerpV = 0.36f;
                        break;
                    }
                    case StateSlam: {
                        int slapIdx = SlapIndex;
                        int windup = slapIdx < SlamCount ? SlapWindups[slapIdx] : 20;
                        bool isActive = i == ActiveHand && slapIdx < SlamCount;
                        if (isActive && t <= windup) {
                            //蓄力举手：高举过颅并向背离目标侧后拉，pow(6) 憋到最后猛吸一口气
                            Vector2 awayDir = (handPos[i] - aimPos).SafeNormalize(-Vector2.UnitY);
                            float k = MathF.Pow(t / (float)windup, 6f);
                            anchor = head + new Vector2(side * 46f, -172f) + awayDir * (k * 46f);
                            wantRot = FingersRot((aimPos - handPos[i]).SafeNormalize(Vector2.UnitY));
                            chase = 0.16f;
                            lerpV = 0.34f;
                        }
                        else if (isActive && t <= windup + StrikeFrames) {
                            //挥击窗：沿定参弧线运动学摆位，几乎全部角程压在前几帧——一记响拍
                            float k = (t - windup) / (float)StrikeFrames;
                            float ease = 1f - MathF.Pow(1f - k, 9f);
                            float ang = slamStartAng + slamEndDelta * ease;
                            float r = MathHelper.Lerp(slamR0, slamR1, MathF.Min(1f, ease * 1.2f));
                            handPos[i] = head + ang.ToRotationVector2() * r;
                            handVel[i] = handPos[i] - prev;
                            handRot[i] = handRot[i].AngleLerp(
                                FingersRot(handVel[i].SafeNormalize(Vector2.UnitY)), 0.7f);
                            kinematic = true;
                            anchor = handPos[i];
                            wantRot = handRot[i];
                        }
                        else if (isActive) {
                            //收势：先硬刹掉挥速再弹回悬停位
                            handVel[i] *= t <= windup + StrikeFrames + 4 ? 0.68f : 1f;
                            anchor = HoverPost(i);
                            wantRot = FingersRot((aimPos - handPos[i]).SafeNormalize(Vector2.UnitY));
                            chase = 0.1f;
                            lerpV = 0.22f;
                        }
                        else {
                            //闲手：压低撑场，指尖始终咬住目标
                            anchor = HoverPost(i) + new Vector2(side * 10f, 16f);
                            wantRot = FingersRot((aimPos - handPos[i]).SafeNormalize(Vector2.UnitY));
                        }
                        break;
                    }
                    case StateSpin: {
                        int phase = (int)StateParam;
                        if (phase == 0) {
                            //收拢护体
                            anchor = head + new Vector2(side * 36f, 10f);
                            wantRot = FingersRot((head - handPos[i]).SafeNormalize(-Vector2.UnitY));
                            chase = 0.2f;
                        }
                        else if (phase <= 3) {
                            //随颅同旋：护体双手贴着转，指尖朝外读出离心
                            float orbit = Projectile.rotation + (i == 0 ? MathHelper.Pi : 0f);
                            anchor = head + orbit.ToRotationVector2() * 46f;
                            wantRot = FingersRot(orbit.ToRotationVector2());
                            chase = 0.55f;
                            lerpV = 0.6f;
                            maxSpd = 60f;
                        }
                        else {
                            anchor = HoverPost(i);
                            wantRot = MathHelper.Pi;
                        }
                        break;
                    }
                    case StateSkulls: {
                        int phase = (int)StateParam;
                        //戏台手势：目标侧的手抬起挑衅点向猎物，另一只压低摊开
                        bool taunting = i == (aimPos.X < head.X ? 0 : 1);
                        if (taunting && phase <= 2) {
                            anchor = head + new Vector2(side * 112f, -12f);
                            float beckon = MathF.Sin(Main.GlobalTimeWrappedHourly * 5.5f + Seed) * 0.16f;
                            wantRot = FingersRot((aimPos - handPos[i]).SafeNormalize(Vector2.UnitY)) + beckon;
                        }
                        else {
                            anchor = head + new Vector2(side * 86f, 70f);
                            wantRot = FingersRot(new Vector2(side * 0.5f, 1f).SafeNormalize(Vector2.UnitY));
                        }
                        break;
                    }
                    case StateDissolve: {
                        //被湖收走：手先松劲下沉
                        anchor = handPos[i] + new Vector2(0f, 2.4f);
                        wantRot = MathHelper.Pi;
                        chase = 0.08f;
                        lerpV = 0.15f;
                        break;
                    }
                    default: {
                        //跟随：呼吸浮动 + 间歇挑衅小动作
                        anchor = HoverPost(i);
                        wantRot = cachedTarget >= 0
                            ? FingersRot((aimPos - handPos[i]).SafeNormalize(Vector2.UnitY))
                            : MathHelper.Pi;
                        int cyc = (int)StateTimer % 110;
                        if (cachedTarget >= 0 && cyc >= 64 && cyc < 92 && i == (int)StateTimer / 110 % 2) {
                            //抬手点向猎物，指尖勾两下
                            anchor = head + new Vector2(side * 98f, -20f)
                                + (aimPos - head).SafeNormalize(Vector2.UnitX) * 36f;
                            wantRot += MathF.Sin((cyc - 64) * 0.55f) * 0.18f;
                        }
                        break;
                    }
                }

                if (!kinematic) {
                    Vector2 want = (anchor - handPos[i]) * chase;
                    if (want.Length() > maxSpd) {
                        want = want.SafeNormalize(Vector2.Zero) * maxSpd;
                    }
                    handVel[i] = Vector2.Lerp(handVel[i], want, lerpV);
                    handPos[i] += handVel[i];
                    handRot[i] = handRot[i].AngleLerp(wantRot, State == StateSpin ? 0.5f : 0.22f);
                }

                //过水线水花（出水演出有自己的专拍，这里只管战斗内起落）
                bool below = handPos[i].Y >= lakeY;
                if (below != handBelowWater[i]) {
                    handBelowWater[i] = below;
                    if (State != StateEmerge && domain.AnyActive && domain.RiseT > 0.5f && ViewedOwner) {
                        Vector2 hit = new(handPos[i].X, lakeY);
                        KikasaDomainDeco.RippleAt(hit, 0.7f);
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                hit + new Vector2(Main.rand.NextFloat(-10f, 10f), -3f),
                                new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1.8f, 4f)),
                                BloodTint * 0.6f, Main.rand.NextFloat(0.3f, 0.55f))
                                ?.Configure(Main.rand.Next(12, 24));
                        }
                    }
                }
            }
        }

        //==================== 公共小件 ====================

        private int FindTarget(Player owner) {
            if (owner.HasMinionAttackTargetNPC) {
                NPC picked = Main.npc[owner.MinionAttackTargetNPC];
                if (picked.CanBeChasedBy(Projectile)
                    && Vector2.Distance(picked.Center, owner.Center) < 1500f) {
                    return picked.whoAmI;
                }
            }
            int best = -1;
            float bestDist = 1050f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, owner.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>下颚口位：rotation=0 时正面朝前、齿在下缘</summary>
        private Vector2 MouthPos()
            => Projectile.Center + (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2() * 32f;

        /// <summary>轮廓凝珠滴落：头缘与手缘错拍，帧内限量</summary>
        private void UpdateDrips(KikasaDomainPlayer domain) {
            if (Main.dedServ || HeadAlpha() < 0.5f) {
                return;
            }
            int budget = 2;
            for (int k = 0; k < 3 && budget > 0; k++) {
                if (!Main.rand.NextBool(22)) {
                    continue;
                }
                budget--;
                int part = Main.rand.Next(3);
                Vector2 pos = part == 0
                    ? Projectile.Center + new Vector2(Main.rand.NextFloat(-32f, 32f), Main.rand.NextFloat(20f, 40f))
                    : handPos[part - 1] + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(10f, 20f));
                if (pos.Y >= domain.LakeWorldY) {
                    continue;
                }
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos,
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                    BloodTint * Main.rand.NextFloat(0.4f, 0.55f),
                    Main.rand.NextFloat(0.32f, 0.55f))
                    ?.Configure(Main.rand.Next(18, 32), 0f);
            }
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float HeadAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < HeadBreachFrame ? 0f : MathHelper.Clamp((t - HeadBreachFrame) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        private float HandAlpha(int i) {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < HandsBreachFrame ? 0f : MathHelper.Clamp((t - HandsBreachFrame) / 4f, 0f, 1f),
                StateDissolve => 1f - HandDissolveT(i),
                _ => 1f,
            };
        }

        /// <summary>uForm：1=全血水 0=真身；骨壳常态比毁灭者更水凝，呼吸微涨落</summary>
        private float SteadyForm(float phaseOff)
            => 0.34f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3.0f + Seed + phaseOff) * 0.05f;

        private float HeadForm() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < HeadBreachFrame
                    ? 1f
                    : MathHelper.Lerp(1f, SteadyForm(0f),
                        SmoothStep01(MathHelper.Clamp((t - HeadBreachFrame) / (float)(RiseEnd - HeadBreachFrame), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(SteadyForm(0f) + t / (float)DissolveFrames * 0.35f, 0f, 1f),
                _ => SteadyForm(0f),
            };
        }

        private float HandForm(int i) {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < HandsBreachFrame
                    ? 1f
                    : MathHelper.Lerp(1f, SteadyForm(1.3f + i),
                        SmoothStep01(MathHelper.Clamp((t - HandsBreachFrame) / 20f, 0f, 1f))),
                StateDissolve => MathHelper.Clamp(SteadyForm(1.3f + i) + HandDissolveT(i) * 0.5f, 0f, 1f),
                _ => SteadyForm(1.3f + i),
            };
        }

        /// <summary>uScanMode：出水期自上而下扫描凝实，头手窗口各自错开</summary>
        private float HeadScan() {
            if (State != StateEmerge) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= RiseEnd) {
                return 1f;
            }
            return 1f - MathHelper.Clamp((t - RiseEnd) / (float)(AwakenFrame - RiseEnd), 0f, 1f);
        }

        private float HandScan() {
            if (State != StateEmerge) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= HandsBreachFrame + 18) {
                return 1f;
            }
            return 1f - MathHelper.Clamp((t - HandsBreachFrame - 18) / 8f, 0f, 1f);
        }

        /// <summary>双手先化（左右错 8 帧），头骨 18 帧后跟上</summary>
        private float HandDissolveT(int i)
            => State == StateDissolve
                ? MathHelper.Clamp((StateTimer - i * 8f) / 26f, 0f, 1f)
                : 0f;

        private float HeadDissolveT()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp((StateTimer - 18f) / 30f, 0f, 1f), 0.9f)
                : 0f;

        private float HeadScale() {
            float scale = HeadDrawScale;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= HeadBreachFrame && t < HeadBreachFrame + 10) {
                //破水过冲
                scale *= 1f + 0.08f * (1f - (t - HeadBreachFrame) / 10f);
            }
            else if (State == StateSkulls && (int)StateParam == 1) {
                //蓄力鼓颅
                scale *= 1f + 0.06f * MathHelper.Clamp(t / (float)SkullChargeFrames, 0f, 1f);
            }
            return scale;
        }

        private float HandScale() {
            float scale = HandDrawScale;
            if (clenchTimer > 0) {
                //觉醒握拳脉冲
                scale *= 1f + 0.12f * MathF.Sin(clenchTimer / 14f * MathHelper.Pi);
            }
            return scale;
        }

        /// <summary>吐颅蓄力进度，口光与汇聚流线共用</summary>
        private float SkullChargeLevel() {
            if (State != StateSkulls) {
                return 0f;
            }
            int phase = (int)StateParam;
            if (phase == 1) {
                return MathHelper.Clamp(StateTimer / SkullChargeFrames, 0f, 1f);
            }
            //连吐窗维持余温
            return phase == 2 ? 0.55f : 0f;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        /// <summary>确定性 0~1 散列，腕链断珠闪烁用</summary>
        private static float Hash01(int n) {
            float v = MathF.Sin(n * 127.1f) * 43758.5453f;
            return v - MathF.Floor(v);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.SkeletronHead);
            Main.instance.LoadNPC(NPCID.SkeletronHand);
            Texture2D headTex = TextureAssets.Npc[NPCID.SkeletronHead]?.Value;
            Texture2D handTex = TextureAssets.Npc[NPCID.SkeletronHand]?.Value;
            if (headTex == null || handTex == null || !handsInit) {
                return false;
            }
            Rectangle headFrame = new(0, 0, headTex.Width, headTex.Height / Main.npcFrameCount[NPCID.SkeletronHead]);
            Rectangle handFrame = new(0, 0, handTex.Width, handTex.Height / Main.npcFrameCount[NPCID.SkeletronHand]);
            SpriteBatch sb = Main.spriteBatch;

            //旋转残影与挥击拖影：主批平染
            DrawSpinGhosts(sb, headTex, headFrame);
            DrawSlamSmears(sb, handTex, handFrame);

            //血水腕链：滴珠串垂向湖面
            DrawWristChains(sb);

            //三件套本体：血湖材质
            DrawBodies(sb, headTex, headFrame, handTex, handFrame, lightColor);

            //加色层：水下预兆血光 / 眼窝骨火 / 口中冷火与汇聚流线 / 掌心蓄力
            DrawGlow(sb);

            return false;
        }

        /// <summary>旋颅残影：同位多旋角的鬼影圈，只在转速起来后亮</summary>
        private void DrawSpinGhosts(SpriteBatch sb, Texture2D headTex, Rectangle frame) {
            if (SpinOmega() < 0.3f || HeadAlpha() < 0.1f) {
                return;
            }
            Vector2 origin = frame.Size() * 0.5f;
            for (int k = Projectile.oldPos.Length - 1; k >= 1; k -= 2) {
                Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                if (oldCenter == Projectile.Size * 0.5f) {
                    continue;
                }
                float fall = 1f - k / (float)Projectile.oldPos.Length;
                sb.Draw(headTex, oldCenter - Main.screenPosition, frame,
                    BloodTint * (0.26f * fall), Projectile.oldRot[k],
                    origin, HeadDrawScale * (0.97f - k * 0.012f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>挥击拖影：按手速在身后铺两三张残掌，速度门控免得常开成噪声</summary>
        private void DrawSlamSmears(SpriteBatch sb, Texture2D handTex, Rectangle frame) {
            Vector2 origin = frame.Size() * 0.5f;
            for (int i = 0; i < 2; i++) {
                float speed = handVel[i].Length();
                float alpha = HandAlpha(i);
                if (speed < 12f || alpha < 0.1f) {
                    continue;
                }
                SpriteEffects fx = i == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                for (int k = 1; k <= 3; k++) {
                    sb.Draw(handTex, handPos[i] - handVel[i] * (k * 0.55f) - Main.screenPosition, frame,
                        BloodTint * (0.3f * alpha * (1f - k * 0.28f)), handRot[i],
                        origin, HandDrawScale * (1f - k * 0.05f), fx, 0f);
                }
            }
        }

        /// <summary>血水腕链：从腕根垂向湖面的滴珠串，若断若续——被湖缚住的手</summary>
        private void DrawWristChains(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            Vector2 gOrigin = glow.Size() * 0.5f;
            int flickerBeat = (int)(Main.GlobalTimeWrappedHourly * 2.5f);

            for (int i = 0; i < 2; i++) {
                float alpha = HandAlpha(i) * (1f - HandDissolveT(i));
                if (alpha < 0.05f || handBelowWater[i]) {
                    continue;
                }
                //腕根在指尖反侧
                Vector2 fingers = (handRot[i] - MathHelper.PiOver2).ToRotationVector2();
                Vector2 wrist = handPos[i] - fingers * 20f;
                bool fast = handVel[i].Length() > 7f;
                Vector2 fastDir = -handVel[i].SafeNormalize(Vector2.UnitY);

                for (int k = 0; k < 6; k++) {
                    //断珠：确定性散列间歇缺一两粒
                    if (Hash01(Projectile.identity * 31 + i * 97 + k * 7 + flickerBeat) < 0.28f) {
                        continue;
                    }
                    Vector2 bead = fast
                        ? wrist + fastDir * (k * 15f)   //高速时腕链被拉直甩在身后
                        : wrist + new Vector2(
                            MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + k * 1.3f + Seed + i * 2f) * 3.5f,
                            6f + k * 14f);
                    float fade = alpha * (1f - k / 7f);
                    float size = (4.6f - k * 0.5f) * 2f;
                    sb.Draw(glow, bead - Main.screenPosition, null, BloodDeep * (0.55f * fade), 0f,
                        gOrigin, new Vector2(size / glow.Width, size * 1.35f / glow.Height), SpriteEffects.None, 0f);
                    sb.Draw(glow, bead - Main.screenPosition, null, (FoamGlow with { A = 0 }) * (0.3f * fade), 0f,
                        gOrigin, new Vector2(size * 0.45f / glow.Width, size * 0.6f / glow.Height), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>三件套本体：先双手后头（头压顶层），血湖材质逐件设参</summary>
        private void DrawBodies(SpriteBatch sb, Texture2D headTex, Rectangle headFrame,
            Texture2D handTex, Rectangle handFrame, Color lightColor) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            }

            //双手
            for (int i = 0; i < 2; i++) {
                float alpha = HandAlpha(i);
                float dissolve = HandDissolveT(i);
                if (alpha < 0.01f || dissolve >= 1f) {
                    continue;
                }
                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed + 2.3f + i * 1.7f);
                    form.Parameters["uForm"]?.SetValue(HandForm(i));
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.Parameters["uScanMode"]?.SetValue(HandScan());
                    form.Parameters["uUvRect"]?.SetValue(new Vector4(
                        0f, 0f, 1f, handFrame.Height / (float)handTex.Height));
                    form.Parameters["uTexel"]?.SetValue(new Vector2(1f / handTex.Width, 1f / handTex.Height));
                    form.Parameters["uAspect"]?.SetValue(handFrame.Width / (float)handFrame.Height);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    color = Color.Lerp(lightColor, BloodTint, 0.55f) * alpha * (1f - dissolve);
                }
                sb.Draw(handTex, handPos[i] - Main.screenPosition, handFrame, color, handRot[i],
                    handFrame.Size() * 0.5f, HandScale(),
                    i == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }

            //头
            float headAlpha = HeadAlpha();
            float headDissolve = HeadDissolveT();
            if (headAlpha > 0.01f && headDissolve < 1f) {
                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed);
                    form.Parameters["uForm"]?.SetValue(HeadForm());
                    form.Parameters["uDissolve"]?.SetValue(headDissolve);
                    form.Parameters["uScanMode"]?.SetValue(HeadScan());
                    form.Parameters["uUvRect"]?.SetValue(new Vector4(
                        0f, 0f, 1f, headFrame.Height / (float)headTex.Height));
                    form.Parameters["uTexel"]?.SetValue(new Vector2(1f / headTex.Width, 1f / headTex.Height));
                    form.Parameters["uAspect"]?.SetValue(headFrame.Width / (float)headFrame.Height);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(headAlpha * 255f));
                }
                else {
                    color = Color.Lerp(lightColor, BloodTint, 0.55f) * headAlpha * (1f - headDissolve);
                }
                sb.Draw(headTex, Projectile.Center - Main.screenPosition, headFrame, color,
                    Projectile.rotation, headFrame.Size() * 0.5f, HeadScale(), SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>加色装饰：出水预兆三点血光、眼窝骨火、口中冷火与汇聚流线、掌心蓄力</summary>
        private void DrawGlow(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();
            if (domain == null) {
                return;
            }

            bool begun = false;
            Vector2 gOrigin = glow.Size() * 0.5f;
            void EnsureBegin() {
                if (!begun) {
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }
            }

            int t = (int)StateTimer;

            //出水预兆：湖下三点血光——两翼浅亮（手）、中央深沉（头），越憋越亮
            if (State == StateEmerge && t < HeadBreachFrame) {
                float ot = MathHelper.Clamp(t / (float)HeadBreachFrame, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                EnsureBegin();
                for (int side = -1; side <= 1; side += 2) {
                    float handEase = MathHelper.Clamp(ease * 1.3f, 0f, 1f);
                    Vector2 pos = new(Projectile.Center.X + side * 56f,
                        domain.LakeWorldY + MathHelper.Lerp(30f, 6f, handEase));
                    float r = 20f + 14f * handEase;
                    //手已破水后两翼血光熄灭
                    if (t < HandsBreachFrame) {
                        sb.Draw(glow, pos - Main.screenPosition, null, FoamGlow * (0.4f * handEase), 0f,
                            gOrigin, new Vector2(r * 2.4f / glow.Width, r * 1.1f / glow.Height), SpriteEffects.None, 0f);
                    }
                }
                Vector2 center = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(58f, 16f, ease));
                float rc = 30f + 24f * ease;
                sb.Draw(glow, center - Main.screenPosition, null, FoamGlow * (0.34f * ease), 0f,
                    gOrigin, new Vector2(rc * 2.8f / glow.Width, rc * 1.1f / glow.Height), SpriteEffects.None, 0f);
            }

            //觉醒拍与威压期：眼窝骨火两点
            float socketGlow = 0f;
            if (State == StateEmerge && t >= AwakenFrame) {
                socketGlow = MathF.Sin(MathHelper.Clamp((t - AwakenFrame) / (float)(EmergeTotal - AwakenFrame), 0f, 1f) * MathHelper.Pi) * 0.9f;
            }
            else if (State == StateSpin && (int)StateParam is 1 or 2) {
                socketGlow = 0.5f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Seed);
            }
            else if (State == StateSkulls) {
                socketGlow = 0.4f + 0.4f * SkullChargeLevel();
            }
            if (socketGlow > 0.03f && HeadAlpha() > 0.1f) {
                EnsureBegin();
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 eye = Projectile.Center
                        + new Vector2(side * 15f, -12f).RotatedBy(Projectile.rotation) * HeadDrawScale;
                    sb.Draw(glow, eye - Main.screenPosition, null, BoneFire * (0.65f * socketGlow), 0f,
                        gOrigin, new Vector2(11f * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //吐颅蓄力：口中冷火 + 汇聚流线（确定性流线，各端一致；72% 后静默余吸）
            float charge = SkullChargeLevel();
            if (charge > 0.03f && HeadAlpha() > 0.1f) {
                EnsureBegin();
                Vector2 mouth = MouthPos();
                float r = 8f + 18f * charge;
                sb.Draw(glow, mouth - Main.screenPosition, null, BoneFire * (0.6f * charge), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                if (charge < 0.72f) {
                    const int streaks = 6;
                    for (int i = 0; i < streaks; i++) {
                        float phase = (Main.GlobalTimeWrappedHourly * 0.9f + i / (float)streaks + Seed * 0.13f) % 1f;
                        float ang = Seed + i * MathHelper.TwoPi / streaks + MathF.Sin(Seed * 3f + i) * 0.7f;
                        float dist = MathHelper.Lerp(88f, 16f, phase);
                        Vector2 pos = mouth + ang.ToRotationVector2() * dist;
                        float a = charge * 0.4f * MathF.Sin(phase * MathHelper.Pi);
                        sb.Draw(glow, pos - Main.screenPosition, null, BoneFire * a, ang,
                            gOrigin, new Vector2(28f / glow.Width * 2.2f, 7f / glow.Height), SpriteEffects.None, 0f);
                    }
                }
            }

            //拍击蓄力：掌心血光憋压
            if (State == StateSlam && SlapIndex < SlamCount) {
                int windup = SlapWindups[SlapIndex];
                if (t <= windup && HandAlpha(ActiveHand) > 0.1f) {
                    float k = t / (float)windup;
                    EnsureBegin();
                    Vector2 palm = handPos[ActiveHand];
                    float r = 8f + 16f * k;
                    sb.Draw(glow, palm - Main.screenPosition, null, FoamGlow * (0.5f * k), 0f,
                        gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //拍击/旋颅撞击的溅血与骨响（OnHit 只在 owner 端跑，队友看拖影即可）
            if (Main.dedServ) {
                return;
            }
            Vector2 impactVel = State == StateSlam ? handVel[ActiveHand] : Projectile.velocity;
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    impactVel * 0.22f + Main.rand.NextVector2Circular(2.6f, 2.6f),
                    BloodTint * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 26), Main.rand.NextFloat(0.2f, 0.45f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.7f, Pitch = -0.25f, MaxInstances = 3 }, target.Center);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = -0.4f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠：头与双手各留一口血水，异常移除也有交代
            if (Main.dedServ || !handsInit) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(28f, 28f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.6f)),
                    BloodTint * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            for (int i = 0; i < 2; i++) {
                for (int k = 0; k < 4; k++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        handPos[i] + Main.rand.NextVector2Circular(14f, 14f),
                        new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.2f)),
                        BloodTint * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(Main.rand.Next(12, 24));
                }
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
