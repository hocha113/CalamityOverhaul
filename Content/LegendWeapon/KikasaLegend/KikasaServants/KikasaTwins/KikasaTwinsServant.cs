using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaTwins
{
    /// <summary>
    /// 鬼奴·湖水版双子魔眼。单弹幕同时驱动两只眼：Projectile.Center 为编队质心权威同步，
    /// 两眼位置由状态机 + Seed 在各端本地推算（毁灭者内部模拟范式），硬纠阈值防抽搐，
    /// 两眼各自朝向自己的表演目标。签名视觉为两眼之间下垂滴血的血脐带，悬链弧垂随
    /// 间距绷紧/松弛，中点周期坠血珠；签名机制为交叉剪切冲刺：两眼拉开到目标两侧对峙
    /// （同时后拉蓄力、脐带绷直发亮），互换位置交叉冲过，剪切窗内绷直的脐带就是伤害线。
    /// 激光眼压阵远程负责精准脉冲点射（细直快二连发、弹道预告线一闪），魔焰眼游走近逼
    /// 负责锥形血焰吐息（独立弹幕锚定口器）。联机契约与克眼/毁灭者同构：
    /// owner 裁决转场盖 netUpdate 章、节拍闩防快照回卷、生命线只有 owner 判
    /// </summary>
    internal class KikasaTwinsServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>剪切冲刺与脐带伤害线基伤（召唤加成前）</summary>
        internal const int ScissorDamage = 620;

        /// <summary>脉冲点射与血焰吐息基伤（召唤加成前），由子弹幕消费</summary>
        internal const int ShotDamage = 340;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateScissor = 2;
        private const int StatePulse = 3;
        private const int StateFlame = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>
        /// 状态内子参数。剪切期编码为 符号×(1+相位)：符号=剪切轴倾斜向，
        /// |值|-1=相位(0拉开/1对峙蓄力/2剪切/3收势)；连射期=已发弹数；其余为 0
        /// </summary>
        private ref float StateParam => ref Projectile.ai[2];

        private float TiltDir => MathF.Sign(StateParam) == 0f ? 1f : MathF.Sign(StateParam);
        private int ScissorPhase => (int)MathF.Abs(StateParam) - 1;

        //==================== 时序 ====================

        //出水：两点预兆→双眼先后破水（第二只慢半拍）→升起凝实→拽带→觉醒
        private const int OmenFrames = 30;
        private const int BreachGap = 9;
        private const int RiseEnd = 72;
        private const int ScanSettleEnd = 84;
        private const int AwakenFrame = 78;
        private const int EmergeTotal = 96;
        private const float EmergeHalfSpan = 84f;

        //剪切：拉开→对峙蓄力（pow6 迟发后拉、72% 静默）→一帧定速交叉→硬刹收势
        private const int SplitFrames = 34;
        private const int PoiseFrames = 30;
        private const int DashFrames = 18;
        private const int SettleFrames = 24;
        private const float ScissorSpan = 300f;
        private const float PoisePull = 66f;
        private const float DashSpeed = 36f;

        //点射：刹停→锁线（转率衰减）→二连发×2 轮，轮间重新锁线
        private const int PulseBrakeEnd = 10;
        private const int PulseFirstAimEnd = 36;
        private static readonly int[] PulseShotTimes = { 40, 49, 73, 82 };
        private const int PulseReAimStart = 55;
        private const int PulseTotal = 102;

        //吐息：魔焰眼扑近→后仰蓄力（72% 静默）→点燃持续喷吐→散热
        private const int FlameApproachEnd = 28;
        private const int FlameWindupEnd = 46;
        /// <summary>吐息持续帧数，供吐息弹幕共用</summary>
        internal const int FlameBreathFrames = 66;
        private const int FlameTotal = FlameWindupEnd + FlameBreathFrames + 16;

        //溶解：相拥→脐带崩断→各自坠湖化水
        private const int DissolveSnapFrame = 16;
        private const int DissolveFrames = 62;

        private const float FollowHalfSpan = 96f;

        //==================== 血脐带 ====================

        private const int CordSegs = 22;
        private const float CordRestLen = 330f;

        private readonly Vector2[] cordPoints = new Vector2[CordSegs + 1];
        private float cordSlack;
        private int cordLowIndex;

        //==================== 双眼本地模拟（各端自算，质心同步纠偏）====================

        private readonly Vector2[] eyePos = new Vector2[2];
        private readonly Vector2[] eyeVel = new Vector2[2];
        private readonly float[] eyeRot = new float[2];
        private readonly Vector2[] eyeTarget = new Vector2[2];
        private readonly Vector2[][] eyeOld = new Vector2[2][] { new Vector2[8], new Vector2[8] };
        private readonly float[][] eyeOldRot = new float[2][] { new float[8], new float[8] };
        private bool eyesInit;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private readonly int[] eyeFrameTick = new int[2];
        private readonly int[] eyeFrameIndex = new int[2];
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private readonly bool[] breachDone = new bool[2];
        private readonly bool[] awakenDone = new bool[2];
        private bool cordPulled;
        private bool scissorLaunched;
        private bool crossFlashed;
        private int crossFlashTick;
        private bool slackFlung;
        private int lastShotFired = -1;
        private bool pulseAimInit;
        private float aimRotRet;
        private float pulseAimDist = 520f;
        private bool breathSpawned;
        private bool cordSnapped;
        private Vector2 snapMid;
        private readonly bool[] dissolveSplashed = new bool[2];
        private int cordDripTimer;
        private int cordWaterTimer;

        //==================== 血色板（随观看域鬼雨异化冷化，与湖系同族）====================

        internal static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        internal static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        internal static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        internal static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        internal static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        /// <summary>激光眼的灼热点缀，只作次要加色层</summary>
        internal static Color PulseHot => KikasaDomain.CoolTint(new(255, 168, 150), new(196, 218, 220));
        /// <summary>魔焰眼残余的诅咒病绿，只作次要点缀层</summary>
        internal static Color CursedTinge => KikasaDomain.CoolTint(new(118, 196, 108), new(110, 168, 150));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        private static float Side(int i) => i == 0 ? -1f : 1f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ScissorDamage);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 46f), Vector2.Zero,
                ModContent.ProjectileType<KikasaTwinsServant>(), damage, 7f, owner.whoAmI);
        }

        /// <summary>按 owner 找本人的双子鬼奴，吐息弹幕锚定用</summary>
        internal static KikasaTwinsServant FindFor(int owner) {
            int type = ModContent.ProjectileType<KikasaTwinsServant>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p?.active == true && p.owner == owner && p.type == type
                    && p.ModProjectile is KikasaTwinsServant servant) {
                    return servant;
                }
            }
            return null;
        }

        /// <summary>吐息弹幕锚定用：眼位与就绪态（0=激光眼 1=魔焰眼）</summary>
        internal Vector2 EyeCenter(int i) => eyePos[i];
        internal bool EyesReady => eyesInit;

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //双眼与脐带远超质心 hitbox，出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
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

        /// <summary>接触伤害只开在剪切窗，与可见的交叉冲刺严格对齐</summary>
        public override bool? CanDamage()
            => State == StateScissor && ScissorPhase == 2 ? null : false;

        /// <summary>剪切命中：绷直的脐带线段 + 两只眼体</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!eyesInit) {
                return false;
            }
            float _ = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                eyePos[0], eyePos[1], 30f, ref _)) {
                return true;
            }
            for (int i = 0; i < 2; i++) {
                Rectangle eyeRect = new((int)(eyePos[i].X - 38f), (int)(eyePos[i].Y - 38f), 76, 76);
                if (eyeRect.Intersects(targetHitbox)) {
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
            //第二只眼还没破水就要收场：直接收掉，免得溶解演出让它凭空闪现再化水
            if (State == StateEmerge && StateTimer < OmenFrames + BreachGap) {
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

            //生命线：只有 owner 裁决，服务器无领域状态（既定契约），
            //迟入场客户端首份快照前也会误判；其余端只跟 owner 的包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ScissorDamage);

            //换场清闩：远端可能靠收包换场而非本地同拍转场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                scissorLaunched = false;
                crossFlashed = false;
                slackFlung = false;
                lastShotFired = -1;
                pulseAimInit = false;
                breathSpawned = false;
                if (State == StateDissolve) {
                    cordSnapped = false;
                    dissolveSplashed[0] = dissolveSplashed[1] = false;
                }
            }

            if (!eyesInit) {
                RebuildEyes(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateScissor: UpdateScissor(owner, authority); break;
                case StatePulse: UpdatePulse(owner, authority); break;
                case StateFlame: UpdateFlame(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateEyes(owner, domain);
            UpdateCord(domain);
            PushEyeHistory();
            UpdateFrames();
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            for (int i = 0; i < 2; i++) {
                float glow = EyeAlpha(i) * 0.5f;
                if (glow > 0.02f) {
                    Lighting.AddLight(eyePos[i], 0.4f * glow, 0.1f * glow, 0.09f * glow);
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：两点预兆、先后破水、脐带被拽出 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                //水下待命：相距一段的两处水面同时起预兆
                Projectile.velocity = Vector2.Zero;
                if (viewed) {
                    if (t % 6 == 2) {
                        float converge = 1f - t / (float)OmenFrames;
                        float wobble = (t / 6 % 2 == 0 ? 1f : -1f) * converge * 40f;
                        for (int s = 0; s < 2; s++) {
                            KikasaDomainDeco.RippleAt(
                                new Vector2(Projectile.Center.X + Side(s) * EmergeHalfSpan + wobble, lakeY),
                                0.35f + (1f - converge) * 0.5f);
                        }
                    }
                    if (t == 6 || t == 18) {
                        //左右两点先后滴响，预告这不是一只
                        float x = Projectile.Center.X + (t == 6 ? -EmergeHalfSpan : EmergeHalfSpan);
                        SoundEngine.PlaySound(SoundID.Drip with {
                            Volume = 0.45f,
                            Pitch = t == 6 ? -0.5f : -0.15f,
                            MaxInstances = 2
                        }, new Vector2(x, lakeY));
                    }
                }
                return;
            }

            //激光眼先破水
            if (!breachDone[0]) {
                breachDone[0] = true;
                Projectile.velocity = new Vector2(0f, -6f);
                eyeVel[0] += new Vector2(0f, -11.5f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.42f, Pitch = -0.55f, MaxInstances = 2 }, eyePos[0]);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X - EmergeHalfSpan, lakeY), 4f);
                }
            }
            //魔焰眼慢半拍跟上
            if (!breachDone[1] && t >= OmenFrames + BreachGap) {
                breachDone[1] = true;
                Projectile.velocity.Y -= 5.4f;
                eyeVel[1] += new Vector2(0f, -12f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.45f, Pitch = -0.2f, MaxInstances = 2 }, eyePos[1]);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X + EmergeHalfSpan, lakeY), 4.5f);
                }
            }

            //升起：破水动量指数衰减，前快后慢
            Projectile.velocity.Y *= 0.957f;
            Projectile.velocity.X = 0f;

            //身上的血水成帘往下淌（只淌已破水的眼）
            if (viewed && t < RiseEnd) {
                for (int i = 0; i < 2; i++) {
                    if (t < BreachTime(i) || t % 3 != i) {
                        continue;
                    }
                    Vector2 dropPos = eyePos[i] + new Vector2(
                        Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(4f, 26f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2.2f, 3.6f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.4f, 0.65f))
                        ?.Configure(Main.rand.Next(14, 26), 0f);
                }
            }

            //觉醒双拍：两只瞳孔先后亮起转向猎物
            for (int i = 0; i < 2; i++) {
                if (!awakenDone[i] && t >= AwakenFrame + i * 4) {
                    awakenDone[i] = true;
                    SoundEngine.PlaySound(SoundID.NPCHit13 with {
                        Volume = 0.45f,
                        Pitch = -0.6f + i * 0.25f,
                        MaxInstances = 2
                    }, eyePos[i]);
                    if (viewed) {
                        ShakeViewer(1.2f);
                    }
                }
            }

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），各端同拍；owner 盖章纠偏
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 30;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>单眼破水浪冠：环涟漪 + 扇形血珠 + 水柱束 + 血雾；两次起爆双拍成对</summary>
        private void BreachBurst(Vector2 hit, float shake) {
            KikasaDomainDeco.RippleAt(hit, 1.9f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(30f, 0f), 0.8f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(28f, 0f), 0.75f);
            KikasaDomainDeco.SplashAt(hit, 10);

            for (int i = 0; i < 16; i++) {
                float angle = -MathHelper.Pi * (0.14f + 0.72f * i / 15f);
                float speed = Main.rand.NextFloat(3f, 7f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                    angle.ToRotationVector2() * speed,
                    BloodMain * Main.rand.NextFloat(0.45f, 0.66f),
                    Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(Main.rand.Next(20, 34), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-6f, 6f), -6f),
                    new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(8f, 12.5f)),
                    BloodMain * Main.rand.NextFloat(0.5f, 0.7f),
                    Main.rand.NextFloat(0.5f, 0.85f))
                    ?.Configure(Main.rand.Next(30, 46), Main.rand.NextFloat(-0.3f, 0.3f));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-24f, 24f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.7f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.65f, 0.95f))
                    ?.Configure(Main.rand.Next(55, 90));
            }

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.95f, Pitch = -0.3f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.4f, Pitch = -0.7f, MaxInstances = 1 }, hit);
            ShakeViewer(shake);
        }

        //==================== 跟随：一只压阵、一只游走 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 30f, -122f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + Seed) * 6f;
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.2f + Seed * 2f) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢就贴回来，别拖着脐带横穿半张地图
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildEyes(owner.GetModPlayer<KikasaDomainPlayer>());
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.085f;
            const float maxSpeed = 17f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);

            //出手裁决：点射→吐息→交叉剪切轮换；转场规则各端一致，owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                int pick = attackIndex % 3;
                StateTimer = 0;
                if (pick == 1) {
                    State = StatePulse;
                    StateParam = 0;
                }
                else if (pick == 2) {
                    State = StateFlame;
                    StateParam = 0;
                }
                else {
                    State = StateScissor;
                    //剪切轴倾斜向盖进 ai[2] 符号，owner 章一并带给远端
                    StateParam = (Projectile.identity + attackIndex) % 2 == 0 ? 1f : -1f;
                }
                Projectile.netUpdate = authority;
            }
        }

        //==================== 交叉剪切冲刺 ====================

        private Vector2 ScissorAxis() => new Vector2(1f, 0f).RotatedBy(TiltDir * 0.22f);

        /// <summary>剪切驻位：质心（压在目标处）沿倾斜轴两侧展开</summary>
        private Vector2 ScissorPost(int side, float extra)
            => Projectile.Center + ScissorAxis() * Side(side) * (ScissorSpan + extra);

        private void UpdateScissor(Player owner, bool authority) {
            int phase = ScissorPhase;
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && phase <= 1) {
                EndAttack(authority, 50);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 8f
                : Projectile.Center;

            void NextPhase(int next) {
                StateParam = TiltDir * (1 + next);
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //拉开：质心压到目标中心，双眼在 UpdateEyes 里奔两侧驻位
                Vector2 want = (focus - Projectile.Center) * 0.09f;
                if (want.Length() > 19f) {
                    want = want.SafeNormalize(Vector2.Zero) * 19f;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.2f);

                bool posted = Vector2.Distance(eyePos[0], eyeTarget[0]) < 60f
                    && Vector2.Distance(eyePos[1], eyeTarget[1]) < 60f;
                if ((posted && t > 10) || t >= SplitFrames) {
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //对峙蓄力：质心软钉在目标处，双眼沿轴外拉、脐带绷直发亮
                Projectile.velocity *= 0.85f;
                Projectile.velocity += (focus - Projectile.Center) * 0.015f;
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Volume = 0.45f, Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);
                }
                //蓄势血珠向脐带中点收拢，72% 后静默，爆发前的吸气
                if (!Main.dedServ && t < PoiseFrames * 0.72f && t % 2 == 1) {
                    Vector2 mid = Vector2.Lerp(eyePos[0], eyePos[1], 0.5f);
                    Vector2 from = mid + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 130f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        (mid - from) * 0.14f,
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(9, 0f);
                }
                if (t % 8 == 4 && ViewedOwner) {
                    float p = t / (float)PoiseFrames;
                    ShakeViewer(0.6f + 1.2f * p * p);
                }
                if (t >= PoiseFrames) {
                    NextPhase(2);
                }
                return;
            }

            if (phase == 2) {
                if (!scissorLaunched) {
                    //一帧定速：各自冲向对面的驻位，互换位置
                    scissorLaunched = true;
                    Vector2 postA = ScissorPost(0, 0f);
                    Vector2 postB = ScissorPost(1, 0f);
                    eyeVel[0] = (postB - eyePos[0]).SafeNormalize(Vector2.UnitX) * DashSpeed;
                    eyeVel[1] = (postA - eyePos[1]).SafeNormalize(-Vector2.UnitX) * DashSpeed;
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 3 }, eyePos[0]);
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = 0.08f, MaxInstances = 3 }, eyePos[1]);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 2 }, Projectile.Center);
                    if (ViewedOwner) {
                        ShakeViewer(3.5f);
                    }
                    Projectile.netUpdate = authority;
                }

                //质心钉住，剪切在两眼上演
                Projectile.velocity *= 0.9f;

                //交错瞬间：脐带扫过中间区域的重拍
                if (!crossFlashed && Vector2.Distance(eyePos[0], eyePos[1]) < 74f) {
                    crossFlashed = true;
                    crossFlashTick = 8;
                    Vector2 mid = Vector2.Lerp(eyePos[0], eyePos[1], 0.5f);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.42f, Pitch = 0.15f, MaxInstances = 2 }, mid);
                    if (!Main.dedServ) {
                        Vector2 perp = ScissorAxis().RotatedBy(MathHelper.PiOver2);
                        PRTLoader.NewParticle<PRT_DWave>(mid, Vector2.Zero, BloodBright, 0.08f)
                            ?.Configure(new Vector2(0.5f, 1f), perp.ToRotation(), 0.3f, 9);
                        for (int i = 0; i < 8; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                mid + Main.rand.NextVector2Circular(16f, 16f),
                                perp * Main.rand.NextFloat(-5f, 5f) + Main.rand.NextVector2Circular(1.5f, 1.5f),
                                Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                                Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 28));
                        }
                    }
                    if (ViewedOwner) {
                        ShakeViewer(3f);
                    }
                }
                if (crossFlashTick > 0) {
                    crossFlashTick--;
                }

                if (t >= DashFrames) {
                    NextPhase(3);
                }
                return;
            }

            //收势：脐带松弛回垂，甩出挂不住的血珠
            if (!slackFlung) {
                slackFlung = true;
                if (!Main.dedServ) {
                    for (int k = 2; k < CordSegs; k += 3) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            cordPoints[k] + Main.rand.NextVector2Circular(4f, 4f),
                            new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(0.5f, 2.6f)),
                            BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.55f))
                            ?.Configure(Main.rand.Next(14, 26), 0.35f);
                    }
                }
            }
            if (crossFlashTick > 0) {
                crossFlashTick--;
            }
            if (t >= SettleFrames) {
                EndAttack(authority, 130);
            }
        }

        //==================== 精准脉冲点射（激光眼）====================

        private void UpdatePulse(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= PulseFirstAimEnd) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 aimPos = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                : eyePos[0] + aimRotRet.ToRotationVector2() * 500f;

            if (!pulseAimInit) {
                pulseAimInit = true;
                aimRotRet = (aimPos - eyePos[0]).ToRotation();
            }
            //锁线：转率随进度衰减，"锁死"读得见；轮间重新提转率
            float lockRate = t <= PulseFirstAimEnd
                ? MathHelper.Lerp(0.3f, 0.07f, t / (float)PulseFirstAimEnd)
                : t is > PulseReAimStart and <= 73 ? 0.18f : 0.1f;
            aimRotRet = aimRotRet.AngleTowards((aimPos - eyePos[0]).ToRotation(), lockRate);
            pulseAimDist = MathHelper.Clamp(Vector2.Distance(eyePos[0], aimPos) + 60f, 200f, 900f);

            //质心退到主人身侧的射击位
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 64f, -142f);
            Vector2 desired = (anchor - Projectile.Center) * 0.06f;
            if (desired.Length() > 12f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 12f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.1f);

            if (t == PulseBrakeEnd + 2) {
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 2 }, eyePos[0]);
            }
            //蓄力血珠向义眼镜筒收拢，72% 静默截断
            float charge = PulseCharge();
            if (!Main.dedServ && charge is > 0.05f and < 0.72f && t % 2 == 0) {
                Vector2 muzzle = PulseMuzzle();
                Vector2 from = muzzle + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 90f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                    (muzzle - from) * 0.16f,
                    PulseHot * (0.3f + charge * 0.3f), Main.rand.NextFloat(0.25f, 0.45f))
                    ?.Configure(8, 0f);
            }

            //按表出膛：二连发×2 轮，节拍闩防快照回卷重播
            for (int idx = 0; idx < PulseShotTimes.Length; idx++) {
                if (t == PulseShotTimes[idx] && lastShotFired < idx) {
                    lastShotFired = idx;
                    StateParam = idx + 1;
                    FirePulse(owner, authority);
                    break;
                }
            }

            if (t >= PulseTotal) {
                EndAttack(authority, 95);
            }
        }

        private Vector2 PulseMuzzle() => eyePos[0] + aimRotRet.ToRotationVector2() * 30f;

        private void FirePulse(Player owner, bool authority) {
            Vector2 dir = aimRotRet.ToRotationVector2();
            Vector2 muzzle = PulseMuzzle();
            //每发后坐：知重量者先退半步
            eyeVel[0] -= dir * 5f;

            SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 3 }, muzzle);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.3f, Pitch = -0.2f, MaxInstances = 3 }, muzzle);
            if (!Main.dedServ) {
                //出膛：细锥血珠 + 一圈窄扩散环
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(muzzle + Main.rand.NextVector2Circular(2f, 2f),
                        dir.RotatedByRandom(0.14f) * Main.rand.NextFloat(4f, 9f),
                        Main.rand.NextBool(3) ? BloodDeep : PulseHot,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18));
                }
                PRTLoader.NewParticle<PRT_DWave>(muzzle + dir * 6f, Vector2.Zero, PulseHot, 0.05f)
                    ?.Configure(new Vector2(0.4f, 1f), aimRotRet, 0.16f, 7);
            }
            if (ViewedOwner) {
                ShakeViewer(0.9f);
            }

            //弹体只在 owner 端生成，spawn 包自带全部初值
            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ShotDamage);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), muzzle, dir * 20f,
                    ModContent.ProjectileType<KikasaTwinsPulseShot>(), damage, 2f, Projectile.owner);
            }
        }

        /// <summary>点射蓄力进度 0~1：首轮锁线与二轮重锁两段，绘制层预告线共用</summary>
        private float PulseCharge() {
            if (State != StatePulse) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= PulseBrakeEnd) {
                return 0f;
            }
            if (t <= PulseShotTimes[1]) {
                return MathHelper.Clamp((t - PulseBrakeEnd) / (float)(PulseFirstAimEnd - PulseBrakeEnd), 0f, 1f);
            }
            if (t <= PulseShotTimes[3]) {
                return MathHelper.Clamp((t - PulseReAimStart) / 15f, 0f, 1f);
            }
            return 0f;
        }

        /// <summary>出膛前 3 帧预告线闪亮</summary>
        private float TelegraphFlash() {
            if (State != StatePulse) {
                return 0f;
            }
            int t = (int)StateTimer;
            for (int i = 0; i < PulseShotTimes.Length; i++) {
                int dt = PulseShotTimes[i] - t;
                if (dt is >= 0 and <= 3) {
                    return 1f - dt / 4f;
                }
            }
            return 0f;
        }

        //==================== 锥形血焰吐息（魔焰眼）====================

        private void UpdateFlame(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= FlameApproachEnd) {
                EndAttack(authority, 50);
                return;
            }
            Vector2 tpos = target >= 0
                ? Main.npc[target].Center
                : eyePos[1] + FrontDirEye(1) * 300f;

            //质心跟到半程：脐带从压阵的激光眼斜拉向贴身的魔焰眼
            Vector2 anchor = Vector2.Lerp(
                owner.Center + new Vector2(-owner.direction * 50f, -150f), tpos, 0.42f);
            Vector2 desired = (anchor - Projectile.Center) * 0.07f;
            if (desired.Length() > 15f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 15f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);

            if (t <= FlameApproachEnd) {
                return;
            }

            if (t <= FlameWindupEnd) {
                //后仰蓄力：聚焰粒子涌向口器，72% 后静默
                float k = (t - FlameApproachEnd) / (float)(FlameWindupEnd - FlameApproachEnd);
                if (t == FlameApproachEnd + 2) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Volume = 0.45f, Pitch = -0.45f, MaxInstances = 2 }, eyePos[1]);
                }
                if (!Main.dedServ && k < 0.72f && t % 2 == 0) {
                    Vector2 mouth = eyePos[1] + FrontDirEye(1) * 30f;
                    Vector2 from = mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 100f);
                    PRTLoader.NewParticle<PRT_KikasaTwinsFlame>(from,
                        (mouth - from) * 0.13f,
                        BloodMain * (0.4f + k * 0.3f), Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(10, 0.02f);
                }
                return;
            }

            if (!breathSpawned) {
                //点燃拍：后坐 + 双层声；吐息弹幕只在 owner 端生成，起始角随 spawn 包走
                breathSpawned = true;
                float aim = (tpos - eyePos[1]).ToRotation();
                eyeVel[1] -= aim.ToRotationVector2() * 7f;
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 2 }, eyePos[1]);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.3f, Pitch = -0.1f, MaxInstances = 2 }, eyePos[1]);
                if (ViewedOwner) {
                    ShakeViewer(2f);
                }
                if (authority) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ShotDamage);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        eyePos[1] + aim.ToRotationVector2() * 30f, Vector2.Zero,
                        ModContent.ProjectileType<KikasaTwinsFlameBreath>(), damage, 6f,
                        Projectile.owner, aim);
                }
            }

            if (t >= FlameTotal) {
                EndAttack(authority, 125);
            }
        }

        /// <summary>吐息蓄力进度 0~1，绘制层口器积光共用</summary>
        private float FlameCharge() {
            if (State != StateFlame) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= FlameApproachEnd || t > FlameWindupEnd) {
                return 0f;
            }
            return (t - FlameApproachEnd) / (float)(FlameWindupEnd - FlameApproachEnd);
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解：脐带先断，两眼各自坠湖 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;

            if (t < DissolveSnapFrame) {
                //相拥拍：两眼相向微靠、面对面，脐带垂到最低、滴得最凶
                Projectile.velocity *= 0.9f;
            }
            else {
                if (!cordSnapped) {
                    //崩断拍：同源的证物先断
                    cordSnapped = true;
                    snapMid = cordPoints[cordLowIndex];
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = -0.75f, MaxInstances = 2 }, snapMid);
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = -0.8f, MaxInstances = 2 }, snapMid);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 10; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                snapMid + Main.rand.NextVector2Circular(8f, 8f),
                                new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), Main.rand.NextFloat(-1f, 3f)),
                                Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                                Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(16, 30), 0.4f);
                        }
                        PRTLoader.NewParticle<PRT_GhostRainMist>(snapMid, new Vector2(0f, -0.2f),
                            MistBlood * 0.7f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(40, 70));
                    }
                    if (ViewedOwner) {
                        ShakeViewer(2f);
                    }
                }
                //断带后各自坠湖，质心跟着一起沉
                if (lakeAlive) {
                    Projectile.velocity.X *= 0.93f;
                    Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.26f, 9f);
                }
                else {
                    Projectile.velocity *= 0.9f;
                }
            }

            //各自的过水线拍（先后落水，第二只自然慢半拍）
            for (int i = 0; i < 2; i++) {
                if (lakeAlive && !dissolveSplashed[i] && t >= DissolveSnapFrame && eyePos[i].Y >= lakeY) {
                    dissolveSplashed[i] = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.6f,
                        Pitch = -0.4f + i * 0.15f,
                        MaxInstances = 2
                    }, eyePos[i]);
                    if (ViewedOwner) {
                        Vector2 hit = new(eyePos[i].X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 8);
                        KikasaDomainDeco.RippleAt(hit, 1.1f);
                        ShakeViewer(1.5f);
                    }
                }
            }

            //边沉边化成血珠
            if (!Main.dedServ && t % 2 == 0 && EyeAlpha(0) > 0.15f) {
                int i = t / 2 % 2;
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    eyePos[i] + Main.rand.NextVector2Circular(26f, 26f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.5f, 3f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
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

        //==================== 双眼推进：状态机 + Seed 确定性驻位，本地弹簧追随 ====================

        /// <summary>初始化或硬纠时按当前状态直接落位，防两眼与脐带抽搐</summary>
        private void RebuildEyes(KikasaDomainPlayer domain) {
            eyesInit = true;
            for (int i = 0; i < 2; i++) {
                eyePos[i] = State == StateEmerge
                    ? new Vector2(Projectile.Center.X + Side(i) * EmergeHalfSpan, domain.LakeWorldY + 34f)
                    : Projectile.Center + new Vector2(Side(i) * FollowHalfSpan, i == 0 ? -30f : 24f);
                eyeVel[i] = Vector2.Zero;
                eyeRot[i] = 0f;
                for (int k = 0; k < eyeOld[i].Length; k++) {
                    eyeOld[i][k] = eyePos[i];
                    eyeOldRot[i][k] = 0f;
                }
            }
            //脐带采样点即刻铺直线：中途入场/硬纠帧的断带、甩珠节拍不许打在世界原点
            for (int k = 0; k <= CordSegs; k++) {
                cordPoints[k] = Vector2.Lerp(eyePos[0], eyePos[1], k / (float)CordSegs);
            }
            cordLowIndex = CordSegs / 2;
        }

        private void ChaseEye(int i, float accel, float damp) {
            eyeVel[i] = (eyeVel[i] + (eyeTarget[i] - eyePos[i]) * accel) * damp;
            eyePos[i] += eyeVel[i];
        }

        /// <summary>呼吸浮动相位（Seed 确定性，各端一致）</summary>
        private float Sway(int i, float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed + i * 2.4f) * amp;

        private void UpdateEyes(Player owner, KikasaDomainPlayer domain) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            Vector2 targetPos = target >= 0 ? Main.npc[target].Center : owner.Center;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    for (int i = 0; i < 2; i++) {
                        eyeTarget[i] = t < BreachTime(i)
                            ? new Vector2(Projectile.Center.X + Side(i) * EmergeHalfSpan, lakeY + 34f)
                            : Projectile.Center + new Vector2(Side(i) * EmergeHalfSpan, Sway(i, 2.1f, 10f));
                        ChaseEye(i, 0.06f, 0.85f);
                        //升起期低头看水面，觉醒后转向猎物
                        if (t < AwakenFrame + i * 4) {
                            eyeRot[i] = eyeRot[i].AngleLerp(0f, 0.2f);
                        }
                        else {
                            FaceEye(i, targetPos, 0.25f);
                        }
                    }
                    break;
                }
                case StateFollow: {
                    //激光眼压阵后上位，魔焰眼前下游走画小圈
                    eyeTarget[0] = Projectile.Center + new Vector2(
                        -owner.direction * FollowHalfSpan, -30f + Sway(0, 2.0f, 8f));
                    eyeTarget[1] = Projectile.Center + new Vector2(
                        owner.direction * (FollowHalfSpan - 12f) + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f + Seed * 3f) * 26f,
                        24f + Sway(1, 2.6f, 12f));
                    ChaseEye(0, 0.045f, 0.86f);
                    ChaseEye(1, 0.055f, 0.85f);
                    FaceEye(0, targetPos, 0.14f);
                    FaceEye(1, target >= 0 ? targetPos : eyePos[1] + eyeVel[1], 0.12f);
                    //轮廓下缘偶发凝珠
                    if (!Main.dedServ && Main.rand.NextBool(28)) {
                        int i = Main.rand.Next(2);
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            eyePos[i] + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(14f, 30f)),
                            new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                            BloodMain * Main.rand.NextFloat(0.4f, 0.55f),
                            Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(18, 32), 0f);
                    }
                    break;
                }
                case StateScissor: {
                    int phase = ScissorPhase;
                    if (phase == 0) {
                        //奔两侧驻位
                        eyeTarget[0] = ScissorPost(0, 0f);
                        eyeTarget[1] = ScissorPost(1, 0f);
                        ChaseEye(0, 0.075f, 0.82f);
                        ChaseEye(1, 0.075f, 0.82f);
                        FaceEye(0, eyePos[1], 0.2f);
                        FaceEye(1, eyePos[0], 0.2f);
                    }
                    else if (phase == 1) {
                        //迟发后拉：pow6 憋到最后几帧猛吸一口气
                        float pull = MathF.Pow(MathHelper.Clamp(t / (float)PoiseFrames, 0f, 1f), 6f) * PoisePull;
                        eyeTarget[0] = ScissorPost(0, pull);
                        eyeTarget[1] = ScissorPost(1, pull);
                        ChaseEye(0, 0.16f, 0.7f);
                        ChaseEye(1, 0.16f, 0.7f);
                        //面对面锁向对方，冲刺方向的 tell
                        FaceEye(0, eyePos[1], 0.35f);
                        FaceEye(1, eyePos[0], 0.35f);
                    }
                    else if (phase == 2) {
                        //剪切段：复利续力直线互换，不转向
                        skipFix = true;
                        for (int i = 0; i < 2; i++) {
                            eyeVel[i] *= 1.012f;
                            eyePos[i] += eyeVel[i];
                            eyeRot[i] = eyeVel[i].ToRotation() - MathHelper.PiOver2;
                            //沿途甩出速度拉伸的血水
                            if (!Main.dedServ && Main.rand.NextBool(2)) {
                                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                                    eyePos[i] - eyeVel[i] * 0.4f + Main.rand.NextVector2Circular(14f, 14f),
                                    -eyeVel[i] * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                                    BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))
                                    ?.Configure(Main.rand.Next(9, 16), 0f);
                            }
                        }
                        eyeTarget[0] = ScissorPost(1, 0f);
                        eyeTarget[1] = ScissorPost(0, 0f);
                    }
                    else {
                        //收势：硬刹 ×0.72 读出撞墙般的分量，再软贴换位后的驻位
                        eyeTarget[0] = ScissorPost(1, 0f);
                        eyeTarget[1] = ScissorPost(0, 0f);
                        for (int i = 0; i < 2; i++) {
                            if (t <= 6) {
                                eyeVel[i] *= 0.72f;
                                eyePos[i] += eyeVel[i];
                            }
                            else {
                                ChaseEye(i, 0.03f, 0.92f);
                            }
                            FaceEye(i, targetPos, 0.1f);
                        }
                    }
                    break;
                }
                case StatePulse: {
                    //激光眼退到质心侧上狙位，锁线期只微颤
                    eyeTarget[0] = Projectile.Center + new Vector2(
                        -owner.direction * 46f, -66f + Sway(0, 1.4f, 4f));
                    ChaseEye(0, 0.07f, 0.8f);
                    eyeRot[0] = eyeRot[0].AngleLerp(aimRotRet - MathHelper.PiOver2, 0.4f)
                        + MathF.Sin(Main.GlobalTimeWrappedHourly * 37f + Seed) * 0.006f;
                    //魔焰眼绕质心快速游走，脐带一直在动
                    float orbit = StateTimer * 0.09f + Seed;
                    eyeTarget[1] = Projectile.Center + orbit.ToRotationVector2() * 118f;
                    ChaseEye(1, 0.06f, 0.85f);
                    FaceEye(1, targetPos, 0.15f);
                    break;
                }
                case StateFlame: {
                    //激光眼退回主人高位压阵，脐带被拉成斜线
                    eyeTarget[0] = owner.Center + new Vector2(
                        -owner.direction * 118f, -158f + Sway(0, 1.8f, 7f));
                    ChaseEye(0, 0.05f, 0.86f);
                    FaceEye(0, targetPos, 0.15f);

                    Vector2 tpos = target >= 0 ? Main.npc[target].Center : eyePos[1] + FrontDirEye(1) * 300f;
                    Vector2 toTarget = (tpos - eyePos[1]).SafeNormalize(Vector2.UnitX);
                    if (t <= FlameApproachEnd) {
                        //猛扑近位
                        eyeTarget[1] = tpos - toTarget * 176f;
                        ChaseEye(1, 0.09f, 0.84f);
                        FaceEye(1, tpos, 0.3f);
                    }
                    else if (t <= FlameWindupEnd) {
                        //后仰蓄力
                        float k = MathF.Pow((t - FlameApproachEnd) / (float)(FlameWindupEnd - FlameApproachEnd), 6f);
                        eyeTarget[1] = tpos - toTarget * (176f + k * 54f);
                        ChaseEye(1, 0.14f, 0.72f);
                        FaceEye(1, tpos, 0.35f);
                    }
                    else {
                        //喷吐推进：口器跟权威吐息角，一边喷一边压上去
                        Projectile breath = FindBreath();
                        float aim = breath?.rotation ?? (tpos - eyePos[1]).ToRotation();
                        float push = MathHelper.Clamp((t - FlameWindupEnd) / (float)FlameBreathFrames, 0f, 1f);
                        eyeTarget[1] = tpos - aim.ToRotationVector2() * (170f - push * 52f);
                        ChaseEye(1, 0.05f, 0.86f);
                        eyeRot[1] = eyeRot[1].AngleLerp(aim - MathHelper.PiOver2, 0.4f)
                            + MathF.Sin(Main.GlobalTimeWrappedHourly * 42f) * 0.014f;
                    }
                    break;
                }
                case StateDissolve: {
                    if (!cordSnapped) {
                        //相拥：相向微靠、彼此对望
                        Vector2 mid = Vector2.Lerp(eyePos[0], eyePos[1], 0.5f);
                        for (int i = 0; i < 2; i++) {
                            eyeTarget[i] = mid + (eyePos[i] - mid).SafeNormalize(Vector2.UnitX * Side(i)) * 66f;
                            ChaseEye(i, 0.04f, 0.88f);
                            FaceEye(i, eyePos[1 - i], 0.12f);
                        }
                    }
                    else {
                        //断带后各自失力坠湖，歪着头沉下去
                        skipFix = true;
                        for (int i = 0; i < 2; i++) {
                            eyeVel[i].X *= 0.93f;
                            eyeVel[i].Y = MathF.Min(eyeVel[i].Y + 0.3f, 9f);
                            eyePos[i] += eyeVel[i];
                            eyeRot[i] = eyeRot[i].AngleLerp(Side(i) * 0.4f, 0.03f);
                            eyeTarget[i] = eyePos[i];
                        }
                    }
                    break;
                }
            }

            //硬纠：同步包把质心拽走半屏时按驻位重建，防弹簧甩鞭
            if (!skipFix) {
                for (int i = 0; i < 2; i++) {
                    if (Vector2.Distance(eyePos[i], eyeTarget[i]) > 780f) {
                        eyePos[i] = eyeTarget[i];
                        eyeVel[i] = Vector2.Zero;
                    }
                }
            }
        }

        private Projectile FindBreath() {
            int type = ModContent.ProjectileType<KikasaTwinsFlameBreath>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p?.active == true && p.owner == Projectile.owner && p.type == type) {
                    return p;
                }
            }
            return null;
        }

        private void PushEyeHistory() {
            for (int i = 0; i < 2; i++) {
                Vector2[] arr = eyeOld[i];
                float[] rots = eyeOldRot[i];
                for (int k = arr.Length - 1; k >= 1; k--) {
                    arr[k] = arr[k - 1];
                    rots[k] = rots[k - 1];
                }
                arr[0] = eyePos[i];
                rots[0] = eyeRot[i];
            }
        }

        //==================== 血脐带推进 ====================

        /// <summary>脐带在眼体上的锚点：朝向对方一侧的下缘</summary>
        private Vector2 CordAnchor(int i) {
            Vector2 toOther = (eyePos[1 - i] - eyePos[i]).SafeNormalize(Vector2.UnitX * Side(1 - i));
            return eyePos[i] + toOther * 22f + new Vector2(0f, 8f);
        }

        /// <summary>绷直程度 0~1：对峙蓄力渐紧、剪切窗全紧、收势松回</summary>
        private float CordTaut() {
            if (State != StateScissor) {
                return 0f;
            }
            int t = (int)StateTimer;
            return ScissorPhase switch {
                1 => MathHelper.Clamp(t / (float)PoiseFrames, 0f, 1f),
                2 => 1f,
                3 => MathHelper.Clamp(1f - t / (float)SettleFrames, 0f, 1f),
                _ => 0f,
            };
        }

        private void UpdateCord(KikasaDomainPlayer domain) {
            if (!eyesInit) {
                return;
            }
            Vector2 a = CordAnchor(0);
            Vector2 b = CordAnchor(1);
            float dist = Vector2.Distance(a, b);
            cordSlack = MathF.Max(0f, CordRestLen - dist);
            float taut = CordTaut();
            float sag = MathF.Min((16f + cordSlack * 0.42f) * (1f - taut), 150f);

            Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float time = Main.GlobalTimeWrappedHourly;
            float lowest = float.MinValue;
            cordLowIndex = CordSegs / 2;
            for (int k = 0; k <= CordSegs; k++) {
                float f = k / (float)CordSegs;
                Vector2 p = Vector2.Lerp(a, b, f) + Vector2.UnitY * (sag * 4f * f * (1f - f));
                //松弛态的横向蠕动（Seed 确定性），绷直时归零
                p += perp * (MathF.Sin(f * 9.3f + time * 2.3f + Seed)
                    * cordSlack * 0.045f * MathF.Sin(f * MathHelper.Pi) * (1f - taut));
                cordPoints[k] = p;
                if (p.Y > lowest) {
                    lowest = p.Y;
                    cordLowIndex = k;
                }
            }

            float lakeY = domain.LakeWorldY;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;

            //出水拽带拍：第二只眼跟上后，脐带最低点被拉出水面的那一帧
            if (State == StateEmerge && !cordPulled && breachDone[1]
                && cordPoints[cordLowIndex].Y <= lakeY - 2f) {
                cordPulled = true;
                Vector2 mid = cordPoints[cordLowIndex];
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.55f, Pitch = -0.6f, MaxInstances = 2 }, mid);
                if (ViewedOwner) {
                    for (int s = -1; s <= 1; s++) {
                        KikasaDomainDeco.SplashAt(new Vector2(mid.X + s * 38f, lakeY), 6);
                    }
                    KikasaDomainDeco.RippleAt(new Vector2(mid.X, lakeY), 1.3f);
                    //绷直甩落的水珠沿带身散
                    for (int k = 2; k < CordSegs; k += 3) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            cordPoints[k] + Main.rand.NextVector2Circular(4f, 4f),
                            new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(1f, 3f)),
                            BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.55f))
                            ?.Configure(Main.rand.Next(14, 24), 0f);
                    }
                    ShakeViewer(2f);
                }
            }

            //中点周期坠血珠：松弛越多滴得越勤；溶解相拥期滴到最凶
            bool cordAlive = !(State == StateDissolve && cordSnapped)
                && EyeAlpha(0) > 0.5f && EyeAlpha(1) > 0.5f;
            if (!Main.dedServ && cordAlive && cordSlack > 30f && --cordDripTimer <= 0) {
                cordDripTimer = State == StateDissolve ? Main.rand.Next(4, 9) : Main.rand.Next(16, 30);
                Vector2 low = cordPoints[cordLowIndex];
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    low + Main.rand.NextVector2Circular(3f, 2f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.6f, 1.2f)),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(20, 34), 0.3f);
            }

            //带身蹭到湖面：低点亲水的细碎涟漪
            if (lakeAlive && ViewedOwner && cordAlive
                && MathF.Abs(cordPoints[cordLowIndex].Y - lakeY) < 10f && --cordWaterTimer <= 0) {
                cordWaterTimer = 20;
                KikasaDomainDeco.RippleAt(new Vector2(cordPoints[cordLowIndex].X, lakeY), 0.35f);
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

        /// <summary>贴图正面（虹膜/镜筒/巨口）朝向：rotation=0 时正面朝下</summary>
        private Vector2 FrontDirEye(int i) => (eyeRot[i] + MathHelper.PiOver2).ToRotationVector2();

        private void FaceEye(int i, Vector2 worldPos, float rate) {
            float want = (worldPos - eyePos[i]).ToRotation() - MathHelper.PiOver2;
            eyeRot[i] = eyeRot[i].AngleLerp(want, rate);
        }

        private void UpdateFrames() {
            int t = (int)StateTimer;
            //激光眼点射窗快闪，魔焰眼喷吐时巨口高频开合
            bool retActive = State == StatePulse && t > PulseBrakeEnd;
            bool spazActive = State == StateFlame && t > FlameApproachEnd;
            for (int i = 0; i < 2; i++) {
                int speed = i == 0 ? (retActive ? 4 : 7) : (spazActive ? 3 : 6);
                if (++eyeFrameTick[i] >= speed) {
                    eyeFrameTick[i] = 0;
                    eyeFrameIndex[i] = (eyeFrameIndex[i] + 1) % 3;
                }
            }
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private static int BreachTime(int i) => OmenFrames + i * BreachGap;

        private float EyeAlpha(int i) {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < BreachTime(i) ? 0f : MathHelper.Clamp((t - BreachTime(i)) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>uForm：1=全血水 0=真身。激光眼残留机械义眼底子更多、魔焰眼更水更有机</summary>
        private float EyeForm(int i) {
            int t = (int)StateTimer;
            float steady = (i == 0 ? 0.30f : 0.44f)
                + MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + Seed + i * 1.7f) * 0.05f;
            return State switch {
                StateEmerge => t < BreachTime(i)
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp(
                        (t - BreachTime(i)) / (float)(RiseEnd - BreachTime(i)), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.35f, 0f, 1f),
                _ => steady,
            };
        }

        /// <summary>uScanMode：出水期自上而下扫描凝实，落定后渐回噪声斑驳半沉态</summary>
        private float CurrentScanMode() {
            if (State != StateEmerge) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= RiseEnd) {
                return 1f;
            }
            return 1f - MathHelper.Clamp((t - RiseEnd) / (float)(ScanSettleEnd - RiseEnd), 0f, 1f);
        }

        private float CurrentDissolve()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp(StateTimer / 46f, 0f, 1f), 0.9f)
                : 0f;

        private float EyeScale(int i) {
            float scale = 0.86f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= BreachTime(i) && t < BreachTime(i) + 10) {
                //破水过冲
                scale *= 1f + 0.08f * (1f - (t - BreachTime(i)) / 10f);
            }
            else if (State == StateFlame && i == 1) {
                //蓄力鼓胀
                scale *= 1f + 0.07f * FlameCharge();
            }
            else if (State == StatePulse && i == 0) {
                //锁线眯眼收束
                scale *= 1f - 0.03f * PulseCharge();
            }
            return scale;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!eyesInit) {
                return false;
            }
            Main.instance.LoadNPC(NPCID.Retinazer);
            Main.instance.LoadNPC(NPCID.Spazmatism);
            Texture2D texR = TextureAssets.Npc[NPCID.Retinazer]?.Value;
            Texture2D texS = TextureAssets.Npc[NPCID.Spazmatism]?.Value;
            if (texR == null || texS == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            //剪切残影：只在交叉窗内亮
            DrawDashTrails(sb, texR, texS);

            //血脐带压在双眼身后
            DrawCord(sb);

            //双眼本体：血湖材质
            DrawBodies(sb, texR, texS);

            //加色层：预兆血光 / 觉醒瞳闪 / 预告线 / 蓄力积光 / 交错闪拍
            DrawGlow(sb);

            return false;
        }

        private Rectangle EyeFrame(Texture2D tex, int npcType, int i) {
            int frameH = tex.Height / Main.npcFrameCount[npcType];
            //常驻二形态：激光镜筒与魔焰巨口在下三帧
            return new Rectangle(0, frameH * (3 + eyeFrameIndex[i]), tex.Width, frameH);
        }

        private void DrawDashTrails(SpriteBatch sb, Texture2D texR, Texture2D texS) {
            if (State != StateScissor || ScissorPhase < 2) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                if (eyeVel[i].Length() < 15f) {
                    continue;
                }
                Texture2D tex = i == 0 ? texR : texS;
                Rectangle frame = EyeFrame(tex, i == 0 ? NPCID.Retinazer : NPCID.Spazmatism, i);
                Vector2 origin = frame.Size() * 0.5f;
                for (int k = eyeOld[i].Length - 1; k >= 1; k--) {
                    float fall = 1f - k / (float)eyeOld[i].Length;
                    sb.Draw(tex, eyeOld[i][k] - Main.screenPosition, frame,
                        BloodMain * (0.3f * fall), eyeOldRot[i][k],
                        origin, EyeScale(i) * (0.96f - k * 0.015f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawCord(SpriteBatch sb) {
            float alpha = MathF.Min(EyeAlpha(0), EyeAlpha(1));
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            if (State == StateDissolve && cordSnapped) {
                DrawCordHalves(sb, tex, alpha);
                return;
            }
            if (alpha <= 0.02f) {
                return;
            }
            float taut = CordTaut();
            Color dark = BloodDark * (0.85f * alpha);
            Color main = BloodMain * (0.95f * alpha);
            Color sheen = (BloodBright with { A = 0 }) * ((0.26f + taut * 0.45f) * alpha);
            Color hot = (new Color(255, 208, 196) with { A = 0 })
                * (taut * alpha * (0.6f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 26f + Seed)));
            Vector2 origin = tex.Size() * 0.5f;

            for (int k = 0; k < CordSegs; k++) {
                Vector2 p0 = cordPoints[k];
                Vector2 p1 = cordPoints[k + 1];
                Vector2 mid = (p0 + p1) * 0.5f - Main.screenPosition;
                float rot = (p1 - p0).ToRotation();
                float len = Vector2.Distance(p0, p1) + 2f;
                float f = (k + 0.5f) / CordSegs;
                //中段坠得更粗，绷直整体收细
                float w = (4.6f + 1.4f * MathF.Sin(f * MathHelper.Pi)) * (1f - taut * 0.35f);

                Vector2 lenScale = new(len / tex.Width, 1f);
                sb.Draw(tex, mid, null, dark, rot, origin,
                    lenScale * new Vector2(1f, (w + 3.4f) * 2.2f / tex.Height), SpriteEffects.None, 0f);
                sb.Draw(tex, mid, null, main, rot, origin,
                    lenScale * new Vector2(1f, w * 2.2f / tex.Height), SpriteEffects.None, 0f);
                sb.Draw(tex, mid, null, sheen, rot, origin,
                    lenScale * new Vector2(1f, w * 0.9f / tex.Height), SpriteEffects.None, 0f);
                if (taut > 0.05f) {
                    sb.Draw(tex, mid, null, hot, rot, origin,
                        lenScale * new Vector2(1f, w * 0.55f / tex.Height), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>断带后的两截残带：向各自眼体回缩，末端下垂淌血</summary>
        private void DrawCordHalves(SpriteBatch sb, Texture2D tex, float alpha) {
            float retract = MathHelper.Clamp((StateTimer - DissolveSnapFrame) / 12f, 0f, 1f);
            if (retract >= 1f || alpha <= 0.02f) {
                return;
            }
            Vector2 origin = tex.Size() * 0.5f;
            float fade = alpha * (1f - retract);
            Color dark = BloodDark * (0.85f * fade);
            Color main = BloodMain * (0.9f * fade);

            for (int i = 0; i < 2; i++) {
                Vector2 anchor = CordAnchor(i);
                Vector2 freeEnd = Vector2.Lerp(snapMid, anchor, retract)
                    + new Vector2(0f, 34f * (1f - retract));
                const int halfSegs = 6;
                for (int k = 0; k < halfSegs; k++) {
                    float f0 = k / (float)halfSegs;
                    float f1 = (k + 1) / (float)halfSegs;
                    //残带自身也下垂
                    Vector2 p0 = Vector2.Lerp(anchor, freeEnd, f0) + new Vector2(0f, 18f * f0 * f0 * (1f - retract));
                    Vector2 p1 = Vector2.Lerp(anchor, freeEnd, f1) + new Vector2(0f, 18f * f1 * f1 * (1f - retract));
                    Vector2 mid = (p0 + p1) * 0.5f - Main.screenPosition;
                    float rot = (p1 - p0).ToRotation();
                    float len = Vector2.Distance(p0, p1) + 2f;
                    float w = 4.4f * (1f - f1 * 0.5f);
                    Vector2 lenScale = new(len / tex.Width, 1f);
                    sb.Draw(tex, mid, null, dark, rot, origin,
                        lenScale * new Vector2(1f, (w + 3f) * 2.2f / tex.Height), SpriteEffects.None, 0f);
                    sb.Draw(tex, mid, null, main, rot, origin,
                        lenScale * new Vector2(1f, w * 2.2f / tex.Height), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawBodies(SpriteBatch sb, Texture2D texR, Texture2D texS) {
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
                form.Parameters["uDissolve"]?.SetValue(CurrentDissolve());
            }

            for (int i = 0; i < 2; i++) {
                float alpha = EyeAlpha(i);
                if (alpha <= 0.01f) {
                    continue;
                }
                Texture2D tex = i == 0 ? texR : texS;
                int npcType = i == 0 ? NPCID.Retinazer : NPCID.Spazmatism;
                Rectangle frame = EyeFrame(tex, npcType, i);

                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 2.3f);
                    form.Parameters["uForm"]?.SetValue(EyeForm(i));
                    form.Parameters["uUvRect"]?.SetValue(new Vector4(
                        frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                        frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                    form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                    form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    //无着色器回退：CPU 血染
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }

                sb.Draw(tex, eyePos[i] - Main.screenPosition, frame, color,
                    eyeRot[i], frame.Size() * 0.5f, EyeScale(i), SpriteEffects.None, 0f);
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

            //预兆：两处水下血光并肩上浮
            if (State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                EnsureBegin();
                for (int s = 0; s < 2; s++) {
                    Vector2 pos = new(Projectile.Center.X + Side(s) * EmergeHalfSpan,
                        domain.LakeWorldY + MathHelper.Lerp(46f, 8f, ease));
                    float r = 26f + 18f * ease;
                    sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * (0.4f * ease), 0f,
                        gOrigin, new Vector2(r * 2.4f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //觉醒双拍：瞳孔先后灼亮
            if (State == StateEmerge && t >= AwakenFrame) {
                for (int i = 0; i < 2; i++) {
                    int start = AwakenFrame + i * 4;
                    if (t < start) {
                        continue;
                    }
                    float f = MathHelper.Clamp((t - start) / (float)(EmergeTotal - start), 0f, 1f);
                    float a = MathF.Sin(f * MathHelper.Pi) * 0.7f;
                    if (a > 0.02f) {
                        EnsureBegin();
                        Vector2 pupil = eyePos[i] + FrontDirEye(i) * 26f;
                        float r = 13f + 12f * f;
                        sb.Draw(glow, pupil - Main.screenPosition, null,
                            (i == 0 ? PulseHot : BloodBright) * a, 0f,
                            gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                    }
                }
            }

            //点射：镜筒积光 + 弹道预告线（细、快闪，出膛前一亮）
            float charge = PulseCharge();
            float flash = TelegraphFlash();
            if ((charge > 0.03f || flash > 0.02f) && EyeAlpha(0) > 0.1f) {
                EnsureBegin();
                Vector2 muzzle = PulseMuzzle();
                float r = 8f + 14f * charge;
                sb.Draw(glow, muzzle - Main.screenPosition, null, PulseHot * (0.5f * MathF.Max(charge, flash)), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);

                float lineA = charge * (0.14f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + Seed))
                    + flash * 0.5f;
                if (lineA > 0.02f) {
                    Vector2 dir = aimRotRet.ToRotationVector2();
                    const int lineSegs = 3;
                    for (int k = 0; k < lineSegs; k++) {
                        float f0 = k / (float)lineSegs;
                        Vector2 segMid = muzzle + dir * pulseAimDist * (f0 + 0.5f / lineSegs);
                        float segLen = pulseAimDist / lineSegs;
                        float fallA = lineA * (1f - f0 * 0.35f);
                        sb.Draw(glow, segMid - Main.screenPosition, null, PulseHot * fallA, aimRotRet,
                            gOrigin, new Vector2(segLen * 1.15f / glow.Width, 3.2f / glow.Height), SpriteEffects.None, 0f);
                        sb.Draw(glow, segMid - Main.screenPosition, null, Color.White * (fallA * 0.5f), aimRotRet,
                            gOrigin, new Vector2(segLen * 1.1f / glow.Width, 1.3f / glow.Height), SpriteEffects.None, 0f);
                    }
                }
            }

            //吐息蓄力：巨口积光，随蓄力鼓大
            float flame = FlameCharge();
            if (flame > 0.03f && EyeAlpha(1) > 0.1f) {
                EnsureBegin();
                Vector2 maw = eyePos[1] + FrontDirEye(1) * 28f;
                float r = 9f + 20f * flame;
                sb.Draw(glow, maw - Main.screenPosition, null, BloodMain * (0.55f * flame), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                sb.Draw(glow, maw - Main.screenPosition, null, CursedTinge * (0.2f * flame), 0f,
                    gOrigin, new Vector2(r * 3f / glow.Width), SpriteEffects.None, 0f);
            }

            //交错闪拍：剪切交点的十字亮痕余光
            if (crossFlashTick > 0) {
                EnsureBegin();
                float a = crossFlashTick / 8f;
                Vector2 mid = Vector2.Lerp(eyePos[0], eyePos[1], 0.5f);
                sb.Draw(glow, mid - Main.screenPosition, null, BloodBright * (0.6f * a), 0f,
                    gOrigin, new Vector2(60f * a * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //剪切命中：沿脐带法向的双向撕开溅血（OnHit 只在 owner 端跑）
            if (Main.dedServ) {
                return;
            }
            Vector2 perp = (eyePos[1] - eyePos[0]).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 8; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(18f, 18f),
                    perp * side * Main.rand.NextFloat(2f, 6f) + Main.rand.NextVector2Circular(1.5f, 1.5f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 26), 0.35f);
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = -0.25f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠：两眼与脐带中点各留一口血水
            if (Main.dedServ || !eyesInit) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                for (int k = 0; k < 6; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        eyePos[i] + Main.rand.NextVector2Circular(24f, 24f),
                        new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.6f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(14, 26), 0f);
                }
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                Vector2.Lerp(eyePos[0], eyePos[1], 0.5f),
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
