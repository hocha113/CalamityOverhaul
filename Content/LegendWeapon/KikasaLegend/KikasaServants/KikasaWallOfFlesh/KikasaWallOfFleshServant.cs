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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaWallOfFlesh
{
    /// <summary>
    /// 鬼奴·湖水版血肉墙：界墙残像，唯一的区域压制者。不是全图墙——
    /// 一面局部墙段从湖里升起（基座永浸水下），墙面嵌两眼一口。
    /// 出水四拍（涟漪横排宽度预告→整排破水浪冠→墙体指数衰减推升、水帘倾泻→双眼觉醒）；
    /// 跟随即压制：朝目标恒缓速水平漂移，不追不冲，接触判定覆盖整面墙；
    /// 攻击为双眼交替细激光剪切（细、快、短促）与口吐游空水蛭；
    /// 溶解为墙列自两侧向中间滑塌回湖，建筑倒塌式谢幕。
    /// 联机同基准契约：转场纯计时确定性、owner 盖 netUpdate 章，
    /// 子弹幕只在 owner 端生成且 spawn 参数完备，演出节拍用本地闩防快照回卷
    /// </summary>
    internal class KikasaWallOfFleshServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>墙体接触基伤（召唤加成前）</summary>
        internal const int ContactDamage = 560;

        /// <summary>眼激光与水蛭基伤（召唤加成前），由攻击弹幕消费</summary>
        internal const int RayDamage = 300;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateAdvance = 1;
        private const int StateEyeLaser = 2;
        private const int StateLeechSpit = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子计数：激光=已发射根数，吐蛭=已吐条数；出水期存初始面向（±1）</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：涟漪横排预告→整排破水→指数推升→觉醒
        private const int OmenFrames = 40;
        private const int RiseEnd = 112;
        private const int SettleEnd = 126;
        private const int AwakenFrame = 114;
        private const int EmergeTotal = 134;

        //双眼剪切：锁定蓄光→上眼横切→下眼斜切（反向扫、与上道交叉）→收势
        private const int LaserLockFrames = 22;
        private const int LaserSecondDelay = 12;

        //吐蛭：咀嚼蓄势（72% 后静默）→三口错拍吐出→回摆
        private const int ChewFrames = 28;
        private const int SpitGap = 11;
        private const int SpitCount = 3;
        private const int LeechStateEnd = 80;

        //溶解：六列按边缘→中心的秩次错拍滑塌
        private const int ColCount = 6;
        private const int ColRankGap = 16;
        private const float ColFallAccel = 0.42f;
        private const int DissolveTotal = 96;

        //==================== 几何 ====================

        /// <summary>墙体出水全高</summary>
        private const float FullHeight = 440f;
        /// <summary>水线下的绘制续段，藏住底部硬切</summary>
        private const float UnderBleed = 40f;
        /// <summary>压制推进速度：缓慢而不可阻挡</summary>
        private const float AdvanceSpeed = 1.45f;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int wofFrameTick;
        private int wofFrameIndex;
        private int mouthFrameTick;
        private int mouthFrameIndex;
        private int eyeFrameTick;
        private int eyeFrameIndex;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool facingInit;
        private int facing = 1;
        private bool breachDone;
        private bool awakenDone;
        private int lastLaserFired = -1;
        private int lastLeechSpit = -1;
        private readonly bool[] colSplashed = new bool[ColCount];
        private int colSoundBudget;
        private readonly float[] eyeLookRot = new float[2];
        /// <summary>墙宽运行时缓存（取自 Wof 贴图），无贴图端用回退值</summary>
        private float wallHalfW = 84f;
        private float lakeYCache;

        //血系配色随观看域鬼雨异化冷化，与湖系同族；血肉墙点缀偏腐肉暖
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(150, 36, 32), new(88, 108, 114));
        private static Color FleshGlow => KikasaDomain.CoolTint(new(248, 128, 104), new(178, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面。墙面朝背离主人一侧升起</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);
            float dir = MathF.Sign(emergeAt.X - owner.Center.X);
            if (dir == 0f) {
                dir = owner.direction;
            }
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                new Vector2(emergeAt.X, emergeAt.Y + 30f), Vector2.Zero,
                ModContent.ProjectileType<KikasaWallOfFleshServant>(), damage, 8f, owner.whoAmI,
                ai2: dir);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //墙体远超 hitbox，中心出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1600;
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
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

        /// <summary>墙的威胁就是墙本身：觉醒落定后接触判定常开，出水前与溶解中关死</summary>
        public override bool? CanDamage() => State switch {
            StateEmerge => StateTimer >= AwakenFrame ? null : false,
            StateAdvance or StateEyeLaser or StateLeechSpit => null,
            _ => false,
        };

        /// <summary>接触判定覆盖整面出水墙体（矩形），不含未出水的基座</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float exposed = ExposedHeight();
            if (exposed < FullHeight * 0.4f) {
                return false;
            }
            Rectangle wall = new(
                (int)(Projectile.Center.X - wallHalfW),
                (int)(lakeYCache - exposed),
                (int)(wallHalfW * 2f),
                (int)(exposed + 20f));
            return wall.Intersects(targetHitbox);
        }

        public override bool? CanCutTiles() => false;

        /// <summary>压着走：接触击退恒朝推进方向，把敌人往一侧赶</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = facing;

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //还没破水就要收场：什么都没露出来，不演倒塌
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
            lakeYCache = domain.LakeWorldY;

            //生命线：湖塌/收域/主人死亡 → 倒塌回湖。只有 owner 裁决——
            //服务器无领域状态（恒 Closed 是既定契约），别处判会当场误杀；其余端只跟包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            //接触伤害随召唤加成逐帧刷新，命中在 owner 端结算
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);

            //初始面向：spawn 包里 ai[2] 带来的 ±1；此后由速度规则演化，各端一致
            if (!facingInit) {
                facingInit = true;
                facing = MathF.Sign(Projectile.ai[2]) >= 0 ? 1 : -1;
                //视线初值对齐面向正前方，免得睁眼瞬间从背面甩视线
                eyeLookRot[0] = eyeLookRot[1] = facing > 0 ? 0f : MathHelper.Pi;
            }

            //换场清闩：远端可能靠收包切状态而非本地同拍转场
            if (State != lastSeenState) {
                lastSeenState = State;
                lastLaserFired = -1;
                lastLeechSpit = -1;
                if (State == StateDissolve) {
                    Array.Clear(colSplashed, 0, ColCount);
                    colSoundBudget = 3;
                }
            }

            //基座锚定：墙底永远浸在湖下，纵位由出水进度直接决定（各端同式）
            Projectile.velocity.Y = 0f;
            float exposed = ExposedHeight();
            Projectile.Center = new Vector2(Projectile.Center.X, lakeYCache - exposed * 0.5f);

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(domain); break;
                case StateAdvance: UpdateAdvance(owner, domain, authority); break;
                case StateEyeLaser: UpdateEyeLaser(owner, authority); break;
                case StateLeechSpit: UpdateLeechSpit(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateFrames();
            UpdateAmbient(domain);
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            //沿墙补光：顶、眼层、水线
            if (exposed > 30f) {
                float top = lakeYCache - exposed;
                for (int i = 0; i < 3; i++) {
                    Lighting.AddLight(new Vector2(Projectile.Center.X, top + exposed * (0.2f + 0.3f * i)),
                        0.34f, 0.09f, 0.08f);
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水演出 ====================

        private void UpdateEmerge(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            Projectile.velocity.X = 0f;

            if (t < OmenFrames) {
                //预兆：一整排涟漪自中心向两侧横着排开——宽度预告，读出"墙"而非"怪"
                if (viewed) {
                    if (t % 4 == 1) {
                        float spread = t / (float)OmenFrames;
                        float dx = spread * wallHalfW;
                        KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X + dx, lakeY),
                            0.35f + spread * 0.4f);
                        KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X - dx, lakeY),
                            0.35f + spread * 0.4f);
                    }
                    //临破水前整排水面拱起的碎涌
                    if (t > OmenFrames - 10 && t % 3 == 0) {
                        KikasaDomainDeco.SplashAt(new Vector2(
                            Projectile.Center.X + (t % 6 - 2.5f) * wallHalfW * 0.3f, lakeY), 3);
                    }
                }
                if (t == 8 || t == 24) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.45f,
                        Pitch = t == 8 ? -0.5f : -0.2f,
                        MaxInstances = 2
                    }, new Vector2(Projectile.Center.X, lakeY));
                }
                if (t == 30) {
                    //水下闷吼：墙在湖底醒了
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.2f, Pitch = -0.9f, MaxInstances = 2 },
                        new Vector2(Projectile.Center.X, lakeY));
                }
                return;
            }

            if (!breachDone) {
                //破水拍：整排浪冠一次起爆，量感读"墙顶破面"
                breachDone = true;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.6f, Pitch = -0.75f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBurstWide(lakeY);
                }
            }

            //推升期：水帘从墙面倾泻回湖
            if (viewed && t < RiseEnd) {
                float exposed = ExposedHeight();
                float top = lakeY - exposed;
                //墙面淌下的血水帘，帧内限量
                for (int i = 0; i < 3; i++) {
                    float x = Projectile.Center.X + Main.rand.NextFloat(-wallHalfW, wallHalfW);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        new Vector2(x, top + Main.rand.NextFloat(0f, exposed * 0.7f)),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(2.6f, 4.4f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.45f, 0.7f))
                        ?.Configure(Main.rand.Next(16, 30), 0f);
                }
                //基座持续搅水
                if (t % 4 == 2) {
                    KikasaDomainDeco.RippleAt(new Vector2(
                        Projectile.Center.X + Main.rand.NextFloat(-wallHalfW, wallHalfW), lakeY), 0.45f);
                }
                if (t % 9 == 4) {
                    KikasaDomainDeco.SplashAt(new Vector2(
                        Projectile.Center.X + Main.rand.NextFloat(-wallHalfW * 0.8f, wallHalfW * 0.8f), lakeY), 4);
                }
            }
            if (t == RiseEnd / 2 + OmenFrames / 2) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.6f, MaxInstances = 2 },
                    new Vector2(Projectile.Center.X, lakeY));
            }

            if (!awakenDone && t >= AwakenFrame) {
                //觉醒拍：两眼一口同时亮起，墙有了视线
                awakenDone = true;
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.55f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.45f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeYCache), 0.8f);
                    ShakeViewer(2.4f);
                }
            }

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），各端同拍；owner 盖章纠偏
                State = StateAdvance;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>整排破水浪冠：沿墙宽多点起爆，宽度即量感</summary>
        private void BreachBurstWide(float lakeY) {
            for (int k = -2; k <= 2; k++) {
                Vector2 hit = new(Projectile.Center.X + k * wallHalfW * 0.45f, lakeY);
                KikasaDomainDeco.RippleAt(hit, k == 0 ? 2.2f : 1.2f);
                KikasaDomainDeco.SplashAt(hit, k == 0 ? 12 : 8);
            }
            //浪冠血珠沿整排上抛
            for (int i = 0; i < 26; i++) {
                float x = Projectile.Center.X + Main.rand.NextFloat(-wallHalfW, wallHalfW);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    new Vector2(x, lakeY - 4f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(4.5f, 10.5f)),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(26, 44));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-wallHalfW, wallHalfW), lakeY - 10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 0.8f)),
                    MistBlood * 0.85f, Main.rand.NextFloat(0.8f, 1.1f))
                    ?.Configure(Main.rand.Next(70, 110));
            }
            PRTLoader.NewParticle<PRT_DWave>(new Vector2(Projectile.Center.X, lakeY - 6f), Vector2.Zero,
                BloodDeep, 0.1f)?.Configure(new Vector2(0.45f, 1f), -MathHelper.PiOver2, 0.42f, 12);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 1 }, Projectile.Center);
            ShakeViewer(6f);
        }

        //==================== 压制推进 ====================

        /// <summary>恒缓速水平漂移：这面墙不追不冲，压迫感全靠"一直在来"</summary>
        private void AdvanceDrift(Player owner, float speedMul) {
            int target = FindTarget(owner);
            float wantVx;
            if (target >= 0) {
                float dx = Main.npc[target].Center.X - Projectile.Center.X;
                wantVx = MathF.Abs(dx) < 30f
                    ? MathF.Sign(dx) * 0.4f
                    : MathF.Sign(dx) * AdvanceSpeed;
            }
            else {
                float dxOwner = owner.Center.X - Projectile.Center.X;
                //无猎物：缓缓退回主人近旁驻位
                wantVx = MathF.Abs(dxOwner) > 320f ? MathF.Sign(dxOwner) * 2.0f : 0f;
            }
            //离主人太远开赶路挡；再远直接贴回
            float far = owner.Center.X - Projectile.Center.X;
            if (MathF.Abs(far) > 1900f) {
                wantVx = MathF.Sign(far) * 7f;
            }
            //大质量惯性：改向缓慢，读出不可阻挡
            Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, wantVx * speedMul, 0.025f);

            //面向随实际移动演化（速度已同步，各端收敛一致）
            if (MathF.Abs(Projectile.velocity.X) > 0.12f) {
                facing = Projectile.velocity.X > 0f ? 1 : -1;
            }
        }

        private void UpdateAdvance(Player owner, KikasaDomainPlayer domain, bool authority) {
            //跟丢硬贴回：别让墙在半个地图外淌血
            if (MathF.Abs(owner.Center.X - Projectile.Center.X) > 2600f) {
                if (ViewedOwner) {
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, lakeYCache), 8);
                }
                Projectile.Center = new Vector2(owner.Center.X - owner.direction * 360f, Projectile.Center.Y);
                Projectile.velocity.X = 0f;
                Projectile.netUpdate = authority;
                if (ViewedOwner) {
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, lakeYCache), 10);
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeYCache), 1.6f);
                }
                return;
            }

            AdvanceDrift(owner, 1f);

            //出手裁决：激光剪切与吐蛭交替；转场规则各端一致，owner 盖章
            int target = FindTarget(owner);
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 45) {
                attackIndex++;
                State = attackIndex % 2 == 1 ? StateEyeLaser : StateLeechSpit;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 双眼细激光剪切 ====================

        private void UpdateEyeLaser(Player owner, bool authority) {
            int t = (int)StateTimer;
            //剪切期墙照旧压来——攻击不打断推进，这是它的性格
            AdvanceDrift(owner, 0.7f);

            int target = FindTarget(owner);
            if (target < 0 && t <= LaserLockFrames) {
                EndAttack(authority, 45);
                return;
            }

            if (t == 2) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 },
                    EyeWorldPos(0));
            }

            //蓄光：两眼各自积攒收拢的血光，72% 后静默——剪切前的吸气
            if (!Main.dedServ && t < LaserLockFrames * 0.72f && t % 2 == 0) {
                for (int e = 0; e < 2; e++) {
                    Vector2 eye = EyeWorldPos(e);
                    Vector2 from = eye + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 86f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        (eye - from) * 0.17f,
                        FleshGlow * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(7, 0f);
                }
            }

            //上眼先出横切，下眼隔拍反扫出斜切——两道细激光在猎物身上交叉
            if (t >= LaserLockFrames && lastLaserFired < 0) {
                lastLaserFired = 0;
                StateParam = 1;
                FireLaser(owner, eyeIndex: 0, sweepSign: 1f, authority);
            }
            if (t >= LaserLockFrames + LaserSecondDelay && lastLaserFired < 1) {
                lastLaserFired = 1;
                StateParam = 2;
                FireLaser(owner, eyeIndex: 1, sweepSign: -1f, authority);
            }

            if (t >= LaserLockFrames + LaserSecondDelay + KikasaWallOfFleshEyeLaser.TotalLife + 6) {
                EndAttack(authority, 130);
            }
        }

        private void FireLaser(Player owner, int eyeIndex, float sweepSign, bool authority) {
            Vector2 eye = EyeWorldPos(eyeIndex);

            //出手拍演出：各端由闩触发一次
            SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.5f, Pitch = 0.25f, MaxInstances = 3 }, eye);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.3f, Pitch = -0.35f, MaxInstances = 3 }, eye);
            if (!Main.dedServ) {
                PRTLoader.NewParticle<PRT_DWave>(eye, Vector2.Zero, FleshGlow, 0.06f)
                    ?.Configure(new Vector2(0.6f, 1f), 0f, 0.2f, 7);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(eye + Main.rand.NextVector2Circular(4f, 4f),
                        Main.rand.NextVector2Circular(2.2f, 2.2f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18));
                }
            }
            if (ViewedOwner) {
                ShakeViewer(1.6f);
            }
            //一丝后坐：墙也知道自己开了刃
            Projectile.velocity.X -= facing * 0.8f;

            //激光只在 owner 端生成，扫掠参数全在 spawn args 里（2.7：包随 NewProjectile 即走）
            if (authority) {
                int target = FindTarget(owner);
                Vector2 aimPos = target >= 0
                    ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                    : eye + new Vector2(facing * 520f, 0f);
                float aim = (aimPos - eye).ToRotation();
                float startAngle = aim - KikasaWallOfFleshEyeLaser.HalfArc * sweepSign;
                float sweepSpeed = 2f * KikasaWallOfFleshEyeLaser.HalfArc
                    / KikasaWallOfFleshEyeLaser.SweepFrames * sweepSign;
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RayDamage);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), eye, Vector2.Zero,
                    ModContent.ProjectileType<KikasaWallOfFleshEyeLaser>(), damage, 2f,
                    Projectile.owner, startAngle, sweepSpeed, eyeIndex);
            }
        }

        //==================== 口吐游空水蛭 ====================

        private void UpdateLeechSpit(Player owner, bool authority) {
            int t = (int)StateTimer;
            AdvanceDrift(owner, 0.7f);

            int target = FindTarget(owner);
            if (target < 0 && t <= ChewFrames) {
                EndAttack(authority, 45);
                return;
            }

            if (t == 4) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = -0.7f, MaxInstances = 2 },
                    MouthWorldPos());
            }

            //咀嚼蓄势：血珠向口部汇聚，72% 后静默
            if (!Main.dedServ && t < ChewFrames * 0.72f && t % 2 == 1) {
                Vector2 mouth = MouthWorldPos();
                Vector2 from = mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(36f, 80f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                    (mouth - from) * 0.15f,
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(8, 0f);
            }

            //三口错拍吐出
            int spitIndex = (t - ChewFrames) / SpitGap;
            if (t >= ChewFrames && (t - ChewFrames) % SpitGap == 0
                && spitIndex < SpitCount && lastLeechSpit < spitIndex) {
                lastLeechSpit = spitIndex;
                StateParam = spitIndex + 1;
                SpitLeech(owner, spitIndex, authority);
            }

            if (t >= LeechStateEnd) {
                EndAttack(authority, 150);
            }
        }

        private void SpitLeech(Player owner, int spitIndex, bool authority) {
            Vector2 mouth = MouthWorldPos();

            //吐出拍：湿噗 + 口部锥形喷溅，各端由闩触发一次
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.55f, Pitch = -0.2f + spitIndex * 0.08f, MaxInstances = 3 }, mouth);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.3f, Pitch = -0.5f, MaxInstances = 3 }, mouth);
            if (!Main.dedServ) {
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(mouth + Main.rand.NextVector2Circular(4f, 4f),
                        new Vector2(facing, 0f).RotatedByRandom(0.5f) * Main.rand.NextFloat(2.5f, 6.5f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(12, 22));
                }
                PRTLoader.NewParticle<PRT_DWave>(mouth + new Vector2(facing * 10f, 0f), Vector2.Zero,
                    BloodDeep, 0.07f)?.Configure(new Vector2(0.55f, 1f), facing > 0 ? 0f : MathHelper.Pi, 0.2f, 8);
            }
            if (ViewedOwner) {
                ShakeViewer(0.8f);
            }
            //吐一口退半步
            Projectile.velocity.X -= facing * 1.1f;

            //水蛭只在 owner 端生成，初速带满参数（自寻的，无 spawn 后补写）
            if (authority) {
                int target = FindTarget(owner);
                Vector2 aimPos = target >= 0
                    ? Main.npc[target].Center + Main.npc[target].velocity * 8f
                    : mouth + new Vector2(facing * 400f, -60f);
                Vector2 aim = (aimPos - mouth).SafeNormalize(new Vector2(facing, 0f));
                Vector2 vel = aim.RotatedBy((spitIndex - 1) * 0.17f + Main.rand.NextFloat(-0.06f, 0.06f)) * 8.5f;
                vel.Y -= 0.8f;
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RayDamage);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), mouth, vel,
                    ModContent.ProjectileType<KikasaWallOfFleshLeech>(), damage, 2f, Projectile.owner);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateAdvance;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解：逐列塌回湖 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            Projectile.velocity.X *= 0.9f;

            //各列过水线拍：晚到的列音量递减，帧内限声
            float exposed = ExposedHeight();
            for (int c = 0; c < ColCount; c++) {
                if (colSplashed[c] || ColDrop(c) < exposed) {
                    continue;
                }
                colSplashed[c] = true;
                float colX = Projectile.Center.X - wallHalfW + wallHalfW * 2f * (c + 0.5f) / ColCount;
                if (lakeAlive && ViewedOwner) {
                    Vector2 hit = new(colX, lakeYCache);
                    KikasaDomainDeco.SplashAt(hit, 8);
                    KikasaDomainDeco.RippleAt(hit, 1.2f);
                }
                if (colSoundBudget > 0) {
                    //先取音量再扣预算：首列最响，随后递减
                    float vol = 0.24f + colSoundBudget * 0.12f;
                    colSoundBudget--;
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = vol,
                        Pitch = -0.4f + c * 0.05f,
                        MaxInstances = 3
                    }, new Vector2(colX, lakeYCache));
                    if (ViewedOwner) {
                        ShakeViewer(1.2f);
                    }
                }
            }

            //列体滑落途中的崩解残珠
            if (!Main.dedServ && t % 2 == 0) {
                int c = Main.rand.Next(ColCount);
                float drop = ColDrop(c);
                if (drop > 4f && drop < exposed) {
                    float colX = Projectile.Center.X - wallHalfW + wallHalfW * 2f * (c + 0.5f) / ColCount;
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        new Vector2(colX + Main.rand.NextFloat(-10f, 10f),
                            lakeYCache - exposed + drop + Main.rand.NextFloat(0f, exposed * 0.5f)),
                        new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), Main.rand.NextFloat(1.4f, 3f)),
                        BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(14, 24));
                }
            }
            //首拍裂响：墙体开始垮
            if (t == 2) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= DissolveTotal) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveTotal + 10) {
                Projectile.Kill();
            }
        }

        /// <summary>列滑落深度：边缘列先垮、中心列殿后，加速度下滑——建筑倒塌的秩序</summary>
        private float ColDrop(int col) {
            if (State != StateDissolve) {
                return 0f;
            }
            int rank = Math.Min(col, ColCount - 1 - col);
            float t = StateTimer - rank * ColRankGap;
            if (t <= 0f) {
                return 0f;
            }
            return 0.5f * ColFallAccel * t * t;
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

        /// <summary>出水高度：破水后指数衰减推升——首拍猛涌，随后缓慢而不可阻挡地顶满。
        /// 落定后带轻微呼吸浮沉（水线处读出活着的起伏）；溶解期定高，列滑落自己演</summary>
        private float ExposedHeight() {
            int t = (int)StateTimer;
            if (State == StateEmerge) {
                if (t < OmenFrames) {
                    return 0f;
                }
                float k = 1f - MathF.Pow(0.945f, t - OmenFrames);
                return FullHeight * MathHelper.Clamp(k * 1.02f, 0f, 1f);
            }
            if (State == StateDissolve) {
                return FullHeight;
            }
            return FullHeight + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 4f;
        }

        /// <summary>眼位：0=上眼 0.26 高度，1=下眼 0.60 高度，嵌在墙面朝向侧</summary>
        internal Vector2 EyeWorldPos(int eyeIndex) {
            float exposed = ExposedHeight();
            float top = lakeYCache - exposed;
            float frac = eyeIndex == 0 ? 0.26f : 0.60f;
            return new Vector2(Projectile.Center.X + facing * (wallHalfW - 18f), top + exposed * frac);
        }

        private Vector2 MouthWorldPos() {
            float exposed = ExposedHeight();
            return new Vector2(Projectile.Center.X + facing * (wallHalfW - 8f),
                lakeYCache - exposed + exposed * 0.43f);
        }

        /// <summary>跟随/推进期的常驻湖面互动：船首波在前缘，尾迹在后缘</summary>
        private void UpdateAmbient(KikasaDomainPlayer domain) {
            if (!ViewedOwner || State == StateEmerge || State == StateDissolve) {
                return;
            }
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            float speed = MathF.Abs(Projectile.velocity.X);
            //前缘船首波：推进越快圈越大
            if (t % 7 == 1) {
                KikasaDomainDeco.RippleAt(new Vector2(
                    Projectile.Center.X + facing * wallHalfW, lakeY), 0.35f + speed * 0.16f);
            }
            //后缘尾迹
            if (t % 13 == 5) {
                KikasaDomainDeco.RippleAt(new Vector2(
                    Projectile.Center.X - facing * wallHalfW * 0.9f, lakeY), 0.25f);
            }
            //前缘偶发挤水碎星
            if (speed > 0.8f && t % 11 == 3) {
                KikasaDomainDeco.FootSplash(new Vector2(
                    Projectile.Center.X + facing * wallHalfW, lakeY), 0.8f, Projectile.velocity.X);
            }
            //墙面凝珠滴落
            if (!Main.dedServ && Main.rand.NextBool(18)) {
                float exposed = ExposedHeight();
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-wallHalfW, wallHalfW),
                        lakeY - exposed + Main.rand.NextFloat(0f, exposed * 0.8f)),
                    new Vector2(0f, Main.rand.NextFloat(0.6f, 1.4f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.55f),
                    Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(20, 34), 0f);
            }
        }

        private void UpdateFrames() {
            //墙体平铺帧：原版 6tick 一格、3 帧循环
            if (++wofFrameTick >= 6) {
                wofFrameTick = 0;
                wofFrameIndex = (wofFrameIndex + 1) % 3;
            }
            //口：吐蛭蓄势期咀嚼加速
            bool chewing = State == StateLeechSpit && StateTimer < ChewFrames + SpitGap * SpitCount;
            if (++mouthFrameTick >= (chewing ? 5 : 12)) {
                mouthFrameTick = 0;
                mouthFrameIndex = (mouthFrameIndex + 1) % 2;
            }
            //眼：激光期锁睁眼帧（原版锁帧 0 语义），其余眨动
            bool glaring = State == StateEyeLaser
                || State == StateEmerge && StateTimer >= AwakenFrame;
            if (glaring) {
                eyeFrameIndex = 0;
                eyeFrameTick = 0;
            }
            else if (++eyeFrameTick >= 12) {
                eyeFrameTick = 0;
                eyeFrameIndex = (eyeFrameIndex + 1) % 2;
            }

            //眼球视线：盯猎物，闲时看主人；限制在面向前半球
            Player owner = Owner;
            if (owner?.active == true) {
                int target = FindTarget(owner);
                Vector2 look = target >= 0 ? Main.npc[target].Center : owner.Center;
                for (int e = 0; e < 2; e++) {
                    float want = (look - EyeWorldPos(e)).ToRotation();
                    //背面目标视线归正：眼睛长在墙面上，转不过去
                    if (MathF.Cos(want) * facing < -0.1f) {
                        want = facing > 0 ? 0f : MathHelper.Pi;
                    }
                    eyeLookRot[e] = eyeLookRot[e].AngleLerp(want, 0.15f);
                }
            }
        }

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        /// <summary>uForm：1=全血水 0=真身；升起期从全血水凝向半沉稳态</summary>
        private float BaseForm() {
            float steady = 0.32f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + Seed) * 0.05f;
            if (State == StateEmerge) {
                int t = (int)StateTimer;
                if (t < OmenFrames) {
                    return 1f;
                }
                float p = MathHelper.Clamp((t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f);
                return MathHelper.Lerp(1f, steady, p * p * (3f - 2f * p));
            }
            return steady;
        }

        /// <summary>uScanMode：推升期自上而下扫描凝实，落定窗内退光</summary>
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

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();
            if (domain == null) {
                return false;
            }
            //墙宽尽早从贴图刷新：预兆涟漪的排宽要与真实墙宽一致
            Texture2D wofEarly = TextureAssets.Wof?.Value;
            if (wofEarly != null) {
                wallHalfW = wofEarly.Width * 0.5f;
            }
            float lakeY = domain.LakeWorldY;
            float exposed = ExposedHeight();
            SpriteBatch sb = Main.spriteBatch;

            //预兆期的水下血光横条
            DrawOmenGlow(sb, lakeY);

            if (exposed > 2f) {
                DrawWallBody(sb, lightColor, lakeY, exposed);
                DrawGlowAccents(sb, lakeY, exposed);
            }
            return false;
        }

        /// <summary>预兆：水下一条横向血光自深处浮起、随宽度预告一起变宽</summary>
        private void DrawOmenGlow(SpriteBatch sb, float lakeY) {
            if (State != StateEmerge || StateTimer >= OmenFrames + 6) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float ot = MathHelper.Clamp(StateTimer / (float)OmenFrames, 0f, 1f);
            float ease = 1f - (1f - ot) * (1f - ot);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 pos = new(Projectile.Center.X, lakeY + MathHelper.Lerp(46f, 10f, ease));
            float w = wallHalfW * 2.4f * (0.3f + 0.7f * ease);
            sb.Draw(glow, pos - Main.screenPosition, null, FleshGlow * (0.45f * ease), 0f,
                glow.Size() * 0.5f, new Vector2(w / glow.Width, 26f / glow.Height), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>墙体：Wof 平铺条带 + 眼/口贴图，经血湖材质着色器上屏；溶解期按列拆绘滑落</summary>
        private void DrawWallBody(SpriteBatch sb, Color lightColor, float lakeY, float exposed) {
            Main.instance.LoadNPC(NPCID.WallofFlesh);
            Main.instance.LoadNPC(NPCID.WallofFleshEye);
            Texture2D wof = TextureAssets.Wof?.Value;
            Texture2D mouthTex = TextureAssets.Npc[NPCID.WallofFlesh]?.Value;
            Texture2D eyeTex = TextureAssets.Npc[NPCID.WallofFleshEye]?.Value;
            if (wof == null) {
                return;
            }
            wallHalfW = wof.Width * 0.5f;
            int frameH = wof.Height / 3;
            //异常贴图护栏：条带高度过小既没意义又会拉爆着色器切换次数
            if (frameH < 16) {
                return;
            }
            int srcFrameY = wofFrameIndex * frameH;

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
            }

            //原版墙面朝左；面朝右时翻转，列源序同步镜像保证横向连续
            SpriteEffects fx = facing > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float wallLeft = Projectile.Center.X - wallHalfW;
            float wallTop = lakeY - exposed;
            float bottom = lakeY + UnderBleed;
            float baseForm = BaseForm();

            int cols = State == StateDissolve ? ColCount : 1;
            int colW = wof.Width / cols;

            for (int c = 0; c < cols; c++) {
                float drop = cols == 1 ? 0f : ColDrop(c);
                if (drop >= exposed + UnderBleed + 20f) {
                    continue;
                }
                float colT = cols == 1 ? 0f : MathHelper.Clamp(drop / (exposed + 40f), 0f, 1f);
                int srcCol = facing > 0 ? cols - 1 - c : c;
                int srcX = srcCol * colW;
                int thisW = srcCol == cols - 1 ? wof.Width - srcX : colW;

                for (float y = wallTop + drop; y < bottom + drop; y += frameH) {
                    float sliceH = MathF.Min(frameH, bottom + drop - y);
                    if (sliceH < 1f) {
                        continue;
                    }
                    Rectangle src = new(srcX, srcFrameY, thisW, (int)sliceH);
                    Vector2 pos = new(wallLeft + c * colW, y);

                    Color color;
                    if (shaderOk) {
                        //越贴水线越水化：底部条带浸润感更重
                        float wetBoost = MathHelper.Clamp((y + sliceH * 0.5f - (lakeY - 100f)) / 100f, 0f, 1f) * 0.16f;
                        form.Parameters["uSeed"]?.SetValue(Seed + c * 1.3f + y * 0.002f);
                        form.Parameters["uForm"]?.SetValue(MathHelper.Clamp(baseForm + wetBoost, 0f, 1f));
                        form.Parameters["uDissolve"]?.SetValue(colT * 0.85f);
                        form.Parameters["uUvRect"]?.SetValue(new Vector4(
                            src.X / (float)wof.Width, src.Y / (float)wof.Height,
                            src.Width / (float)wof.Width, src.Height / (float)wof.Height));
                        form.Parameters["uTexel"]?.SetValue(new Vector2(1f / wof.Width, 1f / wof.Height));
                        form.Parameters["uAspect"]?.SetValue(src.Width / (float)src.Height);
                        form.CurrentTechnique.Passes[0].Apply();
                        color = Color.White;
                    }
                    else {
                        //无着色器回退：CPU 血染
                        color = Color.Lerp(lightColor, BloodMain, 0.55f) * (1f - colT);
                    }
                    sb.Draw(wof, pos - Main.screenPosition, src, color, 0f,
                        Vector2.Zero, 1f, fx, 0f);
                }
            }

            //面部件压在墙面上层：口一件、眼两件，各随所在列滑落
            if (mouthTex != null) {
                //口不转头，视线角取面向正前方（世界角语义，面朝左即 π）
                DrawFacePart(sb, mouthTex, NPCID.WallofFlesh, mouthFrameIndex,
                    MouthWorldPos(), facing > 0 ? 0f : MathHelper.Pi, lightColor, exposed);
            }
            if (eyeTex != null) {
                for (int e = 0; e < 2; e++) {
                    DrawFacePart(sb, eyeTex, NPCID.WallofFleshEye, eyeFrameIndex,
                        EyeWorldPos(e), eyeLookRot[e], lightColor, exposed);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>眼/口贴图：原版脸朝左约定——面朝右翻转、旋转取世界视线角</summary>
        private void DrawFacePart(SpriteBatch sb, Texture2D tex, int npcType, int frameIdx,
            Vector2 worldPos, float lookRot, Color lightColor, float exposed) {
            //溶解期部件跟自己那列一起滑
            float drop = 0f;
            float colT = 0f;
            if (State == StateDissolve) {
                float local = worldPos.X - (Projectile.Center.X - wallHalfW);
                int col = (int)MathHelper.Clamp(local / (wallHalfW * 2f) * ColCount, 0, ColCount - 1);
                drop = ColDrop(col);
                colT = MathHelper.Clamp(drop / (exposed + 40f), 0f, 1f);
                if (drop >= exposed + UnderBleed + 20f) {
                    return;
                }
            }
            int frameH = tex.Height / Main.npcFrameCount[npcType];
            Rectangle frame = new(0, frameH * frameIdx, tex.Width, frameH);

            SpriteEffects fx = facing > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float rot = facing > 0 ? lookRot : lookRot + MathHelper.Pi;

            Effect form = EffectLoader.KikasaItemForm?.Value;
            Color color;
            if (form != null && CWRAsset.PerlinNoise?.Value != null) {
                form.Parameters["uSeed"]?.SetValue(Seed + worldPos.Y * 0.01f);
                form.Parameters["uForm"]?.SetValue(BaseForm() * 0.9f);
                form.Parameters["uDissolve"]?.SetValue(colT * 0.85f);
                form.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                    frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                form.CurrentTechnique.Passes[0].Apply();
                color = Color.White;
            }
            else {
                color = Color.Lerp(lightColor, BloodMain, 0.55f) * (1f - colT);
            }
            sb.Draw(tex, worldPos + new Vector2(0f, drop) - Main.screenPosition, frame, color,
                rot, frame.Size() * 0.5f, 1f, fx, 0f);
        }

        /// <summary>加色层：眼瞳常燃余光 / 激光蓄光 / 觉醒闪 / 吐蛭口部积血</summary>
        private void DrawGlowAccents(SpriteBatch sb, float lakeY, float exposed) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || State == StateDissolve) {
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

            //眼瞳余光：常燃低亮，随呼吸相位微搏
            if (State != StateEmerge || t >= AwakenFrame) {
                EnsureBegin();
                for (int e = 0; e < 2; e++) {
                    float pulse = 0.2f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + Seed + e * 2.2f);
                    sb.Draw(glow, EyeWorldPos(e) - Main.screenPosition, null, FleshGlow * pulse, 0f,
                        gOrigin, new Vector2(26f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //觉醒拍：两眼一闪
            if (State == StateEmerge && t >= AwakenFrame) {
                float f = MathHelper.Clamp((t - AwakenFrame) / (float)(EmergeTotal - AwakenFrame), 0f, 1f);
                float a = MathF.Sin(f * MathHelper.Pi) * 0.7f;
                if (a > 0.02f) {
                    EnsureBegin();
                    for (int e = 0; e < 2; e++) {
                        sb.Draw(glow, EyeWorldPos(e) - Main.screenPosition, null, FleshGlow * a, 0f,
                            gOrigin, new Vector2((30f + 22f * f) * 2f / glow.Width), SpriteEffects.None, 0f);
                    }
                }
            }

            //激光蓄光：锁定期两眼积亮；已开火的眼交给激光弹幕自己的口辉
            if (State == StateEyeLaser && t <= LaserLockFrames + LaserSecondDelay) {
                float charge = MathHelper.Clamp(t / (float)LaserLockFrames, 0f, 1f);
                EnsureBegin();
                for (int e = 0; e < 2; e++) {
                    bool fired = (int)StateParam > e;
                    if (fired) {
                        continue;
                    }
                    float r = 8f + 20f * charge;
                    sb.Draw(glow, EyeWorldPos(e) - Main.screenPosition, null,
                        FleshGlow * (0.55f * charge), 0f,
                        gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //吐蛭蓄势：口部积血渐亮
            if (State == StateLeechSpit && t <= ChewFrames + SpitGap * SpitCount) {
                float charge = MathHelper.Clamp(t / (float)ChewFrames, 0f, 1f);
                EnsureBegin();
                sb.Draw(glow, MouthWorldPos() - Main.screenPosition, null,
                    BloodMain with { A = 0 } * (0.45f * charge), 0f,
                    gOrigin, new Vector2((10f + 14f * charge) * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //墙面碾压的溅血（OnHit 只在 owner 端跑）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    new Vector2(facing * Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(-2.2f, 1f)),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 26), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = -0.45f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠沿墙基一线散开，异常移除也留一口血水
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-wallHalfW, wallHalfW),
                        lakeYCache - Main.rand.NextFloat(0f, 60f)),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.6f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                new Vector2(Projectile.Center.X, lakeYCache - 30f),
                new Vector2(0f, -0.2f), MistBlood * 0.7f, Main.rand.NextFloat(0.8f, 1.1f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
