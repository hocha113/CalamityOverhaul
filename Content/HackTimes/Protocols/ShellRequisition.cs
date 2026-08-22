using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 躯壳征用：标记目标，不改任何行为；标记期间被打死的目标在原地
    /// 立起一具 <see cref="ShellPuppetProj"/> 傀儡替你打十五秒。<br/>
    /// 不真的复活 NPC 实体，傀儡是借死者贴图绘制的友方弹幕，
    /// 战利品照掉（死亡走正常管线）。投资型协议：花 5 RAM 押"我能在窗口内杀掉它"。<br/>
    /// 死亡边由 <see cref="HackNpcProtocolNPC.OnKill"/> 接（权威端独占），
    /// 效果本身留给追踪器按房规收尾（目标死亡照常给击杀退款）
    /// </summary>
    internal class ShellRequisition : QuickHackDef
    {
        /// <summary>傀儡存活帧数（十五秒）</summary>
        internal const int PuppetLifetime = 900;
        /// <summary>同一施术者同时最多几具躯壳</summary>
        internal const int MaxPuppets = 2;
        /// <summary>傀儡耐久 = 死者 lifeMax × 此值</summary>
        internal const float DurabilityRatio = 0.30f;
        /// <summary>傀儡接触伤害 = 死者 npc.damage × 此值</summary>
        internal const float DamageRatio = 0.60f;

        internal static readonly Color Seance = new(140, 255, 200);
        private static readonly Color SeanceDim = new(60, 120, 95);

        public override void SetDefaults() {
            UploadTime = 130;
            RamCost = 5;
            Category = QuickHackCategory.Paranormal;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 420;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            return !npc.friendly && !npc.townNPC
                && !HackEffectTracker.HasEffect<ShellRequisition>(npc.whoAmI);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            if (Main.netMode != NetmodeID.Server) EmitMark(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitMark(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitString(npc, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitString(npc, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            //走到这里说明标记期满目标还活着，赌输了；死亡路径不会触发 OnRemove
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitFizzle(npc);
            }
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitFizzle(npc);
        }

        /// <summary>
        /// 标记目标死亡时由权威端调用；生成傀儡并显式广播
        /// 服务端替玩家生成的弹幕不带自动同步包（owner 不是服务端的 myPlayer）
        /// </summary>
        internal static void OnMarkedKill(NPC npc, ActiveHackEffect effect) {
            if (Main.netMode == NetmodeID.MultiplayerClient || effect == null) return;
            int casterIndex = effect.CasterIndex;
            if (casterIndex < 0 || casterIndex >= Main.maxPlayers
                || Main.player[casterIndex]?.active != true) {
                return;
            }
            if (CountPuppets(casterIndex) >= MaxPuppets) return;

            int damage = Math.Max(10, (int)(npc.damage * DamageRatio));
            float durability = Math.Max(40f, npc.lifeMax * DurabilityRatio);
            int index = Projectile.NewProjectile(npc.GetSource_Death(), npc.Center,
                Vector2.Zero, ModContent.ProjectileType<ShellPuppetProj>(), damage, 3f,
                casterIndex, npc.type, durability);
            if (Main.netMode == NetmodeID.Server
                && index >= 0 && index < Main.maxProjectiles) {
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, index);
            }
        }

        private static int CountPuppets(int casterIndex) {
            int type = ModContent.ProjectileType<ShellPuppetProj>();
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && proj.owner == casterIndex) count++;
            }
            return count;
        }

        #region 表现

        //落标：一根提线从上方垂下勾住目标
        private static void EmitMark(NPC npc) {
            Vector2 top = new(npc.Center.X, npc.position.Y - 46f);
            for (int i = 0; i < 8; i++) {
                float t = i / 7f;
                Vector2 pos = Vector2.Lerp(top, npc.Top, t);
                PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Seance, 0.7f)
                    ?.Configure(false, 22);
            }
            PRTLoader.NewParticle<PRT_TBUGGlitch>(npc.Center,
                Main.rand.NextVector2Circular(1f, 1f), Seance, 1.1f)?.Configure(24);
        }

        //标记维持：头顶悬一点线头微光
        private static void EmitString(NPC npc, int elapsed) {
            if (elapsed % 18 != 0) return;
            Vector2 pos = new(
                npc.Center.X + Main.rand.NextFloat(-6f, 6f),
                npc.position.Y - Main.rand.NextFloat(14f, 34f));
            PRTLoader.NewParticle<PRT_Spark>(pos, new Vector2(0f, 0.4f), SeanceDim, 0.45f)
                ?.Configure(false, 16);
        }

        //窗口期满没杀掉：提线断掉散场
        private static void EmitFizzle(NPC npc) {
            for (int i = 0; i < 5; i++) {
                Vector2 pos = new(npc.Center.X + Main.rand.NextFloat(-8f, 8f),
                    npc.position.Y - Main.rand.NextFloat(4f, 30f));
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), 1.2f), SeanceDim, 0.5f)
                    ?.Configure(false, 14);
            }
        }

        #endregion
    }

    /// <summary>
    /// 躯壳傀儡：借被杀 NPC 的贴图绘制的友方冲撞弹。<br/>
    /// AI 各端本地模拟，索敌规则确定性（全场最近的可追击敌怪），命中由 owner 端结算；
    /// 耐久只在 owner 端有意义（OnHitNPC 只跑在 owner 端），耗尽由 owner 下架并随包同步
    /// </summary>
    internal class ShellPuppetProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>被征用躯壳的 NPC 类型，随生成包到达各端</summary>
        private int ShellType => (int)Projectile.ai[0];
        /// <summary>剩余耐久；owner 端权威</summary>
        private ref float Durability => ref Projectile.localAI[0];
        private ref float Initialized => ref Projectile.localAI[1];

        private const float SeekSpeed = 9.5f;
        private const float SeekRange = 2000f;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = ShellRequisition.PuppetLifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.netImportant = true;
            Projectile.DamageType = DamageClass.Generic;
        }

        public override void AI() {
            if (Initialized == 0f) {
                Initialized = 1f;
                Durability = Projectile.ai[1];
                //命中盒贴合躯壳本体；样本表各端一致，尺寸不必进包
                if (ContentSamples.NpcsByNetId.TryGetValue(ShellType, out NPC sample)) {
                    Projectile.Resize(Math.Max(24, sample.width),
                        Math.Max(24, sample.height));
                }
            }

            NPC quarry = FindQuarry();
            if (quarry != null) {
                Vector2 dir = quarry.Center - Projectile.Center;
                float dist = dir.Length();
                if (dist > 8f) dir /= dist;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    dir * SeekSpeed, 0.07f);
            }
            else {
                //没活干就悬着，轻微沉浮
                Projectile.velocity *= 0.94f;
                Projectile.velocity.Y +=
                    MathF.Sin(Main.GameUpdateCount * 0.05f + Projectile.whoAmI) * 0.04f;
            }

            Projectile.rotation = Projectile.velocity.X * 0.03f;
            if (MathF.Abs(Projectile.velocity.X) > 0.4f) {
                Projectile.spriteDirection = Projectile.velocity.X > 0f ? 1 : -1;
            }
            Projectile.frameCounter++;

            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(
                    Projectile.width * 0.4f, Projectile.height * 0.4f);
                PRTLoader.NewParticle<PRT_TBUGGlitch>(pos,
                    new Vector2(0f, Main.rand.NextFloat(-0.6f, 0.2f)),
                    ShellRequisition.Seance, 0.7f)?.Configure(18);
            }
        }

        //全场最近的可追击敌怪：规则确定性，各端自会指向同一个目标
        private NPC FindQuarry() {
            NPC best = null;
            float bestDist = SeekRange * SeekRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.life <= 0
                    || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.DistanceSquared(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //只在 owner 端跑：冲撞是互耗，挨到谁就折谁的接触伤
            Durability -= Math.Max(20f, target.damage);
            if (Durability <= 0f) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) return;
            //崩解成灰烬：躯壳到点散架
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.2f, 3.2f)
                    * Main.rand.NextFloat(0.3f, 1f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel,
                    ShellRequisition.Seance, 0.8f)?.Configure(false, 22);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_TBUGGlitch>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f),
                    ShellRequisition.Seance, 1.0f)?.Configure(22);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int type = ShellType;
            if (type <= 0 || type >= NPCLoader.NPCCount) return false;
            Main.instance.LoadNPC(type);
            Texture2D tex = TextureAssets.Npc[type].Value;

            int frameCount = Math.Max(1, Main.npcFrameCount[type]);
            int frameHeight = tex.Height / frameCount;
            Rectangle frame = new(0,
                frameHeight * (Projectile.frameCounter / 8 % frameCount),
                tex.Width, frameHeight);
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            SpriteEffects flip = Projectile.spriteDirection < 0
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //灵体色主体 + 两瓣错位残影，读作"借来的躯壳"而不是活物
            float flicker = 0.72f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f
                + Projectile.whoAmI);
            Color body = ShellRequisition.Seance * flicker;
            Main.EntitySpriteDraw(tex, drawPos + new Vector2(3f, 0f), frame,
                new Color(90, 160, 255) * 0.22f, Projectile.rotation, origin, 1f, flip);
            Main.EntitySpriteDraw(tex, drawPos - new Vector2(3f, 0f), frame,
                new Color(255, 90, 160) * 0.22f, Projectile.rotation, origin, 1f, flip);
            Main.EntitySpriteDraw(tex, drawPos, frame, body,
                Projectile.rotation, origin, 1f, flip);
            return false;
        }
    }
}
