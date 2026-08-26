using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 安德鲁·渔夫(席位1)：圣光渔网。
    /// 向敌人最密集处撒下光网束缚场，网中敌人被持续拖缓
    /// </summary>
    internal class Andrew : BaseDisciple
    {
        public override int Seat => 1;

        private const float CastRange = 460f;

        protected override bool TryCast() => FindClusterCenter() != Vector2.Zero;

        protected override void ExecuteAbility() {
            SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.7f, Pitch = 0.4f }, Projectile.Center);
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 center = FindClusterCenter();
            if (center == Vector2.Zero) {
                return;
            }
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), center, Vector2.Zero,
                ModContent.ProjectileType<AndrewNetField>(), 0, 0f, Projectile.owner);
        }

        /// <summary>敌群重心：取射程内敌人的簇中心(以最近敌为锚)</summary>
        private Vector2 FindClusterCenter() {
            int anchor = -1;
            float closest = CastRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Owner.Center);
                if (dist < closest) {
                    closest = dist;
                    anchor = i;
                }
            }
            if (anchor < 0) {
                return Vector2.Zero;
            }

            Vector2 sum = Vector2.Zero;
            int count = 0;
            Vector2 anchorPos = Main.npc[anchor].Center;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(Projectile)
                    || Vector2.Distance(npc.Center, anchorPos) > 180f) {
                    continue;
                }
                sum += npc.Center;
                count++;
            }
            return count > 0 ? sum / count : anchorPos;
        }
    }

    /// <summary>
    /// 圣光渔网束缚场：展开的光网笼罩一片区域，网中敌人被拖缓。
    /// 减速在各端一致施加(服务器权威自然覆盖)
    /// </summary>
    internal class AndrewNetField : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const float Radius = 150f;
        private const int ExpandTime = 16;
        private const int HoldTime = 210;
        private const int FadeTime = 20;
        private const int TotalLife = ExpandTime + HoldTime + FadeTime;

        private int Timer => TotalLife - Projectile.timeLeft;

        private float Envelope {
            get {
                if (Timer < ExpandTime) {
                    return VaultUtils.EaseOutCubic(Timer / (float)ExpandTime);
                }
                if (Timer > TotalLife - FadeTime) {
                    return 1f - (Timer - (TotalLife - FadeTime)) / (float)FadeTime;
                }
                return 1f;
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override void AI() {
            float envelope = Envelope;
            float radius = Radius * envelope;

            //网中拖缓：各端一致写入，服务器权威自然收束
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.boss || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radius * radius) {
                    continue;
                }
                npc.velocity *= 0.88f;
                if (!Main.dedServ && Main.rand.NextBool(14)) {
                    PRTLoader.NewParticle<PRT_Light>(npc.Center + Main.rand.NextVector2Circular(12f, 12f)
                        , Vector2.Zero, new Color(160, 216, 255), 0.2f)?.Configure(10, 0.7f);
                }
            }

            Lighting.AddLight(Projectile.Center, 0.25f * envelope, 0.4f * envelope, 0.5f * envelope);
        }

        public override bool PreDraw(ref Color lightColor) {
            float envelope = Envelope;
            if (envelope < 0.02f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float radius = Radius * envelope;
            DiscipleDef def = DiscipleCatalog.Get(1);

            //边界光环
            ShockRingDraw.Draw(sb, Projectile.Center, radius, 6f,
                def.AccentColor, def.BodyColor, new Color(40, 70, 100),
                envelope * 0.7f, innerGlow: 0.2f, timeSeed: Projectile.identity * 0.211f);

            //光网经纬：旋转的交叉光线束，弦线截取圆域
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return false;
            }
            float weaveRot = Main.GlobalTimeWrappedHourly * 0.14f;
            Color threadColor = def.AccentColor with { A = 0 } * (0.32f * envelope);
            for (int axis = 0; axis < 2; axis++) {
                float rot = weaveRot + axis * MathHelper.PiOver2 + MathHelper.PiOver4;
                Vector2 dir = rot.ToRotationVector2();
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                for (int i = -3; i <= 3; i++) {
                    float offset = i * radius * 0.26f;
                    float half = MathF.Sqrt(MathF.Max(radius * radius - offset * offset, 0f));
                    if (half < 6f) {
                        continue;
                    }
                    Vector2 a = center + perp * offset - dir * half;
                    sb.Draw(px, a, new Rectangle(0, 0, 1, 1), threadColor, rot,
                        Vector2.Zero, new Vector2(half * 2f, 1.4f), SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
