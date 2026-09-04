using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power;
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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaSHPC
{
    /// <summary>
    /// 械奴·SHPC（专属条目，HoldUp 魔法炮进不了通用推断）。
    /// 单杆湖水凝成的赛博手炮：血枪吐赛博光——普攻直接生成武器本体的
    /// <see cref="CyberTraceBeamProj"/>（青/电蓝/幻紫三阶轮换），
    /// 二式泼 <see cref="SHPCVoltArcProj"/> 高压电弧，特招冷却就绪时
    /// 冲位猎物上方点起 <see cref="SHPCPlasmaSunProj"/> 等离子残阳。
    /// 强度读沉入原件的传奇等级（SHPCOverride.GetOnDamage 烘焙，ExtraAI 随包补发），
    /// 联机契约与比目鱼同构：owner 裁决转场、弹只在 authority 生成、生命线只有 owner 判
    /// </summary>
    internal class KikasaSHPCServant : ModProjectile, IKikasaArmsServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>描迹光束单发倍率（基伤=原件等级伤害）</summary>
        internal const float TraceDamageMul = 1.5f;

        /// <summary>高压电弧倍率</summary>
        internal const float VoltDamageMul = 2.2f;

        /// <summary>残阳每记倍率（残阳自身 20f 一跳）</summary>
        internal const float SunDamageMul = 0.9f;

        /// <summary>残阳冷却帧数（约 15 秒）</summary>
        internal const int SunCooldownFrames = 900;

        //==================== 烘焙数值 ====================

        private int baseDamage = 9;
        private int legendLevel;

        public int ArmsItemType => SHPCOverride.ID;

        /// <summary>专属单体：强度由原件等级烘焙，不吃编队摊薄</summary>
        public int UnitCount => 1;

        /// <summary>绘制缩放与炮口探出：SHPC 贴图对齐（占位初值）</summary>
        private const float GunDrawScale = 0.9f;
        private const float MuzzleLen = 34f;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateTrace = 2;
        private const int StateVolt = 3;
        private const int StateSunCast = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：保位与通用械奴同构，当前未用</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        private const int OmenFrames = 26;
        private const int RiseEnd = 56;
        private const int FormupFrame = 60;
        private const int EmergeTotal = 76;

        //描迹点射：甩入射击位→锁线→三束点射（三色轮换）→收势
        private const int TraceFormEnd = 12;
        private const int TraceLockEnd = 18;
        private const int TraceShots = 3;
        private const int TracePeriod = 11;
        private const int TraceTotal = 78;

        //高压电弧：压近→蓄压双拍→两道电弧
        private const int VoltFormEnd = 16;
        private const int VoltArcA = 28;
        private const int VoltArcB = 46;
        private const int VoltTotal = 74;

        //残阳压制：冲位到猎物上方→充能→点起残阳→收势
        private const int CastDashEnd = 16;
        private const int CastReleaseFrame = 36;
        private const int CastTotal = 58;

        private const int DissolveFrames = 70;

        //==================== 炮体本地模拟 ====================

        private Vector2 gunPos;
        private Vector2 gunVel;
        private Vector2 gunTarget;
        private float gunRot;
        private float gunSpin;
        private float gunRecoil;
        private bool gunFlip;
        private readonly Vector2[] gunOld = new Vector2[8];
        private readonly float[] gunOldRot = new float[8];
        private bool gunInit;

        //==================== 本地表现量 ====================

        private bool breachDone;
        private int muzzleFlash;
        /// <summary>本轮枪口闪的主题色号（0 青 / 1 电蓝 / 2 幻紫）</summary>
        private int flashTheme;
        private int lastFireTick;
        private bool castReleased;
        private int lastSeenState = -1;
        private int attackCooldown;
        private int attackIndex;
        private int sunCooldown;
        private bool formSnapDone;
        private bool dissolveSplashed;
        private Vector2 castPos;
        private bool castDeclared;

        private Player Owner => Main.player[Projectile.owner];
        private float Seed => Projectile.identity * 0.6173f;

        /// <summary>等离子三阶色（与 CyberTraceBeamProj 主题同源）：加色层用</summary>
        private static readonly Color[] ThemeColors = [
            new(110, 255, 235),
            new(120, 190, 255),
            new(190, 150, 255),
        ];

        //==================== 召唤入口 ====================

        /// <summary>count 不折算编制——传奇沉一件即完整形态，多件只取最高等级件定强度</summary>
        internal static void Summon(Player owner, Vector2 emergeAt, int count) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            Item best = null;
            int bestLv = -1;
            foreach (Item item in owner.GetModPlayer<KikasaVaultPlayer>().Stored) {
                if (item?.IsAir == false && item.type == SHPCOverride.ID) {
                    int lv = SHPCOverride.GetLevel(item);
                    if (lv > bestLv) {
                        bestLv = lv;
                        best = item;
                    }
                }
            }
            int baseDmg = best != null ? SHPCOverride.GetOnDamage(best) : SHPCOverride.GetStartDamage;
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDmg * TraceDamageMul);
            int index = Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 42f), Vector2.Zero,
                ModContent.ProjectileType<KikasaSHPCServant>(), damage, 2f, owner.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.projectile[index].ModProjectile is KikasaSHPCServant gun) {
                gun.baseDamage = baseDmg;
                gun.legendLevel = Math.Max(bestLv, 0);
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

        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(baseDamage);
            writer.Write((byte)Math.Clamp(legendLevel, 0, 255));
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            int dmg = reader.ReadInt32();
            int lv = reader.ReadByte();
            if (dmg > 0) {
                baseDamage = dmg;
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
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * TraceDamageMul);

            if (State != lastSeenState) {
                lastSeenState = State;
                lastFireTick = -1;
                castReleased = false;
                castDeclared = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                }
            }

            if (!gunInit) {
                RebuildGun(domain);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateTrace: UpdateTrace(owner, authority); break;
                case StateVolt: UpdateVolt(owner, authority); break;
                case StateSunCast: UpdateSunCast(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateGun(owner, domain);
            PushGunHistory();
            UpdateAmbient();

            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (sunCooldown > 0) {
                sunCooldown--;
            }
            if (muzzleFlash > 0) {
                muzzleFlash--;
            }
            gunRecoil *= 0.76f;
            float glow = GunAlpha() * 0.35f;
            if (glow > 0.02f) {
                //血炮身 + 赛博青的混色灯
                Lighting.AddLight(gunPos, 0.2f * glow, 0.4f * glow, 0.42f * glow);
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

            if (!breachDone) {
                breachDone = true;
                gunVel = new Vector2(0f, -12.6f);
                gunSpin = 0.3f;
                Projectile.velocity = new Vector2(0f, -3.2f);
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.72f,
                    Pitch = -0.38f,
                    MaxInstances = 3
                }, gunPos);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            Projectile.velocity *= 0.96f;

            if (viewed && t < RiseEnd && t % 3 == 0) {
                Vector2 dropPos = gunPos + new Vector2(
                    Main.rand.NextFloat(-24f, 24f), Main.rand.NextFloat(2f, 14f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2f, 3.4f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(12, 24), 0f);
            }

            //上电拍：一顿之后一声电容嗡鸣——赛博的醒法
            if (!formSnapDone && t >= FormupFrame) {
                formSnapDone = true;
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.42f, Pitch = 0.15f, MaxInstances = 2 }, Projectile.Center);
                gunVel += new Vector2(0f, -1.2f);
                if (viewed) {
                    for (int k = 0; k < 3; k++) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            gunPos + Main.rand.NextVector2Circular(16f, 8f),
                            new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.5f, 1.8f)),
                            BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(10, 18), 0.25f);
                    }
                    ShakeViewer(2f);
                }
            }
            if (t == FormupFrame + 4) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = 0.05f, MaxInstances = 2 }, Projectile.Center);
            }

            if (t >= EmergeTotal) {
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                sunCooldown = SunCooldownFrames / 2;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 1.4f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(22f, 0f), 0.6f);
            KikasaDomainDeco.SplashAt(hit, 8);
            for (int k = 0; k < 12; k++) {
                float angle = -MathHelper.Pi * (0.16f + 0.68f * k / 11f);
                float speed = Main.rand.NextFloat(2.6f, 6f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-14f, 14f), -4f),
                    angle.ToRotationVector2() * speed,
                    BloodMain * Main.rand.NextFloat(0.45f, 0.65f),
                    Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                hit + new Vector2(Main.rand.NextFloat(-18f, 18f), -8f),
                new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.6f)),
                MistBlood * 0.75f, Main.rand.NextFloat(0.55f, 0.8f))
                ?.Configure(Main.rand.Next(50, 80));
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = 0.32f,
                Pitch = -0.75f,
                MaxInstances = 1
            }, hit);
            ShakeViewer(1.5f);
        }

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);

            Vector2 anchor = owner.Center + new Vector2(0f, -28f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildGun(owner.GetModPlayer<KikasaDomainPlayer>());
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
                if (sunCooldown <= 0) {
                    State = StateSunCast;
                }
                else {
                    State = attackIndex % 2 == 1 ? StateTrace : StateVolt;
                }
                Projectile.netUpdate = authority;
            }
        }

        //==================== 描迹点射 ====================

        private static int TraceShotFrame(int k) => TraceLockEnd + 4 + k * TracePeriod;

        private void UpdateTrace(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= TraceLockEnd) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 5f
                : Projectile.Center + gunRot.ToRotationVector2() * 500f;

            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 perp = toT.RotatedBy(MathHelper.PiOver2);
            float strafe = MathF.Sin(t * 0.05f + Seed) * 26f;
            Vector2 anchor = owner.Center + toT * 56f + perp * strafe + new Vector2(0f, -22f);
            Vector2 desired = (anchor - Projectile.Center) * 0.11f;
            if (desired.Length() > 14f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 14f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.18f);

            //锁线一声充能
            if (t == 6) {
                SoundEngine.PlaySound(SoundID.Item15 with {
                    Volume = 0.32f,
                    Pitch = 0.3f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            for (int k = 0; k < TraceShots; k++) {
                if (t == TraceShotFrame(k) && k > lastFireTick) {
                    lastFireTick = k;
                    FireTrace(owner, authority, k);
                }
            }

            if (t >= TraceTotal) {
                EndAttack(authority, 80);
            }
        }

        /// <summary>吐一束描迹光：三色随拍序轮换（attackIndex 移相，束束不同色）</summary>
        private void FireTrace(Player owner, bool authority, int k) {
            Vector2 aimDir = gunRot.ToRotationVector2();
            Vector2 muzzle = MuzzlePos();
            int theme = (attackIndex + k) % 3;
            gunRecoil = 12f;
            gunVel -= aimDir * 1.3f;
            muzzleFlash = 5;
            flashTheme = theme;

            SoundEngine.PlaySound(SoundID.Item91 with {
                Volume = 0.4f,
                Pitch = 0.05f + k * 0.06f,
                MaxInstances = 4
            }, muzzle);
            if (!Main.dedServ) {
                for (int d = 0; d < 4; d++) {
                    Dust dust = Dust.NewDustPerfect(muzzle,
                        DustID.Electric, aimDir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(2f, 5f),
                        120, ThemeColors[theme], 0.9f);
                    dust.noGravity = true;
                }
            }
            if (ViewedOwner) {
                ShakeViewer(0.6f);
            }

            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * TraceDamageMul);
                //光束首帧本地初始化：速度只取方向（内置 14），ai0=主题 ai1=追踪倍率
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), muzzle, aimDir * 14f,
                    ModContent.ProjectileType<CyberTraceBeamProj>(), damage, 2f, Projectile.owner,
                    theme, 1f);
            }
        }

        //==================== 高压电弧 ====================

        private void UpdateVolt(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= VoltFormEnd) {
                EndAttack(authority, 60);
                return;
            }
            Vector2 focus = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 4f
                : Projectile.Center + gunRot.ToRotationVector2() * 300f;
            Vector2 toT = (focus - owner.Center).SafeNormalize(Vector2.UnitX);

            //压近站位：电弧要贴脸放
            Vector2 anchor = focus - toT * 230f + new Vector2(0f, -14f);
            Vector2 desired = (anchor - Projectile.Center) * 0.12f;
            if (desired.Length() > 16f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 16f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.2f);

            //蓄压双拍：电容充能的嗡
            if (t == VoltFormEnd || t == VoltFormEnd + 6) {
                SoundEngine.PlaySound(SoundID.Item15 with {
                    Volume = 0.4f,
                    Pitch = t == VoltFormEnd ? 0.1f : 0.35f,
                    MaxInstances = 2
                }, Projectile.Center);
                gunVel -= gunRot.ToRotationVector2() * 1.1f;
            }

            for (int k = 0; k < 2; k++) {
                int frame = k == 0 ? VoltArcA : VoltArcB;
                if (t == frame && k > lastFireTick) {
                    lastFireTick = k;
                    FireVolt(owner, authority, focus, k);
                }
            }

            if (t >= VoltTotal) {
                EndAttack(authority, 120);
            }
        }

        /// <summary>放一道高压电弧：velocity 即方向（模块契约），整炮后坐</summary>
        private void FireVolt(Player owner, bool authority, Vector2 focus, int k) {
            Vector2 muzzle = MuzzlePos();
            Vector2 dir = (focus - muzzle).SafeNormalize(Vector2.UnitX).RotatedBy((k * 2 - 1) * 0.06f);
            gunRecoil = 18f;
            gunVel -= dir * 2.4f;
            muzzleFlash = 6;
            flashTheme = 1;

            SoundEngine.PlaySound(SoundID.Item94 with {
                Volume = 0.5f,
                Pitch = -0.1f + k * 0.1f,
                MaxInstances = 3
            }, muzzle);
            if (!Main.dedServ) {
                for (int d = 0; d < 6; d++) {
                    Dust dust = Dust.NewDustPerfect(muzzle,
                        DustID.Electric, dir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(3f, 7f),
                        120, ThemeColors[1], 1.1f);
                    dust.noGravity = true;
                }
            }
            if (ViewedOwner) {
                ShakeViewer(1.8f);
            }

            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * VoltDamageMul);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), muzzle + dir * 30f, dir,
                    ModContent.ProjectileType<SHPCVoltArcProj>(), damage, 6f, Projectile.owner);
            }
        }

        //==================== 残阳压制 ====================

        private void UpdateSunCast(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= CastDashEnd) {
                EndAttack(authority, 60);
                return;
            }

            if (!castDeclared) {
                castDeclared = true;
                castPos = target >= 0 ? Main.npc[target].Center : Projectile.Center;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with {
                    Volume = 0.5f,
                    Pitch = -0.1f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            Vector2 anchor = castPos + new Vector2(0f, t <= CastDashEnd ? -170f : -140f);
            float chase = t <= CastDashEnd ? 0.2f : 0.08f;
            Vector2 desired = (anchor - Projectile.Center) * chase;
            if (desired.Length() > 24f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 24f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.25f);

            //充能：电容爬升音
            if (t == CastDashEnd + 6) {
                SoundEngine.PlaySound(SoundID.Item15 with {
                    Volume = 0.42f,
                    Pitch = 0.4f,
                    MaxInstances = 2
                }, Projectile.Center);
            }
            if (!Main.dedServ && t > CastDashEnd && t < CastReleaseFrame && t % 3 == 0) {
                Dust d = Dust.NewDustPerfect(MuzzlePos() + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Electric, new Vector2(0f, 1.6f), 110, ThemeColors[2], 1f);
                d.noGravity = true;
            }

            //点起残阳（节拍闩防重放）
            if (t >= CastReleaseFrame && !castReleased) {
                castReleased = true;
                sunCooldown = SunCooldownFrames;
                gunRecoil = 22f;
                gunVel += new Vector2(0f, -3f);
                muzzleFlash = 7;
                flashTheme = 2;
                SoundEngine.PlaySound(SoundID.Item92 with {
                    Volume = 0.6f,
                    Pitch = -0.3f,
                    MaxInstances = 2
                }, castPos);
                if (ViewedOwner) {
                    ShakeViewer(3f);
                }
                if (authority) {
                    int damage = (int)Owner.GetTotalDamage(DamageClass.Summon).ApplyTo(baseDamage * SunDamageMul);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), castPos, Vector2.Zero,
                        ModContent.ProjectileType<SHPCPlasmaSunProj>(), damage, 2f, Projectile.owner);
                }
            }

            if (t >= CastTotal) {
                EndAttack(authority, 90);
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

            if (lakeAlive && !dissolveSplashed && gunPos.Y >= lakeY) {
                dissolveSplashed = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.55f,
                    Pitch = -0.4f,
                    MaxInstances = 3
                }, gunPos);
                if (ViewedOwner) {
                    Vector2 hit = new(gunPos.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 6);
                    KikasaDomainDeco.RippleAt(hit, 0.9f);
                    ShakeViewer(1f);
                }
            }

            if (!Main.dedServ && GunAlpha() > 0.15f && t % 2 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    gunPos + Main.rand.NextVector2Circular(20f, 10f),
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

        //==================== 炮体推进 ====================

        private void RebuildGun(KikasaDomainPlayer domain) {
            gunInit = true;
            if (State == StateEmerge) {
                gunPos = new Vector2(Projectile.Center.X, domain.LakeWorldY + 26f);
                gunRot = -MathHelper.PiOver2;
            }
            else {
                gunPos = Projectile.Center + new Vector2(0f, -6f);
                gunRot = 0f;
            }
            gunFlip = MathF.Cos(gunRot) < 0f;
            gunVel = Vector2.Zero;
            gunSpin = 0f;
            gunRecoil = 0f;
            gunTarget = gunPos;
            for (int k = 0; k < gunOld.Length; k++) {
                gunOld[k] = gunPos;
                gunOldRot[k] = gunRot;
            }
        }

        private void ChaseGun(float accel, float damp) {
            gunVel = (gunVel + (gunTarget - gunPos) * accel) * damp;
            gunPos += gunVel;
        }

        private float Sway(float speed, float amp)
            => MathF.Sin(Main.GlobalTimeWrappedHourly * speed + Seed) * amp;

        private void FaceGun(Vector2 worldPos, float rate) {
            float want = (worldPos - gunPos).ToRotation();
            gunRot = gunRot.AngleLerp(want, rate);
        }

        private void UpdateGun(Player owner, KikasaDomainPlayer domain) {
            if (!gunInit) {
                return;
            }
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            Vector2 targetPos = target >= 0 ? Main.npc[target].Center : owner.Center;
            bool skipFix = false;

            switch (State) {
                case StateEmerge: {
                    float lakeY = domain.LakeWorldY;
                    if (t < OmenFrames) {
                        gunPos = new Vector2(Projectile.Center.X, lakeY + 26f);
                        gunVel = Vector2.Zero;
                        gunTarget = gunPos;
                        gunRot = -MathHelper.PiOver2;
                        break;
                    }
                    gunTarget = new Vector2(Projectile.Center.X, lakeY - 96f + Sway(2.1f, 9f));
                    int lt = t - OmenFrames;
                    if (lt < 14) {
                        gunVel.Y *= 0.955f;
                        gunVel.X *= 0.98f;
                        gunPos += gunVel;
                        gunRot += gunSpin;
                        gunSpin *= 0.94f;
                    }
                    else {
                        ChaseGun(0.05f, 0.86f);
                        gunRot += gunSpin;
                        gunSpin *= 0.9f;
                        if (MathF.Abs(gunSpin) < 0.05f) {
                            float level = gunPos.X >= Projectile.Center.X ? 0f : MathHelper.Pi;
                            gunRot = gunRot.AngleLerp(level, 0.14f);
                        }
                    }
                    break;
                }
                case StateFollow: {
                    float tGlobal = Main.GlobalTimeWrappedHourly;
                    float phase = tGlobal * 0.62f + Seed;
                    Vector2 slot = Projectile.Center + new Vector2(MathF.Cos(phase) * 30f, MathF.Sin(phase) * 12f - 8f);
                    slot.Y += MathF.Sin(tGlobal * 2.3f + Seed * 2f) * 7f;
                    gunTarget = slot;
                    ChaseGun(0.06f, 0.84f);

                    if (target >= 0) {
                        FaceGun(targetPos, 0.16f);
                    }
                    else if (gunVel.Length() > 2.6f) {
                        gunRot = gunRot.AngleLerp(gunVel.ToRotation(), 0.12f);
                    }
                    else {
                        gunRot = gunRot.AngleLerp(owner.direction > 0 ? 0f : MathHelper.Pi, 0.05f);
                    }
                    break;
                }
                case StateTrace: {
                    Vector2 aimPos = target >= 0
                        ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                        : Projectile.Center + gunRot.ToRotationVector2() * 500f;
                    Vector2 toT = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 slot = Projectile.Center + toT * 26f + new Vector2(0f, Sway(1.8f, 4f));
                    gunTarget = slot;
                    ChaseGun(t < TraceFormEnd ? 0.12f : 0.08f, 0.8f);
                    FaceGun(aimPos, t < TraceLockEnd ? 0.3f : 0.45f);
                    break;
                }
                case StateVolt: {
                    Vector2 aimPos = target >= 0
                        ? Main.npc[target].Center + Main.npc[target].velocity * 4f
                        : Projectile.Center + gunRot.ToRotationVector2() * 300f;
                    Vector2 toT = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 slot = Projectile.Center + toT * 20f + new Vector2(0f, Sway(2f, 3f));
                    gunTarget = slot;
                    ChaseGun(0.13f, 0.78f);
                    FaceGun(aimPos, 0.42f);
                    break;
                }
                case StateSunCast: {
                    Vector2 slot = Projectile.Center + new Vector2(Sway(1.5f, 6f), 4f);
                    gunTarget = slot;
                    ChaseGun(0.1f, 0.8f);
                    FaceGun(castDeclared ? castPos : targetPos, 0.24f);
                    break;
                }
                case StateDissolve: {
                    skipFix = true;
                    gunVel.X *= 0.93f;
                    gunVel.Y = MathF.Min(gunVel.Y + 0.3f, 9.5f);
                    float droop = gunRot + (MathF.Cos(gunRot) >= 0f ? 0.5f : -0.5f);
                    gunRot = gunRot.AngleLerp(droop, 0.02f);
                    gunPos += gunVel;
                    gunTarget = gunPos;
                    break;
                }
            }

            if (!skipFix && Vector2.Distance(gunPos, gunTarget) > 780f) {
                gunPos = gunTarget;
                gunVel = Vector2.Zero;
            }

            float c = MathF.Cos(gunRot);
            if (c > 0.22f) {
                gunFlip = false;
            }
            else if (c < -0.22f) {
                gunFlip = true;
            }
        }

        private void PushGunHistory() {
            for (int k = gunOld.Length - 1; k >= 1; k--) {
                gunOld[k] = gunOld[k - 1];
                gunOldRot[k] = gunOldRot[k - 1];
            }
            gunOld[0] = gunPos;
            gunOldRot[0] = gunRot;
        }

        private void UpdateAmbient() {
            if (Main.dedServ
                || State is not (StateFollow or StateTrace or StateVolt or StateSunCast)) {
                return;
            }
            if (Main.rand.NextBool(16) && GunAlpha() > 0.5f) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    gunPos + new Vector2(Main.rand.NextFloat(-20f, 20f), Main.rand.NextFloat(6f, 12f)),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.1f)),
                    BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.28f, 0.5f))?.Configure(Main.rand.Next(16, 28), 0f);
            }
            //赛博电火花：血水身上偶尔跳一粒电
            if (Main.rand.NextBool(24) && GunAlpha() > 0.5f) {
                Dust d = Dust.NewDustPerfect(gunPos + Main.rand.NextVector2Circular(14f, 8f),
                    DustID.Electric, Main.rand.NextVector2Circular(1.2f, 1.2f), 130,
                    ThemeColors[Main.rand.Next(3)], 0.7f);
                d.noGravity = true;
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

        private Vector2 GunDrawPos()
            => gunPos - gunRot.ToRotationVector2() * gunRecoil;

        private Vector2 MuzzlePos()
            => GunDrawPos() + gunRot.ToRotationVector2() * MuzzleLen;

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float GunAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        private float GunForm() {
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

        private float GunScale() {
            float scale = 1f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 10) {
                scale *= 1f + 0.08f * (1f - (t - OmenFrames) / 10f);
            }
            scale *= 1f - gunRecoil * 0.004f;
            return scale * GunDrawScale;
        }

        /// <summary>残阳充能进度 0~1</summary>
        private float CastCharge() {
            if (State != StateSunCast) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= CastDashEnd || t >= CastReleaseFrame) {
                return 0f;
            }
            return MathHelper.Clamp((t - CastDashEnd) / (float)(CastReleaseFrame - CastDashEnd), 0f, 1f);
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        private SpriteEffects GunFx()
            => gunFlip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        private float FlipRotOffset() => gunFlip ? MathHelper.Pi : 0f;

        private float GunDrawRot()
            => gunRot - gunRecoil * 0.006f * (gunFlip ? -1f : 1f);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!gunInit) {
                return false;
            }
            Main.instance.LoadItem(SHPCOverride.ID);
            Texture2D tex = TextureAssets.Item[SHPCOverride.ID]?.Value;
            if (tex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            DrawDashTrails(sb, tex);
            DrawBody(sb, tex);
            DrawGlow(sb);
            return false;
        }

        private void DrawDashTrails(SpriteBatch sb, Texture2D tex) {
            float trailA = MathHelper.Clamp((gunVel.Length() - 8f) / 10f, 0f, 1f) * GunAlpha();
            if (trailA <= 0.03f) {
                return;
            }
            Vector2 origin = tex.Size() * 0.5f;
            for (int k = gunOld.Length - 1; k >= 1; k--) {
                float fall = 1f - k / (float)gunOld.Length;
                sb.Draw(tex, gunOld[k] - Main.screenPosition, null,
                    BloodMain * (0.26f * fall * trailA), gunOldRot[k] + FlipRotOffset(),
                    origin, GunScale() * (0.96f - k * 0.015f), GunFx(), 0f);
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

            float alpha = GunAlpha();
            if (alpha > 0.01f) {
                float rot = GunDrawRot() + FlipRotOffset();
                Vector2 drawPos = GunDrawPos() - Main.screenPosition;
                float dissolve = DissolveAmt();
                Vector2 origin = tex.Size() * 0.5f;

                if (shaderOk) {
                    float wt = Main.GlobalTimeWrappedHourly * 2.4f + Seed;
                    Vector2 wobOff = new(MathF.Sin(wt) * 1.7f, MathF.Cos(wt * 0.83f) * 2.1f);
                    float wobRot = MathF.Sin(wt * 0.7f) * 0.035f;
                    float envScale = GunScale() * (1.14f + MathF.Sin(wt * 1.6f) * 0.025f);
                    form.Parameters["uSeed"]?.SetValue(Seed + 5.1f);
                    form.Parameters["uForm"]?.SetValue(1f);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(tex, drawPos + wobOff, null,
                        new Color(255, 255, 255, (byte)(alpha * 130f)),
                        rot + wobRot, origin, envScale, GunFx(), 0f);
                }

                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed);
                    form.Parameters["uForm"]?.SetValue(GunForm());
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }
                sb.Draw(tex, drawPos, null, color, rot, origin, GunScale(), GunFx(), 0f);
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

            if (State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                EnsureBegin();
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(42f, 8f, ease));
                float r = 20f + 14f * ease;
                sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * (0.35f * ease), 0f,
                    gOrigin, new Vector2(r * 2.4f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
            }

            //水光扫掠
            if (State is StateFollow or StateTrace or StateVolt or StateSunCast) {
                float p = (Main.GlobalTimeWrappedHourly * 0.42f + Seed * 0.13f) % 1f;
                if (p < 0.34f && GunAlpha() > 0.5f) {
                    EnsureBegin();
                    float k = p / 0.34f;
                    float a = MathF.Sin(k * MathHelper.Pi) * 0.3f * GunAlpha();
                    Vector2 dir = gunRot.ToRotationVector2();
                    float halfLen = MuzzleLen * 0.9f;
                    Vector2 pos = GunDrawPos() + dir * MathHelper.Lerp(-halfLen, halfLen, k);
                    sb.Draw(glow, pos - Main.screenPosition, null, BloodBright * a,
                        gunRot + MathHelper.PiOver2, gOrigin,
                        new Vector2(20f * 2f / glow.Width, 5f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //残阳充能：施放点一圈渐涨的等离子光 + 炮口积能
            float charge = CastCharge();
            if (charge > 0.05f) {
                EnsureBegin();
                Color plasma = ThemeColors[2];
                float ringR = MathHelper.Lerp(24f, 88f, charge);
                sb.Draw(glow, castPos - Main.screenPosition, null, plasma * (0.3f * charge), 0f,
                    gOrigin, new Vector2(ringR * 2f / glow.Width), SpriteEffects.None, 0f);
                sb.Draw(glow, MuzzlePos() - Main.screenPosition, null,
                    ThemeColors[0] * (0.5f * charge), 0f,
                    gOrigin, new Vector2((8f + 12f * charge) * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //炮口闪：当前主题色的能量爆点
            if (muzzleFlash > 0) {
                EnsureBegin();
                float a = muzzleFlash / 4f;
                Vector2 muzzle = MuzzlePos();
                Color theme = ThemeColors[flashTheme % 3];
                sb.Draw(glow, muzzle - Main.screenPosition, null,
                    theme * (0.55f * a), gunRot,
                    gOrigin, new Vector2(30f / glow.Width * 2f, 10f / glow.Height), SpriteEffects.None, 0f);
                sb.Draw(glow, muzzle - Main.screenPosition, null,
                    Color.White with { A = 0 } * (0.25f * a), 0f,
                    gOrigin, new Vector2(12f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !gunInit) {
                return;
            }
            for (int k = 0; k < 5; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    gunPos + Main.rand.NextVector2Circular(18f, 10f),
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
