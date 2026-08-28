using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>
    /// 沙爆漩涡：漩涡冲刺的蓄力实体 + 定时炸弹。本体无伤害（hostile=false，杀伤全在
    /// 后爆沙球环），全程漫反射沙材质（原版沙球贴图多层旋转 + 尘涡，不走加色辉光）。
    /// 时间轴与状态共读 Director 常数：蓄力 ai[0] 帧 → 塌缩（缩到四成 + 粒子静默）→
    /// 待爆（蛇已冲走）→ 自爆放环，P3 错半步追加第二波慢环。
    /// 蓄力粒子密度在 72% 硬切——最后四分之一安静，尖叫前的吸气。
    /// 孤儿保险：宿主头不在漩涡状态（转阶段/死亡打断）即静默消散，不爆球。
    /// ai[0]=蓄力帧数 ai[1]=头 whoAmI ai[2]=生成时阶段。
    /// </summary>
    internal class BssSandVortexProj : BssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        private int ChargeFrames => Math.Max((int)Projectile.ai[0], 1);
        private int HeadIndex => (int)Projectile.ai[1];
        private int PhaseAtSpawn => (int)Projectile.ai[2];

        private int CollapseEnd => ChargeFrames + BssDirector.VortexCollapseFrames;
        private int DetonateFrame => CollapseEnd + BssDirector.VortexDetonateDelay;
        private int SecondWaveFrame => DetonateFrame + BssDirector.VortexSecondWaveDelay;

        /// <summary>视觉自旋角（本地累积，越搓越快）</summary>
        private ref float SpinAngle => ref Projectile.localAI[0];
        /// <summary>寿命计数（本地推进；玩法裁决只在权威端发生）</summary>
        private ref float Age => ref Projectile.localAI[1];

        private bool detonated;
        private bool secondWaveDone;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
            Projectile.timeLeft = 600;
        }

        /// <summary>蓄力进度 0..1</summary>
        private float Charge => MathHelper.Clamp(Age / ChargeFrames, 0f, 1f);

        public override void AI() {
            Projectile.velocity = Vector2.Zero;

            //孤儿保险：头没了或已不在漩涡状态（被转阶段/死亡演出打断）→ 静默消散
            if (!detonated && !HostStillSpinning()) {
                Dissipate();
                return;
            }

            int age = (int)Age;
            float charge = Charge;

            //自旋提速：搓得越久转得越快
            SpinAngle += 0.16f + 0.26f * charge;

            if (age < ChargeFrames) {
                UpdateChargeFx(charge);
            }
            else if (age < CollapseEnd) {
                //塌缩段：无粒子（静默即预告），只留一记倒吸气
                if (age == ChargeFrames && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.6f, Pitch = 0.4f, MaxInstances = 2 },
                        Projectile.Center);
                }
            }
            else if (!detonated && age >= DetonateFrame) {
                detonated = true;
                Detonate(BssDirector.VortexGlobRing,
                    BssDirector.VortexGlobSpeedMin, BssDirector.VortexGlobSpeedMax, 0f, big: true);
            }

            if (detonated && !secondWaveDone && age >= SecondWaveFrame) {
                secondWaveDone = true;
                //P3 第二波慢环：角度错半步，速度压到下半区（内圈落点）
                if (PhaseAtSpawn >= 3) {
                    Detonate(BssDirector.VortexGlobRingSecond,
                        BssDirector.VortexGlobSpeedMin, 9f,
                        MathHelper.Pi / BssDirector.VortexGlobRing, big: false);
                }
            }

            //波放完收尾
            if (detonated && age >= SecondWaveFrame + 6) {
                Projectile.Kill();
                return;
            }

            Age++;
        }

        /// <summary>宿主头是否仍在漩涡冲刺状态（含爆冲/硬刹段）</summary>
        private bool HostStillSpinning() {
            if (HeadIndex < 0 || HeadIndex >= Main.maxNPCs) {
                return false;
            }
            NPC head = Main.npc[HeadIndex];
            return head.active && head.type == ModContent.NPCType<BssHead>()
                && (int)head.ai[3] == (int)BssStateIndex.VortexDash;
        }

        /// <summary>
        /// 蓄力表现：径向吸入沙线 + 切向轨道尘（有旋度的吸入），密度 ∝ √charge，
        /// 72% 后硬切；隆隆声随蓄力加密爬调，微震 ∝ charge²。
        /// </summary>
        private void UpdateChargeFx(float charge) {
            //隆隆节拍（各端本地）
            int rumbleGap = Math.Max(16 - (int)(6f * charge), 9);
            if ((int)Age % rumbleGap == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.WormDig with {
                    Volume = 0.35f + 0.4f * charge,
                    Pitch = -0.55f + 0.5f * charge,
                    MaxInstances = 3,
                }, Projectile.Center);
                BssVfx.Shake(Projectile.Center, 1f + 3f * charge * charge, 1000f);
            }

            if (Main.dedServ || charge > 0.72f) {
                return;
            }

            int count = 1 + (int)(5f * MathF.Sqrt(charge));
            for (int i = 0; i < count; i++) {
                if (Main.rand.NextBool(3)) {
                    continue;
                }
                if (Main.rand.NextBool(3)) {
                    //切向轨道尘：贴核绕圈（涡的旋度）
                    Vector2 radial = Main.rand.NextVector2CircularEdge(1f, 1f);
                    Vector2 pos = Projectile.Center + radial * Main.rand.NextFloat(40f, 160f);
                    Vector2 tangent = new(-radial.Y, radial.X);
                    Dust orbit = Dust.NewDustPerfect(pos, DustID.Sand,
                        tangent * Main.rand.NextFloat(2f, 5f) - radial * 1.2f,
                        110, default, Main.rand.NextFloat(0.8f, 1.2f));
                    orbit.noGravity = true;
                }
                else {
                    //径向吸入线：远处拉向涡心（比例吸引 = 越近越慢，读作汇聚）
                    Vector2 pos = Projectile.Center
                        + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(100f, 340f);
                    Dust intake = Dust.NewDustPerfect(pos, DustID.Sand,
                        (Projectile.Center - pos) * 0.085f,
                        100, default, Main.rand.NextFloat(1f, 1.6f));
                    intake.noGravity = true;
                }
            }
        }

        /// <summary>
        /// 自爆放环：径向沙球环（四档速度分层 = 内外圈落点），表现各端本地、弹幕权威端。
        /// 公平口径：爆点是玩家盯了一秒半的固定位置，全部重力弧线可读可躲。
        /// </summary>
        private void Detonate(int count, float speedMin, float speedMax, float angleOffset, bool big) {
            if (!Main.dedServ) {
                BssVfx.SandBurst(Projectile.Center, big ? 2.2f : 1.2f);
                BssVfx.Shake(Projectile.Center, big ? 9f : 4f, 1500f);
                if (big) {
                    BssVfx.Roar(Projectile.Center, -0.6f, 1f);
                    //沙土闷爆
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.85f, Pitch = -0.35f, MaxInstances = 2 },
                        Projectile.Center);
                }
                int blast = big ? 36 : 18;
                for (int i = 0; i < blast; i++) {
                    Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
                    Dust d = Dust.NewDustPerfect(Projectile.Center + dir * 14f,
                        Main.rand.NextBool(4) ? DustID.Dirt : DustID.Sand,
                        dir * Main.rand.NextFloat(5f, 14f),
                        90, default, Main.rand.NextFloat(1.1f, 1.8f));
                    d.noGravity = Main.rand.NextBool();
                }
            }

            if (VaultUtils.isClient || count <= 0) {
                return;
            }
            NPC head = HeadIndex >= 0 && HeadIndex < Main.maxNPCs ? Main.npc[HeadIndex] : null;
            if (head == null || !head.Alives()) {
                return;
            }
            int damage = BssDirector.ScaleProjectileDamage(head, BssDirector.SandGlobDamage);
            int type = ModContent.ProjectileType<BssSandGlob>();
            for (int i = 0; i < count; i++) {
                float ang = angleOffset + i * MathHelper.TwoPi / count
                    + Main.rand.NextFloat(-0.05f, 0.05f);
                float speed = MathHelper.Lerp(speedMin, speedMax, i % 4 / 3f)
                    + Main.rand.NextFloat(-0.4f, 0.4f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    ang.ToRotationVector2() * speed, type, damage, 0.6f, Main.myPlayer);
            }
        }

        /// <summary>孤儿消散：散沙不爆球（被打断的演出安静退场）</summary>
        private void Dissipate() {
            if (!Main.dedServ) {
                for (int i = 0; i < 14; i++) {
                    Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
                    Dust d = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(10f, 40f),
                        DustID.Sand, dir * Main.rand.NextFloat(1f, 3f) + new Vector2(0f, -0.8f),
                        120, default, Main.rand.NextFloat(0.8f, 1.3f));
                    d.noGravity = Main.rand.NextBool();
                }
            }
            Projectile.Kill();
        }

        /// <summary>
        /// 漩涡本体：三层同向异速的沙球环（内快外慢 = 涡的剪切），核心尺寸随 charge³ 增长，
        /// 塌缩段缩到四成 + 余弦闪烁（爆前变小）。漫反射材质，乘本地光照。
        /// </summary>
        public override bool PreDraw(ref Color lightColor) {
            int age = (int)Age;
            float charge = Charge;

            //核心半径：charge³ 增长（不起眼的开局，吓人的收尾）
            float radius = MathHelper.Lerp(12f, 110f, charge * charge * charge);
            float flicker = 1f;
            if (age >= ChargeFrames) {
                float cp = MathHelper.Clamp((age - ChargeFrames) / (float)BssDirector.VortexCollapseFrames, 0f, 1f);
                radius = MathHelper.Lerp(110f, 44f, MathHelper.SmoothStep(0f, 1f, cp));
                flicker = 0.93f + 0.07f * MathF.Cos(Age * 0.9f);
            }
            if (detonated) {
                return false;
            }

            float fadeIn = MathHelper.Clamp(charge * 3f, 0f, 1f);
            Main.instance.LoadProjectile(ProjectileID.SandBallFalling);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SandBallFalling].Value;
            Vector2 origin = tex.Size() * 0.5f;

            //三层环：环序越外转得越慢（剪切读数），色越深
            Span<int> ringCount = stackalloc int[] { 4, 7, 9 };
            Span<float> ringRad = stackalloc float[] { 0.35f, 0.7f, 1f };
            Span<float> ringSpin = stackalloc float[] { 1.6f, 1.05f, 0.7f };
            for (int r = 0; r < 3; r++) {
                Color tint = Color.Lerp(BssVfx.SandWarm, BssVfx.SandDark, r * 0.35f);
                float scale = (1.15f - r * 0.18f) * (0.55f + 0.45f * charge) * flicker;
                for (int i = 0; i < ringCount[r]; i++) {
                    float ang = SpinAngle * ringSpin[r] + i * MathHelper.TwoPi / ringCount[r];
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * (radius * ringRad[r] * flicker);
                    Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null,
                        lightColor.MultiplyRGB(tint) * (0.9f * fadeIn),
                        ang * 2f + SpinAngle, origin, scale, SpriteEffects.None, 0);
                }
            }

            //暗核：深色压心（涡眼）
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor.MultiplyRGB(BssVfx.SandDark) * (0.8f * fadeIn),
                SpinAngle * 2f, origin, 1.3f * (0.5f + 0.5f * charge) * flicker, SpriteEffects.None, 0);
            return false;
        }
    }
}
