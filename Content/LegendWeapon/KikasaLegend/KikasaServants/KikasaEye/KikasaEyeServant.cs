using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye
{
    /// <summary>
    /// 鬼奴·湖水版克苏鲁之眼。血湖之水凝成的眼球随从：
    /// 出水四拍（预兆聚涟漪→破水浪冠→血水升起凝实→觉醒睁瞳），
    /// 战斗循环为三连冲刺与血弹连射交替，领域绑定，收域/退水/主人死亡即溶解回湖。
    /// 状态机各端同推（规则确定性），owner 在每次转场盖 netUpdate 章纠偏；
    /// 血弹只在 owner 端生成，演出节拍用本地闩防快照回卷重播
    /// </summary>
    internal class KikasaEyeServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>冲刺接触基伤（召唤加成前）</summary>
        internal const int DashDamage = 500;

        /// <summary>血弹基伤（召唤加成前）</summary>
        internal const int ShotDamage = 300;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateDash = 2;
        private const int StateVolley = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子计数：冲刺=已完成段数，连射=已发弹数，溶解=过水线闩</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：预兆→破水→升起凝实→落定→觉醒
        private const int OmenFrames = 24;
        private const int RiseEnd = 60;
        private const int SettleEnd = 74;
        private const int AwakenFrame = 74;
        private const int EmergeTotal = 88;

        //冲刺：首段预备长（读得懂），后两段短（压迫感）；发力集中在 launch 一帧
        private const int DashFirstWindup = 22;
        private const int DashNextWindup = 15;
        private const int DashActiveFrames = 12;
        private const int DashBrakeFrames = 16;
        private const int DashCount = 3;

        //连射：刹停→蓄力（72% 后静默）→四发→回摆
        private const int VolleyBrakeEnd = 10;
        private const int VolleyChargeEnd = 36;
        private const int VolleyShotGap = 7;
        private const int VolleyShotCount = 4;
        private const int VolleyFireEnd = VolleyChargeEnd + VolleyShotGap * VolleyShotCount;
        private const int VolleyRecoverEnd = VolleyFireEnd + 16;

        private const int DissolveFrames = 46;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int frameTick;
        private int frameIndex;
        private bool mouthOpen;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool breachDone;
        private bool awakenDone;
        private int lastDashLaunched = -1;
        private int lastShotFired = -1;
        private bool dissolveSplashed;
        private bool brakeFlungOnce;

        //记忆脉冲：出手/命中/主人受击时原色随带自下而上扫一遍（纯本地表现量）
        private const int PulseFrames = 30;
        private int pulseTimer = PulseFrames;
        private int ownerLifePrev;

        //血系配色随观看域鬼雨异化冷化，与沉溺/湖藏同族
        private static Color BloodTint => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(DashDamage);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 26f), Vector2.Zero,
                ModContent.ProjectileType<KikasaEyeServant>(), damage, 7f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = 92;
            Projectile.height = 92;
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

        /// <summary>接触伤害只开在冲刺激活窗，与可见的突进严格对齐</summary>
        public override bool? CanDamage() {
            if (State != StateDash) {
                return false;
            }
            int windup = (int)StateParam == 0 ? DashFirstWindup : DashNextWindup;
            int t = (int)StateTimer;
            return t > windup && t <= windup + DashActiveFrames ? null : false;
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
            //还没破水就要收场：什么都没露出来，不走溶解演出
            //否则透明度会从 0 跳到 1，水下凭空闪出一只眼再化掉
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

            //生命线：湖塌/收域/退水/主人死亡 → 溶解回湖。只有 owner 裁决
            //服务器没有领域状态（恒 Closed 是既定契约），在那边跑这条会把鬼奴当场判死；
            //迟入场的客户端在首份领域快照到达前同样会误判。其余端只跟 owner 的同步包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            //伤害随召唤加成逐帧刷新，命中在 owner 端结算
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(DashDamage);

            //主人受击 → 记忆震颤（各端都能从生命值下降本地判知，不用同步）
            if (owner.statLife < ownerLifePrev) {
                StartPulse();
            }
            ownerLifePrev = owner.statLife;
            if (pulseTimer < PulseFrames) {
                pulseTimer++;
            }

            //换场清闩：远端可能靠收包切状态而非本地同拍转场，
            //上一场残闩会吞掉新场的节拍（起跳音、后坐、过水线拍）
            if (State != lastSeenState) {
                lastSeenState = State;
                lastDashLaunched = -1;
                lastShotFired = -1;
                brakeFlungOnce = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateDash: UpdateDash(owner, authority); break;
                case StateVolley: UpdateVolley(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateFrames();
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            float glow = CurrentAlpha() * 0.55f;
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.42f * glow, 0.10f * glow, 0.09f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水 ====================

        private void UpdateEmerge(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                //水下待命：湖面先给预兆
                Projectile.velocity = Vector2.Zero;
                if (viewed) {
                    if (t % 6 == 2) {
                        //涟漪自外向出水点收拢，一圈比一圈近、比一圈大
                        float converge = 1f - t / (float)OmenFrames;
                        float side = t / 6 % 2 == 0 ? 1f : -1f;
                        KikasaDomainDeco.RippleAt(
                            new Vector2(Projectile.Center.X + side * converge * 52f, lakeY),
                            0.4f + (1f - converge) * 0.55f);
                    }
                    if (t == 4 || t == 16) {
                        SoundEngine.PlaySound(SoundID.Drip with {
                            Volume = 0.45f,
                            Pitch = t == 4 ? -0.4f : -0.1f,
                            MaxInstances = 2
                        }, new Vector2(Projectile.Center.X, lakeY));
                    }
                }
                return;
            }

            if (!breachDone) {
                //破水拍：一帧起速 + 浪冠水柱 + 闷吼
                breachDone = true;
                Projectile.velocity = new Vector2(0f, -11.5f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.55f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            //升起：起速后指数衰减，前快后慢，禁匀速
            Projectile.velocity.Y *= 0.955f;
            Projectile.velocity.X = 0f;

            if (viewed && t < RiseEnd) {
                //身上的血水成帘往下淌，落点连环小涟漪
                if (t % 2 == 0) {
                    Vector2 dropPos = Projectile.Center + new Vector2(
                        Main.rand.NextFloat(-34f, 34f), Main.rand.NextFloat(6f, 30f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2.4f, 3.8f)),
                        BloodTint * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.45f, 0.7f))
                        ?.Configure(Main.rand.Next(14, 26), 0f);
                }
                if (t % 5 == 3) {
                    KikasaDomainDeco.RippleAt(
                        new Vector2(Projectile.Center.X + Main.rand.NextFloat(-20f, 20f), lakeY), 0.35f);
                }
            }

            if (!awakenDone && t >= AwakenFrame) {
                //觉醒拍：瞳孔亮起转向猎物
                awakenDone = true;
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 0.55f);
                    ShakeViewer(1.5f);
                }
            }

            //升起期张口低吼朝下，觉醒后转向目标
            if (t < AwakenFrame) {
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);
            }
            else {
                int target = FindTarget(Owner);
                Vector2 look = target >= 0 ? Main.npc[target].Center : Main.player[Projectile.owner].Center;
                FaceToward(look, 0.25f);
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

        /// <summary>破水浪冠：大环涟漪 + 扇形血珠 + 垂直水柱束 + 血雾，量级压过物件浮出一头</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.4f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(38f, 0f), 1.0f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(34f, 0f), 0.9f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-16f, 0f), 12);
            KikasaDomainDeco.SplashAt(hit + new Vector2(16f, 0f), 12);

            //浪冠：扇形血珠向外上抛
            for (int i = 0; i < 22; i++) {
                float angle = -MathHelper.Pi * (0.12f + 0.76f * i / 21f);
                float speed = Main.rand.NextFloat(3.2f, 7.4f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-26f, 26f), -4f),
                    angle.ToRotationVector2() * speed,
                    BloodTint * Main.rand.NextFloat(0.45f, 0.68f),
                    Main.rand.NextFloat(0.5f, 0.85f))
                    ?.Configure(Main.rand.Next(22, 36), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            //水柱束：近垂直高抛，回落自然成雨
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-8f, 8f), -6f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(8.5f, 13f)),
                    BloodTint * Main.rand.NextFloat(0.5f, 0.7f),
                    Main.rand.NextFloat(0.55f, 0.9f))
                    ?.Configure(Main.rand.Next(34, 50), Main.rand.NextFloat(-0.3f, 0.3f));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-30f, 30f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.7f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.7f, 1.0f))
                    ?.Configure(Main.rand.Next(60, 100));
            }

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.3f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.5f, Pitch = -0.7f, MaxInstances = 1 }, hit);
            ShakeViewer(5f);
        }

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            //悬在主人侧上方，呼吸浮动
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 86f, -112f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f + Seed) * 6f;
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.3f + Seed * 2f) * 4f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢就贴回来，别在半个地图外淌血
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.085f;
            const float maxSpeed = 17f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);

            //有猎物盯猎物，闲着看主人
            Vector2 look = target >= 0 ? Main.npc[target].Center : owner.Center;
            FaceToward(look, 0.14f);

            //轮廓下缘偶发凝珠滴落
            if (!Main.dedServ && Main.rand.NextBool(24)) {
                DripFromRim();
            }

            //出手裁决：冲刺与连射交替；转场规则各端一致，owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 26) {
                attackIndex++;
                State = attackIndex % 2 == 1 ? StateDash : StateVolley;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 三连冲刺 ====================

        private void UpdateDash(Player owner, bool authority) {
            int dashIndex = (int)StateParam;
            int windup = dashIndex == 0 ? DashFirstWindup : DashNextWindup;
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            //目标没了就收势回位
            if (target < 0 && t <= windup) {
                EndAttack(authority, 45);
                return;
            }

            Vector2 aimPos = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 9f
                : Projectile.Center + FrontDir() * 300f;
            Vector2 aim = (aimPos - Projectile.Center).SafeNormalize(-Vector2.UnitY);

            if (t <= windup) {
                //迟发后拉：pow(6) 憋到最后几帧猛吸一口气
                float k = MathF.Pow(t / (float)windup, 6f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -aim * (2f + 16f * k), 0.35f);
                FaceToward(aimPos, 0.45f);

                //蓄势收拢的血珠，72% 后静默，爆发前的吸气
                if (!Main.dedServ && t < windup * 0.72f && t % 3 == 1) {
                    Vector2 mouth = MouthPos();
                    Vector2 from = mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(48f, 90f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        (mouth - from) * 0.16f,
                        BloodTint * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(8, 0f);
                }
                return;
            }

            if (lastDashLaunched < dashIndex) {
                //launch 一帧设速，不做斜坡；owner 重新取新鲜瞄准并盖章
                lastDashLaunched = dashIndex;
                Projectile.velocity = aim * 29f;
                Projectile.netUpdate = authority;
                brakeFlungOnce = false;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = -0.15f + dashIndex * 0.08f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
                StartPulse();
                if (ViewedOwner) {
                    ShakeViewer(3f);
                }
            }

            if (t <= windup + DashActiveFrames) {
                //冲刺段：复利续力，不转向，直才快
                Projectile.velocity *= 1.013f;
                Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
                //沿途甩出速度拉伸的血水
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center - Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(20f, 20f),
                        -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                        BloodTint * 0.55f, Main.rand.NextFloat(0.4f, 0.65f))
                        ?.Configure(Main.rand.Next(10, 18), 0f);
                }
                return;
            }

            if (t <= windup + DashActiveFrames + DashBrakeFrames) {
                //硬刹：×0.68 急停读出撞墙般的分量，甩出水珠是重量的答话
                Projectile.velocity *= t <= windup + DashActiveFrames + 5 ? 0.68f : 0.9f;
                if (!brakeFlungOnce) {
                    brakeFlungOnce = true;
                    if (!Main.dedServ) {
                        for (int i = 0; i < 7; i++) {
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(
                                Projectile.Center + Main.rand.NextVector2Circular(26f, 26f),
                                Projectile.velocity * 0.35f + Main.rand.NextVector2Circular(2.2f, 2.2f),
                                BloodTint * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                                ?.Configure(Main.rand.Next(12, 22), Main.rand.NextFloat(-0.4f, 0.4f));
                        }
                    }
                }
                //过冲回摆
                Vector2 look = target >= 0 ? Main.npc[target].Center : owner.Center;
                FaceToward(look, 0.1f);
                return;
            }

            //本段结束
            StateParam++;
            StateTimer = 0;
            if ((int)StateParam >= DashCount) {
                EndAttack(authority, 110);
            }
        }

        //==================== 血弹连射 ====================

        private void UpdateVolley(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= VolleyChargeEnd) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center : Projectile.Center + FrontDir() * 300f;
            Vector2 aim = (aimPos - Projectile.Center).SafeNormalize(-Vector2.UnitY);

            if (t <= VolleyBrakeEnd) {
                Projectile.velocity *= 0.82f;
                FaceToward(aimPos, 0.3f);
                return;
            }

            if (t <= VolleyChargeEnd) {
                //蓄力：身体后倾、鼓动；汇聚流在绘制层（确定性流线），密度随蓄力
                float charge = (t - VolleyBrakeEnd) / (float)(VolleyChargeEnd - VolleyBrakeEnd);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -aim * 1.4f, 0.1f);
                FaceToward(aimPos, 0.35f);
                if (t == VolleyBrakeEnd + 2) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Volume = 0.45f, Pitch = -0.75f, MaxInstances = 2 }, Projectile.Center);
                }
                //蓄力血珠向瞳孔汇聚，72% 静默截断
                if (!Main.dedServ && charge < 0.72f && t % 2 == 0) {
                    Vector2 mouth = MouthPos();
                    Vector2 from = mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(50f, 110f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        (mouth - from) * 0.14f,
                        BloodTint * (0.35f + charge * 0.3f), Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(9, 0f);
                }
                return;
            }

            if (t <= VolleyFireEnd) {
                FaceToward(aimPos, 0.4f);
                int shotIndex = (t - VolleyChargeEnd) / VolleyShotGap;
                if ((t - VolleyChargeEnd) % VolleyShotGap == 0 && shotIndex < VolleyShotCount
                    && lastShotFired < shotIndex) {
                    lastShotFired = shotIndex;
                    StateParam = shotIndex + 1;
                    FireBloodShot(owner, aim, authority);
                    if (shotIndex == 0) {
                        //连射开火拍只在首发闪回一次，四连逐发会抖成频闪
                        StartPulse();
                    }
                }
                //弹间悬停微稳
                Projectile.velocity *= 0.9f;
                return;
            }

            if (t >= VolleyRecoverEnd) {
                EndAttack(authority, 90);
            }
            else {
                Projectile.velocity *= 0.92f;
            }
        }

        private void FireBloodShot(Player owner, Vector2 aim, bool authority) {
            //每发后坐：知重量者先退半步
            Projectile.velocity -= aim * 4.5f;

            Vector2 mouth = MouthPos();
            //吐痰的湿噗声，不是水花
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.5f, Pitch = -0.25f, MaxInstances = 3 }, mouth);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.35f, Pitch = -0.45f, MaxInstances = 3 }, mouth);
            if (!Main.dedServ) {
                //出膛喷吐：锥形血珠 + 一圈扩散环
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(mouth + Main.rand.NextVector2Circular(3f, 3f),
                        aim.RotatedByRandom(0.26f) * Main.rand.NextFloat(3f, 8f),
                        Main.rand.NextBool(3) ? KikasaEyeBloodShot.BloodDeep : KikasaEyeBloodShot.BloodMain,
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 24));
                }
                PRTLoader.NewParticle<PRT_DWave>(mouth + aim * 8f, Vector2.Zero,
                    KikasaEyeBloodShot.BloodDeep, 0.07f)
                    ?.Configure(new Vector2(0.55f, 1f), aim.ToRotation(), 0.22f, 8);
            }
            if (ViewedOwner) {
                ShakeViewer(0.8f);
            }

            //弹体只在 owner 端生成，spawn 包自带全部初值
            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ShotDamage);
                Vector2 vel = aim.RotatedBy(Main.rand.NextFloat(-0.055f, 0.055f)) * 15f;
                //痰是抛出去的：给一点上抛偏置，配合弹体重力走弧线
                vel.Y -= 1.3f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), mouth, vel,
                    ModContent.ProjectileType<KikasaEyeBloodShot>(), damage, 3.5f, Projectile.owner);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解回湖 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;

            if (lakeAlive) {
                //坠回湖里
                Projectile.velocity.X *= 0.92f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.28f, 9f);
            }
            else {
                //湖已不在：原地化水
                Projectile.velocity *= 0.9f;
            }

            //过水线拍（一次）
            if (lakeAlive && !dissolveSplashed && Projectile.Center.Y >= lakeY) {
                dissolveSplashed = true;
                StateParam = 1f;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    Vector2 hit = new(Projectile.Center.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 10);
                    KikasaDomainDeco.RippleAt(hit, 1.3f);
                    ShakeViewer(2f);
                }
            }

            //边沉边化成血珠
            if (!Main.dedServ && t % 2 == 0 && CurrentAlpha() > 0.15f) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.5f, 3f)),
                    BloodTint * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
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

        /// <summary>贴图正面（虹膜/口）朝向：rotation=0 时正面朝下</summary>
        private Vector2 FrontDir() => (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();

        private Vector2 MouthPos() => Projectile.Center + FrontDir() * 34f;

        private void FaceToward(Vector2 worldPos, float rate) {
            float want = (worldPos - Projectile.Center).ToRotation() - MathHelper.PiOver2;
            Projectile.rotation = Projectile.rotation.AngleLerp(want, rate);
        }

        private void DripFromRim() {
            //轮廓下缘凝珠：先挂一拍再坠（速度先小后由粒子重力接管）
            Vector2 rim = Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(18f, 34f));
            PRTLoader.NewParticle<PRT_GhostRainDrop>(rim,
                new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                BloodTint * Main.rand.NextFloat(0.4f, 0.55f),
                Main.rand.NextFloat(0.35f, 0.6f))
                ?.Configure(Main.rand.Next(20, 34), 0f);
        }

        private void UpdateFrames() {
            int t = (int)StateTimer;
            mouthOpen = State switch {
                StateDash => true,
                StateVolley => t > VolleyBrakeEnd && t < VolleyFireEnd + 6,
                StateEmerge => t >= OmenFrames && t < 66,
                _ => false,
            };
            if (++frameTick >= (mouthOpen ? 5 : 8)) {
                frameTick = 0;
                frameIndex = (frameIndex + 1) % 3;
            }
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float CurrentAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>记忆脉冲重起拍：原色随带自下而上扫一遍</summary>
        private void StartPulse() => pulseTimer = 0;

        /// <summary>
        /// uForm：1=全液态血躯 0=落定鬼躯；落定态基本全鬼躯（血玻璃材质自带活性），
        /// 只留一丝微沸呼吸，出水仍自上而下凝实、溶解回液
        /// </summary>
        private float CurrentForm() {
            int t = (int)StateTimer;
            float steady = 0.06f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + Seed) * 0.03f;
            return State switch {
                StateEmerge => t < OmenFrames
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp((t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.55f, 0f, 1f),
                _ => steady,
            };
        }

        /// <summary>uScanMode：出水期走自上而下扫描，落定后渐变回噪声斑驳的半沉态</summary>
        private float CurrentScanMode() {
            if (State != StateEmerge) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= RiseEnd) {
                return 1f;
            }
            return 1f - MathHelper.Clamp((t - RiseEnd) / (float)(SettleEnd - RiseEnd), 0f, 1f);
        }

        private float CurrentDissolve()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp(StateTimer / 38f, 0f, 1f), 0.9f)
                : 0f;

        private float BodyScale() {
            float scale = 0.92f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 10) {
                //破水过冲
                scale *= 1f + 0.08f * (1f - (t - OmenFrames) / 10f);
            }
            else if (State == StateVolley && t > VolleyBrakeEnd && t <= VolleyChargeEnd) {
                float charge = (t - VolleyBrakeEnd) / (float)(VolleyChargeEnd - VolleyBrakeEnd);
                scale *= 1f + 0.07f * charge;
            }
            return scale;
        }

        /// <summary>连射蓄力进度 0~1，绘制层汇聚流线与瞳孔灼亮共用</summary>
        private float ChargeLevel() {
            if (State != StateVolley) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= VolleyBrakeEnd || t > VolleyFireEnd) {
                return 0f;
            }
            if (t <= VolleyChargeEnd) {
                return (t - VolleyBrakeEnd) / (float)(VolleyChargeEnd - VolleyBrakeEnd);
            }
            //射击窗内维持余温
            return 0.6f;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.EyeofCthulhu);
            Texture2D tex = TextureAssets.Npc[NPCID.EyeofCthulhu]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.EyeofCthulhu];
            Rectangle frame = new(0, frameH * (frameIndex + (mouthOpen ? 3 : 0)), tex.Width, frameH);

            float alpha = CurrentAlpha();
            SpriteBatch sb = Main.spriteBatch;

            //冲刺残影：只在高速时亮，速度门控免得常开成噪声
            float speed = Projectile.velocity.Length();
            if (alpha > 0.1f && speed > 15f) {
                Vector2 origin = frame.Size() * 0.5f;
                for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                    Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                    if (oldCenter == Projectile.Size * 0.5f) {
                        continue;
                    }
                    float fall = 1f - k / (float)Projectile.oldPos.Length;
                    sb.Draw(tex, oldCenter - Main.screenPosition, frame,
                        BloodTint * (0.32f * fall * alpha), Projectile.oldRot[k],
                        origin, BodyScale() * (0.96f - k * 0.015f), SpriteEffects.None, 0f);
                }
            }

            //本体：血湖材质
            if (alpha > 0.01f) {
                DrawBody(sb, tex, frame, alpha);
            }

            //加色层：预兆水下血光 / 蓄力汇聚流线 / 瞳孔灼亮
            DrawGlow(sb, alpha);

            return false;
        }

        private void DrawBody(SpriteBatch sb, Texture2D tex, Rectangle frame, float alpha) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (KikasaServantGhostDraw.Ready) {
                float pulsePhase = MathHelper.Clamp(pulseTimer / (float)PulseFrames, 0f, 1f);
                KikasaServantGhostDraw.Apply(tex, frame, new KikasaGhostParams {
                    Seed = Seed,
                    Form = CurrentForm(),
                    Dissolve = CurrentDissolve(),
                    ScanMode = CurrentScanMode(),
                    Liquefy = 0.85f,
                    //钟形包络：闪回渐入渐出，不硬切
                    Pulse = pulseTimer < PulseFrames ? MathF.Sin(pulsePhase * MathHelper.Pi) : 0f,
                    PulsePhase = pulsePhase,
                    Memory = 0f,
                });
                KikasaServantGhostDraw.DrawPadded(sb, tex, frame,
                    Projectile.Center - Main.screenPosition,
                    new Color(255, 255, 255, (byte)(alpha * 255f)),
                    Projectile.rotation, frame.Size() * 0.5f, new Vector2(BodyScale()),
                    SpriteEffects.None);
            }
            else {
                //无着色器回退：CPU 血染
                sb.Draw(tex, Projectile.Center - Main.screenPosition, frame,
                    Color.Lerp(Color.White, BloodTint, 0.55f) * alpha,
                    Projectile.rotation, frame.Size() * 0.5f, BodyScale(), SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawGlow(SpriteBatch sb, float alpha) {
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

            //预兆：水下血光自深处上浮（复刻湖藏浮出预兆但更宽更亮，这是生物不是物件）
            if (State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(52f, 8f, ease));
                float r = 34f + 22f * ease;
                EnsureBegin();
                sb.Draw(glow, pos - Main.screenPosition, null, FoamGlow * (0.42f * ease), 0f,
                    gOrigin, new Vector2(r * 2.6f / glow.Width, r * 1.1f / glow.Height), SpriteEffects.None, 0f);
            }

            //觉醒拍：瞳孔灼亮一闪
            if (State == StateEmerge && t >= AwakenFrame) {
                float f = MathHelper.Clamp((t - AwakenFrame) / (float)(EmergeTotal - AwakenFrame), 0f, 1f);
                float a = MathF.Sin(f * MathHelper.Pi) * 0.75f;
                if (a > 0.02f) {
                    EnsureBegin();
                    Vector2 pupil = MouthPos();
                    float r = 16f + 14f * f;
                    sb.Draw(glow, pupil - Main.screenPosition, null, FoamGlow * a, 0f,
                        gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //连射蓄力：瞳孔积光 + 汇聚流线（确定性流线，各端一致）
            float charge = ChargeLevel();
            if (charge > 0.03f && alpha > 0.1f) {
                EnsureBegin();
                Vector2 mouth = MouthPos();
                float r = 10f + 20f * charge;
                sb.Draw(glow, mouth - Main.screenPosition, null, FoamGlow * (0.55f * charge), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);

                //吸入流线：各向异性拉长、指向瞳孔，密度随蓄力、蓄力末段 72% 后静默
                //（射击窗 ChargeLevel 回落 0.6，流线自然复燃为余吸）
                if (charge < 0.72f) {
                    int streaks = 7;
                    for (int i = 0; i < streaks; i++) {
                        float phase = (Main.GlobalTimeWrappedHourly * 0.9f + i / (float)streaks + Seed * 0.13f) % 1f;
                        float ang = Seed + i * MathHelper.TwoPi / streaks + MathF.Sin(Seed * 3f + i) * 0.7f;
                        float dist = MathHelper.Lerp(96f, 18f, phase);
                        Vector2 pos = mouth + ang.ToRotationVector2() * dist;
                        float a = charge * 0.4f * MathF.Sin(phase * MathHelper.Pi);
                        sb.Draw(glow, pos - Main.screenPosition, null, FoamGlow * a, ang,
                            gOrigin, new Vector2(30f / glow.Width * 2.2f, 8f / glow.Height), SpriteEffects.None, 0f);
                    }
                }
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //冲刺撞击的溅血（OnHit 只在 owner 端跑，队友看拖尾即可；脉冲同样 owner 本地）
            StartPulse();
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    Projectile.velocity * 0.25f + Main.rand.NextVector2Circular(2.5f, 2.5f),
                    BloodTint * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠：溶解尾拍或异常移除都留一口血水
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(28f, 28f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.8f)),
                    BloodTint * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26), 0f);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
