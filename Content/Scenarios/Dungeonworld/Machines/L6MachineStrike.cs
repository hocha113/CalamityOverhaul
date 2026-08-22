using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Machines
{
    /// <summary>
    /// L6 铸造层机器的一次行程：活塞下捶 / 碾轮横扫。<br/>
    /// 上膛段不致伤，只出声出尘把"要砸了"喊出来（§机关必须可读，公平性）；
    /// 行程段才咬人。预警与伤害压在同一个实体里，各端就不必对"机器相位"达成一致
    /// 弹幕生成包一发过去，看到的和挨打的自然对齐。<br/>
    /// ai[0]=形态（0 活塞 / 1 碾轮）· ai[1]=行程长度（格）
    /// </summary>
    internal class L6MachineStrike : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //活塞：上膛抬升→急坠→顶住→回抽。慢起快落才有"蓄力砸下"的重量
        private const int PistonWind = 30;
        private const int PistonSlam = 8;
        private const int PistonHold = 6;
        private const int PistonBack = 24;
        //碾轮：探头→匀速碾过→退场
        private const int RollerWind = 34;
        private const int RollerSweep = 46;
        private const int RollerExit = 12;

        //铸铁三色：暗部/本体/受光棱，锈橙是本层强调色（与 L6Palette 层染同族）
        private static readonly Color IronDark = new(24, 26, 32);
        private static readonly Color IronBody = new(58, 62, 72);
        private static readonly Color IronLit = new(104, 110, 124);
        private static readonly Color RustHot = new(196, 96, 34);

        private ref float Life => ref Projectile.localAI[0];
        //锚点=生成位（已随生成包同步），各端据此算出同一条行程
        private ref float AnchorX => ref Projectile.localAI[1];
        private ref float AnchorY => ref Projectile.localAI[2];

        private bool IsRoller => Projectile.ai[0] >= 0.5f;
        //ai[1] 带符号：绝对值是行程格数，正负是碾轮从哪一头滚过来（活塞恒为正=向下）
        private float TravelPixels => MathF.Abs(Projectile.ai[1]) * 16f;
        private int SweepDir => Projectile.ai[1] < 0f ? -1 : 1;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 48;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = PistonWind + PistonSlam + PistonHold + PistonBack;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            if (Life == 0f) {
                AnchorX = Projectile.Center.X;
                AnchorY = Projectile.Center.Y;
                if (IsRoller) {
                    Projectile.width = 44;
                    Projectile.height = 44;
                    Projectile.timeLeft = RollerWind + RollerSweep + RollerExit;
                }
                SoundEngine.PlaySound(
                    SoundID.Mech with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 4 },
                    Projectile.Center);
            }
            Life++;

            if (IsRoller) {
                RollerStep();
            }
            else {
                PistonStep();
            }
        }

        //==================== 行程 ====================

        private void PistonStep() {
            float head;
            if (Life <= PistonWind) {
                //上膛：先微微上抬再抖，抬得越高砸得越狠的读感
                float t = Life / PistonWind;
                head = -6f * MathF.Sin(t * MathHelper.Pi) + MathF.Sin(Life * 1.7f) * (1.2f * t);
                WindDust(t);
            }
            else if (Life <= PistonWind + PistonSlam) {
                //急坠：三次方入速，末速最大
                float t = (Life - PistonWind) / PistonSlam;
                head = TravelPixels * (t * t * t);
                if (Life == PistonWind + PistonSlam) {
                    Impact(new Vector2(AnchorX, AnchorY + TravelPixels));
                }
            }
            else if (Life <= PistonWind + PistonSlam + PistonHold) {
                head = TravelPixels;
            }
            else {
                //回抽：慢，机器"喘一口"
                float t = (Life - PistonWind - PistonSlam - PistonHold) / PistonBack;
                head = TravelPixels * (1f - t * t);
            }
            Projectile.Center = new Vector2(AnchorX, AnchorY + head);
        }

        private void RollerStep() {
            float travel;
            if (Life <= RollerWind) {
                //探头：从帧外滚进来一截，先露齿
                float t = Life / RollerWind;
                travel = -Projectile.width * (1f - t);
                WindDust(t);
            }
            else if (Life <= RollerWind + RollerSweep) {
                float t = (Life - RollerWind) / RollerSweep;
                travel = TravelPixels * t;
                if (Life % 7 == 0) {
                    Impact(Projectile.Bottom);
                }
            }
            else {
                float t = (Life - RollerWind - RollerSweep) / RollerExit;
                travel = TravelPixels + Projectile.width * t;
            }
            Projectile.Center = new Vector2(AnchorX + travel * SweepDir, AnchorY);
            Projectile.rotation += 0.11f * SweepDir;
        }

        //上膛期只有伤害是关的，声画照常，这就是"读得出来"的那半秒
        private void WindDust(float t) {
            if (Main.dedServ || Main.rand.NextFloat() > 0.25f + t * 0.35f) {
                return;
            }
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                DustID.Iron, Scale: Main.rand.NextFloat(0.6f, 1.0f));
            dust.noGravity = true;
            dust.velocity = Main.rand.NextVector2Circular(0.8f, 0.8f);
            dust.color = IronLit;
        }

        private void Impact(Vector2 at) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.85f, Pitch = -0.65f, MaxInstances = 3 }, at);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.9f, MaxInstances = 3 }, at);
            for (int i = 0; i < 10; i++) {
                //铁屑贴地横飞，不是四散烟花
                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(Projectile.width * 0.4f, 4f),
                    Main.rand.NextBool(3) ? DustID.Torch : DustID.Iron,
                    new Vector2(Main.rand.NextFloat(-3.2f, 3.2f), Main.rand.NextFloat(-1.6f, -0.2f)),
                    Scale: Main.rand.NextFloat(0.7f, 1.3f));
                dust.noGravity = Main.rand.NextBool();
            }
            //震屏随距离衰减:一层几十台机器,隔着半个层带砸也把镜头晃了会很廉价
            float dist = Vector2.Distance(Main.LocalPlayer.Center, at);
            if (dist < 40f * 16f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(2.6f * (1f - dist / (40f * 16f)));
            }
        }

        //==================== 伤害窗口 ====================

        //上膛不咬人：预警与伤害同体，靠这一条把两段切开
        public override bool CanHitPlayer(Player target) => IsRoller
            ? Life > RollerWind && Life <= RollerWind + RollerSweep
            : Life > PistonWind && Life <= PistonWind + PistonSlam + PistonHold;

        //==================== 绘制：铸铁机件（杆+头 / 滚轮+辐条）====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            if (IsRoller) {
                DrawRoller(sb, px);
            }
            else {
                DrawPiston(sb, px);
            }
            return false;
        }

        private void DrawPiston(SpriteBatch sb, Texture2D px) {
            Vector2 head = Projectile.Center - Main.screenPosition;
            Vector2 anchor = new Vector2(AnchorX, AnchorY) - Main.screenPosition;
            float rodLen = MathF.Max(head.Y - anchor.Y, 0f);

            //缸杆：两道细钢柱从槽顶垂到锤头
            if (rodLen > 1f) {
                Rect(sb, px, anchor.X - 5f, anchor.Y, 3f, rodLen, IronBody);
                Rect(sb, px, anchor.X + 2f, anchor.Y, 3f, rodLen, IronBody);
            }
            //锤头：暗底压一圈，本体，顶面受光棱
            float w = Projectile.width;
            float h = Projectile.height;
            Rect(sb, px, head.X - w * 0.5f - 2f, head.Y - h * 0.5f, w + 4f, h, IronDark);
            Rect(sb, px, head.X - w * 0.5f, head.Y - h * 0.5f, w, h - 3f, IronBody);
            Rect(sb, px, head.X - w * 0.5f, head.Y - h * 0.5f, w, 3f, IronLit);
            //砸面锈棱：只在行程段发热，静止时是冷铁
            bool hot = Life > PistonWind && Life <= PistonWind + PistonSlam + PistonHold;
            Rect(sb, px, head.X - w * 0.5f, head.Y + h * 0.5f - 3f, w, 3f, hot ? RustHot : IronDark);
            if (hot) {
                Glow(sb, head + new Vector2(0f, h * 0.5f), w * 1.1f, 10f, 0.5f);
            }
        }

        private void DrawRoller(SpriteBatch sb, Texture2D px) {
            Vector2 c = Projectile.Center - Main.screenPosition;
            float r = Projectile.width * 0.5f;
            //轮体：暗底 + 本体
            Rect(sb, px, c.X - r - 2f, c.Y - r - 2f, r * 2f + 4f, r * 2f + 4f, IronDark, Projectile.rotation);
            Rect(sb, px, c.X - r, c.Y - r, r * 2f, r * 2f, IronBody, Projectile.rotation);
            //四道辐条：转起来才看得出这轮在滚而不是在滑
            for (int i = 0; i < 4; i++) {
                float a = Projectile.rotation + i * MathHelper.PiOver4;
                Vector2 dir = a.ToRotationVector2();
                sb.Draw(px, c, null, IronLit, a, new Vector2(0.5f, 0.5f),
                    new Vector2(r * 2f, 3f), SpriteEffects.None, 0f);
                //齿：辐条外端的一小块，蹭到地面就是它在咬
                sb.Draw(px, c + dir * r, null, i % 2 == 0 ? RustHot : IronLit, a,
                    new Vector2(0.5f, 0.5f), new Vector2(6f, 8f), SpriteEffects.None, 0f);
            }
            bool hot = Life > RollerWind && Life <= RollerWind + RollerSweep;
            if (hot) {
                Glow(sb, c + new Vector2(0f, r * 0.8f), r * 1.6f, 12f, 0.42f);
            }
        }

        //轴对齐实心矩形（px 是纯色 1x1，拉伸出硬边机件，不做假羽化）
        private static void Rect(SpriteBatch sb, Texture2D px, float x, float y, float w, float h,
            Color color, float rotation = 0f) {
            sb.Draw(px, new Vector2(x + w * 0.5f, y + h * 0.5f), null, color, rotation,
                new Vector2(0.5f, 0.5f), new Vector2(w, h), SpriteEffects.None, 0f);
        }

        //摩擦热：只当受光底衬，不做本体（A=0 加色）
        private static void Glow(SpriteBatch sb, Vector2 center, float w, float h, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            sb.Draw(glow, center, null, (RustHot with { A = 0 }) * alpha, 0f, glow.Size() * 0.5f,
                new Vector2(w / glow.Width, h / glow.Height), SpriteEffects.None, 0f);
        }
    }
}
