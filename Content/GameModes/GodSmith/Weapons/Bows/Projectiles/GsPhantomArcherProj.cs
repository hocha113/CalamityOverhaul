using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles
{
    /// <summary>
    /// 幻影弓「猎魂标」召来的幻影射手：悬于射手后上方，用原版幻影弓物品贴图默认绘制，
    /// 随 owner 举弓角朝向。本体无伤害；开火由方案在 owner 端射击流里驱动（生成 25% 幻影箭）。
    /// 至多 1 名/玩家，时长可被连击续满
    /// </summary>
    internal class GsPhantomArcherProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Phantasm;

        private ref float Life => ref Projectile.localAI[1];

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 44;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            Life++;
            //锚定射手后上方，缓动跟随
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 34f, -50f);
            Projectile.Center = Life <= 1f ? anchor : Vector2.Lerp(Projectile.Center, anchor, 0.18f);
            //默认绘制的朝向：随 owner 举弓角，待机时微微下垂
            Projectile.rotation = owner.itemAnimation > 0 ? owner.itemRotation : owner.direction * -0.22f;
            Projectile.spriteDirection = owner.direction;
        }
    }
}
