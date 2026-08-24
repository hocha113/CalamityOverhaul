using CalamityOverhaul.Common;
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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦恶犬：左键自梦里唤出的猎手。原版狼贴图 + <c>KikasaHound.fx</c> 实体模式
    /// （体成而实、双目常燃）。自玩家脚下黑水跃出，落地追猎最近的敌人：
    /// 中距收步点火冲刺，近身伏地蓄势后一口扑咬；蹬地够得到的高度就跳，
    /// 再高则斜向上冲，落地滑停回追。高速段拖出噪蚀狼影与黑烟，寿命尽头化雾散回梦里。
    /// 梦境绑定，离开 Dreaming 即溶解。
    /// 各端同推确定性规则，弹幕仅 owner 端生成，伤害在 owner 端结算
    /// </summary>
    internal class KikasaDreamHound : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>在场寿命（帧），尽头化雾</summary>
        internal const int LifeFrames = 300;

        private const int DissolveFrames = 26;

        //==================== 状态 ====================

        private const int StateLeap = 0;
        private const int StateRun = 1;
        private const int StateLunge = 2;
        private const int StateDissolve = 3;
        private const int StateCrouch = 4;
        private const int StateSprint = 5;
        private const int StateSkid = 6;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>出手冷却（帧），Run 状态里递减，扑咬/冲刺共用</summary>
        private ref float LungeCooldown => ref Projectile.ai[2];

        //==================== 运动参数 ====================

        private const float Gravity = 0.32f;
        private const float MaxFall = 11f;
        private const float RunAccel = 0.30f;
        private const float RunMaxSpeed = 9.6f;
        private const float SprintSpeed = 17f;
        private const float PounceSpeed = 21.5f;
        /// <summary>索敌半径（像素）。约 100 格，犬以自身为圆心找最近可追目标</summary>
        private const float ChaseRange = 1600f;
        /// <summary>蹬地还能帮上忙的高度差。约 12 格；再高就改冲，别白跳</summary>
        private const float JumpReachDy = 192f;
        /// <summary>仍愿出手的对空高度。约 32 格；再高只追不扑，不硬够天花板</summary>
        private const float AirAttackDy = 512f;
        /// <summary>近身伏地扑咬允许的高度差。约 16 格</summary>
        private const float CrouchMaxDy = 260f;
        private const int CrouchFrames = 14;
        private const int SprintPrepFrames = 6;
        private const int SkidFrames = 8;

        //==================== 本地表现量 ====================

        private int frameIndex;
        private float frameCounter;
        private bool spawnFxDone;
        private float eyeGlowVis = 0.95f;

        private Player Owner => Main.player[Projectile.owner];

        private float Seed => Projectile.identity * 0.7391f;

        /// <summary>同窝六犬按 identity 错开触发距离与出手拍，不齐步走</summary>
        private float PackJitter => Projectile.identity % 5;

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = 66;
            Projectile.height = 38;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.timeLeft = LifeFrames;
        }

        /// <summary>
        /// 撕咬伤：鬼伞面板 × 魇系倍率。命中只在 owner 端结算，
        /// 远端不要读影位盘（储钱罐语义），保持面板基数
        /// </summary>
        internal static int ResolveBiteDamage(Player owner, bool applyNightmare) {
            float scale = applyNightmare
                ? KikasaServants.KikasaEffigyBoard.HoundDamageScale(owner) : 1f;
            return Math.Max(1, (int)(KikasaOverride.GetPanelDamage(owner) * scale));
        }

        public override bool MinionContactDamage() => true;

        /// <summary>化雾中没有牙</summary>
        public override bool? CanDamage() => State == StateDissolve ? false : null;

        public override bool? CanCutTiles() => false;

        /// <summary>撞墙不死：横向撞停走小跳逻辑，落地竖速归零</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = 0f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = 0f;
            }
            return false;
        }

        /// <summary>提前化雾（超编遣散/离开梦境）。owner 端受理并盖章</summary>
        internal void BeginDissolve() {
            if (State == StateDissolve) {
                return;
            }
            State = StateDissolve;
            StateTimer = 0f;
            //化雾要走完整个包络，别被寿命先掐掉
            if (Projectile.timeLeft < DissolveFrames + 4) {
                Projectile.timeLeft = DissolveFrames + 4;
            }
            if (Main.myPlayer == Projectile.owner) {
                Projectile.netUpdate = true;
            }
        }

        //==================== AI ====================

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            //面板伤逐帧刷新；魇倍率只有 owner 端读得到盘，远端保持面板基数
            Projectile.damage = ResolveBiteDamage(owner, Main.myPlayer == Projectile.owner);

            //梦境绑定：owner 端判定离梦即散，其余端跟同步包
            bool authority = Main.myPlayer == Projectile.owner;
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();
            if (authority && State != StateDissolve
                && (owner.dead || domain.Phase != KikasaDomainPhase.Dreaming)) {
                BeginDissolve();
            }

            //寿命进入化雾窗
            if (State != StateDissolve && Projectile.timeLeft <= DissolveFrames) {
                State = StateDissolve;
                StateTimer = 0f;
            }

            SpawnBurstFx();

            //接地性必须在施加重力前采样：原版碰撞在 AI 之后才把竖速归零，
            //先加重力再看 velocity.Y 会永远读到下坠，犬会卡在跃出态不索敌、帧停在坠落
            bool grounded = Projectile.velocity.Y == 0f;
            float vyIn = Projectile.velocity.Y;

            float gravity = Gravity;
            switch (State) {
                case StateLeap: gravity = UpdateLeap(grounded); break;
                case StateRun: gravity = UpdateRun(grounded); break;
                case StateLunge: gravity = UpdateLunge(grounded); break;
                case StateDissolve: gravity = UpdateDissolve(); break;
                case StateCrouch: gravity = UpdateCrouch(); break;
                case StateSprint: gravity = UpdateSprint(grounded); break;
                case StateSkid: gravity = UpdateSkid(); break;
            }
            ApplyGravity(gravity);

            //蓄势与滑停锁定朝向，别被反缩/惯性翻面
            if (MathF.Abs(Projectile.velocity.X) > 0.2f
                && State != StateCrouch && State != StateSkid) {
                Projectile.spriteDirection = Projectile.velocity.X > 0f ? 1 : -1;
            }
            UpdateTilt(grounded);
            UpdateFrame(grounded, vyIn);
        }

        /// <summary>状态切换，owner 端盖同步章</summary>
        private void EnterState(int state) {
            State = state;
            StateTimer = 0f;
            if (Main.myPlayer == Projectile.owner) {
                Projectile.netUpdate = true;
            }
        }

        //出场那一口黑水，各端首帧自播

        private void SpawnBurstFx() {
            if (spawnFxDone || Main.dedServ) {
                return;
            }
            spawnFxDone = true;
            SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.55f, Volume = 0.5f, MaxInstances = 3 }, Projectile.Center);
            KikasaHoundVoice.Wuff(Projectile.Center, 0.55f, -0.06f);
            for (int i = 0; i < 7; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-2.4f, -0.6f));
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 10f), vel,
                    new Color(30, 10, 13) * 0.9f, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(40, 70));
            }
            //破水拍甩出的黑烟
            for (int i = 0; i < 5; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2.2f, 2.2f), Main.rand.NextFloat(-3f, -0.8f));
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 8f), vel,
                    new Color(52, 18, 22) * 0.9f, Main.rand.NextFloat(0.26f, 0.44f))
                    ?.Configure(Main.rand.Next(20, 34), 0.015f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaDreamAsh>(
                    Projectile.Center, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, -0.8f)),
                    new Color(214, 84, 34), Main.rand.NextFloat(0.10f, 0.16f))
                    ?.Configure(Main.rand.Next(40, 80), true);
            }
        }

        //各状态返回本帧应施加的重力，统一在 AI 尾部结算

        private float UpdateLeap(bool grounded) {
            StateTimer++;
            //落地即入追猎
            if (StateTimer > 6f && grounded) {
                State = StateRun;
                StateTimer = 0f;
            }
            return Gravity;
        }

        private float UpdateRun(bool grounded) {
            StateTimer++;
            if (LungeCooldown > 0f) {
                LungeCooldown--;
            }

            NPC target = FindTarget();
            if (target == null) {
                //没有猎物：缓步收住，站定等
                Projectile.velocity.X *= 0.92f;
                return Gravity;
            }

            float dx = target.Center.X - Projectile.Center.X;
            float dy = target.Center.Y - Projectile.Center.Y;
            float absDx = MathF.Abs(dx);
            float absDy = MathF.Abs(dy);
            int dir = dx > 0f ? 1 : -1;

            //地面追击
            Projectile.velocity.X = MathHelper.Clamp(
                Projectile.velocity.X + RunAccel * dir, -RunMaxSpeed, RunMaxSpeed);

            //撞墙小跳；只有蹬地够得到的高度才跳，再高留给冲刺
            if (grounded) {
                bool blocked = MathF.Abs(Projectile.velocity.X) < 0.6f && absDx > 40f;
                bool jumpable = dy < -56f && dy > -JumpReachDy && absDx < 220f;
                if (blocked) {
                    Projectile.velocity.Y = -7.4f;
                }
                else if (jumpable) {
                    float t = MathHelper.Clamp((-dy - 56f) / (JumpReachDy - 56f), 0f, 1f);
                    Projectile.velocity.Y = MathHelper.Lerp(-8.4f, -12.2f, t);
                }
            }

            if (LungeCooldown <= 0f) {
                TryCommitHunt(target, grounded, absDx, absDy, dy);
            }
            return Gravity;
        }

        /// <summary>
        /// 出手选择：近地伏地扑、空中顺势咬、中距平冲；
        /// 猎物高于蹬地高度就收步斜向上冲，别在底下空跳。
        /// </summary>
        private void TryCommitHunt(NPC target, bool grounded, float absDx, float absDy, float dy) {
            float near = 190f + PackJitter * 6f;
            bool aboveJump = dy < -JumpReachDy && absDy <= AirAttackDy;

            if (grounded && aboveJump && StateTimer > 10f) {
                EnterState(StateSprint);
                return;
            }
            if (grounded && absDx < near && absDy < CrouchMaxDy) {
                EnterState(StateCrouch);
                Projectile.velocity.X *= 0.5f;
                return;
            }
            if (!grounded && absDx < 200f && absDy < 320f) {
                StartLunge(target, PounceSpeed * 0.82f, 40f);
                return;
            }
            if (grounded && StateTimer > 16f && absDy < AirAttackDy
                && absDx > 260f + PackJitter * 22f && absDx < 1180f) {
                EnterState(StateSprint);
            }
        }

        /// <summary>伏地蓄势,收步、猛向后一缩、末三帧死寂,黑雾向身体倒吸</summary>
        private float UpdateCrouch() {
            StateTimer++;
            NPC target = FindTarget();
            if (target == null) {
                EnterState(StateRun);
                return Gravity;
            }

            //低吠盖住整段蓄势，起扑不再叠第二声
            if (StateTimer == 1 && !Main.dedServ) {
                KikasaHoundVoice.Wuff(Projectile.Center, 0.52f, -0.12f);
            }

            //面朝猎物钉死
            int dir = target.Center.X > Projectile.Center.X ? 1 : -1;
            Projectile.spriteDirection = dir;

            if (StateTimer <= CrouchFrames - 7f) {
                //收步刹住
                Projectile.velocity.X *= 0.62f;
            }
            else if (StateTimer <= CrouchFrames - 3f) {
                //后攒,突然向后一缩,起跳前的深吸气
                Projectile.velocity.X = -dir * 3.2f;
            }
            else {
                //末三帧彻底定住，爆发前的静默
                Projectile.velocity.X *= 0.3f;
            }

            //黑雾向身体倒吸，给起跳一个看得见的因
            if (!Main.dedServ && StateTimer % 3 == 1) {
                Vector2 at = Projectile.Center + Main.rand.NextVector2CircularEdge(46f, 30f);
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(at,
                    (Projectile.Center - at) * 0.11f,
                    new Color(44, 16, 20) * 0.8f, Main.rand.NextFloat(0.2f, 0.3f))
                    ?.Configure(Main.rand.Next(14, 22), 0.004f);
            }

            if (StateTimer >= CrouchFrames + PackJitter * 0.8f) {
                StartLunge(target, PounceSpeed + PackJitter * 0.4f, 58f, growl: false);
            }
            return Gravity;
        }

        /// <summary>一帧点火起扑,带猎物速度预判直线咬向落点。
        /// <paramref name="growl"/>：空中顺势咬没有伏地，这里补低吠；伏地起扑则否</summary>
        private void StartLunge(NPC target, float speed, float cooldown, bool growl = true) {
            Vector2 lead = target.Center + target.velocity * 7f;
            Vector2 aim = (lead - Projectile.Center)
                .SafeNormalize(Vector2.UnitX * Projectile.spriteDirection);
            Projectile.velocity = aim * speed + new Vector2(0f, -1.4f);
            LungeCooldown = cooldown;
            EnterState(StateLunge);
            LaunchFx(aim, growl);
        }

        private float UpdateLunge(bool grounded) {
            StateTimer++;
            //前段还在咬紧加速,不转向,直线才读得出快
            if (StateTimer <= 14f) {
                Projectile.velocity *= 1.012f;
            }
            ShedFx(1f);

            if (grounded && StateTimer > 5f) {
                EnterState(StateSkid);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.5f, Volume = 0.24f, MaxInstances = 3 }, Projectile.Center);
                    BurstSmoke(5, -Projectile.spriteDirection);
                }
                return Gravity;
            }
            if (StateTimer > 44f) {
                EnterState(StateRun);
            }
            //扑击前段低重力咬直线，后段自然坠回
            return StateTimer <= 11f ? 0.05f : Gravity;
        }

        /// <summary>冲刺：收步压身、一帧点火。平地近乎零转向直线；对空按预判斜向上冲，近了改咬</summary>
        private float UpdateSprint(bool grounded) {
            StateTimer++;
            NPC target = FindTarget();
            int dir = Projectile.spriteDirection;
            if (target != null) {
                dir = target.Center.X > Projectile.Center.X ? 1 : -1;
            }

            if (StateTimer < SprintPrepFrames) {
                Projectile.velocity.X *= 0.72f;
                Projectile.spriteDirection = dir;
                if (StateTimer == 1 && !Main.dedServ) {
                    KikasaHoundVoice.Wuff(Projectile.Center, 0.46f, -0.08f);
                }
                return Gravity;
            }
            if (StateTimer == SprintPrepFrames) {
                //一帧点火，不做斜坡。平地走直线，对空按预判点斜向上冲
                Projectile.velocity = SprintLaunchVelocity(target, dir);
                LungeCooldown = MathF.Max(LungeCooldown, 26f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.3f, Volume = 0.26f, MaxInstances = 3 }, Projectile.Center);
                    BurstSmoke(6, -dir);
                }
                if (Main.myPlayer == Projectile.owner) {
                    Projectile.netUpdate = true;
                }
                return Gravity;
            }

            //冲刺段复利续力：对空沿瞄准线续，落地仍只催水平
            if (Projectile.velocity.Y < -1.5f) {
                Projectile.velocity *= 1.004f;
                float cap = SprintSpeed * 1.25f;
                if (Projectile.velocity.Length() > cap) {
                    Projectile.velocity *= cap / Projectile.velocity.Length();
                }
            }
            else {
                Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X * 1.006f,
                    -SprintSpeed * 1.25f, SprintSpeed * 1.25f);
            }
            ShedFx(0.5f);

            if (target != null && StateTimer > SprintPrepFrames) {
                float dx = target.Center.X - Projectile.Center.X;
                float dy = target.Center.Y - Projectile.Center.Y;
                float absDx = MathF.Abs(dx);
                float absDy = MathF.Abs(dy);
                if (!grounded && absDx < 210f && absDy < 340f) {
                    //对空冲进牙距，直接咬，别等落地伏地
                    StartLunge(target, PounceSpeed * 0.9f, 36f, growl: false);
                    return 0.05f;
                }
                if (grounded && absDx < 185f && absDy < 200f) {
                    //冲进扑距，滑进蓄势，一套连招
                    EnterState(StateCrouch);
                    return Gravity;
                }
            }
            //平地撞墙才按水平失速收招；对空冲刺几乎竖直，不能拿 |vx| 当失败
            if (StateTimer > 48f || (grounded && MathF.Abs(Projectile.velocity.X) < 1f)) {
                EnterState(StateRun);
            }
            return grounded ? Gravity : 0.09f;
        }

        /// <summary>冲刺点火速度：平地保持直线，猎物明显高于跳跃高度则按预判点斜向上冲</summary>
        private Vector2 SprintLaunchVelocity(NPC target, int dir) {
            if (target == null) {
                return new Vector2(dir * SprintSpeed, Projectile.velocity.Y);
            }
            Vector2 lead = target.Center + target.velocity * 8f;
            if (lead.Y - Projectile.Center.Y >= -72f) {
                return new Vector2(dir * SprintSpeed, Projectile.velocity.Y);
            }
            Vector2 aim = (lead - Projectile.Center).SafeNormalize(new Vector2(dir, 0f));
            return aim * SprintSpeed;
        }

        /// <summary>落地滑停,硬刹车,爪下犁出尘雾</summary>
        private float UpdateSkid() {
            StateTimer++;
            Projectile.velocity.X *= 0.62f;
            if (!Main.dedServ && StateTimer <= 5f && StateTimer % 2 == 1) {
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(
                    Projectile.Bottom + new Vector2(-Projectile.spriteDirection * Main.rand.NextFloat(4f, 18f), -4f),
                    new Vector2(-Projectile.spriteDirection * Main.rand.NextFloat(0.6f, 1.8f), Main.rand.NextFloat(-1.2f, -0.4f)),
                    new Color(60, 22, 24) * 0.85f, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(18, 30), 0.012f);
            }
            if (StateTimer >= SkidFrames) {
                EnterState(StateRun);
            }
            return Gravity;
        }

        private float UpdateDissolve() {
            StateTimer++;
            Projectile.velocity.X *= 0.9f;

            //化雾：黑红潮气一路散
            if (!Main.dedServ && StateTimer % 3 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 12f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.4f, -0.5f)),
                    new Color(28, 10, 12) * 0.85f, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(36, 60));
            }
            if (!Main.dedServ && StateTimer % 4 == 0) {
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1f, -0.3f)),
                    new Color(46, 16, 20) * 0.8f, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(24, 40), 0.012f);
            }
            if (StateTimer >= DissolveFrames) {
                Projectile.Kill();
            }
            return 0.08f;
        }

        private void ApplyGravity(float gravity) {
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + gravity, MaxFall);
        }

        /// <summary>空中沿速度方向压低身位，落地回正</summary>
        private void UpdateTilt(bool grounded) {
            float target = 0f;
            if (!grounded && State != StateDissolve) {
                float vx = MathF.Max(MathF.Abs(Projectile.velocity.X), 3f);
                target = MathHelper.Clamp(MathF.Atan2(Projectile.velocity.Y, vx) * 0.6f, -0.52f, 0.52f)
                    * Projectile.spriteDirection;
            }
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, target, grounded ? 0.35f : 0.2f);
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist >= ChaseRange) {
                    continue;
                }
                //超高目标仍算看见，但加高度惩罚，地上的菜优先
                float overHang = MathF.Max(0f, Projectile.Center.Y - npc.Center.Y - AirAttackDy);
                float score = dist + overHang * 1.6f;
                if (score < bestScore) {
                    bestScore = score;
                    best = npc;
                }
            }
            return best;
        }

        //==================== 表现派发（全部本机） ====================

        /// <summary>起扑那一帧：俯冲风声 + 蹬地烬环。低吠在伏地里已经盖住，空中顺势咬才在这里补</summary>
        private void LaunchFx(Vector2 aim, bool growl) {
            if (Main.dedServ) {
                return;
            }
            if (growl) {
                KikasaHoundVoice.Wuff(Projectile.Center, 0.5f, -0.04f);
            }
            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.55f, Volume = 0.3f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center - aim * 6f, Vector2.Zero,
                new Color(214, 84, 34) * 0.4f, 0.1f)?.Configure(0.1f, 0.5f, 13);
            for (int i = 0; i < 7; i++) {
                Vector2 vel = -aim.RotatedByRandom(0.5f) * Main.rand.NextFloat(1.6f, 4.6f);
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 8f), vel,
                    new Color(56, 20, 24) * 0.9f, Main.rand.NextFloat(0.26f, 0.44f))
                    ?.Configure(Main.rand.Next(18, 30), 0.014f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaDreamAsh>(Projectile.Center,
                    -aim * Main.rand.NextFloat(0.8f, 2f) + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    new Color(214, 84, 34), Main.rand.NextFloat(0.10f, 0.15f))
                    ?.Configure(Main.rand.Next(26, 44), true);
            }
        }

        /// <summary>高速段沿途撕烟剥烬，贴着旧位置生，像从身上撕下来留在原地</summary>
        private void ShedFx(float rate) {
            if (Main.dedServ) {
                return;
            }
            float speed = Projectile.velocity.Length();
            if (speed < 8f) {
                return;
            }
            if (Main.rand.NextFloat() < rate) {
                Vector2 at = Projectile.Center - Projectile.velocity * Main.rand.NextFloat(0.4f, 1.1f)
                    + Main.rand.NextVector2Circular(10f, 8f);
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(at,
                    Projectile.velocity * 0.16f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    new Color(52, 18, 22) * 0.85f, Main.rand.NextFloat(0.22f, 0.38f))
                    ?.Configure(Main.rand.Next(16, 26), 0.013f);
            }
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_KikasaDreamAsh>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 8f),
                    Projectile.velocity * 0.08f + new Vector2(0f, Main.rand.NextFloat(-0.6f, 0.2f)),
                    Main.rand.NextBool(3) ? new Color(214, 84, 34) : new Color(38, 12, 14),
                    Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(20, 40), Main.rand.NextBool(3));
            }
        }

        /// <summary>脚边一撮向后踹出的黑烟</summary>
        private void BurstSmoke(int count, int dir) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = new(dir * Main.rand.NextFloat(0.8f, 3.4f), Main.rand.NextFloat(-1.6f, -0.2f));
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(
                    Projectile.Bottom + new Vector2(Main.rand.NextFloat(-14f, 14f), -6f), vel,
                    new Color(56, 20, 24) * 0.9f, Main.rand.NextFloat(0.24f, 0.42f))
                    ?.Configure(Main.rand.Next(18, 32), 0.014f);
            }
        }

        /// <summary>奔跑蹬地的小口尘雾</summary>
        private void FootPuff() {
            PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(
                Projectile.Bottom + new Vector2(-Projectile.spriteDirection * 10f, -3f),
                new Vector2(-Projectile.spriteDirection * Main.rand.NextFloat(0.3f, 1f), Main.rand.NextFloat(-0.7f, -0.2f)),
                new Color(44, 16, 20) * 0.7f, Main.rand.NextFloat(0.16f, 0.26f))
                ?.Configure(Main.rand.Next(14, 22), 0.010f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //扑咬咬中把冲量交给猎物，身体挂着骤减
            if (State == StateLunge) {
                Projectile.velocity *= 0.35f;
                if (Main.myPlayer == Projectile.owner) {
                    Projectile.netUpdate = true;
                }
            }
            //梦火（焰×魇）：犬牙带火，咬着就燃，buff 骑原版同步，只在 owner 端施加
            if (Main.myPlayer == Projectile.owner
                && KikasaServants.KikasaEffigyBoard.HasDreamFireEdge(Owner)) {
                target.AddBuff(ModContent.BuffType<KikasaWisps.KikasaWispBurn>(), 120);
            }
            if (Main.dedServ) {
                return;
            }
            bool bite = State == StateLunge;
            KikasaHoundVoice.Worry(target.Center, bite ? 0.78f : 0.48f, bite ? 0.04f : -0.06f);
            if (bite) {
                PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(target.Center, Vector2.Zero,
                    new Color(214, 84, 34) * 0.36f, 0.06f)?.Configure(0.06f, 0.4f, 11);
            }
            int mist = bite ? 4 : 2;
            for (int i = 0; i < mist; i++) {
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(
                    target.Center + Main.rand.NextVector2Circular(12f, 10f),
                    Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1.4f, 1.4f),
                    new Color(56, 20, 24) * 0.9f, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(16, 28), 0.013f);
            }
            for (int i = 0; i < (bite ? 7 : 5); i++) {
                PRTLoader.NewParticle<PRT_KikasaDreamAsh>(
                    target.Center + Main.rand.NextVector2Circular(14f, 10f),
                    new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), Main.rand.NextFloat(-2.6f, -0.4f)),
                    Main.rand.NextBool(3) ? new Color(214, 84, 34) : new Color(40, 12, 14),
                    Main.rand.NextFloat(0.10f, 0.18f))
                    ?.Configure(Main.rand.Next(30, 60), Main.rand.NextBool(3));
            }
        }

        //==================== 帧与绘制 ====================

        //帧逻辑与倒影同源（原版狼 FindFrame）：跃 10、坠 11、立 0、落地 12、跑 3-9。
        //接地性与入帧竖速由 AI 在施加重力前采样喂入，别在这里读加过重力的 velocity

        private void UpdateFrame(bool grounded, float vyIn) {
            float vx = Projectile.velocity.X;

            //伏地/滑停/起跑收步都压在落地过渡帧，身子塌着
            if (State == StateCrouch || State == StateSkid
                || (State == StateSprint && StateTimer < SprintPrepFrames)) {
                frameIndex = 12;
                frameCounter = 0f;
                return;
            }
            //扑咬全程绷在跃起帧
            if (State == StateLunge && !grounded) {
                frameIndex = 10;
                frameCounter = 0f;
                return;
            }

            if (!grounded) {
                frameIndex = vyIn < 0f ? 10 : 11;
                frameCounter = 0f;
            }
            else if (MathF.Abs(vx) < 0.2f) {
                frameIndex = 0;
                frameCounter = 0f;
            }
            else {
                frameCounter += MathF.Abs(vx) * 0.4f;
                if (frameIndex == 10 || frameIndex == 11) {
                    frameIndex = 12;
                    frameCounter = 0f;
                }
                else if (frameCounter > 8f) {
                    frameCounter -= 8f;
                    frameIndex++;
                    if (frameIndex > 9 || frameIndex < 3) {
                        frameIndex = 3;
                    }
                    //蹬地拍甩一小口尘雾
                    if (!Main.dedServ && grounded && State == StateRun
                        && (frameIndex == 5 || frameIndex == 9) && MathF.Abs(vx) > 5f) {
                        FootPuff();
                    }
                }
            }
        }

        /// <summary>身体随状态拉伸压扁,蓄势塌身、冲扑沿速度拉长、滑停压住</summary>
        private Vector2 BodyScale() {
            float speed = Projectile.velocity.Length();
            switch (State) {
                case StateCrouch: {
                    float p = MathHelper.Clamp(StateTimer / CrouchFrames, 0f, 1f);
                    return new Vector2(1f + 0.09f * p, 1f - 0.15f * p);
                }
                case StateSprint when StateTimer < SprintPrepFrames: {
                    float p = StateTimer / SprintPrepFrames;
                    return new Vector2(1f + 0.05f * p, 1f - 0.09f * p);
                }
                case StateLunge:
                case StateSprint: {
                    float s = MathHelper.Clamp(speed / 24f, 0f, 1f);
                    return new Vector2(1f + 0.15f * s, 1f - 0.10f * s);
                }
                case StateSkid: {
                    float p = 1f - MathHelper.Clamp(StateTimer / SkidFrames, 0f, 1f);
                    return new Vector2(1f + 0.07f * p, 1f - 0.11f * p);
                }
                default:
                    return Vector2.One;
            }
        }

        /// <summary>双目辉光随状态呼吸,蓄势一路烧亮、扑咬燃满、化雾熄下去</summary>
        private float EyeGlow() {
            float target = State switch {
                StateCrouch => 1.2f + StateTimer / CrouchFrames * 1.3f,
                StateLunge => 1.7f,
                StateSprint => 1.25f,
                StateDissolve => 0.5f,
                _ => 0.95f,
            };
            eyeGlowVis = MathHelper.Lerp(eyeGlowVis, target, 0.25f);
            return eyeGlowVis;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.Wolf);
            Texture2D tex = TextureAssets.Npc[NPCID.Wolf]?.Value;
            if (tex == null) {
                return false;
            }

            int frameH = tex.Height / Main.npcFrameCount[NPCID.Wolf];
            //源矩形上下各内缩 1px，配 shader 帧界钳制双通道防渗色
            Rectangle frame = new(0, frameIndex * frameH + 1, tex.Width, frameH - 2);
            float dissolve = State == StateDissolve
                ? MathHelper.Clamp(StateTimer / DissolveFrames, 0f, 1f) : 0f;
            float alpha = 1f - dissolve * 0.4f;
            SpriteBatch sb = Main.spriteBatch;
            SpriteEffects effects = Projectile.spriteDirection > 0
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = frame.Size() * 0.5f;

            //恶灵拖尾门,冲刺/扑咬全开,其余按速度渐入,化雾让位
            float speed = Projectile.velocity.Length();
            float ghostGate = State is StateLunge or StateSprint
                ? 1f : MathHelper.Clamp((speed - 9.5f) / 5f, 0f, 1f);
            ghostGate *= 1f - dissolve;

            DrawAll(sb, tex, frame, alpha, dissolve, ghostGate, speed, effects, origin);
            return false;
        }

        private void DrawAll(SpriteBatch sb, Texture2D tex, Rectangle frame, float alpha,
            float dissolve, float ghostGate, float speed, SpriteEffects effects, Vector2 origin) {

            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = hound != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 bodyScale = BodyScale();

            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                //逐帧共享参数上载一次，残影与本体只改溶蚀/目光/翻涌
                hound.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                hound.Parameters["uSeed"]?.SetValue(Seed);
                hound.Parameters["uUvRect"]?.SetValue(new Vector4(
                    0f, frame.Y / (float)tex.Height, 1f, frame.Height / (float)tex.Height));
                hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)frame.Height);
                hound.Parameters["uFlipH"]?.SetValue(Projectile.spriteDirection > 0 ? 1f : 0f);
                hound.Parameters["uFlipV"]?.SetValue(0f);
                //实体模式：无水线裁剪，体成而实
                hound.Parameters["uMode"]?.SetValue(1f);
                hound.Parameters["uSeamGate"]?.SetValue(0f);
                hound.Parameters["uEyeAnchor"]?.SetValue(KikasaHoundReflection.EyeAnchor);
                hound.Parameters["uEdgeTint"]?.SetValue(new Vector3(0.66f, 0.17f, 0.10f));
                hound.CurrentTechnique = hound.Techniques["TechHound"];

                //恶灵拖尾,旧位置上一串越来越残破的狼影,前几只还带着余目
                if (ghostGate > 0.05f) {
                    for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                        Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                        if (oldCenter == Projectile.Size * 0.5f) {
                            continue;
                        }
                        float fall = 1f - k / (float)Projectile.oldPos.Length;
                        float ghostA = ghostGate * MathF.Pow(fall, 1.5f) * 0.55f;
                        if (ghostA < 0.03f) {
                            continue;
                        }
                        hound.Parameters["uDissolve"]?.SetValue(
                            MathF.Min(dissolve + 0.22f + (1f - fall) * 0.55f, 0.95f));
                        hound.Parameters["uEyeGlow"]?.SetValue(
                            MathF.Max(0f, 0.6f - k * 0.16f) * ghostGate);
                        hound.Parameters["uWobble"]?.SetValue(0.012f);
                        hound.CurrentTechnique.Passes[0].Apply();
                        sb.Draw(tex, oldCenter - Main.screenPosition, frame,
                            new Color(255, 255, 255, (byte)(ghostA * 255f)),
                            Projectile.oldRot[k], origin, bodyScale, SpriteEffects.None, 0f);
                    }
                }

                //本体
                hound.Parameters["uDissolve"]?.SetValue(dissolve);
                hound.Parameters["uEyeGlow"]?.SetValue(EyeGlow());
                hound.Parameters["uWobble"]?.SetValue(0.006f + MathF.Min(speed * 0.0004f, 0.010f));
                hound.CurrentTechnique.Passes[0].Apply();
                //着色器自己按 uFlipH 翻采样；再叠 SpriteEffects 会双翻回到原生朝左
                sb.Draw(tex, Projectile.Center - Main.screenPosition, frame,
                    new Color(255, 255, 255, (byte)(alpha * 255f)),
                    Projectile.rotation, origin, bodyScale, SpriteEffects.None, 0f);
            }
            else {
                //无着色器回退,残影平色墨影,本体近黑剪影
                if (ghostGate > 0.05f) {
                    for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                        Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                        if (oldCenter == Projectile.Size * 0.5f) {
                            continue;
                        }
                        float fall = 1f - k / (float)Projectile.oldPos.Length;
                        sb.Draw(tex, oldCenter - Main.screenPosition, frame,
                            new Color(22, 8, 11) * (0.4f * fall * ghostGate),
                            Projectile.oldRot[k], origin, bodyScale, effects, 0f);
                    }
                }
                sb.Draw(tex, Projectile.Center - Main.screenPosition, frame,
                    new Color(16, 7, 10) * alpha,
                    Projectile.rotation, origin, bodyScale, effects, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    /// <summary>
    /// 恶犬声带。低吠 <see cref="CWRSound.DogWuff"/>、撕咬 <see cref="CWRSound.DogWorry"/>。
    /// 素材本身已是犬声，不要再按原版 Roar 那套极端降调。六犬同场 ReplaceOldest 顶掉最旧的。
    /// </summary>
    internal static class KikasaHoundVoice
    {
        internal static void Wuff(Vector2 pos, float volume, float pitch = 0f, int maxInstances = 4) {
            SoundEngine.PlaySound(CWRSound.DogWuff with {
                Volume = volume,
                Pitch = pitch,
                PitchVariance = 0.08f,
                MaxInstances = maxInstances,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest,
            }, pos);
        }

        internal static void Worry(Vector2 pos, float volume, float pitch = 0f, int maxInstances = 4) {
            SoundEngine.PlaySound(CWRSound.DogWorry with {
                Volume = volume,
                Pitch = pitch,
                PitchVariance = 0.08f,
                MaxInstances = maxInstances,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest,
            }, pos);
        }
    }
}
