using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFinaleSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using OKF = CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFlashSteps.OniKamuiFlowRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFlashSteps
{
    /// <summary>
    /// 神威疾走·墨痕：冲刺穿过敌人时缠上身的黑红细痕，纳刀帧同帧引爆成墨裂。<br/>
    /// 居合的因果链：穿过（无伤害，只留痕）→ 死寂等待 → "锵"一声所有墨痕同时裂开结算。<br/>
    /// 潜伏期细痕随目标移动、微弱脉动，引爆前 6 帧增亮增宽（可读性预告）；
    /// 引爆瞬间过曝白闪 → 墨裂沿冲刺方向定向蒸发，碎晶垂直喷出。<br/>
    /// 伤害只结算给被标记者本人（重叠敌群互不误伤，各自有各自的痕）。<br/>
    /// ai[0]=绑定NPC索引 ai[1]=引爆延迟(帧，相对生成) ai[2]=冲刺方向角(弧度)
    /// </summary>
    internal class OniFlashMark : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int RendFadeFrames = 14;   //引爆后墨裂蒸发时长
        private const int DamageWindow = 3;      //引爆帧起的伤害窗口
        private const int ForetellFrames = 6;    //引爆前增亮预告

        private bool initialized;
        private bool detonated;
        private int timer;
        private int detonateFrame;
        private float seed;
        private float brandAngle;
        private float sizeMul = 1f;
        private Vector2 lastCenter;
        private float rendHalfLen;

        private int BoundNPC => (int)Projectile.ai[0];
        private float DashAngle => Projectile.ai[2];

        /// <summary>绑定目标的存活实例，死亡/失效返回 null</summary>
        private NPC BoundInstance {
            get {
                int idx = BoundNPC;
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active ? npc : null;
            }
        }

        /// <summary>
        /// 触发接口：在持有者客户端调用（冲刺主控扫描命中时）
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="npc">被标记目标（痕随其移动）</param>
        /// <param name="detonateDelay">引爆延迟（帧）；主控传"距纳刀帧数"使全部墨痕同帧裂开</param>
        /// <param name="damage">引爆伤害</param>
        /// <param name="knockback">击退</param>
        /// <param name="dashAngle">冲刺方向角（决定墨裂走向）</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, NPC npc, int detonateDelay,
            int damage, float knockback, float dashAngle, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniFlashMark");
            return Projectile.NewProjectileDirect(source, npc.Center, Vector2.Zero
                , ModContent.ProjectileType<OniFlashMark>(), damage, knockback, player.whoAmI
                , ai0: npc.whoAmI, ai1: Math.Max(detonateDelay, 4), ai2: dashAngle);
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
            Projectile.timeLeft = 300;   //Initialize 按引爆帧重设
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;   //窗口仅数帧，单次结算
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            detonateFrame = (int)Projectile.ai[1];
            Projectile.timeLeft = detonateFrame + RendFadeFrames + 8;
            seed = Projectile.identity * 0.6180339887f % 1f;
            //痕的走向在冲刺方向上带一点确定性偏斜，敌群里不会全员平行
            brandAngle = DashAngle + (seed - 0.5f) * 0.42f;
            lastCenter = Projectile.Center;

            NPC npc = BoundInstance;
            if (npc != null) {
                lastCenter = npc.Center;
                rendHalfLen = 80f + MathF.Max(npc.width, npc.height) * 0.45f;
                sizeMul = MathHelper.Clamp(0.8f + MathF.Max(npc.width, npc.height) / 220f, 0.8f, 1.8f);
            }
            else {
                rendHalfLen = 90f;
            }
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
            }
            timer++;

            NPC npc = BoundInstance;
            if (npc != null) {
                lastCenter = npc.Center;
                Projectile.Center = lastCenter;
            }
            else if (!detonated) {
                //目标提前死亡：痕无声散去
                Fizzle();
                return;
            }

            if (!detonated && timer >= detonateFrame) {
                Detonate();
            }

            float glow = detonated ? 0.85f : 0.30f;
            Lighting.AddLight(lastCenter, new Vector3(0.75f, 0.13f, 0.11f) * glow);
        }

        /// <summary>目标死亡的兜底退场：一缕墨烟</summary>
        private void Fizzle() {
            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_CrimsonSmoke>(lastCenter + Main.rand.NextVector2Circular(10f, 10f)
                        , Main.rand.NextVector2Circular(0.8f, 0.8f) - Vector2.UnitY * 0.5f
                        , Color.White, Main.rand.NextFloat(0.05f, 0.08f))
                        ?.Configure(Main.rand.Next(14, 22), new Color(110, 24, 32), new Color(30, 14, 22));
                }
            }
            Projectile.Kill();
        }

        /// <summary>引爆：伤害窗开启 + 墨裂过曝白闪 + 碎晶垂直喷出（视觉沿冲刺方向定向蒸发）</summary>
        private void Detonate() {
            detonated = true;

            SoundEngine.PlaySound(CWRSound.MeatySlash with {
                Pitch = 0.12f + seed * 0.3f,
                Volume = 0.34f,
            }, lastCenter);

            if (Main.dedServ) {
                return;
            }
            CrimsonImpactFX.PushImpact(lastCenter, 0.015f);

            Vector2 perp = (brandAngle + MathHelper.PiOver2).ToRotationVector2();
            Vector2 along = brandAngle.ToRotationVector2();

            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(lastCenter, Vector2.Zero
                , new Color(255, 215, 195), 0.85f * sizeMul);

            int shards = 9 + (int)(seed * 4);
            for (int i = 0; i < shards; i++) {
                Vector2 vel = perp * Main.rand.NextFloat(2.5f, 7.5f) * (Main.rand.NextBool() ? 1f : -1f)
                    + along * Main.rand.NextFloat(1f, 3.5f) + Main.rand.NextVector2Circular(1.2f, 1.2f);
                Color c = Main.rand.NextBool(3) ? new Color(255, 232, 205) : new Color(255, 110, 62);
                PRTLoader.NewParticle<PRT_OniShard>(lastCenter, vel, c
                    , Main.rand.NextFloat(0.38f, 0.68f) * sizeMul)
                    ?.Configure(Main.rand.Next(20, 34), Main.rand.NextFloat(-0.22f, 0.22f)
                        , Main.rand.NextFloat(1.5f, 2.6f), affectedByGravity: true);
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(lastCenter + Main.rand.NextVector2Circular(16f, 16f)
                    , perp * Main.rand.NextFloat(-1.5f, 1.5f) + Main.rand.NextVector2Circular(0.6f, 0.6f)
                    , Color.White, Main.rand.NextFloat(0.06f, 0.11f) * sizeMul)
                    ?.Configure(Main.rand.Next(18, 28), new Color(130, 28, 36), new Color(32, 14, 22));
            }
        }

        //==================== 判定 ====================

        /// <summary>只伤被标记者本人：重叠敌群互不误伤</summary>
        public override bool? CanHitNPC(NPC target) {
            if (!detonated || target.whoAmI != BoundNPC) {
                return false;
            }
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!detonated || timer > detonateFrame + DamageWindow) {
                return false;
            }
            Vector2 along = brandAngle.ToRotationVector2() * rendHalfLen;
            float cp = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , lastCenter - along, lastCenter + along, 46f * sizeMul, ref cp);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.CWR().TimeFrozenTick = 2;
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.25f, Volume = 0.6f }, target.Center);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OKF.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }

            if (!detonated) {
                DrawBrand(device, fx);
            }
            else {
                DrawRend(device, fx);
            }

            OKF.EndDraw(device, pb, pr, pd);
        }

        /// <summary>潜伏期：缠身细痕，微脉动；引爆前 6 帧增亮增宽预告</summary>
        private void DrawBrand(GraphicsDevice device, Effect fx) {
            float pulse = 0.42f + 0.10f * MathF.Sin(timer * 0.34f + seed * 9f);
            float foretell = MathHelper.Clamp((timer - (detonateFrame - ForetellFrames)) / (float)ForetellFrames, 0f, 1f);
            float opacity = MathHelper.Lerp(pulse, 0.92f, foretell);
            //出生白闪速落
            float flash = timer <= 1 ? 0.8f : MathF.Pow(0.5f, timer - 1) * 0.8f;
            flash = MathF.Max(flash, foretell * 0.35f);

            float halfLen = rendHalfLen * 0.58f;
            Vector2 along = brandAngle.ToRotationVector2() * halfLen;
            Vector2[] pts = [lastCenter - along, lastCenter, lastCenter + along];

            OKF.RibbonDef def = new() {
                HalfWidth = (8.5f + foretell * 4.5f) * sizeMul,
                PerpOffset = 0f,
                Seed = seed,
                FlowMul = 0.85f,
                TearAmp = 0.55f,
                HeadBoost = 0.35f + foretell * 0.65f,
                OpacityMul = 1f,
            };
            OKF.DrawRibbon(device, fx, pts, in def, retract: 0f, flash: flash, opacity: opacity);
        }

        /// <summary>引爆后：全宽墨裂，过曝一拍后沿冲刺方向定向蒸发</summary>
        private void DrawRend(GraphicsDevice device, Effect fx) {
            int dt = timer - detonateFrame;
            float fadeT = MathHelper.Clamp(dt / (float)RendFadeFrames, 0f, 1f);
            float flash = MathF.Pow(0.60f, dt);
            float opacity = 1f - fadeT * fadeT * 0.4f;

            Vector2 along = brandAngle.ToRotationVector2() * rendHalfLen;
            Vector2[] pts = [lastCenter - along, lastCenter, lastCenter + along];

            OKF.RibbonDef def = new() {
                HalfWidth = 40f * sizeMul,
                PerpOffset = 0f,
                Seed = seed,
                FlowMul = 1.25f,
                TearAmp = 1.05f,
                HeadBoost = 0.9f,
                OpacityMul = 1f,
            };
            OKF.DrawRibbon(device, fx, pts, in def, retract: fadeT, flash: flash, opacity: opacity);
        }
    }
}
