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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaMinishark
{
    /// <summary>
    /// 械奴·湖水鲨群（迷你鲨/巨兽鲨换皮共用）。单弹幕同时驱动至多五把湖水凝成的枪：
    /// Projectile.Center 为编队质心权威同步，各枪位置由状态机 + Seed 在各端本地推算
    /// （双子/毁灭者内部模拟范式），硬纠阈值防抽搐。材质身份：凝不全的湖水枪——
    /// 实体上半 + 液态下缘（KikasaItemForm 扫描模式、水线呼吸起伏）+ 液态水鞘包衣
    /// 慢晃 + 水光沿身扫掠 + 下缘凝珠滴淌；移动即游弋（贴速度倾斜入弯、周期沿轨道
    /// 抢位超车）。签名机制：列阵齐射（扇形阵锁线后轮转开火，每发后坐弹簧回弹 +
    /// 枪口水花锥 + 枪口上跳，边打边横移）与鲨群环猎（散到目标环位加速环绕
    /// 内向射击，收拍全员穿心交错）。联机契约与双子同构：owner 裁决转场盖
    /// netUpdate 章、节拍闩防快照回卷、生命线只有 owner 判；
    /// 枪数与换皮物品类型在 spawn 后经 ExtraAI 随包补发（生成包迟一帧契约）
    /// </summary>
    internal class KikasaMinisharkServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>湖水子弹基伤（召唤加成与换皮档倍率前），由子弹幕消费</summary>
        internal const int ShotDamage = 165;

        /// <summary>编队上限：湖藏存量再多也只凝五把</summary>
        internal const int MaxGuns = 5;

        //==================== 换皮档案：同一鲨群骨架按沉入武器换贴图/口径 ====================

        /// <summary>换皮档：伤害倍率随武器时代，枪口探出随贴图长度</summary>
        private readonly record struct ArmsProfile(float DamageMul, float MuzzleLen);

        private static ArmsProfile ProfileOf(int itemType)
            => itemType == ItemID.Megashark
                ? new ArmsProfile(2.4f, 34f)
                : new ArmsProfile(1f, 26f);

        /// <summary>沉入湖中的原型武器物品类型：贴图与档位来源，ExtraAI 同步</summary>
        private int armsItemType = ItemID.Minishark;

        private ArmsProfile Profile => ProfileOf(armsItemType);

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateVolley = 2;
        private const int StateCarousel = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：环猎期符号=环绕方向；其余为 0</summary>
        private ref float StateParam => ref Projectile.ai[2];

        private float TiltDir => MathF.Sign(StateParam) == 0f ? 1f : MathF.Sign(StateParam);

        //==================== 时序 ====================

        //出水：多点预兆汇聚→逐枪错帧破水翻腾→扫描凝实→整队上膛拍
        private const int OmenFrames = 26;
        private const int BreachGap = 7;
        private const int RiseEnd = 74;
        private const int FormupFrame = 84;
        private const int EmergeTotal = 100;
        /// <summary>相邻破水点横距</summary>
        private const float EmergeSpan = 62f;

        //齐射：甩入扇形阵→锁线 telegraph→轮转开火（边打边横移）→收势
        private const int VolleyFormEnd = 16;
        private const int VolleyLockEnd = 30;
        private const int VolleyFireEnd = 96;
        private const int VolleyTotal = 112;
        /// <summary>单枪射击周期与相邻枪错帧：五枪叠出不断火的持续弹幕</summary>
        private const int FirePeriod = 15;
        private const int FireStagger = 3;

        //环猎：冲位→加速环绕收紧（内向射击）→穿心交错→归队
        private const int CarouselDashEnd = 18;
        private const int CarouselSpinEnd = 96;
        private const int CarouselCrossEnd = 112;
        private const int CarouselTotal = 132;
        private const float CarouselRadius = 205f;
        private const int CarouselFirePeriod = 12;
        private const int CarouselFireStagger = 2;

        //溶解：逐枪错帧失力坠湖
        private const int DissolveStagger = 5;
        private const int DissolveFrames = 70;

        //==================== 各枪本地模拟（各端自算，质心同步纠偏）====================

        private readonly Vector2[] gunPos = new Vector2[MaxGuns];
        private readonly Vector2[] gunVel = new Vector2[MaxGuns];
        private readonly Vector2[] gunTarget = new Vector2[MaxGuns];
        private readonly float[] gunRot = new float[MaxGuns];
        /// <summary>出水翻腾角速度</summary>
        private readonly float[] gunSpin = new float[MaxGuns];
        /// <summary>后坐量 px，沿 -瞄准向偏移绘制位并抬枪口</summary>
        private readonly float[] gunRecoil = new float[MaxGuns];
        /// <summary>贴图翻面状态：带滞回，瞄向正上/正下时不逐帧镜像抖动</summary>
        private readonly bool[] gunFlip = new bool[MaxGuns];
        private readonly Vector2[][] gunOld = new Vector2[MaxGuns][];
        private readonly float[][] gunOldRot = new float[MaxGuns][];
        private bool gunsInit;

        /// <summary>编队枪数：owner 在 Summon 里定值，ExtraAI 随包同步；远端首包前按满编</summary>
        private int gunCount = MaxGuns;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private readonly bool[] breachDone = new bool[MaxGuns];
        private readonly int[] muzzleFlash = new int[MaxGuns];
        private readonly int[] lastFireTick = new int[MaxGuns];
        private readonly bool[] dissolveSplashed = new bool[MaxGuns];
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;
        private bool formSnapDone;
        private bool dashWhooshDone;
        private bool crossLaunched;
        private bool crossFlashed;
        private int crossFlashTick;

        //==================== 血色板（随观看域鬼雨异化冷化，与湖系同族）====================

        internal static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        internal static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        internal static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        internal static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        internal static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        /// <summary>枪口热闪的暖点缀，只作次要加色层</summary>
        internal static Color MuzzleHot => KikasaDomain.CoolTint(new(255, 190, 170), new(200, 220, 222));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.6173f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaArmsIndex 登记的召唤入口；emergeAt.Y = 湖面，count = 湖藏存量，
        /// itemType = 沉入的原型武器（换皮档）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt, int count, int itemType) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            count = Math.Clamp(count, 1, MaxGuns);
            ArmsProfile profile = ProfileOf(itemType);
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ShotDamage * profile.DamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaMinisharkServant>(), damage, 2f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaMinisharkServant pack) {
                //生成包已经带默认编制出门了，这里改完补一发 ExtraAI（迟一帧只影响预兆涟漪点数）
                pack.gunCount = count;
                pack.armsItemType = itemType;
                Main.projectile[index].netUpdate = true;
            }
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //环猎期枪群散布远超质心 hitbox，出屏也要画
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

        /// <summary>枪群不做接触判定，伤害全在湖水子弹上</summary>
        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)gunCount);
            writer.Write(armsItemType);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int count = Math.Clamp((int)reader.ReadByte(), 1, MaxGuns);
            int itemType = reader.ReadInt32();
            if (itemType > ItemID.None && itemType < ItemLoader.ItemCount) {
                armsItemType = itemType;
            }
            if (count != gunCount) {
                gunCount = count;
                //编制变了按新编制重建
                gunsInit = false;
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
            //一把都没破水就要收场：直接收掉，免得溶解演出让枪凭空闪现再化水
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
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ShotDamage * Profile.DamageMul);

            //换场清闩：远端可能靠收包换场而非本地同拍转场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                dashWhooshDone = false;
                crossLaunched = false;
                crossFlashed = false;
                Array.Fill(lastFireTick, -1);
                if (State == StateDissolve) {
                    Array.Fill(dissolveSplashed, false);
                }
            }

            if (!gunsInit) {
                RebuildGuns(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateVolley: UpdateVolley(owner, authority); break;
                case StateCarousel: UpdateCarousel(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateGuns(owner, domain);
            PushGunHistory();
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            for (int i = 0; i < gunCount; i++) {
                if (muzzleFlash[i] > 0) {
                    muzzleFlash[i]--;
                }
                gunRecoil[i] *= 0.76f;
                float glow = GunAlpha(i) * 0.35f;
                if (glow > 0.02f) {
                    Lighting.AddLight(gunPos[i], 0.4f * glow, 0.1f * glow, 0.09f * glow);
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：多点预兆、错帧破水翻腾、整队上膛拍 ====================

        private float BreachX(int i)
            => Projectile.Center.X + (i - (gunCount - 1) * 0.5f) * EmergeSpan;

        private static int BreachTime(int i) => OmenFrames + i * BreachGap;

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                //水下待命：多处水面同时起预兆，涟漪向各自破水点汇聚
                Projectile.velocity = Vector2.Zero;
                if (viewed && t % 5 == 2) {
                    float converge = 1f - t / (float)OmenFrames;
                    for (int i = 0; i < gunCount; i++) {
                        float wobble = MathF.Sin(t * 0.5f + i * 1.7f) * converge * 26f;
                        KikasaDomainDeco.RippleAt(new Vector2(BreachX(i) + wobble, lakeY),
                            0.3f + (1f - converge) * 0.4f);
                    }
                }
                //几声滴响预告：这不止一把
                if (viewed && (t == 5 || t == 14 || t == 22)) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.42f,
                        Pitch = -0.55f + t * 0.012f,
                        MaxInstances = 3
                    }, new Vector2(Projectile.Center.X, lakeY));
                }
                return;
            }

            //错帧破水：一把接一把跃出，翻腾甩水
            for (int i = 0; i < gunCount; i++) {
                if (!breachDone[i] && t >= BreachTime(i)) {
                    breachDone[i] = true;
                    gunVel[i] = new Vector2(0f, -12.6f - i * 0.3f);
                    gunSpin[i] = (i % 2 == 0 ? 1f : -1f) * 0.34f;
                    if (i == 0) {
                        Projectile.velocity = new Vector2(0f, -3.2f);
                    }
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.72f,
                        Pitch = -0.38f + i * 0.07f,
                        MaxInstances = 3
                    }, gunPos[i]);
                    if (viewed) {
                        BreachBurst(new Vector2(BreachX(i), lakeY), i);
                    }
                }
            }

            //升起：破水动量指数衰减，质心缓浮
            Projectile.velocity *= 0.96f;

            //身上的湖水成帘往下淌（只淌已破水的枪）
            if (viewed && t < RiseEnd) {
                for (int i = 0; i < gunCount; i++) {
                    if (t < BreachTime(i) || t % 3 != i % 3) {
                        continue;
                    }
                    Vector2 dropPos = gunPos[i] + new Vector2(
                        Main.rand.NextFloat(-24f, 24f), Main.rand.NextFloat(2f, 14f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.4f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(12, 24), 0f);
                }
            }

            //整队上膛双拍：全员一顿、枪机咔嗒——它们醒了
            if (!formSnapDone && t >= FormupFrame) {
                formSnapDone = true;
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = -0.25f, MaxInstances = 2 }, Projectile.Center);
                for (int i = 0; i < gunCount; i++) {
                    gunVel[i] += new Vector2(
                        -MathF.Sign(gunPos[i].X - Projectile.Center.X) * 1.8f, -1.2f);
                    if (viewed) {
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(
                                gunPos[i] + Main.rand.NextVector2Circular(16f, 8f),
                                new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.5f, 1.8f)),
                                BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))
                                ?.Configure(Main.rand.Next(10, 18), 0.25f);
                        }
                    }
                }
                if (viewed) {
                    ShakeViewer(2f);
                }
            }
            if (t == FormupFrame + 4) {
                //第二声咔嗒收尾上膛
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = 0.05f, MaxInstances = 2 }, Projectile.Center);
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

        /// <summary>单枪破水浪冠：环涟漪 + 扇形水珠 + 水柱束 + 血雾，规格比 boss 鬼奴收一号</summary>
        private void BreachBurst(Vector2 hit, int i) {
            KikasaDomainDeco.RippleAt(hit, 1.4f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(22f, 0f), 0.6f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(20f, 0f), 0.55f);
            KikasaDomainDeco.SplashAt(hit, 8);

            for (int k = 0; k < 12; k++) {
                float angle = -MathHelper.Pi * (0.16f + 0.68f * k / 11f);
                float speed = Main.rand.NextFloat(2.6f, 6f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-14f, 14f), -4f),
                    angle.ToRotationVector2() * speed,
                    BloodMain * Main.rand.NextFloat(0.45f, 0.65f),
                    Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-5f, 5f), -6f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(7f, 11f)),
                    BloodMain * Main.rand.NextFloat(0.5f, 0.7f),
                    Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(26, 40), Main.rand.NextFloat(-0.3f, 0.3f));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                hit + new Vector2(Main.rand.NextFloat(-18f, 18f), -8f),
                new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.6f)),
                MistBlood * 0.75f, Main.rand.NextFloat(0.55f, 0.8f))
                ?.Configure(Main.rand.Next(50, 80));

            //首尾两把带一记闷响垫底，中间几把只留水声——五连不轰成一锅
            if (i == 0 || i == gunCount - 1) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = 0.32f,
                    Pitch = -0.75f,
                    MaxInstances = 1
                }, hit);
            }
            ShakeViewer(1.5f);
        }

        //==================== 跟随：鲨群环游 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            //质心锚：贴着玩家，编队绕质心游
            Vector2 anchor = owner.Center + new Vector2(0f, -26f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢就贴回来，别拖着编队横穿半张地图
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildGuns(owner.GetModPlayer<KikasaDomainPlayer>());
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.09f;
            const float maxSpeed = 17f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);

            //出手裁决：齐射→环猎轮换；转场规则各端一致，owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                StateTimer = 0;
                if (attackIndex % 2 == 1) {
                    State = StateVolley;
                    StateParam = 0;
                }
                else {
                    State = StateCarousel;
                    //环绕方向盖进 ai[2] 符号，owner 章一并带给远端
                    StateParam = (Projectile.identity + attackIndex) % 2 == 0 ? 1f : -1f;
                }
                Projectile.netUpdate = authority;
            }
        }

        //==================== 列阵齐射 ====================

        private void UpdateVolley(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= VolleyLockEnd) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                : Projectile.Center + gunRot[0].ToRotationVector2() * 500f;

            //质心压到玩家与目标之间的射击位，随扫射横移——边打边走
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
            float strafe = MathF.Sin(t * 0.05f + Seed) * 30f;
            Vector2 anchor = owner.Center + toT * 62f + perp * strafe + new Vector2(0f, -22f);
            Vector2 desired = (anchor - Projectile.Center) * 0.11f;
            if (desired.Length() > 14f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 14f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.18f);

            //锁线三声上膛，音高爬升——开火前的呼吸
            if (t == 4 || t == 12 || t == 20) {
                SoundEngine.PlaySound(SoundID.Unlock with {
                    Volume = 0.4f,
                    Pitch = -0.4f + t * 0.028f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            //轮转开火：相邻枪错帧，节拍闩防快照回卷重播
            if (t > VolleyLockEnd && t <= VolleyFireEnd) {
                for (int i = 0; i < gunCount; i++) {
                    int local = t - VolleyLockEnd - i * FireStagger;
                    if (local >= 0 && local % FirePeriod == 0) {
                        int tick = local / FirePeriod;
                        if (tick > lastFireTick[i]) {
                            lastFireTick[i] = tick;
                            FireGun(owner, authority, i);
                        }
                    }
                }
            }

            if (t >= VolleyTotal) {
                EndAttack(authority, 115);
            }
        }

        /// <summary>锁线蓄力进度 0~1，绘制层预告线共用</summary>
        private float VolleyLockCharge() {
            if (State != StateVolley) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= VolleyFormEnd) {
                return 0f;
            }
            return MathHelper.Clamp((t - VolleyFormEnd) / (float)(VolleyLockEnd - VolleyFormEnd), 0f, 1f);
        }

        /// <summary>开火前 3 帧预告线闪亮</summary>
        private float VolleyTelegraphFlash() {
            if (State != StateVolley) {
                return 0f;
            }
            int dt = VolleyLockEnd - (int)StateTimer;
            return dt is >= 0 and <= 3 ? 1f - dt / 4f : 0f;
        }

        private void FireGun(Player owner, bool authority, int i) {
            Vector2 aimDir = gunRot[i].ToRotationVector2();
            Vector2 muzzle = MuzzlePos(i);
            gunRecoil[i] = 13f;
            gunVel[i] -= aimDir * 1.3f;
            muzzleFlash[i] = 4;

            SoundEngine.PlaySound(SoundID.Item11 with {
                Volume = 0.3f,
                Pitch = -0.12f + i * 0.05f,
                MaxInstances = 4
            }, muzzle);
            if (!Main.dedServ) {
                //枪口水花锥：出膛的水被崩碎
                for (int k = 0; k < 3; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(muzzle,
                        aimDir.RotatedBy(Main.rand.NextFloat(-0.34f, 0.34f)) * Main.rand.NextFloat(2f, 5f),
                        BloodMain * 0.55f, Main.rand.NextFloat(0.28f, 0.48f))
                        ?.Configure(Main.rand.Next(8, 14), 0.2f);
                }
            }
            if (ViewedOwner) {
                ShakeViewer(0.4f);
            }

            //弹体只在 owner 端生成，spawn 包自带全部初值
            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ShotDamage * Profile.DamageMul);
                Vector2 vel = aimDir.RotatedBy(Main.rand.NextFloat(-0.05f, 0.05f)) * 16.5f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), muzzle, vel,
                    ModContent.ProjectileType<KikasaMinisharkBullet>(), damage, 2f, Projectile.owner);
            }
        }

        //==================== 鲨群环猎 ====================

        /// <summary>环角解析式：ω 前 60 帧 0.05→0.16 线性加速再恒速，各端从任意 t 都能重建</summary>
        private static float CarouselTheta(int t) {
            float spinT = MathF.Min(t, CarouselSpinEnd);
            return spinT < 60f
                ? 0.05f * spinT + 0.11f * spinT * spinT / 120f
                : 0.05f * 60f + 0.11f * 30f + 0.16f * (spinT - 60f);
        }

        private float CarouselAngle(int i, int t)
            => Seed * 1.3f + TiltDir * CarouselTheta(t) + i * MathHelper.TwoPi / gunCount;

        private float CarouselRadiusAt(int t)
            => CarouselRadius - 48f * MathF.Min(t / (float)CarouselSpinEnd, 1f)
                + MathF.Sin(t * 0.11f + Seed) * 12f;

        private void UpdateCarousel(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= CarouselDashEnd) {
                EndAttack(authority, 60);
                return;
            }

            //冲位拍：两声破空
            if (!dashWhooshDone) {
                dashWhooshDone = true;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.55f, Pitch = -0.15f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
                if (ViewedOwner) {
                    ShakeViewer(2.2f);
                }
            }

            //质心压向猎物：环心就是权威同步的锚，各端枪位从环角公式自算
            if (t <= CarouselCrossEnd) {
                Vector2 want = target >= 0
                    ? (Main.npc[target].Center + Main.npc[target].velocity * 4f - Projectile.Center) * 0.14f
                    : Vector2.Zero;
                if (want.Length() > 21f) {
                    want = want.SafeNormalize(Vector2.Zero) * 21f;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.25f);
            }
            else {
                //收势段质心退回玩家身侧
                Vector2 back = (owner.Center + new Vector2(0f, -26f) - Projectile.Center) * 0.08f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, back, 0.15f);
            }

            //环绕期内向射击：密一档的轮转
            if (t > CarouselDashEnd && t <= CarouselSpinEnd) {
                for (int i = 0; i < gunCount; i++) {
                    int local = t - CarouselDashEnd - i * CarouselFireStagger;
                    if (local >= 0 && local % CarouselFirePeriod == 0) {
                        int tick = local / CarouselFirePeriod;
                        if (tick > lastFireTick[i]) {
                            lastFireTick[i] = tick;
                            FireGun(owner, authority, i);
                        }
                    }
                }
            }

            if (crossFlashTick > 0) {
                crossFlashTick--;
            }
            if (t >= CarouselTotal) {
                EndAttack(authority, 150);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解：逐枪失力坠湖 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;

            //质心跟着一起沉
            if (lakeAlive) {
                Projectile.velocity.X *= 0.94f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.2f, 8f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            //各自的过水线拍（错帧落水，音高逐枪递变）
            for (int i = 0; i < gunCount; i++) {
                int lt = t - i * DissolveStagger;
                if (lakeAlive && !dissolveSplashed[i] && lt >= 0 && gunPos[i].Y >= lakeY) {
                    dissolveSplashed[i] = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.55f,
                        Pitch = -0.4f + i * 0.08f,
                        MaxInstances = 3
                    }, gunPos[i]);
                    if (ViewedOwner) {
                        Vector2 hit = new(gunPos[i].X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 6);
                        KikasaDomainDeco.RippleAt(hit, 0.9f);
                        ShakeViewer(1f);
                    }
                }
            }

            //边沉边化成水珠
            if (!Main.dedServ && GunAlpha(0) > 0.15f) {
                int i = t % gunCount;
                if (t - i * DissolveStagger >= 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        gunPos[i] + Main.rand.NextVector2Circular(20f, 10f),
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

        //==================== 各枪推进：状态机 + Seed 确定性驻位，本地弹簧追随 ====================

        /// <summary>初始化或硬纠时按当前状态直接落位，防枪群与残影抽搐</summary>
        private void RebuildGuns(KikasaDomainPlayer domain) {
            gunsInit = true;
            for (int i = 0; i < MaxGuns; i++) {
                if (State == StateEmerge) {
                    gunPos[i] = new Vector2(BreachX(i), domain.LakeWorldY + 26f);
                    gunRot[i] = -MathHelper.PiOver2;
                }
                else {
                    float phase = Main.GlobalTimeWrappedHourly * 0.62f + Seed + i * MathHelper.TwoPi / Math.Max(gunCount, 1);
                    gunPos[i] = Projectile.Center + new Vector2(MathF.Cos(phase) * 118f, MathF.Sin(phase) * 54f - 34f);
                    gunRot[i] = 0f;
                }
                gunFlip[i] = MathF.Cos(gunRot[i]) < 0f;
                gunVel[i] = Vector2.Zero;
                gunSpin[i] = 0f;
                gunRecoil[i] = 0f;
                gunTarget[i] = gunPos[i];
                gunOld[i] ??= new Vector2[8];
                gunOldRot[i] ??= new float[8];
                for (int k = 0; k < gunOld[i].Length; k++) {
                    gunOld[i][k] = gunPos[i];
                    gunOldRot[i][k] = gunRot[i];
                }
            }
        }

        private void ChaseGun(int i, float accel, float damp) {
            gunVel[i] = (gunVel[i] + (gunTarget[i] - gunPos[i]) * accel) * damp;
            gunPos[i] += gunVel[i];
        }

        /// <summary>呼吸浮动相位（Seed 确定性，各端一致）</summary>
        private float Sway(int i, float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed + i * 2.4f) * amp;

        private void FaceGun(int i, Vector2 worldPos, float rate) {
            float want = (worldPos - gunPos[i]).ToRotation();
            gunRot[i] = gunRot[i].AngleLerp(want, rate);
        }

        private void UpdateGuns(Player owner, KikasaDomainPlayer domain) {
            if (!gunsInit) {
                return;
            }
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            Vector2 targetPos = target >= 0 ? Main.npc[target].Center : owner.Center;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    for (int i = 0; i < gunCount; i++) {
                        if (t < BreachTime(i)) {
                            //水下待命：钉在破水点，鼻朝上
                            gunPos[i] = new Vector2(BreachX(i), lakeY + 26f);
                            gunVel[i] = Vector2.Zero;
                            gunTarget[i] = gunPos[i];
                            gunRot[i] = -MathHelper.PiOver2;
                            continue;
                        }
                        //破水后：先弹道升+翻腾，14 帧后弹簧接管贴向悬位
                        gunTarget[i] = new Vector2(BreachX(i), lakeY - 96f + Sway(i, 2.1f, 9f));
                        int lt = t - BreachTime(i);
                        if (lt < 14) {
                            gunVel[i].Y *= 0.955f;
                            gunVel[i].X *= 0.98f;
                            gunPos[i] += gunVel[i];
                            gunRot[i] += gunSpin[i];
                            gunSpin[i] *= 0.94f;
                        }
                        else {
                            ChaseGun(i, 0.05f, 0.86f);
                            gunRot[i] += gunSpin[i];
                            gunSpin[i] *= 0.9f;
                            if (MathF.Abs(gunSpin[i]) < 0.05f) {
                                //翻腾散尽后校平，鼻朝外
                                float level = gunPos[i].X >= Projectile.Center.X ? 0f : MathHelper.Pi;
                                gunRot[i] = gunRot[i].AngleLerp(level, 0.14f);
                            }
                        }
                    }
                    break;
                }
                case StateFollow: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    for (int i = 0; i < gunCount; i++) {
                        float phase = tGlobal * 0.62f + Seed + i * MathHelper.TwoPi / gunCount;
                        //抢位冲刺：错帧周期沿轨道切向加塞——鲨群的超车
                        float dartT = (t + i * 41) % 170;
                        float dart = dartT < 22 ? MathF.Sin(dartT / 22f * MathHelper.Pi) * 46f : 0f;
                        Vector2 radial = new(MathF.Cos(phase) * 118f, MathF.Sin(phase) * 54f - 34f);
                        Vector2 tangent = new Vector2(-MathF.Sin(phase) * 118f, MathF.Cos(phase) * 54f)
                            .SafeNormalize(Vector2.UnitX);
                        Vector2 slot = Projectile.Center + radial + tangent * dart;
                        slot.Y += MathF.Sin(tGlobal * 2.3f + Seed * 2f + i * 1.9f) * 7f;
                        gunTarget[i] = slot;
                        ChaseGun(i, 0.06f, 0.84f);

                        //朝向：有猎物盯猎物，没有就贴游弋速度入弯
                        if (target >= 0) {
                            FaceGun(i, targetPos, 0.16f);
                        }
                        else if (gunVel[i].Length() > 2.6f) {
                            gunRot[i] = gunRot[i].AngleLerp(gunVel[i].ToRotation(), 0.12f);
                        }
                        else {
                            gunRot[i] = gunRot[i].AngleLerp(owner.direction > 0 ? 0f : MathHelper.Pi, 0.05f);
                        }
                    }
                    break;
                }
                case StateVolley: {
                    Vector2 focus = target >= 0 ? Main.npc[target].Center : Projectile.Center + gunRot[0].ToRotationVector2() * 500f;
                    Vector2 toT = (focus - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
                    Vector2 aimPos = target >= 0
                        ? Main.npc[target].Center + Main.npc[target].velocity * 7f
                        : focus;
                    for (int i = 0; i < gunCount; i++) {
                        //扇形阵：沿垂直向排开，边缘略后收成弧
                        float lane = i - (gunCount - 1) * 0.5f;
                        Vector2 slot = Projectile.Center + perp * (lane * 38f + Sway(i, 1.8f, 4f))
                            - toT * (MathF.Abs(lane) * 15f - 12f);
                        gunTarget[i] = slot;
                        ChaseGun(i, t < VolleyFormEnd ? 0.12f : 0.08f, 0.8f);
                        //锁线期快甩到位，开火期稳咬提前量
                        FaceGun(i, aimPos, t < VolleyLockEnd ? 0.3f : 0.45f);
                    }
                    break;
                }
                case StateCarousel: {
                    Vector2 aimPos = target >= 0
                        ? Main.npc[target].Center + Main.npc[target].velocity * 5f
                        : Projectile.Center;
                    if (t <= CarouselSpinEnd) {
                        float radius = CarouselRadiusAt(t);
                        for (int i = 0; i < gunCount; i++) {
                            Vector2 ring = Projectile.Center + CarouselAngle(i, t).ToRotationVector2() * radius;
                            if (t <= CarouselDashEnd) {
                                //冲位段：弹簧快追散开
                                gunTarget[i] = ring;
                                ChaseGun(i, 0.16f, 0.78f);
                            }
                            else {
                                //环绕段：公式直落位，速度记差分供拖尾/残影
                                skipFix = true;
                                gunVel[i] = ring - gunPos[i];
                                gunPos[i] = ring;
                                gunTarget[i] = ring;
                            }
                            FaceGun(i, aimPos, 0.4f);
                        }
                    }
                    else if (t <= CarouselCrossEnd) {
                        //穿心交错：一帧定速冲过环心互换
                        skipFix = true;
                        if (!crossLaunched) {
                            crossLaunched = true;
                            for (int i = 0; i < gunCount; i++) {
                                gunVel[i] = (Projectile.Center - gunPos[i]).SafeNormalize(Vector2.UnitX) * 30f;
                            }
                            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = 0.05f, MaxInstances = 3 }, Projectile.Center);
                        }
                        float meanDist = 0f;
                        for (int i = 0; i < gunCount; i++) {
                            gunVel[i] *= 1.005f;
                            gunPos[i] += gunVel[i];
                            gunTarget[i] = gunPos[i];
                            gunRot[i] = gunVel[i].ToRotation();
                            meanDist += Vector2.Distance(gunPos[i], Projectile.Center);
                            //沿途甩出速度拉伸的水珠
                            if (!Main.dedServ && Main.rand.NextBool(2)) {
                                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                                    gunPos[i] - gunVel[i] * 0.4f + Main.rand.NextVector2Circular(10f, 10f),
                                    -gunVel[i] * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                                    ?.Configure(Main.rand.Next(8, 15), 0f);
                            }
                        }
                        meanDist /= gunCount;
                        //交错瞬间：全员擦身而过的重拍
                        if (!crossFlashed && meanDist < 46f) {
                            crossFlashed = true;
                            crossFlashTick = 8;
                            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.42f, Pitch = 0.1f, MaxInstances = 2 }, Projectile.Center);
                            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.45f, Pitch = 0.1f, MaxInstances = 2 }, Projectile.Center);
                            if (!Main.dedServ) {
                                PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodBright, 0.08f)
                                    ?.Configure(new Vector2(0.6f, 1f), Seed, 0.3f, 9);
                                for (int k = 0; k < 8; k++) {
                                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                        Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                                        Main.rand.NextVector2Circular(3.5f, 3.5f),
                                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 26));
                                }
                            }
                            if (ViewedOwner) {
                                ShakeViewer(3f);
                            }
                        }
                    }
                    else {
                        //收势：软贴回跟随环位
                        float tGlobal = Main.GlobalTimeWrappedHourly;
                        for (int i = 0; i < gunCount; i++) {
                            float phase = tGlobal * 0.62f + Seed + i * MathHelper.TwoPi / gunCount;
                            gunTarget[i] = Projectile.Center + new Vector2(MathF.Cos(phase) * 118f, MathF.Sin(phase) * 54f - 34f);
                            ChaseGun(i, 0.07f, 0.85f);
                            if (gunVel[i].Length() > 2.6f) {
                                gunRot[i] = gunRot[i].AngleLerp(gunVel[i].ToRotation(), 0.14f);
                            }
                        }
                    }
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    for (int i = 0; i < gunCount; i++) {
                        int lt = t - i * DissolveStagger;
                        if (lt < 0) {
                            //失力前的迟滞：悬停渐沉
                            gunVel[i] *= 0.95f;
                            gunVel[i].Y += 0.05f;
                        }
                        else {
                            gunVel[i].X *= 0.93f;
                            gunVel[i].Y = MathF.Min(gunVel[i].Y + 0.3f, 9.5f);
                            //鼻端下垂着沉下去
                            float droop = gunRot[i] + (MathF.Cos(gunRot[i]) >= 0f ? 0.5f : -0.5f);
                            gunRot[i] = gunRot[i].AngleLerp(droop, 0.02f);
                        }
                        gunPos[i] += gunVel[i];
                        gunTarget[i] = gunPos[i];
                    }
                    break;
                }
            }

            //硬纠：同步包把质心拽走半屏时按驻位重建，防弹簧甩鞭
            if (!skipFix) {
                for (int i = 0; i < gunCount; i++) {
                    if (Vector2.Distance(gunPos[i], gunTarget[i]) > 780f) {
                        gunPos[i] = gunTarget[i];
                        gunVel[i] = Vector2.Zero;
                    }
                }
            }

            //翻面滞回：cos 越过 ±0.22 才换面，正上/正下瞄准不抖镜像
            for (int i = 0; i < gunCount; i++) {
                float c = MathF.Cos(gunRot[i]);
                if (c > 0.22f) {
                    gunFlip[i] = false;
                }
                else if (c < -0.22f) {
                    gunFlip[i] = true;
                }
            }
        }

        private void PushGunHistory() {
            for (int i = 0; i < gunCount; i++) {
                Vector2[] arr = gunOld[i];
                float[] rots = gunOldRot[i];
                if (arr == null) {
                    continue;
                }
                for (int k = arr.Length - 1; k >= 1; k--) {
                    arr[k] = arr[k - 1];
                    rots[k] = rots[k - 1];
                }
                arr[0] = gunPos[i];
                rots[0] = gunRot[i];
            }
        }

        /// <summary>常驻氛围：液态下缘（水线区）偶发凝珠滴落——枪一直在往下滴湖水</summary>
        private void UpdateAmbient() {
            if (Main.dedServ || State is not (StateFollow or StateVolley or StateCarousel)) {
                return;
            }
            if (Main.rand.NextBool(16)) {
                int i = Main.rand.Next(gunCount);
                if (GunAlpha(i) > 0.5f) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        gunPos[i] + new Vector2(Main.rand.NextFloat(-20f, 20f), Main.rand.NextFloat(6f, 12f)),
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
        private Vector2 GunDrawPos(int i)
            => gunPos[i] - gunRot[i].ToRotationVector2() * gunRecoil[i];

        /// <summary>枪口位：绘制位沿瞄准向探出半个枪身（长度随换皮档）</summary>
        private Vector2 MuzzlePos(int i)
            => GunDrawPos(i) + gunRot[i].ToRotationVector2() * Profile.MuzzleLen;

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float GunAlpha(int i) {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < BreachTime(i) ? 0f : MathHelper.Clamp((t - BreachTime(i)) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>
        /// uForm：1=全血水 0=真身。常驻走扫描模式（见 CurrentScanMode）——
        /// 实体上半 + 液态下缘，水线随呼吸慢起伏；斑驳交融模式在小贴图上读作满屏噪点，
        /// 已弃用（2026-08 用户判"沙沙嚷嚷全是噪点"）。出水自血水凝出、溶解水线漫上来
        /// </summary>
        private float GunForm(int i) {
            int t = (int)StateTimer;
            //水线呼吸：下缘 8%~23% 间涨落，各枪错相
            float steady = 0.24f
                + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + Seed + i * 1.7f) * 0.06f;
            return State switch {
                StateEmerge => t < BreachTime(i)
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp(
                        (t - BreachTime(i)) / (float)(RiseEnd - BreachTime(i)), 0f, 1f))),
                //溶解：水线自下缘漫上来，配合 uDissolve 蚀散读作"化回湖水"
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.6f, 0f, 1f),
                _ => steady,
            };
        }

        /// <summary>uScanMode：恒为扫描模式——凝实线干净利落，水线即材质身份</summary>
        private static float CurrentScanMode() => 1f;

        /// <summary>uDissolve：溶解期逐枪错帧蚀散，落水的先散</summary>
        private float DissolveAmt(int i) {
            if (State != StateDissolve) {
                return 0f;
            }
            float p = MathF.Pow(MathHelper.Clamp((StateTimer - i * DissolveStagger) / 46f, 0f, 1f), 0.9f);
            return MathHelper.Clamp(p + (dissolveSplashed[i] ? 0.15f : 0f), 0f, 1f);
        }

        private float GunScale(int i) {
            float scale = 1f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= BreachTime(i) && t < BreachTime(i) + 10) {
                //破水过冲
                scale *= 1f + 0.08f * (1f - (t - BreachTime(i)) / 10f);
            }
            //后坐压缩一口气
            scale *= 1f - gunRecoil[i] * 0.004f;
            return scale;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!gunsInit) {
                return false;
            }
            Main.instance.LoadItem(armsItemType);
            Texture2D tex = TextureAssets.Item[armsItemType]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            //冲刺残影：环猎/穿心/抢位时亮
            DrawDashTrails(sb, tex);

            //枪群本体：血湖材质
            DrawBodies(sb, tex);

            //加色层：预兆水光 / 锁线预告 / 枪口闪 / 交错闪拍
            DrawGlow(sb);

            return false;
        }

        /// <summary>
        /// 翻面走水平镜像 + 旋转加 π（持枪标准做法）：贴图 V 轴不动，
        /// 扫描水线永远贴着枪的下缘——竖直镜像会把液态下缘翻到枪顶上去
        /// </summary>
        private SpriteEffects GunFx(int i)
            => gunFlip[i] ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        /// <summary>翻面时绘制旋转补 π：镜像后的枪鼻在 rotation=0 时朝左</summary>
        private float FlipRotOffset(int i) => gunFlip[i] ? MathHelper.Pi : 0f;

        /// <summary>绘制用旋转：后坐抬枪口（屏幕向上，符号随翻面）</summary>
        private float GunDrawRot(int i)
            => gunRot[i] - gunRecoil[i] * 0.006f * (gunFlip[i] ? -1f : 1f);

        private void DrawDashTrails(SpriteBatch sb, Texture2D tex) {
            Vector2 origin = tex.Size() * 0.5f;
            for (int i = 0; i < gunCount; i++) {
                //残影随速度平滑淡入，不做硬阈值弹跳
                float trailA = MathHelper.Clamp((gunVel[i].Length() - 8f) / 10f, 0f, 1f) * GunAlpha(i);
                if (trailA <= 0.03f) {
                    continue;
                }
                Vector2[] arr = gunOld[i];
                float[] rots = gunOldRot[i];
                for (int k = arr.Length - 1; k >= 1; k--) {
                    float fall = 1f - k / (float)arr.Length;
                    sb.Draw(tex, arr[k] - Main.screenPosition, null,
                        BloodMain * (0.26f * fall * trailA), rots[k] + FlipRotOffset(i),
                        origin, GunScale(i) * (0.96f - k * 0.015f), GunFx(i), 0f);
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
                form.Parameters["uScanMode"]?.SetValue(CurrentScanMode());
                form.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(tex.Width / (float)tex.Height);
            }

            Vector2 origin = tex.Size() * 0.5f;
            for (int i = 0; i < gunCount; i++) {
                float alpha = GunAlpha(i);
                if (alpha <= 0.01f) {
                    continue;
                }
                float rot = GunDrawRot(i) + FlipRotOffset(i);
                Vector2 drawPos = GunDrawPos(i) - Main.screenPosition;
                float dissolve = DissolveAmt(i);

                //液态水鞘包衣：同一剪影放大一号、全血水态、独立慢晃——
                //枪泡在一层随时要垮的水膜里，这层才是"湖水凝成"的身份主张
                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed + i * 1.3f;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.7f, MathF.Cos(wt * 0.83f) * 2.1f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.035f;
                    float envScale = GunScale(i) * (1.14f + MathF.Sin(wt * 1.6f) * 0.025f);
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 2.3f + 5.1f);
                    form.Parameters["uForm"]?.SetValue(1f);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(tex, drawPos + wobOff, null,
                        new Color(255, 255, 255, (byte)(alpha * 130f)),
                        rot + wobRot, origin, envScale, GunFx(i), 0f);
                }

                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 2.3f);
                    form.Parameters["uForm"]?.SetValue(GunForm(i));
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    //无着色器回退：CPU 血染
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }

                sb.Draw(tex, drawPos, null, color,
                    rot, origin, GunScale(i), GunFx(i), 0f);
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
                for (int i = 0; i < gunCount; i++) {
                    Vector2 pos = new(BreachX(i), domain.LakeWorldY + MathHelper.Lerp(42f, 8f, ease));
                    float r = 20f + 14f * ease;
                    sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * (0.35f * ease), 0f,
                        gOrigin, new Vector2(r * 2.4f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //水光扫掠：湿面上一道窄亮痕周期滑过枪身（错帧），湖水质感的常驻记号
            if (State is StateFollow or StateVolley or StateCarousel) {
                for (int i = 0; i < gunCount; i++) {
                    float p = (Main.GlobalTimeWrappedHourly * 0.42f + i * 0.219f + Seed * 0.13f) % 1f;
                    if (p >= 0.34f || GunAlpha(i) <= 0.5f) {
                        continue;
                    }
                    EnsureBegin();
                    float k = p / 0.34f;
                    float a = MathF.Sin(k * MathHelper.Pi) * 0.3f * GunAlpha(i);
                    Vector2 dir = gunRot[i].ToRotationVector2();
                    float halfLen = Profile.MuzzleLen * 0.9f;
                    Vector2 pos = GunDrawPos(i) + dir * MathHelper.Lerp(-halfLen, halfLen, k);
                    //亮痕横跨枪身、顺枪滑行
                    sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * a,
                        gunRot[i] + MathHelper.PiOver2, gOrigin,
                        new Vector2(20f * 2f / glow.Width, 5f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //锁线预告：细水光线一路排到目标向，出膛前一闪
            float charge = VolleyLockCharge();
            float flash = VolleyTelegraphFlash();
            if (charge > 0.03f || flash > 0.02f) {
                EnsureBegin();
                for (int i = 0; i < gunCount; i++) {
                    if (GunAlpha(i) <= 0.1f) {
                        continue;
                    }
                    Vector2 muzzle = MuzzlePos(i);
                    float lineA = charge * (0.09f + 0.05f * MathF.Sin(Main.GlobalTimeWrappedHourly * 19f + Seed + i))
                        + flash * 0.4f;
                    if (lineA <= 0.02f) {
                        continue;
                    }
                    Vector2 dir = gunRot[i].ToRotationVector2();
                    const float lineLen = 420f;
                    const int lineSegs = 3;
                    for (int k = 0; k < lineSegs; k++) {
                        float f0 = k / (float)lineSegs;
                        Vector2 segMid = muzzle + dir * lineLen * (f0 + 0.5f / lineSegs);
                        float segLen = lineLen / lineSegs;
                        float fallA = lineA * (1f - f0 * 0.4f);
                        sb.Draw(glow, segMid - Main.screenPosition, null, MuzzleHot * fallA, gunRot[i],
                            gOrigin, new Vector2(segLen * 1.15f / glow.Width, 2.6f / glow.Height), SpriteEffects.None, 0f);
                    }
                    //镜筒积光
                    float r = 6f + 9f * MathF.Max(charge, flash);
                    sb.Draw(glow, muzzle - Main.screenPosition, null, MuzzleHot * (0.4f * MathF.Max(charge, flash)), 0f,
                        gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //枪口闪：出膛那一帧的水光爆点，沿射向拉伸
            for (int i = 0; i < gunCount; i++) {
                if (muzzleFlash[i] <= 0) {
                    continue;
                }
                EnsureBegin();
                float a = muzzleFlash[i] / 4f;
                Vector2 muzzle = MuzzlePos(i);
                sb.Draw(glow, muzzle - Main.screenPosition, null, MuzzleHot * (0.55f * a), gunRot[i],
                    gOrigin, new Vector2(30f / glow.Width * 2f, 10f / glow.Height), SpriteEffects.None, 0f);
                sb.Draw(glow, muzzle - Main.screenPosition, null, BloodBright * (0.35f * a), 0f,
                    gOrigin, new Vector2(14f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //交错闪拍：穿心交点的余光
            if (crossFlashTick > 0) {
                EnsureBegin();
                float a = crossFlashTick / 8f;
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null, BloodBright * (0.55f * a), 0f,
                    gOrigin, new Vector2(56f * a * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            //谢幕残珠：每把枪留一口水
            if (Main.dedServ || !gunsInit) {
                return;
            }
            for (int i = 0; i < gunCount; i++) {
                for (int k = 0; k < 4; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        gunPos[i] + Main.rand.NextVector2Circular(18f, 10f),
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
