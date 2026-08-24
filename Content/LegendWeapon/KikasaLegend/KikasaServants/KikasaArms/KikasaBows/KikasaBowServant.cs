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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaBows
{
    /// <summary>
    /// 械奴·湖水弓队（通用弓奴）。单弹幕驱动至多四张湖水凝成的弓：
    /// 质心权威同步、各弓位置本地推算（枪奴同范式）。弓的身份在节奏——
    /// 拉弦、满弦一顿、放箭，箭带坠弧；射手站位在主人身后（枪压前、弓坠后）。
    /// 出招池按档案原型：速射/制式=抛射排箭+箭雨压制，重弓=贯穿重箭轮值+排箭。
    /// 联机契约与枪奴同构：owner 裁决转场、弹只在 authority 生成、节拍闩防快照回卷、
    /// 弓数与武器类型 spawn 后经 ExtraAI 随包补发
    /// </summary>
    internal class KikasaBowServant : ModProjectile, IKikasaArmsServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>湖水箭基伤（召唤加成与档案倍率前）</summary>
        internal const int ArrowDamage = 165;

        /// <summary>箭雨单箭倍率折扣（箭多而密）</summary>
        internal const float RainMul = 0.8f;

        /// <summary>贯穿重箭倍率</summary>
        internal const float PierceMul = 2.6f;

        /// <summary>编队硬上限</summary>
        internal const int MaxBows = 4;

        //==================== 档案 ====================

        private int armsItemType = ItemID.WoodenBow;

        public int ArmsItemType => armsItemType;

        private KikasaBowProfile? profileCache;

        private KikasaBowProfile Profile => profileCache ??= KikasaArmsProfiler.BowProfileOf(armsItemType);

        private void SetArmsItemType(int itemType) {
            armsItemType = itemType;
            profileCache = null;
        }

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateVolley = 2;
        private const int StateRain = 3;
        private const int StatePierce = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：保位与通用械奴同构，当前未用</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：同枪奴——多点预兆、错帧破水、整队定弦拍
        private const int OmenFrames = 26;
        private const int BreachGap = 7;
        private const int RiseEnd = 72;
        private const int FormupFrame = 82;
        private const int EmergeTotal = 98;
        private const float EmergeSpan = 58f;

        //排箭：退入射位→拉弦锁线→轮转放箭（拉弦-满弦-释放循环）→收势
        private const int VolleyFormEnd = 16;
        private const int VolleyLockEnd = 30;
        private const int VolleyFireEnd = 100;
        private const int VolleyTotal = 116;

        //箭雨：爬升高位→仰角蓄势→三轮齐抛→看雨落
        private const int RainClimbEnd = 26;
        private const int RainSalvoGap = 24;
        private const int RainSalvos = 3;
        private const int RainTotal = 118;

        private static int RainSalvoFrame(int k) => RainClimbEnd + 8 + k * RainSalvoGap;

        //贯穿重箭（重弓档）：逐弓轮值，压步→满弦蓄力→重箭贯线
        private const int PierceTurnLen = 52;
        private const int PierceFireFrame = 40;
        private const int PierceTail = 16;

        private int PierceTotal => PierceTurnLen * bowCount + PierceTail;

        //溶解
        private const int DissolveStagger = 5;
        private const int DissolveFrames = 70;

        //==================== 各弓本地模拟 ====================

        private readonly Vector2[] bowPos = new Vector2[MaxBows];
        private readonly Vector2[] bowVel = new Vector2[MaxBows];
        private readonly Vector2[] bowTarget = new Vector2[MaxBows];
        private readonly float[] bowRot = new float[MaxBows];
        private readonly float[] bowSpin = new float[MaxBows];
        /// <summary>后坐量 px：放箭后弓身向后一顿</summary>
        private readonly float[] bowRecoil = new float[MaxBows];
        private readonly bool[] bowFlip = new bool[MaxBows];
        private readonly Vector2[][] bowOld = new Vector2[MaxBows][];
        private readonly float[][] bowOldRot = new float[MaxBows][];
        private bool bowsInit;

        private int bowCount = MaxBows;

        //==================== 本地表现量 ====================

        private readonly bool[] breachDone = new bool[MaxBows];
        /// <summary>放箭后的弦鸣闪帧</summary>
        private readonly int[] stringSnap = new int[MaxBows];
        private readonly int[] lastFireTick = new int[MaxBows];
        private readonly bool[] dissolveSplashed = new bool[MaxBows];
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;
        private bool formSnapDone;
        private bool climbWhooshDone;

        private Player Owner => Main.player[Projectile.owner];

        private float Seed => Projectile.identity * 0.6173f;

        //==================== 召唤入口 ====================

        internal static void Summon(Player owner, Vector2 emergeAt, int count, int itemType) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            KikasaBowProfile profile = KikasaArmsProfiler.BowProfileOf(itemType);
            count = Math.Clamp(count, 1, profile.MaxUnits);
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ArrowDamage * profile.ArrowDamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaBowServant>(), damage, 2f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaBowServant pack) {
                pack.bowCount = count;
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
            writer.Write((byte)bowCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int itemType = reader.ReadInt32();
            int count = reader.ReadByte();
            if (itemType > ItemID.None && itemType < ItemLoader.ItemCount && itemType != armsItemType) {
                SetArmsItemType(itemType);
            }
            count = Math.Clamp(count, 1, Profile.MaxUnits);
            if (count != bowCount) {
                bowCount = count;
                bowsInit = false;
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
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ArrowDamage * Profile.ArrowDamageMul);

            if (State != lastSeenState) {
                lastSeenState = State;
                climbWhooshDone = false;
                Array.Fill(lastFireTick, -1);
                if (State == StateDissolve) {
                    Array.Fill(dissolveSplashed, false);
                }
            }

            if (!bowsInit) {
                RebuildBows(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateVolley: UpdateVolley(owner, authority); break;
                case StateRain: UpdateRain(owner, authority); break;
                case StatePierce: UpdatePierce(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateBows(owner, domain);
            PushBowHistory();
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            for (int i = 0; i < bowCount; i++) {
                if (stringSnap[i] > 0) {
                    stringSnap[i]--;
                }
                bowRecoil[i] *= 0.78f;
                float glow = BowAlpha(i) * 0.32f;
                if (glow > 0.02f) {
                    Lighting.AddLight(bowPos[i], 0.4f * glow, 0.1f * glow, 0.09f * glow);
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水 ====================

        private float BreachX(int i)
            => Projectile.Center.X + (i - (bowCount - 1) * 0.5f) * EmergeSpan;

        private static int BreachTime(int i) => OmenFrames + i * BreachGap;

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                Projectile.velocity = Vector2.Zero;
                if (viewed && t % 5 == 2) {
                    float converge = 1f - t / (float)OmenFrames;
                    for (int i = 0; i < bowCount; i++) {
                        float wobble = MathF.Sin(t * 0.5f + i * 1.7f) * converge * 26f;
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

            for (int i = 0; i < bowCount; i++) {
                if (!breachDone[i] && t >= BreachTime(i)) {
                    breachDone[i] = true;
                    bowVel[i] = new Vector2(0f, -12.2f - i * 0.3f);
                    bowSpin[i] = (i % 2 == 0 ? 1f : -1f) * 0.32f;
                    if (i == 0) {
                        Projectile.velocity = new Vector2(0f, -3.2f);
                    }
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.72f, Pitch = -0.38f + i * 0.07f, MaxInstances = 3
                    }, bowPos[i]);
                    if (viewed) {
                        BreachBurst(new Vector2(BreachX(i), lakeY), i);
                    }
                }
            }

            Projectile.velocity *= 0.96f;

            if (viewed && t < RiseEnd) {
                for (int i = 0; i < bowCount; i++) {
                    if (t < BreachTime(i) || t % 3 != i % 3) {
                        continue;
                    }
                    Vector2 dropPos = bowPos[i] + new Vector2(
                        Main.rand.NextFloat(-20f, 20f), Main.rand.NextFloat(2f, 14f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.4f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(12, 24), 0f);
                }
            }

            //整队定弦拍：一顿之后一声弦鸣，弓成了
            if (!formSnapDone && t >= FormupFrame) {
                formSnapDone = true;
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.42f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                for (int i = 0; i < bowCount; i++) {
                    bowVel[i] += new Vector2(
                        -MathF.Sign(bowPos[i].X - Projectile.Center.X) * 1.6f, -1.1f);
                    stringSnap[i] = 5;
                    if (viewed) {
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(
                                bowPos[i] + Main.rand.NextVector2Circular(14f, 10f),
                                new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.5f, 1.8f)),
                                BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))
                                ?.Configure(Main.rand.Next(10, 18), 0.25f);
                        }
                    }
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
            KikasaDomainDeco.RippleAt(hit + new Vector2(20f, 0f), 0.55f);
            KikasaDomainDeco.SplashAt(hit, 7);
            for (int k = 0; k < 11; k++) {
                float angle = -MathHelper.Pi * (0.16f + 0.68f * k / 10f);
                float speed = Main.rand.NextFloat(2.6f, 5.6f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-12f, 12f), -4f),
                    angle.ToRotationVector2() * speed,
                    BloodMain * Main.rand.NextFloat(0.45f, 0.65f),
                    Main.rand.NextFloat(0.4f, 0.68f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                hit + new Vector2(Main.rand.NextFloat(-16f, 16f), -8f),
                new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.6f)),
                MistBlood * 0.7f, Main.rand.NextFloat(0.5f, 0.75f))
                ?.Configure(Main.rand.Next(50, 76));
            if (i == 0 || i == bowCount - 1) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = 0.3f, Pitch = -0.75f, MaxInstances = 1
                }, hit);
            }
            ShakeViewer(1.4f);
        }

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            Vector2 anchor = owner.Center + new Vector2(0f, -28f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildBows(owner.GetModPlayer<KikasaDomainPlayer>());
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
                bool primary = attackIndex % 2 == 1;
                State = Profile.Archetype == KikasaBowArchetype.Longbow
                    ? primary ? StatePierce : StateVolley
                    : primary ? StateVolley : StateRain;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 抛射排箭 ====================

        private void UpdateVolley(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= VolleyLockEnd) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 8f
                : Projectile.Center + bowRot[0].ToRotationVector2() * 500f;

            //射手站位：退到主人身后一段（枪压前、弓坠后的身份差）
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
            float strafe = MathF.Sin(t * 0.045f + Seed) * 24f;
            Vector2 anchor = owner.Center - toT * 52f + perp * strafe + new Vector2(0f, -30f);
            Vector2 desired = (anchor - Projectile.Center) * 0.11f;
            if (desired.Length() > 13f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 13f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.18f);

            //锁线两声弦紧
            if (t == 6 || t == 18) {
                SoundEngine.PlaySound(SoundID.Item5 with {
                    Volume = 0.28f, Pitch = -0.62f + t * 0.01f, MaxInstances = 3
                }, Projectile.Center);
            }

            //轮转放箭：拉弦-满弦-释放循环，节拍闩防快照回卷
            if (t > VolleyLockEnd && t <= VolleyFireEnd) {
                for (int i = 0; i < bowCount; i++) {
                    int local = t - VolleyLockEnd - i * Profile.FireStagger;
                    if (local >= 0 && local % Profile.DrawPeriod == 0) {
                        int tick = local / Profile.DrawPeriod;
                        if (tick > lastFireTick[i]) {
                            lastFireTick[i] = tick;
                            FireArrow(owner, authority, i, focus);
                        }
                    }
                }
            }

            if (t >= VolleyTotal) {
                EndAttack(authority, 110);
            }
        }

        /// <summary>排箭期单弓拉弦进度 0~1（绘制层搭箭/压弦共用）；释放帧归零</summary>
        private float DrawPullOf(int i) {
            int t = (int)StateTimer;
            switch (State) {
                case StateVolley: {
                    if (t <= VolleyLockEnd - 6) {
                        return 0f;
                    }
                    int local = t - VolleyLockEnd - i * Profile.FireStagger;
                    if (local < -6) {
                        return 0f;
                    }
                    if (local < 0) {
                        //锁线尾段先搭上箭
                        return (local + 6) / 12f;
                    }
                    if (t > VolleyFireEnd) {
                        return 0f;
                    }
                    int phase = local % Profile.DrawPeriod;
                    //周期前 70% 拉满，满弦顿到释放
                    return MathHelper.Clamp(phase / (Profile.DrawPeriod * 0.7f), 0f, 1f);
                }
                case StatePierce: {
                    int duty = t / PierceTurnLen;
                    if (duty >= bowCount || i != duty) {
                        return 0f;
                    }
                    int p = t - duty * PierceTurnLen;
                    if (p < 8 || p > PierceFireFrame) {
                        return 0f;
                    }
                    return MathHelper.Clamp((p - 8f) / (PierceFireFrame - 12f), 0f, 1f);
                }
                case StateRain: {
                    if (t <= RainClimbEnd || t > RainSalvoFrame(RainSalvos - 1)) {
                        return 0f;
                    }
                    //齐抛节拍内共用一条拉弦线
                    int sinceSalvo = (t - RainClimbEnd - 8) % RainSalvoGap;
                    return MathHelper.Clamp(sinceSalvo / (RainSalvoGap * 0.72f), 0f, 1f);
                }
                default:
                    return 0f;
            }
        }

        /// <summary>放箭：拉弦循环的释放帧。lob 补偿箭的坠弧，重箭走贯穿模式</summary>
        private void FireArrow(Player owner, bool authority, int i, Vector2 focus, bool heavy = false) {
            Vector2 aimDir = bowRot[i].ToRotationVector2();
            Vector2 nock = NockPos(i);
            bowRecoil[i] = heavy ? 16f : 9f;
            bowVel[i] -= aimDir * (heavy ? 2.6f : 1.1f);
            stringSnap[i] = heavy ? 7 : 4;

            SoundEngine.PlaySound(Profile.FireSound with {
                Volume = heavy ? 0.55f : 0.32f,
                Pitch = (heavy ? -0.3f : -0.08f) + i * 0.05f,
                MaxInstances = 4
            }, nock);
            if (heavy) {
                SoundEngine.PlaySound(SoundID.DD2_BallistaTowerShot with {
                    Volume = 0.4f, Pitch = 0.05f, MaxInstances = 2
                }, nock);
            }
            if (!Main.dedServ) {
                int burst = heavy ? 5 : 3;
                for (int k = 0; k < burst; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(nock,
                        aimDir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(2f, heavy ? 6f : 4.5f),
                        BloodMain * 0.55f, Main.rand.NextFloat(0.26f, 0.44f))
                        ?.Configure(Main.rand.Next(8, 14), 0.2f);
                }
            }
            if (ViewedOwner) {
                ShakeViewer(heavy ? 1.8f : 0.4f);
            }

            if (authority) {
                float dist = Vector2.Distance(nock, focus);
                float speed = Profile.ArrowSpeed * (heavy ? 1.55f : 1f);
                Vector2 vel = aimDir.RotatedBy(Main.rand.NextFloat(-0.03f, 0.03f)) * speed;
                if (!heavy && Profile.Archetype != KikasaBowArchetype.Rapid) {
                    //抛射补偿：箭的后段坠弧靠抬角还回来
                    vel.Y -= dist * 0.0016f;
                }
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                    .ApplyTo(ArrowDamage * Profile.ArrowDamageMul * (heavy ? PierceMul : 1f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), nock, vel,
                    ModContent.ProjectileType<KikasaBowArrow>(), damage, 2f, Projectile.owner,
                    heavy ? 1f : 0f);
            }
        }

        //==================== 箭雨 ====================

        private void UpdateRain(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= RainClimbEnd) {
                EndAttack(authority, 60);
                return;
            }
            Vector2 focus = target >= 0 ? Main.npc[target].Center : Projectile.Center + new Vector2(0f, 300f);

            //爬升拍：一声破空
            if (!climbWhooshDone) {
                climbWhooshDone = true;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with {
                    Volume = 0.5f, Pitch = 0.05f, MaxInstances = 3
                }, Projectile.Center);
            }

            //质心爬到主人上方高位
            Vector2 anchor = owner.Center + new Vector2(0f, -168f);
            Vector2 desired = (anchor - Projectile.Center) * 0.12f;
            if (desired.Length() > 16f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 16f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.2f);

            //三轮齐抛：每弓两支雨箭撒向落区
            for (int k = 0; k < RainSalvos; k++) {
                if (t != RainSalvoFrame(k)) {
                    continue;
                }
                bool fired = false;
                for (int i = 0; i < bowCount; i++) {
                    if (k > lastFireTick[i]) {
                        lastFireTick[i] = k;
                        FireRainPair(owner, authority, i, k, focus);
                        fired = true;
                    }
                }
                if (fired) {
                    SoundEngine.PlaySound(SoundID.Item5 with {
                        Volume = 0.5f, Pitch = -0.2f + k * 0.08f, MaxInstances = 2
                    }, Projectile.Center);
                    if (ViewedOwner) {
                        ShakeViewer(1.6f);
                    }
                }
            }

            if (t >= RainTotal) {
                EndAttack(authority, 150);
            }
        }

        /// <summary>单弓一轮齐抛：两支雨箭，落点按弓位与轮次确定性散开</summary>
        private void FireRainPair(Player owner, bool authority, int i, int salvo, Vector2 focus) {
            Vector2 nock = NockPos(i);
            bowRecoil[i] = 10f;
            stringSnap[i] = 5;
            bowVel[i] += new Vector2(0f, 1.4f);

            if (!Main.dedServ) {
                for (int k = 0; k < 3; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(nock,
                        new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(2f, 4f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.26f, 0.42f))
                        ?.Configure(Main.rand.Next(8, 14), 0.2f);
                }
            }
            if (!authority) {
                return;
            }

            int damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                .ApplyTo(ArrowDamage * Profile.ArrowDamageMul * RainMul);
            for (int k = 0; k < 2; k++) {
                //确定性落点散布：弓位 × 轮次 × 支序铺开一片落区
                float lane = (i - (bowCount - 1) * 0.5f) * 52f + (k * 2 - 1) * 20f + (salvo - 1) * 14f;
                Vector2 aim = focus + new Vector2(lane, 0f);
                float dx = aim.X - nock.X;
                Vector2 vel = new(MathHelper.Clamp(dx * 0.017f, -8.5f, 8.5f), -12.6f - (i % 2) * 0.8f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), nock, vel,
                    ModContent.ProjectileType<KikasaBowArrow>(), damage, 1.5f, Projectile.owner, 2f);
            }
        }

        //==================== 贯穿重箭（重弓档）====================

        private void UpdatePierce(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= 12) {
                EndAttack(authority, 60);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 10f
                : Projectile.Center + bowRot[0].ToRotationVector2() * 600f;

            //质心稳在中距离，重弓的从容
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
            Vector2 anchor = owner.Center - toT * 40f + perp * MathF.Sin(t * 0.03f + Seed) * 22f
                + new Vector2(0f, -32f);
            Vector2 desired = (anchor - Projectile.Center) * 0.1f;
            if (desired.Length() > 11f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 11f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.16f);

            int duty = t / PierceTurnLen;
            if (duty < bowCount) {
                int p = t - duty * PierceTurnLen;
                //弦紧两声，音高爬升
                if (p == 10 || p == 28) {
                    SoundEngine.PlaySound(SoundID.Item5 with {
                        Volume = 0.3f, Pitch = -0.6f + p * 0.012f, MaxInstances = 3
                    }, bowPos[duty]);
                }
                if (p == PierceFireFrame && duty > lastFireTick[duty]) {
                    lastFireTick[duty] = duty;
                    FireArrow(owner, authority, duty, focus, heavy: true);
                    bowSpin[duty] = (bowFlip[duty] ? 1f : -1f) * 0.28f;
                    if (ViewedOwner) {
                        ShakeViewer(2.4f);
                    }
                }
            }

            if (t >= PierceTotal) {
                EndAttack(authority, 150);
            }
        }

        /// <summary>重击前 3 帧的预告闪</summary>
        private float PierceFlashOf(int i) {
            if (State != StatePierce) {
                return 0f;
            }
            int t = (int)StateTimer;
            int duty = t / PierceTurnLen;
            if (duty >= bowCount || i != duty) {
                return 0f;
            }
            int dt = PierceFireFrame - (t - duty * PierceTurnLen);
            return dt is >= 0 and <= 3 ? 1f - dt / 4f : 0f;
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

            for (int i = 0; i < bowCount; i++) {
                int lt = t - i * DissolveStagger;
                if (lakeAlive && !dissolveSplashed[i] && lt >= 0 && bowPos[i].Y >= lakeY) {
                    dissolveSplashed[i] = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.55f, Pitch = -0.4f + i * 0.08f, MaxInstances = 3
                    }, bowPos[i]);
                    if (ViewedOwner) {
                        Vector2 hit = new(bowPos[i].X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 6);
                        KikasaDomainDeco.RippleAt(hit, 0.9f);
                        ShakeViewer(1f);
                    }
                }
            }

            if (!Main.dedServ && BowAlpha(0) > 0.15f) {
                int i = t % bowCount;
                if (t - i * DissolveStagger >= 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        bowPos[i] + Main.rand.NextVector2Circular(18f, 10f),
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

        //==================== 各弓推进 ====================

        private void RebuildBows(KikasaDomainPlayer domain) {
            bowsInit = true;
            for (int i = 0; i < MaxBows; i++) {
                if (State == StateEmerge) {
                    bowPos[i] = new Vector2(BreachX(i), domain.LakeWorldY + 26f);
                    bowRot[i] = -MathHelper.PiOver2;
                }
                else {
                    float phase = Main.GlobalTimeWrappedHourly * 0.58f + Seed + i * MathHelper.TwoPi / Math.Max(bowCount, 1);
                    bowPos[i] = Projectile.Center + new Vector2(MathF.Cos(phase) * 104f, MathF.Sin(phase) * 48f - 30f);
                    bowRot[i] = 0f;
                }
                bowFlip[i] = MathF.Cos(bowRot[i]) < 0f;
                bowVel[i] = Vector2.Zero;
                bowSpin[i] = 0f;
                bowRecoil[i] = 0f;
                bowTarget[i] = bowPos[i];
                bowOld[i] ??= new Vector2[8];
                bowOldRot[i] ??= new float[8];
                for (int k = 0; k < bowOld[i].Length; k++) {
                    bowOld[i][k] = bowPos[i];
                    bowOldRot[i][k] = bowRot[i];
                }
            }
        }

        private void ChaseBow(int i, float accel, float damp) {
            bowVel[i] = (bowVel[i] + (bowTarget[i] - bowPos[i]) * accel) * damp;
            bowPos[i] += bowVel[i];
        }

        private float Sway(int i, float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed + i * 2.4f) * amp;

        private void FaceBow(int i, Vector2 worldPos, float rate) {
            float want = (worldPos - bowPos[i]).ToRotation();
            bowRot[i] = bowRot[i].AngleLerp(want, rate);
        }

        private void UpdateBows(Player owner, KikasaDomainPlayer domain) {
            if (!bowsInit) {
                return;
            }
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            Vector2 targetPos = target >= 0 ? Main.npc[target].Center : owner.Center;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    for (int i = 0; i < bowCount; i++) {
                        if (t < BreachTime(i)) {
                            bowPos[i] = new Vector2(BreachX(i), lakeY + 26f);
                            bowVel[i] = Vector2.Zero;
                            bowTarget[i] = bowPos[i];
                            bowRot[i] = -MathHelper.PiOver2;
                            continue;
                        }
                        bowTarget[i] = new Vector2(BreachX(i), lakeY - 94f + Sway(i, 2.1f, 9f));
                        int lt = t - BreachTime(i);
                        if (lt < 14) {
                            bowVel[i].Y *= 0.955f;
                            bowVel[i].X *= 0.98f;
                            bowPos[i] += bowVel[i];
                            bowRot[i] += bowSpin[i];
                            bowSpin[i] *= 0.94f;
                        }
                        else {
                            ChaseBow(i, 0.05f, 0.86f);
                            bowRot[i] += bowSpin[i];
                            bowSpin[i] *= 0.9f;
                            if (MathF.Abs(bowSpin[i]) < 0.05f) {
                                float level = bowPos[i].X >= Projectile.Center.X ? 0f : MathHelper.Pi;
                                bowRot[i] = bowRot[i].AngleLerp(level, 0.14f);
                            }
                        }
                    }
                    break;
                }
                case StateFollow: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    for (int i = 0; i < bowCount; i++) {
                        float phase = tGlobal * 0.58f + Seed + i * MathHelper.TwoPi / bowCount;
                        float dartT = (t + i * 47) % 180;
                        float dart = dartT < 22 ? MathF.Sin(dartT / 22f * MathHelper.Pi) * 40f : 0f;
                        Vector2 radial = new(MathF.Cos(phase) * 104f, MathF.Sin(phase) * 48f - 30f);
                        Vector2 tangent = new Vector2(-MathF.Sin(phase) * 104f, MathF.Cos(phase) * 48f)
                            .SafeNormalize(Vector2.UnitX);
                        Vector2 slot = Projectile.Center + radial + tangent * dart;
                        slot.Y += MathF.Sin(tGlobal * 2.3f + Seed * 2f + i * 1.9f) * 7f;
                        bowTarget[i] = slot;
                        ChaseBow(i, 0.06f, 0.84f);

                        if (target >= 0) {
                            FaceBow(i, targetPos, 0.16f);
                        }
                        else if (bowVel[i].Length() > 2.6f) {
                            bowRot[i] = bowRot[i].AngleLerp(bowVel[i].ToRotation(), 0.12f);
                        }
                        else {
                            bowRot[i] = bowRot[i].AngleLerp(owner.direction > 0 ? 0f : MathHelper.Pi, 0.05f);
                        }
                    }
                    break;
                }
                case StateVolley: {
                    Vector2 focus = target >= 0 ? Main.npc[target].Center : Projectile.Center + bowRot[0].ToRotationVector2() * 500f;
                    Vector2 toT = (focus - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
                    Vector2 aimPos = target >= 0
                        ? Main.npc[target].Center + Main.npc[target].velocity * 8f
                        : focus;
                    for (int i = 0; i < bowCount; i++) {
                        //雁行斜列：沿垂直向排开、逐弓向后错深，射手梯队
                        float lane = i - (bowCount - 1) * 0.5f;
                        Vector2 slot = Projectile.Center + perp * (lane * 42f + Sway(i, 1.8f, 4f))
                            - toT * (MathF.Abs(lane) * 10f + i * 4f);
                        bowTarget[i] = slot;
                        ChaseBow(i, t < VolleyFormEnd ? 0.12f : 0.08f, 0.8f);
                        FaceBow(i, aimPos, t < VolleyLockEnd ? 0.3f : 0.4f);
                    }
                    break;
                }
                case StateRain: {
                    for (int i = 0; i < bowCount; i++) {
                        //高位横列，弓口上仰
                        float lane = i - (bowCount - 1) * 0.5f;
                        Vector2 slot = Projectile.Center + new Vector2(lane * 46f, Sway(i, 2f, 5f));
                        bowTarget[i] = slot;
                        ChaseBow(i, t < RainClimbEnd ? 0.14f : 0.08f, 0.8f);
                        //仰角朝天：齐抛的姿势（±0.24 扇差）
                        float up = -MathHelper.PiOver2 + lane * 0.24f;
                        bowRot[i] = bowRot[i].AngleLerp(up, t < RainClimbEnd ? 0.18f : 0.1f);
                    }
                    break;
                }
                case StatePierce: {
                    Vector2 aimPos = target >= 0
                        ? Main.npc[target].Center + Main.npc[target].velocity * 10f
                        : Projectile.Center + bowRot[0].ToRotationVector2() * 600f;
                    Vector2 toT = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
                    int duty = Math.Min(t / PierceTurnLen, bowCount - 1);
                    for (int i = 0; i < bowCount; i++) {
                        Vector2 slot;
                        if (i == duty) {
                            //轮值弓压步向前
                            slot = Projectile.Center + toT * 34f + new Vector2(0f, Sway(i, 1.6f, 3f));
                        }
                        else {
                            //候场弓退后排斜列松散盯梢
                            float lane = i - (bowCount - 1) * 0.5f;
                            slot = Projectile.Center - toT * 30f + perp * lane * 40f
                                + new Vector2(0f, Sway(i, 2f, 5f));
                        }
                        bowTarget[i] = slot;
                        ChaseBow(i, i == duty ? 0.12f : 0.07f, 0.8f);
                        FaceBow(i, aimPos, i == duty ? 0.4f : 0.12f);
                    }
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    for (int i = 0; i < bowCount; i++) {
                        int lt = t - i * DissolveStagger;
                        if (lt < 0) {
                            continue;
                        }
                        bowVel[i].X *= 0.93f;
                        bowVel[i].Y = MathF.Min(bowVel[i].Y + 0.3f, 9.5f);
                        float droop = bowRot[i] + (MathF.Cos(bowRot[i]) >= 0f ? 0.5f : -0.5f);
                        bowRot[i] = bowRot[i].AngleLerp(droop, 0.02f);
                        bowPos[i] += bowVel[i];
                        bowTarget[i] = bowPos[i];
                    }
                    break;
                }
            }

            for (int i = 0; i < bowCount; i++) {
                if (!skipFix && Vector2.Distance(bowPos[i], bowTarget[i]) > 780f) {
                    bowPos[i] = bowTarget[i];
                    bowVel[i] = Vector2.Zero;
                }
                float c = MathF.Cos(bowRot[i]);
                if (c > 0.22f) {
                    bowFlip[i] = false;
                }
                else if (c < -0.22f) {
                    bowFlip[i] = true;
                }
            }
        }

        private void PushBowHistory() {
            for (int i = 0; i < bowCount; i++) {
                if (bowOld[i] == null) {
                    continue;
                }
                for (int k = bowOld[i].Length - 1; k >= 1; k--) {
                    bowOld[i][k] = bowOld[i][k - 1];
                    bowOldRot[i][k] = bowOldRot[i][k - 1];
                }
                bowOld[i][0] = bowPos[i];
                bowOldRot[i][0] = bowRot[i];
            }
        }

        private void UpdateAmbient() {
            if (Main.dedServ
                || State is not (StateFollow or StateVolley or StateRain or StatePierce)) {
                return;
            }
            if (Main.rand.NextBool(16) && BowAlpha(0) > 0.5f) {
                int i = Main.rand.Next(bowCount);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    bowPos[i] + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(6f, 12f)),
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

        /// <summary>绘制位：后坐沿 -瞄准向顶回</summary>
        private Vector2 BowDrawPos(int i)
            => bowPos[i] - bowRot[i].ToRotationVector2() * bowRecoil[i];

        /// <summary>搭箭位：弓身中点沿瞄准向略前</summary>
        private Vector2 NockPos(int i)
            => BowDrawPos(i) + bowRot[i].ToRotationVector2() * 8f;

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float BowAlpha(int i) {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < BreachTime(i) ? 0f : MathHelper.Clamp((t - BreachTime(i)) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - (t - i * DissolveStagger)) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        private float BowForm(int i) {
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

        private float BowScale(int i) {
            float scale = 1f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= BreachTime(i) && t < BreachTime(i) + 10) {
                scale *= 1f + 0.08f * (1f - (t - BreachTime(i)) / 10f);
            }
            return scale * Profile.DrawScale;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        private SpriteEffects BowFx(int i)
            => bowFlip[i] ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        private float FlipRotOffset(int i) => bowFlip[i] ? MathHelper.Pi : 0f;

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!bowsInit) {
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
            DrawGlow(sb);
            return false;
        }

        private void DrawDashTrails(SpriteBatch sb, Texture2D tex) {
            Vector2 origin = tex.Size() * 0.5f;
            for (int i = 0; i < bowCount; i++) {
                float trailA = MathHelper.Clamp((bowVel[i].Length() - 8f) / 10f, 0f, 1f) * BowAlpha(i);
                if (trailA <= 0.03f || bowOld[i] == null) {
                    continue;
                }
                for (int k = bowOld[i].Length - 1; k >= 1; k--) {
                    float fall = 1f - k / (float)bowOld[i].Length;
                    sb.Draw(tex, bowOld[i][k] - Main.screenPosition, null,
                        BloodMain * (0.26f * fall * trailA), bowOldRot[i][k] + FlipRotOffset(i),
                        origin, BowScale(i) * (0.96f - k * 0.015f), BowFx(i), 0f);
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
            for (int i = 0; i < bowCount; i++) {
                float alpha = BowAlpha(i);
                if (alpha <= 0.01f) {
                    continue;
                }
                float pull = DrawPullOf(i);
                float rot = bowRot[i] + FlipRotOffset(i);
                Vector2 drawPos = BowDrawPos(i) - Main.screenPosition;
                float dissolve = DissolveAmt(i);
                //拉弦压弓：沿瞄准轴微缩、纵向微涨——弓身受力
                Vector2 scale = new(BowScale(i) * (1f - 0.1f * pull), BowScale(i) * (1f + 0.05f * pull));

                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed + i * 1.7f;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.7f, MathF.Cos(wt * 0.83f) * 2.1f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.035f;
                    Vector2 envScale = scale * (1.14f + MathF.Sin(wt * 1.6f) * 0.025f);
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 3.7f + 5.1f);
                    form.Parameters["uForm"]?.SetValue(1f);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(tex, drawPos + wobOff, null,
                        new Color(255, 255, 255, (byte)(alpha * 130f)),
                        rot + wobRot, origin, envScale, BowFx(i), 0f);
                }

                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 3.7f);
                    form.Parameters["uForm"]?.SetValue(BowForm(i));
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }
                sb.Draw(tex, drawPos, null, color, rot, origin, scale, BowFx(i), 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

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

            //预兆：各破水点水下血光
            if (State == StateEmerge && t < RiseEnd) {
                for (int i = 0; i < bowCount; i++) {
                    if (t >= BreachTime(i)) {
                        continue;
                    }
                    float ot = MathHelper.Clamp(t / (float)BreachTime(i), 0f, 1f);
                    float ease = ot * ot;
                    EnsureBegin();
                    Vector2 pos = new(BreachX(i), domain.LakeWorldY + MathHelper.Lerp(42f, 8f, ease));
                    float r = 18f + 13f * ease;
                    sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * (0.32f * ease), 0f,
                        gOrigin, new Vector2(r * 2.4f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            for (int i = 0; i < bowCount; i++) {
                float alpha = BowAlpha(i);
                if (alpha <= 0.05f) {
                    continue;
                }
                Vector2 dir = bowRot[i].ToRotationVector2();
                float pull = DrawPullOf(i);

                //搭箭：拉弦进度把箭往后收，满弦亮尖
                if (pull > 0.03f) {
                    EnsureBegin();
                    Vector2 arrowMid = BowDrawPos(i) + dir * (10f - pull * 13f);
                    float arrowLen = 30f * Profile.DrawScale;
                    sb.Draw(glow, arrowMid - Main.screenPosition, null,
                        BloodBright * (0.5f * pull * alpha), bowRot[i], gOrigin,
                        new Vector2(arrowLen / glow.Width * 2f, 4.5f / glow.Height * 2f), SpriteEffects.None, 0f);
                    sb.Draw(glow, arrowMid + dir * arrowLen * 0.45f - Main.screenPosition, null,
                        MuzzleHot * (0.45f * pull * pull * alpha), 0f, gOrigin,
                        new Vector2(7f * 2f / glow.Width), SpriteEffects.None, 0f);
                }

                //贯穿蓄力：轮值弓的瞄准线渐亮 + 重击前闪
                if (State == StatePierce) {
                    float charge = DrawPullOf(i);
                    float flash = PierceFlashOf(i);
                    float lineA = MathF.Max(charge * 0.22f, flash * 0.5f);
                    if (lineA > 0.03f) {
                        EnsureBegin();
                        float lineLen = 620f;
                        Vector2 mid = NockPos(i) + dir * lineLen * 0.5f;
                        sb.Draw(glow, mid - Main.screenPosition, null,
                            BloodBright * (lineA * alpha), bowRot[i], gOrigin,
                            new Vector2(lineLen / glow.Width * 2f, 3f / glow.Height * 2f), SpriteEffects.None, 0f);
                    }
                }

                //弦鸣闪：放箭一瞬弓身亮一记
                if (stringSnap[i] > 0) {
                    EnsureBegin();
                    float a = stringSnap[i] / 5f;
                    sb.Draw(glow, BowDrawPos(i) - Main.screenPosition, null,
                        BloodBright * (0.4f * a * alpha), bowRot[i] + MathHelper.PiOver2, gOrigin,
                        new Vector2(Profile.BowSpan * 1.1f / glow.Width * 2f, 6f / glow.Height * 2f), SpriteEffects.None, 0f);
                }
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !bowsInit) {
                return;
            }
            for (int i = 0; i < bowCount; i++) {
                for (int k = 0; k < 4; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        bowPos[i] + Main.rand.NextVector2Circular(16f, 10f),
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
