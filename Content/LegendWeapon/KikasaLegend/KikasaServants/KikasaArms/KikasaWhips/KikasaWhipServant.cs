using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaWhips
{
    /// <summary>
    /// 械奴·湖水鞭群（通用鞭奴）。单弹幕同时驱动至多三条湖水凝成的盘鞭：
    /// Projectile.Center 为编队质心权威同步，各鞭位置由状态机 + Seed 在各端本地推算——
    /// 联机契约与枪奴/刀奴同构（owner 裁决转场盖 netUpdate 章、节拍闩防快照回卷、
    /// 生命线只有 owner 判、鞭数与武器类型经 ExtraAI 随包补发）。
    /// 常态是盘起的鞭（沉入武器物品贴图 + 水鞘扫描水线）绕主人慢游；
    /// 出手为轮转鞭笞：逐鞭错帧抢占目标侧翼驻位（驻距=鞭尖峰值探出×0.62，
    /// 鞭尖正好扫过目标身后——判定慷慨）、短蓄后甩出 <see cref="KikasaWhipLash"/>
    /// 鞭体弹幕（几何/判定/鞭响全对齐原版 AI_165 契约），鞭响帧鞭柄后坐一顿。
    /// 鞭子的机制身份被完整保留：鞭中即把 MinionAttackTargetNPC 指向目标——
    /// 全部役鬼当场集火，原版鞭的标签 buff 照挂。个性化由 KikasaArmsProfiler
    /// 鞭档案承担：射程/段数/节奏/伤害/挥音随沉入武器推得
    /// </summary>
    internal class KikasaWhipServant : ModProjectile, IKikasaArmsServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>鞭笞基伤（召唤加成与档案倍率前），由鞭体弹幕消费</summary>
        internal const int LashDamage = 120;

        /// <summary>编队硬上限：数组容量，实际编制还要过档案 MaxUnits</summary>
        internal const int MaxWhips = 3;

        //==================== 档案 ====================

        /// <summary>沉入湖中的原型武器物品类型：贴图与档案来源，ExtraAI 同步</summary>
        private int armsItemType = ItemID.BlandWhip;

        /// <summary>沉影盘在场判定用：这队械奴复制的是哪件武器</summary>
        public int ArmsItemType => armsItemType;

        private KikasaWhipProfile? profileCache;

        /// <summary>档案惰性求值：模板实例化早于 ContentSamples 灌装，首次访问再推</summary>
        private KikasaWhipProfile Profile => profileCache ??= KikasaArmsProfiler.WhipProfileOf(armsItemType);

        private void SetArmsItemType(int itemType) {
            armsItemType = itemType;
            profileCache = null;
        }

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateLash = 2;
        private const int StateDissolve = 3;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：目前未用，保位与枪奴同构</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：多点预兆→逐鞭错帧破水翻腾→落定→整队起鞭轻甩拍
        private const int OmenFrames = 22;
        private const int BreachGap = 8;
        private const int RiseEnd = 60;
        private const int SnapFrame = 70;
        private const int EmergeTotal = 86;
        /// <summary>相邻破水点横距</summary>
        private const float EmergeSpan = 52f;

        //轮转鞭笞：起手引拍后逐鞭错帧接力，接力间隔走档案 LashPeriod
        private const int LashLead = 10;
        /// <summary>抢位+短蓄帧数：驻位到位、鞭柄后仰蓄势</summary>
        private const int WindupLen = 12;

        /// <summary>单鞭完整一轮：蓄 + 甩出回收 + 收拍</summary>
        private int TurnLen => WindupLen + Profile.LashTime + 8;

        private int LashTotal => LashLead + Profile.LashPeriod * (whipCount - 1) + TurnLen + 10;

        //溶解：逐鞭错帧失力坠湖
        private const int DissolveStagger = 5;
        private const int DissolveFrames = 66;

        //==================== 各鞭本地模拟（各端自算，质心同步纠偏）====================

        private readonly Vector2[] whipPos = new Vector2[MaxWhips];
        private readonly Vector2[] whipVel = new Vector2[MaxWhips];
        private readonly Vector2[] whipTarget = new Vector2[MaxWhips];
        /// <summary>盘鞭本体倾角（纯表现，甩鞭朝向在鞭体弹幕上）</summary>
        private readonly float[] whipRot = new float[MaxWhips];
        /// <summary>出水翻腾角速度</summary>
        private readonly float[] whipSpin = new float[MaxWhips];
        //各鞭当轮驻位与甩向（p==0 声明，跳帧进窗补声明）
        private readonly Vector2[] lashPost = new Vector2[MaxWhips];
        private readonly float[] lashAng = new float[MaxWhips];
        private readonly Vector2[][] whipOld = new Vector2[MaxWhips][];
        private readonly float[][] whipOldRot = new float[MaxWhips][];
        private bool whipsInit;

        /// <summary>编队鞭数：owner 在 Summon 里定值，ExtraAI 随包同步；远端首包前按满编</summary>
        private int whipCount = MaxWhips;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private readonly bool[] breachDone = new bool[MaxWhips];
        private readonly int[] lastLashTick = new int[MaxWhips];
        /// <summary>当轮驻位/甩向已声明：远端快照跳帧也不留陈旧驻位</summary>
        private readonly bool[] aimDeclared = new bool[MaxWhips];
        private readonly bool[] dissolveSplashed = new bool[MaxWhips];
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;
        private bool snapBeatDone;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.6173f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaArmsIndex 登记的召唤入口；emergeAt.Y = 湖面，count = 湖藏存量，
        /// itemType = 沉入的原型武器（档案来源）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt, int count, int itemType) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            KikasaWhipProfile profile = KikasaArmsProfiler.WhipProfileOf(itemType);
            count = Math.Clamp(count, 1, profile.MaxUnits);
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(LashDamage * profile.LashDamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaWhipServant>(), damage, 2f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaWhipServant pack) {
                //生成包已经带默认编制出门了，这里改完补一发 ExtraAI（迟一帧只影响预兆涟漪点数）
                pack.whipCount = count;
                pack.SetArmsItemType(itemType);
                Main.projectile[index].netUpdate = true;
            }
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //鞭笞驻位散布远超质心 hitbox，出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
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

        /// <summary>盘鞭本体不做接触判定，伤害全在鞭体弹幕上</summary>
        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(armsItemType);
            writer.Write((byte)whipCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int itemType = reader.ReadInt32();
            int count = reader.ReadByte();
            if (itemType > ItemID.None && itemType < ItemLoader.ItemCount && itemType != armsItemType) {
                SetArmsItemType(itemType);
            }
            count = Math.Clamp(count, 1, Profile.MaxUnits);
            if (count != whipCount) {
                whipCount = count;
                //编制变了按新编制重建
                whipsInit = false;
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
            //一条都没破水就要收场：直接收掉，免得溶解演出让鞭凭空闪现再化水
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

            //生命线：只有 owner 裁决——服务器无领域状态（既定契约）
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(LashDamage * Profile.LashDamageMul);

            //换场清闩：远端可能靠收包换场而非本地同拍转场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                Array.Fill(lastLashTick, -1);
                Array.Fill(aimDeclared, false);
                if (State == StateDissolve) {
                    Array.Fill(dissolveSplashed, false);
                }
            }

            if (!whipsInit) {
                RebuildWhips(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateLash: UpdateLash(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateWhips(owner, domain);
            PushWhipHistory();
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            for (int i = 0; i < whipCount; i++) {
                float glow = WhipAlpha(i) * 0.32f;
                if (glow > 0.02f) {
                    Lighting.AddLight(whipPos[i], 0.4f * glow, 0.1f * glow, 0.09f * glow);
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水 ====================

        private float BreachX(int i)
            => Projectile.Center.X + (i - (whipCount - 1) * 0.5f) * EmergeSpan;

        private static int BreachTime(int i) => OmenFrames + i * BreachGap;

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                Projectile.velocity = Vector2.Zero;
                if (viewed && t % 5 == 2) {
                    float converge = 1f - t / (float)OmenFrames;
                    for (int i = 0; i < whipCount; i++) {
                        float wobble = MathF.Sin(t * 0.5f + i * 1.7f) * converge * 20f;
                        KikasaDomainDeco.RippleAt(new Vector2(BreachX(i) + wobble, lakeY),
                            0.3f + (1f - converge) * 0.4f);
                    }
                }
                if (viewed && (t == 5 || t == 14)) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.42f,
                        Pitch = -0.55f + t * 0.012f,
                        MaxInstances = 3
                    }, new Vector2(Projectile.Center.X, lakeY));
                }
                return;
            }

            //错帧破水：一条接一条翻腾跃出
            for (int i = 0; i < whipCount; i++) {
                if (!breachDone[i] && t >= BreachTime(i)) {
                    breachDone[i] = true;
                    whipVel[i] = new Vector2(0f, -11f - i * 0.3f);
                    whipSpin[i] = (i % 2 == 0 ? 1f : -1f) * 0.3f;
                    if (i == 0) {
                        Projectile.velocity = new Vector2(0f, -3f);
                    }
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.68f,
                        Pitch = -0.3f + i * 0.08f,
                        MaxInstances = 3
                    }, whipPos[i]);
                    if (viewed) {
                        BreachBurst(new Vector2(BreachX(i), lakeY), i);
                    }
                }
            }

            Projectile.velocity *= 0.96f;

            //身上的湖水成帘往下淌
            if (viewed && t < RiseEnd) {
                for (int i = 0; i < whipCount; i++) {
                    if (t < BreachTime(i) || t % 3 != i % 3) {
                        continue;
                    }
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        whipPos[i] + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(2f, 12f)),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.2f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.32f, 0.55f))
                        ?.Configure(Main.rand.Next(12, 22), 0f);
                }
            }

            //整队起鞭拍：全员一顿 + 一记轻甩空响——鞭醒了
            if (!snapBeatDone && t >= SnapFrame) {
                snapBeatDone = true;
                SoundEngine.PlaySound(Profile.SwingSound with { Volume = 0.4f, Pitch = -0.1f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.35f, Pitch = -0.15f, MaxInstances = 2 }, Projectile.Center);
                for (int i = 0; i < whipCount; i++) {
                    whipVel[i] += new Vector2(
                        -MathF.Sign(whipPos[i].X - Projectile.Center.X) * 1.5f, -1f);
                    whipSpin[i] += (i % 2 == 0 ? 1f : -1f) * 0.12f;
                }
                if (viewed) {
                    ShakeViewer(1.8f);
                }
            }

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），各端同拍；owner 盖章纠偏
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>单鞭破水浪冠：规格比刀奴再收一号</summary>
        private void BreachBurst(Vector2 hit, int i) {
            KikasaDomainDeco.RippleAt(hit, 1.2f);
            KikasaDomainDeco.SplashAt(hit, 6);
            for (int k = 0; k < 9; k++) {
                float angle = -MathHelper.Pi * (0.2f + 0.6f * k / 8f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-9f, 9f), -4f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(2.2f, 5.2f),
                    BloodMain * Main.rand.NextFloat(0.45f, 0.62f),
                    Main.rand.NextFloat(0.38f, 0.6f))
                    ?.Configure(Main.rand.Next(16, 28), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                hit + new Vector2(Main.rand.NextFloat(-12f, 12f), -8f),
                new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.55f)),
                MistBlood * 0.7f, Main.rand.NextFloat(0.45f, 0.7f))
                ?.Configure(Main.rand.Next(45, 70));
            if (i == 0 || i == whipCount - 1) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = 0.26f,
                    Pitch = -0.7f,
                    MaxInstances = 1
                }, hit);
            }
            ShakeViewer(1.2f);
        }

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            //质心锚：贴着玩家，编队绕质心游
            Vector2 anchor = owner.Center + new Vector2(0f, -28f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.5f + Seed) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢就贴回来，别拖着编队横穿半张地图
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildWhips(owner.GetModPlayer<KikasaDomainPlayer>());
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.09f;
            const float maxSpeed = 17f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);

            //出手裁决：鞭只有一式——轮转鞭笞；owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 26) {
                attackIndex++;
                StateTimer = 0;
                StateParam = 0;
                State = StateLash;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 轮转鞭笞 ====================

        /// <summary>该鞭当轮的本地相位；不在轮里返回 -1</summary>
        private int LashPhase(int i, int t) {
            int start = LashLead + i * Profile.LashPeriod;
            int p = t - start;
            return p >= 0 && p <= TurnLen ? p : -1;
        }

        /// <summary>
        /// 当轮驻位与甩向声明：驻距=鞭尖峰值探出×0.62（鞭尖越过目标一截，扫得慷慨），
        /// 方位在主人侧基准上按鞭序与轮次确定性偏摆；预判提前量按蓄势+半程鞭响对齐
        /// </summary>
        private void DeclareLash(int i, Player owner, int target) {
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * (WindupLen + Profile.LashTime * 0.5f)
                : Projectile.Center + new Vector2(owner.direction * 200f, 0f);
            float side = Seed * 2.7f + i * 2.1f + attackIndex * 1.29f;
            float bearing = (owner.Center - focus).ToRotation()
                + MathF.Sin(side) * 1.05f + (i - (whipCount - 1) * 0.5f) * 0.7f;
            Vector2 post = focus + bearing.ToRotationVector2() * (Profile.PeakReach * 0.62f)
                + new Vector2(0f, -12f);
            lashPost[i] = post;
            lashAng[i] = (focus - post).ToRotation();
        }

        private void UpdateLash(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= LashLead) {
                EndAttack(authority, 50);
                return;
            }

            //质心压到目标侧近位陪着鞭群
            Vector2 focus = target >= 0 ? Main.npc[target].Center : Projectile.Center;
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 anchor = owner.Center + toT * 80f + new Vector2(0f, -24f);
            Vector2 desired = (anchor - Projectile.Center) * 0.1f;
            if (desired.Length() > 15f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 15f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.16f);

            for (int i = 0; i < whipCount; i++) {
                int p = LashPhase(i, t);
                if (p < 0) {
                    continue;
                }
                if (!aimDeclared[i]) {
                    //抢位起手声明驻位与甩向（跳帧进窗也补上）；轻声引拍
                    aimDeclared[i] = true;
                    DeclareLash(i, owner, target);
                    SoundEngine.PlaySound(SoundID.Unlock with {
                        Volume = 0.28f,
                        Pitch = -0.45f + i * 0.07f,
                        MaxInstances = 3
                    }, whipPos[i]);
                }
                //甩出帧起放行一次：节拍闩防快照回卷重甩，跳帧迟到也补甩
                if (p >= WindupLen && 0 > lastLashTick[i]) {
                    lastLashTick[i] = 0;
                    LaunchLash(owner, authority, i);
                }
                //鞭响帧：鞭柄后坐一顿（鞭体弹幕在同拍炸响鞭尖）
                if (p == WindupLen + Profile.LashTime / 2) {
                    whipVel[i] -= lashAng[i].ToRotationVector2() * 2.6f;
                    whipSpin[i] += (i % 2 == 0 ? 1f : -1f) * 0.2f;
                    if (ViewedOwner) {
                        ShakeViewer(1.5f);
                    }
                }
            }

            if (t >= LashTotal) {
                EndAttack(authority, 70 + Profile.LashPeriod / 2);
            }
        }

        /// <summary>起鞭：挥音落在甩出帧，owner 端从鞭柄驻位甩出鞭体弹幕（生成包自含全部初值）</summary>
        private void LaunchLash(Player owner, bool authority, int i) {
            Vector2 dir = lashAng[i].ToRotationVector2();

            SoundEngine.PlaySound(Profile.SwingSound with {
                Volume = 0.48f,
                Pitch = -0.05f + i * 0.05f,
                MaxInstances = 4
            }, whipPos[i]);

            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                    .ApplyTo(LashDamage * Profile.LashDamageMul);
                //弹速=原武器 shootSpeed：鞭体曲线的 reach 公式按原版语义消费它
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), whipPos[i],
                    dir * Profile.ShootSpeed,
                    ModContent.ProjectileType<KikasaWhipLash>(), damage, 2f, Projectile.owner,
                    armsItemType);
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

            for (int i = 0; i < whipCount; i++) {
                int lt = t - i * DissolveStagger;
                if (lakeAlive && !dissolveSplashed[i] && lt >= 0 && whipPos[i].Y >= lakeY) {
                    dissolveSplashed[i] = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.5f,
                        Pitch = -0.35f + i * 0.09f,
                        MaxInstances = 3
                    }, whipPos[i]);
                    if (ViewedOwner) {
                        Vector2 hit = new(whipPos[i].X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 5);
                        KikasaDomainDeco.RippleAt(hit, 0.8f);
                        ShakeViewer(0.9f);
                    }
                }
            }

            //边沉边化成水珠
            if (!Main.dedServ && WhipAlpha(0) > 0.15f) {
                int i = t % whipCount;
                if (t - i * DissolveStagger >= 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        whipPos[i] + Main.rand.NextVector2Circular(14f, 10f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.6f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Main.rand.Next(12, 20), 0f);
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

        //==================== 各鞭推进 ====================

        /// <summary>初始化或硬纠时按当前状态直接落位，防鞭群与残影抽搐</summary>
        private void RebuildWhips(KikasaDomainPlayer domain) {
            whipsInit = true;
            for (int i = 0; i < MaxWhips; i++) {
                if (State == StateEmerge) {
                    whipPos[i] = new Vector2(BreachX(i), domain.LakeWorldY + 24f);
                    whipRot[i] = 0f;
                }
                else {
                    float phase = Main.GlobalTimeWrappedHourly * 0.52f + Seed + i * MathHelper.TwoPi / Math.Max(whipCount, 1);
                    whipPos[i] = Projectile.Center + new Vector2(MathF.Cos(phase) * 88f, MathF.Sin(phase) * 40f - 28f);
                    whipRot[i] = 0f;
                }
                whipVel[i] = Vector2.Zero;
                whipSpin[i] = 0f;
                whipTarget[i] = whipPos[i];
                lashPost[i] = whipPos[i];
                lashAng[i] = 0f;
                whipOld[i] ??= new Vector2[8];
                whipOldRot[i] ??= new float[8];
                for (int k = 0; k < whipOld[i].Length; k++) {
                    whipOld[i][k] = whipPos[i];
                    whipOldRot[i][k] = whipRot[i];
                }
            }
        }

        private void ChaseWhip(int i, float accel, float damp) {
            whipVel[i] = (whipVel[i] + (whipTarget[i] - whipPos[i]) * accel) * damp;
            whipPos[i] += whipVel[i];
        }

        /// <summary>呼吸浮动相位（Seed 确定性，各端一致）</summary>
        private float Sway(int i, float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed + i * 2.4f) * amp;

        private void UpdateWhips(Player owner, KikasaDomainPlayer domain) {
            if (!whipsInit) {
                return;
            }
            int t = (int)StateTimer;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    for (int i = 0; i < whipCount; i++) {
                        if (t < BreachTime(i)) {
                            whipPos[i] = new Vector2(BreachX(i), lakeY + 24f);
                            whipVel[i] = Vector2.Zero;
                            whipTarget[i] = whipPos[i];
                            whipRot[i] = 0f;
                            continue;
                        }
                        whipTarget[i] = new Vector2(BreachX(i), lakeY - 84f + Sway(i, 1.9f, 8f));
                        int lt = t - BreachTime(i);
                        if (lt < 14) {
                            whipVel[i].Y *= 0.955f;
                            whipVel[i].X *= 0.98f;
                            whipPos[i] += whipVel[i];
                            whipRot[i] += whipSpin[i];
                            whipSpin[i] *= 0.94f;
                        }
                        else {
                            ChaseWhip(i, 0.05f, 0.86f);
                            whipRot[i] += whipSpin[i];
                            whipSpin[i] *= 0.9f;
                            if (MathF.Abs(whipSpin[i]) < 0.05f) {
                                //翻腾散尽回正：盘鞭安安静静浮着
                                whipRot[i] = whipRot[i].AngleLerp(0f, 0.12f);
                            }
                        }
                    }
                    break;
                }
                case StateFollow: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    for (int i = 0; i < whipCount; i++) {
                        float phase = tGlobal * 0.52f + Seed + i * MathHelper.TwoPi / whipCount;
                        Vector2 slot = Projectile.Center + new Vector2(MathF.Cos(phase) * 88f, MathF.Sin(phase) * 40f - 28f);
                        slot.Y += MathF.Sin(tGlobal * 2f + Seed * 2f + i * 1.9f) * 6f;
                        whipTarget[i] = slot;
                        ChaseWhip(i, 0.06f, 0.84f);
                        //盘鞭微倾慢晃；错帧偶发一记盘卷抽动——静里的一点活
                        float twitchT = (t + i * 71) % 220;
                        if (twitchT < 16f) {
                            whipRot[i] += MathF.Sin(twitchT / 16f * MathHelper.Pi) * 0.1f;
                        }
                        else {
                            whipRot[i] = whipRot[i].AngleLerp(Sway(i, 1.3f, 0.16f), 0.08f);
                        }
                    }
                    break;
                }
                case StateLash: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    for (int i = 0; i < whipCount; i++) {
                        int p = LashPhase(i, t);
                        if (p < 0) {
                            //不在轮里：退居质心环位候场
                            float phase = tGlobal * 0.52f + Seed + i * MathHelper.TwoPi / whipCount;
                            whipTarget[i] = Projectile.Center + new Vector2(MathF.Cos(phase) * 88f, MathF.Sin(phase) * 40f - 28f);
                            ChaseWhip(i, 0.06f, 0.84f);
                            whipRot[i] = whipRot[i].AngleLerp(Sway(i, 1.3f, 0.14f), 0.08f);
                            continue;
                        }
                        Vector2 aimDir = lashAng[i].ToRotationVector2();
                        if (p < WindupLen) {
                            //抢位短蓄：快扑驻位，鞭柄向后仰蓄势
                            float ease = p / (float)WindupLen;
                            whipTarget[i] = lashPost[i] - aimDir * (10f * (1f - ease));
                            ChaseWhip(i, 0.22f, 0.7f);
                            whipRot[i] = whipRot[i].AngleLerp(-aimDir.X * 0.5f - 0.3f * MathF.Sign(aimDir.X), 0.25f);
                        }
                        else {
                            //甩出中：鞭柄钉在驻位（鞭体弹幕接管画面），后坐/回旋衰减
                            whipTarget[i] = lashPost[i];
                            ChaseWhip(i, 0.3f, 0.55f);
                            whipRot[i] += whipSpin[i];
                            whipSpin[i] *= 0.88f;
                        }
                    }
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    for (int i = 0; i < whipCount; i++) {
                        int lt = t - i * DissolveStagger;
                        if (lt < 0) {
                            whipVel[i] *= 0.95f;
                            whipVel[i].Y += 0.05f;
                        }
                        else {
                            whipVel[i].X *= 0.93f;
                            whipVel[i].Y = MathF.Min(whipVel[i].Y + 0.3f, 9.5f);
                            whipRot[i] += whipSpin[i] * 0.5f;
                        }
                        whipPos[i] += whipVel[i];
                        whipTarget[i] = whipPos[i];
                    }
                    break;
                }
            }

            //硬纠：同步包把质心拽走半屏时按驻位重建，防弹簧甩鞭
            if (!skipFix) {
                for (int i = 0; i < whipCount; i++) {
                    if (Vector2.Distance(whipPos[i], whipTarget[i]) > 780f) {
                        whipPos[i] = whipTarget[i];
                        whipVel[i] = Vector2.Zero;
                    }
                }
            }
        }

        private void PushWhipHistory() {
            for (int i = 0; i < whipCount; i++) {
                Vector2[] arr = whipOld[i];
                float[] rots = whipOldRot[i];
                if (arr == null) {
                    continue;
                }
                for (int k = arr.Length - 1; k >= 1; k--) {
                    arr[k] = arr[k - 1];
                    rots[k] = rots[k - 1];
                }
                arr[0] = whipPos[i];
                rots[0] = whipRot[i];
            }
        }

        /// <summary>常驻氛围：盘鞭下缘偶发凝珠滴落</summary>
        private void UpdateAmbient() {
            if (Main.dedServ || State is not (StateFollow or StateLash)) {
                return;
            }
            if (Main.rand.NextBool(18)) {
                int i = Main.rand.Next(whipCount);
                if (WhipAlpha(i) > 0.5f) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        whipPos[i] + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(5f, 12f)),
                        new Vector2(0f, Main.rand.NextFloat(0.5f, 1f)),
                        BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                        Main.rand.NextFloat(0.26f, 0.46f))?.Configure(Main.rand.Next(16, 26), 0f);
                }
            }
        }

        //==================== 公共小件 ====================

        private int FindTarget(Player owner) {
            if (owner.HasMinionAttackTargetNPC) {
                NPC picked = Main.npc[owner.MinionAttackTargetNPC];
                if (picked.CanBeChasedBy(Projectile)
                    && Vector2.Distance(picked.Center, owner.Center) < 1300f) {
                    return picked.whoAmI;
                }
            }
            int best = -1;
            float bestDist = 900f;
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

        private float WhipAlpha(int i) {
            int t = (int)StateTimer;
            float alpha = State switch {
                StateEmerge => t < BreachTime(i) ? 0f : MathHelper.Clamp((t - BreachTime(i)) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
            //甩出窗内盘鞭让位给鞭体弹幕（鞭体自带鞭柄帧，双柄会穿帮）
            if (IsLashing(i)) {
                alpha *= 0.18f;
            }
            return alpha;
        }

        /// <summary>该鞭正处在甩出窗里（鞭体弹幕在场的时段）</summary>
        private bool IsLashing(int i) {
            if (State != StateLash) {
                return false;
            }
            int p = LashPhase(i, (int)StateTimer);
            return p >= WindupLen && p < WindupLen + Profile.LashTime;
        }

        /// <summary>uForm 水线呼吸：同族契约——实体上半 + 液态下缘</summary>
        private float WhipForm(int i) {
            int t = (int)StateTimer;
            float steady = 0.24f
                + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + Seed + i * 1.7f) * 0.06f;
            return State switch {
                StateEmerge => t < BreachTime(i)
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp(
                        (t - BreachTime(i)) / (float)(RiseEnd - BreachTime(i)), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.6f, 0f, 1f),
                _ => steady,
            };
        }

        /// <summary>uDissolve：溶解期逐鞭错帧蚀散，落水的先散</summary>
        private float DissolveAmt(int i) {
            if (State != StateDissolve) {
                return 0f;
            }
            float p = MathF.Pow(MathHelper.Clamp((StateTimer - i * DissolveStagger) / 44f, 0f, 1f), 0.9f);
            return MathHelper.Clamp(p + (dissolveSplashed[i] ? 0.15f : 0f), 0f, 1f);
        }

        private float WhipScale(int i) {
            float scale = 1f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= BreachTime(i) && t < BreachTime(i) + 10) {
                //破水过冲
                scale *= 1f + 0.08f * (1f - (t - BreachTime(i)) / 10f);
            }
            return scale * Profile.DrawScale;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!whipsInit) {
                return false;
            }
            Main.instance.LoadItem(armsItemType);
            Texture2D tex = TextureAssets.Item[armsItemType]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            //抢位拖影
            DrawDashTrails(sb, tex);

            //盘鞭本体：血湖材质
            DrawBodies(sb, tex);

            //加色层：预兆水光 / 蓄势鞭柄亮意
            DrawGlow(sb);

            return false;
        }

        private void DrawDashTrails(SpriteBatch sb, Texture2D tex) {
            Vector2 origin = tex.Size() * 0.5f;
            for (int i = 0; i < whipCount; i++) {
                float trailA = MathHelper.Clamp((whipVel[i].Length() - 8f) / 12f, 0f, 1f) * WhipAlpha(i);
                if (trailA <= 0.03f) {
                    continue;
                }
                Vector2[] arr = whipOld[i];
                float[] rots = whipOldRot[i];
                for (int k = arr.Length - 1; k >= 1; k--) {
                    float fall = 1f - k / (float)arr.Length;
                    sb.Draw(tex, arr[k] - Main.screenPosition, null,
                        BloodMain * (0.28f * fall * trailA), rots[k],
                        origin, WhipScale(i) * (0.97f - k * 0.015f), SpriteEffects.None, 0f);
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
            for (int i = 0; i < whipCount; i++) {
                float alpha = WhipAlpha(i);
                if (alpha <= 0.01f) {
                    continue;
                }
                float rot = whipRot[i];
                Vector2 drawPos = whipPos[i] - Main.screenPosition;
                float dissolve = DissolveAmt(i);

                //液态水鞘包衣：同一剪影放大一号、全血水态、独立慢晃
                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed + i * 1.3f;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.5f, MathF.Cos(wt * 0.83f) * 1.9f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.03f;
                    float envScale = WhipScale(i) * (1.13f + MathF.Sin(wt * 1.6f) * 0.025f);
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 2.3f + 5.1f);
                    form.Parameters["uForm"]?.SetValue(1f);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(tex, drawPos + wobOff, null,
                        new Color(255, 255, 255, (byte)(alpha * 130f)),
                        rot + wobRot, origin, envScale, SpriteEffects.None, 0f);
                }

                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 2.3f);
                    form.Parameters["uForm"]?.SetValue(WhipForm(i));
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    //无着色器回退：CPU 血染
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }

                sb.Draw(tex, drawPos, null, color,
                    rot, origin, WhipScale(i), SpriteEffects.None, 0f);
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

            //预兆：几处水下血光并肩上浮
            if (State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                EnsureBegin();
                for (int i = 0; i < whipCount; i++) {
                    Vector2 pos = new(BreachX(i), domain.LakeWorldY + MathHelper.Lerp(40f, 8f, ease));
                    float r = 16f + 12f * ease;
                    sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * (0.35f * ease), 0f,
                        gOrigin, new Vector2(r * 2.2f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //蓄势亮意：抢位短蓄期鞭柄积起一点水光——要起鞭了
            if (State == StateLash) {
                for (int i = 0; i < whipCount; i++) {
                    int p = LashPhase(i, t);
                    if (p < 0 || p >= WindupLen) {
                        continue;
                    }
                    float charge = p / (float)WindupLen;
                    EnsureBegin();
                    sb.Draw(glow, whipPos[i] - Main.screenPosition, null,
                        BloodBright * (0.3f * charge), 0f,
                        gOrigin, new Vector2(18f * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            //谢幕残珠：每条鞭留一口水
            if (Main.dedServ || !whipsInit) {
                return;
            }
            for (int i = 0; i < whipCount; i++) {
                for (int k = 0; k < 4; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        whipPos[i] + Main.rand.NextVector2Circular(12f, 10f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.5f, 2.2f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Main.rand.Next(12, 22), 0f);
                }
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.6f, Main.rand.NextFloat(0.45f, 0.75f))
                ?.Configure(Main.rand.Next(40, 65));
        }
    }
}
