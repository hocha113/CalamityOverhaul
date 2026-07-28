using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
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
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs
{
    /// <summary>终之太刀环形刀光</summary>
    internal class OniFinaleRing : ModProjectile, IOniCrispDrawable, ICrimsonFarDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 30;

        private OFR.BladeDef def;
        private bool initialized;
        private bool impactDone;
        private int timer;

        private float Roll => Projectile.ai[0];
        private float EscalateT => MathF.Abs(Projectile.ai[1]);
        private float FlipSign => Projectile.ai[1] < 0f ? -1f : 1f;
        private float SizeMul => Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f;

        /// <summary>触发接口、在持有者客户端调用，世界锚定于 center</summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="center">环心（世界坐标，生成后不追踪）</param>
        /// <param name="roll">滚转角（弧度，决定椭圆长轴朝向）</param>
        /// <param name="escalate">升调进度 0..1（绯红→白热，同时驱动尺寸/白闪递增）</param>
        /// <param name="flip">扫掠方向镜像 ±1</param>
        /// <param name="damage">伤害</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 center, float roll, float escalate, int flip,
            int damage, float knockback, float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniFinaleRing");
            escalate = MathHelper.Clamp(escalate, 0.02f, 1f);
            return Projectile.NewProjectileDirect(source, center, Vector2.Zero
                , ModContent.ProjectileType<OniFinaleRing>(), damage, knockback, player.whoAmI
                , ai0: MathHelper.WrapAngle(roll), ai1: escalate * (flip < 0 ? -1f : 1f), ai2: scale);
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
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;   //伤害窗短于冷却，单环对单位只结算一次

        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            float t = EscalateT;
            float s = SizeMul;
            //identity 确定性派生（identity 多人同步，各端形态一致）

            float seed = Projectile.identity * 0.6180339887f % 1f;
            float squash = 0.30f + 0.26f * (seed * 7.13f % 1f);
            float half = (280f + 330f * t) * s;

            def = new OFR.BladeDef {
                SweepFrames = 5, Life = Lifetime, ErodeStart = 9, ErodeFrames = 15,
                ColorShiftDelay = 8, ColorShiftFrames = 13, DamageStart = 1, DamageEnd = 8,
                Mode = 0f, Rot = Roll, Span = 5.2f + 0.62f * (seed * 3.71f % 1f),
                Thick = 0.30f + 0.06f * t,
                HalfX = half, HalfY = half * squash, Flip = FlipSign,
                Opacity = 0.95f, FrontGlow = 2.4f + 0.5f * t, Seed = seed,
                TailErode = 0.45f, FlashPower = 0.55f + 0.28f * t,
                FarDim = 0.52f, SweepSnap = 0f, RazorTailWiden = 0.45f,
                //环族升调封顶在白热之下，纯 t=1 留给终斩独占

                Palette = OFR.BladePalette.Escalate(t * 0.80f),
            };
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
                float t = EscalateT;
                SoundEngine.PlaySound(SoundID.Item71 with {
                    Pitch = -0.10f + 0.62f * t,
                    Volume = 0.50f,
                }, Projectile.Center);
                if (!Main.dedServ) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center
                        , Main.rand.NextVector2Unit(), 1.6f + 2.4f * t, 5f, 9, -1f, FullName));
                    //升调后段的环斩开始切到画面本体+落刀碎面，乱舞越到后面世界被切得越狠越碎

                    if (t > 0.45f) {
                        OniFinaleFX.PushSlice(Projectile.Center, Roll, (1.8f + 2.1f * t) * SizeMul);
                        OniFinaleShatter.AddFacets(Projectile.Center, 1, SizeMul);
                    }
                }
            }
            timer++;

            if (!Main.dedServ) {
                SpawnSweepSparks();
            }

            Vector3 light = Vector3.Lerp(new Vector3(1.0f, 0.25f, 0.18f)
                , new Vector3(1.35f, 0.55f, 0.30f), EscalateT);
            Lighting.AddLight(Projectile.Center, light * 0.8f);
        }

        /// <summary>扫掠前缘火花、喷量随本帧扫掠增量走，颜色随升调红→炽橙</summary>
        private void SpawnSweepSparks() {
            if (timer > def.SweepFrames + 1) {
                return;
            }
            OFR.BladeState s = OFR.ComputeState(in def, timer);
            float prevSweep = timer > 0 ? OFR.Sweep(in def, timer - 1) : 0f;
            int count = s.Sweep - prevSweep > 0.15f ? 3 : 2;

            float edgeU = MathHelper.Clamp(s.Sweep * 1.05f, 0.06f, 0.94f);
            Vector2 pos = OFR.PointAt(in def, in s, Projectile.Center, edgeU);
            Vector2 tangent = (OFR.PointAt(in def, in s, Projectile.Center, MathHelper.Clamp(edgeU + 0.03f, 0f, 1f)) - pos)
                .SafeNormalize(Roll.ToRotationVector2());

            Color c = Color.Lerp(new Color(255, 120, 80), new Color(255, 190, 125), EscalateT);
            for (int k = 0; k < count; k++) {
                Vector2 vel = tangent * Main.rand.NextFloat(4f, 10f) + Main.rand.NextVector2Circular(1.2f, 1.2f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, c
                    , Main.rand.NextFloat(0.3f, 0.55f) * SizeMul)
                    ?.Configure(Main.rand.Next(10, 17), affectedByGravity: false);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!initialized || timer < def.DamageStart || timer > def.DamageEnd) {
                return false;
            }

            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(14, 14);

            OFR.BladeState state = OFR.ComputeState(in def, timer);
            float hitScale = MathF.Max(state.ScaleMul, 0.92f);
            float sweepU = MathHelper.Clamp(state.Sweep * 1.05f, 0f, 1f);
            float thickWorld = MathF.Max(32f, def.Thick * def.HalfX * hitScale * 1.1f);
            float spokeW = MathF.Max(40f, def.HalfX * hitScale * 0.12f);

            const int samples = 18;
            Vector2 prev = Vector2.Zero;
            bool hasPrev = false;
            float cp = 0f;
            for (int k = 0; k < samples; k++) {
                float uc = 0.05f + 0.90f * (k / (float)(samples - 1));
                if (uc > sweepU) {
                    break;
                }
                OFR.BladeState hitState = state;
                hitState.ScaleMul = hitScale;
                Vector2 mid = OFR.PointAt(in def, in hitState, Projectile.Center, uc);
                if (hasPrev && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                    , prev, mid, thickWorld, ref cp)) {
                    return true;
                }
                //辐条、立体环内侧不是空洞（罩进挥砍平面的目标）

                if (k % 2 == 0 && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                    , Projectile.Center, mid, spokeW, ref cp)) {
                    return true;
                }
                prev = mid;
                hasPrev = true;
            }
            return false;
        }

        /// <summary>割草断藤、沿揭开中的环弧 + 辐条扫切</summary>
        public override void CutTiles() {
            if (!initialized || timer < def.DamageStart || timer > def.DamageEnd) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;

            OFR.BladeState state = OFR.ComputeState(in def, timer);
            float hitScale = MathF.Max(state.ScaleMul, 0.92f);
            float sweepU = MathHelper.Clamp(state.Sweep * 1.05f, 0f, 1f);
            float width = MathF.Max(28f, def.Thick * def.HalfX * hitScale * 0.9f);
            float spokeW = MathF.Max(36f, def.HalfX * hitScale * 0.12f);

            const int samples = 12;
            Vector2 prev = Vector2.Zero;
            bool hasPrev = false;
            for (int k = 0; k < samples; k++) {
                float uc = 0.05f + 0.90f * (k / (float)(samples - 1));
                if (uc > sweepU) {
                    break;
                }
                OFR.BladeState hitState = state;
                hitState.ScaleMul = hitScale;
                Vector2 mid = OFR.PointAt(in def, in hitState, Projectile.Center, uc);
                if (hasPrev) {
                    Utils.PlotTileLine(prev, mid, width, DelegateMethods.CutTiles);
                }
                if (k % 2 == 0) {
                    Utils.PlotTileLine(Projectile.Center, mid, spokeW, DelegateMethods.CutTiles);
                }
                prev = mid;
                hasPrev = true;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            float offsetX = Projectile.To(target.Center).X;
            modifiers.HitDirectionOverride = MathF.Abs(offsetX) > 0.01f
                ? Math.Sign(offsetX)
                : (MathF.Cos(Roll) >= 0f ? 1 : -1);
            OnikiriItem.ApplySlashPenetration(target, ref modifiers);
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (CWRLoad.ExoMechAresSegments.Contains(target.type)) {
                modifiers.FinalDamage *= 0.75f;
            }
            //对双子魔眼造成1.25倍伤害
            if (target.type == NPCID.Spazmatism || target.type == NPCID.Retinazer) {
                modifiers.FinalDamage *= 1.25f;
            }
            //对塔纳托斯头造成2.85倍伤害
            if (target.type == CWRID.NPC_ThanatosHead) {
                modifiers.FinalDamage *= 2.85f;
            }
            //对星流双子造成1.66倍伤害
            if (target.type == CWRID.NPC_Apollo || target.type == CWRID.NPC_Artemis) {
                modifiers.FinalDamage *= 1.66f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.25f, Volume = 0.7f }, target.Center);

            if (Main.dedServ) {
                return;
            }
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            float t = EscalateT;
            Vector2 aim = Main.rand.NextVector2Unit();
            //每环首次命中轻量爆点,血肉可贴血 / 金属火花;重头戏留给终斩
            if (!impactDone) {
                impactDone = true;
                CrimsonImpactFX.PushAmbience(target.Center, 0.20f + 0.10f * t);
                CrimsonRendHitVFX.SpawnImpactBurst(target.Center, aim, 0.35f + 0.35f * t, SizeMul, steel);
            }
            else {
                CrimsonRendHitVFX.SpawnHitTick(target.Center, aim, SizeMul * (0.75f + 0.25f * t), steel);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>近半侧、锋利层（后效之上，不被自己的斩击切碎）</summary>
        void IOniCrispDrawable.DrawCrisp() => DrawPass(1f);

        /// <summary>远半侧、玩家绘制前回调（留在世界层被后效处理，纵深线索）</summary>
        void ICrimsonFarDrawable.DrawFarSlashes() => DrawPass(-1f);

        private void DrawPass(float farSel) {
            if (Main.dedServ || !initialized || timer >= def.Life) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OFR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }
            OFR.BladeState state = OFR.ComputeState(in def, timer);
            OFR.DrawBladeLayers(device, fx, in def, in state, Projectile.Center, farSel);
            OFR.EndDraw(device, pb, pr, pd);
        }
    }
}
