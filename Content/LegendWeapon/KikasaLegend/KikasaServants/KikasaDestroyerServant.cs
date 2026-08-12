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

        internal const int SegCount = 16;
        internal const float DrawScale = 0.7f;
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

        //喷射（对齐 LaserBarrage 语义）：抬头(仅湖态)→昂首定位/锁线→静默→喷射横扫→散热回摆
        private const int JetRaiseMax = 24;
        private const int JetPoiseFrames = 30;
        private const int JetSilenceFrames = 8;
        private const int JetRecoverFrames = 18;
        /// <summary>横扫半弧</summary>
        private const float JetArcHalf = 0.85f;

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

        //BTD 本体同款跟随：每节是独立对象，持有自己的位置与旋转，
        //目标向量先按与前节的转差做 0.18 阻尼旋转再贴到前节后方
        private readonly Vector2[] spine = new Vector2[SegCount];
        /// <summary>蠕虫约定旋转（指向前节的方向角 + PiOver2），与 BTD 本体一致</summary>
        private readonly float[] segRot = new float[SegCount];
        /// <summary>节湿度：过水线拉满、出水后衰减，驱动滴落与材质血水度</summary>
        private readonly float[] wetness = new float[SegCount];
        private readonly bool[] belowWater = new bool[SegCount];
        private bool spineInit;

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
        /// <summary>喷射期头部朝向锁（NaN=不锁，方向角语义），激光弹幕的锚定角来源</summary>
        private float lockedHeadRot = float.NaN;
        /// <summary>本次喷射的横扫参数（锁线帧定参，各端同规则自算；远端兜底从激光弹幕读）</summary>
        private float jetStartAngle = float.NaN;
        private float jetSweepSpeed;
        private Vector2 jetAnchor;
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
            jetStartAngle = float.NaN;
            Projectile.netUpdate = authority;
        }

        //==================== 血液喷柱 ====================

        private void UpdateJet(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            float lakeY = domain.LakeWorldY;
            int phase = (int)StateParam;   //0抬头 1昂首定位/锁线 2静默 3喷射横扫 4散热回摆
            int target = FindTarget(owner);
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center
                : Projectile.Center + (float.IsNaN(lockedHeadRot) ? -Vector2.UnitY : lockedHeadRot.ToRotationVector2()) * 400f;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //湖态先抬头出水；已在水上直接进定位
                if (target < 0) {
                    EndAttack(authority, 50);
                    return;
                }
                if (Projectile.Center.Y < lakeY - 90f || t >= JetRaiseMax) {
                    lockedHeadRot = (aimPos - Projectile.Center).ToRotation();
                    jetAnchor = Projectile.Center;
                    NextPhase(1);
                    return;
                }
                Vector2 up = new(Projectile.Center.X, lakeY - 130f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    (up - Projectile.Center).SafeNormalize(-Vector2.UnitY) * 13f, 0.25f);
                return;
            }

            if (phase == 1) {
                //昂首定位：抓锚冻结、鼻锁目标、蓄力反向漂移（LaserBarrage.UpdatePoise 语义）
                float p = MathHelper.Clamp(t / (float)JetPoiseFrames, 0f, 1f);
                Vector2 aimDir0 = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Vector2 holdPos = jetAnchor - aimDir0 * (p * p * 90f);
                Projectile.velocity *= 0.86f;
                Projectile.velocity += (holdPos - Projectile.Center) * 0.02f;
                if (Projectile.velocity.Length() > 9f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 9f;
                }
                //鼻锁：转率随蓄力衰减——"锁线"
                float wantAngle = (aimPos - Projectile.Center).ToRotation();
                lockedHeadRot = lockedHeadRot.AngleTowards(wantAngle, MathHelper.Lerp(0.24f, 0.1f, p));

                //口器向心汇聚流光（本体同款橙热），72% 后停粒子——尖啸前的吸气
                if (!Main.dedServ && p < 0.72f && Main.rand.NextFloat() < 0.35f + 0.5f * p) {
                    Vector2 from = spine[0] + Main.rand.NextVector2Unit() * Main.rand.NextFloat(90f, 320f);
                    PRTLoader.NewParticle<PRT_Spark>(from, (spine[0] - from) * 0.1f,
                        Color.Lerp(new Color(255, 150, 70), Color.White, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.9f, 1.5f))?.Configure(false, 15);
                }
                //低鸣震屏随蓄力平方爬升
                if (t % 7 == 0 && ViewedOwner) {
                    ShakeViewer(0.8f + 2f * p * p);
                }
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.75f, Pitch = -0.55f, MaxInstances = 2 }, Projectile.Center);
                }

                if (t >= JetPoiseFrames) {
                    //锁线定参：从目标一侧扫过另一侧，扫向由相对位取定（各端同规则）
                    float aimAngle = (aimPos - Projectile.Center).ToRotation();
                    float side = aimPos.X >= Projectile.Center.X ? 1f : -1f;
                    jetStartAngle = aimAngle - JetArcHalf * side;
                    jetSweepSpeed = 2f * JetArcHalf / KikasaDestroyerBloodJet.SweepFrames * side;
                    jetAnchor = Projectile.Center;
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = -0.8f, MaxInstances = 2 }, Projectile.Center);
                    NextPhase(2);
                }
                return;
            }

            if (phase == 2) {
                //静默：充能骤停、转向起始角——巨炮前的吸气
                Projectile.velocity *= 0.7f;
                lockedHeadRot = lockedHeadRot.AngleLerp(
                    float.IsNaN(jetStartAngle) ? lockedHeadRot : jetStartAngle, 0.4f);
                if (t >= JetSilenceFrames) {
                    //开火拍（LaserBarrage.FireBeam 语义）：后坐冲量 + 重拍；激光只在 owner 端生成
                    float startAngle = float.IsNaN(jetStartAngle)
                        ? lockedHeadRot : jetStartAngle;
                    Vector2 startDir = startAngle.ToRotationVector2();
                    lockedHeadRot = startAngle;
                    Projectile.velocity = -startDir * 9f;
                    SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = 0.85f, Pitch = 0.1f, MaxInstances = 2 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                    if (ViewedOwner) {
                        ShakeViewer(7f);
                    }
                    if (authority) {
                        int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(JetDamage);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            spine[0], Vector2.Zero,
                            ModContent.ProjectileType<KikasaDestroyerBloodJet>(), damage, 4f,
                            Projectile.owner, startAngle, jetSweepSpeed);
                    }
                    NextPhase(3);
                }
                return;
            }

            if (phase == 3) {
                //喷射横扫：持位刹车回拉稳住 pivot，口器跟权威光束角+高频微颤
                Projectile.velocity *= 0.9f;
                Projectile.velocity += (jetAnchor - Projectile.Center) * 0.012f;
                if (Projectile.velocity.Length() > 8f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 8f;
                }

                Projectile beam = KikasaDestroyerBloodJet.FindFor(Projectile.owner);
                float beamAngle = beam != null ? beam.rotation
                    : float.IsNaN(jetStartAngle) ? lockedHeadRot
                    : jetStartAngle + jetSweepSpeed * MathHelper.Clamp(
                        t - KikasaDestroyerBloodJet.ExpandFrames, 0f, KikasaDestroyerBloodJet.SweepFrames);
                lockedHeadRot = lockedHeadRot.AngleLerp(beamAngle, 0.5f)
                    + MathF.Sin(Main.GlobalTimeWrappedHourly * 46f) * 0.012f;

                if (t % 6 == 0 && ViewedOwner) {
                    ShakeViewer(1.8f);
                }
                if (t >= KikasaDestroyerBloodJet.TotalLife) {
                    NextPhase(4);
                }
                return;
            }

            //散热回摆
            Projectile.velocity *= 0.9f;
            if (t >= JetRecoverFrames) {
                EndAttack(authority, 140);
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
            jetStartAngle = float.NaN;
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

        //==================== 链体推进（BTD 本体同款跟随，DestroyerBodyAI.Move 移植）====================

        /// <summary>头位硬纠或初始化时沿指定方向直线重建，防链体抽搐</summary>
        private void RebuildChain(Vector2 headDir) {
            spineInit = true;
            Vector2 head = Projectile.Center;
            Vector2 back = -headDir.SafeNormalize(Vector2.UnitX);
            float wormRot = headDir.ToRotation() + MathHelper.PiOver2;
            for (int i = 0; i < SegCount; i++) {
                spine[i] = head + back * (i * SegSpacing);
                segRot[i] = wormRot;
                belowWater[i] = true;
                wetness[i] = 1f;
            }
        }

        private void UpdateChain(KikasaDomainPlayer domain) {
            //本帧渲染位 = Center + velocity（AI 在位移积分前跑）
            Vector2 head = Projectile.Center + Projectile.velocity;

            //硬纠检测：同步包把头拽走半屏，直线重建
            if (Vector2.Distance(spine[0], head) > 140f) {
                RebuildChain(Projectile.velocity.SafeNormalize(Vector2.UnitX));
                return;
            }

            spine[0] = head;
            //头旋转：喷射期锁瞄准角，否则随速度（蠕虫约定 = 方向角 + PiOver2）
            if (!float.IsNaN(lockedHeadRot)) {
                segRot[0] = lockedHeadRot + MathHelper.PiOver2;
            }
            else if (Projectile.velocity.Length() > 0.5f) {
                segRot[0] = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            //每节独立对象追前节：目标向量先按转差做阻尼旋转再贴位——本体手感的来源
            const float dampingInertia = 0.18f;
            for (int i = 1; i < SegCount; i++) {
                Vector2 segmentTarget = spine[i - 1] - spine[i];
                if (segRot[i - 1] != segRot[i]) {
                    segmentTarget = segmentTarget.RotatedBy(
                        MathHelper.WrapAngle(segRot[i - 1] - segRot[i]) * dampingInertia);
                    segmentTarget = segmentTarget.MoveTowards(
                        (segRot[i - 1] - segRot[i]).ToRotationVector2(), 1f);
                }
                segRot[i] = segmentTarget.ToRotation() + MathHelper.PiOver2;
                spine[i] = spine[i - 1] - segmentTarget.SafeNormalize(Vector2.Zero) * SegSpacing;
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
            int budget = 2;
            for (int k = 0; k < 3 && budget > 0; k++) {
                int i = Main.rand.Next(SegCount);
                if (belowWater[i] || wetness[i] < 0.1f) {
                    continue;
                }
                //湿度即概率
                if (Main.rand.NextFloat() > wetness[i] * 0.45f) {
                    continue;
                }
                budget--;
                Vector2 pos = spine[i] + Main.rand.NextVector2Circular(24f, 18f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                    new Vector2(Projectile.velocity.X * 0.05f, Main.rand.NextFloat(0.8f, 1.8f)),
                    (Main.rand.NextBool(3) ? KikasaEyeBloodShot.BloodDeep : KikasaEyeBloodShot.BloodMain)
                        * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(18, 32), 0.3f);
            }
        }

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        /// <summary>喷柱锚定用：头位与头向（方向角语义；segRot 存的是蠕虫约定 +PiOver2）</summary>
        internal Vector2 HeadPos => spine[0];
        internal float HeadRot => segRot[0] - MathHelper.PiOver2;

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

        private void DrawGhostChains(SpriteBatch sb) {
            if (State != StateDiveRam || (int)StateParam != 2) {
                return;
            }
            for (int s = 1; s >= 0; s--) {
                Vector2[] snap = ghostSnaps[s];
                if (snap == null) {
                    continue;
                }
                float alpha = s == 0 ? 0.24f : 0.12f;
                for (int i = SegCount - 1; i >= 0; i--) {
                    GetSegDraw(i, out Texture2D tex, out _, out Rectangle frame);
                    float rot = i == 0 ? segRot[0] + MathHelper.Pi
                        : (snap[i - 1] - snap[i]).ToRotation() + MathHelper.PiOver2 + MathHelper.Pi;
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
                Vector2 pos = spine[i] - Main.screenPosition;
                float rot = segRot[i] + MathHelper.Pi;

                Color color;
                if (shaderOk) {
                    //贴图为主体、血水只是浸润层；节湿度短暂冲高后淌干
                    float segForm = MathHelper.Clamp(0.28f + wetness[i] * 0.15f
                        + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + Seed + i * 0.8f) * 0.04f, 0f, 0.55f);
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

            //血灯呼吸：压向深血、低亮度——加色粉光盖贴图是泡沫感主凶
            Color lampTint = Color.Lerp(BloodMain, KikasaEyeBloodShot.BloodDeep, 0.55f);
            for (int i = SegCount - 1; i >= 0; i--) {
                float dissolve = SegDissolve(i);
                if (dissolve >= 1f) {
                    continue;
                }
                GetSegDraw(i, out _, out Texture2D glow, out Rectangle frame);
                float pulse = 0.22f + 0.10f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f + i * 0.7f + Seed);
                Color c = (lampTint with { A = 0 }) * (pulse * (1f - dissolve));
                sb.Draw(glow, spine[i] - Main.screenPosition, frame, c,
                    segRot[i] + MathHelper.Pi,
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
