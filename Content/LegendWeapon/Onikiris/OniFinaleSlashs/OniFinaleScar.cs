using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.CrimsonRendSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using OFR = CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFinaleSlashs
{
    /// <summary>
    /// 终之太刀·激光直痕：三态时间轴外控的跨屏直线斩痕。<br/>
    /// 闪现（1~2 帧白热割开，伤害窗）→ 定格（不侵蚀的余烬红伤痕，屏幕上蓄积成刀痕网）→
    /// 引爆（终斩纳刀瞬间白紫回燃、碎成晶片）。<br/>
    /// 蓄积-引爆让乱舞与终斩产生因果：每道直痕都是给最后一刀布线。<br/>
    /// 生命周期不走 <see cref="OFR.ComputeState"/> 标准采样，由本类状态机手工合成。<br/>
    /// ai[0]=刃方向角(弧度) ai[1]=引爆延迟(帧，&lt;=0 回退 70) ai[2]=尺寸倍率
    /// </summary>
    internal class OniFinaleScar : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int FadeFrames = 12;      //引爆后残影淡出
        private const int DefaultDetonate = 70; //独立调试时的自爆延迟

        private OFR.BladeDef def;
        private bool initialized;
        private bool detonated;
        private int timer;
        private int detonateFrame;

        private float BladeAngle => Projectile.ai[0];
        private float SizeMul => Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f;

        /// <summary>
        /// 触发接口：在持有者客户端调用，世界锚定于 center
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="center">刀刃中心（世界坐标，生成后不追踪）</param>
        /// <param name="bladeAngle">刃方向角（弧度）</param>
        /// <param name="detonateDelay">引爆延迟（帧）；主控传"距纳刀帧数"，&lt;=0 回退自爆</param>
        /// <param name="damage">伤害（闪现窗单次结算）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 center, float bladeAngle, int detonateDelay,
            int damage, float knockback, float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniFinaleScar");
            return Projectile.NewProjectileDirect(source, center, Vector2.Zero
                , ModContent.ProjectileType<OniFinaleScar>(), damage, knockback, player.whoAmI
                , ai0: MathHelper.WrapAngle(bladeAngle), ai1: detonateDelay, ai2: scale);
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;   //Initialize 按引爆帧重设
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60;   //闪现窗单次结算
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            detonateFrame = Projectile.ai[1] > 0f ? (int)Projectile.ai[1] : DefaultDetonate;
            Projectile.timeLeft = detonateFrame + FadeFrames + 4;
            float s = SizeMul;
            float seed = Projectile.identity * 0.6180339887f % 1f;

            def = new OFR.BladeDef {
                SweepFrames = 2, Life = detonateFrame + FadeFrames,
                ErodeStart = 0, ErodeFrames = 1,      //侵蚀由状态机手工驱动，标准采样不使用
                ColorShiftDelay = 0, ColorShiftFrames = 1,
                DamageStart = 0, DamageEnd = 6,
                Mode = 1f, Rot = BladeAngle, Span = 0f, Thick = 0.26f,
                HalfX = 1500f * s, HalfY = 64f * s,
                Flip = Projectile.identity % 2 == 0 ? 1f : -1f,
                Opacity = 0.95f, FrontGlow = 2.6f, Seed = seed,
                TailErode = 0f, FlashPower = 1f, FarDim = 0f, SweepSnap = 0f,
                RazorTailWiden = 0.30f,
                Palette = OFR.BladePalette.Crimson,
            };
        }

        //==================== 状态机 ====================

        /// <summary>手工合成本帧动态量：闪现→定格→引爆三态</summary>
        private OFR.BladeState ComposeState() {
            OFR.BladeState s = new() {
                Sweep = OFR.EaseOutCubic(timer / 2f),
                ScaleMul = 1f,
                ThickMul = 1f,
                FlowPhase = 0.62f * OFR.EaseOutCubic(timer / 15f),
                Opacity = def.Opacity,
                FrontGlow = timer <= 3 ? def.FrontGlow : 0.5f,
            };

            //闪现白闪：割开瞬间过曝，速落
            float spawnFlash = timer <= 1 ? 1f : MathF.Pow(0.55f, timer - 1);
            s.Flash = spawnFlash > 0.02f ? spawnFlash : 0f;

            //定格降温：白热→余烬红伤痕，微弱呼吸防"贴纸感"
            float settle = MathHelper.Clamp((timer - 6) / 14f, 0f, 1f);
            s.ColorShift = settle * 0.72f;
            s.Opacity = def.Opacity * MathHelper.Lerp(1f, 0.78f, settle);
            s.ThickMul = MathHelper.Lerp(1f, 0.80f, settle)
                * (1f + 0.05f * MathF.Sin(timer * 0.23f + def.Seed * 12f));

            if (detonated) {
                int dt = timer - detonateFrame;
                //回燃：伤痕重新烧白 + 增厚，随后随侵蚀碎去
                float burn = MathF.Pow(0.62f, dt);
                s.Flash = MathF.Max(s.Flash, 1.15f * burn);
                s.ColorShift = 0f;
                s.ThickMul = 1f + 0.35f * burn;
                float fadeT = MathHelper.Clamp(dt / (float)FadeFrames, 0f, 1f);
                s.Erode = fadeT;
                s.Opacity = def.Opacity * (1f - fadeT * fadeT);
                s.FrontGlow = 0f;
            }
            return s;
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.85f, Volume = 0.36f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.70f, Volume = 0.20f }, Projectile.Center);
            }
            timer++;

            if (!detonated && timer >= detonateFrame) {
                Detonate();
            }

            //闪现期前缘火花
            if (!Main.dedServ && timer <= def.SweepFrames + 1) {
                OFR.BladeState st = ComposeState();
                float edgeU = MathHelper.Clamp(st.Sweep * 1.05f, 0.06f, 0.94f);
                Vector2 pos = OFR.PointAt(in def, in st, Projectile.Center, edgeU);
                Vector2 tangent = (OFR.PointAt(in def, in st, Projectile.Center, MathHelper.Clamp(edgeU + 0.03f, 0f, 1f)) - pos)
                    .SafeNormalize(BladeAngle.ToRotationVector2());
                for (int k = 0; k < 2; k++) {
                    Vector2 vel = tangent * Main.rand.NextFloat(5f, 12f) + Main.rand.NextVector2Circular(1f, 1f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 130, 90)
                        , Main.rand.NextFloat(0.3f, 0.55f) * SizeMul)
                        ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
                }
            }

            //定格期伤痕微光
            float glow = detonated ? 0.9f : 0.35f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.14f, 0.12f) * glow);
        }

        /// <summary>引爆：白紫回燃 + 沿刃碎成晶片，碎晶顺主控给的流向漂移（斩碎的空间被终斩卷走）</summary>
        private void Detonate() {
            detonated = true;
            //引爆瞬间调色烧向鬼火紫，配合 ColorShift=0 的回燃读作"伤痕被终斩点燃"
            def.Palette = OFR.BladePalette.Escalate(0.92f);

            SoundEngine.PlaySound(SoundID.Item27 with {
                Pitch = Main.rand.NextFloat(0.15f, 0.45f),
                Volume = 0.42f,
            }, Projectile.Center);

            if (Main.dedServ) {
                return;
            }
            CrimsonImpactFX.PushImpact(Projectile.Center, 0.02f);

            OFR.BladeState st = ComposeState();
            Vector2 perp = (BladeAngle + MathHelper.PiOver2).ToRotationVector2();
            Vector2 flow = OniFinaleSlash.ShatterFlowActive
                ? OniFinaleSlash.ShatterFlowAngle.ToRotationVector2() * 3.2f
                : Vector2.Zero;

            int shards = 12 + (int)(4 * SizeMul);
            for (int i = 0; i < shards; i++) {
                float uc = Main.rand.NextFloat(0.08f, 0.92f);
                Vector2 pos = OFR.PointAt(in def, in st, Projectile.Center, uc);
                Vector2 vel = perp * Main.rand.NextFloat(2.5f, 8f) * (Main.rand.NextBool() ? 1f : -1f)
                    + flow + Main.rand.NextVector2Circular(1.4f, 1.4f);
                Color c = Main.rand.NextBool(3) ? new Color(235, 205, 255) : new Color(185, 110, 255);
                PRTLoader.NewParticle<PRT_OniShard>(pos, vel, c
                    , Main.rand.NextFloat(0.42f, 0.78f) * SizeMul)
                    ?.Configure(Main.rand.Next(22, 38), Main.rand.NextFloat(-0.24f, 0.24f)
                        , Main.rand.NextFloat(1.6f, 2.8f), affectedByGravity: true);
            }
        }

        //==================== 判定 ====================

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!initialized || timer < def.DamageStart || timer > def.DamageEnd) {
                return false;
            }
            OFR.BladeState st = ComposeState();
            float sweepU = MathHelper.Clamp(st.Sweep * 1.05f, 0f, 1f);
            Vector2 head = OFR.PointAt(in def, in st, Projectile.Center, 0.05f);
            Vector2 tail = OFR.PointAt(in def, in st, Projectile.Center, MathF.Min(0.95f, sweepU));
            float cp = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , head, tail, def.HalfY * 0.62f, ref cp);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.CWR().TimeFrozenTick = 2;
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.2f, Volume = 0.65f }, target.Center);

            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = BladeAngle.ToRotationVector2().RotatedByRandom(0.6) * Main.rand.NextFloat(4f, 11f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(target.Center, vel, new Color(255, 96, 60)
                    , Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 24), affectedByGravity: true);
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }
            OFR.BladeState st = ComposeState();
            if (st.Opacity <= 0.012f) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OFR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }
            OFR.DrawBladeLayers(device, fx, in def, in st, Projectile.Center, 0f);
            OFR.EndDraw(device, pb, pr, pd);
        }
    }
}
