using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 门徒圣徽飞弹：携带席位徽记追向目标，命中时施加对应门徒的印记。
    /// ai[0]=模式(0约翰之眼易伤 1马太金币祝福 2巴多罗买之刃剥甲) ai[1]=目标索引
    /// </summary>
    internal class DiscipleSigilBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int Mode => (int)Projectile.ai[0];
        private int TargetIndex => (int)Projectile.ai[1];

        /// <summary>模式→徽记所属席位</summary>
        private int SigilSeat => Mode switch { 0 => 3, 1 => 7, _ => 5 };

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 100;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[TargetIndex] : null;
            if (target == null || !target.active || !target.CanBeChasedBy(Projectile)) {
                //目标失效：泄力淡出
                Projectile.velocity *= 0.92f;
                if (Projectile.timeLeft > 14) {
                    Projectile.timeLeft = 14;
                }
                return;
            }

            //曲率限幅追踪，复利续力
            Vector2 toTarget = target.Center - Projectile.Center;
            if (Projectile.velocity == Vector2.Zero) {
                Projectile.velocity = toTarget.SafeNormalize(Vector2.UnitX) * 6f;
            }
            else {
                float desired = toTarget.ToRotation();
                float current = Projectile.velocity.ToRotation();
                float turn = MathHelper.Clamp(MathHelper.WrapAngle(desired - current), -0.09f, 0.09f);
                float speed = Math.Min(Projectile.velocity.Length() * 1.045f + 0.2f, 19f);
                Projectile.velocity = Projectile.velocity.RotatedBy(turn).SafeNormalize(Vector2.UnitX) * speed;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                DiscipleDef def = DiscipleCatalog.Get(SigilSeat);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.1f
                    , def.AccentColor, Main.rand.NextFloat(0.16f, 0.28f))?.Configure(Main.rand.Next(10, 16), 0.8f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            switch (Mode) {
                case 0:
                    target.AddBuff(ModContent.BuffType<RevelationMarkDebuff>(), 300);
                    break;
                case 1:
                    target.AddBuff(ModContent.BuffType<WealthBlessingDebuff>(), 480);
                    break;
                default:
                    target.AddBuff(ModContent.BuffType<TruthRevealDebuff>(), 360);
                    break;
            }

            if (Main.dedServ) {
                return;
            }
            DiscipleDef def = DiscipleCatalog.Get(SigilSeat);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center, VaultUtils.RandVr(2f, 5f)
                    , def.AccentColor, Main.rand.NextFloat(0.5f, 0.85f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            DiscipleDef def = DiscipleCatalog.Get(SigilSeat);
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = Math.Min(Projectile.timeLeft / 14f, 1f);

            //辉光底(AlphaBlend批里A=0即加色)
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                sb.Draw(glow, drawPos, null, def.AccentColor with { A = 0 } * (0.55f * fade), 0f
                    , glow.Size() / 2f, 0.16f, SpriteEffects.None, 0f);
            }

            //徽记线稿随行
            SvgPath path = SvgPathPen.Path(def.EmblemPath);
            if (path != null) {
                SvgPathPen.Stroke(sb, path, drawPos, 8f, Projectile.rotation * 0.15f,
                    def.AccentColor with { A = 0 } * fade, 1.1f, fade,
                    core: Color.White with { A = 0 } * (0.55f * fade));
            }
            return false;
        }
    }
}
