using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaZenith
{
    /// <summary>
    /// 械奴·天顶（专属条目，短路通用推断——天顶剑 noMelee=true 本也进不了刀奴档案）。
    /// 主刀是湖水凝成的天顶剑复制体（血湖材质+水鞘包衣），而天顶剑里住着泰拉旅途
    /// 全部名剑的记忆：攻击时湖水放出这些记忆，幻影剑沿原版 AI_182 的椭圆环线
    /// 绕目标环旋穿心（中点为椭圆心、X 半轴=半距、Y 半轴确定性抽、旋向交替），
    /// 过心帧在目标身上炸开带各剑档案色的记忆斩痕。
    /// 普攻=剑环轮舞（三柄幻影错拍环旋）；每第三轮天顶轮：六柄幻影绕主刀绽成剑环、
    /// 静谷蓄势后连环穿心，末拍主刀亲自闪步终结巨斩（天顶色月牙+星芒爆）。
    /// 沉一件即完整形态（万剑合一，数量无义），强度烘焙湖藏原件的 item.damage。
    /// 联机契约与鬼切械奴同构：owner 裁决转场盖 netUpdate 章、斩痕仅 authority 生成、
    /// 生命线只有 owner 判、节拍闩防快照回卷；幻影剑是确定性本地模拟（纯表现）
    /// </summary>
    internal class KikasaZenithServant : ModProjectile, IKikasaArmsServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>剑环轮舞单斩倍率（基伤=湖藏原件 item.damage）</summary>
        internal const float LoopDamageMul = 1.15f;

        /// <summary>天顶轮环斩单段倍率</summary>
        internal const float StormDamageMul = 0.9f;

        /// <summary>主刀终结巨斩倍率</summary>
        internal const float FinisherDamageMul = 3.2f;

        //==================== 烘焙数值（owner 在 Summon 里定值，ExtraAI 随包同步）====================

        /// <summary>湖藏原件的攻击力（含词缀），远端与服务器不读湖藏，只认这份烘焙</summary>
        private int baseDamage = 190;

        /// <summary>沉影盘在场判定用：专属械奴恒复制天顶剑</summary>
        public int ArmsItemType => ItemID.Zenith;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateLoop = 2;
        private const int StateStorm = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：保位与同族械奴同构，当前未用</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：单点预兆→立剑破水→翻转落定→出鞘鸣+诸剑觉醒环
        private const int OmenFrames = 26;
        private const int RiseEnd = 58;
        private const int SheatheFrame = 62;
        /// <summary>诸剑觉醒：出鞘鸣起六柄幻影绕主刀旋开一圈没入刀身（纯绘制层）</summary>
        private const int AwakenLen = 30;
        private const int EmergeTotal = 96;

        //剑环轮舞：引拍后三柄幻影错拍起飞，各沿椭圆环一整圈
        private const int LoopLead = 12;
        private const int LoopStagger = 9;
        private const int LoopLife = 46;
        private const int LoopBlades = 3;
        private const int LoopTotal = LoopLead + LoopStagger * (LoopBlades - 1) + LoopLife + 10;

        //天顶轮：六柄幻影绽环蓄势→错拍连环穿心→主刀终结闪步
        private const int StormRise = 14;
        private const int StormFanEnd = 26;
        private const int StormHoldEnd = 40;
        private const int StormStagger = 5;
        private const int StormLife = 42;
        private const int StormBlades = 6;
        private const int FinDeclareAt = 88;
        private const int FinGather = 14;
        private const int FinStrikeAt = FinDeclareAt + FinGather;
        private const int StormTotal = 132;
        /// <summary>剑环驻位半径</summary>
        private const float StormRingRadius = 118f;

        //溶解：失力坠湖
        private const int DissolveFrames = 70;

        //==================== 主刀本地模拟（各端自算，质心同步纠偏）====================

        private Vector2 bladePos;
        private Vector2 bladeVel;
        private Vector2 bladeTarget;
        /// <summary>刀尖指向角（剑贴图斜置画法补 π/4 修正）</summary>
        private float bladeRot;
        private float bladeSpin;
        //终结冲线：声明后蓄/爆/斩痕共用同一条线，先声明后砍
        private Vector2 dashFrom;
        private Vector2 dashTo;
        private float dashAng;
        /// <summary>终结斩痕锚点（猎物身上）</summary>
        private Vector2 slashMid;
        private readonly Vector2[] bladeOld = new Vector2[10];
        private readonly float[] bladeOldRot = new float[10];
        private bool bladeInit;

        //==================== 幻影剑（确定性本地模拟，伤害走 owner 生成的斩痕）====================

        private const int MaxPhantoms = 6;
        /// <summary>残影链长度</summary>
        private const int PhantomTrail = 10;

        private readonly bool[] phActive = new bool[MaxPhantoms];
        /// <summary>驻环候飞中（天顶轮的绽环阶段）</summary>
        private readonly bool[] phRing = new bool[MaxPhantoms];
        private readonly int[] phProfile = new int[MaxPhantoms];
        private readonly float[] phScale = new float[MaxPhantoms];
        /// <summary>状态时间轴上的起飞帧</summary>
        private readonly int[] phStart = new int[MaxPhantoms];
        private readonly int[] phLife = new int[MaxPhantoms];
        private readonly Vector2[] phAnchor = new Vector2[MaxPhantoms];
        private readonly Vector2[] phFocus = new Vector2[MaxPhantoms];
        /// <summary>椭圆纵半轴（带符号=旋向观感）</summary>
        private readonly float[] phYAxis = new float[MaxPhantoms];
        private readonly Vector2[] phPos = new Vector2[MaxPhantoms];
        private readonly float[] phRot = new float[MaxPhantoms];
        private readonly Vector2[][] phOld = new Vector2[MaxPhantoms][];
        private readonly float[][] phOldRot = new float[MaxPhantoms][];

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private bool breachDone;
        private bool sheatheSnapDone;
        private bool dissolveSplashed;
        /// <summary>逐柄声明闩（起飞即声明冲线）</summary>
        private readonly bool[] phDeclared = new bool[MaxPhantoms];
        /// <summary>逐柄过心出斩闩</summary>
        private readonly bool[] phSlashed = new bool[MaxPhantoms];
        /// <summary>终结声明/出刀闩</summary>
        private bool finDeclared;
        private bool finSlashed;
        /// <summary>天顶轮剑环点名闩（跳帧进窗也补）</summary>
        private bool stormRingSpawned;
        private int finFlashTick;
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.6173f;

        //==================== 召唤入口 ====================

        /// <summary>
        /// KikasaArmsIndex 专门条目的召唤入口；count 不折算编制——天顶剑已是万剑合一，
        /// 数量对它没有意义，沉一件即完整形态；强度取湖藏里攻击力最高的原件（含词缀）
        /// </summary>
        internal static void Summon(Player owner, Vector2 emergeAt, int count) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            //烘焙基伤：湖藏最强原件与模板兜底取大（湖藏数据本机私有，烘焙后随包同步）
            int baseDmg = 0;
            foreach (Item item in owner.GetModPlayer<KikasaVaultPlayer>().Stored) {
                if (item?.IsAir == false && item.type == ItemID.Zenith && item.damage > baseDmg) {
                    baseDmg = item.damage;
                }
            }
            if (ContentSamples.ItemsByType.TryGetValue(ItemID.Zenith, out Item sample)
                && sample?.IsAir == false) {
                baseDmg = Math.Max(baseDmg, sample.damage);
            }
            if (baseDmg <= 0) {
                baseDmg = 190;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDmg * LoopDamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaZenithServant>(), damage, 3f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaZenithServant blade) {
                blade.baseDamage = baseDmg;
                Main.projectile[index].netUpdate = true;
            }
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //椭圆环旋的幻影剑散布远超质心 hitbox，出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1400;
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

        /// <summary>刀体与幻影不做接触判定，伤害全在记忆斩痕上</summary>
        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(baseDamage);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int dmg = reader.ReadInt32();
            if (dmg > 0) {
                baseDamage = dmg;
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
            //还没破水就要收场：直接收掉，免得溶解演出让刀凭空闪现再化水
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
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * LoopDamageMul);

            //换场清闩：远端可能靠收包换场而非本地同拍转场，残闩会吞掉新场节拍；
            //残余幻影跟着退场（进溶解=碎成星屑，其余=悄然化水）
            if (State != lastSeenState) {
                lastSeenState = State;
                Array.Fill(phDeclared, false);
                Array.Fill(phSlashed, false);
                finDeclared = false;
                finSlashed = false;
                stormRingSpawned = false;
                RetirePhantoms(shatter: State == StateDissolve);
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                }
            }

            if (!bladeInit) {
                RebuildBlade(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateLoop: UpdateLoop(owner, authority); break;
                case StateStorm: UpdateStorm(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateBlade(owner, domain);
            PushBladeHistory();
            UpdatePhantoms(owner, authority);
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (finFlashTick > 0) {
                finFlashTick--;
            }

            float glow = BladeAlpha() * 0.4f;
            if (glow > 0.02f) {
                //主刀光色：天顶苍绿沉进血湖里
                Vector3 tint = Color.Lerp(KikasaZenithArsenal.ZenithColor, BloodMain, 0.42f).ToVector3();
                Lighting.AddLight(bladePos, tint * glow);
            }
            for (int i = 0; i < MaxPhantoms; i++) {
                if (phActive[i]) {
                    Lighting.AddLight(phPos[i], KikasaZenithArsenal.ColorOf(phProfile[i]).ToVector3() * 0.3f);
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：立剑破水、诸剑觉醒 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                //水下待命：一处水面起预兆，涟漪向破水点收拢
                Projectile.velocity = Vector2.Zero;
                if (viewed && t % 5 == 2) {
                    float converge = 1f - t / (float)OmenFrames;
                    float wobble = MathF.Sin(t * 0.5f) * converge * 24f;
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X + wobble, lakeY),
                        0.35f + (1f - converge) * 0.45f);
                }
                if (viewed && (t == 6 || t == 16)) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.42f,
                        Pitch = -0.55f + t * 0.012f,
                        MaxInstances = 3
                    }, new Vector2(Projectile.Center.X, lakeY));
                }
                return;
            }

            //破水：立剑尖朝上刺出水面
            if (!breachDone) {
                breachDone = true;
                bladeVel = new Vector2(0f, -12.6f);
                bladeSpin = 0.18f;
                Projectile.velocity = new Vector2(0f, -3f);
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.72f,
                    Pitch = -0.3f,
                    MaxInstances = 3
                }, bladePos);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            Projectile.velocity *= 0.96f;

            //身上的湖水成帘往下淌
            if (viewed && t < RiseEnd && t % 3 == 0) {
                Vector2 dropPos = bladePos + new Vector2(
                    Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(2f, 16f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.4f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(12, 24), 0f);
            }

            //出鞘鸣+诸剑觉醒：一顿、天顶自己的嗓音低低响一声，二十柄剑的记忆醒了
            if (!sheatheSnapDone && t >= SheatheFrame) {
                sheatheSnapDone = true;
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = -0.15f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item169 with { Volume = 0.36f, Pitch = -0.45f, MaxInstances = 2 }, Projectile.Center);
                bladeVel += new Vector2(0f, -1.2f);
                if (viewed) {
                    for (int k = 0; k < 4; k++) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            bladePos + Main.rand.NextVector2Circular(14f, 12f),
                            new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 1.7f)),
                            BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(10, 18), 0.25f);
                    }
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

        /// <summary>破水浪冠：单刀规格，与鬼切同一郑重度</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 1.4f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(20f, 0f), 0.55f);
            KikasaDomainDeco.SplashAt(hit, 8);

            for (int k = 0; k < 12; k++) {
                float angle = -MathHelper.Pi * (0.18f + 0.64f * k / 11f);
                float speed = Main.rand.NextFloat(2.6f, 6f);
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

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = 0.32f,
                Pitch = -0.72f,
                MaxInstances = 1
            }, hit);
            ShakeViewer(1.6f);
        }

        //==================== 跟随：丰碑鞘姿悬浮 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            //质心锚：悬在主人肩后上方，随呼吸轻沉浮
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 34f, -50f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f + Seed) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢就贴回来，别拖着刀横穿半张地图
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildBlade(owner.GetModPlayer<KikasaDomainPlayer>());
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.09f;
            const float maxSpeed = 17f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);

            //出手裁决：剑环轮舞为常、每第三轮天顶轮；转场规则各端一致，owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                StateTimer = 0;
                StateParam = 0;
                State = attackIndex % 3 == 0 ? StateStorm : StateLoop;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 剑环轮舞：幻影错拍环旋穿心 ====================

        private void UpdateLoop(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= LoopLead) {
                EndAttack(authority, 50);
                return;
            }

            //质心压到目标侧近位：环旋的观众席
            Vector2 focus = target >= 0 ? Main.npc[target].Center : Projectile.Center;
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 anchor = owner.Center + toT * 92f + new Vector2(0f, -30f);
            Vector2 desired = (anchor - Projectile.Center) * 0.1f;
            if (desired.Length() > 15f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 15f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.16f);

            //逐柄错拍起飞（跳帧进窗也补声明）：从主刀位置射出，沿椭圆环线绕猎物一整圈
            for (int i = 0; i < LoopBlades; i++) {
                int launch = LoopLead + i * LoopStagger;
                if (t >= launch && !phDeclared[i]) {
                    phDeclared[i] = true;
                    LaunchPhantom(i, launch, LoopLife, target, owner, fromRing: false);
                }
            }

            if (t >= LoopTotal) {
                EndAttack(authority, 95);
            }
        }

        //==================== 天顶轮：绽环蓄势、连环穿心、主刀终结 ====================

        /// <summary>剑环驻位角：绕主刀慢旋</summary>
        private float RingAngle(int i, int t)
            => Seed * 1.9f + i * MathHelper.TwoPi / StormBlades + t * 0.015f;

        /// <summary>剑环驻位半径包络：绽开-呼吸</summary>
        private static float RingRadius(int t) {
            float fan = MathHelper.Clamp((t - 2) / (float)(StormFanEnd - 2), 0f, 1f);
            float ease = 1f - (1f - fan) * (1f - fan);
            return StormRingRadius * ease + MathF.Sin(t * 0.17f) * 3f * fan;
        }

        private void UpdateStorm(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= StormRise) {
                EndAttack(authority, 60);
                return;
            }

            //起手：主刀升位举过头顶，诸剑记忆被点名（窗口+闩，远端跳帧进场也能补上剑环）
            if (t == 1) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = -0.3f, MaxInstances = 2 }, bladePos);
            }
            if (!stormRingSpawned && t >= 2 && t < StormHoldEnd) {
                stormRingSpawned = true;
                for (int i = 0; i < StormBlades; i++) {
                    ActivateRingPhantom(i, StormHoldEnd + i * StormStagger, StormLife);
                }
            }

            //质心稳在主人头上：剑环的台座
            Vector2 anchor = owner.Center + new Vector2(0f, -66f);
            Vector2 want = (anchor - Projectile.Center) * 0.1f;
            if (want.Length() > 16f) {
                want = want.SafeNormalize(Vector2.Zero) * 16f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.18f);

            //静谷上膛：两声轻响后全场压住不动，静得越彻底，剑环炸开越有力
            if (t == StormHoldEnd - 12 || t == StormHoldEnd - 4) {
                SoundEngine.PlaySound(SoundID.Unlock with {
                    Volume = 0.4f,
                    Pitch = -0.4f + (StormHoldEnd - t) * 0.015f,
                    MaxInstances = 2
                }, Projectile.Center);
            }

            //逐柄错拍离环起飞（跳帧进窗补声明）
            for (int i = 0; i < StormBlades; i++) {
                int launch = StormHoldEnd + i * StormStagger;
                if (t >= launch && !phDeclared[i]) {
                    phDeclared[i] = true;
                    LaunchPhantom(i, launch, StormLife, target, owner, fromRing: true);
                }
            }

            //终结：诸剑将尽时主刀亲自出手——声明冲线、深蓄、两帧闪步穿越、巨斩
            if (t >= FinDeclareAt && !finDeclared) {
                finDeclared = true;
                DeclareFinisher(owner, target);
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.42f, Pitch = -0.35f, MaxInstances = 2 }, bladePos);
            }
            if (t >= FinStrikeAt && !finSlashed) {
                finSlashed = true;
                FinisherStrike(owner, authority);
            }

            if (t >= StormTotal) {
                EndAttack(authority, 175);
            }
        }

        /// <summary>终结冲线声明：主刀从当前位穿过主猎物的巨斩线</summary>
        private void DeclareFinisher(Player owner, int target) {
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * (FinGather + 2)
                : Projectile.Center + new Vector2(owner.direction * 320f, 0f);
            Vector2 dir = (focus - bladePos).SafeNormalize(Vector2.UnitX);
            float reach = KikasaZenithArsenal.ZenithBladeLen * 1.7f + 110f;
            dashFrom = bladePos;
            dashTo = focus + dir * reach * 0.65f;
            dashAng = dir.ToRotation();
            slashMid = focus;
        }

        /// <summary>
        /// 主刀终结：两帧闪步穿越（藏行程），owner 端在猎物身上生成天顶色终结巨斩
        /// （生成包自含冲线角与剑谱索引 -1=本体色）
        /// </summary>
        private void FinisherStrike(Player owner, bool authority) {
            Vector2 dir = dashAng.ToRotationVector2();
            finFlashTick = 10;

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.42f, Pitch = -0.15f, MaxInstances = 2 }, slashMid);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.38f, Pitch = 0.05f, MaxInstances = 2 }, slashMid);
            if (ViewedOwner) {
                ShakeViewer(3.4f);
            }
            if (!Main.dedServ) {
                PRTLoader.NewParticle<PRT_DWave>(slashMid, Vector2.Zero,
                    Color.Lerp(KikasaZenithArsenal.ZenithColor, BloodBright, 0.35f), 0.1f)
                    ?.Configure(new Vector2(0.6f, 1f), Seed, 0.3f, 9);
                for (int k = 0; k < 10; k++) {
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(
                        slashMid + Main.rand.NextVector2Circular(16f, 16f),
                        dir.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(3f, 7f),
                        Main.rand.NextBool(3) ? Color.White : KikasaZenithArsenal.ZenithColor,
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 28), affectedByGravity: true);
                }
            }

            //终结巨斩只在 owner 端生成，spawn 包自带冲线（ai0=判定半长，ai1=终结，ai2=-1 本体色）
            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                    .ApplyTo(baseDamage * FinisherDamageMul);
                float halfLen = KikasaZenithArsenal.ZenithBladeLen * 1.6f + 60f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), slashMid, dir * 3.4f,
                    ModContent.ProjectileType<KikasaZenithSlash>(), damage, 3f, Projectile.owner,
                    halfLen, 1f, -1f);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解：失力坠湖 ====================

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

            if (lakeAlive && !dissolveSplashed && bladePos.Y >= lakeY) {
                dissolveSplashed = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.55f,
                    Pitch = -0.35f,
                    MaxInstances = 3
                }, bladePos);
                if (ViewedOwner) {
                    Vector2 hit = new(bladePos.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 6);
                    KikasaDomainDeco.RippleAt(hit, 0.9f);
                    ShakeViewer(1f);
                }
            }

            //边沉边化成水珠
            if (!Main.dedServ && BladeAlpha() > 0.15f && t % 2 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    bladePos + Main.rand.NextVector2Circular(14f, 14f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.8f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 22), 0f);
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
            }
        }

        //==================== 幻影剑：声明、椭圆采样、推进 ====================

        /// <summary>确定性纵半轴：46~120，旋向按剑序交替（各端一致，不掷 Main.rand）</summary>
        private float PickYAxis(int ordinal) {
            float mag = 46f + MathF.Abs(MathF.Sin(Seed * 3.7f + attackIndex * 1.31f + ordinal * 2.17f)) * 74f;
            return (ordinal % 2 == 0 ? 1f : -1f) * mag;
        }

        /// <summary>
        /// 幻影起飞声明：锚点=主刀当前位置（离环起飞则用剑环驻位，不吸回主刀），
        /// 锁定目标提前量（过心=半圈处），抽一柄记忆剑，主刀轻退一步作出手反坐
        /// </summary>
        private void LaunchPhantom(int i, int launchTick, int life, int target, Player owner, bool fromRing) {
            Vector2 anchor = fromRing && phActive[i] ? phPos[i] : bladePos;
            phActive[i] = true;
            phRing[i] = false;
            phStart[i] = launchTick;
            phLife[i] = life;
            phProfile[i] = KikasaZenithArsenal.Pick(Seed, attackIndex, i);
            phScale[i] = KikasaZenithArsenal.DrawScaleOf(phProfile[i]);
            phYAxis[i] = PickYAxis(i);
            phAnchor[i] = anchor;
            phFocus[i] = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * (life * 0.5f)
                : bladePos + new Vector2(owner.direction * 340f, -20f);
            phPos[i] = anchor;
            phRot[i] = (phFocus[i] - anchor).ToRotation();
            SeedPhantomHistory(i);
            phSlashed[i] = false;

            //出手反坐：主刀被记忆借力，轻轻一沉
            Vector2 aim = (phFocus[i] - bladePos).SafeNormalize(Vector2.UnitX);
            bladeVel -= aim * 1.3f;

            //天顶自己的嗓音：每柄剑离手都响一声，音高随剑序爬
            SoundEngine.PlaySound(SoundID.Item169 with {
                Volume = 0.34f,
                Pitch = -0.08f + i * 0.06f,
                MaxInstances = 4
            }, anchor);
        }

        /// <summary>天顶轮点名：先入环候飞，起飞帧再声明冲线</summary>
        private void ActivateRingPhantom(int i, int launchTick, int life) {
            phActive[i] = true;
            phRing[i] = true;
            phStart[i] = launchTick;
            phLife[i] = life;
            phProfile[i] = KikasaZenithArsenal.Pick(Seed, attackIndex, i);
            phScale[i] = KikasaZenithArsenal.DrawScaleOf(phProfile[i]);
            phYAxis[i] = PickYAxis(i);
            phPos[i] = bladePos;
            phRot[i] = RingAngle(i, 2);
            SeedPhantomHistory(i);
            phSlashed[i] = false;
        }

        private void SeedPhantomHistory(int i) {
            phOld[i] ??= new Vector2[PhantomTrail];
            phOldRot[i] ??= new float[PhantomTrail];
            for (int k = 0; k < PhantomTrail; k++) {
                phOld[i][k] = phPos[i];
                phOldRot[i][k] = phRot[i];
            }
        }

        /// <summary>
        /// 椭圆环线采样（照抄原版 AI_182 的参数化）：中点为椭圆心、X 半轴=半距
        /// （后半程涨 40、下限 60——近身时起点后撤成蓄势过冲，原版同款）、
        /// Y 半轴带符号定旋向观感；u=0 在锚点、u=0.5 恰过猎物、u=1 回到锚点近旁
        /// </summary>
        private static Vector2 SampleLoopPath(Vector2 anchor, Vector2 focus, float yAxis, float u) {
            Vector2 offset = focus - anchor;
            float aim = offset.ToRotation();
            float spin = offset.X >= 0f ? 1f : -1f;
            float xAxis = MathF.Max(offset.Length() * 0.5f
                + Utils.GetLerpValue(0.5f, 1f, u, clamped: true) * 40f, 60f);
            float sweep = MathHelper.Pi + spin * u * MathHelper.TwoPi;
            Vector2 local = new(MathF.Cos(sweep) * xAxis, MathF.Sin(sweep) * yAxis);
            return anchor + offset * 0.5f + local.RotatedBy(aim);
        }

        private void UpdatePhantoms(Player owner, bool authority) {
            int t = (int)StateTimer;
            //环内指向只查一次猎物（六柄共用）
            Vector2 ringAim = Vector2.Zero;
            bool ringAimSet = false;
            for (int i = 0; i < MaxPhantoms; i++) {
                if (!phActive[i]) {
                    continue;
                }

                //剑环候飞：贴向驻位，剑尖先外指、静谷末段转指猎物（杀意的宣告）
                if (phRing[i]) {
                    Vector2 post = bladePos + RingAngle(i, t).ToRotationVector2() * RingRadius(t);
                    phPos[i] = Vector2.Lerp(phPos[i], post, 0.24f);
                    float wantRot;
                    if (t < StormHoldEnd - 10) {
                        wantRot = RingAngle(i, t);
                    }
                    else {
                        if (!ringAimSet) {
                            ringAimSet = true;
                            ringAim = FindTargetPos(owner);
                        }
                        wantRot = (ringAim - phPos[i]).ToRotation();
                    }
                    phRot[i] = phRot[i].AngleLerp(wantRot, 0.2f);
                    //静谷末段的蓄势颤
                    if (t > StormHoldEnd - 10) {
                        phRot[i] += MathF.Sin(t * 1.9f + i * 2.1f) * 0.016f;
                    }
                    PushPhantomHistory(i);
                    continue;
                }

                int tp = t - phStart[i];
                if (tp < 0) {
                    PushPhantomHistory(i);
                    continue;
                }
                float u = tp / (float)phLife[i];
                if (u >= 1f) {
                    RetirePhantom(i, shatter: false);
                    continue;
                }

                Vector2 prev = phPos[i];
                phPos[i] = SampleLoopPath(phAnchor[i], phFocus[i], phYAxis[i], u);
                Vector2 delta = phPos[i] - prev;
                if (delta.LengthSquared() > 0.01f) {
                    //刀尖顺着行进方向：椭圆离心的急弯全由位置差给出
                    phRot[i] = delta.ToRotation();
                }
                PushPhantomHistory(i);

                //过心帧：斩痕在猎物身上炸开（节拍闩防快照回卷，跳帧迟到也补砍）
                if (u >= 0.5f && !phSlashed[i]) {
                    phSlashed[i] = true;
                    PassStrike(i, owner, authority);
                }

                //飞行余屑：剑色星屑偶发（纯表现）
                if (!Main.dedServ && Main.rand.NextBool(6)) {
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(
                        phPos[i] + Main.rand.NextVector2Circular(8f, 8f),
                        delta * 0.1f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                        KikasaZenithArsenal.ColorOf(phProfile[i]),
                        Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(10, 18), affectedByGravity: false);
                }
            }
        }

        /// <summary>
        /// 幻影过心：主音落在穿过猎物的一瞬，owner 端沿行进向生成记忆斩痕
        /// （生成包自含判定半长与剑谱索引）
        /// </summary>
        private void PassStrike(int i, Player owner, bool authority) {
            Vector2 pass = SampleLoopPath(phAnchor[i], phFocus[i], phYAxis[i], 0.5f);
            Vector2 dir = (phFocus[i] - phAnchor[i]).SafeNormalize(Vector2.UnitX);

            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.4f,
                Pitch = 0.15f + i * 0.03f,
                MaxInstances = 4
            }, pass);
            if (ViewedOwner) {
                ShakeViewer(State == StateStorm ? 1.5f : 1.1f);
            }

            if (authority) {
                float mul = State == StateStorm ? StormDamageMul : LoopDamageMul;
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * mul);
                float halfLen = KikasaZenithArsenal.BladeLenOf(phProfile[i]) * 0.85f + 34f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), pass, dir * 3.2f,
                    ModContent.ProjectileType<KikasaZenithSlash>(), damage, 3f, Projectile.owner,
                    halfLen, 0f, phProfile[i]);
            }
        }

        /// <summary>单柄退场：环走完回到主刀近旁，散成几粒剑色星屑与水珠（记忆归鞘）</summary>
        private void RetirePhantom(int i, bool shatter) {
            phActive[i] = false;
            phRing[i] = false;
            if (Main.dedServ) {
                return;
            }
            Color color = KikasaZenithArsenal.ColorOf(phProfile[i]);
            int sparks = shatter ? 6 : 3;
            for (int k = 0; k < sparks; k++) {
                PRTLoader.NewParticle<PRT_CrimsonSpark>(
                    phPos[i] + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(2.2f, 2.2f),
                    color, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(12, 20), affectedByGravity: shatter);
            }
            for (int k = 0; k < 2; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    phPos[i] + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.8f, 1.8f)),
                    BloodMain * 0.45f, Main.rand.NextFloat(0.28f, 0.45f))?.Configure(Main.rand.Next(12, 20), 0f);
            }
            if (shatter) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.28f, Pitch = 0.1f, MaxInstances = 3 }, phPos[i]);
            }
        }

        /// <summary>清场：换场或溶解时残余幻影统一退场（溶解=碎散，其余=悄然化水）</summary>
        private void RetirePhantoms(bool shatter) {
            for (int i = 0; i < MaxPhantoms; i++) {
                if (phActive[i]) {
                    RetirePhantom(i, shatter);
                }
            }
        }

        private void PushPhantomHistory(int i) {
            Vector2[] arr = phOld[i];
            float[] rots = phOldRot[i];
            if (arr == null) {
                return;
            }
            for (int k = arr.Length - 1; k >= 1; k--) {
                arr[k] = arr[k - 1];
                rots[k] = rots[k - 1];
            }
            arr[0] = phPos[i];
            rots[0] = phRot[i];
        }

        //==================== 主刀推进 ====================

        /// <summary>初始化或硬纠时按当前状态直接落位，防刀体与残影抽搐</summary>
        private void RebuildBlade(KikasaDomainPlayer domain) {
            bladeInit = true;
            if (State == StateEmerge) {
                bladePos = new Vector2(Projectile.Center.X, domain.LakeWorldY + 28f);
                bladeRot = -MathHelper.PiOver2;
            }
            else {
                bladePos = Projectile.Center + new Vector2(0f, -8f);
                bladeRot = MonumentRot();
            }
            bladeVel = Vector2.Zero;
            bladeSpin = 0f;
            bladeTarget = bladePos;
            dashFrom = bladePos;
            dashTo = bladePos;
            dashAng = bladeRot;
            slashMid = bladePos;
            for (int k = 0; k < bladeOld.Length; k++) {
                bladeOld[k] = bladePos;
                bladeOldRot[k] = bladeRot;
            }
        }

        private void ChaseBlade(float accel, float damp) {
            bladeVel = (bladeVel + (bladeTarget - bladePos) * accel) * damp;
            bladePos += bladeVel;
        }

        /// <summary>丰碑鞘姿基准角：剑尖朝天直立（圣剑不垂头），随呼吸轻晃</summary>
        private float MonumentRot()
            => -MathHelper.PiOver2 + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.3f + Seed) * 0.07f;

        private void UpdateBlade(Player owner, KikasaDomainPlayer domain) {
            if (!bladeInit) {
                return;
            }
            int t = (int)StateTimer;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    if (t < OmenFrames) {
                        //水下待命：钉在破水点，剑尖朝上
                        bladePos = new Vector2(Projectile.Center.X, lakeY + 28f);
                        bladeVel = Vector2.Zero;
                        bladeTarget = bladePos;
                        bladeRot = -MathHelper.PiOver2;
                        break;
                    }
                    //破水后：先弹道升 + 轻翻，14 帧后弹簧接管贴向悬位
                    bladeTarget = new Vector2(Projectile.Center.X, lakeY - 98f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + Seed) * 8f);
                    int lt = t - OmenFrames;
                    if (lt < 14) {
                        bladeVel.Y *= 0.955f;
                        bladeVel.X *= 0.98f;
                        bladePos += bladeVel;
                        bladeRot += bladeSpin;
                        bladeSpin *= 0.94f;
                    }
                    else {
                        ChaseBlade(0.05f, 0.86f);
                        bladeRot += bladeSpin;
                        bladeSpin *= 0.9f;
                        if (MathF.Abs(bladeSpin) < 0.05f) {
                            //翻转散尽后落定：剑尖朝天的丰碑姿
                            bladeRot = bladeRot.AngleLerp(MonumentRot(), 0.12f);
                        }
                    }
                    break;
                }
                case StateFollow: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    float phase = tGlobal * 0.7f + Seed;
                    Vector2 slot = Projectile.Center + new Vector2(MathF.Cos(phase) * 22f, MathF.Sin(phase * 1.3f) * 9f - 8f);
                    bladeTarget = slot;
                    ChaseBlade(0.06f, 0.84f);

                    //丰碑姿慢游：错帧偶发一次挽花（静里的一点活）
                    float flourishT = t % 320;
                    if (flourishT < 34f) {
                        bladeRot += MathF.Sin(flourishT / 34f * MathHelper.Pi) * 0.12f;
                    }
                    else {
                        bladeRot = bladeRot.AngleLerp(MonumentRot(), 0.08f);
                    }
                    break;
                }
                case StateLoop: {
                    //主刀持仪：刀尖遥指猎物方向压阵，每柄幻影离手时的反坐由 LaunchPhantom 给
                    Vector2 focus = FindTargetPos(owner);
                    bladeTarget = Projectile.Center + new Vector2(0f, -12f);
                    ChaseBlade(0.09f, 0.8f);
                    float aimRot = (focus - bladePos).ToRotation();
                    bladeRot = bladeRot.AngleLerp(aimRot, 0.14f);
                    break;
                }
                case StateStorm: {
                    if (t <= StormHoldEnd || !finDeclared) {
                        //举刀蓄势：升过质心上方剑尖朝天，静谷末段微颤
                        bladeTarget = Projectile.Center + new Vector2(0f, -40f);
                        ChaseBlade(0.12f, 0.78f);
                        bladeRot = bladeRot.AngleLerp(-MathHelper.PiOver2, 0.16f);
                        if (t > StormHoldEnd - 14 && t <= StormHoldEnd) {
                            bladeRot += MathF.Sin(t * 1.9f) * 0.018f;
                        }
                        break;
                    }
                    //终结段：深蓄-两帧闪步-停驻亮相
                    {
                        int p = t - FinDeclareAt;
                        Vector2 dir = dashAng.ToRotationVector2();
                        if (p < FinGather) {
                            //长蓄：刀身压平贴向冲线后端
                            float ease = MathHelper.Clamp(p / (float)FinGather, 0f, 1f);
                            bladeTarget = dashFrom - dir * (24f + 14f * ease);
                            ChaseBlade(0.2f, MathHelper.Lerp(0.66f, 0.4f, ease));
                            float cockRot = dashAng + MathHelper.Pi * 0.9f;
                            bladeRot = bladeRot.AngleLerp(cockRot, 0.26f);
                            if (ease > 0.55f) {
                                bladeRot += MathF.Sin(t * 2.1f) * 0.024f;
                            }
                        }
                        else if (p == FinGather || p == FinGather + 1) {
                            //闪步穿越：两帧瞬移全程，刀体让位给拖影（藏行程）
                            skipFix = true;
                            Vector2 snapPos = p == FinGather ? Vector2.Lerp(dashFrom, dashTo, 0.6f) : dashTo;
                            bladeVel = snapPos - bladePos;
                            bladePos = snapPos;
                            bladeTarget = snapPos;
                            bladeRot = dashAng;
                        }
                        else {
                            //终结停驻：几何冻住的亮相
                            bladeVel *= 0.5f;
                            bladePos += bladeVel;
                            bladeTarget = bladePos;
                            bladeRot = dashAng;
                        }
                    }
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    bladeVel.X *= 0.93f;
                    bladeVel.Y = MathF.Min(bladeVel.Y + 0.3f, 9.5f);
                    //剑尖垂下去，沉
                    bladeRot = bladeRot.AngleLerp(MathHelper.PiOver2, 0.03f);
                    bladePos += bladeVel;
                    bladeTarget = bladePos;
                    break;
                }
            }

            //硬纠：同步包把质心拽走半屏时按驻位重建，防弹簧甩鞭
            if (!skipFix && Vector2.Distance(bladePos, bladeTarget) > 780f) {
                bladePos = bladeTarget;
                bladeVel = Vector2.Zero;
            }
        }

        private void PushBladeHistory() {
            for (int k = bladeOld.Length - 1; k >= 1; k--) {
                bladeOld[k] = bladeOld[k - 1];
                bladeOldRot[k] = bladeOldRot[k - 1];
            }
            bladeOld[0] = bladePos;
            bladeOldRot[0] = bladeRot;
        }

        /// <summary>常驻氛围：液态下缘偶发凝珠滴落，刀一直在往下滴湖水</summary>
        private void UpdateAmbient() {
            if (Main.dedServ || State is not (StateFollow or StateLoop or StateStorm)) {
                return;
            }
            if (Main.rand.NextBool(18) && BladeAlpha() > 0.5f) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    bladePos + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(6f, 16f)),
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
                    && Vector2.Distance(picked.Center, owner.Center) < 1400f) {
                    return picked.whoAmI;
                }
            }
            int best = -1;
            float bestDist = 1000f;
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

        /// <summary>猎物落点（无猎物时取主人面向前方），幻影候飞的指向用</summary>
        private Vector2 FindTargetPos(Player owner) {
            int target = FindTarget(owner);
            return target >= 0 ? Main.npc[target].Center : owner.Center + new Vector2(owner.direction * 300f, 0f);
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        /// <summary>主刀正处在终结的两帧闪步里</summary>
        private bool IsBursting() {
            if (State != StateStorm) {
                return false;
            }
            int p = (int)StateTimer - FinDeclareAt;
            return p == FinGather || p == FinGather + 1;
        }

        private float BladeAlpha() {
            int t = (int)StateTimer;
            float alpha = State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
            //闪步两帧刀体让位给拖影（藏行程）：本体压暗
            if (IsBursting()) {
                alpha *= 0.2f;
            }
            return alpha;
        }

        /// <summary>uForm 水线呼吸：同族械奴，实体上半 + 液态下缘，出水凝出、溶解漫上来</summary>
        private float BladeForm() {
            int t = (int)StateTimer;
            float steady = 0.24f
                + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + Seed) * 0.06f;
            return State switch {
                StateEmerge => t < OmenFrames
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp(
                        (t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.6f, 0f, 1f),
                _ => steady,
            };
        }

        /// <summary>uDissolve：溶解期蚀散，落水后加速</summary>
        private float DissolveAmt() {
            if (State != StateDissolve) {
                return 0f;
            }
            float p = MathF.Pow(MathHelper.Clamp(StateTimer / 46f, 0f, 1f), 0.9f);
            return MathHelper.Clamp(p + (dissolveSplashed ? 0.15f : 0f), 0f, 1f);
        }

        /// <summary>主刀绘制缩放：贴图对角折算到略大于巨兵的口径（这是柄传说）</summary>
        private float BladeDrawScale(Texture2D tex) {
            float diag = MathF.Sqrt(tex.Width * tex.Width + tex.Height * tex.Height);
            float scale = Math.Clamp(100f / MathF.Max(diag, 30f), 0.65f, 1.5f);
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 10) {
                //破水过冲
                scale *= 1f + 0.08f * (1f - (t - OmenFrames) / 10f);
            }
            return scale;
        }

        /// <summary>蓄势进度 0~1：终结深蓄末段亮刃口</summary>
        private float GatherCharge() {
            if (State != StateStorm || !finDeclared) {
                return 0f;
            }
            int p = (int)StateTimer - FinDeclareAt;
            if (p < 0 || p >= FinGather) {
                return 0f;
            }
            return MathHelper.Clamp((p - FinGather * 0.3f) / (FinGather * 0.7f), 0f, 1f);
        }

        /// <summary>幻影透明度：起飞 3 帧渐入、末 8% 渐出；候飞随环绽开渐入</summary>
        private float PhantomAlpha(int i) {
            if (!phActive[i]) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (phRing[i]) {
                return MathHelper.Clamp((t - 2) / 10f, 0f, 1f) * 0.92f;
            }
            int tp = t - phStart[i];
            if (tp < 0) {
                return 0.92f;
            }
            float u = tp / (float)phLife[i];
            float fadeIn = MathHelper.Clamp(tp / 3f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((1f - u) / 0.08f, 0f, 1f);
            return fadeIn * fadeOut * 0.92f;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        /// <summary>剑贴图斜置画法（柄左下尖右上）：刃尖指向角补 π/4 修正，不做镜像</summary>
        private static float SwordDrawRot(float tipRot) => tipRot + MathHelper.PiOver4;

        public override bool PreDraw(ref Color lightColor) {
            if (!bladeInit) {
                return false;
            }
            Main.instance.LoadItem(ItemID.Zenith);
            Texture2D tex = TextureAssets.Item[ItemID.Zenith]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            //主刀穿越拖影：终结两帧的路径由残影承载（藏行程）
            DrawMasterTrail(sb, tex);

            //幻影剑群：残影链 + 本体（记忆残象不走水材质，剑色半透）
            DrawPhantoms(sb);

            //诸剑觉醒环：出水签名演出（纯绘制层）
            DrawAwakenRing(sb);

            //主刀本体：血湖材质
            DrawMasterBody(sb, tex);

            //加色层：预兆水光 / 幻影流光星芒 / 蓄势刃口 / 终结闪拍
            DrawGlow(sb);

            return false;
        }

        private void DrawMasterTrail(SpriteBatch sb, Texture2D tex) {
            float trailA = MathHelper.Clamp((bladeVel.Length() - 8f) / 12f, 0f, 1f);
            if (State == StateEmerge || State == StateDissolve) {
                trailA *= BladeAlpha();
            }
            if (trailA <= 0.03f) {
                return;
            }
            Vector2 origin = tex.Size() * 0.5f;
            float scale = BladeDrawScale(tex);
            for (int k = bladeOld.Length - 1; k >= 1; k--) {
                float fall = 1f - k / (float)bladeOld.Length;
                Color trailColor = Color.Lerp(BloodMain, KikasaZenithArsenal.ZenithColor, 0.35f);
                sb.Draw(tex, bladeOld[k] - Main.screenPosition, null,
                    trailColor * (0.3f * fall * trailA), SwordDrawRot(bladeOldRot[k]),
                    origin, scale * (0.97f - k * 0.012f), SpriteEffects.None, 0f);
            }
        }

        private void DrawPhantoms(SpriteBatch sb) {
            for (int i = 0; i < MaxPhantoms; i++) {
                float alpha = PhantomAlpha(i);
                if (alpha <= 0.02f) {
                    continue;
                }
                KikasaZenithArsenal.SwordProfile profile = KikasaZenithArsenal.Swords[phProfile[i]];
                Main.instance.LoadItem(profile.ItemType);
                Texture2D tex = TextureAssets.Item[profile.ItemType]?.Value;
                if (tex == null) {
                    continue;
                }
                Vector2 origin = tex.Size() * 0.5f;

                //残影链：环旋的剑链观感（原版拖尾幻影的 sprite 版）
                Vector2[] arr = phOld[i];
                float[] rots = phOldRot[i];
                if (arr != null && !phRing[i]) {
                    for (int k = arr.Length - 1; k >= 1; k--) {
                        float fall = 1f - k / (float)arr.Length;
                        sb.Draw(tex, arr[k] - Main.screenPosition, null,
                            profile.TrailColor * (0.3f * fall * alpha), SwordDrawRot(rots[k]),
                            origin, phScale[i] * (0.96f - k * 0.02f), SpriteEffects.None, 0f);
                    }
                }

                //本体：白骨架上浮剑色、向血湖底色微拉（记忆残象，不是实体）
                Color body = Color.Lerp(Color.Lerp(Color.White, profile.TrailColor, 0.42f), BloodMain, 0.14f);
                sb.Draw(tex, phPos[i] - Main.screenPosition, null,
                    body * alpha, SwordDrawRot(phRot[i]),
                    origin, phScale[i], SpriteEffects.None, 0f);
            }
        }

        /// <summary>诸剑觉醒：出鞘鸣起六柄记忆剑绕主刀旋开一圈又没入刀身（宣告它是谁）</summary>
        private void DrawAwakenRing(SpriteBatch sb) {
            if (State != StateEmerge) {
                return;
            }
            int t = (int)StateTimer;
            if (t < SheatheFrame || t >= SheatheFrame + AwakenLen) {
                return;
            }
            float w = (t - SheatheFrame) / (float)AwakenLen;
            float envelope = MathF.Sin(w * MathHelper.Pi);
            float radius = envelope * 84f;
            float alpha = envelope * 0.8f;

            for (int i = 0; i < 6; i++) {
                int idx = KikasaZenithArsenal.Pick(Seed, 0, i);
                KikasaZenithArsenal.SwordProfile profile = KikasaZenithArsenal.Swords[idx];
                Main.instance.LoadItem(profile.ItemType);
                Texture2D tex = TextureAssets.Item[profile.ItemType]?.Value;
                if (tex == null) {
                    continue;
                }
                float ang = Seed + i * MathHelper.TwoPi / 6f + w * 2.6f;
                Vector2 pos = bladePos + ang.ToRotationVector2() * radius;
                //切向持剑：绕环的仪仗
                float tipRot = ang + MathHelper.PiOver2;
                Color body = Color.Lerp(Color.White, profile.TrailColor, 0.45f);
                sb.Draw(tex, pos - Main.screenPosition, null,
                    body * alpha, SwordDrawRot(tipRot),
                    tex.Size() * 0.5f, KikasaZenithArsenal.DrawScaleOf(idx) * 0.9f, SpriteEffects.None, 0f);
            }
        }

        private void DrawMasterBody(SpriteBatch sb, Texture2D tex) {
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

            float alpha = BladeAlpha();
            if (alpha > 0.01f) {
                Vector2 origin = tex.Size() * 0.5f;
                float rot = SwordDrawRot(bladeRot);
                Vector2 drawPos = bladePos - Main.screenPosition;
                float dissolve = DissolveAmt();
                float scale = BladeDrawScale(tex);

                //液态水鞘包衣：同一剪影放大一号、全血水态、独立慢晃
                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.6f, MathF.Cos(wt * 0.83f) * 2f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.03f;
                    float envScale = scale * (1.12f + MathF.Sin(wt * 1.6f) * 0.025f);
                    form.Parameters["uSeed"]?.SetValue(Seed + 5.1f);
                    form.Parameters["uForm"]?.SetValue(1f);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(tex, drawPos + wobOff, null,
                        new Color(255, 255, 255, (byte)(alpha * 130f)),
                        rot + wobRot, origin, envScale, SpriteEffects.None, 0f);
                }

                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed);
                    form.Parameters["uForm"]?.SetValue(BladeForm());
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    //无着色器回退：CPU 血染
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }

                sb.Draw(tex, drawPos, null, color, rot, origin, scale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawGlow(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
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

            //预兆：水下血光上浮
            if (State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                EnsureBegin();
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(42f, 8f, ease));
                float r = 20f + 14f * ease;
                sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * (0.35f * ease), 0f,
                    gOrigin, new Vector2(r * 2.2f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
            }

            //诸剑觉醒星芒：出鞘鸣的刀尖一记白芯苍绿缘的四芒星
            if (State == StateEmerge && star != null && t >= SheatheFrame + 2 && t < SheatheFrame + 18) {
                float w = (t - SheatheFrame - 2) / 16f;
                float pulse = MathF.Sin(w * MathHelper.Pi);
                EnsureBegin();
                Vector2 tip = bladePos + bladeRot.ToRotationVector2() * 44f;
                Vector2 sOrigin = star.Size() * 0.5f;
                sb.Draw(star, tip - Main.screenPosition, null,
                    KikasaZenithArsenal.ZenithColor * (0.6f * pulse), w * 0.6f,
                    sOrigin, 0.3f * pulse, SpriteEffects.None, 0f);
                sb.Draw(star, tip - Main.screenPosition, null,
                    Color.White * (0.45f * pulse), w * 0.6f,
                    sOrigin, 0.17f * pulse, SpriteEffects.None, 0f);
            }

            //跟随期的静默星辉：偶发一记刀尖微光，提醒这柄剑还醒着
            if (State == StateFollow && star != null) {
                int cycle = (t + (int)(Seed * 97f)) % 170;
                if (cycle < 16) {
                    float pulse = MathF.Sin(cycle / 16f * MathHelper.Pi);
                    EnsureBegin();
                    Vector2 tip = bladePos + bladeRot.ToRotationVector2() * 42f;
                    sb.Draw(star, tip - Main.screenPosition, null,
                        KikasaZenithArsenal.ZenithColor * (0.34f * pulse), cycle * 0.05f,
                        star.Size() * 0.5f, 0.13f * pulse, SpriteEffects.None, 0f);
                }
            }

            //幻影流光：每柄剑一条速度拉伸的剑色光带（移动即拉伸）+ 起飞星闪
            for (int i = 0; i < MaxPhantoms; i++) {
                float alpha = PhantomAlpha(i);
                if (alpha <= 0.02f || phRing[i]) {
                    continue;
                }
                Vector2[] arr = phOld[i];
                if (arr == null) {
                    continue;
                }
                Vector2 vel = arr[0] - arr[Math.Min(2, arr.Length - 1)];
                float speed = vel.Length();
                if (speed > 3f) {
                    EnsureBegin();
                    Color streak = KikasaZenithArsenal.ColorOf(phProfile[i]);
                    float len = MathHelper.Clamp(speed * 2.6f, 20f, 130f);
                    Vector2 mid = phPos[i] - vel.SafeNormalize(Vector2.Zero) * len * 0.4f;
                    sb.Draw(glow, mid - Main.screenPosition, null,
                        streak * (0.42f * alpha), vel.ToRotation(),
                        gOrigin, new Vector2(len * 2f / glow.Width, 7f / glow.Height), SpriteEffects.None, 0f);
                }
                //起飞后 5 帧的离手星闪
                int tp = t - phStart[i];
                if (star != null && tp >= 0 && tp < 5) {
                    EnsureBegin();
                    float pulse = 1f - tp / 5f;
                    sb.Draw(star, phPos[i] - Main.screenPosition, null,
                        KikasaZenithArsenal.ColorOf(phProfile[i]) * (0.55f * pulse), tp * 0.2f,
                        star.Size() * 0.5f, 0.2f * pulse, SpriteEffects.None, 0f);
                }
            }

            //终结蓄势刃口：深蓄末段沿冲线一道渐亮的窄光，天顶要亲自出手了
            float charge = GatherCharge();
            if (charge > 0.05f) {
                EnsureBegin();
                Vector2 dir = dashAng.ToRotationVector2();
                float len = KikasaZenithArsenal.ZenithBladeLen * 0.8f;
                Vector2 pos = bladePos + dir * len * 0.3f;
                sb.Draw(glow, pos - Main.screenPosition, null,
                    Color.Lerp(KikasaZenithArsenal.ZenithColor, Color.White, 0.4f) * (0.44f * charge), dashAng,
                    gOrigin, new Vector2(len * 1.7f / glow.Width, 3.4f / glow.Height), SpriteEffects.None, 0f);
                sb.Draw(glow, bladePos - Main.screenPosition, null,
                    BloodBright * (0.24f * charge), 0f,
                    gOrigin, new Vector2(17f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //终结闪拍：穿越点的星芒余光
            if (finFlashTick > 0 && star != null) {
                EnsureBegin();
                float a = finFlashTick / 10f;
                Vector2 sOrigin = star.Size() * 0.5f;
                sb.Draw(star, slashMid - Main.screenPosition, null,
                    KikasaZenithArsenal.ZenithColor * (0.7f * a), Seed,
                    sOrigin, 0.5f * a + 0.12f, SpriteEffects.None, 0f);
                sb.Draw(star, slashMid - Main.screenPosition, null,
                    Color.White * (0.5f * a), Seed + MathHelper.PiOver4,
                    sOrigin, 0.3f * a + 0.08f, SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            //谢幕残珠：留一口水；残余幻影就地散成星屑
            RetirePhantoms(shatter: true);
            if (Main.dedServ || !bladeInit) {
                return;
            }
            for (int k = 0; k < 5; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    bladePos + Main.rand.NextVector2Circular(14f, 14f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.5f, 2.4f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 24), 0f);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.65f, Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(Main.rand.Next(45, 70));
        }
    }
}
