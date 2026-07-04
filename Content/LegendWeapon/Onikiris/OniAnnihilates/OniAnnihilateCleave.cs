using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFinaleSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using OFR = CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniAnnihilates
{
    /// <summary>
    /// 鬼哭·灭世一闪·爆发巨斩：蓄力全部倾泻的那一刀。<br/>
    /// 与纳刀断世的"细线滞拍→引爆"不同，这一刀在出生帧即已完成 —— 以玩家为曲率
    /// 中心的环绕巨月牙（弓背朝瞄准方向、跨度 ~200° 绕身扫开，出生尺寸爆发读作
    /// "从人挥出去"），身后滞两帧跟一层滚转略落后的暗色回声浪，白热光束芯沿刀线
    /// 延伸出屏（伤害带的可视化），整屏暖白一瞬 + 径向模糊连推十余帧把能量从
    /// 极点向全屏拽开，伤害同帧单次巨额结算。<br/>
    /// 判定为沿刀线中心向两端各延伸 2600px 的带（蠕虫/阿瑞斯节段减伤惯例）。<br/>
    /// ai[0]=刀线角(弧度) ai[1]=尺寸倍率
    /// </summary>
    internal class OniAnnihilateCleave : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int DamageEnd = 8;
        private const int Lifetime = 52;
        /// <summary>爆发后径向模糊续推帧数（衰减曲线由 <see cref="BlurEnvelope"/> 给出）</summary>
        private const int BlurPushFrames = 14;

        private OFR.BladeDef arcDef;    //巨浪月牙：全屏斜斩的主形，刃面过焦点
        private OFR.BladeDef echoDef;   //回声浪：主浪身后一层暗色残势
        private OFR.BladeDef beamDef;   //白热光束芯：钉住刀线的爆发高光
        private bool initialized;
        private int timer;

        private float CutAngle => Projectile.ai[0];
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;

        /// <summary>
        /// 触发接口：在持有者客户端调用，世界锚定于 center；出生帧即引爆结算
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="center">刀线中心（世界坐标）</param>
        /// <param name="cutAngle">刀线角度（弧度）</param>
        /// <param name="damage">伤害（单次巨额结算）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 center, float cutAngle, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniAnnihilateCleave");
            return Projectile.NewProjectileDirect(source, center, Vector2.Zero
                , ModContent.ProjectileType<OniAnnihilateCleave>(), damage, knockback, player.whoAmI
                , ai0: MathHelper.WrapAngle(cutAngle), ai1: scale);
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
            Projectile.timeLeft = Lifetime + 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60;   //伤害窗单次结算
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            float s = SizeMul;
            float seed = Projectile.identity * 0.6180339887f % 1f;
            float flip = Projectile.identity % 2 == 0 ? 1f : -1f;

            //环绕巨月牙：quad 中心 = 玩家（曲率中心在人身上，读作"从人挥出去"），
            //Rot = 瞄准角 → 弓背朝瞄准方向鼓出，跨度 ~200° 绕身扫开；
            //刃面外缘 ≈ 0.90×HalfX ≈ 520px，屏幕内完整可见
            arcDef = new OFR.BladeDef {
                SweepFrames = 4, Life = Lifetime,
                ErodeStart = 20, ErodeFrames = 26,
                ColorShiftDelay = 14, ColorShiftFrames = 30,
                Mode = 0f, Rot = CutAngle, Span = 3.50f,
                Thick = 0.42f,
                HalfX = 580f * s, HalfY = 520f * s, Flip = flip,
                Opacity = 1f, FrontGlow = 2.9f, Seed = seed + 0.37f,
                TailErode = 0.35f, FlashPower = 1f, SweepSnap = 0f,
                RazorTailWiden = 0.85f,
                Palette = OFR.BladePalette.WhiteHot,
            };
            //回声浪：滚转略落后于主浪的暗色残势（同心、半径更小），读出挥舞的厚度
            echoDef = new OFR.BladeDef {
                SweepFrames = 5, Life = Lifetime - 2,
                ErodeStart = 16, ErodeFrames = 24,
                ColorShiftDelay = 8, ColorShiftFrames = 22,
                Mode = 0f, Rot = CutAngle - flip * 0.42f, Span = 3.70f,
                Thick = 0.46f,
                HalfX = 470f * s, HalfY = 420f * s, Flip = flip,
                Opacity = 0.78f, FrontGlow = 1.2f, Seed = seed + 0.61f,
                TailErode = 0.50f, FlashPower = 0.5f,
                RazorTailWiden = 0.60f,
                Palette = OFR.BladePalette.Escalate(0.5f),
            };
            beamDef = new OFR.BladeDef {
                SweepFrames = 2, Life = Lifetime,
                ErodeStart = 14, ErodeFrames = 20,
                ColorShiftDelay = 10, ColorShiftFrames = 24,
                Mode = 1f, Rot = CutAngle, Span = 0f, Thick = 0.32f,
                HalfX = 2600f * s, HalfY = 130f * s, Flip = flip,
                Opacity = 1f, FrontGlow = 2.4f, Seed = seed + 0.71f,
                TailErode = 0f, FlashPower = 1f,
                RazorTailWiden = 0.40f,
                Palette = OFR.BladePalette.WhiteHot,
            };
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
                DetonateFx();
            }
            timer++;

            //径向模糊连推：暗底细线拉不出丝，模糊要压在浪体最亮的十几帧上才可见
            if (timer <= BlurPushFrames && !Main.dedServ) {
                OniAnnihilateFX.PushBlur(Projectile.Center, BlurEnvelope(timer));
            }

            float seam = MathF.Exp(-timer * 0.10f);
            Lighting.AddLight(Projectile.Center, new Vector3(1.35f, 0.55f, 0.32f) * seam * 1.5f);
        }

        /// <summary>模糊包络：前 3 帧顶满 0.22，随后指数回落到爬入量级</summary>
        private static float BlurEnvelope(int t) {
            return t <= 3 ? 0.22f : 0.22f * MathF.Pow(0.86f, t - 3);
        }

        /// <summary>出生帧：全部声画一次砸下</summary>
        private void DetonateFx() {
            //爆响复合：低爆垫底、布帛撕裂、高频刀鸣（不用 Unlock —— 鞘鸣留给纳刀断世）
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.30f, Volume = 1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.50f, Volume = 0.85f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.50f, Volume = 0.90f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.45f, Volume = 0.85f }, Projectile.Center);

            if (Main.dedServ) {
                return;
            }

            //白屏一瞬 + 冲击拉丝：攒了一整场的东西在这一帧砸满整个画面
            OniAnnihilateFX.PushWhiteFlash(Projectile.Center, 0.85f);
            CrimsonImpactFX.PushImpact(Projectile.Center, 0.30f);
            CrimsonImpactFX.PushAmbience(Projectile.Center, 0.55f);

            Vector2 dir = CutAngle.ToRotationVector2();
            Vector2 perp = (CutAngle + MathHelper.PiOver2).ToRotationVector2();
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center
                , perp, 15f, 9f, 24, -1f, FullName));

            //沿刀线迸出的碎晶帘
            for (int i = 0; i < 32; i++) {
                float along = Main.rand.NextFloat(-1f, 1f);
                Vector2 pos = Projectile.Center + dir * along * 1400f * SizeMul;
                Vector2 vel = perp * Main.rand.NextFloat(3f, 12f) * (Main.rand.NextBool() ? 1f : -1f)
                    + dir * Main.rand.NextFloat(-2f, 2f);
                Color c = Main.rand.NextBool(3) ? new Color(255, 238, 215) : new Color(255, 115, 62);
                PRTLoader.NewParticle<PRT_OniShard>(pos, vel, c
                    , Main.rand.NextFloat(0.5f, 0.95f) * SizeMul)
                    ?.Configure(Main.rand.Next(26, 44), Main.rand.NextFloat(-0.28f, 0.28f)
                        , Main.rand.NextFloat(1.8f, 3.2f), affectedByGravity: true);
            }
            for (int i = 0; i < 14; i++) {
                Vector2 vel = perp.RotatedByRandom(0.5) * Main.rand.NextFloat(6f, 15f) * (Main.rand.NextBool() ? 1f : -1f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center + dir * Main.rand.NextFloat(-450f, 450f) * SizeMul
                    , vel, new Color(255, 150, 95), Main.rand.NextFloat(0.5f, 0.9f) * SizeMul)
                    ?.Configure(Main.rand.Next(20, 34), affectedByGravity: true);
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(Projectile.Center, Vector2.Zero
                , new Color(255, 236, 216), 1.7f * SizeMul);
        }

        //==================== 判定 ====================

        public override bool? CanHitNPC(NPC target) {
            if (timer > DamageEnd) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        /// <summary>巨物减伤（参照村正处刑斩）：蠕虫节体 0.2，阿瑞斯节段 0.4</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.2f;
            }
            if (CWRLoad.ExoMechAresSegments.Contains(target.type)) {
                modifiers.FinalDamage *= 0.4f;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = CutAngle.ToRotationVector2();
            Vector2 start = Projectile.Center - dir * 2600f * SizeMul;
            Vector2 end = Projectile.Center + dir * 2600f * SizeMul;
            float cp = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , start, end, 160f * SizeMul, ref cp);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //世界刚解冻就吃下巨斩的顿帧，命中重量最大化
            target.CWR().TimeFrozenTick = 10;
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.45f, Volume = 0.9f }, target.Center);

            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(target.Center, Vector2.Zero
                , new Color(255, 222, 198), 1.2f);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = CutAngle.ToRotationVector2().RotatedByRandom(0.55) * Main.rand.NextFloat(5f, 13f);
                PRTLoader.NewParticle<PRT_OniShard>(target.Center, vel, new Color(255, 132, 76)
                    , Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(Main.rand.Next(20, 34), Main.rand.NextFloat(-0.25f, 0.25f)
                        , Main.rand.NextFloat(1.5f, 2.6f), affectedByGravity: true);
            }
        }

        //==================== 绘制 ====================

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (OFR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                //回声浪先画（垫在主浪身后，滞 2 帧跟进）
                int echoT = timer - 2;
                if (echoT > 0) {
                    OFR.BladeState echoState = OFR.ComputeState(in echoDef, echoT);
                    if (echoState.Opacity > 0.012f) {
                        OFR.DrawBladeLayers(device, fx, in echoDef, in echoState, Projectile.Center, 0f);
                    }
                }

                OFR.BladeState arcState = OFR.ComputeState(in arcDef, timer);
                if (arcState.Opacity > 0.012f) {
                    OFR.DrawBladeLayers(device, fx, in arcDef, in arcState, Projectile.Center, 0f);
                }

                OFR.BladeState beamState = OFR.ComputeState(in beamDef, timer);
                //光束芯不吃标准侵蚀，改走快速整体衰减——爆发高光要干脆地熄灭
                beamState.Erode = 0f;
                beamState.Opacity = beamDef.Opacity * MathF.Max(0f, 1f - timer / 16f);
                if (beamState.Opacity > 0.012f) {
                    OFR.DrawBladeLayers(device, fx, in beamDef, in beamState, Projectile.Center, 0f);
                }
                OFR.EndDraw(device, pb, pr, pd);
            }

            DrawDetonateDressing();
        }

        /// <summary>引爆加色敷层：星爆核心 + 扩散环 + 沿刀线速度线</summary>
        private void DrawDetonateDressing() {
            const int dressLife = 20;
            if (timer >= dressLife) {
                return;
            }
            float bp = timer / (float)dressLife;
            float inv = 1f - bp;
            float easeOut = 1f - MathF.Pow(inv, 3f);
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (OnikiriAssets.StarFlare02?.Value is Texture2D flare) {
                float coreA = MathF.Pow(inv, 2.0f) * 0.9f;
                float coreS = (1.3f + easeOut * 1.2f) * SizeMul;
                sb.Draw(flare, screenPos, null, new Color(255, 244, 230) * coreA, Projectile.whoAmI * 1.37f
                    , flare.Size() * 0.5f, coreS, SpriteEffects.None, 0);
                sb.Draw(flare, screenPos, null, new Color(255, 122, 68) * (coreA * 0.6f), -Projectile.whoAmI * 0.8f
                    , flare.Size() * 0.5f, coreS * 1.4f, SpriteEffects.None, 0);
            }

            if (OnikiriAssets.Ring01?.Value is Texture2D ring) {
                float ringS = (0.5f + easeOut * 3.8f) * SizeMul;
                float ringA = MathF.Pow(inv, 2.4f) * 0.7f;
                sb.Draw(ring, screenPos, null, new Color(255, 98, 58) * ringA, 0f
                    , ring.Size() * 0.5f, ringS, SpriteEffects.None, 0);
            }

            if (OnikiriAssets.SpeedLines01?.Value is Texture2D lines) {
                float lA = MathF.Pow(inv, 1.7f) * 0.55f;
                Vector2 dir = CutAngle.ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    float seed = (Projectile.whoAmI + i) * 0.6180339887f % 1f;
                    Rectangle src = new(0, (int)(seed * (1024 - 96)), 1024, 96);
                    Vector2 pos = screenPos + dir * ((seed - 0.5f) * 950f + easeOut * 240f) * SizeMul
                        + dir.RotatedBy(MathHelper.PiOver2) * (seed * 2f - 1f) * 95f * SizeMul;
                    sb.Draw(lines, pos, src, new Color(255, 176, 138) * lA, CutAngle
                        , src.Size() * 0.5f, new Vector2(0.95f + easeOut * 0.5f, 0.55f) * SizeMul
                        , SpriteEffects.None, 0);
                }
            }

            sb.End();
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
