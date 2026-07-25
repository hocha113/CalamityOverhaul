using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using CSR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer;
using SlashDef = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer.SlashDef;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 息合「吐息」行进弧形剑气：短蓄松手后沿瞄准线飞出的绯红月牙，
    /// 凸面朝前，穿透每目标一次，到程后侵蚀消散。<br/>
    /// 渲染/命中全走绯系列断斩栈(<see cref="CrimsonSlashRenderer"/> + <see cref="CrimsonRendHitVFX"/>)，
    /// 飞行四阶段：出手爆点→行进(减速曲线+前缘介质)→命中→沿途丝痕余寿大于弹体。<br/>
    /// ai[0]=瞄准角(弧度) ai[1]=尺寸倍率
    /// </summary>
    internal class OniMeiBreathArc : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====介质色(与吐息断斩同源：纸白刃口+绯红墨浪)====
        private static readonly Color PaperEdge = new(255, 236, 220);
        private static readonly Color InkDeep = new(70, 16, 22);

        private SlashDef def;
        private bool initialized;
        private int timer;
        /// <summary>到程消散中，伤害关闭</summary>
        private bool dissolving;
        /// <summary>丝痕采样累计位移</summary>
        private float trailAccum;

        private float AimAngle => Projectile.ai[0];
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;

        /// <summary>owner 端生成；伤害基数由调用方压好</summary>
        public static Projectile Fire(Player player, Vector2 origin, float aim, int damage,
            float knockback, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniMeiBreathArc");
            return Projectile.NewProjectileDirect(source, origin
                , aim.ToRotationVector2() * OniMeiCombat.BreathArcLaunchSpeed
                , ModContent.ProjectileType<OniMeiBreathArc>(), damage, knockback, player.whoAmI
                , ai0: MathHelper.WrapAngle(aim), ai1: 1f);
        }

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 54;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = OniMeiCombat.BreathArcFlightFrames + 30;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;   //每目标只咬一口
        }

        private void Initialize() {
            initialized = true;
            float s = SizeMul;
            //Life/ErodeStart 先放远，到程 BeginDissolve 再压回来播侵蚀
            def = new SlashDef {
                Birth = 0, SweepFrames = 4, Life = 600, ErodeStart = 590, ErodeFrames = 12,
                ColorShiftDelay = 8f, ColorShiftFrames = 30f, DamageStart = 1, DamageEnd = 580,
                Mode = 0f, Rot = AimAngle, Span = 1.85f, Thick = 0.30f,
                HalfX = 152f * s, HalfY = 130f * s, Flip = 1f,
                Opacity = 0.94f, FrontGlow = 2.8f, OffsetAlongAim = 0f,
                Seed = Projectile.identity * 0.173f % 1f,
                TailErode = 0.30f, FlashPower = 0.60f, FarDim = 0f,
                Ink = 0.40f, FeiBai = 0.60f, Bleed = 0.12f, SplitTail = 0.68f,
            };
        }

        /// <summary>把时间轴压进侵蚀段，弧体失速碎散</summary>
        private void BeginDissolve() {
            if (dissolving) {
                return;
            }
            dissolving = true;
            def.ErodeStart = timer;
            def.ErodeFrames = 12;
            def.Life = timer + 18;
            Projectile.timeLeft = 20;
            if (!Main.dedServ) {
                Vector2 aimDir = AimAngle.ToRotationVector2();
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_OniInkDrop>(
                        CSR.PointAt(in def, Projectile.Center, Main.rand.NextFloat(0.15f, 0.85f), timer)
                        , aimDir.RotatedByRandom(0.7f) * Main.rand.NextFloat(1f, 3f)
                            + Vector2.UnitY * Main.rand.NextFloat(0.2f, 1f)
                        , InkDeep, Main.rand.NextFloat(0.18f, 0.32f) * SizeMul)
                        ?.Configure(Main.rand.Next(18, 30));
                }
                CrimsonImpactFX.PushAmbience(Projectile.Center, 0.10f);
            }
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.28f, Volume = 0.45f }, Projectile.Center);
                SoundEngine.PlaySound(CWRSound.KatanaSwing with { Pitch = -0.25f, Volume = 0.4f, MaxInstances = 3 }, Projectile.Center);
                CrimsonImpactFX.PushImpact(Projectile.Center, 0.10f);
                if (!Main.dedServ) {
                    //出手爆点：纸白火花顺刃前抛
                    Vector2 aimDir0 = AimAngle.ToRotationVector2();
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center + aimDir0 * 20f
                            , aimDir0.RotatedByRandom(0.35f) * Main.rand.NextFloat(5f, 12f)
                            , PaperEdge, Main.rand.NextFloat(0.26f, 0.44f) * SizeMul)
                            ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
                    }
                }
            }
            timer++;

            Vector2 aimDir = AimAngle.ToRotationVector2();
            if (!dissolving) {
                //减速曲线：出手快、行进渐稳，禁匀速
                float speed = MathHelper.Lerp(OniMeiCombat.BreathArcLaunchSpeed
                    , OniMeiCombat.BreathArcCruiseSpeed, CSR.EaseOutQuad(timer / 22f));
                Projectile.velocity = aimDir * speed;
                trailAccum += speed;
                if (timer >= OniMeiCombat.BreathArcFlightFrames) {
                    BeginDissolve();
                }
            }
            else {
                Projectile.velocity *= 0.80f;
            }

            if (Main.dedServ) {
                return;
            }

            //行进期前缘介质：纸白为主，偶发深红
            if (!dissolving && timer > def.SweepFrames) {
                for (int k = 0; k < 2; k++) {
                    float uc = Main.rand.NextFloat(0.30f, 0.70f);
                    Vector2 pos = CSR.PointAt(in def, Projectile.Center, uc, timer);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos
                        , aimDir.RotatedByRandom(0.3f) * Main.rand.NextFloat(2f, 5f)
                        , Main.rand.NextBool(4) ? new Color(255, 120, 90) : PaperEdge
                        , Main.rand.NextFloat(0.18f, 0.32f) * SizeMul)
                        ?.Configure(Main.rand.Next(8, 14), affectedByGravity: false);
                }
                //弧尖甩墨(两端各低概率)
                if (Main.rand.NextBool(3)) {
                    float tipU = Main.rand.NextBool() ? 0.08f : 0.92f;
                    Vector2 tip = CSR.PointAt(in def, Projectile.Center, tipU, timer);
                    PRTLoader.NewParticle<PRT_OniInkDrop>(tip
                        , -aimDir * Main.rand.NextFloat(0.5f, 1.5f) + Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.8f)
                        , InkDeep, Main.rand.NextFloat(0.14f, 0.24f) * SizeMul)
                        ?.Configure(Main.rand.Next(14, 22));
                }
                //沿途丝痕：几乎驻留的拉伸纸白屑，寿命长过弹体余程
                while (trailAccum >= 26f) {
                    trailAccum -= 26f;
                    Vector2 pos = CSR.PointAt(in def, Projectile.Center
                        , Main.rand.NextFloat(0.25f, 0.75f), timer) - aimDir * 30f;
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos
                        , aimDir * Main.rand.NextFloat(0.2f, 0.7f)
                        , PaperEdge * 0.75f, Main.rand.NextFloat(0.14f, 0.24f) * SizeMul)
                        ?.Configure(Main.rand.Next(26, 42), affectedByGravity: false);
                }
            }

            float bloom = 0.14f * (dissolving ? 1f - MathHelper.Clamp((timer - def.ErodeStart) / 16f, 0f, 1f) : 1f);
            CrimsonImpactFX.PushAmbience(Projectile.Center, bloom);
            Lighting.AddLight(Projectile.Center + aimDir * 30f, new Vector3(0.85f, 0.18f, 0.13f));
        }

        public override bool? CanDamage() => !dissolving && initialized && timer >= def.DamageStart ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!initialized || dissolving) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(12, 12);
            //沿月牙中线折线采样
            const int samples = 11;
            float sweepU = MathHelper.Clamp(CSR.Sweep(in def, timer) * 1.05f, 0f, 1f);
            float thick = MathF.Max(32f, def.Thick * def.HalfX);
            Vector2 prev = Vector2.Zero;
            bool hasPrev = false;
            float cp = 0f;
            for (int k = 0; k < samples; k++) {
                float uc = 0.08f + 0.84f * (k / (float)(samples - 1));
                if (uc > sweepU) {
                    break;
                }
                Vector2 mid = CSR.PointAt(in def, Projectile.Center, uc, timer);
                if (hasPrev && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                    , prev, mid, thick, ref cp)) {
                    return true;
                }
                prev = mid;
                hasPrev = true;
            }
            return false;
        }

        /// <summary>割草断藤，沿月牙弧线</summary>
        public override void CutTiles() {
            if (!initialized || dissolving) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            const int samples = 7;
            Vector2 prev = Vector2.Zero;
            bool hasPrev = false;
            for (int k = 0; k < samples; k++) {
                float uc = 0.10f + 0.80f * (k / (float)(samples - 1));
                Vector2 mid = CSR.PointAt(in def, Projectile.Center, uc, timer);
                if (hasPrev) {
                    Utils.PlotTileLine(prev, mid, 30f, DelegateMethods.CutTiles);
                }
                prev = mid;
                hasPrev = true;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            float offsetX = Projectile.To(target.Center).X;
            modifiers.HitDirectionOverride = MathF.Abs(offsetX) > 0.01f
                ? Math.Sign(offsetX)
                : (MathF.Cos(AimAngle) >= 0f ? 1 : -1);
            //与全系斩击同穿透管线(伤害基数已压低)
            OnikiriItem.ApplySlashPenetration(target, ref modifiers);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            SoundEngine.PlaySound((steel ? SoundID.NPCHit4 : SoundID.NPCHit1) with {
                Pitch = steel ? -0.05f : -0.2f,
                Volume = 0.65f
            }, target.Center);
            CrimsonRendHitVFX.SpawnHitTick(target.Center, AimAngle.ToRotationVector2(), SizeMul, steel);
            CrimsonImpactFX.PushImpact(target.Center, 0.10f);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!CSR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }
            CSR.DrawThreeLayers(device, fx, in def, Projectile.Center, timer, 0f);
            CSR.EndDraw(device, pb, pr, pd);
        }
    }
}
