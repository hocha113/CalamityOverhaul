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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaYoyos
{
    /// <summary>
    /// 械奴·湖水球手（通用球奴）。单弹幕驱动至多三名球手，各托一枚湖水凝成的悠悠球：
    /// 轮转放球逐手错拍甩出球体，球飞抵猎物周身驻留磨伤（悠悠球的机制身份=驻留连击）
    /// 再沿线收回；隔两次三球齐放结阵，驻留时绕猎物公转旋切一轮。
    /// 手上球影的可见性走在场扫描（本手球体在外即空手），各端独立推导一致，
    /// 收线拍由 在外→归手 的边沿触发。
    /// 联机契约与通用械奴同构：owner 裁决转场、球只在 authority 生成、
    /// 球手数与武器类型 spawn 后经 ExtraAI 随包补发
    /// </summary>
    internal class KikasaYoyoServant : ModProjectile, IKikasaArmsServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>单跳基伤（召唤加成与档案倍率前；驻留高频跳，单跳轻）</summary>
        internal const int TickDamage = 130;

        /// <summary>齐放单球折扣（结阵三球同磨）</summary>
        internal const float RingMul = 0.8f;

        /// <summary>编队硬上限</summary>
        internal const int MaxHands = 3;

        //==================== 档案 ====================

        private int armsItemType = ItemID.WoodYoyo;

        public int ArmsItemType => armsItemType;

        private KikasaYoyoProfile? profileCache;

        private KikasaYoyoProfile Profile => profileCache ??= KikasaArmsProfiler.YoyoProfileOf(armsItemType);

        private void SetArmsItemType(int itemType) {
            armsItemType = itemType;
            profileCache = null;
        }

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateRelay = 2;
        private const int StateRing = 3;
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
        private const float EmergeSpan = 52f;

        //轮转放球：逐手错拍甩出，球体自持驻留与回收
        private const int CastWindup = 10;
        private const int CastStagger = 18;

        private int RelayTotal => CastStagger * handCount + 16;

        //齐放结阵：拢势→同帧三球出线
        private const int RingWindup = 22;
        private const int RingTotal = 60;

        private const int DissolveStagger = 5;
        private const int DissolveFrames = 70;

        //==================== 各手本地模拟 ====================

        private readonly Vector2[] handPos = new Vector2[MaxHands];
        private readonly Vector2[] handVel = new Vector2[MaxHands];
        private readonly Vector2[] handTarget = new Vector2[MaxHands];
        /// <summary>托举球影自转角：球旋着悬</summary>
        private readonly float[] handRot = new float[MaxHands];
        /// <summary>自转速度：蓄势时抡起来</summary>
        private readonly float[] handSpin = new float[MaxHands];
        private bool handsInit;

        private int handCount = MaxHands;

        public int UnitCount => handCount;

        //==================== 本地表现量 ====================

        private readonly bool[] breachDone = new bool[MaxHands];
        private readonly int[] lastFireTick = new int[MaxHands];
        private readonly bool[] dissolveSplashed = new bool[MaxHands];
        /// <summary>本手的球体在外飞（每帧在场扫描重建，各端独立一致）</summary>
        private readonly bool[] yoyoOut = new bool[MaxHands];
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;
        private bool formSnapDone;

        private Player Owner => Main.player[Projectile.owner];
        private float Seed => Projectile.identity * 0.6173f;

        //==================== 召唤入口 ====================

        internal static void Summon(Player owner, Vector2 emergeAt, int count, int itemType) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            KikasaYoyoProfile profile = KikasaArmsProfiler.YoyoProfileOf(itemType);
            count = Math.Clamp(count, 1, profile.MaxUnits);
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(TickDamage * profile.TickDamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaYoyoServant>(), damage, 2f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaYoyoServant pack) {
                pack.handCount = count;
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

        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(armsItemType);
            writer.Write((byte)handCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int itemType = reader.ReadInt32();
            int count = reader.ReadByte();
            if (itemType > ItemID.None && itemType < ItemLoader.ItemCount && itemType != armsItemType) {
                SetArmsItemType(itemType);
            }
            count = Math.Clamp(count, 1, Profile.MaxUnits);
            if (count != handCount) {
                handCount = count;
                handsInit = false;
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
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(TickDamage * Profile.TickDamageMul);

            if (State != lastSeenState) {
                lastSeenState = State;
                Array.Fill(lastFireTick, -1);
                if (State == StateDissolve) {
                    Array.Fill(dissolveSplashed, false);
                }
            }

            if (!handsInit) {
                RebuildHands(domain);
            }

            ScanYoyosOut();

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateRelay: UpdateRelay(owner, authority); break;
                case StateRing: UpdateRing(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateHands(owner, domain);
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            for (int i = 0; i < handCount; i++) {
                float glow = HandAlpha(i) * HeldVisible(i) * 0.28f;
                if (glow > 0.02f) {
                    Lighting.AddLight(handPos[i], 0.36f * glow, 0.1f * glow, 0.09f * glow);
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        /// <summary>在场扫描重建各手"球在外"标记：在外→归手的边沿即收线拍</summary>
        private void ScanYoyosOut() {
            Span<bool> nowOut = stackalloc bool[MaxHands];
            nowOut.Clear();
            int yoyoType = ModContent.ProjectileType<KikasaYoyoProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != yoyoType || proj.owner != Projectile.owner) {
                    continue;
                }
                if ((int)proj.ai[0] != Projectile.identity) {
                    continue;
                }
                int hand = KikasaYoyoProj.UnpackHand((int)proj.ai[1]);
                if (hand >= 0 && hand < MaxHands) {
                    nowOut[hand] = true;
                }
            }
            for (int i = 0; i < handCount; i++) {
                if (yoyoOut[i] && !nowOut[i] && State != StateDissolve && State != StateEmerge) {
                    OnReelBack(i);
                }
                yoyoOut[i] = nowOut[i];
            }
        }

        /// <summary>收线拍：手一沉、线响，掌心溅几粒</summary>
        private void OnReelBack(int i) {
            handVel[i].Y += 1.6f;
            handSpin[i] = 0.5f * (i % 2 == 0 ? 1f : -1f);
            SoundEngine.PlaySound(SoundID.Grab with {
                Volume = 0.24f,
                Pitch = 0.4f,
                MaxInstances = 3
            }, handPos[i]);
            if (Main.dedServ) {
                return;
            }
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    handPos[i] + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.6f, 1.6f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(8, 14), 0f);
            }
        }

        //==================== 出水 ====================

        private float BreachX(int i)
            => Projectile.Center.X + (i - (handCount - 1) * 0.5f) * EmergeSpan;

        private static int BreachTime(int i) => OmenFrames + i * BreachGap;

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                Projectile.velocity = Vector2.Zero;
                if (viewed && t % 5 == 2) {
                    float converge = 1f - t / (float)OmenFrames;
                    for (int i = 0; i < handCount; i++) {
                        float wobble = MathF.Sin(t * 0.5f + i * 1.7f) * converge * 22f;
                        KikasaDomainDeco.RippleAt(new Vector2(BreachX(i) + wobble, lakeY),
                            0.3f + (1f - converge) * 0.4f);
                    }
                }
                if (viewed && (t == 5 || t == 14 || t == 22)) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.42f,
                        Pitch = -0.55f + t * 0.012f,
                        MaxInstances = 3
                    }, new Vector2(Projectile.Center.X, lakeY));
                }
                return;
            }

            for (int i = 0; i < handCount; i++) {
                if (!breachDone[i] && t >= BreachTime(i)) {
                    breachDone[i] = true;
                    handVel[i] = new Vector2(0f, -11.8f - i * 0.3f);
                    handSpin[i] = (i % 2 == 0 ? 1f : -1f) * 0.5f;
                    if (i == 0) {
                        Projectile.velocity = new Vector2(0f, -3f);
                    }
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.7f,
                        Pitch = -0.38f + i * 0.07f,
                        MaxInstances = 3
                    }, handPos[i]);
                    if (viewed) {
                        KikasaDomainDeco.RippleAt(new Vector2(BreachX(i), lakeY), 1.2f);
                        KikasaDomainDeco.SplashAt(new Vector2(BreachX(i), lakeY), 6);
                        for (int k = 0; k < 9; k++) {
                            float angle = -MathHelper.Pi * (0.2f + 0.6f * k / 8f);
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(
                                new Vector2(BreachX(i) + Main.rand.NextFloat(-10f, 10f), lakeY - 4f),
                                angle.ToRotationVector2() * Main.rand.NextFloat(2.4f, 5.2f),
                                BloodMain * Main.rand.NextFloat(0.45f, 0.62f),
                                Main.rand.NextFloat(0.36f, 0.6f))
                                ?.Configure(Main.rand.Next(16, 26), Main.rand.NextFloat(-0.4f, 0.4f));
                        }
                        ShakeViewer(1.2f);
                    }
                }
            }

            Projectile.velocity *= 0.96f;

            if (viewed && t < RiseEnd) {
                for (int i = 0; i < handCount; i++) {
                    if (t < BreachTime(i) || t % 3 != i % 3) {
                        continue;
                    }
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        handPos[i] + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(2f, 10f)),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.2f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.3f, 0.52f))
                        ?.Configure(Main.rand.Next(12, 22), 0f);
                }
            }

            //定编拍：转速齐收一顿
            if (!formSnapDone && t >= FormupFrame) {
                formSnapDone = true;
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.44f, Pitch = -0.2f, MaxInstances = 2 }, Projectile.Center);
                for (int i = 0; i < handCount; i++) {
                    handVel[i] += new Vector2(
                        -MathF.Sign(handPos[i].X - Projectile.Center.X) * 1.4f, -1f);
                }
                if (viewed) {
                    ShakeViewer(1.5f);
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

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            Vector2 anchor = owner.Center + new Vector2(0f, -26f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildHands(owner.GetModPlayer<KikasaDomainPlayer>());
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
                State = attackIndex % 3 == 0 ? StateRing : StateRelay;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 轮转放球 ====================

        private void UpdateRelay(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= 8) {
                EndAttack(authority, 50);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center
                : Projectile.Center + new Vector2(owner.direction * 260f, 0f);

            //质心稳在猎物侧中距离：球要放得出去也收得回来
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
            Vector2 anchor = owner.Center + toT * 30f + perp * MathF.Sin(t * 0.04f + Seed) * 22f
                + new Vector2(0f, -28f);
            Vector2 desired = (anchor - Projectile.Center) * 0.1f;
            if (desired.Length() > 13f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 13f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.16f);

            //逐手错拍甩球：球体自持驻留与回收，空手的轮次静默跳过
            for (int i = 0; i < handCount; i++) {
                int fireAt = CastWindup + i * CastStagger;
                if (t == fireAt - 6 && !yoyoOut[i]) {
                    SoundEngine.PlaySound(SoundID.Item7 with {
                        Volume = 0.22f,
                        Pitch = 0.3f,
                        MaxInstances = 3
                    }, handPos[i]);
                }
                if (t == fireAt && 0 > lastFireTick[i] && !yoyoOut[i]) {
                    lastFireTick[i] = 0;
                    CastYoyo(owner, authority, i, focus, ring: false, RingMulOf(false));
                }
            }

            if (t >= RelayTotal) {
                EndAttack(authority, Math.Max(Profile.CastPeriod - RelayTotal, 42));
            }
        }

        private static float RingMulOf(bool ring) => ring ? RingMul : 1f;

        /// <summary>甩出一枚自研球体：驻留磨伤自持，收线回本手才归位</summary>
        private void CastYoyo(Player owner, bool authority, int i, Vector2 focus, bool ring, float mul) {
            Vector2 from = handPos[i];
            Vector2 aim = (focus - from).SafeNormalize(Vector2.UnitX);
            handSpin[i] = 0.62f * (aim.X >= 0f ? 1f : -1f);
            handVel[i] -= aim * 2.2f;

            SoundEngine.PlaySound(Profile.CastSound with {
                Volume = 0.34f,
                Pitch = 0.15f + i * 0.05f,
                MaxInstances = 4
            }, from);
            if (!Main.dedServ) {
                for (int k = 0; k < 3; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        aim.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(1.5f, 3.5f),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.24f, 0.4f))
                        ?.Configure(Main.rand.Next(8, 14), 0.2f);
                }
            }
            if (ViewedOwner) {
                ShakeViewer(0.5f);
            }

            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                    .ApplyTo(TickDamage * Profile.TickDamageMul * mul);
                //ai0=父 identity、ai1=手序+结阵旗打包、ai2=武器类型：spawn 包自含
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), from,
                    aim * Profile.TopSpeed,
                    ModContent.ProjectileType<KikasaYoyoProj>(), damage, 2f, Projectile.owner,
                    Projectile.identity, KikasaYoyoProj.PackHand(i, ring), armsItemType);
                yoyoOut[i] = true;
            }
        }

        //==================== 齐放结阵 ====================

        private void UpdateRing(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= RingWindup / 2) {
                EndAttack(authority, 60);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center
                : Projectile.Center + new Vector2(owner.direction * 260f, 0f);

            //拢近半步
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 anchor = owner.Center + toT * 50f + new Vector2(0f, -26f);
            Vector2 desired = (anchor - Projectile.Center) * 0.12f;
            if (desired.Length() > 15f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 15f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.2f);

            //拢势拍
            if (t == 6) {
                SoundEngine.PlaySound(SoundID.Item7 with {
                    Volume = 0.32f,
                    Pitch = -0.1f,
                    MaxInstances = 2
                }, Projectile.Center);
            }

            //同帧三球出线：驻留时按手序相位差绕猎物公转
            if (t == RingWindup && 0 > lastFireTick[0]) {
                lastFireTick[0] = 0;
                for (int i = 0; i < handCount; i++) {
                    if (yoyoOut[i]) {
                        continue;
                    }
                    CastYoyo(owner, authority, i, focus, ring: true, RingMulOf(true));
                }
                if (ViewedOwner) {
                    ShakeViewer(1.2f);
                }
            }

            if (t >= RingTotal) {
                EndAttack(authority, Math.Max(Profile.CastPeriod - RingTotal, 70));
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

            for (int i = 0; i < handCount; i++) {
                int lt = t - i * DissolveStagger;
                if (lakeAlive && !dissolveSplashed[i] && lt >= 0 && handPos[i].Y >= lakeY) {
                    dissolveSplashed[i] = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.5f,
                        Pitch = -0.4f + i * 0.08f,
                        MaxInstances = 3
                    }, handPos[i]);
                    if (ViewedOwner) {
                        Vector2 hit = new(handPos[i].X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 5);
                        KikasaDomainDeco.RippleAt(hit, 0.8f);
                        ShakeViewer(0.9f);
                    }
                }
            }

            if (!Main.dedServ && HandAlpha(0) > 0.15f) {
                int i = t % handCount;
                if (t - i * DissolveStagger >= 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        handPos[i] + Main.rand.NextVector2Circular(14f, 8f),
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

        //==================== 各手推进 ====================

        private void RebuildHands(KikasaDomainPlayer domain) {
            handsInit = true;
            for (int i = 0; i < MaxHands; i++) {
                if (State == StateEmerge) {
                    handPos[i] = new Vector2(BreachX(i), domain.LakeWorldY + 26f);
                }
                else {
                    float phase = Main.GlobalTimeWrappedHourly * 0.6f + Seed + i * MathHelper.TwoPi / Math.Max(handCount, 1);
                    handPos[i] = Projectile.Center + new Vector2(MathF.Cos(phase) * 88f, MathF.Sin(phase) * 42f - 26f);
                }
                handRot[i] = Seed + i * 2.1f;
                handVel[i] = Vector2.Zero;
                handSpin[i] = 0f;
                handTarget[i] = handPos[i];
            }
        }

        private void ChaseHand(int i, float accel, float damp) {
            handVel[i] = (handVel[i] + (handTarget[i] - handPos[i]) * accel) * damp;
            handPos[i] += handVel[i];
        }

        private float Sway(int i, float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed + i * 2.4f) * amp;

        private void UpdateHands(Player owner, KikasaDomainPlayer domain) {
            if (!handsInit) {
                return;
            }
            int t = (int)StateTimer;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    for (int i = 0; i < handCount; i++) {
                        if (t < BreachTime(i)) {
                            handPos[i] = new Vector2(BreachX(i), lakeY + 26f);
                            handVel[i] = Vector2.Zero;
                            handTarget[i] = handPos[i];
                            continue;
                        }
                        handTarget[i] = new Vector2(BreachX(i), lakeY - 88f + Sway(i, 2.1f, 8f));
                        int lt = t - BreachTime(i);
                        if (lt < 14) {
                            handVel[i].Y *= 0.955f;
                            handVel[i].X *= 0.98f;
                            handPos[i] += handVel[i];
                        }
                        else {
                            ChaseHand(i, 0.05f, 0.86f);
                        }
                        handRot[i] += handSpin[i];
                        handSpin[i] *= 0.95f;
                    }
                    break;
                }
                case StateFollow: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    for (int i = 0; i < handCount; i++) {
                        float phase = tGlobal * 0.6f + Seed + i * MathHelper.TwoPi / handCount;
                        Vector2 slot = Projectile.Center + new Vector2(MathF.Cos(phase) * 88f, MathF.Sin(phase) * 42f - 26f);
                        slot.Y += MathF.Sin(tGlobal * 2.3f + Seed * 2f + i * 1.9f) * 6f;
                        handTarget[i] = slot;
                        ChaseHand(i, 0.06f, 0.84f);
                        //歇姿慢转：球在手上转着等
                        handRot[i] += handSpin[i] + 0.05f;
                        handSpin[i] *= 0.92f;
                    }
                    break;
                }
                case StateRelay: {
                    Vector2 focus = Projectile.Center + new Vector2(owner.direction * 200f, 0f);
                    int target = FindTarget(owner);
                    if (target >= 0) {
                        focus = Main.npc[target].Center;
                    }
                    Vector2 toT = (focus - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
                    for (int i = 0; i < handCount; i++) {
                        int fireAt = CastWindup + i * CastStagger;
                        bool winding = t >= fireAt - 8 && t < fireAt && !yoyoOut[i];
                        float lane = i - (handCount - 1) * 0.5f;
                        Vector2 slot;
                        if (winding) {
                            //蓄：抢前半步、往后仰，抡起来
                            float w = (t - (fireAt - 8)) / 8f;
                            slot = Projectile.Center + toT * (22f - w * 30f) + perp * lane * 34f;
                            handSpin[i] = MathHelper.Lerp(handSpin[i], 0.55f, 0.2f);
                        }
                        else {
                            slot = Projectile.Center + toT * 14f + perp * lane * 38f
                                + new Vector2(0f, Sway(i, 2f, 4f));
                        }
                        handTarget[i] = slot;
                        ChaseHand(i, winding ? 0.14f : 0.07f, 0.8f);
                        handRot[i] += handSpin[i] + 0.03f;
                        handSpin[i] *= 0.9f;
                    }
                    break;
                }
                case StateRing: {
                    int target = FindTarget(owner);
                    Vector2 focus = target >= 0 ? Main.npc[target].Center
                        : Projectile.Center + new Vector2(owner.direction * 260f, 0f);
                    Vector2 toT = (focus - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
                    for (int i = 0; i < handCount; i++) {
                        float lane = i - (handCount - 1) * 0.5f;
                        //拢成一排，蓄势期全体抡转
                        Vector2 slot = Projectile.Center + toT * 20f + perp * lane * 30f;
                        handTarget[i] = slot;
                        ChaseHand(i, 0.12f, 0.8f);
                        if (t < RingWindup && !yoyoOut[i]) {
                            handSpin[i] = MathHelper.Lerp(handSpin[i], 0.6f, 0.15f);
                        }
                        handRot[i] += handSpin[i] + 0.02f;
                        handSpin[i] *= 0.92f;
                    }
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    for (int i = 0; i < handCount; i++) {
                        int lt = t - i * DissolveStagger;
                        if (lt < 0) {
                            continue;
                        }
                        handVel[i].X *= 0.93f;
                        handVel[i].Y = MathF.Min(handVel[i].Y + 0.3f, 9.5f);
                        handPos[i] += handVel[i];
                        handTarget[i] = handPos[i];
                        handRot[i] += 0.03f;
                    }
                    break;
                }
            }

            for (int i = 0; i < handCount; i++) {
                if (!skipFix && Vector2.Distance(handPos[i], handTarget[i]) > 780f) {
                    handPos[i] = handTarget[i];
                    handVel[i] = Vector2.Zero;
                }
            }
        }

        private void UpdateAmbient() {
            if (Main.dedServ
                || State is not (StateFollow or StateRelay or StateRing)) {
                return;
            }
            if (Main.rand.NextBool(18) && HandAlpha(0) > 0.5f) {
                int i = Main.rand.Next(handCount);
                if (HeldVisible(i) > 0.5f) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        handPos[i] + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(4f, 10f)),
                        new Vector2(0f, Main.rand.NextFloat(0.5f, 1.1f)),
                        BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                        Main.rand.NextFloat(0.26f, 0.46f))?.Configure(Main.rand.Next(14, 26), 0f);
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

        /// <summary>球体收线的归手点：球手在场时读本手实时位，编队没了退回主人</summary>
        internal Vector2 ReelPointOf(int hand) {
            if (!handsInit || hand < 0 || hand >= MaxHands) {
                return Projectile.Center;
            }
            return handPos[hand];
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float HandAlpha(int i) {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < BreachTime(i) ? 0f : MathHelper.Clamp((t - BreachTime(i)) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - (t - i * DissolveStagger)) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>手上球影的可见度：球在外飞就空手</summary>
        private float HeldVisible(int i) => yoyoOut[i] ? 0f : 1f;

        private float HandForm(int i) {
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

        private float HandScale(int i) {
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
            if (!handsInit) {
                return false;
            }
            Main.instance.LoadItem(armsItemType);
            Texture2D tex = TextureAssets.Item[armsItemType]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            DrawBodies(sb, tex);
            return false;
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
            for (int i = 0; i < handCount; i++) {
                float alpha = HandAlpha(i) * HeldVisible(i);
                if (alpha <= 0.01f) {
                    continue;
                }
                float rot = handRot[i];
                Vector2 drawPos = handPos[i] - Main.screenPosition;
                float dissolve = DissolveAmt(i);

                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed + i * 1.7f;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.5f, MathF.Cos(wt * 0.83f) * 1.8f);
                    float envScale = HandScale(i) * (1.16f + MathF.Sin(wt * 1.6f) * 0.03f);
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 3.7f + 5.1f);
                    form.Parameters["uForm"]?.SetValue(1f);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(tex, drawPos + wobOff, null,
                        new Color(255, 255, 255, (byte)(alpha * 130f)),
                        rot, origin, envScale, SpriteEffects.None, 0f);
                }

                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 3.7f);
                    form.Parameters["uForm"]?.SetValue(HandForm(i));
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }
                sb.Draw(tex, drawPos, null, color, rot, origin, HandScale(i), SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !handsInit) {
                return;
            }
            for (int i = 0; i < handCount; i++) {
                for (int k = 0; k < 4; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        handPos[i] + Main.rand.NextVector2Circular(12f, 8f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.5f, 2.4f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(Main.rand.Next(12, 24), 0f);
                }
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.55f, Main.rand.NextFloat(0.45f, 0.7f))
                ?.Configure(Main.rand.Next(40, 65));
        }
    }
}
