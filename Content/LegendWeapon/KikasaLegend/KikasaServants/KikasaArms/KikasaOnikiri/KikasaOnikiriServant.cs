using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps;
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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaOnikiri
{
    /// <summary>
    /// 械奴·鬼切（专属条目，短路通用推断，鬼切 useStyle=Shoot 本也进不了刀奴档案）。
    /// 单柄无主之刀：湖水凝成的鬼切复制体，普攻是它生前的居合
    /// 蓄（收鞘锁线）→ 闪步穿越（两帧瞬移藏行程，神威流带承载路径）→
    /// 绯红斩痕在穿越线上炸开 → 停驻亮相，一轮三拍换三条刀路；
    /// 每第三轮起手大居合：连续三段闪步链斩不同猎物，末拍巨月牙终结。
    /// 强度读沉入原件的传奇等级（Summon 时 owner 本机烘焙，ExtraAI 随包补发），
    /// 联机契约与通用械奴同构：owner 裁决转场盖 netUpdate 章、斩痕仅 authority 生成、
    /// 生命线只有 owner 判、节拍闩防快照回卷
    /// </summary>
    internal class KikasaOnikiriServant : ModProjectile, IKikasaArmsServant, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>居合单斩伤害倍率（基伤=沉入原件的等级伤害）</summary>
        internal const float IaiDamageMul = 1.5f;

        /// <summary>大居合链斩单段倍率</summary>
        internal const float LinkDamageMul = 1.15f;

        /// <summary>终结月牙倍率</summary>
        internal const float FinisherDamageMul = 2.6f;

        //==================== 烘焙数值（owner 在 Summon 里定值，ExtraAI 随包同步）====================

        /// <summary>沉入原件的等级伤害（召唤加成前）；远端与服务器不读湖藏，只认这份烘焙</summary>
        private int baseDamage = 12;

        /// <summary>刀刃尺寸缩放（原件等级成长 0.70→1.2），斩痕半长与绘制共用</summary>
        private float bladeScaleLv = 0.7f;

        /// <summary>沉入原件的传奇等级（展示/微调用）</summary>
        private int legendLevel;

        /// <summary>沉影盘在场判定用：专属械奴恒复制鬼切</summary>
        public int ArmsItemType => OnikiriOverride.ID;

        /// <summary>专属单体：强度由原件等级烘焙，不吃编队摊薄</summary>
        public int UnitCount => 1;

        /// <summary>居合斩痕半长 px</summary>
        private float IaiHalfLen => 130f * bladeScaleLv + 58f;

        /// <summary>绘制缩放：82×230 大贴图收进械奴尺度，随等级放大</summary>
        private float DrawScaleBase => 0.5f * bladeScaleLv;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateIai = 2;
        private const int StateGrand = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：保位与通用械奴同构，当前未用</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：单点预兆→立剑破水→翻转落定→出鞘鸣
        private const int OmenFrames = 26;
        private const int RiseEnd = 58;
        private const int SheatheFrame = 62;
        private const int EmergeTotal = 78;

        //居合猎杀：引拍后三拍接力，每拍 蓄-两帧穿越-停驻
        private const int IaiLead = 10;
        private const int IaiGather = 13;
        private const int IaiRest = 13;
        private const int IaiBeatLen = IaiGather + 2 + IaiRest;
        private const int IaiBeats = 3;
        private const int IaiTotal = IaiLead + IaiBeatLen * IaiBeats + 6;

        //大居合：三段链斩接终结月牙
        private const int GrandLead = 12;
        private const int LinkGather = 8;
        private const int LinkRest = 6;
        private const int LinkLen = LinkGather + 2 + LinkRest;
        private const int GrandLinks = 3;
        private const int FinStart = GrandLead + LinkLen * GrandLinks;
        private const int FinGather = 14;
        private const int FinRest = 22;
        private const int GrandTotal = FinStart + FinGather + 2 + FinRest + 6;

        //溶解：失力坠湖
        private const int DissolveFrames = 70;

        //==================== 刀体本地模拟（各端自算，质心同步纠偏）====================

        private Vector2 bladePos;
        private Vector2 bladeVel;
        private Vector2 bladeTarget;
        /// <summary>刀尖指向角（护手支点绘制按贴图轴修正）</summary>
        private float bladeRot;
        private float bladeSpin;
        //当前拍冲线：声明后蓄/爆/斩痕共用同一条线，先声明后砍
        private Vector2 dashFrom;
        private Vector2 dashTo;
        private float dashAng;
        /// <summary>斩痕锚点（居合=冲线中点，链斩=猎物身上）</summary>
        private Vector2 slashMid;
        private readonly Vector2[] bladeOld = new Vector2[10];
        private readonly float[] bladeOldRot = new float[10];
        private bool bladeInit;

        //==================== 神威流带：每次闪步录一条路径，客户端各自演 ====================

        private const int RibbonLife = 26;
        private const int MaxRibbons = 5;
        private readonly Vector2[][] ribbonPts = new Vector2[MaxRibbons][];
        private readonly int[] ribbonAge = new int[MaxRibbons];
        private int ribbonCursor;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private bool breachDone;
        /// <summary>逐拍声明闩（居合三拍 / 大居合三链+终结）</summary>
        private readonly bool[] beatDeclared = new bool[GrandLinks + 1];
        /// <summary>逐拍出刀闩</summary>
        private readonly bool[] beatSlashed = new bool[GrandLinks + 1];
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;
        private bool sheatheSnapDone;
        private bool dissolveSplashed;
        //大居合的猎物名单：声明帧就地圈定（各端本地圈，斩痕仍由 owner 权威）
        private readonly int[] grandTargets = new int[GrandLinks];
        private int grandTargetCount;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.6173f;

        //==================== 召唤入口 ====================

        /// <summary>
        /// KikasaArmsIndex 专门条目的召唤入口；count 不折算编制，传奇武器沉一件即完整形态，
        /// 多件只取最高等级件定强度
        /// </summary>
        internal static void Summon(Player owner, Vector2 emergeAt, int count) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            //湖藏里最高等级的鬼切原件：伤害与刀刃尺寸的依据（湖藏数据本机私有，烘焙后随包同步）
            Item best = null;
            int bestLv = -1;
            foreach (Item item in owner.GetModPlayer<KikasaVaultPlayer>().Stored) {
                if (item?.IsAir == false && item.type == OnikiriOverride.ID) {
                    int lv = OnikiriOverride.GetLevel(item);
                    if (lv > bestLv) {
                        bestLv = lv;
                        best = item;
                    }
                }
            }
            int baseDmg = best != null ? OnikiriOverride.GetOnDamage(best) : OnikiriOverride.GetStartDamage;
            float scale = best != null ? OnikiriOverride.GetBladeScale(best) : 0.7f;
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDmg * IaiDamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaOnikiriServant>(), damage, 3f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaOnikiriServant blade) {
                blade.baseDamage = baseDmg;
                blade.bladeScaleLv = scale;
                blade.legendLevel = Math.Max(bestLv, 0);
                Main.projectile[index].netUpdate = true;
            }
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //闪步链斩的刀与流带散布远超质心 hitbox，出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1200;
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

        /// <summary>刀体不做接触判定，伤害全在绯红斩痕上</summary>
        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(baseDamage);
            writer.Write(bladeScaleLv);
            writer.Write((byte)Math.Clamp(legendLevel, 0, 255));
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int dmg = reader.ReadInt32();
            float scale = reader.ReadSingle();
            int lv = reader.ReadByte();
            if (dmg > 0) {
                baseDamage = dmg;
            }
            if (scale > 0.1f && scale < 4f) {
                bladeScaleLv = scale;
            }
            legendLevel = lv;
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
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * IaiDamageMul);

            //换场清闩：远端可能靠收包换场而非本地同拍转场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                Array.Fill(beatDeclared, false);
                Array.Fill(beatSlashed, false);
                grandTargetCount = 0;
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
                case StateIai: UpdateIai(owner, authority); break;
                case StateGrand: UpdateGrand(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateBlade(owner, domain);
            PushBladeHistory();
            UpdateRibbons();
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            float glow = BladeAlpha() * 0.4f;
            if (glow > 0.02f) {
                Lighting.AddLight(bladePos, 0.5f * glow, 0.09f * glow, 0.08f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：立剑破水、出鞘鸣 ====================

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
                bladeVel = new Vector2(0f, -12.4f);
                bladeSpin = 0.2f;
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

            //出鞘鸣：一顿、一声幽冷刃鸣，它醒了
            if (!sheatheSnapDone && t >= SheatheFrame) {
                sheatheSnapDone = true;
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.42f, Pitch = -0.2f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(CWRSound.KatanaSwing with { Volume = 0.3f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
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

        /// <summary>破水浪冠：单刀规格，比刃群同款郑重一号</summary>
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

        //==================== 跟随：鞘姿悬浮 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            //质心锚：悬在主人肩后上方，随呼吸轻沉浮
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 36f, -46f);
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

            //出手裁决：居合为常、每第三轮大居合；转场规则各端一致，owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                StateTimer = 0;
                StateParam = 0;
                State = attackIndex % 3 == 0 ? StateGrand : StateIai;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 居合猎杀：蓄-闪步穿越-斩痕-停驻 ====================

        /// <summary>当前处在第几拍；引拍期与收尾返回 -1</summary>
        private static int IaiBeatAt(int t) {
            if (t < IaiLead) {
                return -1;
            }
            int beat = (t - IaiLead) / IaiBeatLen;
            return beat < IaiBeats ? beat : -1;
        }

        /// <summary>拍内本地相位 0..IaiBeatLen-1</summary>
        private static int IaiPhase(int t, int beat) => t - IaiLead - beat * IaiBeatLen;

        /// <summary>当拍冲线声明：锁定目标提前量，三拍换三条刀路（斜切/反手/横薙）</summary>
        private void DeclareIaiBeat(int beat, Player owner, int target) {
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * (IaiGather + 2)
                : Projectile.Center + new Vector2(owner.direction * 320f, 0f);
            //确定性刀路偏角：拍序定基调，轮次抖变化
            float baseSkew = beat switch {
                0 => -0.42f,
                1 => 0.55f,
                _ => -0.14f,
            };
            float jitter = MathF.Sin(Seed * 3.1f + attackIndex * 1.71f + beat * 2.39f) * 0.24f;
            float ang = (focus - bladePos).ToRotation() + baseSkew + jitter;
            Vector2 dir = ang.ToRotationVector2();
            float reach = IaiHalfLen + 84f;
            dashFrom = focus - dir * reach;
            dashTo = focus + dir * reach * 0.88f;
            dashAng = ang;
            slashMid = focus;
        }

        private void UpdateIai(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= IaiLead) {
                EndAttack(authority, 50);
                return;
            }

            //质心压到目标侧近位：闪步的观众席
            Vector2 focus = target >= 0 ? Main.npc[target].Center : Projectile.Center;
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 anchor = owner.Center + toT * 96f + new Vector2(0f, -30f);
            Vector2 desired = (anchor - Projectile.Center) * 0.1f;
            if (desired.Length() > 15f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 15f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.16f);

            int beat = IaiBeatAt(t);
            if (beat >= 0) {
                int p = IaiPhase(t, beat);
                if (!beatDeclared[beat]) {
                    //蓄势起点锁线（跳帧进窗也补上）；轻声引拍，刀要来了
                    beatDeclared[beat] = true;
                    DeclareIaiBeat(beat, owner, target);
                    SoundEngine.PlaySound(SoundID.Unlock with {
                        Volume = 0.3f,
                        Pitch = -0.5f + beat * 0.08f,
                        MaxInstances = 3
                    }, bladePos);
                }
                //爆发帧起放行一次：节拍闩防快照回卷重砍，跳帧迟到也补砍
                if (p >= IaiGather && !beatSlashed[beat]) {
                    beatSlashed[beat] = true;
                    FlashStepStrike(owner, authority, beat, heavy: false, IaiDamageMul, IaiHalfLen);
                }
            }

            if (t >= IaiTotal) {
                EndAttack(authority, 92);
            }
        }

        //==================== 大居合：链斩三段接终结月牙 ====================

        /// <summary>圈定至多三个猎物（按离主人的距离近序）；不足就复用最近者换刀路</summary>
        private void FillGrandTargets(Player owner) {
            grandTargetCount = 0;
            for (int n = 0; n < GrandLinks; n++) {
                int best = -1;
                float bestDist = 1150f;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                        continue;
                    }
                    bool taken = false;
                    for (int k = 0; k < grandTargetCount; k++) {
                        if (grandTargets[k] == i) {
                            taken = true;
                            break;
                        }
                    }
                    if (taken) {
                        continue;
                    }
                    float dist = Vector2.Distance(npc.Center, owner.Center);
                    if (dist < bestDist) {
                        bestDist = dist;
                        best = i;
                    }
                }
                if (best < 0) {
                    break;
                }
                grandTargets[grandTargetCount++] = best;
            }
        }

        /// <summary>链斩声明：从刀当前位置穿过第 j 个猎物；猎物不足时旋角换刀路复斩最近者</summary>
        private void DeclareGrandLink(int j, Player owner) {
            int npcIndex = grandTargetCount > 0 ? grandTargets[Math.Min(j, grandTargetCount - 1)] : -1;
            Vector2 focus;
            if (npcIndex >= 0 && Main.npc[npcIndex].active) {
                focus = Main.npc[npcIndex].Center + Main.npc[npcIndex].velocity * (LinkGather + 2);
            }
            else {
                focus = Projectile.Center + (Seed * 2.3f + j * 2.1f).ToRotationVector2() * 260f;
            }
            Vector2 dir = (focus - bladePos).SafeNormalize(Vector2.UnitX);
            if (j > 0 && grandTargetCount <= 1) {
                //同一个猎物：换个角度再穿一次
                dir = dir.RotatedBy((j % 2 == 0 ? 1f : -1f) * 0.85f);
            }
            dashFrom = bladePos;
            dashTo = focus + dir * 96f;
            dashAng = dir.ToRotation();
            slashMid = focus;
        }

        /// <summary>终结声明：穿过主猎物的巨月牙冲线</summary>
        private void DeclareFinisher(Player owner, int target) {
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * (FinGather + 2)
                : Projectile.Center + new Vector2(owner.direction * 300f, 0f);
            Vector2 dir = (focus - bladePos).SafeNormalize(Vector2.UnitX);
            float reach = IaiHalfLen * 1.45f + 90f;
            dashFrom = bladePos;
            dashTo = focus + dir * reach * 0.7f;
            dashAng = dir.ToRotation();
            slashMid = focus;
        }

        private void UpdateGrand(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= GrandLead) {
                EndAttack(authority, 60);
                return;
            }

            //起手圈猎物 + 举刀蓄势拍
            if (t == 1) {
                FillGrandTargets(owner);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = -0.3f, MaxInstances = 2 }, bladePos);
            }

            //质心跟着刀链游走：链斩期贴着最新斩点，终结期压向主猎物
            Vector2 focus = target >= 0 ? Main.npc[target].Center : Projectile.Center;
            Vector2 want = ((t < FinStart ? slashMid : focus) - Projectile.Center) * 0.12f;
            if (want.Length() > 19f) {
                want = want.SafeNormalize(Vector2.Zero) * 19f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.2f);

            //三段链斩
            if (t >= GrandLead && t < FinStart) {
                int j = Math.Min((t - GrandLead) / LinkLen, GrandLinks - 1);
                int p = t - GrandLead - j * LinkLen;
                if (!beatDeclared[j]) {
                    beatDeclared[j] = true;
                    DeclareGrandLink(j, owner);
                }
                if (p >= LinkGather && !beatSlashed[j]) {
                    beatSlashed[j] = true;
                    FlashStepStrike(owner, authority, j, heavy: false, LinkDamageMul, IaiHalfLen * 0.82f);
                }
            }
            //终结月牙
            else if (t >= FinStart) {
                int p = t - FinStart;
                if (!beatDeclared[GrandLinks]) {
                    beatDeclared[GrandLinks] = true;
                    DeclareFinisher(owner, target);
                    //静谷上膛：终结前最后一声轻响
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 2 }, bladePos);
                }
                if (p >= FinGather && !beatSlashed[GrandLinks]) {
                    beatSlashed[GrandLinks] = true;
                    FlashStepStrike(owner, authority, GrandLinks, heavy: true, FinisherDamageMul, IaiHalfLen * 1.45f);
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 2 }, slashMid);
                }
            }

            if (t >= GrandTotal) {
                EndAttack(authority, 160);
            }
        }

        /// <summary>
        /// 闪步出刀：录神威流带、主挥砍音在爆发帧落拍，owner 端在斩痕锚点生成绯红斩痕
        /// （生成包自含冲线角），刀体自己的两帧穿越在 UpdateBlade 里走
        /// </summary>
        private void FlashStepStrike(Player owner, bool authority, int beat, bool heavy, float damageMul, float halfLen) {
            Vector2 dir = dashAng.ToRotationVector2();

            //神威流带：路径微弓，弓向逐拍交替
            RecordDashRibbon(dashFrom, dashTo, beat % 2 == 0 ? 1f : -1f);

            SoundEngine.PlaySound(CWRSound.KatanaSwing with {
                Volume = heavy ? 0.66f : 0.52f,
                Pitch = (heavy ? -0.22f : 0.02f) + beat * 0.04f,
                MaxInstances = 4
            }, slashMid);
            SoundEngine.PlaySound(CWRSound.KatanaSprint with {
                Volume = heavy ? 0.44f : 0.32f,
                Pitch = 0.1f,
                MaxInstances = 3
            }, dashFrom);
            if (ViewedOwner) {
                ShakeViewer(heavy ? 3.2f : 1.6f);
            }

            //斩痕只在 owner 端生成，spawn 包自带冲线（ai0=判定半长，ai1=终结月牙）
            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                    .ApplyTo(baseDamage * damageMul);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), slashMid, dir * 3.4f,
                    ModContent.ProjectileType<KikasaOnikiriSlash>(), damage, 3f, Projectile.owner,
                    halfLen, heavy ? 1f : 0f);
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

        //==================== 刀体推进 ====================

        /// <summary>初始化或硬纠时按当前状态直接落位，防刀体与残影抽搐</summary>
        private void RebuildBlade(KikasaDomainPlayer domain) {
            bladeInit = true;
            if (State == StateEmerge) {
                bladePos = new Vector2(Projectile.Center.X, domain.LakeWorldY + 28f);
                bladeRot = -MathHelper.PiOver2;
            }
            else {
                bladePos = Projectile.Center + new Vector2(0f, -8f);
                bladeRot = SheathedRot();
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

        /// <summary>鞘姿基准角：刀尖微垂，随呼吸轻晃</summary>
        private float SheathedRot()
            => MathHelper.PiOver2 * 0.82f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.4f + Seed) * 0.12f;

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
                    bladeTarget = new Vector2(Projectile.Center.X, lakeY - 96f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + Seed) * 8f);
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
                            //翻转散尽后落定：刀尖微垂的收鞘姿
                            bladeRot = bladeRot.AngleLerp(SheathedRot(), 0.12f);
                        }
                    }
                    break;
                }
                case StateFollow: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    float phase = tGlobal * 0.7f + Seed;
                    Vector2 slot = Projectile.Center + new Vector2(MathF.Cos(phase) * 24f, MathF.Sin(phase * 1.3f) * 10f - 8f);
                    bladeTarget = slot;
                    ChaseBlade(0.06f, 0.84f);

                    //收鞘姿慢游：错帧偶发一次挽花（静里的一点活）
                    float flourishT = t % 300;
                    if (flourishT < 36f) {
                        bladeRot += MathF.Sin(flourishT / 36f * MathHelper.Pi) * 0.14f;
                    }
                    else {
                        bladeRot = bladeRot.AngleLerp(SheathedRot(), 0.08f);
                    }
                    break;
                }
                case StateIai: {
                    int beat = IaiBeatAt(t);
                    if (beat < 0 || !beatDeclared[beat]) {
                        //引拍与收尾：候在质心边，刀尖渐醒（微抬）
                        bladeTarget = Projectile.Center + new Vector2(0f, -10f);
                        ChaseBlade(0.08f, 0.82f);
                        bladeRot = bladeRot.AngleLerp(SheathedRot() - 0.35f, 0.1f);
                        break;
                    }
                    int p = IaiPhase(t, beat);
                    Vector2 dir = dashAng.ToRotationVector2();
                    if (p < IaiGather) {
                        //蓄：拉到冲线后端收鞘减速近停，只留呼吸颤
                        float ease = p / (float)IaiGather;
                        Vector2 cock = dashFrom - dir * (18f + 12f * ease);
                        cock += dir.RotatedBy(MathHelper.PiOver2) * MathF.Sin(Seed + beat) * 9f;
                        bladeTarget = cock;
                        ChaseBlade(0.22f, MathHelper.Lerp(0.7f, 0.42f, ease));
                        //刃尖回指收鞘（拔刀术的反向），末段颤 0.02 rad
                        float cockRot = dashAng + MathHelper.Pi * 0.88f * (beat % 2 == 0 ? 1f : -1f);
                        bladeRot = bladeRot.AngleLerp(cockRot, 0.3f);
                        if (ease > 0.6f) {
                            bladeRot += MathF.Sin(t * 1.7f + beat) * 0.02f;
                        }
                    }
                    else if (p == IaiGather || p == IaiGather + 1) {
                        //闪步穿越：两帧瞬移全程，刀体让位给流带与拖影（藏行程）
                        skipFix = true;
                        Vector2 snapPos = p == IaiGather ? (dashFrom + dashTo) * 0.5f : dashTo;
                        bladeVel = snapPos - bladePos;
                        bladePos = snapPos;
                        bladeTarget = snapPos;
                        bladeRot = dashAng;
                    }
                    else {
                        //停驻：过冲点硬停亮相，几何冻住，静谷本身就是下一拍的蓄势
                        bladeVel *= 0.6f;
                        bladePos += bladeVel;
                        bladeTarget = bladePos;
                        bladeRot = dashAng;
                    }
                    break;
                }
                case StateGrand: {
                    if (t < GrandLead) {
                        //起手：刀举过质心上方，蓄势微颤
                        bladeTarget = Projectile.Center + new Vector2(0f, -34f);
                        ChaseBlade(0.12f, 0.78f);
                        bladeRot = bladeRot.AngleLerp(-MathHelper.PiOver2, 0.16f);
                        if (t > GrandLead / 2) {
                            bladeRot += MathF.Sin(t * 1.9f) * 0.018f;
                        }
                        break;
                    }
                    if (t < FinStart) {
                        int j = Math.Min((t - GrandLead) / LinkLen, GrandLinks - 1);
                        int p = t - GrandLead - j * LinkLen;
                        if (!beatDeclared[j] || p < LinkGather) {
                            //短蓄：原地收鞘压住（链斩从当前位置出发，不回拉）
                            bladeTarget = dashFrom;
                            ChaseBlade(0.2f, 0.5f);
                            float cockRot = dashAng + MathHelper.Pi * 0.82f * (j % 2 == 0 ? 1f : -1f);
                            bladeRot = bladeRot.AngleLerp(cockRot, 0.34f);
                        }
                        else if (p == LinkGather || p == LinkGather + 1) {
                            skipFix = true;
                            Vector2 snapPos = p == LinkGather ? Vector2.Lerp(dashFrom, dashTo, 0.55f) : dashTo;
                            bladeVel = snapPos - bladePos;
                            bladePos = snapPos;
                            bladeTarget = snapPos;
                            bladeRot = dashAng;
                        }
                        else {
                            bladeVel *= 0.55f;
                            bladePos += bladeVel;
                            bladeTarget = bladePos;
                            bladeRot = dashAng;
                        }
                        break;
                    }
                    //终结段
                    {
                        int p = t - FinStart;
                        Vector2 dir = dashAng.ToRotationVector2();
                        if (!beatDeclared[GrandLinks] || p < FinGather) {
                            //长蓄：深收鞘，刀身压平贴向冲线后端
                            float ease = MathHelper.Clamp(p / (float)FinGather, 0f, 1f);
                            bladeTarget = dashFrom - dir * (24f + 14f * ease);
                            ChaseBlade(0.2f, MathHelper.Lerp(0.66f, 0.4f, ease));
                            float cockRot = dashAng + MathHelper.Pi * 0.92f;
                            bladeRot = bladeRot.AngleLerp(cockRot, 0.26f);
                            if (ease > 0.55f) {
                                bladeRot += MathF.Sin(t * 2.1f) * 0.024f;
                            }
                        }
                        else if (p == FinGather || p == FinGather + 1) {
                            skipFix = true;
                            Vector2 snapPos = p == FinGather ? Vector2.Lerp(dashFrom, dashTo, 0.6f) : dashTo;
                            bladeVel = snapPos - bladePos;
                            bladePos = snapPos;
                            bladeTarget = snapPos;
                            bladeRot = dashAng;
                        }
                        else {
                            //终结停驻：更久的亮相
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
                    //刀尖垂下去，沉
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

        //==================== 神威流带记录与老化 ====================

        /// <summary>录一条闪步路径：直线微弓成弧，供流带渲染（各端在自己的爆发帧各录各的）</summary>
        private void RecordDashRibbon(Vector2 from, Vector2 to, float bowSign) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = to - from;
            Vector2 perp = dir.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float bow = dir.Length() * 0.07f * bowSign;
            Vector2[] pts = new Vector2[7];
            for (int k = 0; k < pts.Length; k++) {
                float u = k / (pts.Length - 1f);
                pts[k] = Vector2.Lerp(from, to, u) + perp * (MathF.Sin(u * MathHelper.Pi) * bow);
            }
            ribbonPts[ribbonCursor] = pts;
            ribbonAge[ribbonCursor] = 0;
            ribbonCursor = (ribbonCursor + 1) % MaxRibbons;
        }

        private void UpdateRibbons() {
            for (int i = 0; i < MaxRibbons; i++) {
                if (ribbonPts[i] == null) {
                    continue;
                }
                if (++ribbonAge[i] > RibbonLife) {
                    ribbonPts[i] = null;
                }
            }
        }

        /// <summary>常驻氛围：液态下缘偶发凝珠滴落，刀一直在往下滴湖水</summary>
        private void UpdateAmbient() {
            if (Main.dedServ || State is not (StateFollow or StateIai or StateGrand)) {
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
            float bestDist = 980f;
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

        /// <summary>该刀正处在两帧闪步穿越里（居合/链斩/终结）</summary>
        private bool IsBursting() {
            int t = (int)StateTimer;
            if (State == StateIai) {
                int beat = IaiBeatAt(t);
                if (beat < 0) {
                    return false;
                }
                int p = IaiPhase(t, beat);
                return p == IaiGather || p == IaiGather + 1;
            }
            if (State == StateGrand) {
                if (t >= GrandLead && t < FinStart) {
                    int j = Math.Min((t - GrandLead) / LinkLen, GrandLinks - 1);
                    int p = t - GrandLead - j * LinkLen;
                    return p == LinkGather || p == LinkGather + 1;
                }
                if (t >= FinStart) {
                    int p = t - FinStart;
                    return p == FinGather || p == FinGather + 1;
                }
            }
            return false;
        }

        private float BladeAlpha() {
            int t = (int)StateTimer;
            float alpha = State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
            //闪步两帧刀体让位给流带（藏行程）：本体压暗
            if (IsBursting()) {
                alpha *= 0.2f;
            }
            return alpha;
        }

        /// <summary>uForm 水线呼吸：同通用械奴，实体上半 + 液态下缘，出水凝出、溶解漫上来</summary>
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

        private float BladeDrawScale() {
            float scale = DrawScaleBase;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 10) {
                //破水过冲
                scale *= 1f + 0.08f * (1f - (t - OmenFrames) / 10f);
            }
            return scale;
        }

        /// <summary>蓄势进度 0~1：收鞘末段亮刃口</summary>
        private float GatherCharge() {
            int t = (int)StateTimer;
            if (State == StateIai) {
                int beat = IaiBeatAt(t);
                if (beat < 0 || !beatDeclared[beat]) {
                    return 0f;
                }
                int p = IaiPhase(t, beat);
                if (p >= IaiGather) {
                    return 0f;
                }
                return MathHelper.Clamp((p - IaiGather * 0.4f) / (IaiGather * 0.6f), 0f, 1f);
            }
            if (State == StateGrand && (int)StateTimer >= FinStart) {
                int p = t - FinStart;
                if (p >= FinGather) {
                    return 0f;
                }
                return MathHelper.Clamp(p / (float)FinGather, 0f, 1f);
            }
            return 0f;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        //鬼切贴图的护手/刀尖 UV（与 OniBladePose 同源约定）：护手支点，刀尖严格指向 bladeRot
        private static readonly Vector2 HiltUV = new(0.1f, 1f);
        private static readonly Vector2 TipUV = new(0.73f, 0.01f);

        /// <summary>贴图轴修正：护手→刀尖在贴图空间的基准角（不镜像，械奴同刃群约定）</summary>
        private static float TextureAxis(Texture2D tex) {
            Vector2 size = tex.Size();
            Vector2 origin = new(size.X * HiltUV.X, size.Y * HiltUV.Y);
            Vector2 tip = new(size.X * TipUV.X, size.Y * TipUV.Y);
            return (tip - origin).ToRotation();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!bladeInit) {
                return false;
            }
            Main.instance.LoadItem(OnikiriOverride.ID);
            Texture2D tex = TextureAssets.Item[OnikiriOverride.ID]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            //穿越拖影：闪步两帧的路径由残影承载（藏行程的另一半，流带在图元层垫底）
            DrawDashTrails(sb, tex);

            //刀体本体：血湖材质
            DrawBody(sb, tex);

            //加色层：预兆水光 / 蓄势刃口冷光
            DrawGlow(sb);

            return false;
        }

        private void DrawDashTrails(SpriteBatch sb, Texture2D tex) {
            float trailA = MathHelper.Clamp((bladeVel.Length() - 8f) / 12f, 0f, 1f);
            if (State == StateEmerge || State == StateDissolve) {
                trailA *= BladeAlpha();
            }
            if (trailA <= 0.03f) {
                return;
            }
            float axis = TextureAxis(tex);
            Vector2 origin = new(tex.Width * HiltUV.X, tex.Height * HiltUV.Y);
            for (int k = bladeOld.Length - 1; k >= 1; k--) {
                float fall = 1f - k / (float)bladeOld.Length;
                sb.Draw(tex, bladeOld[k] - Main.screenPosition, null,
                    BloodMain * (0.3f * fall * trailA), bladeOldRot[k] - axis,
                    origin, BladeDrawScale() * (0.97f - k * 0.012f), SpriteEffects.None, 0f);
            }
        }

        private void DrawBody(SpriteBatch sb, Texture2D tex) {
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
                float axis = TextureAxis(tex);
                Vector2 origin = new(tex.Width * HiltUV.X, tex.Height * HiltUV.Y);
                float rot = bladeRot - axis;
                Vector2 drawPos = bladePos - Main.screenPosition;
                float dissolve = DissolveAmt();

                //液态水鞘包衣：同一剪影放大一号、全血水态、独立慢晃
                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.6f, MathF.Cos(wt * 0.83f) * 2f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.03f;
                    float envScale = BladeDrawScale() * (1.12f + MathF.Sin(wt * 1.6f) * 0.025f);
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

                sb.Draw(tex, drawPos, null, color, rot, origin, BladeDrawScale(), SpriteEffects.None, 0f);
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

            //蓄势刃口冷光：收鞘末段沿冲线一道渐亮的窄光，刀要来了
            float charge = GatherCharge();
            if (charge > 0.05f) {
                EnsureBegin();
                Vector2 dir = dashAng.ToRotationVector2();
                float len = IaiHalfLen * 0.55f;
                Vector2 pos = bladePos + dir * len * 0.3f;
                //绯红侧的蓄势光：暖白芯 + 绯红缘（区别于血湖冷调，宣告这刀是鬼切的）
                sb.Draw(glow, pos - Main.screenPosition, null,
                    new Color(255, 176, 130) * (0.42f * charge), dashAng,
                    gOrigin, new Vector2(len * 1.7f / glow.Width, 3.2f / glow.Height), SpriteEffects.None, 0f);
                sb.Draw(glow, bladePos - Main.screenPosition, null,
                    new Color(255, 64, 44) * (0.26f * charge), 0f,
                    gOrigin, new Vector2(17f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        /// <summary>神威流带：闪步路径的绯红余焰，图元层垫在刀光之下</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ) {
                return;
            }
            bool anyAlive = false;
            for (int i = 0; i < MaxRibbons; i++) {
                if (ribbonPts[i] != null) {
                    anyAlive = true;
                    break;
                }
            }
            if (!anyAlive) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OniKamuiFlowRenderer.BeginDraw(device, out Effect fx,
                out BlendState pb, out RasterizerState pr, out DepthStencilState pd)) {
                return;
            }
            for (int i = 0; i < MaxRibbons; i++) {
                Vector2[] pts = ribbonPts[i];
                if (pts == null) {
                    continue;
                }
                float ageT = ribbonAge[i] / (float)RibbonLife;
                float opacity = (1f - ageT) * (1f - ageT);
                float retract = MathF.Pow(ageT, 1.25f) * 0.9f;
                float flash = ribbonAge[i] <= 2 ? 1f - ribbonAge[i] / 3f : 0f;
                float seed = Seed + i * 2.7f;

                //三股子带：主带 + 两条错位细带（层间视差）
                OniKamuiFlowRenderer.DrawRibbon(device, fx, pts, new OniKamuiFlowRenderer.RibbonDef {
                    HalfWidth = 11f,
                    PerpOffset = 0f,
                    Seed = seed,
                    FlowMul = 1f,
                    TearAmp = 0.4f,
                    HeadBoost = 1f,
                    OpacityMul = 1f,
                }, retract, flash, opacity);
                OniKamuiFlowRenderer.DrawRibbon(device, fx, pts, new OniKamuiFlowRenderer.RibbonDef {
                    HalfWidth = 6.5f,
                    PerpOffset = 7f,
                    Seed = seed + 1.3f,
                    FlowMul = 1.6f,
                    TearAmp = 0.7f,
                    HeadBoost = 0.5f,
                    OpacityMul = 0.7f,
                }, retract, flash * 0.6f, opacity);
                OniKamuiFlowRenderer.DrawRibbon(device, fx, pts, new OniKamuiFlowRenderer.RibbonDef {
                    HalfWidth = 6.5f,
                    PerpOffset = -7f,
                    Seed = seed + 2.6f,
                    FlowMul = 1.3f,
                    TearAmp = 0.7f,
                    HeadBoost = 0.5f,
                    OpacityMul = 0.7f,
                }, retract, flash * 0.6f, opacity);
            }
            OniKamuiFlowRenderer.EndDraw(device, pb, pr, pd);
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            //谢幕残珠：留一口水
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
