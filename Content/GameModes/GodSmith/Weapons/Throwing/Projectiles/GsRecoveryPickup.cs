using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles
{
    /// <summary>
    /// 回收体:投掷物未命中而亡时留在世上的「可捡回的那一件」。真弹幕,远端可见。<br/>
    /// ai[0]=返还物品 ID;ai[1]=1 强制磁吸(回收体超员时被挤出的最旧一颗)。<br/>
    /// 300px 内磁吸向主人,12s 超时;触碰返还只在 owner 端结算(客户端权威背包)
    /// </summary>
    internal class GsRecoveryPickup : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int ItemType => (int)Projectile.ai[0];
        private bool ForcePull => Projectile.ai[1] == 1f;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 720;
            Projectile.netImportant = true;
        }

        public override bool? CanHitNPC(NPC target) => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            bool pulling = false;
            if (owner.active && !owner.dead) {
                float dist = Projectile.Distance(owner.Center);
                if (ForcePull || dist <= 300f) {
                    //磁吸相:越近越快,穿墙回手
                    pulling = true;
                    float speed = MathHelper.Lerp(6f, 13f, 1f - MathHelper.Clamp(dist / 300f, 0f, 1f));
                    Vector2 want = (owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.16f);
                    Projectile.tileCollide = false;
                    Projectile.rotation += 0.22f;
                }
            }
            if (!pulling) {
                //落地体:重力,触地立住
                Projectile.velocity.Y += 0.22f;
                if (Projectile.velocity.Y > 10f) {
                    Projectile.velocity.Y = 10f;
                }
                Projectile.velocity.X *= 0.985f;
                Projectile.rotation += Projectile.velocity.X * 0.04f;
                Projectile.tileCollide = true;
            }
            Lighting.AddLight(Projectile.Center, GsThrowScheme.GsGold.ToVector3() * 0.2f);
            //待拾金尘:低频上飘
            if (!VaultUtils.isServer && Main.rand.NextBool(22)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 0.9f),
                    GsThrowScheme.GsGold, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, 14);
            }
            //触碰返还:owner 权威,写自己背包
            if (Projectile.owner == Main.myPlayer && owner.active && !owner.dead
                && Projectile.Hitbox.Intersects(owner.Hitbox)) {
                owner.GiveItem(Projectile.GetSource_FromThis(), ItemType, 1);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.7f }, Projectile.Center);
                }
                Projectile.Kill();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //落地不消亡,立住等待磁吸
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //收取/消散余痕:金尘上飘(各端可见)
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.8f, 1.8f)),
                    Main.rand.NextBool() ? GsThrowScheme.GsGold : GsThrowScheme.GsGoldPale,
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(false, 18);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int itemType = ItemType;
            if (itemType <= 0) {
                return false;
            }
            //将逝 2s:闪烁提示
            if (Projectile.timeLeft < 120 && Projectile.timeLeft / 6 % 2 == 0) {
                return false;
            }
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //呼吸相位用 identity 定种,绘制路径不掷随机
            float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity * 0.83f);
            //金色 shimmer 重影(加色 A=0)垫底
            Color glow = GsThrowScheme.GsGold * (0.55f * pulse);
            glow.A = 0;
            Main.EntitySpriteDraw(tex, pos, null, glow, Projectile.rotation, origin, 1.16f, SpriteEffects.None, 0);
            //物品本体
            Main.EntitySpriteDraw(tex, pos, null, lightColor, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            //亮芯
            Color core = GsThrowScheme.GsGoldPale * (0.25f * pulse);
            core.A = 0;
            Main.EntitySpriteDraw(tex, pos, null, core, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
