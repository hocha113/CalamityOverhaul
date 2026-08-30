using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.NPCs;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using CalamityOverhaul.Content.NPCs.FestersandSerpents;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaFesterSerpent
{
    /// <summary>
    /// 鬼奴·湖水版脓蕾沙蟒。单弹幕内部模拟一条中链变异蟒（头+12体+尾，放大 1.15），
    /// 贴图借 BSS 素材（与 boss 同源），血水衣之上再压一层坏死紫渍、囊肿节渗灼金。
    /// 出场为脓泡鼓爆：湖面先鼓起一个渗金的脓泡，泡破蟒出。
    /// 攻击一为灵液齐射（盘身两轮吐灵液痰，落点留小脓池，场上脓池封顶），
    /// 攻击二为囊爆掠身（锁线爆冲穿过目标，沿途囊肿节链式爆裂）。
    /// 链体力学复用 <see cref="SerpentChainMath"/>；联机同世吞契约：
    /// 状态走 ai[0..2]、owner 转场盖 netUpdate 章、链体各端本地重建、生命线只有 owner 判
    /// </summary>
    internal class KikasaFesterSerpentServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>囊爆掠身接触基伤（召唤加成前）</summary>
        internal const int SweepDamage = 580;

        /// <summary>灵液痰基伤（召唤加成前），痰弹幕消费</summary>
        internal const int GlobDamage = 300;

        /// <summary>小脓池触伤基伤（召唤加成前），痰按比例折算传给池</summary>
        internal const int PoolDamage = 160;

        /// <summary>同主人小脓池在场上限（场地经济不许铺满）</summary>
        internal const int PoolCap = 3;

        //==================== 链体尺寸 ====================

        internal const int SegCount = 14;
        internal const float DrawScale = 1.15f;
        /// <summary>节距 = boss 节距同值（borrow BSS 素材放大后的链距）</summary>
        internal const float SegSpacing = 46f;

        /// <summary>囊肿节：链序 3/6/9/12（与 boss 的 ordinal%3==2 同律，头尾恒否）</summary>
        internal static bool IsCystSeg(int i)
            => i > 0 && i < SegCount - 1 && (i - 1) % 3 == 2;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateIchorVolley = 2;
        private const int StateCystSweep = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：出水期=起跳横向符号；攻击期=相位号</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //脓泡鼓爆出水：鼓泡预兆→破泡拍→S 形爬升→落定
        private const int OmenEnd = 34;
        private const int RiseEnd = 86;
        private const int EmergeTotal = 108;
        private const int EmergeTimeout = 260;

        //灵液齐射：盘身后拉→两轮吐痰→回摆
        private const int CoilFrames = 24;
        private const int VolleyFrames = 24;
        private const int VolleyRecover = 16;
        private const int GlobsPerRound = 2;

        //囊爆掠身：锁线蓄力→爆冲连爆→硬刹收势
        private const int SweepWindup = 26;
        private const int SweepActive = 26;
        private const int SweepBrake = 18;

        private const int DissolvePerSegGap = 3;
        private const int DissolveSegFrames = 22;
        private const int DissolveTotal = (SegCount - 1) * DissolvePerSegGap + DissolveSegFrames + 10;

        //==================== 链体数据（各端本地重建，头位置由同步纠偏）====================

        private readonly Vector2[] spine = new Vector2[SegCount];
        /// <summary>蠕虫约定旋转（行进方向角 + PiOver2）；BSS 贴图前方朝下，绘制时减 π</summary>
        private readonly float[] segRot = new float[SegCount];
        private readonly float[] wetness = new float[SegCount];
        private readonly bool[] belowWater = new bool[SegCount];
        /// <summary>囊肿爆闪余辉（掠身连爆的逐节高光，本地表现）</summary>
        private readonly float[] cystFlash = new float[SegCount];
        private bool spineInit;

        //==================== 链体力学声明量（本地表现，状态逐帧声明）====================

        private float gatherLevel;
        private int waveKind = SerpentChainMath.WaveNone;
        private float waveAge;
        private float waveAmp;

        //==================== 本地表现量（不入同步）====================

        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool launchDone;
        private bool sweepRoared;
        private bool brakeDusted;
        private int volleyRoundsFired;
        /// <summary>掠身已爆囊肿位掩码（换场清零）</summary>
        private int cystPopMask;
        /// <summary>囊肿蓄光 0..1</summary>
        private float cystGlow;
        private float lockedHeadRot = float.NaN;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面（脓泡鼓起点）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SweepDamage);
            float dir = MathF.Sign(owner.Center.X - emergeAt.X);
            if (dir == 0f) {
                dir = owner.direction;
            }
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 60f), Vector2.Zero,
                ModContent.ProjectileType<KikasaFesterSerpentServant>(), damage, 7.5f, owner.whoAmI,
                ai2: dir);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1400;
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
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 180;
        }

        public override bool MinionContactDamage() => true;

        /// <summary>接触伤害只开在掠身窗，与可见的爆冲严格对齐</summary>
        public override bool? CanDamage()
            => State == StateCystSweep && (int)StateParam == 1 ? null : false;

        /// <summary>多节命中：相邻脊柱点两两线碰撞（放大体，线宽随 scale）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!spineInit) {
                return false;
            }
            float _ = 0f;
            for (int i = 1; i < SegCount; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    spine[i - 1], spine[i], 26f, ref _)) {
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
            //泡还没破就要收场：什么都没露出来，不演谢幕
            if (State == StateEmerge && StateTimer < OmenEnd) {
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
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SweepDamage);

            //换场清闩：远端可能靠收包换场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                sweepRoared = false;
                brakeDusted = false;
                volleyRoundsFired = 0;
                cystPopMask = 0;
                lockedHeadRot = float.NaN;
                gatherLevel = 0f;
            }

            if (!spineInit) {
                RebuildChain(-Vector2.UnitY);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateIchorVolley: UpdateIchorVolley(owner, authority); break;
                case StateCystSweep: UpdateCystSweep(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateChain(domain);
            UpdateDrips();
            cystGlow = MathF.Max(0f, cystGlow - 0.03f);
            for (int i = 0; i < SegCount; i++) {
                cystFlash[i] = MathF.Max(0f, cystFlash[i] - 0.06f);
            }
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            //沿链补光：血红里透一线灼金
            for (int i = 0; i < SegCount; i += 4) {
                Lighting.AddLight(spine[i], 0.20f, 0.12f, 0.06f);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 脓泡鼓爆出水 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            float dir = MathF.Sign(StateParam) == 0f ? 1f : MathF.Sign(StateParam);
            Vector2 boil = new(Projectile.Center.X, lakeY);

            if (t < OmenEnd) {
                //鼓泡预兆：水面隆一个渗金的泡，涟漪外推、金珠上蹿
                Projectile.velocity = Vector2.Zero;
                if (viewed) {
                    if (t % 5 == 2) {
                        KikasaDomainDeco.RippleAt(boil, 0.3f + t / (float)OmenEnd * 0.55f);
                    }
                    if (!Main.dedServ && t % 4 == 1) {
                        FssVfx.FesterTrickle(boil + new Vector2(Main.rand.NextFloat(-22f, 22f), -4f), 1.2f);
                    }
                    if (t == 12 || t == 27) {
                        SoundEngine.PlaySound(SoundID.Drip with {
                            Volume = 0.55f,
                            Pitch = t == 12 ? -0.7f : -0.4f,
                            MaxInstances = 2
                        }, boil);
                        ShakeViewer(t == 12 ? 0.8f : 1.4f);
                    }
                }
                return;
            }

            if (!launchDone) {
                //破泡拍：脓泡炸开，蟒头带着仰角自泡心钻出
                launchDone = true;
                Projectile.velocity = new Vector2(dir * 3.8f, -16.5f);
                FssVfx.Roar(Projectile.Center, -0.35f, 0.75f);
                if (viewed) {
                    BoilBurst(boil);
                }
            }

            if (t <= RiseEnd) {
                float riseT = t - OmenEnd;
                float weaveIn = MathHelper.Clamp(riseT / 12f, 0f, 1f);
                Projectile.velocity.Y = -16.5f * MathF.Exp(-0.048f * riseT);
                Projectile.velocity.X = dir * 3.8f * MathF.Exp(-0.03f * riseT)
                    + MathF.Sin(riseT * 0.14f + Seed) * 3.8f * weaveIn;
            }
            else {
                //落定：弯向主人侧下方的低位悬点
                Vector2 anchor = owner.Center + new Vector2(-owner.direction * 170f, 26f);
                Vector2 want = (anchor - Projectile.Center) * 0.06f;
                if (want.Length() > 11f) {
                    want = want.SafeNormalize(Vector2.Zero) * 11f;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
            }

            if (t >= EmergeTotal || t > EmergeTimeout) {
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 46;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>破泡浪冠：灼金迸溅掺腐沙与血珠，泡皮化雨</summary>
        private void BoilBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.4f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(52f, 0f), 1f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(52f, 0f), 1f);
            KikasaDomainDeco.SplashAt(hit, 11);
            FssVfx.IchorBurst(hit, 1.6f);
            FssVfx.CorruptSandBurst(hit, 0.8f);

            for (int i = 0; i < 14; i++) {
                float angle = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.85f, 0.85f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-26f, 26f), -4f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.45f, 0.8f))?.Configure(Main.rand.Next(22, 38));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.1f)
                ?.Configure(new Vector2(0.4f, 1f), -MathHelper.PiOver2, 0.42f, 11);

            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.6f, Pitch = -0.5f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.35f, MaxInstances = 2 }, hit);
            ShakeViewer(5.5f);
        }

        //==================== 沉重巡曳跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            //比荒花更沉的巡曳：频率更低、幅面更宽，读作更庞大的身躯
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 165f, 26f);
            float w = (float)StateTimer * 0.022f + Seed;
            anchor += new Vector2(MathF.Sin(w) * 205f, MathF.Sin(w * 2f + Seed * 2f) * 34f);

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildChain(Vector2.UnitX * owner.direction);
                Projectile.netUpdate = authority;
                return;
            }
            float maxSpeed = to.Length() > 1400f ? 23f : 13f;
            Vector2 desired = to * 0.065f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.09f);
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin((float)StateTimer * 0.14f + Seed * 3f) * 0.045f);
            if (Projectile.velocity.Length() < 2.2f) {
                Projectile.velocity += (w * 2.2f).ToRotationVector2() * 0.4f;
            }

            //体表持续渗漏：暗沙细流掺灵液珠（变异体常态）
            if (!Main.dedServ && Main.rand.NextBool(7)) {
                FssVfx.FesterTrickle(spine[Main.rand.Next(SegCount)], 0.7f);
            }

            //出手裁决：灵液齐射与囊爆掠身交替，owner 盖章
            int target = FindTarget(owner);
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 40) {
                attackIndex++;
                State = attackIndex % 2 == 1 ? StateIchorVolley : StateCystSweep;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 灵液齐射 ====================

        private void UpdateIchorVolley(Player owner, bool authority) {
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);
            Vector2 aimPos = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 8f
                : Projectile.Center + (float.IsNaN(lockedHeadRot) ? Vector2.UnitX : lockedHeadRot.ToRotationVector2()) * 400f;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //盘身后拉：头锁猎物，喉底金珠向口器汇聚
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                Vector2 aimDir = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                float wantAngle = aimDir.ToRotation();
                lockedHeadRot = float.IsNaN(lockedHeadRot) ? wantAngle
                    : lockedHeadRot.AngleTowards(wantAngle, 0.2f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -aimDir * 2.4f, 0.12f);
                Projectile.velocity = Projectile.velocity.RotatedBy(
                    MathF.Sin(t * 0.4f + Seed) * 0.09f);
                gatherLevel = MathHelper.Clamp(t / (float)CoilFrames, 0f, 1f) * 0.6f;

                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.35f, Pitch = -0.75f, MaxInstances = 2 }, Projectile.Center);
                }
                if (!Main.dedServ && t % 3 == 0 && t < CoilFrames * 0.75f) {
                    Vector2 mouth = MouthPos();
                    Dust gold = Dust.NewDustPerfect(
                        mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 70f),
                        DustID.Ichor, Vector2.Zero, 40, default, Main.rand.NextFloat(0.8f, 1.1f));
                    gold.velocity = (mouth - gold.position) * 0.14f;
                    gold.noGravity = true;
                }
                if (t >= CoilFrames) {
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //两轮吐痰：t=2 与 t=12 各一口，后坐鞭甩传下链
                Projectile.velocity *= 0.9f;
                if ((t == 2 || t == 12) && volleyRoundsFired < 2) {
                    volleyRoundsFired++;
                    Vector2 aimDir = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    lockedHeadRot = aimDir.ToRotation();
                    Projectile.velocity = -aimDir * 7f;
                    Projectile.netUpdate = authority;
                    PulseGapWave(SerpentChainMath.WaveRelease, 0.35f);

                    Vector2 mouth = MouthPos();
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 3 }, mouth);
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.4f, Pitch = -0.55f, MaxInstances = 3 }, mouth);
                    if (!Main.dedServ) {
                        FssVfx.IchorBurst(mouth, 0.9f, aimDir);
                        PRTLoader.NewParticle<PRT_DWave>(mouth + aimDir * 10f, Vector2.Zero,
                            IchorDeepColor, 0.07f)?.Configure(new Vector2(0.55f, 1f), aimDir.ToRotation(), 0.24f, 8);
                    }
                    if (ViewedOwner) {
                        ShakeViewer(1.7f);
                    }
                    if (authority) {
                        int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(GlobDamage);
                        for (int k = 0; k < GlobsPerRound; k++) {
                            float off = (k - 0.5f) * 0.2f + Main.rand.NextFloat(-0.04f, 0.04f);
                            Vector2 vel = aimDir.RotatedBy(off) * 12f;
                            //痰是抛出去的：上抛偏置配合弹体重力走弧线
                            vel.Y -= 2.2f;
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), mouth, vel,
                                ModContent.ProjectileType<KikasaFesterGlob>(), damage, 3f, Projectile.owner);
                        }
                    }
                }
                if (t >= VolleyFrames) {
                    NextPhase(2);
                }
                return;
            }

            //回摆
            Projectile.velocity *= 0.92f;
            if (t >= VolleyRecover) {
                EndAttack(authority, 115);
            }
        }

        //==================== 囊爆掠身 ====================

        private void UpdateCystSweep(Player owner, bool authority) {
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //锁线蓄力：全身后拉，囊肿逐帧鼓胀发亮
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                NPC npc = Main.npc[target];
                Vector2 aim = npc.Center + npc.velocity * 9f;
                Vector2 aimDir = (aim - Projectile.Center).SafeNormalize(Vector2.UnitX);
                lockedHeadRot = float.IsNaN(lockedHeadRot) ? aimDir.ToRotation()
                    : lockedHeadRot.AngleTowards(aimDir.ToRotation(), 0.22f);

                float progress = MathHelper.Clamp(t / (float)SweepWindup, 0f, 1f);
                float late = MathF.Pow(progress, 6f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -aimDir * (2.5f + 11f * late), 0.25f);
                gatherLevel = progress;
                cystGlow = MathF.Max(cystGlow, progress);

                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.4f, Pitch = -0.65f, MaxInstances = 2 }, Projectile.Center);
                }
                if (!Main.dedServ && t % 3 == 1 && t < SweepWindup * 0.75f) {
                    //随机一枚囊肿渗漏加剧（3/6/9/12 全为合法囊肿位）
                    FssVfx.FesterTrickle(spine[3 + Main.rand.Next(4) * 3], 1.4f);
                }

                if (t >= SweepWindup) {
                    Projectile.velocity = aimDir * 29f;
                    gatherLevel = 0f;
                    PulseGapWave(SerpentChainMath.WaveRelease, 0.5f);
                    NextPhase(1);
                    if (!sweepRoared) {
                        sweepRoared = true;
                        FssVfx.Roar(Projectile.Center, -0.2f, 0.85f);
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = -0.25f, MaxInstances = 2 }, Projectile.Center);
                    }
                    if (ViewedOwner) {
                        ShakeViewer(3f);
                    }
                }
                return;
            }

            if (phase == 1) {
                //爆冲连爆：复利续力，囊肿沿链序错帧爆裂
                lockedHeadRot = float.NaN;
                Projectile.velocity *= 1.012f;
                if (Projectile.velocity.Length() > 36f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 36f;
                }
                cystGlow = 1f;

                //链式爆裂：第 n 枚囊肿在 t = 4 + n*5 爆（确定性节拍，各端一致）
                int order = 0;
                for (int i = 0; i < SegCount; i++) {
                    if (!IsCystSeg(i)) {
                        continue;
                    }
                    if (t >= 4 + order * 5 && (cystPopMask & 1 << i) == 0) {
                        cystPopMask |= 1 << i;
                        PopCyst(i, order);
                    }
                    order++;
                }

                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    FssVfx.FesterTrickle(
                        Projectile.Center - Projectile.velocity * Main.rand.NextFloat(0.3f, 1.2f)
                            + Main.rand.NextVector2Circular(14f, 14f), 1.2f);
                }

                bool passed = target >= 0 && t > 8
                    && Vector2.Dot(Main.npc[target].Center - Projectile.Center,
                        Projectile.velocity.SafeNormalize(Vector2.UnitX)) < -70f;
                if (passed || t > SweepActive) {
                    PulseGapWave(SerpentChainMath.WavePress, 0.45f);
                    NextPhase(2);
                }
                return;
            }

            //硬刹收势
            Projectile.velocity *= t <= 5 ? 0.72f : 0.9f;
            if (!brakeDusted) {
                brakeDusted = true;
                if (!Main.dedServ && ViewedOwner) {
                    FssVfx.IchorBurst(spine[0], 0.7f, -Projectile.velocity.SafeNormalize(Vector2.UnitY));
                }
            }
            if (t >= SweepBrake) {
                EndAttack(authority, 125);
            }
        }

        /// <summary>单枚囊肿爆裂：灼金迸溅 + 扩散环 + 音高沿链爬升（纯表现，伤害走接触窗）</summary>
        private void PopCyst(int segIndex, int order) {
            cystFlash[segIndex] = 1f;
            SoundEngine.PlaySound(SoundID.NPCDeath13 with {
                Volume = 0.45f,
                Pitch = -0.3f + order * 0.12f,
                MaxInstances = 3
            }, spine[segIndex]);
            if (Main.dedServ) {
                return;
            }
            FssVfx.IchorBurst(spine[segIndex], 1.15f);
            PRTLoader.NewParticle<PRT_DWave>(spine[segIndex], Vector2.Zero, IchorDeepColor, 0.08f)
                ?.Configure(new Vector2(0.8f, 1f), Main.rand.NextFloat(MathHelper.TwoPi), 0.3f, 9);
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    spine[segIndex] + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    BloodDeep * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(14, 24));
            }
            if (ViewedOwner) {
                ShakeViewer(1.2f);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            lockedHeadRot = float.NaN;
            gatherLevel = 0f;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解遣返 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;

            if (lakeAlive) {
                Projectile.velocity.X *= 0.94f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 8f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            //化水残珠沿链错拍，囊肿位瘪泄金珠
            if (!Main.dedServ && t % 3 == 0) {
                int i = Main.rand.Next(SegCount);
                float dissolve = SegDissolve(i);
                if (dissolve is > 0.1f and < 0.9f) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        spine[i] + Main.rand.NextVector2Circular(18f, 18f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.2f, 2.6f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(14, 24));
                    if (IsCystSeg(i)) {
                        FssVfx.FesterTrickle(spine[i], 1.6f);
                    }
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

        //==================== 链体推进（阻尼追踪 + SerpentChainMath 力学）====================

        private void RebuildChain(Vector2 headDir) {
            spineInit = true;
            Vector2 head = Projectile.Center;
            Vector2 back = -headDir.SafeNormalize(Vector2.UnitX);
            float wormRot = headDir.ToRotation() + MathHelper.PiOver2;
            float lakeY = Owner?.active == true && Owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                ? domain.LakeWorldY : float.MaxValue;
            for (int i = 0; i < SegCount; i++) {
                spine[i] = head + back * (i * SegSpacing);
                segRot[i] = wormRot;
                belowWater[i] = spine[i].Y >= lakeY;
                wetness[i] = 1f;
            }
        }

        /// <summary>触发一记行进肌肉波（出手释放/急刹追压）</summary>
        private void PulseGapWave(int kind, float amp) {
            waveKind = kind;
            waveAge = 0f;
            waveAmp = amp;
        }

        private void UpdateChain(KikasaDomainPlayer domain) {
            Vector2 head = Projectile.Center + Projectile.velocity;

            if (Vector2.Distance(spine[0], head) > 150f) {
                RebuildChain(Projectile.velocity.SafeNormalize(Vector2.UnitX));
                return;
            }

            spine[0] = head;
            if (!float.IsNaN(lockedHeadRot)) {
                segRot[0] = lockedHeadRot + MathHelper.PiOver2;
            }
            else if (Projectile.velocity.Length() > 0.5f) {
                segRot[0] = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            if (waveKind != SerpentChainMath.WaveNone) {
                waveAge++;
                if (waveAge > SegCount * 2.2f + 26f) {
                    waveKind = SerpentChainMath.WaveNone;
                }
            }
            float headSpeed = Projectile.velocity.Length();

            for (int i = 1; i < SegCount; i++) {
                Vector2 front = spine[i - 1];
                Vector2 segmentTarget = front - spine[i];

                if (segRot[i - 1] != segRot[i]) {
                    float stiffness = SerpentChainMath.StiffnessFactor(i, SegCount);
                    segmentTarget = segmentTarget.RotatedBy(
                        MathHelper.WrapAngle(segRot[i - 1] - segRot[i]) * stiffness);
                    segmentTarget = segmentTarget.MoveTowards(
                        (segRot[i - 1] - segRot[i]).ToRotationVector2(), 1f);
                }

                float maxBend = SerpentChainMath.MaxBendAngle(i);
                if (maxBend < MathHelper.Pi && segmentTarget.LengthSquared() > 1f) {
                    float frontAxis = segRot[i - 1] - MathHelper.PiOver2;
                    float bend = MathHelper.WrapAngle(segmentTarget.ToRotation() - frontAxis);
                    if (MathF.Abs(bend) > maxBend) {
                        float clamped = frontAxis + MathF.Sign(bend) * maxBend;
                        segmentTarget = clamped.ToRotationVector2() * segmentTarget.Length();
                    }
                }

                segRot[i] = segmentTarget.ToRotation() + MathHelper.PiOver2;

                float gap = SegSpacing;
                gap *= SerpentChainMath.GatherFactor(i, gatherLevel);
                gap *= SerpentChainMath.GapWaveFactor(i, waveKind, waveAge, waveAmp);
                gap *= SerpentChainMath.SpeedStretchFactor(headSpeed);
                spine[i] = front - segmentTarget.SafeNormalize(Vector2.Zero) * gap;
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
                        KikasaDomainDeco.RippleAt(hit, i == 0 ? 0.9f : 0.5f);
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                hit + new Vector2(Main.rand.NextFloat(-14f, 14f), -3f),
                                new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(2f, 4.4f)),
                                BloodMain * 0.6f, Main.rand.NextFloat(0.35f, 0.6f))
                                ?.Configure(Main.rand.Next(14, 26));
                        }
                        if (soundLeft) {
                            soundLeft = false;
                            SoundEngine.PlaySound(SoundID.SplashWeak with {
                                Volume = 0.42f,
                                Pitch = -0.35f + i * 0.015f,
                                MaxInstances = 3
                            }, hit);
                        }
                    }
                }
                wetness[i] = below ? 1f : MathF.Max(0f, wetness[i] - 0.011f);
            }
        }

        /// <summary>湿度驱动滴落：湿节淌血珠，囊肿节改渗金</summary>
        private void UpdateDrips() {
            if (Main.dedServ) {
                return;
            }
            int budget = 2;
            for (int k = 0; k < 3 && budget > 0; k++) {
                int i = Main.rand.Next(SegCount);
                if (belowWater[i] || wetness[i] < 0.1f) {
                    continue;
                }
                if (Main.rand.NextFloat() > wetness[i] * 0.42f) {
                    continue;
                }
                budget--;
                if (IsCystSeg(i) && Main.rand.NextBool(3)) {
                    FssVfx.FesterTrickle(spine[i], 1f);
                    continue;
                }
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    spine[i] + Main.rand.NextVector2Circular(20f, 15f),
                    new Vector2(Projectile.velocity.X * 0.05f, Main.rand.NextFloat(0.8f, 1.8f)),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.35f, 0.58f))?.Configure(Main.rand.Next(16, 30), 0.3f);
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
            float bestDist = 1150f;
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

        private Vector2 MouthPos()
            => spine[0] + (segRot[0] - MathHelper.PiOver2).ToRotationVector2() * 28f;

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 血系配色（CoolTint 家族，灼金做次要点缀）====================

        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        /// <summary>湖化灼金：灵液高光的鬼奴变调</summary>
        internal static Color GhostIchor => KikasaDomain.CoolTint(new(232, 186, 82), new(170, 172, 140));
        /// <summary>湖化深琥珀：灵液打底</summary>
        internal static Color IchorDeepColor => KikasaDomain.CoolTint(new(168, 112, 34), new(120, 118, 92));
        /// <summary>坏死紫渍：血水衣上的变异压色（乘色语义，手染回退同源）</summary>
        private static readonly Color NecroWash = new(172, 152, 205);

        //==================== 绘制 ====================

        private int SegNpcType(int i)
            => i == 0 ? ModContent.NPCType<BssHead>()
            : i == SegCount - 1 ? ModContent.NPCType<BssTail>()
            : ModContent.NPCType<BssBody>();

        private void GetSegDraw(int i, out Texture2D tex, out Rectangle frame) {
            int type = SegNpcType(i);
            Main.instance.LoadNPC(type);
            tex = TextureAssets.Npc[type].Value;
            int frames = Math.Max(1, Main.npcFrameCount[type]);
            int frameHeight = tex.Height / frames;
            //体节两款式：囊肿节用款式2 帧（借红花帧位当疮口）
            int frameY = frames > 1 && IsCystSeg(i) ? frameHeight : 0;
            frame = new Rectangle(0, frameY, tex.Width, frameHeight);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!spineInit) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            //本体：血湖材质逐节 + 坏死紫渍压色
            DrawChain(sb, lightColor);

            //辉光层：出水脓泡金光 / 湿面反光 / 囊肿蓄光与爆闪
            DrawGlowLayer(sb);

            return false;
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

            int emergeT = State == StateEmerge ? (int)StateTimer : int.MaxValue;

            //尾→头，头压顶层
            for (int i = SegCount - 1; i >= 0; i--) {
                float dissolve = SegDissolve(i);
                if (dissolve >= 1f) {
                    continue;
                }
                GetSegDraw(i, out Texture2D tex, out Rectangle frame);
                Vector2 pos = spine[i] - Main.screenPosition;
                float rot = segRot[i] - MathHelper.Pi;

                Color color;
                if (shaderOk) {
                    float steady = MathHelper.Clamp(0.30f + wetness[i] * 0.15f
                        + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + Seed + i * 0.8f) * 0.04f, 0f, 0.6f);
                    float segForm = steady;
                    if (emergeT != int.MaxValue) {
                        float condense = MathHelper.Clamp((emergeT - OmenEnd - i * 2f) / 40f, 0f, 1f);
                        segForm = MathHelper.Lerp(0.9f, steady, condense * condense * (3f - 2f * condense));
                    }
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
                    color = Color.Lerp(lightColor, BloodMain, 0.5f) * (1f - dissolve);
                }

                sb.Draw(tex, pos, frame, color, rot, frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //坏死紫渍：血水体上薄薄压一层变异色，脓蕾与荒花的分家读数
            for (int i = SegCount - 1; i >= 0; i--) {
                float dissolve = SegDissolve(i);
                if (dissolve >= 1f) {
                    continue;
                }
                GetSegDraw(i, out Texture2D tex, out Rectangle frame);
                Color wash = NecroWash with { A = 150 };
                sb.Draw(tex, spine[i] - Main.screenPosition, frame,
                    wash * (0.34f * (1f - dissolve)),
                    segRot[i] - MathHelper.Pi, frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>辉光层：出水脓泡金光 + 湿节水膜反光 + 囊肿蓄光/爆闪</summary>
        private void DrawGlowLayer(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //湿节水膜
            Color sheen = BloodBright with { A = 0 };
            for (int i = SegCount - 1; i >= 0; i--) {
                if (wetness[i] < 0.45f || SegDissolve(i) >= 1f) {
                    continue;
                }
                GetSegDraw(i, out Texture2D tex, out Rectangle frame);
                float a = 0.10f * wetness[i] * (1f - SegDissolve(i));
                sb.Draw(tex, spine[i] - Main.screenPosition, frame, sheen * a,
                    segRot[i] - MathHelper.Pi, frame.Size() * 0.5f, DrawScale * 1.03f, SpriteEffects.None, 0f);
            }

            //囊肿灼金：蓄光常亮 + 爆闪高光
            Color gold = GhostIchor with { A = 0 };
            for (int i = 0; i < SegCount; i++) {
                if (!IsCystSeg(i) || SegDissolve(i) >= 1f) {
                    continue;
                }
                float glow = MathF.Max(cystGlow * (0.7f + 0.3f * MathF.Sin(
                    Main.GlobalTimeWrappedHourly * 12f + i * 1.1f)), cystFlash[i]);
                if (glow < 0.04f) {
                    continue;
                }
                GetSegDraw(i, out Texture2D tex, out Rectangle frame);
                sb.Draw(tex, spine[i] - Main.screenPosition, frame,
                    gold * (0.55f * glow * (1f - SegDissolve(i))),
                    segRot[i] - MathHelper.Pi, frame.Size() * 0.5f,
                    DrawScale * (1.05f + cystFlash[i] * 0.08f), SpriteEffects.None, 0f);
                Lighting.AddLight(spine[i], GhostIchor.ToVector3() * 0.3f * glow);
            }

            //出水脓泡：泡心金光自水下鼓起
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            if (softGlow != null && State == StateEmerge && StateTimer < OmenEnd && ViewedOwner
                && Owner.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                float ot = MathHelper.Clamp(StateTimer / (float)OmenEnd, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(40f, 4f, ease));
                float r = 34f + 30f * ease;
                sb.Draw(softGlow, pos - Main.screenPosition, null, (GhostIchor with { A = 0 }) * (0.4f * ease), 0f,
                    softGlow.Size() * 0.5f,
                    new Vector2(r * 2.8f / softGlow.Width, r * 1.1f / softGlow.Height), SpriteEffects.None, 0f);
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
            //掠身穿体：灼金迸溅掺血珠
            FssVfx.IchorBurst(target.Center, 1f, Projectile.velocity.SafeNormalize(Vector2.UnitX));
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(24f, 24f),
                    Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(2.6f, 2.6f),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(16, 26), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 3 }, target.Center);
            if (ViewedOwner) {
                ShakeViewer(2.2f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !spineInit) {
                return;
            }
            //谢幕残珠沿链散，囊肿位各泄一撮金
            for (int i = 0; i < SegCount; i += 2) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    spine[i] + Main.rand.NextVector2Circular(14f, 14f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.4f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            for (int i = 0; i < SegCount; i++) {
                if (IsCystSeg(i)) {
                    FssVfx.FesterTrickle(spine[i], 1.8f);
                }
            }
        }
    }
}
