using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles
{
    /// <summary>
    /// 缠足藤索：投技的抓取判定与全程藤蔓视觉。本身零伤害零接触伤——
    /// 命中即"抓"。飞行段直线(出手前已有预警线)，服务端逐帧判缠；
    /// 缠中后钉在被抓者脚踝，全程画本体→自身的藤蔓，各端从Boss同步ai自决生命周期
    /// </summary>
    internal class PlanteraSnareVine : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>缠住判定半径</summary>
        private const float CatchRadius = 52f;
        /// <summary>回收/消散速率</summary>
        private const int FadeTime = 12;

        /// <summary>本地消散计时(各端从同步态同判定,无需入包)</summary>
        private int fadeTimer = -1;
        private float spawnSeed;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
            Projectile.netImportant = true;
        }

        /// <summary>纯抓取件，永不走伤害路径</summary>
        public override bool? CanDamage() => false;

        /// <summary>服务端从巨口射出藤索</summary>
        internal static void Spawn(NPC boss, Vector2 pos, Vector2 vel) {
            if (VaultUtils.isClient) {
                return;
            }
            int id = Projectile.NewProjectile(boss.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<PlanteraSnareVine>(), 0, 0f, Main.myPlayer);
            if (id >= 0 && id < Main.maxProjectiles) {
                Main.projectile[id].netUpdate = true;
            }
        }

        public override void AI() {
            if (spawnSeed == 0f) {
                spawnSeed = 0.21f + Projectile.identity * 0.043f % 0.7f;
            }

            NPC boss = PlanteraVineFeastState.FindFeastBoss();
            int sub = boss != null ? PlanteraVineFeastState.GrabSubPhase(boss) : -1;

            //投技结束/主体丢失→本地消散(各端读的都是同步ai,同判定)
            bool alive = boss != null && sub >= PlanteraVineFeastState.SubLash
                && sub <= PlanteraVineFeastState.SubSpit;
            if (!alive || fadeTimer >= 0) {
                UpdateFade();
                return;
            }

            Projectile.timeLeft = 120;

            if (sub == PlanteraVineFeastState.SubLash) {
                UpdateFlight(boss);
            }
            else {
                UpdateLatched(boss);
            }

            Lighting.AddLight(Projectile.Center, PlanteraRenderHelper.GlowMagenta.ToVector3() * 0.35f);
        }

        /// <summary>飞行段：直线掠出，服务端逐帧判缠+射程止损</summary>
        private void UpdateFlight(NPC boss) {
            Projectile.rotation = Projectile.velocity.ToRotation();

            //梢头飞行叶屑
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.JungleGrass, 0f, 0f, 100, default, Main.rand.NextFloat(0.8f, 1.3f));
                dust.velocity = -Projectile.velocity * 0.1f;
                dust.noGravity = true;
            }

            if (VaultUtils.isClient) {
                return;
            }

            //超射程→标记空挥，本体进入软垂
            if (Projectile.Distance(boss.Center) > PlanteraVineFeastState.LashRange) {
                boss.ai[0] = PlanteraVineFeastState.SubWhiff;
                boss.netUpdate = true;
                return;
            }

            //逐帧扫玩家：取梢头半径内最近的可缠者
            int caught = -1;
            float best = CatchRadius;
            foreach (var player in Main.ActivePlayers) {
                if (!PlanteraVineFeastState.VictimEligible(player)) {
                    continue;
                }
                float dist = player.Distance(Projectile.Center);
                if (dist < best) {
                    best = dist;
                    caught = player.whoAmI;
                }
            }
            if (caught >= 0) {
                boss.ai[0] = PlanteraVineFeastState.SubDrag;
                boss.ai[1] = caught + 1;
                boss.netUpdate = true;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>缠住段：钉在被抓者脚踝，随其移动(位置由被抓者客户端权威+玩家同步)</summary>
        private void UpdateLatched(NPC boss) {
            int victim = PlanteraVineFeastState.GrabVictim(boss);
            if (victim < 0 || victim >= Main.maxPlayers || !Main.player[victim].active) {
                UpdateFade();
                return;
            }
            Player prey = Main.player[victim];
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = prey.Center + new Vector2(0f, prey.height * 0.34f);
            Projectile.rotation = (Projectile.Center - boss.Center).ToRotation();
        }

        /// <summary>消散：藤蔓快速枯缩</summary>
        private void UpdateFade() {
            if (fadeTimer < 0) {
                fadeTimer = 0;
            }
            fadeTimer++;
            Projectile.velocity *= 0.8f;
            if (fadeTimer >= FadeTime) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC boss = PlanteraAI.FindBoss();
            if (boss == null) {
                return false;
            }

            float fade = fadeTimer >= 0 ? 1f - fadeTimer / (float)FadeTime : 1f;
            if (fade <= 0.02f) {
                return false;
            }

            int sub = PlanteraVineFeastState.GrabSubPhase(boss);
            bool latched = PlanteraAI.GetStateIndex(boss) == PlanteraStateIndex.VineFeast
                && sub >= PlanteraVineFeastState.SubDrag && sub <= PlanteraVineFeastState.SubSpit;

            //本体→梢头的活藤；飞行/拖拽绷直，消散回软
            float dist = Vector2.Distance(boss.Center, Projectile.Center);
            VineParams vine = VineParams.Default;
            vine.RestLength = dist + (latched ? 4f : 18f);
            vine.HalfWidth = 8f;
            vine.Taut = latched ? 1f : 0.8f;
            vine.Taut *= fade;
            vine.Pulse = latched ? 0.7f : 0.4f;
            //拖拽期行波向本体(收线感)，飞行期向梢头
            vine.PulseDir = latched ? -1f : 1f;
            vine.Fade = fade;
            vine.Phase2 = true;
            vine.Seed = spawnSeed;

            PlanteraVineRenderer.DrawVine(Main.spriteBatch, boss.Center, Projectile.Center, vine);

            //梢头爪叶：三片瓣叶扇形张开，缠中后收拢扣紧
            Texture2D petal = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float openAngle = latched ? 0.32f : 0.85f;
            float baseRot = Projectile.rotation;
            Color leafColor = Color.Lerp(PlanteraRenderHelper.FleshCrimson, Color.White, 0.15f) * fade;

            for (int i = -1; i <= 1; i++) {
                float rot = baseRot + i * openAngle;
                Main.EntitySpriteDraw(petal, drawPos, null, leafColor, rot + MathHelper.PiOver2,
                    new Vector2(petal.Width / 2f, petal.Height * 0.9f),
                    new Vector2(0.16f, 0.34f), SpriteEffects.None, 0);
            }
            //梢心荧光(加色)
            Main.EntitySpriteDraw(glow, drawPos, null,
                PlanteraRenderHelper.GlowMagenta with { A = 0 } * (0.65f * fade),
                0f, glow.Size() / 2f, 0.4f, SpriteEffects.None, 0);

            return false;
        }
    }
}
