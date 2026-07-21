using CalamityOverhaul.Common;
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

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs
{
    /// <summary>
    /// 绯红裂空·断斩：从连段中移植出的独立直线斩击（原"交叉裂斩"），供斩切类效果复用。<br/>
    /// 白热中脊直线刀刃，三层异步 + 全形白闪 + 彗星尾蒸发，世界锚定（不跟随玩家），
    /// 单发或经 <see cref="FireCross"/> 成对交叉释放。<br/>
    /// ai[0]=刃方向角(弧度) ai[1]=扫掠镜像(±1，决定从刃的哪端扫向另一端) ai[2]=尺寸倍率
    /// </summary>
    internal class CrimsonRendCleave : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 26;

        private SlashDef def;
        private bool initialized;
        private int timer;

        private float BladeAngle => Projectile.ai[0];
        private float Flip => Projectile.ai[1] < 0f ? -1f : 1f;
        private float SizeMul => Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f;

        /// <summary>
        /// 触发接口：在持有者客户端调用，世界锚定于 center（适合"斩切标记"式演出）
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="center">刀刃中心（世界坐标，生成后不追踪）</param>
        /// <param name="bladeAngle">刃方向角（弧度）</param>
        /// <param name="damage">伤害</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率（刃长/幅宽同步缩放）</param>
        /// <param name="flip">扫掠方向镜像 ±1</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 center, float bladeAngle, int damage, float knockback,
            float scale = 1f, int flip = 1, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_CrimsonRendCleave");
            return Projectile.NewProjectileDirect(source, center, Vector2.Zero
                , ModContent.ProjectileType<CrimsonRendCleave>(), damage, knockback, player.whoAmI
                , ai0: MathHelper.WrapAngle(bladeAngle), ai1: flip, ai2: scale);
        }

        /// <summary>成对交叉释放（X 型）：基准角 ± halfSpread 两道，扫掠方向相对</summary>
        public static void FireCross(Player player, Vector2 center, float aimAngle, float halfSpread,
            int damage, float knockback, float scale = 1f, IEntitySource source = null) {
            Fire(player, center, aimAngle - halfSpread, damage, knockback, scale, 1, source);
            Fire(player, center, aimAngle + halfSpread, damage, knockback, scale, -1, source);
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
            Projectile.localNPCHitCooldown = 40;   //单发只结算一次
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            float s = SizeMul;
            def = new SlashDef {
                Birth = 0, SweepFrames = 3, Life = Lifetime, ErodeStart = 7, ErodeFrames = 15,
                ColorShiftDelay = 9, ColorShiftFrames = 11, DamageStart = 0, DamageEnd = 8,
                Mode = 1f, Rot = BladeAngle, Span = 0f, Thick = 0.34f,
                HalfX = 235f * s, HalfY = 128f * s, Flip = Flip,
                Opacity = 0.95f, FrontGlow = 2.7f, OffsetAlongAim = 0f, Seed = Projectile.whoAmI * 0.173f % 1f,
                TailErode = 0.55f, FlashPower = 0.75f, FarDim = 0f,
                Ink = 0.45f, FeiBai = 0.45f, Bleed = 0.12f, SplitTail = 0.60f,
            };
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
            }
            timer++;

            //张开瞬间的轻确认
            if (timer == def.SweepFrames + 1) {
                CrimsonImpactFX.PushImpact(Projectile.Center, 0.2f);
                if (!Main.dedServ) {
                    Vector2 tip = CSR.PointAt(in def, Projectile.Center, 0.94f, timer);
                    for (int i = 0; i < 6; i++) {
                        Vector2 vel = BladeAngle.ToRotationVector2().RotatedByRandom(0.5)
                            * Main.rand.NextFloat(4f, 11f) * SizeMul;
                        PRTLoader.NewParticle<PRT_CrimsonSpark>(tip, vel, new Color(255, 130, 90)
                            , Main.rand.NextFloat(0.35f, 0.65f) * SizeMul)
                            ?.Configure(Main.rand.Next(12, 20), affectedByGravity: false);
                    }
                }
            }

            //扫掠期前缘火花
            if (!Main.dedServ && timer <= def.SweepFrames + 1) {
                float edgeU = MathHelper.Clamp(CSR.Sweep(in def, timer) * 1.05f, 0.06f, 0.94f);
                Vector2 pos = CSR.PointAt(in def, Projectile.Center, edgeU, timer);
                Vector2 tangent = (CSR.PointAt(in def, Projectile.Center, MathHelper.Clamp(edgeU + 0.03f, 0f, 1f), timer) - pos)
                    .SafeNormalize(BladeAngle.ToRotationVector2());
                for (int k = 0; k < 2; k++) {
                    Vector2 vel = tangent * Main.rand.NextFloat(4f, 10f) + Main.rand.NextVector2Circular(1f, 1f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 120, 80)
                        , Main.rand.NextFloat(0.3f, 0.55f) * SizeMul)
                        ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
                }
            }

            //屏幕 Bloom 轻推
            float bloom = 0.18f * (1f - MathHelper.Clamp((timer - Lifetime + 10) / 10f, 0f, 1f));
            CrimsonImpactFX.PushAmbience(Projectile.Center, bloom);

            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.16f, 0.12f));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!initialized || timer < def.DamageStart || timer > def.DamageEnd) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(14, 14);
            float sweepU = MathHelper.Clamp(CSR.Sweep(in def, timer) * 1.05f, 0f, 1f);
            Vector2 head = CSR.PointAt(in def, Projectile.Center, 0.05f, timer);
            Vector2 tail = CSR.PointAt(in def, Projectile.Center, MathF.Min(0.95f, sweepU), timer);
            float cp = 0f;
            float thick = MathF.Max(28f, def.HalfY * 0.85f);
            return Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                , head, tail, thick, ref cp);
        }

        /// <summary>割草断藤：沿直线刃扫切</summary>
        public override void CutTiles() {
            if (!initialized || timer < def.DamageStart || timer > Math.Max(def.DamageEnd, def.SweepFrames)) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            float sweepU = MathHelper.Clamp(CSR.Sweep(in def, timer) * 1.05f, 0f, 1f);
            Vector2 head = CSR.PointAt(in def, Projectile.Center, 0.05f, timer);
            Vector2 tail = CSR.PointAt(in def, Projectile.Center, MathF.Min(0.95f, sweepU), timer);
            Utils.PlotTileLine(head, tail, MathF.Max(24f, def.HalfY * 0.8f), DelegateMethods.CutTiles);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.CWR().TimeFrozenTick = 4;
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            SoundEngine.PlaySound((steel ? SoundID.NPCHit4 : SoundID.NPCHit1) with {
                Pitch = steel ? -0.05f : -0.2f,
                Volume = 0.7f
            }, target.Center);

            CrimsonRendHitVFX.SpawnHitTick(target.Center, BladeAngle.ToRotationVector2(), SizeMul, steel);
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
