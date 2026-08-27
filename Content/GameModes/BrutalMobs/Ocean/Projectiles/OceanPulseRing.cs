using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ocean.Projectiles
{
    /// <summary>
    /// 水母电场脉冲环（预告实体+判定载体一体）。ai[0]=来源打包（whoAmI+1 | type&lt;&lt;8）
    /// ai[1]=Pack(变体,档位索引)。
    /// 可见环=判定环：绘制与 <see cref="Colliding"/> 读同一 <see cref="Radius"/>（具名安全半径，环外恒安全）；
    /// 充能期（≥40 帧，档位一律不缩短）环由内向外张到判定半径、恒无判定；
    /// 只有拍窗（<see cref="BeatWindow"/> 帧）环面闪放时才有伤害（伤害窗=可见闪光窗），
    /// 绿水母两连拍间隔 <see cref="BeatGap"/>。判定在受击端本地按同一确定性时间轴展开
    /// （敌对弹幕对玩家的命中只在受害者本机结算），每个受击者的可见窗与伤害窗自洽。
    /// 充能与拍间宿主死亡/槽位复用即取消（击杀施法者=有效反制，未发之拍随之作废）。
    /// 原版水母受击反电机制不受本层影响（叠加层不改原版行为）
    /// </summary>
    internal class OceanPulseRing : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int VariantBlue = 0;
        internal const int VariantPink = 1;
        internal const int VariantGreen = 2;

        /// <summary>脉冲半径（按变体）：绘制与判定共用的具名安全半径，环外恒安全</summary>
        private static readonly float[] RadiusByVariant = [120f, 150f, 112f];
        /// <summary>充能帧数（按变体）＝预告期，公平契约 ≥30，各档位一律不缩短</summary>
        private static readonly int[] ChargeByVariant = [40, 48, 40];
        /// <summary>脉冲拍数（按变体）：绿水母两连拍</summary>
        private static readonly int[] BeatsByVariant = [1, 1, 2];
        /// <summary>连拍间隔帧（绿水母第二拍延后量）</summary>
        internal const int BeatGap = 30;
        /// <summary>单拍判定窗帧数（伤害窗=闪光可见窗）</summary>
        private const int BeatWindow = 6;
        /// <summary>末拍后的消散帧</summary>
        private const int FadeFrames = 14;
        /// <summary>半径的档位增量（每级；可见环同步变大，判定=可见不破坏）</summary>
        internal const int RadiusStepPerTier = 8;
        /// <summary>环缘节点数（绘制用）</summary>
        private const int RingNodes = 14;

        /// <summary>变体色（差异主体在半径/充能/拍数，颜色只是辨识辅助）</summary>
        private static readonly Color[] VariantTint = [
            new Color(110, 190, 255),
            new Color(255, 150, 215),
            new Color(150, 255, 175),
        ];

        internal static float Pack(int variant, int tierIndex)
            => variant | (Math.Clamp(tierIndex, 0, 3) << 2);

        /// <summary>NPC 侧的定身持续帧：充能+全部拍窗（与实体时间轴同源，单一事实）</summary>
        internal static int NpcHoldFrames(int variant)
            => ChargeByVariant[variant] + (BeatsByVariant[variant] - 1) * BeatGap + BeatWindow;

        /// <summary>某变体在某档位的判定半径（NPC 触发距离与实体判定/绘制同读此处，单一事实）</summary>
        internal static float RadiusFor(int variant, int tierIndex)
            => RadiusByVariant[variant] + RadiusStepPerTier * Math.Clamp(tierIndex, 0, 3);

        private int Variant => (int)Projectile.ai[1] & 3;
        private int TierIndex => ((int)Projectile.ai[1] >> 2) & 3;
        /// <summary>判定半径＝可见环半径（同一读处）</summary>
        private float Radius => RadiusFor(Variant, TierIndex);
        private int ChargeFrames => ChargeByVariant[Variant];
        private int Beats => BeatsByVariant[Variant];
        private int TotalLife => NpcHoldFrames(Variant) + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>当前是否处于某一拍的判定窗内（取消后恒否）</summary>
        private bool InBeat {
            get {
                if (Cancelled) {
                    return false;
                }
                int sinceCharge = Elapsed - ChargeFrames;
                for (int b = 0; b < Beats; b++) {
                    int local = sinceCharge - b * BeatGap;
                    if (local >= 0 && local < BeatWindow) {
                        return true;
                    }
                }
                return false;
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;//伤害经由拍窗判定；窗外 CanDamage 恒假
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 130;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>伤害窗=拍窗：充能与拍间恒无判定（不清零 damage，各端本地确定性开窗）</summary>
        public override bool? CanDamage() => InBeat ? null : false;

        /// <summary>拍窗按圆判定：判定半径与可见环同一 <see cref="Radius"/></summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!InBeat) {
                return false;
            }
            Vector2 nearest = new Vector2(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(Projectile.Center, nearest) <= Radius * Radius;
        }

        private bool TryHost(out NPC host) {
            host = null;
            int packed = (int)Projectile.ai[0];
            int src = (packed & 255) - 1;
            if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                || Main.npc[src].type != packed >> 8) {
                return false;
            }
            host = Main.npc[src];
            return true;
        }

        public override void AI() {
            //首帧定死时间轴（两端以同一 ai 值各自展开；timeLeft 不进同步包）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
            }

            //来源校验：宿主死亡/槽位复用即取消，未发之拍作废（失败方向=安全方向）
            bool hostValid = TryHost(out NPC host);
            if (!Cancelled && Elapsed < TotalLife - FadeFrames && !hostValid) {
                Cancelled = true;
            }
            if (hostValid && !Cancelled) {
                Projectile.Center = host.Center;//环随宿主（各端从同步的 NPC 位置确定性推得）
            }

            if (Main.dedServ) {
                return;
            }

            int sinceCharge = Elapsed - ChargeFrames;
            float chargeT = MathHelper.Clamp(Elapsed / (float)ChargeFrames, 0f, 1f);
            Color tint = VariantTint[Variant];
            Lighting.AddLight(Projectile.Center, tint.ToVector3() * (Cancelled ? 0.1f : 0.15f + 0.45f * chargeT));

            if (Cancelled) {
                return;
            }

            //拍起始帧：各端本地按同一确定性倒数播放（放电闪+环缘电尘標出精确判定半径）
            for (int b = 0; b < Beats; b++) {
                if (sinceCharge == b * BeatGap) {
                    SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.55f, Pitch = 0.2f - 0.1f * b, MaxInstances = 4 },
                        Projectile.Center);
                    for (int i = 0; i < 14; i++) {
                        float ang = MathHelper.TwoPi * i / 14f + Main.rand.NextFloat(0.2f);
                        Dust arc = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * Radius,
                            DustID.Electric, ang.ToRotationVector2() * Main.rand.NextFloat(0.2f, 1f),
                            80, default, Main.rand.NextFloat(0.8f, 1.2f));
                        arc.noGravity = true;
                    }
                }
            }

            //充能期体表微电（≤1 粒/帧），电尘沿导引环缘走，读作半径正在张开
            if (Elapsed < ChargeFrames && Main.rand.NextBool(2)) {
                float guideR = Radius * (0.25f + 0.75f * chargeT);
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Dust spark = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * guideR,
                    DustID.Electric, Vector2.Zero, 120, default, 0.7f);
                spark.noGravity = true;
                spark.velocity = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 0.8f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float chargeT = MathHelper.Clamp(elapsed / (float)ChargeFrames, 0f, 1f);
            float fade;
            if (Cancelled) {
                fade = 0.35f * MathHelper.Clamp(1f - elapsed / (float)TotalLife, 0f, 1f);
            }
            else if (elapsed >= TotalLife - FadeFrames) {
                fade = MathHelper.Clamp((TotalLife - elapsed) / (float)FadeFrames, 0f, 1f);
            }
            else {
                fade = MathHelper.Clamp(elapsed / 10f, 0f, 1f);
            }
            if (fade <= 0.01f) {
                return false;
            }

            //导引半径：充能期由内向外张开，恰在充能结束达到判定半径并保持（可见环=判定环）
            float ringR = Radius * (0.25f + 0.75f * chargeT);
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);
            Color tint = VariantTint[Variant];
            Texture2D node = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 nodeOrig = node.Size() / 2f;
            Vector2 glowOrig = glow.Size() / 2f;
            float spin = Main.GlobalTimeWrappedHourly * 1.2f + Projectile.identity * 0.7f;

            //环缘电极节点：真 alpha 垫底（实体锚点），加色亮点叠上
            for (int i = 0; i < RingNodes; i++) {
                float ang = MathHelper.TwoPi * i / RingNodes + spin;
                Vector2 pos = center + ang.ToRotationVector2() * ringR;
                Main.EntitySpriteDraw(node, pos, null, tint * (0.40f * fade), ang, nodeOrig,
                    new Vector2(0.12f, 0.05f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, (tint with { A = 0 }) * (0.35f * fade * pulse),
                    0f, glowOrig, 0.035f + 0.015f * chargeT, SpriteEffects.None, 0);
            }

            //宿主体光渐亮（充能读数）
            Main.EntitySpriteDraw(glow, center, null, (tint with { A = 0 }) * (fade * (0.12f + 0.38f * chargeT) * pulse),
                0f, glowOrig, 0.16f + 0.10f * chargeT, SpriteEffects.None, 0);

            //拍窗放电闪：满半径光盘一闪即收。
            //豁免声明：放电层属闪电/纯光类，按 M5 豁免遮挡像素要求走纯加色；实体感由真 alpha 节点层承担
            if (InBeat) {
                int sinceCharge = elapsed - ChargeFrames;
                int local = sinceCharge % BeatGap;
                float flash = 1f - local / (float)BeatWindow;
                float scale = Radius * 2f / glow.Width;
                Main.EntitySpriteDraw(glow, center, null, (tint with { A = 0 }) * (0.55f * flash),
                    0f, glowOrig, scale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, center, null, (Color.White with { A = 0 }) * (0.35f * flash),
                    0f, glowOrig, scale * 0.55f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
