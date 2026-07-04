using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.CrimsonRendSlashs;
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

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFinaleSlashs
{
    /// <summary>
    /// 终之太刀·纳刀断世：居合语法的终斩。<br/>
    /// 出鞘瞬间只留一条无声细线（斩击已经完成，世界还没反应过来）→ 滞拍 →
    /// 纳刀脆响的刹那伤口撕开：巨月牙+白紫光束芯爆开、世界沿刀线裂成两半滑移、
    /// 伤害在此刻一次性结算 —— 最大的一刀的重量全压在那声刀鞘响上。<br/>
    /// 判定为沿刀线中心向两端各延伸 2400px 的线（参照村正处刑斩），蠕虫/阿瑞斯节段减伤。<br/>
    /// ai[0]=刀线角(弧度) ai[1]=尺寸倍率
    /// </summary>
    internal class OniFinaleCut : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        /// <summary>细线滞拍帧数：出现→纳刀引爆的间隔，主控用它对齐解冻时刻</summary>
        public const int HoldFrames = 18;
        private const int DamageEnd = HoldFrames + 8;
        private const int Lifetime = HoldFrames + 58;

        private OFR.BladeDef lineDef;   //滞拍细线（斩痕本体，引爆后作残影淡出）
        private OFR.BladeDef arcDef;    //巨月牙：伤口撕开的主形
        private OFR.BladeDef beamDef;   //白紫光束芯：沿刀线的爆发高光
        private bool initialized;
        private bool detonatedFx;
        private int timer;

        private float CutAngle => Projectile.ai[0];
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;

        /// <summary>
        /// 触发接口：在持有者客户端调用，世界锚定于 center；
        /// 生成后 <see cref="HoldFrames"/> 帧滞拍，随后纳刀引爆并结算伤害
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="center">刀线中心（世界坐标）</param>
        /// <param name="cutAngle">刀线角度（弧度）</param>
        /// <param name="damage">伤害（引爆窗单次巨额结算）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 center, float cutAngle, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniFinaleCut");
            return Projectile.NewProjectileDirect(source, center, Vector2.Zero
                , ModContent.ProjectileType<OniFinaleCut>(), damage, knockback, player.whoAmI
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
            Projectile.localNPCHitCooldown = 60;   //引爆窗单次结算
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            float s = SizeMul;
            float seed = Projectile.identity * 0.6180339887f % 1f;
            float flip = Projectile.identity % 2 == 0 ? 1f : -1f;

            lineDef = new OFR.BladeDef {
                SweepFrames = 2, Life = Lifetime,
                DamageStart = HoldFrames, DamageEnd = DamageEnd,
                Mode = 1f, Rot = CutAngle, Span = 0f, Thick = 0.22f,
                HalfX = 2600f * s, HalfY = 26f * s, Flip = flip,
                Opacity = 0.85f, FrontGlow = 1.6f, Seed = seed,
                RazorTailWiden = 0f,
                Palette = OFR.BladePalette.Escalate(0.55f),
            };
            //巨月牙：弦沿刀线（长轴=HalfY 方向），弓背垂直刀线鼓出，近平面轻压扁
            arcDef = new OFR.BladeDef {
                SweepFrames = 4, Life = Lifetime - HoldFrames,
                Mode = 0f, Rot = CutAngle + flip * MathHelper.PiOver2, Span = 3.55f,
                Thick = 0.40f,
                HalfX = 1450f * s, HalfY = 2050f * s, Flip = flip,
                Opacity = 1f, FrontGlow = 2.9f, Seed = seed + 0.37f,
                TailErode = 0.35f, FlashPower = 1f, SweepSnap = 0f,
                RazorTailWiden = 0.85f,
                Palette = OFR.BladePalette.OniFire,
            };
            beamDef = new OFR.BladeDef {
                SweepFrames = 2, Life = Lifetime - HoldFrames,
                Mode = 1f, Rot = CutAngle, Span = 0f, Thick = 0.32f,
                HalfX = 2600f * s, HalfY = 120f * s, Flip = flip,
                Opacity = 1f, FrontGlow = 2.4f, Seed = seed + 0.71f,
                TailErode = 0f, FlashPower = 1f,
                RazorTailWiden = 0.40f,
                Palette = OFR.BladePalette.OniFire,
            };
        }

        //==================== 时间轴 ====================

        public override void AI() {
            if (!initialized) {
                Initialize();
                //出鞘的"斩击"本身近乎无声——世界还没意识到已经被斩开
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 1f, Volume = 0.28f }, Projectile.Center);
            }
            timer++;

            if (timer < HoldFrames) {
                //滞拍：细线在后效层保有一条微弱裂缝辉光，随呼吸渐强
                float breath = 0.18f + 0.16f * (timer / (float)HoldFrames)
                    + 0.05f * MathF.Sin(timer * 0.55f);
                OniFinaleFX.PushSplit(Projectile.Center, CutAngle, 0f, breath);
            }

            if (timer == HoldFrames - 1) {
                //引爆前 1 帧负片闪：刹那反白
                OniFinaleFX.PushNegative(Projectile.Center, 0.85f);
            }

            if (timer == HoldFrames) {
                DetonateFx();
            }

            if (timer >= HoldFrames) {
                //裂屏滑移曲线：2 帧顶满、指数收回；裂缝辉光同步衰减
                int dt = timer - HoldFrames;
                float ramp = MathF.Min(dt / 2f, 1f);
                float offset = 30f * SizeMul * ramp * MathF.Exp(-MathF.Max(dt - 2, 0) * 0.18f);
                float seam = MathF.Exp(-dt * 0.10f);
                OniFinaleFX.PushSplit(Projectile.Center, CutAngle, offset, seam);

                Lighting.AddLight(Projectile.Center, new Vector3(0.9f, 0.55f, 1.2f) * seam * 1.4f);
            }
        }

        /// <summary>纳刀引爆：全部声画在这一帧砸下</summary>
        private void DetonateFx() {
            detonatedFx = true;

            //纳刀脆响先行半拍打头，爆响垫底
            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.15f, Volume = 0.95f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.35f, Volume = 1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.50f, Volume = 0.80f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.45f, Volume = 0.90f }, Projectile.Center);

            if (Main.dedServ) {
                return;
            }

            CrimsonImpactFX.PushImpact(Projectile.Center, 0.10f);

            Vector2 perp = (CutAngle + MathHelper.PiOver2).ToRotationVector2();
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center
                , perp, 14f, 9f, 22, -1f, FullName));

            //沿刀线迸出的碎晶帘
            Vector2 dir = CutAngle.ToRotationVector2();
            for (int i = 0; i < 30; i++) {
                float along = Main.rand.NextFloat(-1f, 1f);
                Vector2 pos = Projectile.Center + dir * along * 1300f * SizeMul;
                Vector2 vel = perp * Main.rand.NextFloat(3f, 11f) * (Main.rand.NextBool() ? 1f : -1f)
                    + dir * Main.rand.NextFloat(-2f, 2f);
                Color c = Main.rand.NextBool(3) ? new Color(240, 220, 255) : new Color(185, 110, 255);
                PRTLoader.NewParticle<PRT_OniShard>(pos, vel, c
                    , Main.rand.NextFloat(0.5f, 0.95f) * SizeMul)
                    ?.Configure(Main.rand.Next(26, 44), Main.rand.NextFloat(-0.28f, 0.28f)
                        , Main.rand.NextFloat(1.8f, 3.2f), affectedByGravity: true);
            }
            for (int i = 0; i < 14; i++) {
                Vector2 vel = perp.RotatedByRandom(0.5) * Main.rand.NextFloat(6f, 15f) * (Main.rand.NextBool() ? 1f : -1f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center + dir * Main.rand.NextFloat(-400f, 400f) * SizeMul
                    , vel, new Color(210, 150, 255), Main.rand.NextFloat(0.5f, 0.9f) * SizeMul)
                    ?.Configure(Main.rand.Next(20, 34), affectedByGravity: true);
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(Projectile.Center, Vector2.Zero
                , new Color(235, 215, 255), 1.6f * SizeMul);
        }

        //==================== 判定 ====================

        public override bool? CanHitNPC(NPC target) {
            if (timer < HoldFrames || timer > DamageEnd) {
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
            Vector2 start = Projectile.Center - dir * 2400f * SizeMul;
            Vector2 end = Projectile.Center + dir * 2400f * SizeMul;
            float cp = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , start, end, 110f * SizeMul, ref cp);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //世界刚解冻就吃下终斩的顿帧，命中重量最大化
            target.CWR().TimeFrozenTick = 10;
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.45f, Volume = 0.9f }, target.Center);

            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(target.Center, Vector2.Zero
                , new Color(235, 210, 255), 1.2f);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = CutAngle.ToRotationVector2().RotatedByRandom(0.55) * Main.rand.NextFloat(5f, 13f);
                PRTLoader.NewParticle<PRT_OniShard>(target.Center, vel, new Color(200, 130, 255)
                    , Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(Main.rand.Next(20, 34), Main.rand.NextFloat(-0.25f, 0.25f)
                        , Main.rand.NextFloat(1.5f, 2.6f), affectedByGravity: true);
            }
        }

        //==================== 状态合成与绘制 ====================

        /// <summary>滞拍细线：呼吸脉动，引爆后作残影快速淡出</summary>
        private OFR.BladeState ComposeLineState() {
            OFR.BladeState s = new() {
                Sweep = OFR.EaseOutCubic(timer / 2f),
                ScaleMul = 1f,
                FlowPhase = 0.5f * OFR.EaseOutCubic(timer / 12f),
                FrontGlow = timer <= 3 ? lineDef.FrontGlow : 0.3f,
            };
            float spawnFlash = timer <= 1 ? 0.8f : MathF.Pow(0.5f, timer - 1);
            s.Flash = spawnFlash > 0.02f ? spawnFlash : 0f;

            if (timer < HoldFrames) {
                //滞拍呼吸：亮度与厚度轻微起伏，攒住"将断未断"的势
                float breath = 0.5f + 0.5f * MathF.Sin(timer * 0.55f - MathHelper.PiOver2);
                s.Opacity = lineDef.Opacity * (0.72f + 0.22f * breath);
                s.ThickMul = 0.9f + 0.18f * breath;
                s.ColorShift = 0.15f;
            }
            else {
                int dt = timer - HoldFrames;
                float fade = MathHelper.Clamp(dt / 10f, 0f, 1f);
                s.Flash = MathF.Max(s.Flash, MathF.Pow(0.6f, dt));
                s.Opacity = lineDef.Opacity * (1f - fade);
                s.ThickMul = 1.2f - 0.5f * fade;
                s.Erode = fade * 0.8f;
            }
            return s;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (OFR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                OFR.BladeState lineState = ComposeLineState();
                if (lineState.Opacity > 0.012f) {
                    OFR.DrawBladeLayers(device, fx, in lineDef, in lineState, Projectile.Center, 0f);
                }

                if (timer >= HoldFrames) {
                    int lt = timer - HoldFrames;
                    OFR.BladeState arcState = OFR.ComputeState(in arcDef, lt);
                    if (arcState.Opacity > 0.012f) {
                        OFR.DrawBladeLayers(device, fx, in arcDef, in arcState, Projectile.Center, 0f);
                    }
                    OFR.BladeState beamState = OFR.ComputeState(in beamDef, lt);
                    //光束芯不吃标准侵蚀，改走快速整体衰减——爆发高光要干脆地熄灭
                    beamState.Erode = 0f;
                    beamState.Opacity = beamDef.Opacity * MathF.Max(0f, 1f - lt / 16f);
                    if (beamState.Opacity > 0.012f) {
                        OFR.DrawBladeLayers(device, fx, in beamDef, in beamState, Projectile.Center, 0f);
                    }
                }
                OFR.EndDraw(device, pb, pr, pd);
            }

            if (detonatedFx) {
                DrawDetonateDressing();
            }
        }

        /// <summary>引爆加色敷层：星爆核心 + 扩散环 + 沿刀线速度线</summary>
        private void DrawDetonateDressing() {
            int dt = timer - HoldFrames;
            const int dressLife = 20;
            if (dt < 0 || dt >= dressLife) {
                return;
            }
            float bp = dt / (float)dressLife;
            float inv = 1f - bp;
            float easeOut = 1f - MathF.Pow(inv, 3f);
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (OnikiriAssets.StarFlare02?.Value is Texture2D flare) {
                float coreA = MathF.Pow(inv, 2.0f) * 0.85f;
                float coreS = (1.2f + easeOut * 1.1f) * SizeMul;
                sb.Draw(flare, screenPos, null, new Color(245, 230, 255) * coreA, Projectile.whoAmI * 1.37f
                    , flare.Size() * 0.5f, coreS, SpriteEffects.None, 0);
                sb.Draw(flare, screenPos, null, new Color(170, 100, 255) * (coreA * 0.6f), -Projectile.whoAmI * 0.8f
                    , flare.Size() * 0.5f, coreS * 1.4f, SpriteEffects.None, 0);
            }

            if (OnikiriAssets.Ring01?.Value is Texture2D ring) {
                float ringS = (0.5f + easeOut * 3.4f) * SizeMul;
                float ringA = MathF.Pow(inv, 2.4f) * 0.7f;
                sb.Draw(ring, screenPos, null, new Color(190, 120, 255) * ringA, 0f
                    , ring.Size() * 0.5f, ringS, SpriteEffects.None, 0);
            }

            if (OnikiriAssets.SpeedLines01?.Value is Texture2D lines) {
                float lA = MathF.Pow(inv, 1.7f) * 0.55f;
                Vector2 dir = CutAngle.ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    float seed = (Projectile.whoAmI + i) * 0.6180339887f % 1f;
                    Rectangle src = new(0, (int)(seed * (1024 - 96)), 1024, 96);
                    Vector2 pos = screenPos + dir * ((seed - 0.5f) * 900f + easeOut * 220f) * SizeMul
                        + dir.RotatedBy(MathHelper.PiOver2) * (seed * 2f - 1f) * 90f * SizeMul;
                    sb.Draw(lines, pos, src, new Color(210, 170, 255) * lA, CutAngle
                        , src.Size() * 0.5f, new Vector2(0.9f + easeOut * 0.5f, 0.5f) * SizeMul
                        , SpriteEffects.None, 0);
                }
            }

            sb.End();
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
