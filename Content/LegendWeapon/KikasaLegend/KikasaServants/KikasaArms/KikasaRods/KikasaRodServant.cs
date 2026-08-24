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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaRods
{
    /// <summary>
    /// 械奴·湖水钓翁（通用钓奴，独钓一竿）。血湖既是家也是渔场：
    /// 抛竿入湖→候鱼（浮标轻沉浮）→咬钩预告（浮标连点三下）→起竿，
    /// 从湖里拽出渔获甩向猎物（<see cref="KikasaRodCatch"/>：罐头/旧靴/鱼，
    /// 每第四竿起大物整条旗鱼）。没有猎物就安静垂钓——独钓寒江的摆件。
    /// 强度全押渔力（档案 CatchDamage 由 fishingPole 推得，各端确定性一致），
    /// 联机契约与通用械奴同构：owner 裁决转场、渔获只在 authority 生成
    /// </summary>
    internal class KikasaRodServant : ModProjectile, IKikasaArmsServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>大物倍率（每第四竿）</summary>
        internal const float BigCatchMul = 2.4f;

        //==================== 档案 ====================

        private int armsItemType = ItemID.WoodFishingPole;

        public int ArmsItemType => armsItemType;

        private KikasaRodProfile? profileCache;

        private KikasaRodProfile Profile => profileCache ??= KikasaArmsProfiler.RodProfileOf(armsItemType);

        private void SetArmsItemType(int itemType) {
            armsItemType = itemType;
            profileCache = null;
        }

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFish = 1;
        private const int StateDissolve = 2;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：保位与通用械奴同构，当前未用</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        private const int OmenFrames = 26;
        private const int RiseEnd = 56;
        private const int FormupFrame = 60;
        private const int EmergeTotal = 76;

        //垂钓循环：抛竿→候鱼（档案节拍）→咬钩预告→起竿
        private const int CastLen = 14;
        private const int BiteLen = 16;
        private const int PullLen = 20;

        /// <summary>一轮垂钓总长：候鱼段长度走档案（渔力越高候得越短）</summary>
        private int CycleLen => CastLen + Profile.CastPeriod + BiteLen + PullLen;

        private int BiteStart => CastLen + Profile.CastPeriod;

        private int PullFrame => BiteStart + BiteLen;

        private const int DissolveFrames = 70;

        //==================== 竿体本地模拟 ====================

        private Vector2 rodPos;
        private Vector2 rodVel;
        private Vector2 rodTarget;
        /// <summary>竿轴世界向（竿梢方向），绘制时补 π/4 斜置</summary>
        private float rodRot;
        private float rodSpin;
        private bool rodInit;

        /// <summary>持竿朝向：+1 右 -1 左（跟猎物/主人换边，转竿不镜像）</summary>
        private int rodFacing = 1;

        //==================== 本地表现量 ====================

        private bool breachDone;
        private int lastSeenState = -1;
        private bool formSnapDone;
        private bool dissolveSplashed;
        /// <summary>本轮起竿闩（快照回卷不重甩）</summary>
        private int lastPullCycle = -1;
        /// <summary>渔获计数：皮肤轮换与大物节拍（owner 端生成时消费）</summary>
        private int catchIndex;
        /// <summary>本轮是否有鱼上钩：咬钩预告帧由 FindTarget 确定性判定</summary>
        private bool cycleBiting;
        private int lastBiteCycle = -1;
        /// <summary>抛竿水花闩</summary>
        private int lastPlopCycle = -1;

        private Player Owner => Main.player[Projectile.owner];
        private float Seed => Projectile.identity * 0.6173f;

        //==================== 召唤入口 ====================

        internal static void Summon(Player owner, Vector2 emergeAt, int count, int itemType) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            KikasaRodProfile profile = KikasaArmsProfiler.RodProfileOf(itemType);
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(profile.CatchDamage);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaRodServant>(), damage, 2f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaRodServant rod) {
                rod.SetArmsItemType(itemType);
                Main.projectile[index].netUpdate = true;
            }
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1200;
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
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int itemType = reader.ReadInt32();
            if (itemType > ItemID.None && itemType < ItemLoader.ItemCount && itemType != armsItemType) {
                SetArmsItemType(itemType);
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
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(Profile.CatchDamage);

            if (State != lastSeenState) {
                lastSeenState = State;
                lastPullCycle = -1;
                lastBiteCycle = -1;
                lastPlopCycle = -1;
                cycleBiting = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                }
            }

            if (!rodInit) {
                RebuildRod(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFish: UpdateFish(owner, domain, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateRod(owner, domain);

            float glow = RodAlpha() * 0.3f;
            if (glow > 0.02f) {
                Lighting.AddLight(rodPos, 0.32f * glow, 0.24f * glow, 0.3f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                Projectile.velocity = Vector2.Zero;
                if (viewed && t % 5 == 2) {
                    float converge = 1f - t / (float)OmenFrames;
                    float wobble = MathF.Sin(t * 0.5f) * converge * 22f;
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X + wobble, lakeY),
                        0.3f + (1f - converge) * 0.4f);
                }
                if (viewed && (t == 6 || t == 16)) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.4f, Pitch = -0.55f + t * 0.012f, MaxInstances = 3
                    }, new Vector2(Projectile.Center.X, lakeY));
                }
                return;
            }

            if (!breachDone) {
                breachDone = true;
                rodVel = new Vector2(0f, -11.4f);
                rodSpin = 0.26f;
                Projectile.velocity = new Vector2(0f, -3f);
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.68f, Pitch = -0.38f, MaxInstances = 3
                }, rodPos);
                if (viewed) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 1.3f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, lakeY), 7);
                    for (int k = 0; k < 10; k++) {
                        float angle = -MathHelper.Pi * (0.2f + 0.6f * k / 9f);
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            new Vector2(Projectile.Center.X + Main.rand.NextFloat(-12f, 12f), lakeY - 4f),
                            angle.ToRotationVector2() * Main.rand.NextFloat(2.4f, 5.4f),
                            BloodMain * Main.rand.NextFloat(0.45f, 0.62f),
                            Main.rand.NextFloat(0.38f, 0.62f))
                            ?.Configure(Main.rand.Next(18, 28), Main.rand.NextFloat(-0.4f, 0.4f));
                    }
                    ShakeViewer(1.3f);
                }
            }

            Projectile.velocity *= 0.96f;

            if (viewed && t < RiseEnd && t % 3 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    rodPos + new Vector2(Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(2f, 12f)),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.2f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.32f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 22), 0f);
            }

            //定竿拍：轻一顿，线轮咔嗒
            if (!formSnapDone && t >= FormupFrame) {
                formSnapDone = true;
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 2 }, Projectile.Center);
                rodVel += new Vector2(0f, -1f);
                if (viewed) {
                    ShakeViewer(1.2f);
                }
            }

            if (t >= EmergeTotal) {
                State = StateFish;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        //==================== 垂钓循环 ====================

        /// <summary>浮标锚位：钓翁前方湖面上（跟持竿朝向），各端确定性一致</summary>
        private Vector2 BobberAnchor(KikasaDomainPlayer domain)
            => new(Projectile.Center.X + rodFacing * 108f + MathF.Sin(Seed * 3.1f) * 14f,
                domain.LakeWorldY);

        /// <summary>浮标当前位（含抛竿飞行、候鱼沉浮、咬钩连点、大物深沉）</summary>
        private Vector2 BobberPos(KikasaDomainPlayer domain) {
            Vector2 anchor = BobberAnchor(domain);
            if (State != StateFish) {
                return anchor;
            }
            int t = (int)StateTimer;
            int phase = t % CycleLen;

            //抛竿段：浮标从竿梢飞向锚位的弧
            if (phase < CastLen) {
                float p = phase / (float)CastLen;
                Vector2 tip = RodTipPos();
                Vector2 arc = Vector2.Lerp(tip, anchor, p);
                arc.Y -= MathF.Sin(p * MathHelper.Pi) * 46f;
                return arc;
            }
            //起竿段：浮标被拽出水面回收
            if (phase >= PullFrame) {
                float p = (phase - PullFrame) / (float)PullLen;
                Vector2 tip = RodTipPos();
                Vector2 arc = Vector2.Lerp(anchor, tip, MathF.Pow(p, 0.7f));
                arc.Y -= MathF.Sin(p * MathHelper.Pi) * 30f;
                return arc;
            }
            //候鱼：轻沉浮；咬钩：连点下沉（大物沉更深）
            float bob = MathF.Sin(t * 0.09f + Seed) * 2.5f;
            if (phase >= BiteStart && cycleBiting) {
                float biteT = (phase - BiteStart) / (float)BiteLen;
                float dip = MathF.Abs(MathF.Sin(biteT * MathHelper.Pi * 3f));
                bob += dip * (IsBigCycle() ? 15f : 7f);
            }
            return anchor + new Vector2(0f, bob);
        }

        /// <summary>当前轮是否大物轮（第四竿起大物）：owner 的 catchIndex 只影响生成，
        /// 表现端用轮次数确定性判定</summary>
        private bool IsBigCycle() => (int)StateTimer / CycleLen % 4 == 3;

        private void UpdateFish(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int phase = t % CycleLen;
            int cycle = t / CycleLen;
            bool viewed = ViewedOwner;

            //钓翁驻位：主人身侧偏后，稳稳当当
            Vector2 anchor = owner.Center + new Vector2(-rodFacing * 24f, -34f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.5f + Seed) * 4f;
            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildRod(domain);
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.09f;
            if (desired.Length() > 16f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 16f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);

            //持竿朝向：跟最近猎物换边，没猎物跟主人朝向
            int target = FindTarget(owner);
            if (phase == 1) {
                //只在抛竿起点换边，钓到一半不扭头
                rodFacing = target >= 0
                    ? MathF.Sign(Main.npc[target].Center.X - Projectile.Center.X) >= 0 ? 1 : -1
                    : owner.direction;
            }

            //抛竿落水的一声扑通
            if (phase == CastLen && cycle > lastPlopCycle) {
                lastPlopCycle = cycle;
                Vector2 plop = BobberAnchor(domain);
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.3f, Pitch = 0.3f, MaxInstances = 3
                }, plop);
                if (viewed) {
                    KikasaDomainDeco.RippleAt(plop, 0.6f);
                }
            }

            //咬钩判定：预告起点一次性确定性裁决（各端同规则同帧）
            if (phase == BiteStart && cycle > lastBiteCycle) {
                lastBiteCycle = cycle;
                cycleBiting = target >= 0;
                if (cycleBiting) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.42f, Pitch = IsBigCycle() ? -0.6f : -0.2f, MaxInstances = 3
                    }, BobberAnchor(domain));
                }
            }

            //咬钩连点的涟漪
            if (viewed && cycleBiting && phase > BiteStart && phase < PullFrame && phase % 5 == 0) {
                KikasaDomainDeco.RippleAt(BobberAnchor(domain), IsBigCycle() ? 0.8f : 0.45f);
            }

            //起竿：有鱼甩渔获，空竿轻拉回
            if (phase == PullFrame && cycle > lastPullCycle) {
                lastPullCycle = cycle;
                if (cycleBiting) {
                    PullCatch(owner, domain, authority, target);
                }
                else {
                    //空竿：轻一声收线
                    SoundEngine.PlaySound(SoundID.Unlock with {
                        Volume = 0.22f, Pitch = 0.35f, MaxInstances = 2
                    }, Projectile.Center);
                }
            }
        }

        /// <summary>起竿甩渔获：从浮标位把湖底的东西拽出来砸向猎物</summary>
        private void PullCatch(Player owner, KikasaDomainPlayer domain, bool authority, int target) {
            bool big = IsBigCycle();
            Vector2 from = BobberAnchor(domain);
            rodSpin = -rodFacing * (big ? 0.3f : 0.18f);

            SoundEngine.PlaySound(Profile.CastSound with {
                Volume = big ? 0.55f : 0.36f, Pitch = big ? -0.35f : 0.1f, MaxInstances = 3
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = big ? 0.6f : 0.35f, Pitch = big ? -0.4f : -0.1f, MaxInstances = 3
            }, from);
            if (ViewedOwner) {
                KikasaDomainDeco.SplashAt(from, big ? 9 : 5);
                KikasaDomainDeco.RippleAt(from, big ? 1.5f : 0.9f);
                ShakeViewer(big ? 2.6f : 0.9f);
            }
            if (!Main.dedServ) {
                int burst = big ? 10 : 6;
                for (int k = 0; k < burst; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(3f, big ? 9f : 6f)),
                        BloodMain * Main.rand.NextFloat(0.45f, 0.65f),
                        Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(16, 28), Main.rand.NextFloat(-0.3f, 0.3f));
                }
            }

            if (!authority) {
                return;
            }
            Vector2 aim = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                : Projectile.Center + new Vector2(rodFacing * 300f, -60f);
            float dist = Vector2.Distance(from, aim);
            Vector2 vel = (aim - from).SafeNormalize(Vector2.UnitX) * (big ? 12.5f : 14f);
            //出水抬升 + 抛物补偿
            vel.Y -= 3.5f + dist * 0.0022f;
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                .ApplyTo(Profile.CatchDamage * (big ? BigCatchMul : 1f));
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, vel,
                ModContent.ProjectileType<KikasaRodCatch>(), damage, big ? 6f : 3f, Projectile.owner,
                catchIndex % 6, big ? 1f : 0f);
            catchIndex++;
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

            if (lakeAlive && !dissolveSplashed && rodPos.Y >= lakeY) {
                dissolveSplashed = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.5f, Pitch = -0.4f, MaxInstances = 3
                }, rodPos);
                if (ViewedOwner) {
                    Vector2 hit = new(rodPos.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 5);
                    KikasaDomainDeco.RippleAt(hit, 0.8f);
                    ShakeViewer(0.9f);
                }
            }

            if (!Main.dedServ && RodAlpha() > 0.15f && t % 2 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    rodPos + Main.rand.NextVector2Circular(18f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.8f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 22), 0f);
            }

            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
            }
        }

        //==================== 竿体推进 ====================

        private void RebuildRod(KikasaDomainPlayer domain) {
            rodInit = true;
            if (State == StateEmerge) {
                rodPos = new Vector2(Projectile.Center.X, domain.LakeWorldY + 26f);
                rodRot = -MathHelper.PiOver2;
            }
            else {
                rodPos = Projectile.Center + new Vector2(0f, -6f);
                rodRot = RestRot();
            }
            rodVel = Vector2.Zero;
            rodSpin = 0f;
            rodTarget = rodPos;
        }

        /// <summary>持竿歇姿：竿梢朝前上方 35 度</summary>
        private float RestRot()
            => rodFacing > 0 ? -0.61f : MathHelper.Pi + 0.61f;

        private void ChaseRod(float accel, float damp) {
            rodVel = (rodVel + (rodTarget - rodPos) * accel) * damp;
            rodPos += rodVel;
        }

        private float Sway(float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed) * amp;

        private void UpdateRod(Player owner, KikasaDomainPlayer domain) {
            if (!rodInit) {
                return;
            }
            int t = (int)StateTimer;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    if (t < OmenFrames) {
                        rodPos = new Vector2(Projectile.Center.X, lakeY + 26f);
                        rodVel = Vector2.Zero;
                        rodTarget = rodPos;
                        rodRot = -MathHelper.PiOver2;
                        break;
                    }
                    rodTarget = new Vector2(Projectile.Center.X, lakeY - 92f + Sway(2.1f, 8f));
                    int lt = t - OmenFrames;
                    if (lt < 14) {
                        rodVel.Y *= 0.955f;
                        rodVel.X *= 0.98f;
                        rodPos += rodVel;
                        rodRot += rodSpin;
                        rodSpin *= 0.93f;
                    }
                    else {
                        ChaseRod(0.05f, 0.86f);
                        rodRot += rodSpin;
                        rodSpin *= 0.88f;
                        if (MathF.Abs(rodSpin) < 0.04f) {
                            rodRot = rodRot.AngleLerp(RestRot(), 0.12f);
                        }
                    }
                    break;
                }
                case StateFish: {
                    Vector2 slot = Projectile.Center + new Vector2(0f, Sway(1.8f, 4f) - 4f);
                    rodTarget = slot;
                    ChaseRod(0.07f, 0.84f);

                    int phase = t % CycleLen;
                    float want;
                    if (phase < CastLen) {
                        //抛竿：竿梢向前下方一甩
                        float p = phase / (float)CastLen;
                        float whip = MathF.Sin(p * MathHelper.Pi) * 0.75f;
                        want = RestRot() + rodFacing * whip * 0.55f;
                    }
                    else if (phase >= PullFrame) {
                        //起竿：向后上方猛拉
                        float p = (phase - PullFrame) / (float)PullLen;
                        float yank = MathF.Sin(MathF.Min(p * 2.4f, 1f) * MathHelper.Pi) * 0.8f;
                        want = RestRot() - rodFacing * yank * 0.7f;
                    }
                    else if (cycleBiting && phase >= BiteStart && IsBigCycle()) {
                        //大物咬钩：竿梢被往湖里拽弯
                        float biteT = (phase - BiteStart) / (float)BiteLen;
                        want = RestRot() + rodFacing * MathF.Abs(MathF.Sin(biteT * MathHelper.Pi * 2f)) * 0.3f;
                    }
                    else {
                        want = RestRot();
                    }
                    rodRot += rodSpin;
                    rodSpin *= 0.86f;
                    rodRot = rodRot.AngleLerp(want, 0.2f);
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    rodVel.X *= 0.93f;
                    rodVel.Y = MathF.Min(rodVel.Y + 0.3f, 9.5f);
                    float droop = rodRot + (rodFacing > 0 ? 0.5f : -0.5f);
                    rodRot = rodRot.AngleLerp(droop, 0.02f);
                    rodPos += rodVel;
                    rodTarget = rodPos;
                    break;
                }
            }

            if (!skipFix && Vector2.Distance(rodPos, rodTarget) > 780f) {
                rodPos = rodTarget;
                rodVel = Vector2.Zero;
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

        /// <summary>竿梢位：沿竿轴探出大半竿身</summary>
        private Vector2 RodTipPos()
            => rodPos + rodRot.ToRotationVector2() * (Profile.RodLen * 0.46f);

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float RodAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        private float RodForm() {
            int t = (int)StateTimer;
            float steady = 0.24f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + Seed) * 0.06f;
            return State switch {
                StateEmerge => t < OmenFrames
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp(
                        (t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.6f, 0f, 1f),
                _ => steady,
            };
        }

        private float DissolveAmt() {
            if (State != StateDissolve) {
                return 0f;
            }
            float p = MathF.Pow(MathHelper.Clamp(StateTimer / 46f, 0f, 1f), 0.9f);
            return MathHelper.Clamp(p + (dissolveSplashed ? 0.15f : 0f), 0f, 1f);
        }

        private float RodScale() {
            float scale = 1f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 10) {
                scale *= 1f + 0.08f * (1f - (t - OmenFrames) / 10f);
            }
            return scale * Profile.DrawScale;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!rodInit) {
                return false;
            }
            Main.instance.LoadItem(armsItemType);
            Texture2D tex = TextureAssets.Item[armsItemType]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();

            //钓线与浮标画在竿身之下
            if (domain != null && State == StateFish) {
                DrawLineAndBobber(sb, domain);
            }
            DrawBody(sb, tex);
            return false;
        }

        /// <summary>钓线：竿梢到浮标的两段悬垂折线 + 浮标珠</summary>
        private void DrawLineAndBobber(SpriteBatch sb, KikasaDomainPlayer domain) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (pixel == null || glow == null) {
                return;
            }
            float alpha = RodAlpha();
            if (alpha < 0.05f) {
                return;
            }
            Vector2 tip = RodTipPos();
            Vector2 bobber = BobberPos(domain);

            //悬垂中点：线的肚子往下坠一截
            Vector2 mid = (tip + bobber) * 0.5f + new Vector2(0f, Vector2.Distance(tip, bobber) * 0.12f);
            const int segs = 8;
            Vector2 prev = tip;
            for (int k = 1; k <= segs; k++) {
                float p = k / (float)segs;
                //二次贝塞尔逐段
                Vector2 cur = Vector2.Lerp(Vector2.Lerp(tip, mid, p), Vector2.Lerp(mid, bobber, p), p);
                Vector2 diff = cur - prev;
                float len = diff.Length();
                if (len >= 1f) {
                    sb.Draw(pixel, prev - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                        BloodDeep * (0.55f * alpha), diff.ToRotation(),
                        Vector2.Zero, new Vector2(len, 1.2f), SpriteEffects.None, 0f);
                }
                prev = cur;
            }

            //浮标珠：血亮小点，咬钩时更亮
            int t = (int)StateTimer;
            int phase = t % CycleLen;
            bool biting = cycleBiting && phase >= BiteStart && phase < PullFrame;
            float bobA = (biting ? 0.7f : 0.42f) * alpha;
            sb.Draw(glow, bobber - Main.screenPosition, null,
                (biting ? BloodBright : BloodMain) with { A = 0 } * bobA, 0f,
                glow.Size() * 0.5f, new Vector2(9f * 2f / glow.Width), SpriteEffects.None, 0f);
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

            float alpha = RodAlpha();
            if (alpha > 0.01f) {
                //斜置画法：竿轴向补 π/4，不镜像（避水线翻面陷阱）
                float rot = rodRot + MathHelper.PiOver4;
                Vector2 drawPos = rodPos - Main.screenPosition;
                float dissolve = DissolveAmt();
                Vector2 origin = tex.Size() * 0.5f;

                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.7f, MathF.Cos(wt * 0.83f) * 2.1f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.035f;
                    float envScale = RodScale() * (1.14f + MathF.Sin(wt * 1.6f) * 0.025f);
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
                    form.Parameters["uForm"]?.SetValue(RodForm());
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }
                sb.Draw(tex, drawPos, null, color, rot, origin, RodScale(), SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !rodInit) {
                return;
            }
            for (int k = 0; k < 5; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    rodPos + Main.rand.NextVector2Circular(18f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.5f, 2.4f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 24), 0f);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.6f, Main.rand.NextFloat(0.5f, 0.75f))
                ?.Configure(Main.rand.Next(45, 70));
        }
    }
}
