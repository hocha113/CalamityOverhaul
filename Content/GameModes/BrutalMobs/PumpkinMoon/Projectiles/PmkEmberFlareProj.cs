using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles
{
    /// <summary>
    /// 祭火小卒前摇火光（胸口聚火的可见载体，投火/自燃两组共用）：
    /// ai[0]=锚NPC whoAmI+1|类型&lt;&lt;8 ai[1]=模式打包（位0：0投火/1自燃；位1：突进朝左）
    /// ai[2]=前摇帧+执行帧×1000。前摇期南瓜色尘向胸口聚拢、火光渐亮（自燃组尘偏向突进反侧=蓄力读法），
    /// 自燃组执行期转为躯干拖火尘。锚定怪死亡/槽位复用即消散（击杀施法者=有效反制），
    /// 本体永不判定，联机客户端的前摇可见状态全部由本实体承载
    /// </summary>
    internal class PmkEmberFlareProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>收尾淡出帧</summary>
        private const int FadeFrames = 8;

        private static readonly Color FlareWarm = new Color(255, 150, 48);
        private static readonly Color FlareDeep = new Color(122, 44, 16);

        private int SourcePack => (int)Projectile.ai[0];
        private int AnchorIndex => (SourcePack & 255) - 1;
        private int ExpectedAnchorType => SourcePack >> 8;
        private bool Ignite => ((int)Projectile.ai[1] & 1) != 0;
        /// <summary>突进朝向（仅自燃组用于尘的蓄力偏侧）</summary>
        private float DashDir => ((int)Projectile.ai[1] & 2) != 0 ? -1f : 1f;
        private int WindupFrames => Math.Max((int)Projectile.ai[2] % 1000, 1);
        private int StrikeFrames => (int)Projectile.ai[2] / 1000;
        private int TotalLife => WindupFrames + StrikeFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        /// <summary>聚火进度 0~1（前摇期），绘制与灯光渐亮共用</summary>
        private float Charge => MathHelper.Clamp(Elapsed / (float)WindupFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        /// <summary>纯前摇载体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //寿命由已同步的 ai[2] 各端确定性展开
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.35f, Pitch = -0.45f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            //来源校验：锚定怪死亡/槽位复用即消散（index+type 双校验，槽位不是身份）
            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives() || anchor.type != ExpectedAnchorType) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center + new Vector2(0f, -4f);

            int elapsed = Elapsed;
            if (elapsed == WindupFrames && !Main.dedServ) {
                //释放/点火帧：两组不同的出手声（各端本地播放）
                SoundEngine.PlaySound(Ignite
                    ? SoundID.Item34 with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 4 }
                    : SoundID.Item1 with { Volume = 0.55f, Pitch = 0.1f, MaxInstances = 4 }, Projectile.Center);
            }

            //火光渐亮：聚火越满越亮，执行期保持
            float glow = elapsed < WindupFrames ? 0.15f + 0.5f * Charge : 0.55f;
            Lighting.AddLight(Projectile.Center, FlareWarm.ToVector3() * glow);

            if (Main.dedServ) {
                return;
            }
            if (elapsed < WindupFrames) {
                //聚拢尘：自周身汇入胸口（≤2 粒/帧）；自燃组偏向突进反侧起手=蓄力读法
                if (Main.rand.NextBool(2)) {
                    Vector2 offset = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2()
                        * new Vector2(26f, 20f) * Main.rand.NextFloat(0.6f, 1.2f);
                    if (Ignite) {
                        offset.X -= DashDir * Main.rand.NextFloat(0f, 18f);
                    }
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + offset, DustID.OrangeTorch,
                        -offset * 0.09f, 120, default, Main.rand.NextFloat(0.9f, 1.3f));
                    dust.noGravity = true;
                }
            }
            else if (Ignite && elapsed < WindupFrames + StrikeFrames && Main.rand.NextBool(2)) {
                //突进期：躯干拖火尘（≤2 粒/帧）
                Dust trail = Dust.NewDustPerfect(anchor.Center + Main.rand.NextVector2Circular(10f, 12f),
                    DustID.Torch, new Vector2(-DashDir * Main.rand.NextFloat(1f, 2.4f), -Main.rand.NextFloat(0.2f, 1f)),
                    100, default, Main.rand.NextFloat(1.1f, 1.6f));
                trail.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float strength;
            if (elapsed < WindupFrames) {
                strength = 0.35f + 0.65f * Charge;
            }
            else if (Ignite && elapsed < WindupFrames + StrikeFrames) {
                //自燃组执行期保持满档余焰
                strength = 1f;
            }
            else {
                strength = MathHelper.Clamp(1f - (elapsed - WindupFrames - StrikeFrames) / (float)FadeFrames, 0f, 1f);
            }
            if (strength <= 0.02f) {
                return false;
            }

            //NPC 锚定绘制补 gfxOffY（上坡步进补偿）
            float gfxOff = 0f;
            if (AnchorIndex.TryGetNPC(out NPC anchor) && anchor.Alives()) {
                gfxOff = anchor.gfxOffY;
            }
            Vector2 drawPos = Projectile.Center + new Vector2(0f, gfxOff) - Main.screenPosition;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);

            //真透暗芯（有遮挡像素）+ 加色辉光：聚火越满越亮
            Texture2D core = CWRAsset.Extra_98.Value;
            Main.EntitySpriteDraw(core, drawPos, null, FlareDeep * (0.6f * strength), 0f,
                core.Size() / 2f, 0.16f + 0.1f * strength, SpriteEffects.None, 0);
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glowTex, drawPos, null, (FlareWarm with { A = 0 }) * (0.55f * strength * pulse), 0f,
                glowTex.Size() / 2f, 0.34f + 0.22f * strength, SpriteEffects.None, 0);
            return false;
        }
    }
}
