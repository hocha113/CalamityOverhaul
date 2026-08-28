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
    /// <summary>分离飞头 NPCOverride：环场激光平台，服务端权威运动 + 客户端傀儡</summary>
    internal class GolemFreeHeadAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.GolemHeadFree;

        private NPC body;
        private Player player;
        //服务端运动/火控计时（客户端仅表现）
        private float orbitAngle;
        private int fireTimer;
        //编织散布换边符号（服务端）
        private int weaveSign = -1;
        //死亡坠毁本地表现
        private bool crashed;

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override void SetProperty() {
            npc.knockBackResist = 0f;
            fireTimer = 0;
            crashed = false;
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 10;
        }

        public override bool AI() {
            body = Main.npc[(int)npc.ai[GolemAiSlots.PartBodyIndex]];
            npc.aiStyle = -1;
            npc.netOffset = Vector2.Zero;
            npc.damage = 0;

            if (!GolemFacts.BodyValid(body)) {
                SilentRemoveOnServer();
                return false;
            }

            //伤害转移给躯干
            npc.realLife = body.whoAmI;
            npc.life = body.life;
            npc.lifeMax = body.lifeMax;

            player = Main.player[body.target];
            npc.target = body.target;

            GolemStateIndex bodyState = GolemFacts.GetStateIndex(body);
            int bodyPhase = (int)body.ai[GolemAiSlots.BodyPhase];

            npc.localAI[1] = player.Alives() ? Math.Sign(player.Center.X - npc.Center.X) : 0;

            //死亡演出：坠毁
            if (bodyPhase >= GolemPhase.DeathShow) {
                UpdateDeathCrash();
                return false;
            }

            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.dontTakeDamage = bodyState is GolemStateIndex.Despawn;

            //激怒惩罚与躯干同拍：防御翻倍(飞头走 realLife 血池，防御却是自己的)
            bool enraged = GolemBodyAI.SharedEnrage(body, player);
            npc.defense = enraged ? npc.defDefense * 2 : npc.defDefense;

            //服务端广播位置，客户端傀儡
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
                UpdateMovementServer(bodyState);
                UpdateFireControl(bodyState, enraged);
            }

            //倾角与喷焰表现各端本地；大招节拍广播与投技压制时眼焰常亮
            npc.rotation = npc.velocity.X * -0.02f;
            npc.localAI[0] = bodyState is GolemStateIndex.Crossfire or GolemStateIndex.SunBarrage
                or GolemStateIndex.WallSlam
                || body.ai[GolemAiSlots.BodyBeat] == 2f ? 1f : 0f;

            return false;
        }

        #region 服务端运动
        private void UpdateMovementServer(GolemStateIndex bodyState) {
            if (!player.Alives()) {
                npc.velocity *= 0.96f;
                return;
            }

            switch (bodyState) {
                case GolemStateIndex.Crossfire: {
                    //取躯干对侧翼位，形成交叉火力几何
                    int flank = Math.Sign(player.Center.X - body.Center.X);
                    if (flank == 0) {
                        flank = 1;
                    }
                    Vector2 slot = player.Center + new Vector2(flank * 480f, -240f);
                    ApproachPoint(slot, 16f, 0.10f);
                    break;
                }
                case GolemStateIndex.SolarOverdrive: {
                    //绕大招锁点缓慢巡游，见证者姿态
                    GolemBodyAI ultOverride = GolemFacts.FindOverride<GolemBodyAI>(body);
                    if (ultOverride == null) {
                        LazyOrbit();
                        break;
                    }
                    orbitAngle += 0.012f;
                    Vector2 core = new(ultOverride.ai[GolemAiSlots.OverrideLockX],
                        ultOverride.ai[GolemAiSlots.OverrideLockY]);
                    Vector2 slot = core + orbitAngle.ToRotationVector2() * 560f;
                    ApproachPoint(slot, 14f, 0.08f);
                    break;
                }
                case GolemStateIndex.MeteorLeap: {
                    //悬于锁定落点上空，读作标记助手
                    GolemBodyAI bodyOverride = GolemFacts.FindOverride<GolemBodyAI>(body);
                    Vector2 mark = bodyOverride != null
                        ? new Vector2(bodyOverride.ai[GolemAiSlots.OverrideLockX], bodyOverride.ai[GolemAiSlots.OverrideLockY])
                        : Vector2.Zero;
                    if (mark.LengthSquared() > 1f) {
                        ApproachPoint(mark + new Vector2(0, -420f), 18f, 0.12f);
                    }
                    else {
                        LazyOrbit();
                    }
                    break;
                }
                case GolemStateIndex.WallSlam: {
                    //投技抓取中：飞到钉压点对面栖位，为眼激光横扫就位后定住
                    NPC grabFist = GolemFacts.FindGrabbingFist(GolemFacts.ScanLimbs(body.whoAmI));
                    GolemFistAI fistOverride = grabFist != null ? GolemFacts.FindOverride<GolemFistAI>(grabFist) : null;
                    if (fistOverride == null) {
                        LazyOrbit();
                        break;
                    }
                    var kind = (GolemPinKind)(int)fistOverride.ai[GolemAiSlots.FistPinKind];
                    Vector2 pin = new(fistOverride.ai[GolemAiSlots.FistPinX], fistOverride.ai[GolemAiSlots.FistPinY]);
                    Vector2 normal = GolemFacts.PinNormal(kind);
                    if (kind == GolemPinKind.None || pin.LengthSquared() < 1f) {
                        LazyOrbit();
                        break;
                    }
                    Vector2 perch = pin + normal * 330f + new Vector2(0f, -46f);
                    ApproachPoint(perch, 22f, 0.16f);
                    //就位后硬定，保证扫掠几何稳定
                    if (npc.Distance(perch) < 24f) {
                        npc.velocity *= 0.62f;
                    }
                    break;
                }
                case GolemStateIndex.Despawn: {
                    npc.velocity.Y -= 0.3f;
                    npc.velocity.X *= 0.98f;
                    npc.EncourageDespawn(60);
                    break;
                }
                default:
                    LazyOrbit();
                    break;
            }
        }

        /// <summary>常态绕玩家高位巡游</summary>
        private void LazyOrbit() {
            orbitAngle += 0.0085f;
            float wobble = MathF.Sin(orbitAngle * 3f) * 60f;
            Vector2 slot = player.Center + new Vector2(MathF.Cos(orbitAngle) * (430f + wobble), -270f + MathF.Sin(orbitAngle * 2f) * 50f);
            ApproachPoint(slot, 12f, 0.07f);
        }

        private void ApproachPoint(Vector2 point, float maxSpeed, float accel) {
            Vector2 to = point - npc.Center;
            float dist = to.Length();
            float speed = MathHelper.Clamp(dist / 22f, 2f, maxSpeed);
            Vector2 desired = to.SafeNormalize(Vector2.Zero) * speed;
            npc.velocity = Vector2.Lerp(npc.velocity, desired, accel);
        }
        #endregion

        #region 服务端火控
        private void UpdateFireControl(GolemStateIndex bodyState, bool enraged) {
            if (!player.Alives()) {
                return;
            }

            bool asura = CWRWorld.Asura;

            int interval;
            float boltSpeed;
            //交叉火力窗口刻意不在列：射线声部独占，飞头不再叠散射眼弹（用户令 2026-08-28）
            switch (bodyState) {
                case GolemStateIndex.PunchCombo:
                case GolemStateIndex.HookSwing:
                case GolemStateIndex.StompCombo:
                case GolemStateIndex.TrapScore:
                case GolemStateIndex.SunBarrage:
                case GolemStateIndex.Connector:
                    //常态压制：环场平台持续输出，弹幕线不断档
                    interval = GolemDirector.Tempo(92, asura, enraged);
                    boltSpeed = 8.5f;
                    break;
                default:
                    fireTimer = 0;
                    return;
            }
            //对空提频：悬空目标被飞头咬得更紧
            if (GolemFacts.TargetAirborne(player)) {
                interval = Math.Max((int)(interval * GolemDirector.AirborneTempo), 12);
            }
            if (++fireTimer >= interval) {
                fireTimer = 0;
                FireEyeBolt(boltSpeed, enraged);
            }
        }

        /// <summary>散射压制弹：半额预读 + 编织散布，与一阶段附着头同一套公平语言</summary>
        private void FireEyeBolt(float speed, bool enraged) {
            Vector2 muzzle = npc.Center + new Vector2(npc.localAI[1] * 14f, 6f);
            Vector2 lead = player.Center + player.velocity * 7f;
            weaveSign = -weaveSign;
            float weave = weaveSign * Main.rand.NextFloat(0.1f, 0.16f);
            Vector2 vel = (lead - muzzle).SafeNormalize(Vector2.UnitY).RotatedBy(weave) * speed;
            int damage = GolemDirector.ScaleDamage(GolemDirector.SunBoltDamage, GameModes.GameModeSystem.AsuraActive, enraged);
            Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel,
                ModContent.ProjectileType<GolemSunBolt>(), damage, 0f, Main.myPlayer);
            npc.netUpdate = true;
        }
        #endregion

        #region 死亡坠毁
        /// <summary>死亡演出：光熄灭 → 失控坠落 → 触地碎裂</summary>
        private void UpdateDeathCrash() {
            npc.dontTakeDamage = true;
            npc.localAI[0] = 0f;

            if (crashed) {
                npc.velocity = Vector2.Zero;
                return;
            }

            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.rotation += npc.velocity.X * 0.012f + 0.015f;

            //引擎重力接管坠落，触地判定
            if (npc.velocity.Y == 0f) {
                crashed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.4f }, npc.Center);
                    GolemScreenEffects.Shake(6f);
                    for (int i = 0; i < 18; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(npc.Center + Main.rand.NextVector2Circular(24f, 16f),
                            new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -1f)),
                            new Color(122, 104, 78), Main.rand.NextFloat(0.8f, 1.4f)).Configure(50);
                    }
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_Smoke>(npc.Center + Main.rand.NextVector2Circular(20f, 12f),
                            -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f),
                            new Color(70, 62, 52), Main.rand.NextFloat(0.7f, 1.1f)).Configure(46, 0.6f);
                    }
                }
                //坠毁后由死亡状态在崩解拍统一移除
            }
            else if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                //坠落拖烟
                PRTLoader.NewParticle<PRT_Smoke>(npc.Center, -npc.velocity * 0.1f,
                    new Color(80, 70, 58), Main.rand.NextFloat(0.5f, 0.9f)).Configure(30, 0.55f);
            }
        }
        #endregion

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
            int total = Math.Max(Main.npcFrameCount[NPCID.GolemHeadFree], 1);
            int index = npc.localAI[0] == 1f ? 1 : 0;
            index = Math.Min(index, total - 1);
            npc.frame.Y = index * frameHeight;
            if ((npc.frameCounter += 1.0) >= 16.0) {
                npc.frameCounter = 0.0;
            }
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //高速拖影（速度门控）
            Rendering.GolemRenderHelper.DrawFistTrail(spriteBatch, npc, screenPos);
            return false;
        }
        #endregion
    }
}
