using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.OniRainWorlds.KasaOnis;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls
{
    /// <summary>
    /// 伞奴本体：鬼雨领域击杀转化而来的打伞随从。
    /// 聚拢（水团自尸点流向重组点）→ 成形（污水自下而上凝聚，伞面最后析出）→
    /// 作战（贴地蹒跚，突进冲撞与伞旋雨溅交替）→ 溶解（化回污水）。
    /// 状态机各端同推（规则确定性），owner 在转场盖 netUpdate 章纠偏；
    /// 生命线只由 owner 裁决——服务器没有领域状态是既定契约。
    /// 演出节拍全部走本地闩，快照回卷不重播。
    /// </summary>
    internal class KikasaThrallProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int HitboxWidth = 36;
        internal const int HitboxHeight = 54;

        //==================== 可调基数（占位初值，验收再调） ====================

        /// <summary>伞旋雨溅单滴伤害占本体伤害比例</summary>
        private const float DropletDamageRatio = 0.55f;

        private const float WalkMaxSpeed = 1.55f;
        private const float ChaseMaxSpeed = 2.3f;
        private const float WalkAccel = 0.06f;
        private const float Gravity = 0.35f;
        private const float MaxFallSpeed = 10f;

        //==================== 状态 ====================

        private const int StateGather = 0;
        private const int StateReform = 1;
        private const int StateActive = 2;
        private const int StateDissolve = 3;

        //作战子状态
        private const int SubWalk = 0;
        private const int SubLunge = 1;
        private const int SubSpin = 2;

        //==================== 时序 ====================

        //聚拢：滞蓄（尸点吸融水）→ 流动（滑向重组点）
        private const int GatherPoolEnd = 42;
        private const int GatherTotal = 84;

        //成形：凝聚 46f + 实质化落定 10f
        private const int ReformCondenseEnd = 46;
        private const int ReformTotal = 56;

        //突进：蓄势→跃扑（接触窗）→落地收势
        private const int LungeWindup = 14;
        private const int LungeActiveEnd = 34;
        private const int LungeRecoverEnd = 52;

        //伞旋：驻步→旋伞甩滴→收伞
        private const int SpinBrakeEnd = 8;
        private const int SpinFlingEnd = 34;
        private const int SpinTotal = 46;

        private const int DissolveFrames = 46;

        //==================== 同步字段（ExtraAI 载荷；初值即 spawn 语义，防 2.7） ====================

        private int state = StateGather;
        private int stateTimer;
        private int subState = SubWalk;
        /// <summary>尸体折算基伤，owner 生成后补包；远端不结算命中只作展示</summary>
        private int baseDamage;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播） ====================

        private int lastSeenState = -1;
        private int lastSeenSubState = -1;
        private int subTimer;
        private int attackCooldown;
        private int spinCooldown;
        private int lastFlungDrop = -1;
        private bool gatherFromSet;
        private Vector2 gatherFrom;
        private bool travelBeatDone;
        private bool materializeBeatDone;
        private bool dissolveBeatDone;
        private bool facingLeft;
        private float waddlePhase;
        private int dripTimer;
        private int squelchTimer;

        private Player Owner => Main.player[Projectile.owner];

        private int State { get => state; set => state = value; }
        private ref int StateTimer => ref stateTimer;

        /// <summary>重组点脚底（spawn 包自带）</summary>
        private Vector2 ReformFeet => new(Projectile.ai[0], Projectile.ai[1]);

        /// <summary>体型缩放（spawn 包自带）</summary>
        private float BodyScale => MathHelper.Clamp(Projectile.ai[2] <= 0.01f ? 1f : Projectile.ai[2],
            KikasaThrall.BodyScaleMin, KikasaThrall.BodyScaleMax);

        /// <summary>脚底中心锚点</summary>
        private Vector2 FeetAnchor => new(Projectile.Center.X, Projectile.position.Y + Projectile.height);

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand 定行为）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        /// <summary>个体站位距离：按 identity 错开，多只跟随呈弧散开</summary>
        private float PersonalStop => 52f + Projectile.identity * 29 % 72;

        /// <summary>凝聚度 0~1：Reform 升、Dissolve 降、Active=1、Gather=0</summary>
        private float CondenseProgress => State switch {
            StateReform => MathHelper.Clamp(StateTimer / (float)ReformCondenseEnd, 0f, 1f),
            StateDissolve => 1f - MathHelper.Clamp(StateTimer / (float)DissolveFrames, 0f, 1f),
            StateActive => 1f,
            _ => 0f,
        };

        //==================== 外部接口 ====================

        /// <summary>owner 生成后补基伤（字段错过 spawn 包，跟发 netUpdate）；调试可跳过聚拢</summary>
        internal void SetCorpseStats(int damage, bool skipGather = false) {
            baseDamage = damage;
            if (skipGather) {
                state = StateReform;
            }
            Projectile.netUpdate = true;
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = HitboxWidth;
            Projectile.height = HitboxHeight;
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

        /// <summary>接触伤害只开在跃扑的活跃窗，与可见的扑击严格对齐</summary>
        public override bool? CanDamage() {
            if (State != StateActive || subState != SubLunge) {
                return false;
            }
            return subTimer > LungeWindup && subTimer <= LungeActiveEnd ? null : false;
        }

        public override bool? CanCutTiles() => false;

        //==================== 网络 ====================

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)state);
            writer.Write((ushort)Math.Clamp(stateTimer, 0, ushort.MaxValue));
            writer.Write((byte)subState);
            writer.Write(baseDamage);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            state = reader.ReadByte();
            stateTimer = reader.ReadUInt16();
            subState = reader.ReadByte();
            baseDamage = reader.ReadInt32();
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

            //生命线：收域/退水/翻回血湖/入梦/主人死亡 → 溶解回水。只有 owner 裁决——
            //服务器没有领域状态（恒 Closed 是既定契约），迟入场客户端在首份快照前也会误判
            if (authority && State != StateDissolve && !RainHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            //伤害逐帧随召唤加成刷新；只在 owner 端算（基伤字段远端可能未达，命中也只在 owner 结算）
            if (authority) {
                Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                    .ApplyTo(Math.Max(baseDamage, KikasaThrall.DamageMin));
            }

            //换场清闩：远端可能靠收包切状态而非本地同拍转场
            if (State != lastSeenState) {
                lastSeenState = State;
                lastSeenSubState = -1;
                subTimer = 0;
                lastFlungDrop = -1;
                travelBeatDone = State == StateGather && StateTimer > GatherPoolEnd;
                materializeBeatDone = State == StateReform && StateTimer > ReformCondenseEnd;
                dissolveBeatDone = false;
            }
            if (subState != lastSeenSubState) {
                lastSeenSubState = subState;
                subTimer = 0;
                lastFlungDrop = -1;
            }

            StateTimer++;
            subTimer++;
            switch (State) {
                case StateGather: UpdateGather(authority); break;
                case StateReform: UpdateReform(owner, authority); break;
                case StateActive: UpdateActive(owner, authority); break;
                case StateDissolve: UpdateDissolve(authority); break;
            }

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (spinCooldown > 0) {
                spinCooldown--;
            }

            float glow = CondenseProgress * 0.5f;
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.06f * glow, 0.10f * glow, 0.10f * glow);
            }
        }

        /// <summary>鬼雨生命线：域开着、没在收、满水、鬼雨形态在身、不在梦里、主人活着</summary>
        private static bool RainHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && !domain.InDreamPhase
            && domain.IsRainForm && domain.RainBlend > 0.5f
            && domain.RiseT >= 0.9f;

        //==================== 聚拢 ====================

        /// <summary>水团：滞蓄段钉在尸点吸融水，流动段沿垂枝弧线滑向重组点</summary>
        private void UpdateGather(bool authority) {
            //出发点各端取首帧同步位置，滞蓄段不动故窗口安全
            if (!gatherFromSet) {
                gatherFromSet = true;
                gatherFrom = Projectile.Center;
            }
            Projectile.velocity = Vector2.Zero;

            int t = StateTimer;
            if (t <= GatherPoolEnd) {
                //滞蓄：融化的水正在收进来（化水演出同点进行）
                if (!Main.dedServ && Main.GameUpdateCount % 3 == 0) {
                    Vector2 from = Projectile.Center + new Vector2(
                        Main.rand.NextFloat(-46f, 46f), Main.rand.NextFloat(-30f, 4f));
                    PRTLoader.NewParticle<PRT_SewageGlob>(from,
                        (Projectile.Center - from) * 0.05f,
                        KikasaThrall.SewageDeep * Main.rand.NextFloat(0.5f, 0.8f),
                        Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Main.rand.Next(16, 26), Projectile.Center);
                }
            }
            else {
                //流动：先沉后到——二次弧线压向两点连线下方，读作贴地淌行
                if (!travelBeatDone) {
                    travelBeatDone = true;
                    if (IsViewedOwner()) {
                        SoundEngine.PlaySound(SoundID.SplashWeak with {
                            Volume = 0.4f,
                            Pitch = -0.9f,
                            MaxInstances = 3,
                        }, Projectile.Center);
                    }
                }
                float p = MathHelper.Clamp((t - GatherPoolEnd) / (float)(GatherTotal - GatherPoolEnd), 0f, 1f);
                float ease = p * p * (3f - 2f * p);
                Vector2 line = Vector2.Lerp(gatherFrom, ReformFeet, ease);
                float sag = MathF.Sin(ease * MathHelper.Pi) *
                    MathHelper.Clamp(Vector2.Distance(gatherFrom, ReformFeet) * 0.10f, 8f, 52f);
                Projectile.Center = line + new Vector2(0f, sag);

                //淌行拖尾
                if (!Main.dedServ && Main.GameUpdateCount % 2 == 0) {
                    PRTLoader.NewParticle<PRT_SewageGlob>(
                        Projectile.Center + Main.rand.NextVector2Circular(10f, 6f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.3f, 1.2f)),
                        KikasaThrall.SewageDark * Main.rand.NextFloat(0.5f, 0.75f),
                        Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(12, 20));
                }
            }

            if (t >= GatherTotal) {
                //转场确定性（纯计时），各端同拍；owner 盖章纠偏
                PinFeetTo(ReformFeet);
                State = StateReform;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 成形 ====================

        /// <summary>污水自下而上凝聚出伞奴：脚→身→伞面最后析出，实质化拍甩出一圈污水</summary>
        private void UpdateReform(Player owner, bool authority) {
            PinFeetTo(ReformFeet);
            Projectile.velocity = Vector2.Zero;
            int t = StateTimer;
            float progress = CondenseProgress;

            //面向目标玩家或猎物
            int target = FindTarget(owner);
            Vector2 look = target >= 0 ? Main.npc[target].Center : owner.Center;
            if (Math.Abs(look.X - Projectile.Center.X) > 8f) {
                facingLeft = look.X < Projectile.Center.X;
            }

            //凝聚期：污水团自地面弧线扑入正在成形的身体（镜 KasaOni EmergingFx）
            if (!Main.dedServ && progress < 0.85f && Main.GameUpdateCount % 2 == 0) {
                Vector2 feet = FeetAnchor;
                float side = Main.rand.NextFloat(26f, 96f) * (Main.rand.NextBool() ? 1f : -1f);
                Vector2 from = new(feet.X + side, feet.Y - Main.rand.NextFloat(0f, 5f));
                Vector2 to = feet - new Vector2(Main.rand.NextFloat(-8f, 8f),
                    Main.rand.NextFloat(6f, Projectile.height * (0.2f + progress * 0.75f)));
                PRTLoader.NewParticle<PRT_SewageGlob>(from,
                    new Vector2(-side * 0.015f, -Main.rand.NextFloat(1.4f, 3f)),
                    Color.Lerp(KikasaThrall.SewageDeep, KikasaThrall.CorpseTeal, Main.rand.NextFloat(0.4f))
                        * Main.rand.NextFloat(0.6f, 0.9f),
                    Main.rand.NextFloat(0.5f, 0.95f))
                    ?.Configure(Main.rand.Next(18, 32), to);
            }

            //实质化确认拍：扇形甩出一圈污水 + 湿闷落定声 + 小屏震
            if (!materializeBeatDone && t >= ReformCondenseEnd) {
                materializeBeatDone = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.55f,
                        Pitch = -0.15f,
                        MaxInstances = 3,
                    }, FeetAnchor);
                    for (int i = 0; i < 10; i++) {
                        float angle = -MathHelper.Pi * (0.12f + 0.76f * i / 9f);
                        Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1.6f, 3.6f);
                        PRTLoader.NewParticle<PRT_SewageGlob>(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                            vel, Color.Lerp(KikasaThrall.SewageDeep, KikasaThrall.CorpseTeal,
                                Main.rand.NextFloat(0.5f)) * Main.rand.NextFloat(0.55f, 0.85f),
                            Main.rand.NextFloat(0.4f, 0.75f))
                            ?.Configure(Main.rand.Next(16, 28));
                    }
                    if (IsViewedOwner()) {
                        ShakeViewer(2f);
                    }
                }
            }

            if (t >= ReformTotal) {
                State = StateActive;
                StateTimer = 0;
                subState = SubWalk;
                attackCooldown = 40;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 作战 ====================

        private void UpdateActive(Player owner, bool authority) {
            int target = FindTarget(owner);

            switch (subState) {
                case SubLunge: UpdateLunge(owner, target, authority); break;
                case SubSpin: UpdateSpin(owner, target, authority); break;
                default: UpdateWalk(owner, target, authority); break;
            }

            UpdateFacingAndGait();
            WalkingFx();
        }

        private void UpdateWalk(Player owner, int target, bool authority) {
            Vector2 goal;
            float maxSpeed;
            if (target >= 0) {
                goal = Main.npc[target].Center;
                maxSpeed = ChaseMaxSpeed;
            }
            else {
                //无猎物：跟在主人身侧，个体错位散开
                float side = Projectile.identity % 2 == 0 ? 1f : -1f;
                goal = owner.Center + new Vector2(side * PersonalStop, 0f);
                maxSpeed = WalkMaxSpeed;
            }

            //跟丢太远：水化贴回主人脚边（各端同规则，owner 盖章）
            if (Vector2.Distance(Projectile.Center, owner.Center) > 2600f) {
                SnapToOwner(owner, authority);
                return;
            }

            float dx = goal.X - Projectile.Center.X;
            float stop = target >= 0 ? 46f : PersonalStop;
            float desiredX = Math.Abs(dx) > stop ? Math.Sign(dx) * maxSpeed : 0f;
            WalkIntegrate(desiredX);

            //出手裁决：近身跃扑，中距伞旋；规则各端一致，owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 20) {
                NPC npc = Main.npc[target];
                float distX = Math.Abs(npc.Center.X - Projectile.Center.X);
                float distY = Math.Abs(npc.Center.Y - Projectile.Center.Y);
                if (distX < 240f && distY < 140f) {
                    subState = SubLunge;
                    Projectile.netUpdate = authority;
                }
                else if (spinCooldown <= 0 && distX < 560f && distY < 300f) {
                    subState = SubSpin;
                    Projectile.netUpdate = authority;
                }
            }
        }

        /// <summary>跃扑：蓄势下压 → 一帧起跳（接触窗开）→ 落地收势</summary>
        private void UpdateLunge(Player owner, int target, bool authority) {
            int t = subTimer;
            if (t <= LungeWindup) {
                //蓄势：站定下压，朝猎物
                WalkIntegrate(0f);
                if (target >= 0) {
                    facingLeft = Main.npc[target].Center.X < Projectile.Center.X;
                }
                if (t == LungeWindup) {
                    //起跳一帧设速：知重量者不做斜坡
                    Vector2 aim = target >= 0
                        ? Main.npc[target].Center
                        : Projectile.Center + new Vector2(facingLeft ? -220f : 220f, 0f);
                    float dir = Math.Sign(aim.X - Projectile.Center.X);
                    if (dir == 0f) {
                        dir = facingLeft ? -1f : 1f;
                    }
                    Projectile.velocity = new Vector2(dir * 5.4f, -3.4f);
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.42f,
                        Pitch = 0.05f,
                        MaxInstances = 3,
                    }, Projectile.Center);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 5; i++) {
                            PRTLoader.NewParticle<PRT_SewageGlob>(FeetAnchor,
                                new Vector2(-dir * Main.rand.NextFloat(0.6f, 1.6f),
                                    -Main.rand.NextFloat(0.6f, 1.6f)),
                                KikasaThrall.SewageDeep * Main.rand.NextFloat(0.5f, 0.8f),
                                Main.rand.NextFloat(0.35f, 0.6f))
                                ?.Configure(Main.rand.Next(12, 20));
                        }
                    }
                }
                return;
            }

            if (t <= LungeActiveEnd) {
                //跃扑途中：重力照走、贴地滑撞，身后甩水
                WalkIntegrate(Projectile.velocity.X, keepMomentum: true);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_SewageGlob>(
                        Projectile.Center - Projectile.velocity * 0.5f
                            + Main.rand.NextVector2Circular(8f, 8f),
                        -Projectile.velocity * 0.1f,
                        KikasaThrall.SewageDark * 0.6f, Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(Main.rand.Next(10, 16));
                }
                return;
            }

            if (t <= LungeRecoverEnd) {
                //落地收势
                WalkIntegrate(Projectile.velocity.X * 0.82f, keepMomentum: true);
                return;
            }

            subState = SubWalk;
            attackCooldown = 90;
            Projectile.netUpdate = authority;
        }

        /// <summary>伞旋雨溅：驻步收伞 → 快旋甩出污水滴（owner 端生成）→ 收伞回步</summary>
        private void UpdateSpin(Player owner, int target, bool authority) {
            int t = subTimer;
            WalkIntegrate(0f);
            if (target >= 0 && t <= SpinBrakeEnd) {
                facingLeft = Main.npc[target].Center.X < Projectile.Center.X;
            }

            if (t > SpinBrakeEnd && t <= SpinFlingEnd) {
                //旋伞期：甩滴节拍 10/17/24，闩防快照回卷重发
                int flingIndex = (t - SpinBrakeEnd - 2) / 7;
                bool onBeat = (t - SpinBrakeEnd - 2) % 7 == 0 && flingIndex >= 0 && flingIndex < 3;
                if (onBeat && lastFlungDrop < flingIndex) {
                    lastFlungDrop = flingIndex;
                    FlingDroplet(owner, target, flingIndex, authority);
                }
                //旋出的环形水沫
                if (!Main.dedServ && t % 2 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 rim = Projectile.Center + new Vector2(0f, -Projectile.height * 0.34f)
                        + ang.ToRotationVector2() * 20f * BodyScale;
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(rim,
                        ang.ToRotationVector2() * Main.rand.NextFloat(1.2f, 2.4f),
                        KikasaThrall.PaleSheen * Main.rand.NextFloat(0.3f, 0.45f),
                        Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(Main.rand.Next(12, 20), 0f);
                }
            }

            if (t >= SpinTotal) {
                subState = SubWalk;
                attackCooldown = 60;
                spinCooldown = 200;
                Projectile.netUpdate = authority;
            }
        }

        /// <summary>
        /// 伞旋甩墨：复用主人普攻的追踪墨滴 <see cref="KikasaInkDrop"/>（鬼奴学主人的样子甩墨），
        /// 雨形态轻滴规格 scale 0.85；ai0=锁定目标 ai1=无目标时的坠落列
        /// </summary>
        private void FlingDroplet(Player owner, int target, int index, bool authority) {
            Vector2 muzzle = Projectile.Center + new Vector2(0f, -Projectile.height * 0.38f);
            SoundEngine.PlaySound(SoundID.Item17 with {
                Volume = 0.32f,
                Pitch = -0.5f + index * 0.08f,
                MaxInstances = 3,
            }, muzzle);
            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_SewageGlob>(muzzle,
                        Main.rand.NextVector2Circular(1.6f, 1.2f) - new Vector2(0f, 0.8f),
                        KikasaThrall.SewageDeep * 0.7f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Main.rand.Next(10, 18));
                }
            }

            //弹体只在 owner 端生成，spawn 包自带全部初值
            if (!authority) {
                return;
            }
            Vector2 aimPos = target >= 0
                ? Main.npc[target].Center
                : Projectile.Center + new Vector2(facingLeft ? -260f : 260f, -30f);
            float side = Math.Sign(aimPos.X - muzzle.X);
            if (side == 0f) {
                side = facingLeft ? -1f : 1f;
            }
            //出手向斜上：墨滴的贝塞尔弧以出手方向为切线，甩上去再追着砸下来
            Vector2 flick = new Vector2(side * (0.5f + index * 0.12f), -1f)
                .SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(7f, 9f);
            int damage = Math.Max(1, (int)(Projectile.damage * DropletDamageRatio));
            int p = Projectile.NewProjectile(Projectile.GetSource_FromAI(), muzzle, flick,
                ModContent.ProjectileType<KikasaInkDrop>(), damage, 2f, Projectile.owner,
                target, aimPos.X + Main.rand.NextFloat(-24f, 24f), 0f);
            if (p >= 0 && p < Main.maxProjectiles) {
                Main.projectile[p].scale = 0.85f;
                Main.projectile[p].netUpdate = true;
            }
        }

        /// <summary>跟丢贴回：主人脚边探地落位，两端各自演小水花（规则确定性）</summary>
        private void SnapToOwner(Player owner, bool authority) {
            Vector2 probe = owner.Center + new Vector2(owner.direction * -60f, -80f);
            if (!KasaOniActor.TryFindStandableGround(
                probe, HitboxWidth, HitboxHeight, out Vector2 feet)) {
                feet = new Vector2(owner.Center.X, owner.Bottom.Y);
            }
            PinFeetTo(feet);
            Projectile.velocity = Vector2.Zero;
            Projectile.netUpdate = authority;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.4f,
                    Pitch = -0.6f,
                    MaxInstances = 3,
                }, feet);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_SewageGlob>(feet + new Vector2(
                        Main.rand.NextFloat(-12f, 12f), -4f),
                        new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 2.4f)),
                        KikasaThrall.SewageDeep * 0.7f, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Main.rand.Next(14, 22));
                }
            }
        }

        //==================== 溶解 ====================

        private void UpdateDissolve(bool authority) {
            int t = StateTimer;
            Projectile.velocity.X *= 0.9f;
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity * 0.6f, 6f);

            if (!dissolveBeatDone) {
                dissolveBeatDone = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.5f,
                        Pitch = -0.55f,
                        MaxInstances = 3,
                    }, FeetAnchor);
                }
            }

            //洒落跟着熔断前沿走：顶部先化，残躯向脚底退缩（镜 KasaOni DissolvingFx）
            if (!Main.dedServ && Main.GameUpdateCount % 2 == 0) {
                float progress = CondenseProgress;
                float frontY = Projectile.height * MathHelper.Clamp(1f - progress, 0f, 1f);
                Vector2 from = Projectile.position + new Vector2(
                    Main.rand.NextFloat(2f, Projectile.width - 2f),
                    MathHelper.Clamp(frontY + Main.rand.NextFloat(-6f, 14f), 0f, Projectile.height - 2f));
                PRTLoader.NewParticle<PRT_SewageGlob>(from,
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(0.4f, 1.8f)),
                    Color.Lerp(KikasaThrall.SewageDeep, KikasaThrall.SewageDark, Main.rand.NextFloat())
                        * Main.rand.NextFloat(0.6f, 0.9f),
                    Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(16, 28));
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
            }
        }

        private void BeginDissolve() {
            //还没成形就要收场：什么都没露出来，直接散了
            if (State == StateGather) {
                Projectile.Kill();
                return;
            }
            State = StateDissolve;
            StateTimer = 0;
            subState = SubWalk;
            Projectile.netUpdate = Main.myPlayer == Projectile.owner;
        }

        //==================== 运动积分（全端一致） ====================

        /// <summary>
        /// 贴地行走：台阶蹭上（原版NPC口径）→ 物块裁剪 → 斜坡贴合；
        /// 引擎随后执行 position += velocity。keepMomentum=跃扑期不重设横速只走碰撞
        /// </summary>
        private void WalkIntegrate(float desiredX, bool keepMomentum = false) {
            if (!keepMomentum) {
                Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, desiredX, WalkAccel);
            }
            else {
                Projectile.velocity.X = desiredX;
            }
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MaxFallSpeed);

            Vector2 position = Projectile.position;
            Vector2 velocity = Projectile.velocity;
            if (velocity.Y >= 0f) {
                float stepSpeed = 1f;
                float gfxOffY = 0f;
                Collision.StepUp(ref position, ref velocity, Projectile.width, Projectile.height,
                    ref stepSpeed, ref gfxOffY, 1, false, 1);
                Projectile.position = position;
            }
            velocity = Collision.TileCollision(Projectile.position, velocity,
                Projectile.width, Projectile.height);
            Vector4 slope = Collision.SlopeCollision(Projectile.position, velocity,
                Projectile.width, Projectile.height, Gravity, false);
            Projectile.position = new Vector2(slope.X, slope.Y);
            Projectile.velocity = new Vector2(slope.Z, slope.W);
        }

        private void PinFeetTo(Vector2 feet) {
            Projectile.position = feet - new Vector2(Projectile.width * 0.5f, Projectile.height);
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

        private void UpdateFacingAndGait() {
            if (Math.Abs(Projectile.velocity.X) > 0.15f) {
                if (subState != SubSpin) {
                    facingLeft = Projectile.velocity.X < 0f;
                }
                waddlePhase += 0.1f + Math.Abs(Projectile.velocity.X) * 0.06f;
                if (waddlePhase > MathHelper.TwoPi) {
                    waddlePhase -= MathHelper.TwoPi;
                }
            }
        }

        /// <summary>行走期：伞沿垂滴与湿脚吧唧声（镜 KasaOni WalkingFx）</summary>
        private void WalkingFx() {
            if (Main.dedServ) {
                return;
            }
            dripTimer++;
            if (dripTimer >= 34) {
                dripTimer = 0;
                Vector2 rim = Projectile.position + new Vector2(
                    Main.rand.NextFloat(4f, Projectile.width - 4f), Main.rand.NextFloat(2f, 12f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(rim,
                    new Vector2(0f, Main.rand.NextFloat(1.2f, 2f)),
                    KikasaThrall.PaleSheen * Main.rand.NextFloat(0.3f, 0.45f),
                    Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(Main.rand.Next(16, 26), 0f);
            }

            bool moving = Math.Abs(Projectile.velocity.X) > 0.25f
                && Math.Abs(Projectile.velocity.Y) < 0.4f;
            if (moving && ++squelchTimer >= 30) {
                squelchTimer = 0;
                SoundEngine.PlaySound(SoundID.Drip with {
                    Pitch = Main.rand.NextFloat(-0.55f, -0.3f),
                    Volume = 0.24f,
                    MaxInstances = 5,
                }, FeetAnchor);
            }
        }

        private bool IsViewedOwner()
            => KikasaDomain.Viewed != null
            && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //跃扑撞击的溅水（OnHit 只在 owner 端跑，队友看甩水拖尾即可）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_SewageGlob>(
                    target.Center + Main.rand.NextVector2Circular(16f, 16f),
                    Projectile.velocity * 0.25f + Main.rand.NextVector2Circular(2f, 2f),
                    KikasaThrall.SewageDeep * 0.7f, Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(Main.rand.Next(12, 22));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.5f,
                Pitch = -0.35f,
                MaxInstances = 3,
            }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残水：溶解尾拍或异常移除都留一滩
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_SewageGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 22f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.4f)),
                    KikasaThrall.SewageDeep * 0.6f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 24));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.2f), KikasaThrall.SewageDark * 0.7f,
                Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            float progress = CondenseProgress;
            SpriteBatch sb = Main.spriteBatch;

            //夜雨里保轮廓：环境光染向湿墨灰白
            Color light = Lighting.GetColor((FeetAnchor / 16f).ToPoint());
            light = Color.Lerp(light, KikasaThrall.PaleSheen, 0.30f);

            switch (State) {
                case StateGather:
                    DrawGatherMass(sb);
                    return false;
                case StateReform:
                case StateDissolve: {
                    //凝聚/溶解共用正弦弓形包络：成形期潭被吸干、溶解期潭再涨起
                    float envelope = MathF.Sin(
                        MathHelper.Clamp((1f - progress) * 1.2f, 0f, 1f) * MathHelper.Pi);
                    KikasaThrallRenderer.DrawPuddle(sb, FeetAnchor, envelope, BodyScale, Seed);

                    float wobble = MathF.Sin(Main.GlobalTimeWrappedHourly * 5.3f + Seed * 1.7f)
                        * 0.035f * (1f - progress);
                    float scale = BodyScale * MaterializePop();
                    KikasaThrallRenderer.DrawBodyCondensing(sb, FeetAnchor, WalkFrame(),
                        progress, scale, facingLeft, FeetAnchor.Y + 2f, light, wobble, Seed);
                    return false;
                }
                default: {
                    float moveFactor = MathHelper.Clamp(
                        Math.Abs(Projectile.velocity.X) / WalkMaxSpeed, 0f, 1f);
                    //伞旋期：站桩但伞在转——用高步频假蹒跚读出旋势
                    float phase = waddlePhase;
                    if (subState == SubSpin && subTimer > SpinBrakeEnd && subTimer <= SpinFlingEnd) {
                        phase = Main.GlobalTimeWrappedHourly * 26f + Seed;
                        moveFactor = 0.8f;
                    }
                    KikasaThrallRenderer.DrawBodyWalking(sb, FeetAnchor, WalkFrame(),
                        BodyScale * MaterializePop(), facingLeft, light, phase, moveFactor, Seed);
                    return false;
                }
            }
        }

        /// <summary>聚拢期的水团：低伏的浊水丘，蠕动呼吸；流动段沿速度拉伸</summary>
        private void DrawGatherMass(SpriteBatch sb) {
            //滞蓄段丘体渐涨，流动段保持；水团中心即贴地锚（spawn 位取的是尸体脚底）
            float grow = MathHelper.Clamp(StateTimer / (float)GatherPoolEnd, 0.25f, 1f);
            KikasaThrallRenderer.DrawPuddle(sb, Projectile.Center, 0.5f * grow, BodyScale, Seed);

            Texture2D mask = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98")?.Value;
            if (mask == null) {
                return;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float wob = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 6.2f + Seed) * 0.12f;
            Vector2 core = new(38f * grow * wob / mask.Width, 20f * grow / mask.Height);
            sb.Draw(mask, pos, null, KikasaThrall.SewageDeep * 0.85f, 0f,
                mask.Size() * 0.5f, core * BodyScale, SpriteEffects.None, 0f);
            sb.Draw(mask, pos + new Vector2(0f, 2f), null, KikasaThrall.SewageDark * 0.7f, 0f,
                mask.Size() * 0.5f, core * BodyScale * new Vector2(0.7f, 0.8f), SpriteEffects.None, 0f);
        }

        /// <summary>实质化落定的弹性 pop：46~56f 内 1→1.14→1</summary>
        private float MaterializePop() {
            if (State != StateReform || StateTimer <= ReformCondenseEnd) {
                return 1f;
            }
            float p = MathHelper.Clamp((StateTimer - ReformCondenseEnd) / 10f, 0f, 1f);
            return 1f + 0.14f * MathF.Sin(p * MathHelper.Pi);
        }

        /// <summary>多帧真贴图接入后的默认步频：0.12 相位一帧；单帧恒 0</summary>
        private int WalkFrame()
            => KikasaThrallRenderer.FrameCount <= 1
                ? 0 : (int)(waddlePhase / 0.12f) % KikasaThrallRenderer.FrameCount;
    }

}
