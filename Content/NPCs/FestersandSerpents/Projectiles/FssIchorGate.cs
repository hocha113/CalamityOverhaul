using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles
{
    /// <summary>
    /// 灵液之门：门冲的传送门实体（本体无伤害，出口门即预告实体，生成即锁位锁向）。
    /// ai[0]=朝向（弧度，门面法向 = 蛇的进出方向）；ai[1]=模式 0 进门（吸入尘）/ 1 出口门（外溢尘）。
    /// 椭圆金涡：暗紫真透明芯占位（实体层）+ 双反转金涡层 + 底光，长轴垂直于朝向。
    /// 开门 14 帧长入，寿命末 14 帧收拢；状态提前收门时走同一收拢窗。
    /// </summary>
    internal class FssIchorGate : FssModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private float Facing => Projectile.ai[0];
        private bool IsExit => Projectile.ai[1] == 1f;
        private int Age => (int)Projectile.localAI[0];

        /// <summary>开合包络：入场 14 帧长出，末 14 帧收拢</summary>
        private float OpenT {
            get {
                float grow = MathHelper.Clamp(Age / 14f, 0f, 1f);
                float close = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
                return Math.Min(grow, close);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FssDirector.PortalGateLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.localAI[0]++;
            if (Age == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item100 with { Volume = 0.65f, Pitch = IsExit ? -0.2f : -0.45f, MaxInstances = 4 },
                    Projectile.Center);
            }

            float open = OpenT;
            if (VaultUtils.isServer || open < 0.2f) {
                return;
            }

            //门缘环流尘：沿椭圆缘游走；进门吸入、出口外溢
            Vector2 axisLong = (Facing + MathHelper.PiOver2).ToRotationVector2();
            Vector2 axisShort = Facing.ToRotationVector2();
            if (Main.rand.NextBool(2)) {
                float rimAng = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 rim = Projectile.Center
                    + axisLong * MathF.Sin(rimAng) * 62f * open
                    + axisShort * MathF.Cos(rimAng) * 26f * open;
                Vector2 vel = IsExit
                    ? (rim - Projectile.Center).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(0.8f, 2.2f)
                    : (Projectile.Center - rim).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(1.2f, 2.6f);
                Dust d = Dust.NewDustPerfect(rim, DustID.IchorTorch, vel, 0, default, Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = true;
            }
            if (Main.rand.NextBool(7)) {
                Dust drip = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f) * open,
                    DustID.Ichor, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.6f)), 40, default, Main.rand.NextFloat(0.7f, 1f));
                drip.noGravity = false;
            }
            Lighting.AddLight(Projectile.Center, FssVfx.IchorGold.ToVector3() * 0.6f * open);
        }

        public override bool PreDraw(ref Color lightColor) {
            float open = OpenT;
            if (open <= 0.02f) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float t = Main.GlobalTimeWrappedHourly;
            //椭圆：长轴垂直于朝向（门面对着进出方向）
            float rot = Facing + MathHelper.PiOver2;

            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D cyclone = CWRAsset.Cyclone.Value;
            Texture2D core = TextureAssets.Projectile[Type].Value;

            //底光（金，衬出涡层）
            Main.EntitySpriteDraw(soft, drawPos, null, FssVfx.IchorGold with { A = 0 } * (0.55f * open),
                rot, soft.Size() / 2f, new Vector2(0.62f, 1.05f) * open, SpriteEffects.None, 0);

            //暗紫芯：真透明贴图实体层（门洞的"深处"，亮背景下也有剪影）
            Main.EntitySpriteDraw(core, drawPos, null,
                FssVfx.NecroShadow with { A = 235 } * open, rot + t * 0.4f, core.Size() / 2f,
                new Vector2(0.4f, 0.72f) * open, SpriteEffects.None, 0);

            //双反转金涡（速度差 = 涡的深度读数）
            float pulse = 0.85f + 0.15f * MathF.Sin(t * 7f + Projectile.whoAmI);
            Main.EntitySpriteDraw(cyclone, drawPos, null, FssVfx.IchorGold with { A = 0 } * (0.7f * open * pulse),
                rot + t * 2.6f, cyclone.Size() / 2f, new Vector2(0.5f, 0.9f) * open, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(cyclone, drawPos, null, FssVfx.IchorBright with { A = 0 } * (0.5f * open),
                rot - t * 1.8f, cyclone.Size() / 2f, new Vector2(0.34f, 0.62f) * open, SpriteEffects.None, 0);

            //芯点高光
            Main.EntitySpriteDraw(soft, drawPos, null, FssVfx.IchorBright with { A = 0 } * (0.5f * open * pulse),
                rot, soft.Size() / 2f, new Vector2(0.2f, 0.34f) * open, SpriteEffects.None, 0);
            return false;
        }
    }
}
