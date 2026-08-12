using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>
    /// 鬼奴·湖水版毁灭者。单弹幕内部模拟整条短链蠕虫（头+12体+尾，
    /// 体节沿头部走过的路径回溯摆位——全体从同一个破水孔穿行）。
    /// 出场为蛟龙出海：潜行航迹预兆→破水弹道弧（弧顶重力减轻悬拍）→按战场高度
    /// 回落入湖巡游或拉起转空中蟒行；双栖跟随带滞回。攻击为血液喷柱（独立弹幕）
    /// 与潜浪跃出冲撞。逐节湿度驱动滴落与材质血水度，过水线双向水花。
    /// 联机同克眼契约：owner 裁决转场盖 netUpdate 章，路径缓冲各端本地重建，
    /// 节拍闩防快照回卷，生命线只有 owner 判
    /// </summary>
    internal class KikasaDestroyerServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>冲撞接触基伤（召唤加成前）</summary>
        internal const int RamDamage = 650;

        /// <summary>喷柱单跳基伤（召唤加成前），由喷柱弹幕消费</summary>
        internal const int JetDamage = 320;

        //==================== 链体尺寸 ====================

        internal const int SegCount = 14;
        internal const float DrawScale = 0.55f;
        /// <summary>节距 = BTD 本体 64 × 缩放</summary>
        internal const float SegSpacing = 64f * DrawScale;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateCruise = 1;
        private const int StateAirFollow = 2;
        private const int StateJet = 3;
        private const int StateDiveRam = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>
        /// 状态内子参数。出水期编码为 方向×(1+弧段)：符号=弧线横向，
        /// |值|-1=弧段(0 潜行/预顶、1 落湖、2 拉起)；其余状态为普通相位号
        /// </summary>
        private ref float StateParam => ref Projectile.ai[2];

        private float ArcDir => MathF.Sign(StateParam) == 0f ? 1f : MathF.Sign(StateParam);
        private int ArcPhase => (int)MathF.Abs(StateParam) - 1;

        //==================== 时序 ====================

        //蛟龙出海：潜行→静默→破水弹道→（弧顶自然分段）
        private const int ApproachFrames = 44;
        private const int ApproachSilence = 6;
        private const int EmergeTimeout = 280;

        //喷柱：抬头(仅湖态)→后仰蓄势→开火持续→回摆
        private const int JetRaiseFrames = 12;
        private const int JetRearFrames = 26;
        internal const int JetSustainFrames = 44;
        private const int JetRecoverFrames = 16;

        //冲撞：入水→水下冲刺(就位早退)→跃出激活→回收
        private const int RamDiveFrames = 20;
        private const int RamSprintMax = 50;
        private const int RamLeapFrames = 26;
        private const int RamRecoverFrames = 24;

        private const int DissolvePerSegGap = 3;
        private const int DissolveSegFrames = 26;
        private const int DissolveTotal = (SegCount - 1) * DissolvePerSegGap + DissolveSegFrames + 10;

        //双栖滞回
        private const float AirAboveLake = 480f;
        private const float CruiseBelow = 320f;
        private const int HabitatSwitchHold = 40;

        //==================== 链体数据（各端本地重建，头位置由同步纠偏）====================

        private readonly Vector2[] spine = new Vector2[SegCount];
        private readonly float[] segRot = new float[SegCount];
        /// <summary>节湿度：过水线拉满、出水后衰减，驱动滴落与材质血水度</summary>
        private readonly float[] wetness = new float[SegCount];
        private readonly bool[] belowWater = new bool[SegCount];
        private bool spineInit;

        //头部路径环形缓冲：移动≥3px 才入样本，容量足够覆盖全链弧长
        private const int PathCap = 224;
        private const float PathSampleDist = 3f;
        private readonly Vector2[] path = new Vector2[PathCap];
        private int pathHead;
        private int pathCount;

        //==================== 本地表现量 ====================

        private int frameTick;
        private int frameIndex;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private int habitatHoldTimer;
        private bool launchDone;
        private bool breachDone;
        private bool reentrySplashed;
        private bool leapLaunched;
        private float cruisePhase;
        /// <summary>喷柱期头部朝向锁（NaN=不锁），喷柱弹幕逐帧读这个角</summary>
        private float lockedHeadRot = float.NaN;
        /// <summary>蓄势卷身的节距压缩系数</summary>
        private float spacingMul = 1f;
        /// <summary>开火后坐鞭浪：沿链传播的横向冲量（波位/振幅）</summary>
        private float whipPos = -1f;
        private float whipAmp;
        /// <summary>跃出残影快照（两份，间隔取样）</summary>
        private readonly Vector2[][] ghostSnaps = new Vector2[2][];
        private int ghostSnapTick;

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        private Player Owner => Main.player[Projectile.owner];

        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面（破水点）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RamDamage);
            //起点在破水点后方湖下，潜行段自己游过来
            float dir = MathF.Sign(owner.Center.X - emergeAt.X);
            if (dir == 0f) {
                dir = owner.direction;
            }
            Vector2 spawn = new(emergeAt.X - dir * 380f, emergeAt.Y + 88f);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"), spawn, Vector2.Zero,
                ModContent.ProjectileType<KikasaDestroyerServant>(), damage, 8f, owner.whoAmI,
                ai2: dir);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //链体远超 hitbox，头出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1600;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = 180;
        }

        public override bool MinionContactDamage() => true;

        /// <summary>接触伤害只开在跃出激活窗，与可见的突进严格对齐</summary>
        public override bool? CanDamage()
            => State == StateDiveRam && (int)StateParam == 2 ? null : false;

        /// <summary>多节命中：相邻脊柱点两两线碰撞</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!spineInit) {
                return false;
            }
            float _ = 0f;
            for (int i = 1; i < SegCount; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    spine[i - 1], spine[i], 30f, ref _)) {
                    return true;
                }
            }
            return false;
        }

        public override bool? CanCutTiles() => false;

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //还没破水就要收场：什么都没露出来，不演谢幕
            if (State == StateEmerge && StateTimer < ApproachFrames) {
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
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RamDamage);

            //换场清闩：远端可能靠收包切状态（Emerge 是首状态，出水闩不需要跨场保护）
            if (State != lastSeenState) {
                lastSeenState = State;
                reentrySplashed = false;
                leapLaunched = false;
                whipPos = -1f;
                lockedHeadRot = float.NaN;
            }

            if (!spineInit) {
                RebuildChain((int)Projectile.ai[2] != 0 ? new Vector2(Projectile.ai[2], 0f) : Vector2.UnitX);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateCruise: UpdateCruise(owner, domain, authority); break;
                case StateAirFollow: UpdateAirFollow(owner, domain, authority); break;
                case StateJet: UpdateJet(owner, domain, authority); break;
                case StateDiveRam: UpdateDiveRam(owner, domain, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateChain(domain);
            UpdateWhip();
            UpdateFrames();
            UpdateDrips(domain);
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            //沿链补光
            for (int i = 0; i < SegCount; i += 4) {
                Lighting.AddLight(spine[i], 0.26f, 0.07f, 0.06f);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 蛟龙出海 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            float arcDir = ArcDir;

            if (t <= ApproachFrames) {
                //潜行：湖下滑向破水点，前慢后快；末几帧演出静默（爆发前的憋气）
                float a = MathHelper.Clamp(t / (float)ApproachFrames, 0f, 1f);
                float speed = MathHelper.Lerp(4f, 14f, MathF.Pow(a, 1.6f));
                Projectile.velocity = new Vector2(arcDir * speed, -0.6f * a);

                bool silence = t > ApproachFrames - ApproachSilence;
                if (viewed && !silence) {
                    //航迹涟漪逐帧跟头，间距渐密幅度渐大
                    if (t % MathF.Max(6 - (int)(a * 3f), 2) == 1) {
                        KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY),
                            0.3f + a * 0.55f);
                    }
                    //两记闷涌拍
                    if (t == 18 || t == 34) {
                        KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, lakeY), t == 18 ? 4 : 6);
                        SoundEngine.PlaySound(SoundID.SplashWeak with {
                            Volume = t == 18 ? 0.4f : 0.5f,
                            Pitch = -0.8f,
                            MaxInstances = 2
                        }, new Vector2(Projectile.Center.X, lakeY));
                        ShakeViewer(t == 18 ? 1f : 1.6f);
                    }
                }
                return;
            }

            if (!launchDone) {
                //起跳拍：一帧定弹道，仰角 70° 偏向玩家一侧；吼声先从水下闷出来
                launchDone = true;
                const float launchSpeed = 22f;
                float angle = MathHelper.ToRadians(70f);
                Projectile.velocity = new Vector2(arcDir * MathF.Cos(angle), -MathF.Sin(angle)) * launchSpeed;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.65f, Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);
            }

            //浪冠在头真正撞破水面那帧起爆
            if (!breachDone && Projectile.Center.Y <= lakeY) {
                breachDone = true;
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            //任何弧段的全局兜底：出水演出绝不允许没有出口
            if (t > EmergeTimeout) {
                EnterHabitat(ChooseHabitatIsAir(owner, domain));
                return;
            }

            //弹道推进：越近弧顶重力越轻——顶点悬拍，蛟龙弓身读满
            const float v0y = 20.7f;
            float g = 0.55f * (0.4f + 0.6f * MathHelper.Clamp(MathF.Abs(Projectile.velocity.Y) / v0y, 0f, 1f));
            Projectile.velocity.Y += g;

            //弧顶分段裁决：落湖巡游 or 拉起入空（规则确定性，owner 盖章）
            if (ArcPhase == 0 && Projectile.velocity.Y >= 0f) {
                StateParam = arcDir * (1 + (ChooseHabitatIsAir(owner, domain) ? 2 : 1));
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }

            if (ArcPhase == 2) {
                //拉起：下坠被逐帧抹平，弯成爬升 S 线奔向玩家侧上方
                Vector2 anchor = owner.Center + new Vector2(-owner.direction * 130f, -150f);
                Vector2 want = (anchor - Projectile.Center).SafeNormalize(-Vector2.UnitY) * 15f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.055f);
                if (Vector2.Distance(Projectile.Center, anchor) < 130f || t > EmergeTimeout) {
                    EnterHabitat(isAir: true);
                }
                return;
            }

            //落湖：二次入水拍后水下刹车，弯回巡游层
            if (ArcPhase == 1 && Projectile.Center.Y > lakeY + 8f) {
                if (!reentrySplashed) {
                    reentrySplashed = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.85f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
                    if (viewed) {
                        Vector2 hit = new(Projectile.Center.X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 12);
                        KikasaDomainDeco.RippleAt(hit, 1.8f);
                        ShakeViewer(3f);
                    }
                }
                Projectile.velocity *= 0.88f;
                Projectile.velocity.Y *= 0.8f;
                if (Projectile.velocity.Length() < 6f || t > EmergeTimeout) {
                    EnterHabitat(isAir: false);
                }
            }
        }

        /// <summary>破水浪冠：毁灭者级——量级压过克眼一头</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 3.0f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(52f, 0f), 1.2f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(48f, 0f), 1.1f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-18f, 0f), 14);
            KikasaDomainDeco.SplashAt(hit + new Vector2(18f, 0f), 14);

            for (int i = 0; i < 30; i++) {
                float angle = -MathHelper.Pi * (0.1f + 0.8f * i / 29f);
                float speed = Main.rand.NextFloat(3.5f, 8.5f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-34f, 34f), -4f),
                    angle.ToRotationVector2() * speed,
                    Main.rand.NextBool(3) ? KikasaEyeBloodShot.BloodDeep : KikasaEyeBloodShot.BloodMain,
                    Main.rand.NextFloat(0.55f, 0.95f))?.Configure(Main.rand.Next(24, 40));
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-10f, 10f), -6f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(9f, 14.5f)),
                    KikasaEyeBloodShot.BloodMain * 0.9f,
                    Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(36, 54));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-40f, 40f), -12f),
                    new Vector2(Main.rand.NextFloat(-0.35f, 0.35f), -Main.rand.NextFloat(0.4f, 0.9f)),
                    MistBlood * 0.85f, Main.rand.NextFloat(0.8f, 1.15f))
                    ?.Configure(Main.rand.Next(70, 110));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, KikasaEyeBloodShot.BloodDeep, 0.1f)
                ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.4f, 12);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.45f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.6f, Pitch = -0.75f, MaxInstances = 1 }, hit);
            ShakeViewer(7f);
        }

        /// <summary>栖居裁决：玩家远高于湖面转空中蟒行（带滞回的即时判据）</summary>
        private static bool ChooseHabitatIsAir(Player owner, KikasaDomainPlayer domain)
            => owner.Bottom.Y < domain.LakeWorldY - AirAboveLake;

        private void EnterHabitat(bool isAir) {
            State = isAir ? StateAirFollow : StateCruise;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = Math.Max(attackCooldown, 40);
            habitatHoldTimer = 0;
            Projectile.netUpdate = Main.myPlayer == Projectile.owner;
        }

        //==================== 双栖跟随 ====================

        private void UpdateCruise(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            cruisePhase += 0.045f + MathF.Abs(Projectile.velocity.X) * 0.004f;

            //巡游目标：玩家两侧游弋，纵向正弦穿越水线——背弓由轨迹自然形成
            float targetX = owner.Center.X + MathF.Sin(StateTimer * 0.013f + Seed) * 90f
                + (MathF.Sin(Seed * 3f + StateTimer * 0.0021f) > 0f ? 1f : -1f) * 220f;
            float targetY = lakeY - 6f + MathF.Sin(cruisePhase) * 44f;

            float dx = targetX - Projectile.Center.X;
            float wantVx = MathHelper.Clamp(dx * 0.05f, -11f, 11f);
            //跟丢加速，太远直接贴回
            if (MathF.Abs(dx) > 1600f) {
                wantVx = MathF.Sign(dx) * 22f;
            }
            if (MathF.Abs(dx) > 2600f) {
                Projectile.Center = new Vector2(owner.Center.X - owner.direction * 300f, lakeY + 30f);
                RebuildChain(Vector2.UnitX * owner.direction);
                Projectile.netUpdate = authority;
                return;
            }
            Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, wantVx, 0.08f);
            Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y,
                MathHelper.Clamp((targetY - Projectile.Center.Y) * 0.09f, -7f, 7f), 0.2f);

            UpdateHabitatSwitch(owner, domain, wantAir: true);
            TryStartAttack(owner, domain, authority);
        }

        private void UpdateAirFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            //空中蟒行：绕玩家侧上方锚点画利萨如小圈，链体自然波动
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 130f, -150f);
            anchor += new Vector2(MathF.Sin(StateTimer * 0.11f + Seed) * 70f,
                MathF.Sin(StateTimer * 0.073f + Seed * 2f) * 38f);

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2600f) {
                Projectile.Center = anchor;
                RebuildChain(Vector2.UnitX * owner.direction);
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.06f;
            const float maxSpeed = 16f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.09f);
            //蟒行不许死停，头总在游
            if (Projectile.velocity.Length() < 2.4f) {
                Projectile.velocity += (StateTimer * 0.11f + Seed).ToRotationVector2() * 0.5f;
            }

            UpdateHabitatSwitch(owner, domain, wantAir: false);
            TryStartAttack(owner, domain, authority);
        }

        /// <summary>双栖滞回：持续满足另一栖居的判据才切换，切换本身走过线水花</summary>
        private void UpdateHabitatSwitch(Player owner, KikasaDomainPlayer domain, bool wantAir) {
            float lakeY = domain.LakeWorldY;
            bool crossCondition = wantAir
                ? owner.Bottom.Y < lakeY - AirAboveLake
                : owner.Bottom.Y > lakeY - CruiseBelow;
            habitatHoldTimer = crossCondition ? habitatHoldTimer + 1 : 0;
            if (habitatHoldTimer >= HabitatSwitchHold && StateTimer > 60) {
                EnterHabitat(isAir: wantAir);
            }
        }

        private void TryStartAttack(Player owner, KikasaDomainPlayer domain, bool authority) {
            int target = FindTarget(owner);
            if (target < 0 || attackCooldown > 0 || StateTimer < 30) {
                return;
            }
            //高空目标压向喷柱：跳不到就不空跳
            bool targetTooHigh = Main.npc[target].Center.Y < domain.LakeWorldY - 620f
                && State == StateCruise;
            attackIndex++;
            bool useJet = targetTooHigh || attackIndex % 2 == 1;
            State = useJet ? StateJet : StateDiveRam;
            StateTimer = 0;
            StateParam = 0;
            spacingMul = 1f;
            Projectile.netUpdate = authority;
        }

        //==================== 血液喷柱 ====================

        private void UpdateJet(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            float lakeY = domain.LakeWorldY;
            int phase = (int)StateParam;   //0抬头 1后仰 2持续 3回摆
            int target = FindTarget(owner);
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center
                : Projectile.Center + (float.IsNaN(lockedHeadRot) ? -Vector2.UnitY : lockedHeadRot.ToRotationVector2()) * 400f;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //湖态先抬头出水；已在水上直接进后仰
                if (target < 0) {
                    EndAttack(authority, 50);
                    return;
                }
                if (Projectile.Center.Y < lakeY - 90f || t >= JetRaiseFrames * 2) {
                    lockedHeadRot = (aimPos - Projectile.Center).ToRotation();
                    NextPhase(1);
                    return;
                }
                Vector2 up = new(Projectile.Center.X, lakeY - 130f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    (up - Projectile.Center).SafeNormalize(-Vector2.UnitY) * 13f, 0.25f);
                return;
            }

            //瞄准慢跟：公平阀，喷柱不甩头
            float wantAngle = (aimPos - Projectile.Center).ToRotation();
            lockedHeadRot = float.IsNaN(lockedHeadRot) ? wantAngle
                : lockedHeadRot.AngleTowards(wantAngle, 0.02f);
            Vector2 aim = lockedHeadRot.ToRotationVector2();

            if (phase == 1) {
                //后仰蓄势：pow 迟发后拉 + 链身卷紧；收拢血珠 72% 静默
                float k = MathF.Pow(MathHelper.Clamp(t / (float)JetRearFrames, 0f, 1f), 5f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -aim * (2.5f + 13f * k), 0.3f);
                spacingMul = MathHelper.Lerp(spacingMul, 0.8f, 0.2f);
                if (!Main.dedServ && t < JetRearFrames * 0.72f && t % 2 == 0) {
                    Vector2 maw = spine[0] + aim * 34f * DrawScale;
                    Vector2 from = maw + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 130f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from, (maw - from) * 0.15f,
                        KikasaEyeBloodShot.BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(9);
                }
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Volume = 0.5f, Pitch = -0.85f, MaxInstances = 2 }, Projectile.Center);
                }
                if (t >= JetRearFrames) {
                    //开火拍：后坐 + 鞭浪沿链传播；喷柱弹幕只在 owner 端生成
                    Projectile.velocity = -aim * 9f;
                    whipPos = 0f;
                    whipAmp = 26f;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                    if (ViewedOwner) {
                        ShakeViewer(4f);
                    }
                    if (authority) {
                        int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(JetDamage);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            spine[0], Vector2.Zero,
                            ModContent.ProjectileType<KikasaDestroyerBloodJet>(), damage, 4f,
                            Projectile.owner);
                    }
                    NextPhase(2);
                }
                return;
            }

            if (phase == 2) {
                //持续期：顶着喷压悬停，微幅哆嗦
                spacingMul = MathHelper.Lerp(spacingMul, 0.9f, 0.1f);
                Projectile.velocity *= 0.82f;
                Projectile.velocity += Main.rand.NextVector2Circular(0.7f, 0.7f);
                if (t % 6 == 0 && ViewedOwner) {
                    ShakeViewer(1.6f);
                }
                if (t >= JetSustainFrames) {
                    NextPhase(3);
                }
                return;
            }

            //回摆
            spacingMul = MathHelper.Lerp(spacingMul, 1f, 0.15f);
            Projectile.velocity *= 0.9f;
            if (t >= JetRecoverFrames) {
                EndAttack(authority, 130);
            }
        }

        //==================== 潜浪冲撞 ====================

        private void UpdateDiveRam(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            float lakeY = domain.LakeWorldY;
            int phase = (int)StateParam;
            int target = FindTarget(owner);
            bool viewed = ViewedOwner;

            if (phase == 0) {
                //入水：空中态先俯冲；已在水下直接进冲刺
                if (Projectile.Center.Y > lakeY + 36f) {
                    StateParam = 1;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                    return;
                }
                Vector2 diveTo = new(Projectile.Center.X + MathF.Sign(Projectile.velocity.X) * 60f, lakeY + 80f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    (diveTo - Projectile.Center).SafeNormalize(Vector2.UnitY) * 19f, 0.16f);
                if (t > RamDiveFrames * 3 || target < 0) {
                    EndAttack(authority, 60);
                }
                return;
            }

            if (phase == 1) {
                //水下冲刺：贴湖下奔目标正下方，水面读出高速航迹；就位早退
                if (target < 0) {
                    EndAttack(authority, 50);
                    return;
                }
                NPC npc = Main.npc[target];
                float underX = npc.Center.X + npc.velocity.X * 10f;
                float dx = underX - Projectile.Center.X;
                Projectile.velocity.X = MathHelper.Clamp(
                    Projectile.velocity.X + MathF.Sign(dx) * 1.4f, -38f, 38f);
                Projectile.velocity.Y = MathHelper.Clamp((lakeY + 70f - Projectile.Center.Y) * 0.12f, -6f, 6f);

                if (viewed && t % 2 == 0) {
                    //高速航迹：涟漪 + 沿线泡沫珠
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY),
                        0.45f + MathF.Abs(Projectile.velocity.X) * 0.012f);
                    if (t % 4 == 0) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            new Vector2(Projectile.Center.X, lakeY - 2f),
                            new Vector2(Projectile.velocity.X * 0.06f, -Main.rand.NextFloat(1.5f, 3f)),
                            KikasaEyeBloodShot.BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(10, 18));
                    }
                }

                if (MathF.Abs(dx) < 60f || t >= RamSprintMax) {
                    StateParam = 2;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 2) {
                //跃出穿体：一帧定向起跳，激活窗低重力
                if (!leapLaunched) {
                    leapLaunched = true;
                    Vector2 aimPos = target >= 0
                        ? Main.npc[target].Center + Main.npc[target].velocity * 8f
                        : Projectile.Center - Vector2.UnitY * 500f;
                    Vector2 aim = (aimPos - Projectile.Center).SafeNormalize(-Vector2.UnitY);
                    //至少上扬 20°，不贴水面平扫
                    if (aim.Y > -0.34f) {
                        aim = new Vector2(MathF.Sign(aim.X) * MathF.Cos(0.35f), -MathF.Sin(0.35f));
                    }
                    Projectile.velocity = aim * 34f;
                    ghostSnaps[0] = ghostSnaps[1] = null;
                    ghostSnapTick = 0;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.55f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                    if (viewed) {
                        ShakeViewer(4f);
                    }
                }
                //激活窗低重力，过窗后重量收回
                Projectile.velocity.Y += t <= RamLeapFrames ? 0.30f : 0.7f;
                //残影快照
                if (t <= RamLeapFrames && ++ghostSnapTick % 4 == 0) {
                    ghostSnaps[1] = ghostSnaps[0];
                    ghostSnaps[0] = (Vector2[])spine.Clone();
                }
                if (t > RamLeapFrames + RamRecoverFrames
                    || (t > RamLeapFrames && Projectile.Center.Y > lakeY + 60f)) {
                    EndAttack(authority, 120);
                }
                return;
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            Player owner = Owner;
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();
            State = ChooseHabitatIsAir(owner, domain) ? StateAirFollow : StateCruise;
            StateTimer = 0;
            StateParam = 0;
            spacingMul = 1f;
            attackCooldown = cooldown;
            habitatHoldTimer = 0;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解遣返 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;

            if (lakeAlive) {
                //头先沉，链体跟着穿回水里
                Projectile.velocity.X *= 0.94f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 8f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            //化水残珠沿链错拍
            if (!Main.dedServ && t % 3 == 0) {
                int i = Main.rand.Next(SegCount);
                if (SegDissolve(i) is > 0.1f and < 0.9f) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        spine[i] + Main.rand.NextVector2Circular(18f, 18f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.2f, 2.6f)),
                        KikasaEyeBloodShot.BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(14, 24));
                }
            }

            if (authority && t >= DissolveTotal) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveTotal + 10) {
                Projectile.Kill();
            }
        }

        /// <summary>逐节溶解进度：尾先化、头最后</summary>
        private float SegDissolve(int i) {
            if (State != StateDissolve) {
                return 0f;
            }
            float start = (SegCount - 1 - i) * DissolvePerSegGap;
            return MathHelper.Clamp((StateTimer - start) / DissolveSegFrames, 0f, 1f);
        }

        //==================== 链体推进 ====================

        /// <summary>头位硬纠或初始化时沿指定方向直线重建，防路径抽搐</summary>
        private void RebuildChain(Vector2 headDir) {
            spineInit = true;
            Vector2 head = Projectile.Center;
            Vector2 back = -headDir.SafeNormalize(Vector2.UnitX);
            pathCount = 0;
            pathHead = 0;
            //远端先入、头最后入——环形缓冲最新位必须是头
            for (int i = PathCap - 1; i >= 0; i--) {
                PushPath(head + back * (i * PathSampleDist * 2f));
            }
            for (int i = 0; i < SegCount; i++) {
                spine[i] = head + back * (i * SegSpacing);
                segRot[i] = headDir.ToRotation();
                belowWater[i] = true;
                wetness[i] = 1f;
            }
        }

        private void PushPath(Vector2 pos) {
            pathHead = (pathHead - 1 + PathCap) % PathCap;
            path[pathHead] = pos;
            if (pathCount < PathCap) {
                pathCount++;
            }
        }

        private void UpdateChain(KikasaDomainPlayer domain) {
            //本帧渲染位 = Center + velocity（AI 在位移积分前跑）
            Vector2 head = Projectile.Center + Projectile.velocity;

            //硬纠检测：同步包把头拽走半屏，路径直线重建
            if (pathCount > 0 && Vector2.Distance(path[pathHead], head) > 120f) {
                RebuildChain(Projectile.velocity.SafeNormalize(Vector2.UnitX));
                return;
            }
            if (pathCount == 0 || Vector2.Distance(path[pathHead], head) >= PathSampleDist) {
                PushPath(head);
            }

            //体节沿路径回溯弧长摆位；喷柱期头向被锁定，不吃速度抖动
            spine[0] = head;
            if (!float.IsNaN(lockedHeadRot)) {
                segRot[0] = lockedHeadRot;
            }
            else if (Projectile.velocity.Length() > 0.5f) {
                segRot[0] = Projectile.velocity.ToRotation();
            }

            float spacing = SegSpacing * spacingMul;
            int cursor = 0;
            float walked = 0f;
            for (int i = 1; i < SegCount; i++) {
                float want = i * spacing;
                //从游标继续向旧样本走
                while (cursor < pathCount - 1) {
                    Vector2 a = path[(pathHead + cursor) % PathCap];
                    Vector2 b = path[(pathHead + cursor + 1) % PathCap];
                    float segLen = Vector2.Distance(a, b);
                    if (walked + segLen >= want) {
                        float f = segLen > 0.01f ? (want - walked) / segLen : 0f;
                        spine[i] = Vector2.Lerp(a, b, f);
                        goto placed;
                    }
                    walked += segLen;
                    cursor++;
                }
                //路径不够长：沿末向延伸
                spine[i] = spine[i - 1] + (spine[i - 1] - (i >= 2 ? spine[i - 2] : head))
                    .SafeNormalize(Vector2.UnitX) * spacing;
            placed:
                segRot[i] = (spine[i - 1] - spine[i]).ToRotation();
            }

            UpdateSegmentCrossings(domain);
        }

        /// <summary>逐节过水线（双向）：水花帧内限量、音效只给第一个；出水节湿度拉满</summary>
        private void UpdateSegmentCrossings(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            bool viewed = ViewedOwner;
            int fxBudget = 2;
            bool soundLeft = true;

            for (int i = 0; i < SegCount; i++) {
                bool below = spine[i].Y >= lakeY;
                if (below != belowWater[i]) {
                    belowWater[i] = below;
                    wetness[i] = 1f;
                    if (lakeAlive && viewed && fxBudget > 0) {
                        fxBudget--;
                        Vector2 hit = new(spine[i].X, lakeY);
                        KikasaDomainDeco.RippleAt(hit, i == 0 ? 0.9f : 0.55f);
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                hit + new Vector2(Main.rand.NextFloat(-12f, 12f), -3f),
                                new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(2f, 4.5f)),
                                KikasaEyeBloodShot.BloodMain * 0.6f, Main.rand.NextFloat(0.35f, 0.6f))
                                ?.Configure(Main.rand.Next(14, 26));
                        }
                        if (soundLeft) {
                            soundLeft = false;
                            SoundEngine.PlaySound(SoundID.SplashWeak with {
                                Volume = 0.4f,
                                Pitch = -0.3f + i * 0.015f,
                                MaxInstances = 3
                            }, hit);
                        }
                    }
                }
                //水下恒湿，出水后慢慢淌干
                wetness[i] = below ? 1f : MathF.Max(0f, wetness[i] - 0.011f);
            }
        }

        /// <summary>开火后坐鞭浪推进：波位沿链走、振幅衰减</summary>
        private void UpdateWhip() {
            if (whipPos < 0f) {
                return;
            }
            whipPos += 0.8f;
            whipAmp *= 0.93f;
            if (whipPos > SegCount + 3 || whipAmp < 0.6f) {
                whipPos = -1f;
            }
        }

        //==================== 公共小件 ====================

        private int FindTarget(Player owner) {
            if (owner.HasMinionAttackTargetNPC) {
                NPC picked = Main.npc[owner.MinionAttackTargetNPC];
                if (picked.CanBeChasedBy(Projectile)
                    && Vector2.Distance(picked.Center, owner.Center) < 1600f) {
                    return picked.whoAmI;
                }
            }
            int best = -1;
            float bestDist = 1100f;
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

        private void UpdateFrames() {
            if (++frameTick >= 5) {
                frameTick = 0;
                frameIndex = (frameIndex + 1) % 4;
            }
        }

        /// <summary>湿度驱动滴落：全身预算内错拍，刚出水的节淌得最凶</summary>
        private void UpdateDrips(KikasaDomainPlayer domain) {
            if (Main.dedServ) {
                return;
            }
            int budget = 3;
            for (int k = 0; k < 4 && budget > 0; k++) {
                int i = Main.rand.Next(SegCount);
                if (belowWater[i] || wetness[i] < 0.1f) {
                    continue;
                }
                //湿度即概率
                if (Main.rand.NextFloat() > wetness[i] * 0.45f) {
                    continue;
                }
                budget--;
                Vector2 pos = spine[i] + Main.rand.NextVector2Circular(20f, 16f) * DrawScale / 0.55f;
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                    new Vector2(Projectile.velocity.X * 0.05f, Main.rand.NextFloat(0.8f, 1.8f)),
                    (Main.rand.NextBool(3) ? KikasaEyeBloodShot.BloodDeep : KikasaEyeBloodShot.BloodMain)
                        * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(18, 32), 0.3f);
            }
        }

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        /// <summary>喷柱锚定用：头位与头向（口器前方）</summary>
        internal Vector2 HeadPos => spine[0];
        internal float HeadRot => segRot[0];

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!spineInit) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            //跃出残影：链体旧快照平染（主批直接画）
            DrawGhostChains(sb);

            //本体：血湖材质逐节
            DrawChain(sb, lightColor);

            //辉光层 + 水下血光：加色批
            DrawGlowLayer(sb);

            return false;
        }

        private void GetSegDraw(int i, out Texture2D tex, out Texture2D glow, out Rectangle frame) {
            if (i == 0) {
                tex = DestroyerHeadAI.Head.Value;
                glow = DestroyerHeadAI.Head_Glow.Value;
                frame = tex.GetRectangle(frameIndex, 4);
                return;
            }
            if (i == SegCount - 1) {
                tex = DestroyerBodyAI.Tail.Value;
                glow = DestroyerBodyAI.Tail_Glow.Value;
                frame = tex.GetRectangle((frameIndex + i) % 4, 4);
                return;
            }
            if (i % 2 == 0) {
                tex = DestroyerBodyAI.BodyAlt.Value;
                glow = DestroyerBodyAI.BodyAlt_Glow.Value;
                frame = tex.GetRectangle();
                return;
            }
            tex = DestroyerBodyAI.Body.Value;
            glow = DestroyerBodyAI.Body_Glow.Value;
            frame = tex.GetRectangle((frameIndex + i) % 4, 4);
        }

        /// <summary>鞭浪横向位移：波前高斯衰减</summary>
        private Vector2 WhipOffset(int i) {
            if (whipPos < 0f) {
                return Vector2.Zero;
            }
            float d = i - whipPos;
            float gauss = MathF.Exp(-d * d * 0.35f);
            if (gauss < 0.05f) {
                return Vector2.Zero;
            }
            Vector2 perp = (segRot[i] + MathHelper.PiOver2).ToRotationVector2();
            return perp * MathF.Sin(i * 0.9f + Seed) * whipAmp * gauss;
        }

        private void DrawGhostChains(SpriteBatch sb) {
            if (State != StateDiveRam || (int)StateParam != 2) {
                return;
            }
            for (int s = 1; s >= 0; s--) {
                Vector2[] snap = ghostSnaps[s];
                if (snap == null) {
                    continue;
                }
                float alpha = s == 0 ? 0.3f : 0.16f;
                for (int i = SegCount - 1; i >= 0; i--) {
                    GetSegDraw(i, out Texture2D tex, out _, out Rectangle frame);
                    float rot = (i == 0 ? segRot[0]
                        : (snap[i - 1] - snap[i]).ToRotation()) + MathHelper.PiOver2 + MathHelper.Pi;
                    sb.Draw(tex, snap[i] - Main.screenPosition, frame, BloodMain * alpha,
                        rot, frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawChain(SpriteBatch sb, Color lightColor) {
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
                form.Parameters["uScanMode"]?.SetValue(0f);
            }

            //尾→头，头压顶层
            for (int i = SegCount - 1; i >= 0; i--) {
                float dissolve = SegDissolve(i);
                if (dissolve >= 1f) {
                    continue;
                }
                GetSegDraw(i, out Texture2D tex, out _, out Rectangle frame);
                Vector2 pos = spine[i] + WhipOffset(i) - Main.screenPosition;
                float rot = segRot[i] + MathHelper.PiOver2 + MathHelper.Pi;

                Color color;
                if (shaderOk) {
                    //比克眼更血水的基底 + 节湿度加成
                    float segForm = MathHelper.Clamp(0.5f + wetness[i] * 0.22f
                        + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + Seed + i * 0.8f) * 0.04f, 0f, 0.95f);
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 1.7f);
                    form.Parameters["uForm"]?.SetValue(segForm);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.Parameters["uUvRect"]?.SetValue(new Vector4(
                        frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                        frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                    form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                    form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = Color.White;
                }
                else {
                    color = Color.Lerp(lightColor, BloodMain, 0.55f) * (1f - dissolve);
                }

                sb.Draw(tex, pos, frame, color, rot, frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>辉光层（探针灯→血灯）+ 潜行/冲刺的水下血光</summary>
        private void DrawGlowLayer(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //血灯呼吸
            for (int i = SegCount - 1; i >= 0; i--) {
                float dissolve = SegDissolve(i);
                if (dissolve >= 1f) {
                    continue;
                }
                GetSegDraw(i, out _, out Texture2D glow, out Rectangle frame);
                float pulse = 0.42f + 0.22f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f + i * 0.7f + Seed);
                Color c = (BloodMain with { A = 0 }) * (pulse * (1f - dissolve));
                sb.Draw(glow, spine[i] + WhipOffset(i) - Main.screenPosition, frame, c,
                    segRot[i] + MathHelper.PiOver2 + MathHelper.Pi,
                    frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
            }

            //水下段的行进血光：潜行预兆与水下冲刺时头顶水面拖出光斑
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            if (softGlow != null && Owner.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                bool approach = State == StateEmerge && StateTimer <= ApproachFrames;
                bool sprint = State == StateDiveRam && (int)StateParam == 1;
                if ((approach || sprint) && ViewedOwner) {
                    float speedK = MathHelper.Clamp(MathF.Abs(Projectile.velocity.X) / 30f, 0.3f, 1f);
                    Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + 8f);
                    float r = 30f + 26f * speedK;
                    Color glow2 = KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
                    sb.Draw(softGlow, pos - Main.screenPosition, null, glow2 * (0.4f * speedK), 0f,
                        softGlow.Size() * 0.5f,
                        new Vector2(r * 3.2f / softGlow.Width, r * 0.9f / softGlow.Height), SpriteEffects.None, 0f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(24f, 24f),
                    Projectile.velocity * 0.22f + Main.rand.NextVector2Circular(2.8f, 2.8f),
                    KikasaEyeBloodShot.BloodMain * 0.6f, Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(16, 28), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !spineInit) {
                return;
            }
            //谢幕残珠沿链散
            for (int i = 0; i < SegCount; i += 2) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    spine[i] + Main.rand.NextVector2Circular(14f, 14f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.4f)),
                    KikasaEyeBloodShot.BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(spine[SegCount / 2],
                new Vector2(0f, -0.2f), MistBlood * 0.7f, Main.rand.NextFloat(0.7f, 1f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
