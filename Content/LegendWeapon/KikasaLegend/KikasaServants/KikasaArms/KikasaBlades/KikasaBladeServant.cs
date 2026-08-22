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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaBlades
{
    /// <summary>
    /// 械奴·湖水刃群（通用刀奴）。单弹幕同时驱动至多四柄湖水凝成的刀剑：
    /// Projectile.Center 为编队质心权威同步，各刃位置由状态机 + Seed 在各端本地推算，
    /// 硬纠阈值防抽搐，联机契约与枪奴/双子同构（owner 裁决转场盖 netUpdate 章、
    /// 节拍闩防快照回卷、生命线只有 owner 判、刃数与武器类型经 ExtraAI 随包补发）。
    /// 材质身份与枪奴同族：凝不全的湖水刃（KikasaItemForm 扫描水线）+ 水鞘包衣 + 凝珠滴淌。
    /// 挥砍语法按刀光编排铁律走收-爆-停：轮转突斩＝逐刃错帧接力，收（拉到冲线后端
    /// 蓄势减速到近停）→爆（两帧穿过目标，刃体隐去、路径拖影承载运动）→停（过冲点
    /// 硬停驻帧，斩痕在切线上炸开）；合围十字＝全员冲至目标环位刃尖内指、同步蓄势
    /// 静谷后齐发穿心交错，心点撞拍重收。伤害全在 <see cref="KikasaBladeSlash"/> 斩痕
    /// 弹幕上（owner 生成、生成包自含冲线），刃体不做接触判定。
    /// 个性化由 KikasaArmsProfiler 刀档案承担：轻重档配蓄/停时长与刃数，
    /// 巨兵的突斩偏成过顶下劈；节奏/伤害/挥砍音/规格随沉入武器推得
    /// </summary>
    internal class KikasaBladeServant : ModProjectile, IKikasaArmsServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>斩痕基伤（召唤加成与档案倍率前），由斩痕弹幕消费</summary>
        internal const int SlashDamage = 160;

        /// <summary>编队硬上限：数组容量，实际编制还要过档案 MaxUnits</summary>
        internal const int MaxBlades = 4;

        //==================== 档案 ====================

        /// <summary>沉入湖中的原型武器物品类型：贴图与档案来源，ExtraAI 同步</summary>
        private int armsItemType = ItemID.Katana;

        /// <summary>沉影盘在场判定用：这队械奴复制的是哪件武器</summary>
        public int ArmsItemType => armsItemType;

        private KikasaBladeProfile? profileCache;

        /// <summary>档案惰性求值：模板实例化早于 ContentSamples 灌装，首次访问再推</summary>
        private KikasaBladeProfile Profile => profileCache ??= KikasaArmsProfiler.BladeProfileOf(armsItemType);

        private void SetArmsItemType(int itemType) {
            armsItemType = itemType;
            profileCache = null;
        }

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateRelay = 2;
        private const int StateDissolve = 3;
        private const int StateCross = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：目前未用，保位与枪奴同构</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：多点预兆→逐刃错帧立剑破水→翻转落定→整队出鞘鸣
        private const int OmenFrames = 24;
        private const int BreachGap = 8;
        private const int RiseEnd = 66;
        private const int SheatheFrame = 76;
        private const int EmergeTotal = 92;
        /// <summary>相邻破水点横距</summary>
        private const float EmergeSpan = 56f;

        //轮转突斩：起手引拍后逐刃错帧接力，接力间隔走档案 RelayPeriod
        private const int RelayLead = 10;

        /// <summary>蓄势帧数：拉到冲线后端减速到近停（轻快重慢）</summary>
        private int GatherLen => Profile.Weight switch {
            KikasaBladeWeight.Grand => 20,
            KikasaBladeWeight.Heavy => 16,
            _ => 12,
        };

        /// <summary>驻帧帧数：过冲点硬停凝住几何（重的停更久，停是力量的一半）</summary>
        private int RestLen => Profile.Weight switch {
            KikasaBladeWeight.Grand => 16,
            KikasaBladeWeight.Heavy => 12,
            _ => 8,
        };

        /// <summary>单刃完整一轮：蓄 + 两帧爆 + 停</summary>
        private int TurnLen => GatherLen + 2 + RestLen;

        private int RelayTotal => RelayLead + Profile.RelayPeriod * (bladeCount - 1) + TurnLen + 12;

        //合围十字：冲环位→刃尖内指静谷蓄势→齐发穿心→驻帧收势
        private const int CrossPostEnd = 20;
        private const int CrossHoldEnd = 38;
        private const int CrossRestEnd = 56;
        private const int CrossTotal = 72;
        private const float CrossRadius = 170f;

        //溶解：逐刃错帧失力坠湖
        private const int DissolveStagger = 5;
        private const int DissolveFrames = 70;

        //==================== 各刃本地模拟（各端自算，质心同步纠偏）====================

        private readonly Vector2[] bladePos = new Vector2[MaxBlades];
        private readonly Vector2[] bladeVel = new Vector2[MaxBlades];
        private readonly Vector2[] bladeTarget = new Vector2[MaxBlades];
        /// <summary>刃尖指向角（绘制时补贴图斜置修正）</summary>
        private readonly float[] bladeRot = new float[MaxBlades];
        /// <summary>出水翻腾角速度</summary>
        private readonly float[] bladeSpin = new float[MaxBlades];
        //各刃当轮冲线（p==0 锁定，蓄势/爆发/斩痕共用同一条线，先声明后砍）
        private readonly Vector2[] dashFrom = new Vector2[MaxBlades];
        private readonly Vector2[] dashTo = new Vector2[MaxBlades];
        private readonly float[] dashAng = new float[MaxBlades];
        private readonly Vector2[][] bladeOld = new Vector2[MaxBlades][];
        private readonly float[][] bladeOldRot = new float[MaxBlades][];
        private bool bladesInit;

        /// <summary>编队刃数：owner 在 Summon 里定值，ExtraAI 随包同步；远端首包前按满编</summary>
        private int bladeCount = MaxBlades;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private readonly bool[] breachDone = new bool[MaxBlades];
        private readonly int[] lastSlashTick = new int[MaxBlades];
        /// <summary>当轮冲线已声明：远端快照跳帧错过声明帧时进窗补声明，不留陈旧冲线</summary>
        private readonly bool[] dashDeclared = new bool[MaxBlades];
        private readonly bool[] dissolveSplashed = new bool[MaxBlades];
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;
        private bool sheatheSnapDone;
        private bool crossImpactDone;
        private int crossFlashTick;

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
            KikasaBladeProfile profile = KikasaArmsProfiler.BladeProfileOf(itemType);
            count = Math.Clamp(count, 1, profile.MaxUnits);
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SlashDamage * profile.SlashDamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaBladeServant>(), damage, 3f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaBladeServant pack) {
                //生成包已经带默认编制出门了，这里改完补一发 ExtraAI（迟一帧只影响预兆涟漪点数）
                pack.bladeCount = count;
                pack.SetArmsItemType(itemType);
                Main.projectile[index].netUpdate = true;
            }
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //突斩与合围的刃群散布远超质心 hitbox，出屏也要画
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

        /// <summary>刃群不做接触判定，伤害全在斩痕弹幕上</summary>
        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(armsItemType);
            writer.Write((byte)bladeCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int itemType = reader.ReadInt32();
            int count = reader.ReadByte();
            if (itemType > ItemID.None && itemType < ItemLoader.ItemCount && itemType != armsItemType) {
                SetArmsItemType(itemType);
            }
            count = Math.Clamp(count, 1, Profile.MaxUnits);
            if (count != bladeCount) {
                bladeCount = count;
                //编制变了按新编制重建
                bladesInit = false;
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
            //一柄都没破水就要收场：直接收掉，免得溶解演出让刃凭空闪现再化水
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

            //生命线：只有 owner 裁决，服务器无领域状态（既定契约）
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SlashDamage * Profile.SlashDamageMul);

            //换场清闩：远端可能靠收包换场而非本地同拍转场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                crossImpactDone = false;
                Array.Fill(lastSlashTick, -1);
                Array.Fill(dashDeclared, false);
                if (State == StateDissolve) {
                    Array.Fill(dissolveSplashed, false);
                }
            }

            if (!bladesInit) {
                RebuildBlades(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateRelay: UpdateRelay(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
                case StateCross: UpdateCross(owner, authority); break;
            }

            UpdateBlades(owner, domain);
            PushBladeHistory();
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (crossFlashTick > 0) {
                crossFlashTick--;
            }
            for (int i = 0; i < bladeCount; i++) {
                float glow = BladeAlpha(i) * 0.35f;
                if (glow > 0.02f) {
                    Lighting.AddLight(bladePos[i], 0.4f * glow, 0.1f * glow, 0.09f * glow);
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：立剑破水、翻转落定、出鞘鸣 ====================

        private float BreachX(int i)
            => Projectile.Center.X + (i - (bladeCount - 1) * 0.5f) * EmergeSpan;

        private static int BreachTime(int i) => OmenFrames + i * BreachGap;

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                //水下待命：几处水面同时起预兆
                Projectile.velocity = Vector2.Zero;
                if (viewed && t % 5 == 2) {
                    float converge = 1f - t / (float)OmenFrames;
                    for (int i = 0; i < bladeCount; i++) {
                        float wobble = MathF.Sin(t * 0.5f + i * 1.7f) * converge * 22f;
                        KikasaDomainDeco.RippleAt(new Vector2(BreachX(i) + wobble, lakeY),
                            0.3f + (1f - converge) * 0.4f);
                    }
                }
                if (viewed && (t == 5 || t == 15)) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.42f,
                        Pitch = -0.55f + t * 0.012f,
                        MaxInstances = 3
                    }, new Vector2(Projectile.Center.X, lakeY));
                }
                return;
            }

            //错帧破水：立剑尖朝上一柄接一柄刺出水面
            for (int i = 0; i < bladeCount; i++) {
                if (!breachDone[i] && t >= BreachTime(i)) {
                    breachDone[i] = true;
                    bladeVel[i] = new Vector2(0f, -11.8f - i * 0.3f);
                    bladeSpin[i] = (i % 2 == 0 ? 1f : -1f) * 0.22f;
                    if (i == 0) {
                        Projectile.velocity = new Vector2(0f, -3f);
                    }
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.7f,
                        Pitch = -0.3f + i * 0.07f,
                        MaxInstances = 3
                    }, bladePos[i]);
                    if (viewed) {
                        BreachBurst(new Vector2(BreachX(i), lakeY), i);
                    }
                }
            }

            Projectile.velocity *= 0.96f;

            //身上的湖水成帘往下淌
            if (viewed && t < RiseEnd) {
                for (int i = 0; i < bladeCount; i++) {
                    if (t < BreachTime(i) || t % 3 != i % 3) {
                        continue;
                    }
                    Vector2 dropPos = bladePos[i] + new Vector2(
                        Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(2f, 14f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.4f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(12, 24), 0f);
                }
            }

            //整队出鞘鸣：全员一顿、一声幽冷刃鸣，它们醒了
            if (!sheatheSnapDone && t >= SheatheFrame) {
                sheatheSnapDone = true;
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.42f, Pitch = -0.15f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = -0.1f, MaxInstances = 2 }, Projectile.Center);
                for (int i = 0; i < bladeCount; i++) {
                    bladeVel[i] += new Vector2(
                        -MathF.Sign(bladePos[i].X - Projectile.Center.X) * 1.6f, -1.1f);
                    if (viewed) {
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(
                                bladePos[i] + Main.rand.NextVector2Circular(14f, 10f),
                                new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 1.7f)),
                                BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))
                                ?.Configure(Main.rand.Next(10, 18), 0.25f);
                        }
                    }
                }
                if (viewed) {
                    ShakeViewer(2f);
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

        /// <summary>单刃破水浪冠：规格比枪奴同款收一号（刃形窄，水花也窄）</summary>
        private void BreachBurst(Vector2 hit, int i) {
            KikasaDomainDeco.RippleAt(hit, 1.3f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(18f, 0f), 0.5f);
            KikasaDomainDeco.SplashAt(hit, 7);

            for (int k = 0; k < 10; k++) {
                float angle = -MathHelper.Pi * (0.2f + 0.6f * k / 9f);
                float speed = Main.rand.NextFloat(2.4f, 5.6f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-10f, 10f), -4f),
                    angle.ToRotationVector2() * speed,
                    BloodMain * Main.rand.NextFloat(0.45f, 0.65f),
                    Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                hit + new Vector2(Main.rand.NextFloat(-14f, 14f), -8f),
                new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.6f)),
                MistBlood * 0.75f, Main.rand.NextFloat(0.5f, 0.75f))
                ?.Configure(Main.rand.Next(50, 80));

            if (i == 0 || i == bladeCount - 1) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = 0.3f,
                    Pitch = -0.7f,
                    MaxInstances = 1
                }, hit);
            }
            ShakeViewer(1.4f);
        }

        //==================== 跟随：刃群环游 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            //质心锚：贴着玩家，编队绕质心游
            Vector2 anchor = owner.Center + new Vector2(0f, -30f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f + Seed) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢就贴回来，别拖着编队横穿半张地图
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildBlades(owner.GetModPlayer<KikasaDomainPlayer>());
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.09f;
            const float maxSpeed = 17f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);

            //出手裁决：轮转突斩为主，隔次合围十字重拍；owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                StateTimer = 0;
                StateParam = 0;
                //单刃编制没有合围的意义，恒走突斩
                State = attackIndex % 2 == 1 || bladeCount < 2 ? StateRelay : StateCross;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 轮转突斩：收-爆-停接力 ====================

        /// <summary>该刃当轮的本地相位；不在轮里返回 -1</summary>
        private int RelayPhase(int i, int t) {
            int start = RelayLead + i * Profile.RelayPeriod;
            int p = t - start;
            return p >= 0 && p <= TurnLen ? p : -1;
        }

        /// <summary>当轮冲线声明：蓄势起点锁定目标提前量，蓄/爆/斩痕共用，先声明后砍</summary>
        private void DeclareDash(int i, Player owner, int target) {
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * (GatherLen + 2)
                : Projectile.Center + new Vector2(owner.direction * 300f, 0f);
            //各刃换不同刀路：确定性偏角，巨兵偏成过顶下劈
            float skew = MathF.Sin(Seed * 3.1f + i * 2.39f + attackIndex * 1.71f) * 0.8f;
            float ang = Profile.Weight == KikasaBladeWeight.Grand
                ? MathHelper.PiOver2 + skew * 0.4f
                : (focus - owner.Center).ToRotation() + skew;
            Vector2 dir = ang.ToRotationVector2();
            float reach = Profile.BladeLen * 1.15f + 70f;
            dashFrom[i] = focus - dir * reach;
            dashTo[i] = focus + dir * reach * 0.85f;
            dashAng[i] = ang;
        }

        private void UpdateRelay(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= RelayLead) {
                EndAttack(authority, 50);
                return;
            }

            //质心压到目标侧近位，随刃群接力缓慢横移
            Vector2 focus = target >= 0 ? Main.npc[target].Center : Projectile.Center;
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 anchor = owner.Center + toT * 90f + new Vector2(0f, -26f);
            Vector2 desired = (anchor - Projectile.Center) * 0.1f;
            if (desired.Length() > 15f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 15f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.16f);

            for (int i = 0; i < bladeCount; i++) {
                int p = RelayPhase(i, t);
                if (p < 0) {
                    continue;
                }
                if (!dashDeclared[i]) {
                    //蓄势起点锁线（跳帧进窗也补上）；轻声引拍，刀要来了
                    dashDeclared[i] = true;
                    DeclareDash(i, owner, target);
                    SoundEngine.PlaySound(SoundID.Unlock with {
                        Volume = 0.3f,
                        Pitch = -0.5f + i * 0.06f,
                        MaxInstances = 3
                    }, bladePos[i]);
                }
                //爆发帧起放行一次：节拍闩防快照回卷重砍，跳帧迟到也补砍
                if (p >= GatherLen && 0 > lastSlashTick[i]) {
                    lastSlashTick[i] = 0;
                    LaunchSlash(owner, authority, i, heavyBeat: false);
                }
            }

            if (t >= RelayTotal) {
                EndAttack(authority, 95 + Profile.RelayPeriod);
            }
        }

        /// <summary>
        /// 爆发一刀：主音落在爆发帧（不在蓄势），owner 端沿声明冲线生成斩痕弹幕
        /// （生成包自含全部初值），刃体自己的位移在 UpdateBlades 里走两帧穿越
        /// </summary>
        private void LaunchSlash(Player owner, bool authority, int i, bool heavyBeat) {
            Vector2 dir = dashAng[i].ToRotationVector2();
            Vector2 mid = (dashFrom[i] + dashTo[i]) * 0.5f;

            //主挥砍音借原武器 UseSound，重拍垫一记破空
            SoundEngine.PlaySound(Profile.SwingSound with {
                Volume = heavyBeat ? 0.6f : 0.5f,
                Pitch = Profile.Weight switch {
                    KikasaBladeWeight.Grand => -0.25f,
                    KikasaBladeWeight.Heavy => -0.1f,
                    _ => 0.08f,
                } + i * 0.04f,
                MaxInstances = 4
            }, mid);
            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with {
                Volume = heavyBeat ? 0.5f : 0.34f,
                Pitch = 0.15f,
                MaxInstances = 3
            }, mid);
            if (ViewedOwner) {
                ShakeViewer(Profile.Weight switch {
                    KikasaBladeWeight.Grand => 2.8f,
                    KikasaBladeWeight.Heavy => 2f,
                    _ => 1.2f,
                } * (heavyBeat ? 1.25f : 1f));
            }

            //斩痕只在 owner 端生成，spawn 包自带冲线（ai0=判定半长，ai1=重拍）
            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                    .ApplyTo(SlashDamage * Profile.SlashDamageMul * (heavyBeat ? 1.25f : 1f));
                float halfLen = Profile.BladeLen * 1.05f + 34f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), mid, dir * 3.2f,
                    ModContent.ProjectileType<KikasaBladeSlash>(), damage, 3f, Projectile.owner,
                    halfLen, heavyBeat ? 1f : 0f);
            }
        }

        //==================== 合围十字：静谷蓄势、齐发穿心 ====================

        private float CrossAngle(int i)
            => Seed * 2.1f + attackIndex * 0.97f + i * MathHelper.TwoPi / Math.Max(bladeCount, 1);

        private void UpdateCross(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= CrossPostEnd) {
                EndAttack(authority, 60);
                return;
            }

            //质心压在猎物身上：环心即权威锚
            if (t <= CrossHoldEnd) {
                Vector2 want = target >= 0
                    ? (Main.npc[target].Center + Main.npc[target].velocity * 4f - Projectile.Center) * 0.16f
                    : Vector2.Zero;
                if (want.Length() > 22f) {
                    want = want.SafeNormalize(Vector2.Zero) * 22f;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.28f);
            }
            else {
                Vector2 back = (owner.Center + new Vector2(0f, -30f) - Projectile.Center) * 0.08f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, back, 0.14f);
            }

            //静谷上膛：两声轻响后全场压住不动，静得越彻底，穿心越有力
            if (t == CrossPostEnd + 4 || t == CrossPostEnd + 12) {
                SoundEngine.PlaySound(SoundID.Unlock with {
                    Volume = 0.4f,
                    Pitch = -0.4f + (t - CrossPostEnd) * 0.02f,
                    MaxInstances = 2
                }, Projectile.Center);
            }

            //齐发穿心：每刃沿自己的直径线出斩痕；进窗即声明+出刀（跳帧迟到也补），节拍闩防重砍
            if (t >= CrossHoldEnd) {
                for (int i = 0; i < bladeCount; i++) {
                    if (0 > lastSlashTick[i]) {
                        lastSlashTick[i] = 0;
                        dashDeclared[i] = true;
                        //冲线=从环位穿过环心到对侧
                        float ang = CrossAngle(i) + MathHelper.Pi;
                        dashAng[i] = ang;
                        Vector2 dir = ang.ToRotationVector2();
                        dashFrom[i] = Projectile.Center - dir * CrossRadius;
                        dashTo[i] = Projectile.Center + dir * CrossRadius * 0.9f;
                        LaunchSlash(owner, authority, i, heavyBeat: true);
                    }
                }
            }

            //心点撞拍：全员擦身而过的重收
            if (!crossImpactDone && t == CrossHoldEnd + 3) {
                crossImpactDone = true;
                crossFlashTick = 8;
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.45f, Pitch = 0.05f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.45f, Pitch = 0.05f, MaxInstances = 2 }, Projectile.Center);
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodBright, 0.09f)
                        ?.Configure(new Vector2(0.6f, 1f), Seed, 0.3f, 9);
                    for (int k = 0; k < 9; k++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                            Main.rand.NextVector2Circular(3.8f, 3.8f),
                            Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                            Main.rand.NextFloat(0.35f, 0.62f))?.Configure(Main.rand.Next(14, 26));
                    }
                }
                if (ViewedOwner) {
                    ShakeViewer(3.2f);
                }
            }

            if (t >= CrossTotal) {
                EndAttack(authority, 165);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解：逐刃失力坠湖 ====================

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

            for (int i = 0; i < bladeCount; i++) {
                int lt = t - i * DissolveStagger;
                if (lakeAlive && !dissolveSplashed[i] && lt >= 0 && bladePos[i].Y >= lakeY) {
                    dissolveSplashed[i] = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.55f,
                        Pitch = -0.35f + i * 0.08f,
                        MaxInstances = 3
                    }, bladePos[i]);
                    if (ViewedOwner) {
                        Vector2 hit = new(bladePos[i].X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 6);
                        KikasaDomainDeco.RippleAt(hit, 0.85f);
                        ShakeViewer(1f);
                    }
                }
            }

            //边沉边化成水珠
            if (!Main.dedServ && BladeAlpha(0) > 0.15f) {
                int i = t % bladeCount;
                if (t - i * DissolveStagger >= 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        bladePos[i] + Main.rand.NextVector2Circular(16f, 12f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.8f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(Main.rand.Next(12, 22), 0f);
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

        //==================== 各刃推进 ====================

        /// <summary>初始化或硬纠时按当前状态直接落位，防刃群与残影抽搐</summary>
        private void RebuildBlades(KikasaDomainPlayer domain) {
            bladesInit = true;
            for (int i = 0; i < MaxBlades; i++) {
                if (State == StateEmerge) {
                    bladePos[i] = new Vector2(BreachX(i), domain.LakeWorldY + 26f);
                    bladeRot[i] = -MathHelper.PiOver2;
                }
                else {
                    float phase = Main.GlobalTimeWrappedHourly * 0.56f + Seed + i * MathHelper.TwoPi / Math.Max(bladeCount, 1);
                    bladePos[i] = Projectile.Center + new Vector2(MathF.Cos(phase) * 96f, MathF.Sin(phase) * 44f - 30f);
                    bladeRot[i] = MathHelper.PiOver2 * 0.8f;
                }
                bladeVel[i] = Vector2.Zero;
                bladeSpin[i] = 0f;
                bladeTarget[i] = bladePos[i];
                dashFrom[i] = bladePos[i];
                dashTo[i] = bladePos[i];
                dashAng[i] = bladeRot[i];
                bladeOld[i] ??= new Vector2[8];
                bladeOldRot[i] ??= new float[8];
                for (int k = 0; k < bladeOld[i].Length; k++) {
                    bladeOld[i][k] = bladePos[i];
                    bladeOldRot[i][k] = bladeRot[i];
                }
            }
        }

        private void ChaseBlade(int i, float accel, float damp) {
            bladeVel[i] = (bladeVel[i] + (bladeTarget[i] - bladePos[i]) * accel) * damp;
            bladePos[i] += bladeVel[i];
        }

        /// <summary>呼吸浮动相位（Seed 确定性，各端一致）</summary>
        private float Sway(int i, float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed + i * 2.4f) * amp;

        private void UpdateBlades(Player owner, KikasaDomainPlayer domain) {
            if (!bladesInit) {
                return;
            }
            int t = (int)StateTimer;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    for (int i = 0; i < bladeCount; i++) {
                        if (t < BreachTime(i)) {
                            //水下待命：钉在破水点，剑尖朝上
                            bladePos[i] = new Vector2(BreachX(i), lakeY + 26f);
                            bladeVel[i] = Vector2.Zero;
                            bladeTarget[i] = bladePos[i];
                            bladeRot[i] = -MathHelper.PiOver2;
                            continue;
                        }
                        //破水后：先弹道升 + 轻翻，14 帧后弹簧接管贴向悬位
                        bladeTarget[i] = new Vector2(BreachX(i), lakeY - 92f + Sway(i, 2f, 8f));
                        int lt = t - BreachTime(i);
                        if (lt < 14) {
                            bladeVel[i].Y *= 0.955f;
                            bladeVel[i].X *= 0.98f;
                            bladePos[i] += bladeVel[i];
                            bladeRot[i] += bladeSpin[i];
                            bladeSpin[i] *= 0.94f;
                        }
                        else {
                            ChaseBlade(i, 0.05f, 0.86f);
                            bladeRot[i] += bladeSpin[i];
                            bladeSpin[i] *= 0.9f;
                            if (MathF.Abs(bladeSpin[i]) < 0.05f) {
                                //翻转散尽后落定：刀尖微垂的收鞘姿
                                bladeRot[i] = bladeRot[i].AngleLerp(MathHelper.PiOver2 * 0.8f, 0.12f);
                            }
                        }
                    }
                    break;
                }
                case StateFollow: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    for (int i = 0; i < bladeCount; i++) {
                        float phase = tGlobal * 0.56f + Seed + i * MathHelper.TwoPi / bladeCount;
                        Vector2 slot = Projectile.Center + new Vector2(MathF.Cos(phase) * 96f, MathF.Sin(phase) * 44f - 30f);
                        slot.Y += MathF.Sin(tGlobal * 2.1f + Seed * 2f + i * 1.9f) * 6f;
                        bladeTarget[i] = slot;
                        ChaseBlade(i, 0.06f, 0.84f);

                        //收鞘姿慢游：刀尖微垂轻晃；错帧偶发单刃慢挽花（静里的一点活）
                        float flourishT = (t + i * 63) % 260;
                        if (flourishT < 40f) {
                            bladeRot[i] += MathF.Sin(flourishT / 40f * MathHelper.Pi) * 0.16f;
                        }
                        else {
                            float rest = MathHelper.PiOver2 * 0.8f + Sway(i, 1.4f, 0.14f);
                            bladeRot[i] = bladeRot[i].AngleLerp(rest, 0.08f);
                        }
                    }
                    break;
                }
                case StateRelay: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    for (int i = 0; i < bladeCount; i++) {
                        int p = RelayPhase(i, t);
                        if (p < 0) {
                            //不在轮里：退居质心环位候场
                            float phase = tGlobal * 0.56f + Seed + i * MathHelper.TwoPi / bladeCount;
                            bladeTarget[i] = Projectile.Center + new Vector2(MathF.Cos(phase) * 96f, MathF.Sin(phase) * 44f - 30f);
                            ChaseBlade(i, 0.06f, 0.84f);
                            bladeRot[i] = bladeRot[i].AngleLerp(MathHelper.PiOver2 * 0.8f + Sway(i, 1.4f, 0.12f), 0.08f);
                            continue;
                        }
                        Vector2 dir = dashAng[i].ToRotationVector2();
                        if (p < GatherLen) {
                            //收：拉到冲线后端蓄势，减速到近停，只留呼吸颤
                            float ease = p / (float)GatherLen;
                            Vector2 cock = dashFrom[i] - dir * (16f + 10f * ease);
                            cock += dir.RotatedBy(MathHelper.PiOver2) * MathF.Sin(Seed + i) * 8f;
                            bladeTarget[i] = cock;
                            ChaseBlade(i, 0.2f, MathHelper.Lerp(0.7f, 0.42f, ease));
                            //刃尖回指（拉满弓的反向），末段颤 0.02 rad
                            float cockRot = dashAng[i] + MathHelper.Pi * 0.86f * (i % 2 == 0 ? 1f : -1f);
                            bladeRot[i] = bladeRot[i].AngleLerp(cockRot, 0.28f);
                            if (ease > 0.6f) {
                                bladeRot[i] += MathF.Sin(t * 1.7f + i) * 0.02f;
                            }
                        }
                        else if (p == GatherLen || p == GatherLen + 1) {
                            //爆：两帧穿越全程，刃体让位给路径拖影（藏行程）
                            skipFix = true;
                            Vector2 snapPos = p == GatherLen ? (dashFrom[i] + dashTo[i]) * 0.5f : dashTo[i];
                            bladeVel[i] = snapPos - bladePos[i];
                            bladePos[i] = snapPos;
                            bladeTarget[i] = snapPos;
                            bladeRot[i] = dashAng[i];
                        }
                        else {
                            //停：过冲点硬停驻帧，几何冻住，静谷本身就是下一拍的蓄势
                            bladeVel[i] *= 0.6f;
                            bladePos[i] += bladeVel[i];
                            bladeTarget[i] = bladePos[i];
                            bladeRot[i] = dashAng[i];
                        }
                    }
                    break;
                }
                case StateCross: {
                    if (t <= CrossHoldEnd) {
                        for (int i = 0; i < bladeCount; i++) {
                            //冲环位后刃尖内指压住：静谷里所有刀口都对着猎物
                            Vector2 post = Projectile.Center + CrossAngle(i).ToRotationVector2() * CrossRadius;
                            bladeTarget[i] = post;
                            ChaseBlade(i, t <= CrossPostEnd ? 0.17f : 0.1f, t <= CrossPostEnd ? 0.76f : 0.6f);
                            float inward = (Projectile.Center - bladePos[i]).ToRotation();
                            bladeRot[i] = bladeRot[i].AngleLerp(inward, 0.3f);
                            //静谷末段的蓄势颤
                            if (t > CrossPostEnd + 8) {
                                bladeRot[i] += MathF.Sin(t * 1.9f + i * 2.1f) * 0.015f;
                            }
                        }
                    }
                    else if (t <= CrossHoldEnd + 2) {
                        //齐发穿心：两帧对穿到对侧
                        skipFix = true;
                        for (int i = 0; i < bladeCount; i++) {
                            Vector2 snapPos = t == CrossHoldEnd + 1 ? Projectile.Center : dashTo[i];
                            bladeVel[i] = snapPos - bladePos[i];
                            bladePos[i] = snapPos;
                            bladeTarget[i] = snapPos;
                            bladeRot[i] = dashAng[i];
                        }
                    }
                    else if (t <= CrossRestEnd) {
                        //驻帧：全员钉在对侧出口，几何冻住
                        for (int i = 0; i < bladeCount; i++) {
                            bladeVel[i] *= 0.55f;
                            bladePos[i] += bladeVel[i];
                            bladeTarget[i] = bladePos[i];
                            bladeRot[i] = dashAng[i];
                        }
                    }
                    else {
                        //收势归队
                        float tGlobal = Main.GlobalTimeWrappedHourly;
                        for (int i = 0; i < bladeCount; i++) {
                            float phase = tGlobal * 0.56f + Seed + i * MathHelper.TwoPi / bladeCount;
                            bladeTarget[i] = Projectile.Center + new Vector2(MathF.Cos(phase) * 96f, MathF.Sin(phase) * 44f - 30f);
                            ChaseBlade(i, 0.07f, 0.85f);
                            bladeRot[i] = bladeRot[i].AngleLerp(MathHelper.PiOver2 * 0.8f, 0.1f);
                        }
                    }
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    for (int i = 0; i < bladeCount; i++) {
                        int lt = t - i * DissolveStagger;
                        if (lt < 0) {
                            bladeVel[i] *= 0.95f;
                            bladeVel[i].Y += 0.05f;
                        }
                        else {
                            bladeVel[i].X *= 0.93f;
                            bladeVel[i].Y = MathF.Min(bladeVel[i].Y + 0.3f, 9.5f);
                            //刀尖垂下去，一柄柄沉
                            bladeRot[i] = bladeRot[i].AngleLerp(MathHelper.PiOver2, 0.03f);
                        }
                        bladePos[i] += bladeVel[i];
                        bladeTarget[i] = bladePos[i];
                    }
                    break;
                }
            }

            //硬纠：同步包把质心拽走半屏时按驻位重建，防弹簧甩鞭
            if (!skipFix) {
                for (int i = 0; i < bladeCount; i++) {
                    if (Vector2.Distance(bladePos[i], bladeTarget[i]) > 780f) {
                        bladePos[i] = bladeTarget[i];
                        bladeVel[i] = Vector2.Zero;
                    }
                }
            }
        }

        private void PushBladeHistory() {
            for (int i = 0; i < bladeCount; i++) {
                Vector2[] arr = bladeOld[i];
                float[] rots = bladeOldRot[i];
                if (arr == null) {
                    continue;
                }
                for (int k = arr.Length - 1; k >= 1; k--) {
                    arr[k] = arr[k - 1];
                    rots[k] = rots[k - 1];
                }
                arr[0] = bladePos[i];
                rots[0] = bladeRot[i];
            }
        }

        /// <summary>常驻氛围：液态下缘偶发凝珠滴落，刃一直在往下滴湖水</summary>
        private void UpdateAmbient() {
            if (Main.dedServ || State is not (StateFollow or StateRelay or StateCross)) {
                return;
            }
            if (Main.rand.NextBool(16)) {
                int i = Main.rand.Next(bladeCount);
                if (BladeAlpha(i) > 0.5f) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        bladePos[i] + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(6f, 14f)),
                        new Vector2(0f, Main.rand.NextFloat(0.5f, 1.1f)),
                        BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                        Main.rand.NextFloat(0.28f, 0.5f))?.Configure(Main.rand.Next(16, 28), 0f);
                }
            }
        }

        //==================== 公共小件 ====================

        private int FindTarget(Player owner) {
            if (owner.HasMinionAttackTargetNPC) {
                NPC picked = Main.npc[owner.MinionAttackTargetNPC];
                if (picked.CanBeChasedBy(Projectile)
                    && Vector2.Distance(picked.Center, owner.Center) < 1400f) {
                    return picked.whoAmI;
                }
            }
            int best = -1;
            float bestDist = 950f;
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

        private float BladeAlpha(int i) {
            int t = (int)StateTimer;
            float alpha = State switch {
                StateEmerge => t < BreachTime(i) ? 0f : MathHelper.Clamp((t - BreachTime(i)) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
            //爆发两帧刃体让位给拖影（藏行程）：本体压暗
            if (IsBursting(i)) {
                alpha *= 0.25f;
            }
            return alpha;
        }

        /// <summary>该刃正处在两帧穿越里（突斩或穿心）</summary>
        private bool IsBursting(int i) {
            int t = (int)StateTimer;
            if (State == StateRelay) {
                int p = RelayPhase(i, t);
                return p == GatherLen || p == GatherLen + 1;
            }
            if (State == StateCross) {
                return t == CrossHoldEnd + 1 || t == CrossHoldEnd + 2;
            }
            return false;
        }

        /// <summary>uForm 水线呼吸：同枪奴，实体上半 + 液态下缘，出水凝出、溶解漫上来</summary>
        private float BladeForm(int i) {
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

        /// <summary>uDissolve：溶解期逐刃错帧蚀散，落水的先散</summary>
        private float DissolveAmt(int i) {
            if (State != StateDissolve) {
                return 0f;
            }
            float p = MathF.Pow(MathHelper.Clamp((StateTimer - i * DissolveStagger) / 46f, 0f, 1f), 0.9f);
            return MathHelper.Clamp(p + (dissolveSplashed[i] ? 0.15f : 0f), 0f, 1f);
        }

        private float BladeScale(int i) {
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

        /// <summary>剑贴图斜置画法（柄左下尖右上）：刃尖指向角补 π/4 修正；刃群不做镜像</summary>
        private float BladeDrawRot(int i) => bladeRot[i] + MathHelper.PiOver4;

        public override bool PreDraw(ref Color lightColor) {
            if (!bladesInit) {
                return false;
            }
            Main.instance.LoadItem(armsItemType);
            Texture2D tex = TextureAssets.Item[armsItemType]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            //穿越拖影：爆发两帧的路径由残影承载（藏行程的另一半）
            DrawDashTrails(sb, tex);

            //刃群本体：血湖材质
            DrawBodies(sb, tex);

            //加色层：预兆水光 / 蓄势刃口冷光 / 心点闪拍
            DrawGlow(sb);

            return false;
        }

        private void DrawDashTrails(SpriteBatch sb, Texture2D tex) {
            Vector2 origin = tex.Size() * 0.5f;
            for (int i = 0; i < bladeCount; i++) {
                float trailA = MathHelper.Clamp((bladeVel[i].Length() - 8f) / 12f, 0f, 1f);
                if (State == StateEmerge || State == StateDissolve) {
                    trailA *= BladeAlpha(i);
                }
                if (trailA <= 0.03f) {
                    continue;
                }
                Vector2[] arr = bladeOld[i];
                float[] rots = bladeOldRot[i];
                for (int k = arr.Length - 1; k >= 1; k--) {
                    float fall = 1f - k / (float)arr.Length;
                    sb.Draw(tex, arr[k] - Main.screenPosition, null,
                        BloodMain * (0.3f * fall * trailA), rots[k] + MathHelper.PiOver4,
                        origin, BladeScale(i) * (0.97f - k * 0.015f), SpriteEffects.None, 0f);
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
            for (int i = 0; i < bladeCount; i++) {
                float alpha = BladeAlpha(i);
                if (alpha <= 0.01f) {
                    continue;
                }
                float rot = BladeDrawRot(i);
                Vector2 drawPos = bladePos[i] - Main.screenPosition;
                float dissolve = DissolveAmt(i);

                //液态水鞘包衣：同一剪影放大一号、全血水态、独立慢晃
                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed + i * 1.3f;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.6f, MathF.Cos(wt * 0.83f) * 2f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.03f;
                    float envScale = BladeScale(i) * (1.13f + MathF.Sin(wt * 1.6f) * 0.025f);
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
                    form.Parameters["uForm"]?.SetValue(BladeForm(i));
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    //无着色器回退：CPU 血染
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }

                sb.Draw(tex, drawPos, null, color,
                    rot, origin, BladeScale(i), SpriteEffects.None, 0f);
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
                for (int i = 0; i < bladeCount; i++) {
                    Vector2 pos = new(BreachX(i), domain.LakeWorldY + MathHelper.Lerp(42f, 8f, ease));
                    float r = 18f + 13f * ease;
                    sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * (0.35f * ease), 0f,
                        gOrigin, new Vector2(r * 2.2f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //蓄势刃口冷光：收拍末段沿刃一道渐亮的窄光，要出刀的宣告
            if (State is StateRelay or StateCross) {
                for (int i = 0; i < bladeCount; i++) {
                    float charge = GatherCharge(i);
                    if (charge <= 0.05f) {
                        continue;
                    }
                    EnsureBegin();
                    Vector2 dir = bladeRot[i].ToRotationVector2();
                    float len = Profile.BladeLen * 0.5f;
                    Vector2 pos = bladePos[i] + dir * len * 0.3f;
                    sb.Draw(glow, pos - Main.screenPosition, null,
                        MuzzleHot * (0.4f * charge), bladeRot[i],
                        gOrigin, new Vector2(len * 1.7f / glow.Width, 3.4f / glow.Height), SpriteEffects.None, 0f);
                    //刃根积光
                    sb.Draw(glow, bladePos[i] - Main.screenPosition, null,
                        BloodBright * (0.22f * charge), 0f,
                        gOrigin, new Vector2(16f * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //心点闪拍：穿心交点的余光
            if (crossFlashTick > 0) {
                EnsureBegin();
                float a = crossFlashTick / 8f;
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null, BloodBright * (0.55f * a), 0f,
                    gOrigin, new Vector2(58f * a * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        /// <summary>蓄势进度 0~1：突斩收拍末段与穿心静谷末段亮刃口</summary>
        private float GatherCharge(int i) {
            int t = (int)StateTimer;
            if (State == StateRelay) {
                int p = RelayPhase(i, t);
                if (p < 0 || p >= GatherLen) {
                    return 0f;
                }
                return MathHelper.Clamp((p - GatherLen * 0.4f) / (GatherLen * 0.6f), 0f, 1f);
            }
            if (State == StateCross && t > CrossPostEnd && t <= CrossHoldEnd) {
                return MathHelper.Clamp((t - CrossPostEnd) / (float)(CrossHoldEnd - CrossPostEnd), 0f, 1f);
            }
            return 0f;
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            //谢幕残珠：每柄刃留一口水
            if (Main.dedServ || !bladesInit) {
                return;
            }
            for (int i = 0; i < bladeCount; i++) {
                for (int k = 0; k < 4; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        bladePos[i] + Main.rand.NextVector2Circular(14f, 12f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.5f, 2.4f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(Main.rand.Next(12, 24), 0f);
                }
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.65f, Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(Main.rand.Next(45, 70));
        }
    }
}
