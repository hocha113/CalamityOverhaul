using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 三连爪撕：沙漠虎的伏杀仪式在猎物身上留下的三道风沙爪痕。跟随目标，
    /// 节奏 = 第 0/8/16 帧各落一道爪痕（三平行沙刃撕开，各结算一段伤害），
    /// 24 帧后进入 16 帧沙散收尾（爪痕风化剥落，沙粒滑坠，无伤害）。
    /// ai[0] = 目标索引，ai[1] = 目标类型校验。材质：荒漠流沙刃 + 兽爪风压
    /// </summary>
    internal class GsStormTigerRendProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private static readonly Color SandAmber = new(232, 186, 108);
        private static readonly Color SandDeep = new(150, 108, 56);
        private static readonly Color WindPale = new(250, 240, 214);

        private const int TearGap = 8;
        private const int TearCount = 3;
        private const int RendFrames = TearGap * TearCount;
        private const int SettleFrames = 16;
        private const int TotalFrames = RendFrames + SettleFrames;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Rending => Elapsed < RendFrames;

        private float Seed => Projectile.identity * 0.9127f % MathHelper.TwoPi;

        /// <summary>已落下的爪痕数</summary>
        private int TearsBorn => Math.Min(Elapsed / TearGap + 1, TearCount);

        private NPC BoundTarget {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active && npc.type == (int)Projectile.ai[1] ? npc : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //一爪一段
            Projectile.localNPCHitCooldown = TearGap;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            //撕扯期跟住目标；沙散期留在原地风化
            if (Rending) {
                NPC target = BoundTarget;
                if (target == null) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = target.Center;
            }
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, SandAmber.ToVector3() * 0.22f);
            //每道爪痕落下：破风声 + 沙瀑迸溅
            if (Rending && Elapsed % TearGap == 0) {
                int idx = Elapsed / TearGap;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Volume = 0.7f,
                    Pitch = -0.2f + idx * 0.15f
                }, Projectile.Center);
                float tearAng = TearAngle(idx);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                        tearAng.ToRotationVector2().RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f))
                            * Main.rand.NextFloat(2.5f, 5.5f),
                        Main.rand.NextBool() ? SandAmber : SandDeep,
                        Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(12, 20));
                }
            }
            //沙散相：沙粒滑坠
            if (!Rending && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(26f, 26f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.8f, 1.8f)),
                    SandDeep, Main.rand.NextFloat(0.16f, 0.26f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }

        /// <summary>第 idx 道爪痕的走向（identity 定相，三道交错）</summary>
        private float TearAngle(int idx)
            => Seed * 0.2f - 0.6f + idx * 0.5f - (idx == 1 ? 1.35f : 0f);

        /// <summary>只有撕扯期结算伤害</summary>
        public override bool? CanDamage() => Rending ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Utils.CenteredRectangle(Projectile.Center, new Vector2(84f, 84f))
                .Intersects(targetHitbox);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = 0.25f },
                Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float settleFade = Rending
                ? 1f : MathHelper.Clamp(Projectile.timeLeft / (float)SettleFrames, 0f, 1f);

            //已落下的每道爪痕：三平行沙刃（暗缘垫底 + 亮沙芯），随存活时间风化变淡
            for (int idx = 0; idx < TearsBorn; idx++) {
                int age = Elapsed - idx * TearGap;
                if (age < 0) {
                    continue;
                }
                //落痕瞬间快速划出（4 帧内长度展开），随后缓慢风化
                float grow = MathHelper.Clamp(age / 4f, 0f, 1f);
                float wear = MathHelper.Clamp(1f - age / 34f, 0.25f, 1f) * settleFade;
                float ang = TearAngle(idx);
                Vector2 slide = ang.ToRotationVector2();
                for (int lane = -1; lane <= 1; lane++) {
                    Vector2 laneOff = (ang + MathHelper.PiOver2).ToRotationVector2()
                        * lane * 9f + slide * lane * 4f;
                    float laneLen = (lane == 0 ? 56f : 42f) * grow;
                    //暗缘
                    Main.EntitySpriteDraw(soft, pos + laneOff, null, SandDeep * (0.8f * wear),
                        ang, soft.Size() / 2f,
                        new Vector2(laneLen / soft.Width, 5f / soft.Height), SpriteEffects.None, 0);
                    //亮沙芯（加色）
                    Main.EntitySpriteDraw(soft, pos + laneOff, null,
                        (SandAmber with { A = 0 }) * (0.75f * wear), ang, soft.Size() / 2f,
                        new Vector2(laneLen * 0.8f / soft.Width, 2.2f / soft.Height),
                        SpriteEffects.None, 0);
                    //爪尖风压白（划出端）
                    Main.EntitySpriteDraw(soft, pos + laneOff + slide * (laneLen * 0.5f), null,
                        (WindPale with { A = 0 }) * (0.55f * wear * grow), ang,
                        soft.Size() / 2f, new Vector2(10f / soft.Width, 1.6f / soft.Height),
                        SpriteEffects.None, 0);
                }
            }
            //沙尘底光
            Main.EntitySpriteDraw(glow, pos, null,
                (SandAmber with { A = 0 }) * (0.28f * settleFade), 0f, glow.Size() / 2f,
                0.8f, SpriteEffects.None, 0);
            return false;
        }
    }
}
