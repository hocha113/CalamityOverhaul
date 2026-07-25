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
    /// 息合「吐息」行进弧形剑气：第五拍爆发脆响同帧甩出的绯红月牙，
    /// 凸面朝前，穿透每目标一次，到程后侵蚀消散。<br/>
    /// 材质=墨浪刃口(纸白边+绯红墨体)；动势=出手急冲→急减速→速度拉伸残影，禁匀速贴纸平移。<br/>
    /// ai[0]=瞄准角(弧度) ai[1]=尺寸倍率 ai[2]=刀光翻面(±1)
    /// </summary>
    internal class OniMeiBreathArc : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color PaperEdge = new(255, 236, 220);
        private static readonly Color InkDeep = new(70, 16, 22);

        private const int GhostCapacity = 4;

        private SlashDef def;
        private bool initialized;
        private int timer;
        private bool dissolving;
        private float trailAccum;
        private float baseHalfX;
        private float baseHalfY;
        private float launchRot;
        private readonly Vector2[] ghostPos = new Vector2[GhostCapacity];
        private readonly float[] ghostRot = new float[GhostCapacity];
        private readonly float[] ghostStretch = new float[GhostCapacity];
        private int ghostCount;

        private float AimAngle => Projectile.ai[0];
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;
        private float FlipSign => Projectile.ai[2] >= 0f ? 1f : -1f;

        /// <summary>owner 端生成；伤害基数由调用方压好；sizeMul→ai[1]，flip→ai[2]</summary>
        public static Projectile Fire(Player player, Vector2 origin, float aim, int damage,
            float knockback, float sizeMul = 1f, float flip = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniMeiBreathArc");
            return Projectile.NewProjectileDirect(source, origin
                , aim.ToRotationVector2() * OniMeiCombat.BreathArcLaunchSpeed
                , ModContent.ProjectileType<OniMeiBreathArc>(), damage, knockback, player.whoAmI
                , ai0: MathHelper.WrapAngle(aim)
                , ai1: sizeMul > 0.05f ? sizeMul : 1f
                , ai2: flip >= 0f ? 1f : -1f);
        }

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 96;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = OniMeiCombat.BreathArcFlightFrames + 30;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        private void Initialize() {
            initialized = true;
            float s = SizeMul;
            Vector2 keepCenter = Projectile.Center;
            int box = (int)MathF.Max(96f, 110f * s);
            Projectile.width = box;
            Projectile.height = box;
            Projectile.Center = keepCenter;

            baseHalfX = 248f * s;
            baseHalfY = 210f * s;
            //从终结拍刃姿甩出:初始角略偏,随后回正到瞄准,读作刀势甩离而非水平贴图
            launchRot = AimAngle + FlipSign * 0.28f;
            def = new SlashDef {
                Birth = 0, SweepFrames = 4, Life = 600, ErodeStart = 590, ErodeFrames = 14,
                ColorShiftDelay = 6f, ColorShiftFrames = 26f, DamageStart = 1, DamageEnd = 580,
                Mode = 0f, Rot = launchRot, Span = 2.35f, Thick = 0.36f,
                HalfX = baseHalfX, HalfY = baseHalfY, Flip = FlipSign,
                Opacity = 0.97f, FrontGlow = 3.3f, OffsetAlongAim = 0f,
                Seed = Projectile.identity * 0.173f % 1f,
                TailErode = 0.34f, FlashPower = 0.82f, FarDim = 0.55f,
                Ink = 0.48f, FeiBai = 0.58f, Bleed = 0.18f, SplitTail = 0.78f,
            };

            //出手音压低量,主响留给 Slash 爆发脆响;这里只补一记闷墨浪
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.15f, Volume = 0.32f }, Projectile.Center);

            if (!Main.dedServ) {
                Vector2 aimDir0 = AimAngle.ToRotationVector2();
                Vector2 perp0 = aimDir0.RotatedBy(MathHelper.PiOver2);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center + perp0 * Main.rand.NextFloat(-22f, 22f) * s
                        , aimDir0 * Main.rand.NextFloat(10f, 20f) + perp0 * Main.rand.NextFloat(-1.5f, 1.5f)
                        , PaperEdge, Main.rand.NextFloat(0.30f, 0.50f) * s)
                        ?.Configure(Main.rand.Next(8, 14), affectedByGravity: false);
                }
            }
        }

        private void BeginDissolve() {
            if (dissolving) {
                return;
            }
            dissolving = true;
            def.ErodeStart = timer;
            def.ErodeFrames = 14;
            def.Life = timer + 20;
            Projectile.timeLeft = 22;
            if (!Main.dedServ) {
                Vector2 aimDir = AimAngle.ToRotationVector2();
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_OniInkDrop>(
                        CSR.PointAt(in def, Projectile.Center, Main.rand.NextFloat(0.15f, 0.85f), timer)
                        , aimDir.RotatedByRandom(0.7f) * Main.rand.NextFloat(1f, 3.5f)
                            + Vector2.UnitY * Main.rand.NextFloat(0.2f, 1.2f)
                        , InkDeep, Main.rand.NextFloat(0.20f, 0.36f) * SizeMul)
                        ?.Configure(Main.rand.Next(20, 34));
                }
            }
        }

        private void PushGhost(float stretch) {
            for (int i = GhostCapacity - 1; i > 0; i--) {
                ghostPos[i] = ghostPos[i - 1];
                ghostRot[i] = ghostRot[i - 1];
                ghostStretch[i] = ghostStretch[i - 1];
            }
            ghostPos[0] = Projectile.Center;
            ghostRot[0] = def.Rot;
            ghostStretch[0] = stretch;
            if (ghostCount < GhostCapacity) {
                ghostCount++;
            }
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
            }
            timer++;

            Vector2 aimDir = AimAngle.ToRotationVector2();
            Vector2 perp = aimDir.RotatedBy(MathHelper.PiOver2);
            float stretch = 1f;
            if (!dissolving) {
                //急冲→急减速:前几帧吃满出手,再 EaseOutCubic 砸到巡航,禁匀速
                float brakeT = MathHelper.Clamp(timer / 16f, 0f, 1f);
                float speed = MathHelper.Lerp(OniMeiCombat.BreathArcLaunchSpeed
                    , OniMeiCombat.BreathArcCruiseSpeed, CSR.EaseOutCubic(brakeT));
                Projectile.velocity = aimDir * speed;
                trailAccum += speed;
                stretch = MathHelper.Clamp(speed / OniMeiCombat.BreathArcCruiseSpeed, 1f, 1.65f);
                def.HalfX = baseHalfX * stretch;
                def.HalfY = baseHalfY * MathHelper.Lerp(1f, 0.88f, (stretch - 1f) / 0.65f);
                //刀势回正:甩出偏角收回瞄准,给旋转信息,避免「旋转贴图在平移」
                if (timer <= 12) {
                    def.Rot = MathHelper.Lerp(launchRot, AimAngle, CSR.EaseOutQuad(timer / 12f));
                }
                else {
                    def.Rot = AimAngle;
                }
                //尾缘 FarDim 随减速加重,前缘更锋
                def.FarDim = MathHelper.Lerp(0.35f, 0.72f, brakeT);
                def.FrontGlow = MathHelper.Lerp(3.6f, 2.6f, brakeT);
                def.Opacity = MathHelper.Lerp(1f, 0.88f, brakeT * 0.5f);

                if (timer % 2 == 0) {
                    PushGhost(stretch);
                }
                if (timer >= OniMeiCombat.BreathArcFlightFrames) {
                    BeginDissolve();
                }
            }
            else {
                Projectile.velocity *= 0.78f;
                def.Opacity *= 0.92f;
                def.HalfX = MathHelper.Lerp(def.HalfX, baseHalfX * 0.85f, 0.18f);
            }

            if (Main.dedServ) {
                return;
            }
            Vector2 offset = Projectile.velocity.UnitVector() * -Projectile.width * 2;
            if (!dissolving && timer > def.SweepFrames) {
                float speedNow = Projectile.velocity.Length();
                //行进介质:速度拉伸火花(沿速长、横向窄)
                int shed = speedNow > 24f ? 3 : 2;
                for (int k = 0; k < shed; k++) {
                    float uc = Main.rand.NextFloat(0.28f, 0.72f);
                    Vector2 pos = CSR.PointAt(in def, Projectile.Center + offset, uc, timer);
                    Vector2 vel = aimDir * Main.rand.NextFloat(speedNow * 0.35f, speedNow * 0.7f)
                        + perp * Main.rand.NextFloat(-1.4f, 1.4f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel
                        , Main.rand.NextBool(5) ? new Color(255, 120, 90) : PaperEdge
                        , Main.rand.NextFloat(0.16f, 0.30f) * SizeMul * stretch)
                        ?.Configure(Main.rand.Next(7, 12), affectedByGravity: false);
                }
                if (Main.rand.NextBool(2)) {
                    float tipU = Main.rand.NextBool() ? 0.08f : 0.92f;
                    Vector2 tip = CSR.PointAt(in def, Projectile.Center + offset, tipU, timer);
                    PRTLoader.NewParticle<PRT_OniInkDrop>(tip
                        , -aimDir * Main.rand.NextFloat(0.4f, 1.4f) + Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.9f)
                        , InkDeep, Main.rand.NextFloat(0.14f, 0.26f) * SizeMul)
                        ?.Configure(Main.rand.Next(14, 24));
                }
                //丝痕余寿大于弹体:更疏、更长、更贴尾迹
                float trailStep = MathHelper.Lerp(18f, 30f, MathHelper.Clamp(1f - (stretch - 1f) / 0.65f, 0f, 1f));
                while (trailAccum >= trailStep) {
                    trailAccum -= trailStep;
                    Vector2 pos = CSR.PointAt(in def, Projectile.Center + offset
                        , Main.rand.NextFloat(0.22f, 0.78f), timer) - aimDir * (36f * stretch);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos
                        , aimDir * Main.rand.NextFloat(0.15f, 0.55f)
                        , PaperEdge * 0.7f, Main.rand.NextFloat(0.12f, 0.22f) * SizeMul)
                        ?.Configure(Main.rand.Next(30, 48), affectedByGravity: false);
                }
            }
            Lighting.AddLight(Projectile.Center + aimDir * 36f, new Vector3(0.9f, 0.2f, 0.14f));
        }

        public override bool? CanDamage() => !dissolving && initialized && timer >= def.DamageStart ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!initialized || dissolving) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(14, 14);
            const int samples = 12;
            float sweepU = MathHelper.Clamp(CSR.Sweep(in def, timer) * 1.05f, 0f, 1f);
            float thick = MathF.Max(36f, def.Thick * def.HalfX);
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
                    Utils.PlotTileLine(prev, mid, 34f, DelegateMethods.CutTiles);
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
            OnikiriItem.ApplySlashPenetration(target, ref modifiers);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            if (!steel) {
                SoundEngine.PlaySound(CWRSound.KatanaHitB, target.Center);
            }
            else {
                SoundEngine.PlaySound(CWRSound.KatanaHit, target.Center);
            }

            CrimsonRendHitVFX.SpawnImpactBurst(target.Center, Projectile.velocity, 0.35f, 0.65f, steel);
            CrimsonRendHitVFX.SpawnHitTick(target.Center, AimAngle.ToRotationVector2(), SizeMul, CWRLoad.NPCValue.ISTheofSteel(target));
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
            Vector2 offset = Projectile.velocity.UnitVector() * -Projectile.width * 2;
            //速度残影:由远到近叠绘淡化月牙,读作甩出而非贴图平移
            SlashDef ghostDef = def;
            for (int i = ghostCount - 1; i >= 1; i--) {
                float fade = 0.22f * (1f - i / (float)GhostCapacity);
                ghostDef.Rot = ghostRot[i];
                ghostDef.HalfX = baseHalfX * ghostStretch[i];
                ghostDef.HalfY = baseHalfY * MathHelper.Lerp(1f, 0.9f, (ghostStretch[i] - 1f) / 0.65f);
                ghostDef.Opacity = def.Opacity * fade;
                ghostDef.FrontGlow = def.FrontGlow * 0.55f;
                ghostDef.FarDim = MathF.Min(0.85f, def.FarDim + 0.15f);
                CSR.DrawThreeLayers(device, fx, in ghostDef, ghostPos[i] + offset, timer, 0f);
            }
            CSR.DrawThreeLayers(device, fx, in def, Projectile.Center + offset, timer, 0f);
            CSR.EndDraw(device, pb, pr, pd);
        }
    }
}
