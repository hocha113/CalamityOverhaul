using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles
{
    /// <summary>
    /// 哨兵族通用驻场判定区：火圈/蛛网带/过载电环/极光幕四样式共用一类。<br/>
    /// ai[0]=样式 ai[1]=半径（极光幕为半宽）ai[2]=持续帧（owner 按龄收尾，远端 timeLeft 兜底）。<br/>
    /// 哨兵全为固定炮台，区域生成后不移动；模式关闭当帧自灭
    /// </summary>
    internal class GsSentryZoneProj : ModProjectile
    {
        internal const int StyleFireRing = 0;
        internal const int StyleWebPatch = 1;
        internal const int StyleOverloadRing = 2;
        internal const int StyleAurora = 3;

        /// <summary>极光幕竖向高度</summary>
        private const float AuroraHeight = 160f;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        private ref float Style => ref Projectile.ai[0];
        private ref float Radius => ref Projectile.ai[1];
        private ref float Duration => ref Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }
            Age++;
            if (Age == 1f) {
                //tick 间隔按样式定：火圈/蛛网 30f，过载环/极光幕 15f（本端量，各端由 ai[0] 推得一致）
                Projectile.localNPCHitCooldown = (int)Style >= StyleOverloadRing ? 15 : 30;
            }
            Projectile.timeLeft = 30;
            if (Projectile.IsOwnedByLocalPlayer() && Age >= Duration) {
                Projectile.Kill();
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = GsSentryBurstProj.DistRectPoint(targetHitbox, Projectile.Center);
            return (int)Style switch {
                //过载电环只打外带（原版光环判定让位内圈）
                StyleOverloadRing => dist <= Radius && dist >= Radius / 1.4f,
                StyleAurora => targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center,
                    new Vector2(Radius * 2f, AuroraHeight))),
                _ => dist <= Radius,
            };
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            switch ((int)Style) {
                case StyleFireRing:
                    target.AddBuff(BuffID.OnFire, 60);
                    break;
                case StyleOverloadRing:
                    //感电标记：owner 本地量，链内其他哨兵吃加成
                    SentryGrid.MarkShocked(target);
                    break;
            }
        }

        /// <summary>范围提示：原版气泡贴图按判定区尺寸缩放画一笔（极光幕为矩形拉伸），随龄淡入淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            float env = MathHelper.Clamp(Age / 10f, 0f, 1f)
                * MathHelper.Clamp((Duration - Age) / 20f, 0f, 1f);
            if (env <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float r = MathHelper.Max(Radius, 8f);
            Vector2 scale = (int)Style == StyleAurora
                ? new Vector2(r * 2f / tex.Width, AuroraHeight / tex.Height)
                : new Vector2(r * 2f / tex.Width);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * env, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
