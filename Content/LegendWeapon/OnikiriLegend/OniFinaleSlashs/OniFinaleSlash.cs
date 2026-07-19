using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs
{
    /// <summary>
    /// 终之太刀主控：整场演出的时间轴导演，自身无判定。<br/>
    /// 分镜（60fps，约 2.8 秒）：<br/>
    /// 起手(0~16) 时停挂起、暗场浸染 → 乱舞(16~90) 立体环斩为主体、激光直痕穿插蓄积，
    /// 节奏逐段收紧 → 死寂(90~112) 全部收声、暗场压到最深、细线无声出现 →
    /// 纳刀(112) 负片闪、世界裂开、直痕连锁引爆、伤害结算、时停解除 → 收势(112~165) 画面回落。<br/>
    /// 时停走 <see cref="CWRWorld.TimeFrozenTick"/>（村正次元斩同款）：NPC/敌对弹幕冻结、
    /// 玩家自由、我方弹幕照常结算；每帧刷新、自带衰减兜底，主控意外死亡下一帧世界自动解冻。<br/>
    /// ai[0]=瞄准角(弧度，决定终斩刀线与碎晶流向) ai[1]=尺寸倍率；伤害经 damage 传入，
    /// 环斩 45% / 直痕 35% / 终斩 400% 逐类派生
    /// </summary>
    internal class OniFinaleSlash : ModProjectile, IOverlayDrawable, IOniBladeOccupant
    {
        public override string Texture => CWRConstant.Placeholder;

        //==== 时间轴常量 ====
        /// <summary>乱舞起点</summary>
        public const int FlurryStart = 16;
        /// <summary>死寂起点（最后一道直痕之后）</summary>
        public const int SilenceStart = 90;
        /// <summary>终斩细线出现帧</summary>
        public const int CutSpawnFrame = 94;
        /// <summary>纳刀引爆帧 = 解冻帧</summary>
        public const int DetonateFrame = CutSpawnFrame + OniFinaleCut.HoldFrames;
        /// <summary>演出总时长</summary>
        public const int TotalDuration = 165;

        /// <summary>乱舞环斩排拍：(帧, 滚转偏移, 扫掠镜像)，节奏 8→6→4 帧逐段收紧</summary>
        private static readonly (int Frame, float RollOff, int Flip)[] RingBeats = [
            (16,  0.40f,  1), (24, -0.92f, -1), (32,  1.80f,  1), (40, -2.40f, -1),
            (46,  0.15f,  1), (52,  2.90f, -1), (58, -1.30f,  1), (64,  0.70f, -1),
            (68, -2.00f,  1), (72,  1.10f, -1), (76, -0.50f,  1), (80,  2.30f, -1),
            (84, -1.70f,  1),
        ];

        /// <summary>直痕排拍：(帧, 相对瞄准角偏移)，前疏后密，越接近纳刀越急</summary>
        private static readonly (int Frame, float AngleOff)[] ScarBeats = [
            (38,  1.35f), (48, -0.52f), (50,  0.55f), (60, -1.15f), (70,  0.22f),
            (74, -0.88f), (78,  1.55f), (82, -0.30f), (86,  0.75f), (88, -1.60f),
        ];

        /// <summary>碎晶流向偏置（弧度）：直痕引爆时碎片顺终斩刀线漂移，仅演出期有效</summary>
        internal static float ShatterFlowAngle;
        /// <summary>演出进行中（主控存活），直痕据此决定碎晶是否吃流向偏置</summary>
        internal static bool ShatterFlowActive;

        private int timer;

        private float Aim => Projectile.ai[0];
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;
        private Player Owner => Main.player[Projectile.owner];

        //====残心静立与纳刀(纯视觉,软占刀权)====
        /// <summary>纳刀一挑时长,与引爆帧同步起手</summary>
        private const int NotoFlickFrames = 6;
        /// <summary>纳刀后持刀淡出</summary>
        private const int NotoFadeFrames = 12;
        private readonly OniBladePose bladePose = new();

        /// <summary>开场短促硬占:清掉在场连段刀光,给演出一个干净的起手;之后普攻自由(不锁)</summary>
        bool IOniBladeOccupant.HardOccupiesBlade => timer <= 10;

        /// <summary>纳刀一挑的签名拍软保留:长残心照旧可随时接管,只护刀入鞘与世界裂开压在同一拍的那一挑</summary>
        bool IOniBladeOccupant.ReservesBlade => timer > DetonateFrame && timer <= DetonateFrame + NotoFlickFrames;

        /// <summary>
        /// 触发接口（调试入口）：在持有者客户端调用（<c>player.whoAmI == Main.myPlayer</c> 时），
        /// tML 自动完成多人同步；整场演出由主控自驱，调用方无需后续干预
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="focus">演出焦点（世界坐标，乱舞围绕此处、终斩刀线过此点）</param>
        /// <param name="aim">瞄准方向（无需归一化，决定终斩刀线角度）</param>
        /// <param name="damage">基准伤害（环斩 45%/直痕 35%/终斩 400% 派生）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 focus, Vector2 aim, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniFinaleSlash");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX).ToRotation();
            return Projectile.NewProjectileDirect(source, focus, Vector2.Zero
                , ModContent.ProjectileType<OniFinaleSlash>(), damage, knockback, player.whoAmI
                , ai0: aimAngle, ai1: scale);
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;   //主控无判定，伤害全在子斩体
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            timer++;

            ShatterFlowAngle = Aim;
            ShatterFlowActive = true;

            //时停：纳刀帧前每帧刷新，之后停止——tick 自然衰减，世界恰在终斩落下时苏醒
            if (timer <= DetonateFrame) {
                CWRWorld.TimeFrozenTick = 2;
            }

            PushDimEnvelope();

            //暗场里的刀光辉光：复用绯红 Bloom 管线（本效果权重 1.09 先压暗，
            //1.10 的 Bloom 提取在暗场上只剩刀光，光圈恰好圈住演出主体）
            if (timer >= FlurryStart && timer <= DetonateFrame + 10) {
                CrimsonImpactFX.PushAmbience(Projectile.Center, 0.24f);
            }

            PlayTimelineSounds();
            UpdateStandPose();

            if (Projectile.owner == Main.myPlayer) {
                RunSpawnTimeline();
            }
        }

        /// <summary>
        /// 演出期残心静立(纯视觉,不锁操控)：世界被劈开的整场,持刀人立定屏息;
        /// 纳刀一挑与引爆帧同帧——刀入鞘,世界才裂。<br/>
        /// 软占刀权:玩家重新挥连段或施放技能时立刻放手,行动自由完全保留
        /// </summary>
        private void UpdateStandPose() {
            bladePose.Update();
            if (!Owner.active || Owner.dead) {
                return;
            }
            if (timer > DetonateFrame + NotoFlickFrames + NotoFadeFrames
                || OniBladeOccupancy.ComboClaims(Owner)
                || OniBladeOccupancy.AnyHardOccupant(Owner, Projectile)) {
                bladePose.Opacity = 0f;
                return;
            }

            int facing = MathF.Cos(Aim) >= 0f ? 1 : -1;
            //残心持刀位:刀锋斜垂身前
            float standRot = Aim + facing * 0.72f;
            if (timer <= DetonateFrame) {
                bladePose.Rotation = standRot + MathF.Sin(timer * 0.045f) * 0.035f;   //屏息的呼吸
                bladePose.Opacity = MathHelper.Clamp(timer / 8f, 0f, 1f);
            }
            else if (timer <= DetonateFrame + NotoFlickFrames) {
                //纳刀:与引爆同帧,一挑入鞘
                float t = (timer - DetonateFrame) / (float)NotoFlickFrames;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                bladePose.Rotation = OniBladePose.LerpAngle(standRot, Aim - facing * 1.05f, ease);
                bladePose.Opacity = 1f;
                if (timer - DetonateFrame <= 3) {
                    bladePose.PushSmear(0.8f);
                }
            }
            else {
                bladePose.Opacity = 1f - (timer - DetonateFrame - NotoFlickFrames) / (float)NotoFadeFrames;
            }
            bladePose.ApplyPose(Owner, Projectile);
        }

        /// <summary>遮挡层：残心静立/纳刀的实体刀</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            bladePose.Draw(spriteBatch, Owner);
        }

        /// <summary>暗场包络：起手浸入 → 乱舞恒定 → 死寂压到最深 → 纳刀后停推自然回落</summary>
        private void PushDimEnvelope() {
            float dim;
            if (timer < FlurryStart) {
                dim = 0.62f * timer / FlurryStart;
            }
            else if (timer < SilenceStart) {
                dim = 0.62f;
            }
            else if (timer <= DetonateFrame) {
                dim = MathHelper.Lerp(0.62f, 0.82f
                    , (timer - SilenceStart) / (float)(DetonateFrame - SilenceStart));
            }
            else {
                return;
            }
            OniFinaleFX.PushDim(Projectile.Center, dim);
        }

        /// <summary>主控级音效：开幕低鸣与纳刀前的吸气，其余节拍音交给各斩体自播</summary>
        private void PlayTimelineSounds() {
            if (timer == 1) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.80f, Volume = 0.60f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.90f, Volume = 0.30f }, Projectile.Center);
            }
            //死寂中一丝极轻的高频吸气，暗示屏息
            if (timer == DetonateFrame - 6) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.95f, Volume = 0.22f }, Projectile.Center);
            }
        }

        /// <summary>排拍生成（仅持有者客户端，tML 同步到各端）</summary>
        private void RunSpawnTimeline() {
            int ringDamage = (int)(Projectile.damage * 0.45f);
            int scarDamage = (int)(Projectile.damage * 0.35f);

            for (int i = 0; i < RingBeats.Length; i++) {
                if (timer != RingBeats[i].Frame) {
                    continue;
                }
                float escalate = i / (float)(RingBeats.Length - 1) * 0.85f;
                Vector2 center = Projectile.Center + Main.rand.NextVector2Circular(70f, 55f);
                float roll = Aim + RingBeats[i].RollOff + Main.rand.NextFloat(-0.15f, 0.15f);
                OniFinaleRing.Fire(Owner, center, roll, escalate, RingBeats[i].Flip
                    , ringDamage, Projectile.knockBack, SizeMul * (1f + 0.18f * escalate) * 1.12f
                    , Projectile.GetSource_FromAI());
            }

            for (int i = 0; i < ScarBeats.Length; i++) {
                if (timer != ScarBeats[i].Frame) {
                    continue;
                }
                Vector2 center = Projectile.Center + Main.rand.NextVector2Circular(170f, 130f);
                float angle = Aim + ScarBeats[i].AngleOff + Main.rand.NextFloat(-0.08f, 0.08f);
                //引爆延迟对齐纳刀帧，i%3 错帧让刀痕网连锁碎裂而非同帧齐爆
                int detonateDelay = DetonateFrame - ScarBeats[i].Frame + i % 3;
                OniFinaleScar.Fire(Owner, center, angle, detonateDelay
                    , scarDamage, Projectile.knockBack * 0.5f, SizeMul, Projectile.GetSource_FromAI());
            }

            if (timer == CutSpawnFrame) {
                OniFinaleCut.Fire(Owner, Projectile.Center, Aim
                    , (int)(Projectile.damage * 4f), Projectile.knockBack * 2f, SizeMul
                    , Projectile.GetSource_FromAI());
            }
        }

        public override void OnKill(int timeLeft) {
            ShatterFlowActive = false;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
