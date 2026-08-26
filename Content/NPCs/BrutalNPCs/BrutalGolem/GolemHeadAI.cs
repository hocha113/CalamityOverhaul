using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem
{
    /// <summary>附着头 NPCOverride：确定性锚点 + 观察躯干状态行动，伤害经 realLife 转给躯干</summary>
    internal class GolemHeadAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.GolemHead;

        private NPC body;
        private Player player;
        //服务端开火节拍
        private int fireTimer;
        //编织散布换边符号（服务端）
        private int weaveSign = 1;
        //分离仪式本地表现计时
        private int detachTimer;

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override void SetProperty() {
            npc.knockBackResist = 0f;
            fireTimer = 0;
            detachTimer = 0;
        }

        public override bool AI() {
            body = Main.npc[(int)npc.ai[GolemAiSlots.PartBodyIndex]];
            npc.aiStyle = -1;
            npc.netOffset = Vector2.Zero;
            npc.damage = 0;
            npc.noGravity = true;
            npc.noTileCollide = true;

            if (!GolemFacts.BodyValid(body)) {
                SilentRemoveOnServer();
                return false;
            }

            //伤害转移给躯干，头是躯干血池的延伸
            npc.realLife = body.whoAmI;
            npc.life = body.life;
            npc.lifeMax = body.lifeMax;

            player = Main.player[body.target];
            npc.target = body.target;

            //淡入
            if (npc.alpha > 0) {
                npc.alpha = Math.Max(npc.alpha - 12, 0);
            }
            //躯干沉地淡出时同步隐去（头锚随躯干下沉）
            if (GolemFacts.GetStateIndex(body) == GolemStateIndex.Despawn) {
                npc.alpha = Math.Max(npc.alpha, body.alpha);
            }

            GolemStateIndex bodyState = GolemFacts.GetStateIndex(body);
            int bodyPhase = (int)body.ai[GolemAiSlots.BodyPhase];

            //眼部朝向（FindFrame 消费）
            npc.localAI[1] = player.Alives() ? Math.Sign(player.Center.X - npc.Center.X) : 0;

            if (bodyState == GolemStateIndex.HeadDetach) {
                UpdateDetachRise();
                return false;
            }

            detachTimer = 0;

            //死亡演出：眼光熄灭，跟随躯干晃动
            if (bodyPhase >= GolemPhase.DeathShow) {
                npc.dontTakeDamage = true;
                npc.localAI[0] = 0f;
                npc.Center = GolemFacts.HeadAnchor(body);
                npc.velocity = Vector2.Zero;
                return false;
            }

            //常态：确定性锚点吸附（各端一致，无需高频同步）
            npc.Center = GolemFacts.HeadAnchor(body);
            npc.velocity = Vector2.Zero;
            npc.rotation = 0f;
            npc.dontTakeDamage = bodyState is GolemStateIndex.Intro or GolemStateIndex.Despawn;

            UpdateFireControl(bodyState);
            return false;
        }

        #region 火控（服务端裁决，弹幕自带出膛表现）
        private void UpdateFireControl(GolemStateIndex bodyState) {
            bool death = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            bool enraged = GolemBodyAI.ComputeEnrage(player, CWRRef.GetBossRushActive());

            //眼部炽热提示：任何压制射击窗口发亮
            bool inFireWindow = bodyState is GolemStateIndex.SunBarrage or GolemStateIndex.TrapScore
                or GolemStateIndex.PunchCombo or GolemStateIndex.HookSwing
                or GolemStateIndex.StompCombo or GolemStateIndex.Connector;
            npc.localAI[0] = inFireWindow ? 1f : 0f;

            if (VaultUtils.isClient || !player.Alives()) {
                return;
            }

            //全攻击状态持续压制：弹幕线不断档，密度按窗口分级
            int interval;
            bool alternate;
            switch (bodyState) {
                case GolemStateIndex.SunBarrage:
                    //双眼交替直射弹，与躯干宝石臼炮互补
                    interval = GolemDirector.Tempo(40, death, enraged);
                    alternate = true;
                    break;
                case GolemStateIndex.TrapScore:
                    //机关演奏时点射，把走位逼进刺谱
                    interval = GolemDirector.Tempo(62, death, enraged);
                    alternate = false;
                    break;
                case GolemStateIndex.PunchCombo:
                case GolemStateIndex.HookSwing:
                case GolemStateIndex.StompCombo:
                case GolemStateIndex.Connector:
                    //近战窗口保底压制
                    interval = GolemDirector.Tempo(84, death, enraged);
                    alternate = true;
                    break;
                default:
                    fireTimer = 0;
                    return;
            }
            //对空提频：目标悬空时双眼咬得更紧
            if (GolemFacts.TargetAirborne(player)) {
                interval = System.Math.Max((int)(interval * GolemDirector.AirborneTempo), 12);
            }
            if (++fireTimer >= interval) {
                fireTimer = 0;
                FireEyeBolt(alternate);
            }
        }

        /// <summary>眼位直射太阳弹：半额预读 + 编织散布——弹着点逐发交替夹住目标动向两侧，
        /// 不全数收束于预读点（方向层公平缺口：匀速漂移可从弹缝穿过，急转向令预读落空）</summary>
        private void FireEyeBolt(bool alternate) {
            int eye = alternate ? (int)(npc.localAI[2] = 1f - npc.localAI[2]) : 1;
            Vector2 muzzle = npc.Center + new Vector2((eye == 0 ? -18f : 18f) * npc.scale, -6f * npc.scale);
            Vector2 lead = player.Center + player.velocity * 8f;
            weaveSign = -weaveSign;
            float weave = weaveSign * Main.rand.NextFloat(0.09f, 0.15f);
            Vector2 vel = (lead - muzzle).SafeNormalize(Vector2.UnitY).RotatedBy(weave) * 9.5f;

            int damage = GolemDirector.ScaleDamage(GolemDirector.SunBoltDamage, CWRRef.GetDeathMode());
            Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel,
                ModContent.ProjectileType<GolemSunBolt>(), damage, 0f, Main.myPlayer);
            npc.netUpdate = true;
        }
        #endregion

        #region 分离仪式（表现本地推进，服务端负责最终换体）
        private void UpdateDetachRise() {
            npc.dontTakeDamage = true;
            npc.localAI[0] = 1f;
            detachTimer++;

            Vector2 anchor = GolemFacts.HeadAnchor(body);
            //阶段1 震颤（0~60）：锁扣崩开，头在锚点抖动
            if (detachTimer < 60) {
                npc.Center = anchor + Main.rand.NextVector2Circular(1.6f, 1.2f) * (detachTimer / 60f);
                if (!VaultUtils.isServer && detachTimer % 12 == 0) {
                    SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.4f, Volume = 0.7f }, npc.Center);
                    Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Stone, 0, 1.5f);
                }
                return;
            }

            //阶段2 升起（60~150）：8次幂后仰吸气，缓慢拔离再加速上升
            float t = MathHelper.Clamp((detachTimer - 60) / 90f, 0f, 1f);
            float rise = MathF.Pow(t, 2.4f) * 220f;
            npc.Center = anchor - new Vector2(0f, rise);
            npc.rotation = MathF.Sin(detachTimer * 0.13f) * 0.05f;

            if (!VaultUtils.isServer) {
                //颈口漏光与碎石
                if (detachTimer % 5 == 0) {
                    Vector2 neck = anchor + new Vector2(Main.rand.NextFloat(-14f, 14f), 8f);
                    PRTLoader.NewParticle<PRT_Spark>(neck, new Vector2(0, Main.rand.NextFloat(-3f, -1f)),
                        new Color(255, 200, 90), Main.rand.NextFloat(0.9f, 1.3f)).Configure(true, 20);
                }
                if (detachTimer % 9 == 0) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(anchor + Main.rand.NextVector2Circular(20f, 8f),
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-4f, -1f)),
                        new Color(120, 100, 70), Main.rand.NextFloat(0.8f, 1.2f)).Configure(40);
                }
            }
        }
        #endregion

        /// <summary>静默移除（不触发原版 HitEffect 的自动生成分离头）</summary>
        internal void SilentRemoveOnServer() {
            if (VaultUtils.isClient) {
                return;
            }
            npc.life = 0;
            npc.active = false;
            npc.netUpdate = true;
        }

        #region 绘制
        public override bool FindFrame(int frameHeight) {
            int total = Math.Max(Main.npcFrameCount[NPCID.GolemHead], 1);
            int index = 0;
            //眼部发亮帧
            if (npc.localAI[0] == 1f) {
                index = 1;
            }
            //侧视帧组（原版：右 +2，左 +4）
            if (npc.localAI[1] == 1f) {
                index += 2;
            }
            else if (npc.localAI[1] == -1f) {
                index += 4;
            }
            index = Math.Min(index, total - 1);
            npc.frame.Y = index * frameHeight;
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //分离仪式期颈口光柱
            if (GolemFacts.BodyValid(body) && GolemFacts.GetStateIndex(body) == GolemStateIndex.HeadDetach
                && detachTimer > 30) {
                float lift = MathHelper.Clamp((detachTimer - 30) / 120f, 0f, 1f);
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Vector2 neck = GolemFacts.HeadAnchor(body) + new Vector2(0, 10f) - screenPos;
                Color gold = new Color(255, 190, 80, 0) * (0.55f + 0.45f * lift);
                spriteBatch.Draw(glow, neck, null, gold, 0f, glow.Size() / 2f,
                    new Vector2(1.6f, 3.2f + lift * 3f), SpriteEffects.None, 0f);
            }
            return false;
        }
        #endregion
    }
}
