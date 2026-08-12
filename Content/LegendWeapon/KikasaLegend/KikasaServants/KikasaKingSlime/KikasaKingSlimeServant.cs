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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaKingSlime
{
    /// <summary>
    /// 鬼奴·湖水版史莱姆王。血湖之水凝成的凝胶巨体，全场唯一以弹道跳跃为移动方式：
    /// 出场是湖面隆起血水穹丘（三次抽提越隆越高）、整团拔起凝形；跟随靠有节奏的
    /// 蹲底—起跳—落水压砸循环；攻击为高跳压砸（落点涟漪预告 + 落地按质量守恒分裂
    /// 小血史莱姆）与重踏血浪（双向宽矮横推浪）。挤压拉伸是全身语言，王冠是真身
    /// 残留物——金属实体骑在凝胶上，弹簧滞后，溶解遣返时最后沉没。
    /// 联机同克眼契约：owner 裁决转场盖 netUpdate 章，节拍闩防快照回卷，
    /// 子弹幕只在 owner 端生成，生命线只有 owner 判
    /// </summary>
    internal class KikasaKingSlimeServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>压砸接触基伤（召唤加成前）</summary>
        internal const int SlamDamage = 420;

        /// <summary>小血史莱姆接触基伤（召唤加成前），由分裂弹幕消费</summary>
        internal const int MiniDamage = 220;

        /// <summary>横推血浪基伤（召唤加成前），由血浪弹幕消费</summary>
        internal const int WaveDamage = 220;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateSlam = 2;
        private const int StateWaveSlam = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内相位号：出水=穹丘/腾空/落定，跟随=歇/蹲/空/落，攻击=各自出招相位</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：预兆→三次抽提隆起→拔起腾空→落定
        private const int OmenEnd = 30;
        private const int HeaveLen = 24;
        private const int SwellEnd = OmenEnd + HeaveLen * 3;
        private const int EmergeSettleFrames = 26;
        private const int EmergeTimeout = 300;

        //跟随跳：蹲底短、腾空由弹道决定、落地回弹后必歇一拍——节奏而非抖动
        private const int HopCrouchFrames = 10;
        private const int HopSettleFrames = 14;
        private const int HopMinRest = 18;
        private const float HopTriggerDist = 340f;

        //高跳压砸：深蹲→冲天→顶点悬停锁落点→一帧折向砸下→落坑回弹
        private const int SlamCrouchFrames = 20;
        private const int SlamRiseMax = 50;
        private const int SlamHangFrames = 6;
        private const int SlamDiveMax = 70;
        private const int SlamSettleFrames = 26;
        private const float SlamDiveSpeed = 27f;

        //重踏血浪：更深长的蹲（浪要靠体重）→矮平快跳→落地起浪
        private const int WaveCrouchFrames = 26;
        private const int WaveAirMax = 60;
        private const int WaveSettleFrames = 22;

        //溶解：先坠回湖面，落定后身体塌成血洼，王冠浮面停一拍、最后倾覆沉没。
        //时间线锚在落定帧上（空中被遣返时坠落要多久不定），另设绝对上限兜底
        private const int MeltStart = 12;
        private const int MeltFrames = 46;
        private const int CrownFloatRel = 50;
        private const int CrownSinkRel = 72;
        private const int DissolveTailRel = 100;
        private const int DissolveHardCap = 170;

        private const float Gravity = 0.55f;

        //==================== 本地表现量（不入同步，换场清闩）====================

        private int frameTick;
        private int frameIndex;
        private int groundFrameStep;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        /// <summary>本状态内已放过节拍的最高相位号：快照回卷不重播起跳与落地拍（7.5）</summary>
        private int lastBeatPhase = -1;
        private bool impactDone;
        private bool dissolveSplashed;
        private bool crownSankFired;
        private int emergeSettleStart = -1;
        private int dissolveGroundFrame = -1;
        /// <summary>压砸锁定的落点 X（顶点悬停帧取定，落点涟漪预告共用）</summary>
        private float diveTargetX;
        /// <summary>出水抽提拍已放次数（0~3）</summary>
        private int heavesFired;

        //挤压拉伸弹簧：落地压扁、回弹晃两下才静，凝胶的分量全在这里
        private float squashSy = 1f;
        private float squashVel;
        /// <summary>分裂后失去的体量 0~1，小史莱姆回流逐只补满</summary>
        private float massLost;
        /// <summary>吞回小史莱姆的鼓胀拍</summary>
        private int swellPulse;
        private int lastMiniCount;
        private float lastMiniNearDist = float.MaxValue;

        //王冠弹簧：刚体骑软体，滞后与过冲是"真身残留物"的证词
        private float crownDy;
        private float crownDyVel;

        //血凝胶配色随观看域鬼雨异化冷化，与湖系同族；王冠鎏金只做次要点缀层
        private static Color GelMain => KikasaDomain.CoolTint(new(224, 66, 62), new(122, 154, 160));
        private static Color GelDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color GelDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color GelBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color CrownGold => KikasaDomain.CoolTint(new(228, 186, 104), new(172, 182, 168));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        private const float BodyDrawScale = 1.12f;
        /// <summary>身体贴图单帧显示高度（帧高 120 × 缩放）</summary>
        private const float BodyDrawHeight = 120f * BodyDrawScale;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面（穹丘隆起点）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SlamDamage);
            //整团凝胶先沉在湖下，穹丘从这里往上顶
            Vector2 spawn = new(emergeAt.X, emergeAt.Y + 128f - 43f);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"), spawn, Vector2.Zero,
                ModContent.ProjectileType<KikasaKingSlimeServant>(), damage, 8f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            //高跳会窜出屏顶，出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 110;
            Projectile.height = 86;
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

        /// <summary>接触伤害只开在压砸下坠窗，与可见的砸落严格对齐</summary>
        public override bool? CanDamage()
            => State == StateSlam && (int)StateParam == 3 ? null : false;

        /// <summary>凝胶体命中：随当前挤压拉伸取可见轮廓的 AABB</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            int w = (int)(Projectile.width * visSx);
            int h = (int)(Projectile.height * visSy);
            Rectangle body = new((int)(Projectile.Center.X - w * 0.5f),
                (int)(Projectile.Bottom.Y - h), w, h);
            return body.Intersects(targetHitbox);
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
            //穹丘还没拔起就要收场：整团都在水下，不演谢幕——
            //否则会凭空闪出一只完整史莱姆再化掉
            if (State == StateEmerge && (int)StateParam == 0) {
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

            //生命线：只有 owner 裁决——服务器无领域状态（既定契约），
            //迟入场客户端首份快照前也会误判；其余端只跟包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SlamDamage);

            //换场清闩：远端可能靠收包切状态而非本地同拍转场，
            //残闩会吞掉新场的起跳音、落地拍与过水线水花
            if (State != lastSeenState) {
                lastSeenState = State;
                lastBeatPhase = -1;
                impactDone = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                    crownSankFired = false;
                    dissolveGroundFrame = -1;
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain, authority); break;
                case StateFollow: UpdateFollow(owner, domain, authority); break;
                case StateSlam: UpdateSlam(owner, domain, authority); break;
                case StateWaveSlam: UpdateWaveSlam(owner, domain, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateJelly(domain);
            UpdateCrownSpring();
            UpdateMassLedger();
            UpdateFrames();
            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (swellPulse > 0) {
                swellPulse--;
            }

            float glow = CurrentAlpha() * 0.5f;
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.4f * glow, 0.09f * glow, 0.08f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        /// <summary>节拍闩：只在相位严格前进时放行一次演出，快照回卷重跑转场不重播</summary>
        private bool TryFireBeat(int phaseKey) {
            if (phaseKey <= lastBeatPhase) {
                return false;
            }
            lastBeatPhase = phaseKey;
            return true;
        }

        //==================== 出水：血水穹丘 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            bool viewed = ViewedOwner;

            //全局兜底：出水演出绝不允许没有出口
            if (t > EmergeTimeout) {
                EnterFollow(authority, 40);
                return;
            }

            if (phase == 0) {
                //穹丘期：位置由时间线直摆，不走物理
                Projectile.velocity = Vector2.Zero;
                float bottomOffset = DomeBottomOffset(t);
                Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY + bottomOffset);

                if (t < OmenEnd) {
                    //预兆：涟漪自外向隆起点收拢，水下咕噜的碎泡
                    if (viewed) {
                        if (t % 6 == 2) {
                            float converge = 1f - t / (float)OmenEnd;
                            float side = t / 6 % 2 == 0 ? 1f : -1f;
                            KikasaDomainDeco.RippleAt(
                                new Vector2(Projectile.Center.X + side * converge * 64f, lakeY),
                                0.4f + (1f - converge) * 0.5f);
                        }
                        if (t % 9 == 4) {
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(
                                new Vector2(Projectile.Center.X + Main.rand.NextFloat(-30f, 30f), lakeY - 2f),
                                new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1.2f, 2.4f)),
                                GelMain * Main.rand.NextFloat(0.35f, 0.5f),
                                Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20), 0f);
                        }
                        if (t == 6 || t == 20) {
                            SoundEngine.PlaySound(SoundID.Drip with {
                                Volume = 0.5f,
                                Pitch = t == 6 ? -0.7f : -0.4f,
                                MaxInstances = 2
                            }, new Vector2(Projectile.Center.X, lakeY));
                        }
                    }
                    return;
                }

                //三次抽提：每拍开场一记闷涌，穹丘一截截被顶出水面
                int heaveIndex = (t - OmenEnd) / HeaveLen;
                if (heaveIndex >= heavesFired && heaveIndex < 3 && (t - OmenEnd) % HeaveLen == 0) {
                    heavesFired = heaveIndex + 1;
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.45f + heaveIndex * 0.15f,
                        Pitch = -0.8f + heaveIndex * 0.18f,
                        MaxInstances = 2
                    }, new Vector2(Projectile.Center.X, lakeY));
                    SoundEngine.PlaySound(SoundID.NPCHit1 with {
                        Volume = 0.3f + heaveIndex * 0.1f,
                        Pitch = -0.85f + heaveIndex * 0.1f,
                        MaxInstances = 2
                    }, Projectile.Center);
                    if (viewed) {
                        KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, lakeY), 4 + heaveIndex * 2);
                        ShakeViewer(0.8f + heaveIndex * 0.7f);
                    }
                }
                if (viewed) {
                    //穹丘基缘双侧涟漪：丘越高荡得越宽
                    float rise = MathHelper.Clamp((t - OmenEnd) / (float)(SwellEnd - OmenEnd), 0f, 1f);
                    if (t % 5 == 1) {
                        float side = t / 5 % 2 == 0 ? 1f : -1f;
                        KikasaDomainDeco.RippleAt(
                            new Vector2(Projectile.Center.X + side * (46f + rise * 42f), lakeY),
                            0.35f + rise * 0.45f);
                    }
                    //丘顶血水成帘往下淌
                    if (t % 3 == 0 && rise > 0.2f) {
                        Vector2 crest = new(Projectile.Center.X + Main.rand.NextFloat(-40f, 40f),
                            Projectile.Bottom.Y - BodyDrawHeight * visSy * Main.rand.NextFloat(0.55f, 0.95f));
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(crest,
                            new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(2f, 3.4f)),
                            GelMain * Main.rand.NextFloat(0.4f, 0.55f),
                            Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(12, 22), 0f);
                    }
                }

                if (t >= SwellEnd) {
                    //拔起拍：整团血水撕离湖面，一帧起速；宽矮浪冠 + 闷吼 + 王冠出世
                    StateParam = 1;
                    Projectile.velocity = new Vector2(0f, -12.5f);
                    if (TryFireBeat(1)) {
                        SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.55f, Pitch = -0.65f, MaxInstances = 2 }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, Pitch = 0.25f, MaxInstances = 2 }, Projectile.Center);
                        crownDy = 26f;
                        crownDyVel = 0f;
                        if (viewed) {
                            TearFreeBurst(new Vector2(Projectile.Center.X, lakeY));
                        }
                    }
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 1) {
                //腾空：弧顶重力减轻——拔起后有一拍悬停，整团凝胶在空中收形
                float g = Gravity * (0.45f + 0.55f * MathHelper.Clamp(MathF.Abs(Projectile.velocity.Y) / 12.5f, 0f, 1f));
                Projectile.velocity.Y += g;
                Projectile.velocity.X *= 0.98f;

                if (Projectile.velocity.Y > 0f && Projectile.Bottom.Y >= lakeY) {
                    //落定拍：第一次压扁回弹，宽矮冲击的首次亮相
                    Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
                    Projectile.velocity = Vector2.Zero;
                    StateParam = 2;
                    emergeSettleStart = t;
                    if (TryFireBeat(2)) {
                        LandingBeat(domain, 3.2f, 8, 1.6f);
                    }
                    Projectile.netUpdate = authority;
                }
                return;
            }

            //落定：凝胶晃着归位，晃完即觉醒入跟随
            Projectile.velocity = Vector2.Zero;
            Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
            if (emergeSettleStart < 0) {
                emergeSettleStart = t;
            }
            if (t - emergeSettleStart >= EmergeSettleFrames) {
                EnterFollow(authority, 40);
            }
        }

        /// <summary>穹丘时间线：预兆全沉，三次抽提各顶一截（pow 曲线，前憋后冲）</summary>
        private static float DomeBottomOffset(int t) {
            if (t < OmenEnd) {
                return 128f;
            }
            //各拍终点：128→100→72→46
            float[] steps = [128f, 100f, 72f, 46f];
            int heave = Math.Min((t - OmenEnd) / HeaveLen, 2);
            float k = MathHelper.Clamp((t - OmenEnd - heave * HeaveLen) / (float)HeaveLen, 0f, 1f);
            //憋住大半拍再猛地一顶
            float ease = MathF.Pow(k, 3.2f);
            return MathHelper.Lerp(steps[heave], steps[heave + 1], ease);
        }

        /// <summary>拔起浪冠：宽矮签名——浅角双侧血扇 + 垂帘 + 横压扩散环，不起高柱</summary>
        private void TearFreeBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.6f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(66f, 0f), 1.2f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(62f, 0f), 1.1f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-30f, 0f), 10);
            KikasaDomainDeco.SplashAt(hit + new Vector2(30f, 0f), 10);

            //浅角血扇：贴着水面往两侧扫，不冲天
            for (int side = -1; side <= 1; side += 2) {
                for (int i = 0; i < 11; i++) {
                    float angle = side > 0
                        ? -Main.rand.NextFloat(0.10f, 0.42f)
                        : -MathHelper.Pi + Main.rand.NextFloat(0.10f, 0.42f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        hit + new Vector2(side * Main.rand.NextFloat(10f, 44f), -4f),
                        angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8.5f),
                        Main.rand.NextBool(3) ? GelDeep : GelMain,
                        Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(20, 34));
                }
            }
            //底面撕离时的垂帘血滴
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-52f, 52f), -Main.rand.NextFloat(4f, 26f)),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.6f, 3.2f)),
                    GelMain * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.45f, 0.7f))?.Configure(Main.rand.Next(16, 28), 0f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-40f, 40f), -8f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.6f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.75f, 1.05f))
                    ?.Configure(Main.rand.Next(60, 100));
            }
            //横压扁的扩散环：宽而矮，量感钉在水面
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, GelDeep, 0.1f)
                ?.Configure(new Vector2(1f, 0.34f), 0f, 0.4f, 12);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.4f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 1 }, hit);
            ShakeViewer(6f);
        }

        private void EnterFollow(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = Math.Max(attackCooldown, cooldown);
            Projectile.netUpdate = authority;
        }

        //==================== 跳跃跟随 ====================

        private void UpdateFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            int phase = (int)StateParam;

            //跟丢硬贴回：半个地图外没资格淌血；节拍闩一并复位
            if (Vector2.Distance(owner.Center, Projectile.Center) > 2400f) {
                Projectile.Center = new Vector2(owner.Center.X - owner.direction * 160f, lakeY - Projectile.height * 0.5f);
                Projectile.velocity = Vector2.Zero;
                StateParam = 0;
                StateTimer = 0;
                lastBeatPhase = -1;
                Projectile.netUpdate = authority;
                return;
            }

            float anchorX = owner.Center.X - owner.direction * 130f;

            if (phase == 0) {
                //歇拍：坐在水面上呼吸，体重把湖面压出慢圈
                Projectile.velocity = Vector2.Zero;
                Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);

                if (ViewedOwner && t % 46 == 12) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 0.55f);
                }
                if (!Main.dedServ && Main.rand.NextBool(26)) {
                    DripFromRim();
                }

                //出手裁决：压砸与血浪交替；歇拍里才起手，出招总是从静止读起
                int target = FindTarget(owner);
                if (target >= 0 && attackCooldown <= 0 && t > 24) {
                    attackIndex++;
                    State = attackIndex % 2 == 1 ? StateSlam : StateWaveSlam;
                    StateTimer = 0;
                    StateParam = 0;
                    Projectile.netUpdate = authority;
                    return;
                }

                //跟随跳：歇够了、离锚点够远才起跳——一跳一歇，不许碎步
                if (t >= HopMinRest && MathF.Abs(anchorX - Projectile.Center.X) > HopTriggerDist) {
                    StateParam = 1;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 1) {
                //蹲底：整团压扁蓄力
                Projectile.velocity = Vector2.Zero;
                Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
                if (t >= HopCrouchFrames) {
                    //起跳一帧定弹道：飞行时长按距离缩放，落点带确定性微偏
                    float dx = anchorX + MathF.Sin(Seed * 5f + attackIndex) * 40f - Projectile.Center.X;
                    float flight = MathHelper.Clamp(MathF.Abs(dx) / 13f, 26f, 44f);
                    Projectile.velocity = new Vector2(dx / flight, -Gravity * flight * 0.5f);
                    StateParam = 2;
                    StateTimer = 0;
                    if (TryFireBeat(2)) {
                        TakeoffBeat(domain, 0.9f);
                    }
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 2) {
                //腾空：弹道飞行，顶点微悬
                float g = Gravity * (0.6f + 0.4f * MathHelper.Clamp(MathF.Abs(Projectile.velocity.Y) / 11f, 0f, 1f));
                Projectile.velocity.Y += g;
                if (Projectile.velocity.Y > 0f && Projectile.Bottom.Y >= lakeY) {
                    Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
                    Projectile.velocity = Vector2.Zero;
                    StateParam = 3;
                    StateTimer = 0;
                    if (TryFireBeat(3)) {
                        LandingBeat(domain, 1.0f, 4, 0.7f);
                    }
                }
                //跳过头兜底
                if (t > 90) {
                    StateParam = 3;
                    StateTimer = 0;
                }
                return;
            }

            //落地回弹拍：晃完回歇，节拍闩清零迎接下一跳
            Projectile.velocity = Vector2.Zero;
            Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
            if (t >= HopSettleFrames) {
                StateParam = 0;
                StateTimer = 0;
                lastBeatPhase = -1;
            }
        }

        //==================== 高跳压砸 ====================

        private void UpdateSlam(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);

            if (phase == 0) {
                //深蹲：目标没了就不空跳
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                Projectile.velocity = Vector2.Zero;
                Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);

                //蓄力吸水：湖面血珠被拽进体内，72% 后静默——爆发前的吸气
                if (!Main.dedServ && t < SlamCrouchFrames * 0.72f && t % 2 == 1) {
                    Vector2 from = new(Projectile.Center.X + Main.rand.NextFloat(-90f, 90f), lakeY - Main.rand.NextFloat(0f, 6f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        (Projectile.Center - from) * 0.12f,
                        GelMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(9, 0f);
                }
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = -0.8f, MaxInstances = 2 }, Projectile.Center);
                }

                if (t >= SlamCrouchFrames) {
                    //冲天一帧定速：竖直为主，横向只给个追人的引子
                    NPC npc = Main.npc[target];
                    float dx = npc.Center.X + npc.velocity.X * 18f - Projectile.Center.X;
                    Projectile.velocity = new Vector2(MathHelper.Clamp(dx / 42f, -8f, 8f), -20f);
                    StateParam = 1;
                    StateTimer = 0;
                    if (TryFireBeat(1)) {
                        TakeoffBeat(domain, 1.6f);
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.55f, Pitch = 0.15f, MaxInstances = 2 }, Projectile.Center);
                    }
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 1) {
                //冲天：重力照常，逼近顶点自然减速
                Projectile.velocity.Y += Gravity * 0.9f;
                //落点预告：涟漪跟着目标现在的位置游走，读作"它在瞄"
                if (ViewedOwner && t % 6 == 2 && target >= 0) {
                    KikasaDomainDeco.RippleAt(new Vector2(Main.npc[target].Center.X, lakeY), 0.45f);
                }
                if (Projectile.velocity.Y >= -2.5f || t > SlamRiseMax) {
                    //顶点：锁死落点
                    diveTargetX = target >= 0
                        ? Main.npc[target].Center.X + Main.npc[target].velocity.X * 12f
                        : Projectile.Center.X;
                    StateParam = 2;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 2) {
                //顶点悬停：动量吐尽，一拍死寂——砸落前的静默
                Projectile.velocity *= 0.78f;
                if (ViewedOwner && t % 3 == 1) {
                    KikasaDomainDeco.RippleAt(new Vector2(diveTargetX, lakeY), 0.7f);
                }
                if (t >= SlamHangFrames) {
                    //一帧折向砸下：陡角直线，中途不转向——直才重
                    Vector2 aim = new Vector2(diveTargetX, lakeY + 10f) - Projectile.Center;
                    float vx = MathHelper.Clamp(aim.X / MathF.Max(aim.Y / 24f, 1f), -9f, 9f);
                    Projectile.velocity = new Vector2(vx, SlamDiveSpeed);
                    StateParam = 3;
                    StateTimer = 0;
                    if (TryFireBeat(3)) {
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.75f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                    }
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 3) {
                //砸落：复利续力，接触窗全开
                Projectile.velocity.Y *= 1.012f;
                if (ViewedOwner && t % 3 == 0) {
                    KikasaDomainDeco.RippleAt(new Vector2(diveTargetX, lakeY), 0.9f);
                }
                //沿途甩出速度拉伸的血水
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center - Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(26f, 26f),
                        -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                        GelMain * 0.55f, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(10, 18), 0f);
                }

                if (Projectile.Bottom.Y >= lakeY || t > SlamDiveMax) {
                    //压砸落地：宽矮冲击 + 质量守恒分裂
                    Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
                    float fallSpeed = Projectile.velocity.Y;
                    Projectile.velocity = Vector2.Zero;
                    if (!impactDone) {
                        impactDone = true;
                        SlamImpact(owner, domain, authority, fallSpeed);
                    }
                    StateParam = 4;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            //落坑回弹：分裂出去的体量瘪着，等小史莱姆回流
            Projectile.velocity = Vector2.Zero;
            Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
            if (t >= SlamSettleFrames) {
                EndAttack(authority, 130);
            }
        }

        /// <summary>压砸着水：宽矮浪冠全套 + owner 端按质量守恒撒出小血史莱姆</summary>
        private void SlamImpact(Player owner, KikasaDomainPlayer domain, bool authority, float fallSpeed) {
            Vector2 hit = new(Projectile.Center.X, domain.LakeWorldY);
            float ke = MathHelper.Clamp(fallSpeed / SlamDiveSpeed, 0.5f, 1.2f);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.35f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = -0.7f, MaxInstances = 2 }, hit);

            if (ViewedOwner) {
                //宽矮签名：涟漪一排横铺，血扇全部贴水面扫
                KikasaDomainDeco.RippleAt(hit, 2.8f);
                KikasaDomainDeco.RippleAt(hit + new Vector2(84f, 0f), 1.3f);
                KikasaDomainDeco.RippleAt(hit - new Vector2(80f, 0f), 1.3f);
                KikasaDomainDeco.RippleAt(hit + new Vector2(150f, 0f), 0.8f);
                KikasaDomainDeco.RippleAt(hit - new Vector2(146f, 0f), 0.8f);
                KikasaDomainDeco.SplashAt(hit + new Vector2(-24f, 0f), 12);
                KikasaDomainDeco.SplashAt(hit + new Vector2(24f, 0f), 12);
                for (int side = -1; side <= 1; side += 2) {
                    for (int i = 0; i < 13; i++) {
                        float angle = side > 0
                            ? -Main.rand.NextFloat(0.08f, 0.38f)
                            : -MathHelper.Pi + Main.rand.NextFloat(0.08f, 0.38f);
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            hit + new Vector2(side * Main.rand.NextFloat(8f, 52f), -5f),
                            angle.ToRotationVector2() * Main.rand.NextFloat(4.5f, 9.5f) * ke,
                            Main.rand.NextBool(3) ? GelDeep : GelMain,
                            Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(22, 36));
                    }
                }
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        hit + new Vector2(Main.rand.NextFloat(-60f, 60f), -8f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.3f, 0.7f)),
                        MistBlood * 0.85f, Main.rand.NextFloat(0.8f, 1.15f))?.Configure(Main.rand.Next(60, 100));
                }
                PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, GelDeep, 0.11f)
                    ?.Configure(new Vector2(1f, 0.3f), 0f, 0.46f, 12);
                ShakeViewer(6f);
            }

            //分裂只在 owner 端定夺，spawn 参数一次带齐（湖面高度即回弹地板）
            if (authority) {
                int count = Main.rand.Next(2, 5);
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(MiniDamage);
                for (int i = 0; i < count; i++) {
                    float lerpK = count == 1 ? 0.5f : i / (float)(count - 1);
                    float side = lerpK < 0.5f ? -1f : 1f;
                    float angle = -MathHelper.PiOver2 + side * MathHelper.Lerp(0.35f, 0.95f, MathF.Abs(lerpK - 0.5f) * 2f);
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6.5f, 9.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        hit + new Vector2(side * Main.rand.NextFloat(10f, 30f), -14f), vel,
                        ModContent.ProjectileType<KikasaMiniBloodSlime>(), damage, 3f,
                        Projectile.owner, domain.LakeWorldY);
                }
            }
        }

        //==================== 重踏血浪 ====================

        private void UpdateWaveSlam(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);

            if (phase == 0) {
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                //更深长的蹲：浪要靠体重，基缘泡沫收拢，72% 后静默
                Projectile.velocity = Vector2.Zero;
                Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
                if (!Main.dedServ && t < WaveCrouchFrames * 0.72f && t % 2 == 0) {
                    float side = t % 4 == 0 ? 1f : -1f;
                    Vector2 from = new(Projectile.Center.X + side * Main.rand.NextFloat(70f, 130f), lakeY - 2f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        (new Vector2(Projectile.Center.X, lakeY - 8f) - from) * 0.1f,
                        GelBright * 0.45f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(10, 0f);
                }
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.45f, Pitch = -0.9f, MaxInstances = 2 }, Projectile.Center);
                }
                if (t % 8 == 4 && ViewedOwner) {
                    ShakeViewer(0.5f + t / (float)WaveCrouchFrames * 1.2f);
                }

                if (t >= WaveCrouchFrames) {
                    //矮平快跳：落在自己与目标之间，浪替它跑完剩下的路
                    float dx = Main.npc[target].Center.X - Projectile.Center.X;
                    float hopDx = MathHelper.Clamp(dx * 0.5f, -280f, 280f);
                    const float flight = 26f;
                    Projectile.velocity = new Vector2(hopDx / flight, -Gravity * flight * 0.55f);
                    StateParam = 1;
                    StateTimer = 0;
                    if (TryFireBeat(1)) {
                        TakeoffBeat(domain, 1.4f);
                    }
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 1) {
                //腾空：矮弧，重力全额——这一跳的意义全在落地
                Projectile.velocity.Y += Gravity;
                if (Projectile.velocity.Y > 0f && Projectile.Bottom.Y >= lakeY || t > WaveAirMax) {
                    Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
                    Projectile.velocity = Vector2.Zero;
                    if (!impactDone) {
                        impactDone = true;
                        WaveImpact(owner, domain, authority);
                    }
                    StateParam = 2;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            //落地稳身：浪已上路，自己收势
            Projectile.velocity = Vector2.Zero;
            Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
            if (t >= WaveSettleFrames) {
                EndAttack(authority, 150);
            }
        }

        /// <summary>重踏着水：双向血浪 owner 端上路，落点演出偏横排</summary>
        private void WaveImpact(Player owner, KikasaDomainPlayer domain, bool authority) {
            Vector2 hit = new(Projectile.Center.X, domain.LakeWorldY);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.6f, Pitch = -0.65f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.45f, MaxInstances = 2 }, hit);

            if (ViewedOwner) {
                KikasaDomainDeco.RippleAt(hit, 2.2f);
                KikasaDomainDeco.RippleAt(hit + new Vector2(70f, 0f), 1.1f);
                KikasaDomainDeco.RippleAt(hit - new Vector2(66f, 0f), 1.1f);
                KikasaDomainDeco.SplashAt(hit, 10);
                for (int side = -1; side <= 1; side += 2) {
                    for (int i = 0; i < 8; i++) {
                        float angle = side > 0
                            ? -Main.rand.NextFloat(0.1f, 0.4f)
                            : -MathHelper.Pi + Main.rand.NextFloat(0.1f, 0.4f);
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            hit + new Vector2(side * Main.rand.NextFloat(6f, 40f), -4f),
                            angle.ToRotationVector2() * Main.rand.NextFloat(3.5f, 7f),
                            Main.rand.NextBool(3) ? GelDeep : GelMain,
                            Main.rand.NextFloat(0.45f, 0.75f))?.Configure(Main.rand.Next(18, 30));
                    }
                }
                PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, GelDeep, 0.09f)
                    ?.Configure(new Vector2(1f, 0.3f), 0f, 0.4f, 11);
                ShakeViewer(4.5f);
            }

            //双向浪：一次落地向两侧横推，spawn 参数一次带齐
            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(WaveDamage);
                for (int dir = -1; dir <= 1; dir += 2) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        hit + new Vector2(dir * 46f, 0f), Vector2.Zero,
                        ModContent.ProjectileType<KikasaBloodSurgeWave>(), damage, 6f,
                        Projectile.owner, dir, domain.LakeWorldY);
                }
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
            bool atSurface = Projectile.Bottom.Y >= lakeY - 1f;

            if (lakeAlive && !atSurface) {
                //还悬在空中：加速坠回湖面再化；坠太久就边坠边化，不吊着等
                Projectile.velocity.X *= 0.92f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.3f, 12f);
                if (Projectile.velocity.Y > 0f && !dissolveSplashed && Projectile.Bottom.Y >= lakeY - 6f) {
                    dissolveSplashed = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                    if (ViewedOwner) {
                        KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, lakeY), 8);
                        KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 1.2f);
                        ShakeViewer(1.6f);
                    }
                }
                if (dissolveGroundFrame < 0 && t >= 40) {
                    dissolveGroundFrame = t;
                }
            }
            else if (lakeAlive) {
                Projectile.velocity = Vector2.Zero;
                Projectile.Bottom = new Vector2(Projectile.Bottom.X, lakeY);
                if (dissolveGroundFrame < 0) {
                    dissolveGroundFrame = t;
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = -0.9f, MaxInstances = 2 }, Projectile.Center);
                    //高速下坠可能整帧跳过过水线窗口，落定帧补上水花拍
                    if (!dissolveSplashed && t > 4) {
                        dissolveSplashed = true;
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                        if (ViewedOwner) {
                            KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, lakeY), 8);
                            KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 1.2f);
                            ShakeViewer(1.6f);
                        }
                    }
                }
            }
            else {
                //湖已不在：原地化水
                Projectile.velocity *= 0.9f;
                if (dissolveGroundFrame < 0) {
                    dissolveGroundFrame = t;
                }
            }

            //身体塌洼期：边化边淌，血洼边缘荡圈
            float melt = MeltProgress();
            if (!Main.dedServ && melt is > 0.02f and < 0.98f) {
                if (t % 2 == 0) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-46f, 46f) * visSx, Main.rand.NextFloat(-20f, 12f)),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(1.2f, 2.6f)),
                        GelMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
                }
                if (lakeAlive && ViewedOwner && t % 7 == 3) {
                    float side = t % 14 < 7 ? 1f : -1f;
                    KikasaDomainDeco.RippleAt(
                        new Vector2(Projectile.Center.X + side * (40f + melt * 50f), lakeY), 0.4f);
                }
            }

            //王冠尾声：浮面一拍→倾覆沉没，湖收走最后的残留物（时间锚在落定帧）
            int g0 = dissolveGroundFrame;
            if (g0 >= 0) {
                if (t == g0 + CrownSinkRel) {
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 2 }, CrownDissolvePos(lakeY));
                }
                if (!crownSankFired && t >= g0 + CrownSinkRel + 10) {
                    crownSankFired = true;
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 2 }, CrownDissolvePos(lakeY));
                    if (lakeAlive && ViewedOwner) {
                        KikasaDomainDeco.RippleAt(new Vector2(CrownDissolvePos(lakeY).X, lakeY), 0.9f);
                        KikasaDomainDeco.SplashAt(new Vector2(CrownDissolvePos(lakeY).X, lakeY), 3);
                    }
                }
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀；再叠一层绝对上限
            bool ownerDone = g0 >= 0 && t >= g0 + DissolveTailRel || t >= DissolveHardCap;
            if (authority && ownerDone) {
                Projectile.Kill();
            }
            else if (!authority && (g0 >= 0 && t >= g0 + DissolveTailRel + 10 || t >= DissolveHardCap + 10)) {
                Projectile.Kill();
            }
        }

        /// <summary>身体塌洼进度：落定帧起算，落定前恒 0</summary>
        private float MeltProgress() {
            if (State != StateDissolve || dissolveGroundFrame < 0) {
                return 0f;
            }
            return MathHelper.Clamp((StateTimer - dissolveGroundFrame - MeltStart) / (float)MeltFrames, 0f, 1f);
        }

        /// <summary>王冠是否已脱离躯体浮上水面（溶解尾声）</summary>
        private bool CrownAfloat
            => State == StateDissolve && dissolveGroundFrame >= 0
            && StateTimer >= dissolveGroundFrame + CrownFloatRel;

        /// <summary>王冠倾沉进度 0~1</summary>
        private float CrownSinkT() {
            if (!CrownAfloat) {
                return 0f;
            }
            return MathHelper.Clamp(
                (StateTimer - dissolveGroundFrame - CrownSinkRel) / (float)(DissolveTailRel - CrownSinkRel), 0f, 1f);
        }

        /// <summary>溶解尾声的王冠位置：浮面轻晃→加速倾沉</summary>
        private Vector2 CrownDissolvePos(float lakeY) {
            float x = Projectile.Center.X;
            float sinkT = CrownSinkT();
            if (sinkT <= 0f) {
                float bob = MathF.Sin(StateTimer * 0.17f + Seed) * 2.4f;
                return new Vector2(x, lakeY - 7f + bob);
            }
            return new Vector2(x, lakeY - 7f + sinkT * sinkT * 44f);
        }

        //==================== 挤压拉伸（凝胶的灵魂）====================

        /// <summary>本帧可见缩放，绘制与 Colliding 共用</summary>
        private float visSx = 1f;
        private float visSy = 1f;

        private void UpdateJelly(KikasaDomainPlayer domain) {
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            bool airborne = IsAirborne();

            if (State == StateEmerge && phase == 0) {
                //穹丘形态：宽扁的水丘随隆起缓慢立起来
                float rise = MathHelper.Clamp((t - OmenEnd) / (float)(SwellEnd - OmenEnd), 0f, 1f);
                visSx = MathHelper.Lerp(1.75f, 1.12f, rise);
                visSy = MathHelper.Lerp(0.5f, 0.78f, rise);
                squashSy = visSy;
                squashVel = 0f;
                return;
            }

            if (State == StateDissolve) {
                //落定前还在坠：保持沿速度拉伸；落定后塌成血洼，越化越矮越摊
                if (dissolveGroundFrame < 0 && MathF.Abs(Projectile.velocity.Y) > 0.5f) {
                    float fallStretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.016f, 0f, 0.5f);
                    visSy = 1f + fallStretch;
                    visSx = 1f / MathF.Sqrt(visSy);
                    return;
                }
                float melt = MeltProgress();
                visSx = 1f + melt * 0.7f;
                visSy = MathF.Max(1f - melt, 0.02f);
                squashSy = visSy;
                squashVel = 0f;
                return;
            }

            if (airborne) {
                //腾空：沿速度拉伸，体积守恒
                float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.016f, 0f, 0.5f);
                visSy = 1f + stretch;
                visSx = 1f / MathF.Sqrt(visSy);
                //落地弹簧的初值只由下坠速度种下：坠得越急砸得越扁
                float fall = MathF.Max(Projectile.velocity.Y, 0f);
                squashSy = MathHelper.Clamp(1f - fall * 0.028f, 0.42f, 1f);
                squashVel = 0f;
                return;
            }

            //蹲底：直接驱动压扁曲线（pow 憋气，越蹲越深）
            float crouchTarget = -1f;
            if (State == StateFollow && phase == 1) {
                crouchTarget = MathHelper.Lerp(1f, 0.62f, MathF.Pow(t / (float)HopCrouchFrames, 1.6f));
            }
            else if (State == StateSlam && phase == 0) {
                crouchTarget = MathHelper.Lerp(1f, 0.5f, MathF.Pow(t / (float)SlamCrouchFrames, 2.2f));
            }
            else if (State == StateWaveSlam && phase == 0) {
                crouchTarget = MathHelper.Lerp(1f, 0.46f, MathF.Pow(t / (float)WaveCrouchFrames, 2.4f));
            }
            if (crouchTarget > 0f) {
                squashSy = MathHelper.Lerp(squashSy, crouchTarget, 0.4f);
                squashVel = 0f;
            }
            else {
                //落地回弹：弹簧晃两下归位
                squashVel += (1f - squashSy) * 0.26f;
                squashVel *= 0.8f;
                squashSy += squashVel;
            }

            //呼吸叠底 + 吞胀拍 + 分裂失量
            float breath = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + Seed) * 0.045f;
            float pulse = swellPulse > 0 ? MathF.Sin(swellPulse / 10f * MathHelper.Pi) * 0.07f : 0f;
            visSy = squashSy + breath * squashSy;
            visSx = (1f + (1f - squashSy) * 0.9f - breath * 0.6f) * (1f - massLost * 0.5f) + pulse;
            visSy *= 1f - massLost * 0.5f - pulse * 0.5f;
            visSx = MathF.Max(visSx, 0.2f);
            visSy = MathF.Max(visSy, 0.2f);
        }

        private bool IsAirborne()
            => State == StateEmerge && (int)StateParam == 1
            || State == StateFollow && (int)StateParam == 2
            || State == StateSlam && (int)StateParam is 1 or 2 or 3
            || State == StateWaveSlam && (int)StateParam == 1
            || State == StateDissolve && Projectile.velocity.Y != 0f;

        /// <summary>质量账本：场上自己的小史莱姆越多，本体瘪得越明显；回流一只鼓一拍</summary>
        private void UpdateMassLedger() {
            int count = 0;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active != true || proj.owner != Projectile.owner
                    || proj.type != ModContent.ProjectileType<KikasaMiniBloodSlime>()) {
                    continue;
                }
                count++;
                float dist = Vector2.Distance(proj.Center, Projectile.Center);
                if (dist < nearestDist) {
                    nearestDist = dist;
                }
            }
            //数量减少且上一帧最近那只已贴着本体：判作回流吞并，鼓一拍
            if (count < lastMiniCount && lastMiniNearDist < 130f) {
                swellPulse = 10;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            lastMiniCount = count;
            lastMiniNearDist = nearestDist;
            float targetLost = MathHelper.Clamp(count * 0.06f, 0f, 0.24f);
            massLost = MathHelper.Lerp(massLost, targetLost, 0.12f);
        }

        //==================== 演出小拍 ====================

        /// <summary>起跳拍：蹬水坑——反向水花 + 涟漪 + 短促蹬水声</summary>
        private void TakeoffBeat(KikasaDomainPlayer domain, float strength) {
            Vector2 hit = new(Projectile.Center.X, domain.LakeWorldY);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f * strength, Pitch = 0.1f, MaxInstances = 3 }, hit);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f * strength, Pitch = -0.2f, MaxInstances = 3 }, hit);
            if (!ViewedOwner) {
                return;
            }
            KikasaDomainDeco.RippleAt(hit, 0.7f * strength);
            KikasaDomainDeco.SplashAt(hit, (int)(4 * strength));
            if (strength > 1.2f) {
                ShakeViewer(1.2f);
            }
        }

        /// <summary>落地拍：压扁 + 宽矮水花 + 分级震屏；跟随小跳与大招共用不同量级</summary>
        private void LandingBeat(KikasaDomainPlayer domain, float rippleScale, int splashCount, float shake) {
            Vector2 hit = new(Projectile.Center.X, domain.LakeWorldY);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 3 }, hit);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.3f, MaxInstances = 3 }, hit);
            if (!ViewedOwner) {
                return;
            }
            KikasaDomainDeco.RippleAt(hit, rippleScale);
            KikasaDomainDeco.RippleAt(hit + new Vector2(52f, 0f), rippleScale * 0.45f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(50f, 0f), rippleScale * 0.45f);
            KikasaDomainDeco.SplashAt(hit, splashCount);
            ShakeViewer(shake);
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

        private void DripFromRim() {
            //轮廓下缘凝珠滴回湖面
            Vector2 rim = Projectile.Bottom + new Vector2(Main.rand.NextFloat(-44f, 44f) * visSx, -Main.rand.NextFloat(8f, 30f));
            PRTLoader.NewParticle<PRT_GhostRainDrop>(rim,
                new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                GelMain * Main.rand.NextFloat(0.4f, 0.55f),
                Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(16, 28), 0f);
        }

        /// <summary>帧动画：地面慢滚 0-1-2-1，蹲底压帧 3，升空帧 4，坠落帧 5；穹丘期定帧求稳</summary>
        private void UpdateFrames() {
            if (State == StateEmerge && (int)StateParam == 0) {
                frameIndex = 0;
                return;
            }
            if (State == StateFollow && (int)StateParam == 1
                || State == StateSlam && (int)StateParam == 0
                || State == StateWaveSlam && (int)StateParam == 0) {
                frameIndex = 3;
                return;
            }
            if (IsAirborne()) {
                frameIndex = Projectile.velocity.Y < 0f ? 4 : 5;
                return;
            }
            if (++frameTick >= 9) {
                frameTick = 0;
                groundFrameStep = (groundFrameStep + 1) % 4;
            }
            int[] cycle = [0, 1, 2, 1];
            frameIndex = cycle[groundFrameStep];
        }

        private void UpdateCrownSpring() {
            //王冠是刚体：挂载点随凝胶起落，弹簧滞后读出"骑在软体上"
            crownDyVel += (0f - crownDy) * 0.3f;
            crownDyVel *= 0.78f;
            crownDy = MathHelper.Clamp(crownDy + crownDyVel, -34f, 34f);
        }

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float CurrentAlpha() {
            if (State == StateEmerge && (int)StateParam == 0) {
                //预兆期从水下缓浮现，不许硬弹出
                return MathHelper.Clamp(StateTimer / 12f, 0f, 1f);
            }
            return 1f;
        }

        /// <summary>uForm：穹丘期全血水，拔起后凝形回半沉呼吸——比克眼更水，它本来就是一团凝胶</summary>
        private float CurrentForm() {
            float steady = 0.52f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.7f + Seed) * 0.05f;
            if (State == StateEmerge) {
                if ((int)StateParam == 0) {
                    return 1f;
                }
                //拔起后 40 帧内凝实成形
                float anneal = MathHelper.Clamp((StateTimer - SwellEnd) / 40f, 0f, 1f);
                return MathHelper.Lerp(1f, steady, SmoothStep01(anneal));
            }
            if (State == StateDissolve) {
                return MathHelper.Clamp(steady + MeltProgress() * 0.45f, 0f, 1f);
            }
            return steady;
        }

        private float CurrentDissolve()
            => State == StateDissolve ? MeltProgress() : 0f;

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        /// <summary>王冠是否该出现在挂载点上（穹丘期藏在丘里，只透金光）</summary>
        private bool CrownMounted
            => !(State == StateEmerge && (int)StateParam == 0);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.KingSlime);
            Texture2D tex = TextureAssets.Npc[NPCID.KingSlime]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.KingSlime];
            Rectangle frame = new(0, frameH * Math.Clamp(frameIndex, 0, Main.npcFrameCount[NPCID.KingSlime] - 1), tex.Width, frameH);

            float alpha = CurrentAlpha();
            SpriteBatch sb = Main.spriteBatch;

            //砸落残影：速度门控，只在真正的高速段亮
            float speed = Projectile.velocity.Length();
            if (alpha > 0.1f && speed > 16f) {
                Vector2 origin = new(frame.Width * 0.5f, frame.Height - 8f);
                for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                    Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                    if (oldCenter == Projectile.Size * 0.5f) {
                        continue;
                    }
                    float fall = 1f - k / (float)Projectile.oldPos.Length;
                    Vector2 oldBottom = oldCenter + new Vector2(0f, Projectile.height * 0.5f);
                    sb.Draw(tex, oldBottom - Main.screenPosition, frame,
                        GelMain * (0.3f * fall * alpha), Projectile.rotation,
                        origin, new Vector2(visSx, visSy) * BodyDrawScale * (0.97f - k * 0.02f), SpriteEffects.None, 0f);
                }
            }

            //本体 + 王冠：血湖材质
            if (alpha > 0.01f) {
                DrawBody(sb, tex, frame, alpha);
            }

            //加色层：穹丘水下血光 / 冠芒 / 落点预告
            DrawGlow(sb, alpha);

            return false;
        }

        /// <summary>身体挤压拉伸以底缘为锚：史莱姆蹲下去是"塌"，不是绕中心缩</summary>
        private void DrawBody(SpriteBatch sb, Texture2D tex, Rectangle frame, float alpha) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 bottomOrigin = new(frame.Width * 0.5f, frame.Height - 8f);
            Vector2 drawPos = Projectile.Bottom - Main.screenPosition;
            float lean = MathHelper.Clamp(Projectile.velocity.X * 0.02f, -0.22f, 0.22f);

            Color color;
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                form.Parameters["uSeed"]?.SetValue(Seed);
                form.Parameters["uForm"]?.SetValue(CurrentForm());
                form.Parameters["uDissolve"]?.SetValue(CurrentDissolve());
                form.Parameters["uScanMode"]?.SetValue(0f);
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
                color = Color.Lerp(Color.White, GelMain, 0.6f) * (alpha * (1f - CurrentDissolve()));
            }

            sb.Draw(tex, drawPos, frame, color, lean, bottomOrigin,
                new Vector2(visSx, visSy) * BodyDrawScale, SpriteEffects.None, 0f);

            //王冠：真身残留物，金属实体读数（uForm 极低），溶解期不化、最后沉没
            if (CrownMounted) {
                Texture2D crown = TextureAssets.Extra[ExtrasID.KingSlimeCrown]?.Value;
                if (crown != null) {
                    Vector2 crownPos;
                    float crownAlpha = alpha;
                    float crownRot = MathHelper.Clamp(Projectile.velocity.X * 0.045f, -0.4f, 0.4f);
                    if (CrownAfloat) {
                        //尾声：浮面→倾沉，湖线以下渐隐
                        KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();
                        float lakeY = domain?.LakeWorldY ?? Projectile.Bottom.Y;
                        crownPos = CrownDissolvePos(lakeY);
                        float sinkT = CrownSinkT();
                        crownRot = sinkT * 0.55f + MathF.Sin(StateTimer * 0.17f + Seed) * 0.06f;
                        crownAlpha = 1f - sinkT * sinkT;
                    }
                    else {
                        //挂载点骑在凝胶顶上，弹簧滞后 + 原版逐帧微调
                        float[] frameLift = [2f, -6f, 2f, 10f, 2f, 0f];
                        float mountLift = BodyDrawHeight * visSy - 26f * visSy - frameLift[Math.Clamp(frameIndex, 0, 5)];
                        crownPos = Projectile.Bottom + new Vector2(Projectile.velocity.X * -1.4f, -mountLift + crownDy);
                    }

                    if (shaderOk) {
                        form.Parameters["uForm"]?.SetValue(0.06f);
                        form.Parameters["uDissolve"]?.SetValue(0f);
                        form.Parameters["uSeed"]?.SetValue(Seed + 4.7f);
                        form.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
                        form.Parameters["uTexel"]?.SetValue(new Vector2(1f / crown.Width, 1f / crown.Height));
                        form.Parameters["uAspect"]?.SetValue(crown.Width / (float)crown.Height);
                        form.CurrentTechnique.Passes[0].Apply();
                        sb.Draw(crown, crownPos - Main.screenPosition, null,
                            new Color(255, 255, 255, (byte)(crownAlpha * 255f)),
                            crownRot, crown.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
                    }
                    else {
                        sb.Draw(crown, crownPos - Main.screenPosition, null,
                            Color.Lerp(Color.White, GelMain, 0.25f) * crownAlpha,
                            crownRot, crown.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
                    }
                }
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
            float lakeY = domain.LakeWorldY;

            if (State == StateEmerge && (int)StateParam == 0) {
                //穹丘水下血光：越隆越亮越宽
                float rise = MathHelper.Clamp((t - OmenEnd + 10f) / (SwellEnd - OmenEnd + 10f), 0f, 1f);
                EnsureBegin();
                Vector2 pos = new(Projectile.Center.X, lakeY + MathHelper.Lerp(40f, 6f, rise));
                float r = 46f + 42f * rise;
                sb.Draw(glow, pos - Main.screenPosition, null, GelBright * (0.34f + 0.2f * rise), 0f,
                    gOrigin, new Vector2(r * 2.8f / glow.Width, r * 0.9f / glow.Height), SpriteEffects.None, 0f);

                //丘顶透出的冠芒：第二次抽提起，金点在血水里呼吸
                if (t > OmenEnd + HeaveLen) {
                    float k = MathHelper.Clamp((t - OmenEnd - HeaveLen) / (float)HeaveLen, 0f, 1f);
                    Vector2 crest = new(Projectile.Center.X, Projectile.Bottom.Y - BodyDrawHeight * visSy * 0.82f);
                    float pulse = 0.24f + 0.14f * MathF.Sin(t * 0.23f + Seed);
                    sb.Draw(glow, crest - Main.screenPosition, null, CrownGold * (pulse * k), 0f,
                        gOrigin, new Vector2(26f * 2f / glow.Width, 18f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //压砸落点预告：悬停与砸落期，落点血光横铺、随迫近增宽
            if (State == StateSlam && (int)StateParam is 2 or 3) {
                EnsureBegin();
                float approach = (int)StateParam == 2
                    ? 0.3f
                    : MathHelper.Clamp(1f - (lakeY - Projectile.Bottom.Y) / 500f, 0.3f, 1f);
                float pulse = 0.3f + 0.18f * MathF.Sin(t * 0.9f);
                Vector2 pos = new(diveTargetX, lakeY + 4f);
                float r = 40f + 70f * approach;
                sb.Draw(glow, pos - Main.screenPosition, null, GelBright * (pulse * approach), 0f,
                    gOrigin, new Vector2(r * 2.6f / glow.Width, r * 0.6f / glow.Height), SpriteEffects.None, 0f);
            }

            //砸落速度光带：拖在身后的血光涂抹
            if (Projectile.velocity.Length() > 16f && alpha > 0.1f) {
                EnsureBegin();
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                sb.Draw(glow, Projectile.Center - dir * 40f - Main.screenPosition, null,
                    GelMain * 0.35f, dir.ToRotation(),
                    gOrigin, new Vector2(90f * 2f / glow.Width, 36f * 2f / glow.Height), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //压砸命中的闷响与溅血（OnHit 只在 owner 端跑）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(24f, 24f),
                    Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(2.8f, 2.8f),
                    GelMain * 0.6f, Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(16, 28), 0.4f);
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠：溶解尾拍或异常移除都留一摊血水
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(36f, 22f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(0.5f, 2.6f)),
                    GelMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.65f, 0.95f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
