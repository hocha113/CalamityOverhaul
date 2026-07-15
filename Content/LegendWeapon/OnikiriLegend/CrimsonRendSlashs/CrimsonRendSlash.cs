using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using CSR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer;
using SlashDef = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer.SlashDef;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs
{
    /// <summary>
    /// 绯红裂空斩：按住左键驱动的滚动五段连段控制器（持械手感参照 <see cref="MurasamaLegend.MurasamaProj.MuraSlashDefault"/>）<br/>
    /// 手感契约：<br/>
    /// 1. 每拍开火瞬间捕获鼠标方向，连段全程可转向追敌，姿态每帧跟随鼠标<br/>
    /// 2. 按住循环出刀，轻点只出首拍快斩；松手停排新拍，已挥出的刀光自然收势<br/>
    /// 3. 收势期间再按下从第一拍立刻重启，没有余韵锁死等待<br/>
    /// 4. 拍间隔与命中冷却随近战攻速缩放；伤害类别为无速真近战，避免攻速双重生效<br/>
    /// 5. 连段期间设置玩家持械姿态（heldProj、itemRotation、朝向），角色跟手<br/>
    /// 节拍（60fps，攻速 1）：纵斩下劈 +10 反手上撩 +10 月牙重斩 +13 蓄势重斩 +15 蓄势终结 +24 回到首拍；
    /// 前三拍快斩 easeOut 干脆完成，后两拍高离心率椭圆重斩走蓄势-滞帧-爆发曲线，重击伤害加成回报等待<br/>
    /// 每拍首次命中触发同一套爆点全层演出（强度随拍位递增），拒绝"只有最后一下有反馈"；
    /// 不冻结世界或目标时间，屏幕级只保留短白闪与 Bloom，防眩晕<br/>
    /// ai[0]=初始瞄准角(弧度，远端未同步鼠标前的回退) ai[2]=尺寸倍率
    /// </summary>
    internal class CrimsonRendSlash : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //==== 节拍常量 ====
        private const int BeatCount = 5;
        private const int BurstFadeFrames = 16;
        private const int AfterglowEnd = 46;   //命中余韵层最晚结束帧（相对 lastImpactFrame）
        private const int BaseHitCooldown = 10;
        /// <summary>各拍到下一拍的基准间隔（攻速 1 时），末位为终结后的循环呼吸</summary>
        private static readonly int[] BeatGap = [10, 10, 13, 15, 24];
        /// <summary>快斩四拍收势轻确认参数（白闪强度/火花数/音高/是否命中型白闪）</summary>
        private static readonly (float Flash, int Sparks, float Pitch, bool HitFlash)[] PingTable = [
            (0.02f, 6, 0.50f, false),
            (0.01f, 8, 0.65f, false),
            (0.05f, 10, 0.75f, true),
            (0.06f, 12, 0.85f, true),
        ];

        /// <summary>已挥出的子刀光：出生帧与瞄准角在开火瞬间冻结，视觉与判定随其独立走完生命期</summary>
        private sealed class ActiveSlash
        {
            public SlashDef Def;
            public int Birth;        //绝对 timer 帧
            public int Beat;         //0..4 拍位
            public float Aim;        //该拍开火瞬间的瞄准角
            public bool ImpactDone;  //本拍首次命中爆点已触发
            public bool SnapPlayed;  //重击爆发脆响已播（快斩恒 true）
        }

        /// <summary>停手超过该帧数后再按从第一拍重启，短停续接拍序（节奏点按可走完整连段）</summary>
        private const int ComboResetFrames = 30;

        private readonly List<ActiveSlash> actives = new(8);
        private int timer;
        private int comboIndex;
        private int nextBeatTime;
        private int lastBeatFire;
        private bool scheduling;
        private float sizeMul = 1f;
        private float curAim;
        private int lastImpactFrame = -999;
        private Vector2 lastImpactPos;
        private float lastImpactAim;
        private float lastImpactFlip = 1f;
        private Rectangle[] speedLineRects;
        private float[] speedLineOffsets;

        /// <summary>刃身环境光锚点（与实际命中无关）：最新子刀光刃锋鼓腹沿其瞄准方向的位置，
        /// 供每帧常驻的 Lighting/Bloom 使用；真实命中特效用 <see cref="lastImpactPos"/>（目标实际中心）</summary>
        private Vector2 AmbientAnchor {
            get {
                if (actives.Count > 0) {
                    ActiveSlash a = actives[^1];
                    return Projectile.Center + a.Aim.ToRotationVector2() * (a.Def.OffsetAlongAim + a.Def.HalfX * 0.55f);
                }
                return Projectile.Center + curAim.ToRotationVector2() * 180f * sizeMul;
            }
        }

        /// <summary>
        /// 触发接口：在持有者客户端调用（<c>player.whoAmI == Main.myPlayer</c> 时），
        /// tML 自动完成多人同步；生成后连段由控制器按住循环驱动并跟随玩家移动
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="origin">起手锚点（生成后每帧跟随玩家中心）</param>
        /// <param name="aim">初始瞄准方向（无需归一化，此后每拍重新捕获鼠标方向）</param>
        /// <param name="damage">单段伤害（连段可多次命中，重击拍附带加成）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率（与近战尺寸词缀乘算）</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 origin, Vector2 aim, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_CrimsonRendSlash");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX).ToRotation();
            return Projectile.NewProjectileDirect(source, origin, Vector2.Zero
                , ModContent.ProjectileType<CrimsonRendSlash>(), damage, knockback, player.whoAmI
                , ai0: aimAngle, ai2: scale);
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;   //常态由 UpdateLifetime 刷新，收势完毕主动 Kill
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = BaseHitCooldown;   //随攻速在 FireBeat 内重设
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void Initialize() {
            float itemScale = Item.type == ModContent.ItemType<OnikiriItem>()
                ? MathHelper.Clamp(Owner.GetAdjustedItemScale(Item), 0.5f, 1.5f)
                : 1f;
            sizeMul = (Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f) * itemScale;
            curAim = Projectile.ai[0];
            scheduling = true;
            nextBeatTime = 1;
        }

        /// <summary>五段弧形变奏的确定性美术参数：aim/flip 逐拍传入，Seed 掺入出生帧防止循环重复同噪声</summary>
        private SlashDef BuildBeatDef(int beat, float a, float f, float s) {
            SlashDef d = beat switch {
                //0 纵斩下劈：正面纵切平面（沿瞄准方向纵深压扁为竖长椭圆），自头顶前压至脚下
                0 => new SlashDef {
                    SweepFrames = 4, Life = 26, ErodeStart = 8, ErodeFrames = 14,
                    ColorShiftDelay = 7, ColorShiftFrames = 12, DamageStart = 1, DamageEnd = 9,
                    Mode = 0f, Rot = a + f * 0.15f, Span = 3.60f, Thick = 0.30f,
                    HalfX = 150f * s, HalfY = 208f * s, Flip = f,
                    Opacity = 0.92f, FrontGlow = 2.2f, OffsetAlongAim = 30f * s,
                    TailErode = 0.50f, FlashPower = 0.62f, RazorTailWiden = 0.40f,
                },
                //1 反手上撩：同一平面反向，自脚下撩至头顶收势，覆盖正面 ±100°，更大更立
                1 => new SlashDef {
                    SweepFrames = 3, Life = 26, ErodeStart = 8, ErodeFrames = 14,
                    ColorShiftDelay = 7, ColorShiftFrames = 12, DamageStart = 1, DamageEnd = 8,
                    Mode = 0f, Rot = a - f * 0.10f, Span = 3.55f, Thick = 0.33f,
                    HalfX = 172f * s, HalfY = 238f * s, Flip = -f,
                    Opacity = 0.96f, FrontGlow = 2.4f, OffsetAlongAim = 44f * s,
                    TailErode = 0.45f, FlashPower = 0.68f, RazorTailWiden = 0.40f,
                },
                //2 月牙重斩：满弧重月牙正面自上而下重裂，中段力量拍
                2 => new SlashDef {
                    SweepFrames = 3, Life = 34, ErodeStart = 8, ErodeFrames = 18,
                    ColorShiftDelay = 6, ColorShiftFrames = 14, DamageStart = 1, DamageEnd = 10,
                    Mode = 0f, Rot = a, Span = 3.55f, Thick = 0.36f,
                    HalfX = 245f * s, HalfY = 245f * s, Flip = f,
                    Opacity = 1f, FrontGlow = 2.6f, OffsetAlongAim = 0f,
                    TailErode = 0.42f, FlashPower = 0.60f, RazorTailWiden = 0.55f,
                },
                //3 蓄势重斩：高离心率椭圆冲击形，负偏移贴身；缓推 30% 滞一拍后末 2 帧爆发，
                //  伤害窗对齐爆发（蓄势期无判定）
                3 => new SlashDef {
                    SweepFrames = 8, Life = 30, ErodeStart = 9, ErodeFrames = 16,
                    ColorShiftDelay = 7, ColorShiftFrames = 12, DamageStart = 7, DamageEnd = 12,
                    Mode = 0f, Rot = a - f * 0.35f, Span = 3.45f, Thick = 0.42f,
                    HalfX = 330f * s, HalfY = 195f * s, Flip = f,
                    Opacity = 0.97f, FrontGlow = 2.6f, OffsetAlongAim = -35f * s,
                    TailErode = 0.32f, FlashPower = 0.75f, SweepSnap = 1f, RazorTailWiden = 0.75f,
                },
                //4 蓄势终结：最大最重的镜像椭圆重斩，巨弧把角色罩进挥砍平面、弧尖绕到身后
                _ => new SlashDef {
                    SweepFrames = 9, Life = 56, ErodeStart = 12, ErodeFrames = 30,
                    ColorShiftDelay = 7, ColorShiftFrames = 18, DamageStart = 8, DamageEnd = 14,
                    Mode = 0f, Rot = a + f * 0.20f, Span = 3.35f, Thick = 0.44f,
                    HalfX = 400f * s, HalfY = 230f * s, Flip = -f,
                    Opacity = 1f, FrontGlow = 2.9f, OffsetAlongAim = -60f * s,
                    TailErode = 0.30f, FlashPower = 0.95f, SweepSnap = 1f, RazorTailWiden = 0.85f,
                },
            };
            d.Seed = (beat * 0.191f + timer * 0.037f) % 1f;
            return d;
        }

        private Vector2 CenterOf(ActiveSlash a) => Projectile.Center + a.Aim.ToRotationVector2() * a.Def.OffsetAlongAim;

        //==================== 连段推进 ====================

        public override void AI() {
            Projectile.Center = Owner.Center;
            timer++;

            UpdateCombo();
            UpdateBeatEvents();

            if (!Main.dedServ) {
                SpawnSweepSparks();
                SpawnEdgeSmoke();
            }

            UpdatePose();

            Lighting.AddLight(AmbientAnchor, new Vector3(1.0f, 0.25f, 0.18f));
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.12f, 0.10f));

            PushScreenState();
            UpdateLifetime();
        }

        /// <summary>连段排拍：按住推进，松手停排，收势中再按从第一拍重启（DownLeft 由基类自动同步）</summary>
        private void UpdateCombo() {
            bool canContinue = !Owner.noItems && !Owner.CCed
                && Item.type == ModContent.ItemType<OnikiriItem>();
            bool holding = DownLeft && canContinue;

            //首拍无条件出刀，轻点也有完整反馈
            if (timer == 1) {
                FireBeat();
                scheduling = holding;
                return;
            }

            if (!holding) {
                scheduling = false;
                return;
            }
            if (!scheduling) {
                scheduling = true;
                if (timer - lastBeatFire > ComboResetFrames) {
                    comboIndex = 0;
                }
                nextBeatTime = timer;
            }
            if (timer >= nextBeatTime) {
                FireBeat();
            }
        }

        /// <summary>开火一拍：冻结当前鼠标方向为该刀光的瞄准角，按攻速排下一拍并缩放命中冷却</summary>
        private void FireBeat() {
            float aim = ToMouse.LengthSquared() > 1f ? ToMouseA : Projectile.ai[0];
            curAim = aim;
            float cos = MathF.Cos(aim);
            float flip = MathF.Abs(cos) < 0.05f ? Owner.direction : (cos > 0f ? 1f : -1f);
            int beat = comboIndex;

            actives.Add(new ActiveSlash {
                Def = BuildBeatDef(beat, aim, flip, sizeMul),
                Birth = timer,
                Beat = beat,
                Aim = aim,
                SnapPlayed = beat < 3,
            });
            PlayBeatFireSound(beat);

            float speedFactor = MathHelper.Clamp(1f / Owner.GetWeaponAttackSpeed(Item), 0.5f, 1.6f);
            Projectile.localNPCHitCooldown = Math.Max(5, (int)(BaseHitCooldown * speedFactor));
            comboIndex = (comboIndex + 1) % BeatCount;
            lastBeatFire = timer;
            nextBeatTime = timer + Math.Max(4, (int)MathF.Round(BeatGap[beat] * speedFactor));
        }

        /// <summary>起挥音效：快斩三拍哨声逐段升调（accelerando），重击两拍低音蓄势起手</summary>
        private void PlayBeatFireSound(int beat) {
            (float pitch, float volume) = beat switch {
                0 => (0.20f, 0.60f),
                1 => (0.38f, 0.50f),
                2 => (0.55f, 0.60f),
                3 => (-0.45f, 0.42f),
                _ => (-0.60f, 0.50f),
            };
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = pitch, Volume = volume }, Projectile.Center);
        }

        /// <summary>逐子刀光的时间轴事件：重击爆发脆响（滞帧末 0.75 处，领先首个伤害帧 1 帧的声音先行）、
        /// 快斩收势轻确认、过期剔除</summary>
        private void UpdateBeatEvents() {
            for (int i = actives.Count - 1; i >= 0; i--) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt >= a.Def.Life) {
                    actives.RemoveAt(i);
                    continue;
                }

                if (!a.SnapPlayed && lt == (int)(a.Def.SweepFrames * 0.75f)) {
                    a.SnapPlayed = true;
                    SoundEngine.PlaySound(SoundID.Item71 with {
                        Pitch = a.Beat == 3 ? 0.78f : 0.60f,
                        Volume = a.Beat == 3 ? 0.70f : 0.90f,
                    }, Projectile.Center);
                }

                if (a.Beat < PingTable.Length && lt == a.Def.SweepFrames) {
                    PingBeat(a);
                }
            }
        }

        /// <summary>扫掠完成瞬间的轻确认（白闪/火花/音效），挥空也有：这是刀光美术本身的呼吸，不算打击效果</summary>
        private void PingBeat(ActiveSlash a) {
            (float flash, int sparks, float pitch, bool hitFlash) = PingTable[a.Beat];
            int lt = timer - a.Birth;
            Vector2 pos = CSR.PointAt(in a.Def, CenterOf(a), 0.94f, lt);

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = pitch, Volume = 0.38f }, pos);
            CrimsonImpactFX.PushImpact(pos, flash);

            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 130, 90)
                    , Main.rand.NextFloat(0.35f, 0.65f) * sizeMul)
                    ?.Configure(Main.rand.Next(14, 22), affectedByGravity: false);
            }
            if (hitFlash) {
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos, Vector2.Zero
                    , new Color(255, 200, 180), 1.0f * sizeMul);
            }
        }

        /// <summary>持械姿态：姿态朝向每帧跟随鼠标（刀光角度仍按拍冻结），保持使用状态防止换用其他物品</summary>
        private void UpdatePose() {
            if (!scheduling) {
                return;   //收势期释放角色姿态
            }
            if (ToMouse.LengthSquared() > 1f) {
                curAim = ToMouseA;
            }
            SetHeld();
            float cos = MathF.Cos(curAim);
            if (MathF.Abs(cos) >= 0.05f) {
                Owner.ChangeDir(cos > 0f ? 1 : -1);
            }
            Owner.itemRotation = (curAim.ToRotationVector2() * Owner.direction).ToRotation();
            Owner.itemTime = Owner.itemAnimation = 2;
        }

        /// <summary>存活契约：排拍中或有子刀光时续命；全部收势且命中余韵播完即 Kill，
        /// 期间再按下由 <see cref="UpdateCombo"/> 吸收重启，不经过物品使用</summary>
        private void UpdateLifetime() {
            bool visualsAlive = actives.Count > 0
                || (lastImpactFrame >= 0 && timer - lastImpactFrame < AfterglowEnd);
            if (scheduling || visualsAlive) {
                Projectile.timeLeft = 30;
                return;
            }
            Projectile.Kill();
        }

        /// <summary>每拍首次命中共用的爆点全层演出（白闪 + 粒子层 + 加色爆点绘制），
        /// 强度按拍位 power(0..1) 缩放；不冻结世界或目标时间，命中确认交给音效/粒子/白闪本身</summary>
        private void TriggerImpactBurst(Vector2 pos, float power, float aim, float flip) {
            lastImpactFrame = timer;
            lastImpactPos = pos;
            lastImpactAim = aim;
            lastImpactFlip = flip;

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.5f - power * 0.2f, Volume = 0.5f + power * 0.4f }, pos);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.6f - power * 0.1f, Volume = 0.2f + power * 0.25f }, pos);

            CrimsonImpactFX.PushImpact(pos, 0.02f + power * 0.01f);

            if (Main.dedServ) {
                return;
            }

            Vector2 aimDir = aim.ToRotationVector2();

            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos, Vector2.Zero
                , new Color(255, 225, 205), (0.75f + power * 0.8f) * sizeMul);
            int satellites = 1 + (int)(power * 2f);
            for (int i = 0; i < satellites; i++) {
                Vector2 off = Main.rand.NextVector2Circular(24f, 24f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos + off, off * 0.05f
                    , new Color(255, 140, 110), Main.rand.NextFloat(0.5f, 0.75f) * sizeMul);
            }

            int mainSparks = 8 + (int)(power * 14f);
            for (int i = 0; i < mainSparks; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.78) * Main.rand.NextFloat(5f, 12f + power * 10f) * sizeMul;
                Color c = Main.rand.NextBool(3) ? new Color(255, 236, 210) : new Color(255, 92, 58);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, c
                    , Main.rand.NextFloat(0.45f, 0.7f + power * 0.4f) * sizeMul)
                    ?.Configure(Main.rand.Next(18, 30 + (int)(power * 12f)), affectedByGravity: true);
            }
            int backSparks = 2 + (int)(power * 5f);
            for (int i = 0; i < backSparks; i++) {
                Vector2 vel = (-aimDir).RotatedByRandom(1.1) * Main.rand.NextFloat(3f, 8f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 70, 46)
                    , Main.rand.NextFloat(0.35f, 0.6f) * sizeMul)
                    ?.Configure(Main.rand.Next(16, 26), affectedByGravity: false);
            }
        }

        /// <summary>屏幕级演出包络：仅 Bloom + 命中脉冲（白闪由节拍触发）；
        /// 排拍中恒亮，收势期随最后一道子刀光余寿衰减</summary>
        private void PushScreenState() {
            float envelope = scheduling ? 1f : 0f;
            if (!scheduling) {
                for (int i = 0; i < actives.Count; i++) {
                    float remain = (actives[i].Def.Life - (timer - actives[i].Birth)) / 14f;
                    envelope = MathF.Max(envelope, MathHelper.Clamp(remain, 0f, 1f));
                }
            }
            float bloom = 0.28f * envelope;
            if (lastImpactFrame >= 0) {
                float bp = MathHelper.Clamp((timer - lastImpactFrame) / (float)BurstFadeFrames, 0f, 1f);
                bloom += 0.38f * (1f - bp) * (1f - bp);
            }
            CrimsonImpactFX.PushAmbience(AmbientAnchor, MathF.Max(bloom, 0f));
        }

        /// <summary>各扫开中的刀光前缘火花：喷量随本帧扫掠增量走，
        /// 蓄势缓推期零星细屑，滞帧近乎无声，爆发帧集中迸发（快慢刀的粒子语言）</summary>
        private void SpawnSweepSparks() {
            for (int i = 0; i < actives.Count; i++) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt < 0 || lt > a.Def.SweepFrames + 1) {
                    continue;
                }
                float delta = CSR.Sweep(in a.Def, lt) - (lt > 0 ? CSR.Sweep(in a.Def, lt - 1) : 0f);
                int count = delta > 0.20f ? 5 : delta > 0.015f ? 2 : lt % 2 == 0 ? 1 : 0;
                if (count == 0) {
                    continue;
                }
                float speedMul = delta > 0.20f ? 1.5f : 1f;

                Vector2 center = CenterOf(a);
                float edgeU = MathHelper.Clamp(CSR.Sweep(in a.Def, lt) * 1.05f, 0.06f, 0.94f);
                Vector2 pos = CSR.PointAt(in a.Def, center, edgeU, lt);
                Vector2 tangent = (CSR.PointAt(in a.Def, center, MathHelper.Clamp(edgeU + 0.03f, 0f, 1f), lt) - pos)
                    .SafeNormalize(a.Aim.ToRotationVector2());

                for (int k = 0; k < count; k++) {
                    Vector2 vel = tangent * Main.rand.NextFloat(4f, 11f) * speedMul + Main.rand.NextVector2Circular(1.2f, 1.2f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 120, 80)
                        , Main.rand.NextFloat(0.3f, 0.6f) * sizeMul)
                        ?.Configure(Main.rand.Next(10, 18), affectedByGravity: false);
                }
            }
        }

        /// <summary>终结拍侵蚀期沿外缘生成细碎烟屑，后期停喷</summary>
        private void SpawnEdgeSmoke() {
            if (timer % 2 != 0) {
                return;
            }
            for (int i = 0; i < actives.Count; i++) {
                ActiveSlash a = actives[i];
                if (a.Beat != BeatCount - 1) {
                    continue;
                }
                int lt = timer - a.Birth;
                if (lt <= a.Def.ErodeStart) {
                    continue;
                }
                float erode = CSR.Erode(in a.Def, lt);
                if (erode > 0.78f) {
                    continue;
                }
                Vector2 finCenter = CenterOf(a);
                for (int k = 0; k < 2; k++) {
                    float uc = Main.rand.NextFloat(0.12f, 0.96f);
                    Vector2 mid = CSR.PointAt(in a.Def, finCenter, uc, lt);
                    Vector2 dir = (mid - Projectile.Center).SafeNormalize(a.Aim.ToRotationVector2());
                    Vector2 pos = mid + dir * a.Def.HalfX * 0.06f;
                    Vector2 vel = dir * Main.rand.NextFloat(0.3f, 1.1f) + Main.rand.NextVector2Circular(0.35f, 0.35f);

                    PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel
                        , Color.White, Main.rand.NextFloat(0.055f, 0.105f) * sizeMul)
                        ?.Configure(Main.rand.Next(16, 26)
                            , new Color(150, 26, 34), new Color(46, 16, 24)
                            , Main.rand.NextFloat(0.01f, 0.024f));
                }
            }
        }

        //==================== 判定 ====================

        /// <summary>当前处于伤害窗口内的子刀光（同帧多窗时取最新一拍），供命中回调定位拍位</summary>
        private ActiveSlash FindDamagingSlash() {
            for (int i = actives.Count - 1; i >= 0; i--) {
                int lt = timer - actives[i].Birth;
                if (lt >= actives[i].Def.DamageStart && lt <= actives[i].Def.DamageEnd) {
                    return actives[i];
                }
            }
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            for (int i = 0; i < actives.Count; i++) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt < a.Def.DamageStart || lt > a.Def.DamageEnd) {
                    continue;
                }
                float sweepU = MathHelper.Clamp(CSR.Sweep(in a.Def, lt) * 1.05f, 0f, 1f);
                Vector2 center = CenterOf(a);

                //弧/椭圆：折线采样
                const int samples = 15;
                Vector2 prev = Vector2.Zero;
                bool hasPrev = false;
                float thickWorld = a.Def.Thick * a.Def.HalfX;
                for (int k = 0; k < samples; k++) {
                    float uc = 0.05f + 0.90f * (k / (float)(samples - 1));
                    if (uc > sweepU) {
                        break;
                    }
                    Vector2 mid = CSR.PointAt(in a.Def, center, uc, lt);
                    if (hasPrev) {
                        float cp = 0f;
                        if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                            , prev, mid, MathF.Max(28f, thickWorld * 0.8f), ref cp)) {
                            return true;
                        }
                    }
                    prev = mid;
                    hasPrev = true;
                }
            }
            return false;
        }

        /// <summary>重击拍伤害加成：蓄势期间无判定的等待换成回报（快斩 ×1，重斩 ×1.3，终结 ×1.6）</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            ActiveSlash a = FindDamagingSlash();
            if (a != null && a.Beat >= 3) {
                modifiers.SourceDamage *= a.Beat == BeatCount - 1 ? 1.6f : 1.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.3f, Volume = 0.75f }, target.Center);

            //每拍首次命中触发爆点全层演出，强度按拍位递增，拒绝"只有最后一下有反馈"
            ActiveSlash a = FindDamagingSlash();
            if (a != null && !a.ImpactDone) {
                a.ImpactDone = true;
                TriggerImpactBurst(target.Center, (a.Beat + 1) / (float)BeatCount, a.Aim, a.Def.Flip);
            }

            if (Main.dedServ) {
                return;
            }
            Vector2 aimDir = (a?.Aim ?? curAim).ToRotationVector2();
            for (int i = 0; i < 8; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.65) * Main.rand.NextFloat(4f, 12f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(target.Center, vel, new Color(255, 96, 60)
                    , Main.rand.NextFloat(0.4f, 0.8f))
                    ?.Configure(Main.rand.Next(16, 28), affectedByGravity: true);
            }
        }

        //==================== 绘制 ====================
        //全部刀光 → EndEntityDraw 弹幕扩展层（覆盖实体）；
        //玩家身后分层机制（ICrimsonFarDrawable/FarDim）保留在渲染器中备用，本连段不使用

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || actives.Count == 0 && lastImpactFrame < 0) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (actives.Count > 0 && CSR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                for (int i = 0; i < actives.Count; i++) {
                    ActiveSlash a = actives[i];
                    int lt = timer - a.Birth;
                    if (lt < 0 || lt >= a.Def.Life) {
                        continue;
                    }
                    CSR.DrawThreeLayers(device, fx, in a.Def, CenterOf(a), lt, 0f);
                }
                CSR.EndDraw(device, pb, pr, pd);
            }

            DrawAdditiveLayers();
            DrawCollapseCore();
        }

        /// <summary>命中爆点 + 余韵光球，自管加色批次</summary>
        private void DrawAdditiveLayers() {
            bool burstActive = lastImpactFrame >= 0 && timer - lastImpactFrame < BurstFadeFrames;
            bool afterglowActive = lastImpactFrame >= 0 && timer - lastImpactFrame is >= 26 and < AfterglowEnd;
            if (!burstActive && !afterglowActive) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (burstActive) {
                DrawImpactBurst(sb);
            }

            //余韵：暗紫红光球内爆收束，仅在下一拍命中前完整播放
            if (afterglowActive && OnikiriAssets.StarFlare01?.Value is Texture2D orb) {
                float t = (timer - lastImpactFrame - 26) / 20f;
                float oA = MathF.Sin(t * MathF.PI) * 0.42f;
                float oS = MathHelper.Lerp(0.9f, 0.18f, CSR.EaseOutCubic(t)) * sizeMul;
                Color oc = Color.Lerp(new Color(210, 70, 130), new Color(70, 24, 66), t);
                sb.Draw(orb, lastImpactPos - Main.screenPosition, null, oc * oA
                    , t * 2.4f, orb.Size() * 0.5f, oS, SpriteEffects.None, 0);
            }

            sb.End();
        }

        /// <summary>命中爆点全 layer：星爆核心/放射尖刺/十字闪/扩散环/撕裂形/速度线</summary>
        private void DrawImpactBurst(SpriteBatch sb) {
            float bt = MathHelper.Clamp(timer - lastImpactFrame, 0f, BurstFadeFrames);
            float bp = bt / BurstFadeFrames;
            if (bp >= 1f) {
                return;
            }

            Vector2 impact = lastImpactPos - Main.screenPosition;
            Vector2 aimDir = lastImpactAim.ToRotationVector2();
            float inv = 1f - bp;
            float easeOut = 1f - MathF.Pow(inv, 3f);
            float seedRot = Projectile.whoAmI * 1.37f;

            //白热核心：峰值收紧到 0.7，避免整块纯白糊住刀光笔触细节，随后急剧收缩
            if (OnikiriAssets.StarFlare02?.Value is Texture2D flare) {
                float coreA = MathF.Pow(inv, 2.0f) * 0.70f;
                float coreS = (0.85f + easeOut * 0.65f) * sizeMul;
                sb.Draw(flare, impact, null, new Color(255, 244, 232) * coreA, seedRot
                    , flare.Size() * 0.5f, coreS, SpriteEffects.None, 0);
                sb.Draw(flare, impact, null, new Color(255, 120, 80) * (coreA * 0.5f), -seedRot * 0.6f
                    , flare.Size() * 0.5f, coreS * 1.3f, SpriteEffects.None, 0);
            }

            //放射尖刺
            if (OnikiriAssets.RayBurst01?.Value is Texture2D rays) {
                float rayA = MathF.Pow(inv, 1.8f) * 0.78f;
                float rayS = (1.1f + easeOut * 1.0f) * sizeMul;
                sb.Draw(rays, impact, null, new Color(255, 190, 160) * rayA, seedRot * 0.4f
                    , rays.Size() * 0.5f, rayS, SpriteEffects.None, 0);
            }

            //十字长闪沿命中拍瞄准方向
            if (OnikiriAssets.RayCross01?.Value is Texture2D cross) {
                float cA = MathF.Pow(inv, 2.4f) * 0.82f;
                sb.Draw(cross, impact, null, new Color(255, 230, 215) * cA, lastImpactAim
                    , cross.Size() * 0.5f, new Vector2(2.2f, 1.0f) * easeOut * sizeMul, SpriteEffects.None, 0);
            }

            //扩散环
            if (OnikiriAssets.Ring01?.Value is Texture2D ring) {
                float ringS = (0.4f + easeOut * 2.2f) * sizeMul;
                float ringA = MathF.Pow(inv, 2.5f) * 0.6f;
                sb.Draw(ring, impact, null, new Color(255, 90, 60) * ringA, 0f
                    , ring.Size() * 0.5f, ringS, SpriteEffects.None, 0);
            }

            //手绘撕裂形：沿瞄准方向一大一小，短命
            if (bt < 9f && OnikiriAssets.TearSpread01?.Value is Texture2D tear) {
                float tA = MathF.Pow(1f - bt / 9f, 1.8f) * 0.85f;
                sb.Draw(tear, impact, null, new Color(255, 150, 120) * tA, lastImpactAim
                    , tear.Size() * 0.5f, (1.5f + easeOut * 0.55f) * sizeMul, SpriteEffects.None, 0);
                sb.Draw(tear, impact, null, new Color(255, 60, 40) * (tA * 0.75f), lastImpactAim + 0.35f * lastImpactFlip
                    , tear.Size() * 0.5f, (1.0f + easeOut * 0.4f) * sizeMul
                    , SpriteEffects.FlipVertically, 0);
            }

            //锯齿冲击形垫底
            //if (bt < 7f && OnikiriAssets.HitJagged01?.Value is Texture2D jag) {
            //    float jA = MathF.Pow(1f - bt / 7f, 2f) * 0.5f;
            //    sb.Draw(jag, impact, null, new Color(255, 80, 55) * jA, lastImpactAim + MathHelper.Pi
            //        , jag.Size() * 0.5f, (1.8f + easeOut * 0.6f) * sizeMul, SpriteEffects.None, 0);
            //}

            //速度线：随机截条从冲击点向后扫出
            if (OnikiriAssets.SpeedLines01?.Value is Texture2D lines) {
                EnsureSpeedLineRects();
                float lA = MathF.Pow(inv, 1.6f) * 0.5f;
                for (int i = 0; i < speedLineRects.Length; i++) {
                    Rectangle src = speedLineRects[i];
                    float off = speedLineOffsets[i];
                    Vector2 pos = impact - aimDir * (40f + off * 70f + easeOut * 40f) * sizeMul
                        + aimDir.RotatedBy(MathHelper.PiOver2) * (off - 0.5f) * 110f * sizeMul;
                    sb.Draw(lines, pos, src, new Color(255, 170, 140) * lA, lastImpactAim
                        , src.Size() * 0.5f, new Vector2(0.40f + easeOut * 0.30f, 0.42f) * sizeMul
                        , SpriteEffects.None, 0);
                }
            }
        }

        private void EnsureSpeedLineRects() {
            if (speedLineRects != null) {
                return;
            }
            speedLineRects = new Rectangle[3];
            speedLineOffsets = new float[3];
            for (int i = 0; i < 3; i++) {
                speedLineRects[i] = new Rectangle(0, Main.rand.Next(0, 1024 - 96), 1024, 96);
                speedLineOffsets[i] = Main.rand.NextFloat();
            }
        }

        /// <summary>负片收缩：爆闪第2~8帧，暗核压在加色星爆之上，只留红边<br/>
        /// 注意：AlphaBlend 压暗必须用 alpha 通道承载形状的贴图（SmokeSheet01），
        /// 黑底不透明的亮度型贴图会把整个 quad 糊成暗色方框</summary>
        private void DrawCollapseCore() {
            float bt = timer - lastImpactFrame;
            if (lastImpactFrame < 0 || bt < 2f || bt > 8f) {
                return;
            }
            Texture2D cloud = OnikiriAssets.SmokeSheet01?.Value;
            if (cloud == null) {
                return;
            }

            float t = (bt - 2f) / 6f;   //0..1
            //512px 帧：峰值 ~0.36 倍 ≈ 185px 暗核，收缩至 ~60px
            float coreS = MathHelper.Lerp(0.36f, 0.12f, t * t) * sizeMul;
            float coreA = MathF.Sin(t * MathF.PI) * 0.78f;
            Rectangle frame = new(Projectile.whoAmI % 2 * 512, Projectile.whoAmI / 2 % 2 * 512, 512, 512);

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(cloud, lastImpactPos - Main.screenPosition, frame
                , new Color(16, 4, 9) * coreA, Projectile.whoAmI * 1.37f
                , frame.Size() * 0.5f, coreS, SpriteEffects.None, 0);
            sb.End();
        }
    }
}
