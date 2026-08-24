using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaArmsPalette;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaSpears
{
    /// <summary>
    /// 械奴·湖水矛阵（通用矛奴）。单弹幕驱动至多三杆湖水凝成的长矛：
    /// 矛的身份在直线——轮转突刺走蓄-刺-驻三拍（矛体亲自穿越，
    /// 判定由 <see cref="KikasaSpearThrust"/> 贯线事件承担），
    /// 隔次合围齐刺（三矛环位刺尖内指、齐发穿心）。
    /// 原版矛弹幕锚死玩家挥舞不可借，行程全部自演。
    /// 联机契约与通用械奴同构：owner 裁决转场、刺痕只在 authority 生成、
    /// 节拍闩防快照回卷、矛数与武器类型 spawn 后经 ExtraAI 随包补发
    /// </summary>
    internal class KikasaSpearServant : ModProjectile, IKikasaArmsServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>湖水刺基伤（召唤加成与档案倍率前）</summary>
        internal const int ThrustDamage = 165;

        /// <summary>合围齐刺重拍倍率</summary>
        internal const float SurroundMul = 1.3f;

        /// <summary>编队硬上限</summary>
        internal const int MaxSpears = 3;

        /// <summary>突刺行程 px（矛体穿越距离）</summary>
        private const float LungeDist = 190f;

        //==================== 档案 ====================

        private int armsItemType = ItemID.Spear;

        public int ArmsItemType => armsItemType;

        private KikasaSpearProfile? profileCache;

        private KikasaSpearProfile Profile => profileCache ??= KikasaArmsProfiler.SpearProfileOf(armsItemType);

        private void SetArmsItemType(int itemType) {
            armsItemType = itemType;
            profileCache = null;
        }

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateRelay = 2;
        private const int StateSurround = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：保位与通用械奴同构，当前未用</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        private const int OmenFrames = 26;
        private const int BreachGap = 7;
        private const int RiseEnd = 68;
        private const int FormupFrame = 78;
        private const int EmergeTotal = 94;
        private const float EmergeSpan = 56f;

        //轮转突刺：逐矛轮值一段 TurnLen——蓄（拉后）→刺（3f 穿越）→驻（几何冻住）→回位
        private const int LungeWindup = 12;
        private const int LungeFrames = 3;
        private const int LungeHold = 10;

        /// <summary>单矛轮值段长：走档案接力节拍</summary>
        private int RelayTurnLen => Profile.ThrustPeriod;

        private int RelayTotal => RelayTurnLen * spearCount + 14;

        //合围齐刺：冲环位→静谷蓄势→齐刺穿心→驻→归
        private const int SurroundDashEnd = 18;
        private const int SurroundLungeFrame = 36;
        private const int SurroundTotal = 78;
        private const float SurroundRadius = 160f;

        private const int DissolveStagger = 5;
        private const int DissolveFrames = 70;

        //==================== 各矛本地模拟 ====================

        private readonly Vector2[] spearPos = new Vector2[MaxSpears];
        private readonly Vector2[] spearVel = new Vector2[MaxSpears];
        private readonly Vector2[] spearTarget = new Vector2[MaxSpears];
        /// <summary>矛轴世界向（矛尖方向），绘制补 π/4 斜置、不镜像</summary>
        private readonly float[] spearRot = new float[MaxSpears];
        private readonly float[] spearSpin = new float[MaxSpears];
        private readonly Vector2[][] spearOld = new Vector2[MaxSpears][];
        private readonly float[][] spearOldRot = new float[MaxSpears][];
        private bool spearsInit;

        private int spearCount = MaxSpears;

        //==================== 本地表现量 ====================

        private readonly bool[] breachDone = new bool[MaxSpears];
        private readonly int[] lastFireTick = new int[MaxSpears];
        private readonly bool[] dissolveSplashed = new bool[MaxSpears];
        /// <summary>突刺声明闩：冲线起点与刺向在窗口首帧声明（跳帧进窗也补上）</summary>
        private readonly Vector2[] lungeFrom = new Vector2[MaxSpears];
        private readonly float[] lungeAng = new float[MaxSpears];
        private readonly bool[] lungeDeclared = new bool[MaxSpears];
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;
        private bool formSnapDone;
        private bool dashWhooshDone;

        private Player Owner => Main.player[Projectile.owner];
        private float Seed => Projectile.identity * 0.6173f;

        //==================== 召唤入口 ====================

        internal static void Summon(Player owner, Vector2 emergeAt, int count, int itemType) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            KikasaSpearProfile profile = KikasaArmsProfiler.SpearProfileOf(itemType);
            count = Math.Clamp(count, 1, profile.MaxUnits);
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ThrustDamage * profile.ThrustDamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaSpearServant>(), damage, 3f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaSpearServant pack) {
                pack.spearCount = count;
                pack.SetArmsItemType(itemType);
                Main.projectile[index].netUpdate = true;
            }
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = 180;
        }

        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(armsItemType);
            writer.Write((byte)spearCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int itemType = reader.ReadInt32();
            int count = reader.ReadByte();
            if (itemType > ItemID.None && itemType < ItemLoader.ItemCount && itemType != armsItemType) {
                SetArmsItemType(itemType);
            }
            count = Math.Clamp(count, 1, Profile.MaxUnits);
            if (count != spearCount) {
                spearCount = count;
                spearsInit = false;
            }
        }

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            if (State == StateEmerge && StateTimer < OmenFrames) {
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

            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ThrustDamage * Profile.ThrustDamageMul);

            if (State != lastSeenState) {
                lastSeenState = State;
                dashWhooshDone = false;
                Array.Fill(lastFireTick, -1);
                Array.Fill(lungeDeclared, false);
                if (State == StateDissolve) {
                    Array.Fill(dissolveSplashed, false);
                }
            }

            if (!spearsInit) {
                RebuildSpears(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateRelay: UpdateRelay(owner, authority); break;
                case StateSurround: UpdateSurround(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateSpears(owner, domain);
            PushSpearHistory();
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            for (int i = 0; i < spearCount; i++) {
                float glow = SpearAlpha(i) * 0.3f;
                if (glow > 0.02f) {
                    Lighting.AddLight(spearPos[i], 0.4f * glow, 0.1f * glow, 0.09f * glow);
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水 ====================

        private float BreachX(int i)
            => Projectile.Center.X + (i - (spearCount - 1) * 0.5f) * EmergeSpan;

        private static int BreachTime(int i) => OmenFrames + i * BreachGap;

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                Projectile.velocity = Vector2.Zero;
                if (viewed && t % 5 == 2) {
                    float converge = 1f - t / (float)OmenFrames;
                    for (int i = 0; i < spearCount; i++) {
                        float wobble = MathF.Sin(t * 0.5f + i * 1.7f) * converge * 24f;
                        KikasaDomainDeco.RippleAt(new Vector2(BreachX(i) + wobble, lakeY),
                            0.3f + (1f - converge) * 0.4f);
                    }
                }
                if (viewed && (t == 5 || t == 14 || t == 22)) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.42f, Pitch = -0.55f + t * 0.012f, MaxInstances = 3
                    }, new Vector2(Projectile.Center.X, lakeY));
                }
                return;
            }

            //错帧破水：矛尖朝天跃出——出水就是持械礼
            for (int i = 0; i < spearCount; i++) {
                if (!breachDone[i] && t >= BreachTime(i)) {
                    breachDone[i] = true;
                    spearVel[i] = new Vector2(0f, -13f - i * 0.3f);
                    spearSpin[i] = (i % 2 == 0 ? 1f : -1f) * 0.22f;
                    if (i == 0) {
                        Projectile.velocity = new Vector2(0f, -3.2f);
                    }
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.72f, Pitch = -0.38f + i * 0.07f, MaxInstances = 3
                    }, spearPos[i]);
                    if (viewed) {
                        BreachBurst(new Vector2(BreachX(i), lakeY), i);
                    }
                }
            }

            Projectile.velocity *= 0.96f;

            if (viewed && t < RiseEnd) {
                for (int i = 0; i < spearCount; i++) {
                    if (t < BreachTime(i) || t % 3 != i % 3) {
                        continue;
                    }
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        spearPos[i] + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(2f, 14f)),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.4f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(12, 24), 0f);
                }
            }

            //列阵顿拍：全员一顿，矛尾同点
            if (!formSnapDone && t >= FormupFrame) {
                formSnapDone = true;
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.48f, Pitch = -0.25f, MaxInstances = 2 }, Projectile.Center);
                for (int i = 0; i < spearCount; i++) {
                    spearVel[i] += new Vector2(
                        -MathF.Sign(spearPos[i].X - Projectile.Center.X) * 1.6f, -1.2f);
                }
                if (viewed) {
                    ShakeViewer(1.8f);
                }
            }

            if (t >= EmergeTotal) {
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        private void BreachBurst(Vector2 hit, int i) {
            KikasaDomainDeco.RippleAt(hit, 1.3f);
            KikasaDomainDeco.SplashAt(hit, 7);
            for (int k = 0; k < 10; k++) {
                float angle = -MathHelper.Pi * (0.18f + 0.64f * k / 9f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-12f, 12f), -4f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(2.6f, 5.6f),
                    BloodMain * Main.rand.NextFloat(0.45f, 0.65f),
                    Main.rand.NextFloat(0.4f, 0.66f))
                    ?.Configure(Main.rand.Next(18, 28), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            if (i == 0 || i == spearCount - 1) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = 0.3f, Pitch = -0.75f, MaxInstances = 1
                }, hit);
            }
            ShakeViewer(1.4f);
        }

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            Vector2 anchor = owner.Center + new Vector2(0f, -26f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildSpears(owner.GetModPlayer<KikasaDomainPlayer>());
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.09f;
            const float maxSpeed = 17f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);

            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                StateTimer = 0;
                StateParam = 0;
                State = attackIndex % 3 == 0 ? StateSurround : StateRelay;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 轮转突刺 ====================

        private void UpdateRelay(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= 12) {
                EndAttack(authority, 50);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 4f
                : Projectile.Center + new Vector2(owner.direction * 300f, 0f);

            //质心压向猎物侧翼中距离
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 anchor = focus - toT * 230f + new Vector2(0f, -20f);
            Vector2 desired = (anchor - Projectile.Center) * 0.1f;
            if (desired.Length() > 15f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 15f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.18f);

            int duty = t / RelayTurnLen;
            if (duty < spearCount) {
                int p = t - duty * RelayTurnLen;
                //蓄势起点声明冲线：从当前矛位刺向猎物（跳帧进窗也补声明）
                if (p >= 0 && !lungeDeclared[duty]) {
                    lungeDeclared[duty] = true;
                    lungeFrom[duty] = spearPos[duty];
                    lungeAng[duty] = (focus - spearPos[duty]).ToRotation();
                    SoundEngine.PlaySound(SoundID.Item1 with {
                        Volume = 0.26f, Pitch = -0.4f, MaxInstances = 3
                    }, spearPos[duty]);
                }
                //刺出帧：矛体已在穿越，贯线判定炸开
                if (p == LungeWindup && duty > lastFireTick[duty]) {
                    lastFireTick[duty] = duty;
                    FireThrust(owner, authority, duty, 1f);
                }
            }

            if (t >= RelayTotal) {
                EndAttack(authority, 90);
            }
        }

        /// <summary>贯线事件：沿声明的冲线生成刺痕（owner 端），矛体动画各端自演</summary>
        private void FireThrust(Player owner, bool authority, int i, float mul) {
            Vector2 dir = lungeAng[i].ToRotationVector2();
            Vector2 start = lungeFrom[i] + dir * (Profile.ReachLen * 0.4f);
            float lineLen = LungeDist + Profile.ReachLen;
            Vector2 center = start + dir * lineLen * 0.5f;

            SoundEngine.PlaySound(Profile.ThrustSound with {
                Volume = 0.42f, Pitch = 0.1f + i * 0.05f, MaxInstances = 4
            }, spearPos[i]);
            if (ViewedOwner) {
                ShakeViewer(1.2f * mul);
            }

            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                    .ApplyTo(ThrustDamage * Profile.ThrustDamageMul * mul);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), center, Vector2.Zero,
                    ModContent.ProjectileType<KikasaSpearThrust>(), damage, 4f, Projectile.owner,
                    lineLen * 0.5f, lungeAng[i]);
            }
        }

        //==================== 合围齐刺 ====================

        /// <summary>合围环角：矛位均布，Seed 定相</summary>
        private float SurroundAngle(int i)
            => Seed * 1.7f + i * MathHelper.TwoPi / Math.Max(spearCount, 1);

        private void UpdateSurround(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= SurroundDashEnd) {
                EndAttack(authority, 60);
                return;
            }

            if (!dashWhooshDone) {
                dashWhooshDone = true;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with {
                    Volume = 0.55f, Pitch = -0.1f, MaxInstances = 3
                }, Projectile.Center);
                if (ViewedOwner) {
                    ShakeViewer(1.8f);
                }
            }

            //质心贴住猎物：环心即锚
            if (target >= 0) {
                Vector2 want = (Main.npc[target].Center + Main.npc[target].velocity * 3f - Projectile.Center) * 0.14f;
                if (want.Length() > 20f) {
                    want = want.SafeNormalize(Vector2.Zero) * 20f;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.25f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            //齐刺帧：三矛穿心，判定线从环位过环心
            if (t == SurroundLungeFrame) {
                for (int i = 0; i < spearCount; i++) {
                    if (0 > lastFireTick[i]) {
                        lastFireTick[i] = 0;
                        lungeFrom[i] = Projectile.Center + SurroundAngle(i).ToRotationVector2() * SurroundRadius;
                        lungeAng[i] = (SurroundAngle(i) + MathHelper.Pi);
                        FireThrust(owner, authority, i, SurroundMul);
                    }
                }
                //心点撞拍
                SoundEngine.PlaySound(SoundID.Item71 with {
                    Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2
                }, Projectile.Center);
                if (ViewedOwner) {
                    ShakeViewer(2.6f);
                }
            }

            if (t >= SurroundTotal) {
                EndAttack(authority, 130);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;

            if (lakeAlive) {
                Projectile.velocity.X *= 0.94f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.2f, 8f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            for (int i = 0; i < spearCount; i++) {
                int lt = t - i * DissolveStagger;
                if (lakeAlive && !dissolveSplashed[i] && lt >= 0 && spearPos[i].Y >= lakeY) {
                    dissolveSplashed[i] = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.55f, Pitch = -0.4f + i * 0.08f, MaxInstances = 3
                    }, spearPos[i]);
                    if (ViewedOwner) {
                        Vector2 hit = new(spearPos[i].X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 6);
                        KikasaDomainDeco.RippleAt(hit, 0.9f);
                        ShakeViewer(1f);
                    }
                }
            }

            if (!Main.dedServ && SpearAlpha(0) > 0.15f) {
                int i = t % spearCount;
                if (t - i * DissolveStagger >= 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        spearPos[i] + Main.rand.NextVector2Circular(18f, 10f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.8f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(Main.rand.Next(12, 22), 0f);
                }
            }

            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
            }
        }

        //==================== 各矛推进 ====================

        private void RebuildSpears(KikasaDomainPlayer domain) {
            spearsInit = true;
            for (int i = 0; i < MaxSpears; i++) {
                if (State == StateEmerge) {
                    spearPos[i] = new Vector2(BreachX(i), domain.LakeWorldY + 26f);
                    spearRot[i] = -MathHelper.PiOver2;
                }
                else {
                    float phase = Main.GlobalTimeWrappedHourly * 0.6f + Seed + i * MathHelper.TwoPi / Math.Max(spearCount, 1);
                    spearPos[i] = Projectile.Center + new Vector2(MathF.Cos(phase) * 96f, MathF.Sin(phase) * 44f - 28f);
                    spearRot[i] = -MathHelper.PiOver2;
                }
                spearVel[i] = Vector2.Zero;
                spearSpin[i] = 0f;
                spearTarget[i] = spearPos[i];
                spearOld[i] ??= new Vector2[8];
                spearOldRot[i] ??= new float[8];
                for (int k = 0; k < spearOld[i].Length; k++) {
                    spearOld[i][k] = spearPos[i];
                    spearOldRot[i][k] = spearRot[i];
                }
            }
        }

        private void ChaseSpear(int i, float accel, float damp) {
            spearVel[i] = (spearVel[i] + (spearTarget[i] - spearPos[i]) * accel) * damp;
            spearPos[i] += spearVel[i];
        }

        private float Sway(int i, float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed + i * 2.4f) * amp;

        private void FaceSpear(int i, Vector2 worldPos, float rate) {
            float want = (worldPos - spearPos[i]).ToRotation();
            spearRot[i] = spearRot[i].AngleLerp(want, rate);
        }

        private void UpdateSpears(Player owner, KikasaDomainPlayer domain) {
            if (!spearsInit) {
                return;
            }
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            Vector2 targetPos = target >= 0 ? Main.npc[target].Center : owner.Center;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    for (int i = 0; i < spearCount; i++) {
                        if (t < BreachTime(i)) {
                            spearPos[i] = new Vector2(BreachX(i), lakeY + 26f);
                            spearVel[i] = Vector2.Zero;
                            spearTarget[i] = spearPos[i];
                            spearRot[i] = -MathHelper.PiOver2;
                            continue;
                        }
                        spearTarget[i] = new Vector2(BreachX(i), lakeY - 92f + Sway(i, 2.1f, 9f));
                        int lt = t - BreachTime(i);
                        if (lt < 14) {
                            spearVel[i].Y *= 0.955f;
                            spearVel[i].X *= 0.98f;
                            spearPos[i] += spearVel[i];
                            spearRot[i] += spearSpin[i];
                            spearSpin[i] *= 0.94f;
                        }
                        else {
                            ChaseSpear(i, 0.05f, 0.86f);
                            spearRot[i] += spearSpin[i];
                            spearSpin[i] *= 0.9f;
                            if (MathF.Abs(spearSpin[i]) < 0.05f) {
                                //收翻腾：矛尖归正朝天——仪仗立矛
                                spearRot[i] = spearRot[i].AngleLerp(-MathHelper.PiOver2, 0.14f);
                            }
                        }
                    }
                    break;
                }
                case StateFollow: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    for (int i = 0; i < spearCount; i++) {
                        float phase = tGlobal * 0.6f + Seed + i * MathHelper.TwoPi / spearCount;
                        Vector2 radial = new(MathF.Cos(phase) * 96f, MathF.Sin(phase) * 44f - 28f);
                        Vector2 slot = Projectile.Center + radial;
                        slot.Y += MathF.Sin(tGlobal * 2.3f + Seed * 2f + i * 1.9f) * 6f;
                        spearTarget[i] = slot;
                        ChaseSpear(i, 0.06f, 0.84f);

                        //歇姿立矛，有猎物时矛尖缓缓咬向
                        if (target >= 0) {
                            FaceSpear(i, targetPos, 0.08f);
                        }
                        else {
                            spearRot[i] = spearRot[i].AngleLerp(-MathHelper.PiOver2 + Sway(i, 1.2f, 0.1f), 0.06f);
                        }
                    }
                    break;
                }
                case StateRelay: {
                    Vector2 focus = target >= 0
                        ? Main.npc[target].Center + Main.npc[target].velocity * 4f
                        : Projectile.Center + new Vector2(owner.direction * 300f, 0f);
                    int duty = Math.Min(t / RelayTurnLen, spearCount - 1);
                    for (int i = 0; i < spearCount; i++) {
                        int p = t - i * RelayTurnLen;
                        bool myTurn = i == duty && p >= 0 && p < RelayTurnLen;
                        if (myTurn && lungeDeclared[i]) {
                            Vector2 dir = lungeAng[i].ToRotationVector2();
                            if (p < LungeWindup) {
                                //蓄：沿刺向拉后，尾端减速逼停
                                float w = p / (float)LungeWindup;
                                float back = MathF.Sin(w * MathHelper.PiOver2) * 46f;
                                spearTarget[i] = lungeFrom[i] - dir * back;
                                ChaseSpear(i, 0.2f, 0.72f);
                            }
                            else if (p < LungeWindup + LungeFrames) {
                                //刺：3 帧硬位移穿越，几何直给
                                float lp = (p - LungeWindup + 1) / (float)LungeFrames;
                                spearPos[i] = lungeFrom[i] + dir * (LungeDist * lp);
                                spearVel[i] = dir * 22f;
                                spearTarget[i] = spearPos[i];
                            }
                            else if (p < LungeWindup + LungeFrames + LungeHold) {
                                //驻：几何冻住，余劲衰减
                                spearVel[i] *= 0.7f;
                                spearPos[i] += spearVel[i] * 0.2f;
                                spearTarget[i] = spearPos[i];
                            }
                            else {
                                //回位
                                spearTarget[i] = RelayRestSlot(i, focus);
                                ChaseSpear(i, 0.09f, 0.8f);
                            }
                            spearRot[i] = spearRot[i].AngleLerp(lungeAng[i], p < LungeWindup ? 0.35f : 1f);
                        }
                        else {
                            //候场：斜列驻位盯猎物
                            spearTarget[i] = RelayRestSlot(i, focus);
                            ChaseSpear(i, 0.08f, 0.82f);
                            FaceSpear(i, focus, 0.14f);
                        }
                    }
                    break;
                }
                case StateSurround: {
                    for (int i = 0; i < spearCount; i++) {
                        Vector2 ring = Projectile.Center + SurroundAngle(i).ToRotationVector2() * SurroundRadius;
                        if (t <= SurroundDashEnd) {
                            spearTarget[i] = ring;
                            ChaseSpear(i, 0.16f, 0.78f);
                            //尖内指
                            spearRot[i] = spearRot[i].AngleLerp(SurroundAngle(i) + MathHelper.Pi, 0.25f);
                        }
                        else if (t < SurroundLungeFrame) {
                            //静谷蓄势：驻环位微颤
                            spearTarget[i] = ring + new Vector2(Sway(i, 6f, 2f), Sway(i, 5.3f, 2f));
                            ChaseSpear(i, 0.14f, 0.7f);
                            spearRot[i] = SurroundAngle(i) + MathHelper.Pi;
                        }
                        else if (t < SurroundLungeFrame + LungeFrames) {
                            //齐刺穿心：越过环心到对侧
                            float lp = (t - SurroundLungeFrame + 1) / (float)LungeFrames;
                            Vector2 dir = (SurroundAngle(i) + MathHelper.Pi).ToRotationVector2();
                            spearPos[i] = ring + dir * (SurroundRadius * 2f * lp);
                            spearVel[i] = dir * 24f;
                            spearTarget[i] = spearPos[i];
                            spearRot[i] = SurroundAngle(i) + MathHelper.Pi;
                        }
                        else {
                            //对侧驻帧后缓归
                            spearVel[i] *= 0.72f;
                            spearPos[i] += spearVel[i] * 0.2f;
                            spearTarget[i] = spearPos[i];
                        }
                    }
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    for (int i = 0; i < spearCount; i++) {
                        int lt = t - i * DissolveStagger;
                        if (lt < 0) {
                            continue;
                        }
                        spearVel[i].X *= 0.93f;
                        spearVel[i].Y = MathF.Min(spearVel[i].Y + 0.3f, 9.5f);
                        float droop = spearRot[i] + 0.4f;
                        spearRot[i] = spearRot[i].AngleLerp(droop, 0.02f);
                        spearPos[i] += spearVel[i];
                        spearTarget[i] = spearPos[i];
                    }
                    break;
                }
            }

            for (int i = 0; i < spearCount; i++) {
                if (!skipFix && Vector2.Distance(spearPos[i], spearTarget[i]) > 780f) {
                    spearPos[i] = spearTarget[i];
                    spearVel[i] = Vector2.Zero;
                }
            }
        }

        /// <summary>轮转候场驻位：猎物反侧斜列</summary>
        private Vector2 RelayRestSlot(int i, Vector2 focus) {
            Vector2 toT = (focus - Projectile.Center).SafeNormalize(Vector2.UnitX);
            Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
            float lane = i - (spearCount - 1) * 0.5f;
            return Projectile.Center - toT * 26f + perp * lane * 44f
                + new Vector2(0f, Sway(i, 2f, 4f));
        }

        private void PushSpearHistory() {
            for (int i = 0; i < spearCount; i++) {
                if (spearOld[i] == null) {
                    continue;
                }
                for (int k = spearOld[i].Length - 1; k >= 1; k--) {
                    spearOld[i][k] = spearOld[i][k - 1];
                    spearOldRot[i][k] = spearOldRot[i][k - 1];
                }
                spearOld[i][0] = spearPos[i];
                spearOldRot[i][0] = spearRot[i];
            }
        }

        private void UpdateAmbient() {
            if (Main.dedServ
                || State is not (StateFollow or StateRelay or StateSurround)) {
                return;
            }
            if (Main.rand.NextBool(16) && SpearAlpha(0) > 0.5f) {
                int i = Main.rand.Next(spearCount);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    spearPos[i] + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(6f, 12f)),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.1f)),
                    BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.28f, 0.5f))?.Configure(Main.rand.Next(16, 28), 0f);
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

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float SpearAlpha(int i) {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < BreachTime(i) ? 0f : MathHelper.Clamp((t - BreachTime(i)) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - (t - i * DissolveStagger)) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        private float SpearForm(int i) {
            int t = (int)StateTimer;
            float steady = 0.24f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + Seed + i * 1.3f) * 0.06f;
            return State switch {
                StateEmerge => t < BreachTime(i)
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp(
                        (t - BreachTime(i)) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + (t - i * DissolveStagger) / (float)DissolveFrames * 0.6f, 0f, 1f),
                _ => steady,
            };
        }

        private float DissolveAmt(int i) {
            if (State != StateDissolve) {
                return 0f;
            }
            int lt = (int)StateTimer - i * DissolveStagger;
            float p = MathF.Pow(MathHelper.Clamp(lt / 46f, 0f, 1f), 0.9f);
            return MathHelper.Clamp(p + (dissolveSplashed[i] ? 0.15f : 0f), 0f, 1f);
        }

        private float SpearScale(int i) {
            float scale = 1f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= BreachTime(i) && t < BreachTime(i) + 10) {
                scale *= 1f + 0.08f * (1f - (t - BreachTime(i)) / 10f);
            }
            return scale * Profile.DrawScale;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!spearsInit) {
                return false;
            }
            Main.instance.LoadItem(armsItemType);
            Texture2D tex = TextureAssets.Item[armsItemType]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            DrawDashTrails(sb, tex);
            DrawBodies(sb, tex);
            return false;
        }

        private void DrawDashTrails(SpriteBatch sb, Texture2D tex) {
            Vector2 origin = tex.Size() * 0.5f;
            for (int i = 0; i < spearCount; i++) {
                float trailA = MathHelper.Clamp((spearVel[i].Length() - 8f) / 12f, 0f, 1f) * SpearAlpha(i);
                if (trailA <= 0.03f || spearOld[i] == null) {
                    continue;
                }
                for (int k = spearOld[i].Length - 1; k >= 1; k--) {
                    float fall = 1f - k / (float)spearOld[i].Length;
                    sb.Draw(tex, spearOld[i][k] - Main.screenPosition, null,
                        BloodMain * (0.3f * fall * trailA), spearOldRot[i][k] + MathHelper.PiOver4,
                        origin, SpearScale(i) * (0.96f - k * 0.015f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawBodies(SpriteBatch sb, Texture2D tex) {
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
                form.Parameters["uScanMode"]?.SetValue(1f);
                form.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(tex.Width / (float)tex.Height);
            }

            Vector2 origin = tex.Size() * 0.5f;
            for (int i = 0; i < spearCount; i++) {
                float alpha = SpearAlpha(i);
                if (alpha <= 0.01f) {
                    continue;
                }
                //斜置画法：矛轴向补 π/4，不镜像（避水线翻面陷阱）
                float rot = spearRot[i] + MathHelper.PiOver4;
                Vector2 drawPos = spearPos[i] - Main.screenPosition;
                float dissolve = DissolveAmt(i);

                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed + i * 1.7f;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.7f, MathF.Cos(wt * 0.83f) * 2.1f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.035f;
                    float envScale = SpearScale(i) * (1.14f + MathF.Sin(wt * 1.6f) * 0.025f);
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 3.7f + 5.1f);
                    form.Parameters["uForm"]?.SetValue(1f);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(tex, drawPos + wobOff, null,
                        new Color(255, 255, 255, (byte)(alpha * 130f)),
                        rot + wobRot, origin, envScale, SpriteEffects.None, 0f);
                }

                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 3.7f);
                    form.Parameters["uForm"]?.SetValue(SpearForm(i));
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }
                sb.Draw(tex, drawPos, null, color, rot, origin, SpearScale(i), SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !spearsInit) {
                return;
            }
            for (int i = 0; i < spearCount; i++) {
                for (int k = 0; k < 4; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        spearPos[i] + Main.rand.NextVector2Circular(16f, 10f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.5f, 2.4f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(Main.rand.Next(12, 24), 0f);
                }
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.6f, Main.rand.NextFloat(0.5f, 0.75f))
                ?.Configure(Main.rand.Next(45, 70));
        }
    }
}
