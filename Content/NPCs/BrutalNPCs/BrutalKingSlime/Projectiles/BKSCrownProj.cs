using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>
    /// 史莱姆王之冠，招式期离体威胁单元。默认态王冠由本体渲染层扣画在头顶，
    /// 本弹幕只在"起飞→执行→归位砸扣"的招式三拍内存在，归位扣上头顶即消亡。<br/>
    /// ai[0]=宿主whoAmI ai[1]=模式 ai[2]=锁定X<br/>
    /// 模式：0升空 1瞄准悬停 2天坠 3嵌地 4归位砸扣 6审判指挥 7死亡坠地(纯演出)<br/>
    /// 模式切换服务端驱动；各端按模式+本地计时演出
    /// </summary>
    internal class BKSCrownProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int ModeLaunch = 0;
        internal const int ModeTelegraph = 1;
        internal const int ModeSlam = 2;
        internal const int ModeLanded = 3;
        internal const int ModeReturn = 4;
        internal const int ModeDecree = 6;
        internal const int ModeDeathDrop = 7;

        private const int LaunchTime = 24;
        private const int TelegraphTime = 34;
        /// <summary>嵌地滞留拍：伤害只在前8帧，其后是纯读秒，压短掉空等</summary>
        private const int LandedTime = 22;
        /// <summary>归位悬停整备拍长(收招尾巴，压短)</summary>
        private const int ReturnHoldTime = 6;
        /// <summary>公平阀(契约3)：天坠X在离开瞄准态瞬间按此提前帧锁定，坠落全程不再追踪</summary>
        private const float SlamLockLeadFrames = 10f;

        private NPC Host => (int)Projectile.ai[0] >= 0 && (int)Projectile.ai[0] < Main.maxNPCs
            ? Main.npc[(int)Projectile.ai[0]] : null;

        private int Mode => (int)Projectile.ai[1];

        private ref float ModeTimer => ref Projectile.localAI[0];
        private ref float PrevMode => ref Projectile.localAI[1];
        /// <summary>模式内子拍标记：Return=俯冲/已扣位，DeathDrop=弹跳计数(本地)</summary>
        private ref float SubBeat => ref Projectile.localAI[2];
        /// <summary>首帧入场表现已播(本地)</summary>
        private bool enterInit;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>只有天坠与嵌地前几帧有伤害</summary>
        public override bool? CanDamage() {
            if (Mode == ModeSlam) {
                return null;
            }
            if (Mode == ModeLanded && ModeTimer < 8f) {
                return null;
            }
            return false;
        }

        public override void AI() {
            NPC host = Host;
            bool hostValid = host != null && host.active && host.type == NPCID.KingSlime;

            //宿主消失：演出坠地模式继续，其余直接消失
            if (!hostValid && Mode != ModeDeathDrop) {
                Projectile.Kill();
                return;
            }

            //模式切换检测：重置本地模式计时；首帧也要播入场表现
            if (!enterInit || PrevMode != Projectile.ai[1]) {
                enterInit = true;
                PrevMode = Projectile.ai[1];
                ModeTimer = 0f;
                OnModeEnter();
            }
            ModeTimer++;

            //常驻模式刷新寿命
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 60);

            switch (Mode) {
                case ModeLaunch: UpdateLaunch(host); break;
                case ModeTelegraph: UpdateTelegraph(host); break;
                case ModeSlam: UpdateSlam(); break;
                case ModeLanded: UpdateLanded(host); break;
                case ModeReturn: UpdateReturn(host); break;
                case ModeDecree: UpdateDecree(host); break;
                case ModeDeathDrop: UpdateDeathDrop(); break;
            }

            Lighting.AddLight(Projectile.Center, KingSlimeGelFX.CrownGold.ToVector3() * 0.5f);
        }

        /// <summary>模式进入时的一次性表现，各端本地播</summary>
        private void OnModeEnter() {
            switch (Mode) {
                case ModeLaunch:
                    KingSlimeGelFX.CrownChime(Projectile.Center, 0.3f, 0.9f);
                    KingSlimeGelFX.GoldGlint(Projectile.Center, 8, 5f);
                    break;
                case ModeReturn:
                case ModeDeathDrop:
                    SubBeat = 0f;
                    break;
                case ModeSlam:
                    SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.35f, Volume = 0.9f, MaxInstances = 3 }, Projectile.Center);
                    break;
                case ModeLanded:
                    //落地重响+金屑+震屏，冲击波由服务端生成(ai1=1 皇冠金配色)
                    KingSlimeGelFX.ThudSound(Projectile.Center, 18f);
                    KingSlimeGelFX.CrownChime(Projectile.Center, -0.25f, 1f);
                    KingSlimeGelFX.GoldGlint(Projectile.Center, 22, 9f);
                    KingSlimeGelFX.CameraPunch(Projectile.Center, 7.5f, 16, "BKSCrownSlam", Vector2.UnitY);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                            ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Main.myPlayer, 1f, 1f);
                    }
                    break;
                case ModeDecree:
                    KingSlimeGelFX.CrownChime(Projectile.Center, 0.45f, 1f);
                    break;
            }
        }

        private void UpdateLaunch(NPC host) {
            //从宿主头顶弹射升空，快出慢收
            float t = MathHelper.Clamp(ModeTimer / LaunchTime, 0f, 1f);
            float speed = MathHelper.Lerp(26f, 4f, VaultUtils.EaseOutCubic(t));
            Projectile.velocity = new Vector2(0f, -speed);
            Projectile.rotation += 0.2f * (1f - t);

            //服务端调度：按宿主招式分派，天坠转瞄准、审判转指挥；
            //其余(阶段转换脱冕等纯演出)升空后直接归位砸扣，绝不滞空闲逛
            if (!VaultUtils.isClient && ModeTimer >= LaunchTime) {
                int hostState = (int)host.ai[2];
                if (hostState == (int)KingSlimeStateIndex.CrownSlam) {
                    SetMode(ModeTelegraph);
                }
                else if (hostState == (int)KingSlimeStateIndex.RoyalDecree) {
                    SetMode(ModeDecree);
                }
                else {
                    SetMode(ModeReturn);
                }
            }
        }

        private void UpdateTelegraph(NPC host) {
            Player target = Main.player[host.target];
            if (!target.Alives()) {
                if (!VaultUtils.isClient) {
                    SetMode(ModeReturn);
                }
                return;
            }

            //横向缓追目标，保持高位
            float hoverY = target.Center.Y - 400f;
            Vector2 desired = new Vector2(target.Center.X + target.velocity.X * 14f, hoverY);
            Projectile.velocity = (desired - Projectile.Center) * 0.08f;
            if (Projectile.velocity.Length() > 22f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 22f;
            }
            //旋转回正
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);

            //向心金屑，蓄势可读
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGoldSpark>(from,
                    (Projectile.Center - from) * 0.1f, KingSlimeGelFX.CrownGold, Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(14);
            }

            //服务端：锁定X后天坠(预告即承诺，锁死后不追踪)
            if (!VaultUtils.isClient && ModeTimer >= TelegraphTime) {
                Projectile.ai[2] = target.Center.X + target.velocity.X * SlamLockLeadFrames;
                SetMode(ModeSlam);
            }
        }

        private void UpdateSlam() {
            //锁X直坠，复合加速
            Projectile.Center = new Vector2(MathHelper.Lerp(Projectile.Center.X, Projectile.ai[2], 0.2f), Projectile.Center.Y);
            float vy = Math.Max(Projectile.velocity.Y, 4f) * 1.11f;
            Projectile.velocity = new Vector2(0f, Math.Min(vy, 46f));
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.3f);

            //坠落金线拖尾
            if (!VaultUtils.isServer && Main.rand.NextBool()) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGoldSpark>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 6f),
                    new Vector2(0f, -Main.rand.NextFloat(1f, 3f)), KingSlimeGelFX.CrownGold, Main.rand.NextFloat(0.9f, 1.5f))
                    ?.Configure(Main.rand.Next(10, 18));
            }

            //触地判定(全端同规则，落点由锁定X+地形唯一确定)
            Vector2 ground = KingSlimeGelFX.FindGroundBelow(new Vector2(Projectile.ai[2], Projectile.Center.Y), 10);
            if (Projectile.Center.Y + 14f >= ground.Y || Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                Projectile.Center = new Vector2(Projectile.ai[2], ground.Y - 12f);
                Projectile.velocity = Vector2.Zero;
                if (!VaultUtils.isClient) {
                    SetMode(ModeLanded);
                }
            }
        }

        private void UpdateLanded(NPC host) {
            Projectile.velocity = Vector2.Zero;
            //嵌地微颤
            if (ModeTimer < 10f) {
                Projectile.rotation = (float)Math.Sin(ModeTimer * 1.4f) * 0.12f * (1f - ModeTimer / 10f);
            }

            if (!VaultUtils.isClient && ModeTimer >= LandedTime) {
                SetMode(ModeReturn);
            }
        }

        /// <summary>
        /// 归位砸扣三拍：悬停整备(急停)→滑翔到头顶正上方→锁X俯冲，
        /// 砸到扣冠锚点即扣上(本体凝胶受压微陷回弹)，弹幕消亡交还给渲染层扣冠。
        /// SubBeat：0整备/滑翔 1俯冲 2已扣位待消亡
        /// </summary>
        private void UpdateReturn(NPC host) {
            //扣冠锚点：取本体覆写的形变头顶，取不到回退碰撞盒顶
            Vector2 anchor = host.TryGetOverride(out KingSlimeAI hostAI)
                ? hostAI.GetCrownAnchor() : host.Top + new Vector2(0f, -6f);

            //已扣位：钉在锚点上等服务端 Kill 包，防止先到端漂移
            if (SubBeat >= 2f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = 0f;
                return;
            }

            if (SubBeat < 1f) {
                //拍一：悬停整备，急停回正，蓄一口气
                if (ModeTimer <= ReturnHoldTime) {
                    Projectile.velocity *= 0.72f;
                    Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);
                    return;
                }
                //拍二：滑翔到头顶正上方(加速趋近，有奔赴感)
                Vector2 apex = anchor + new Vector2(0f, -130f);
                Vector2 toApex = apex - Projectile.Center;
                float speed = MathHelper.Clamp(6f + (ModeTimer - ReturnHoldTime) * 1.1f, 6f, 30f);
                Projectile.velocity = toApex.SafeNormalize(Vector2.Zero) * Math.Min(speed, toApex.Length());
                Projectile.rotation = Projectile.rotation.AngleLerp(Projectile.velocity.X * 0.02f, 0.2f);

                //到位(或超时强制)转俯冲
                if ((Math.Abs(toApex.X) < 22f && toApex.Y > -60f) || ModeTimer > 50f) {
                    SubBeat = 1f;
                }
                return;
            }

            //拍三：俯冲砸扣，锁X复合加速直坠
            Projectile.Center = new Vector2(MathHelper.Lerp(Projectile.Center.X, anchor.X, 0.3f), Projectile.Center.Y);
            float vy = Math.Max(Projectile.velocity.Y, 3f) * 1.17f;
            Projectile.velocity = new Vector2(0f, Math.Min(vy, 36f));
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.35f);

            if (Projectile.Center.Y + Projectile.velocity.Y >= anchor.Y - 4f) {
                //砸扣命中：钉位+压弹表现，服务端消亡交棒
                float power = MathHelper.Clamp(Projectile.velocity.Y * 0.012f, 0.14f, 0.4f);
                SubBeat = 2f;
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = 0f;
                hostAI?.NotifyCrownMounted(power);
                KingSlimeGelFX.CrownChime(anchor, -0.2f, 0.9f);
                KingSlimeGelFX.SquishSound(anchor, -0.15f, 0.8f);
                KingSlimeGelFX.GoldGlint(anchor, 12, 6f);
                KingSlimeGelFX.CameraPunch(anchor, 3.5f, 10, "BKSCrownMount", Vector2.UnitY);
                if (!VaultUtils.isClient) {
                    Projectile.Kill();
                }
            }
        }

        private void UpdateDecree(NPC host) {
            //快速升赴战场上空指挥位，到位后端庄小幅呼吸(不漂)
            Vector2 dest = host.Center + new Vector2(0f, -430f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.8f) * 8f);
            Vector2 toDest = dest - Projectile.Center;
            Projectile.velocity = toDest * 0.12f;
            if (Projectile.velocity.Length() > 26f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 26f;
            }
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.15f);

            if (!VaultUtils.isServer) {
                if (Main.rand.NextBool(2)) {
                    Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(120f, 120f);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGoldSpark>(from,
                        (Projectile.Center - from) * 0.08f, KingSlimeGelFX.CrownGold, Main.rand.NextFloat(1f, 1.6f))
                        ?.Configure(18);
                }
            }
            Lighting.AddLight(Projectile.Center, KingSlimeGelFX.CrownGold.ToVector3() * 1.2f);

            //服务端调度：宿主离开审判态则归位砸扣(大招状态终拍也会主动下令归位)
            if (!VaultUtils.isClient && (int)host.ai[2] != (int)KingSlimeStateIndex.RoyalDecree) {
                SetMode(ModeReturn);
            }
        }

        private void UpdateDeathDrop() {
            //纯演出坠地：重力+两次弹跳
            Projectile.velocity.Y += 0.5f;
            if (Projectile.velocity.Y > 18f) {
                Projectile.velocity.Y = 18f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.04f;
            Projectile.velocity.X *= 0.99f;

            if (Collision.SolidCollision(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height)
                && Projectile.velocity.Y > 0f) {
                if (Projectile.velocity.Y > 4f && Projectile.localAI[2] < 2f) {
                    //弹跳
                    Projectile.localAI[2]++;
                    Projectile.velocity.Y *= -0.42f;
                    Projectile.velocity.X *= 0.6f;
                    KingSlimeGelFX.CrownChime(Projectile.Center, -0.1f - Projectile.localAI[2] * 0.15f, 0.8f);
                    KingSlimeGelFX.GoldGlint(Projectile.Bottom, 6, 4f);
                }
                else {
                    Projectile.velocity = Vector2.Zero;
                }
            }

            if (ModeTimer > 340f) {
                Projectile.alpha += 6;
                if (Projectile.alpha >= 255) {
                    Projectile.Kill();
                }
            }
        }

        /// <summary>服务端切换模式并同步</summary>
        private void SetMode(int mode) {
            Projectile.ai[1] = mode;
            Projectile.netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadGore(GoreID.KingSlimeCrown);
            Texture2D crown = TextureAssets.Gore[GoreID.KingSlimeCrown].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = crown.Size() * 0.5f;
            float fade = 1f - Projectile.alpha / 255f;

            //瞄准/天坠期的金色指引与拖影
            if (Mode == ModeTelegraph || Mode == ModeSlam) {
                DrawGuideColumn(pos, fade);
            }
            if (Mode == ModeSlam || (Mode == ModeReturn && Projectile.velocity.Y > 8f)) {
                for (int i = 1; i <= 4; i++) {
                    Vector2 ghost = pos - new Vector2(0f, Projectile.velocity.Y * i * 0.7f) * -1f;
                    Main.EntitySpriteDraw(crown, ghost, null,
                        KingSlimeGelFX.CrownGold with { A = 0 } * (0.3f - i * 0.06f) * fade,
                        Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
                }
            }

            //审判模式的指挥光环：解析成形光环着色器(软核+双金环+金丝弧+辐条)，
            //替换旧"巨型八边形辉光块+星形贴图"，贴图多边形 alpha 边界在超尺度放大下暴露
            if (Mode == ModeDecree) {
                DrawDecreeHalo(fade);
            }

            //本体
            Color light = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Main.EntitySpriteDraw(crown, pos, null, light * fade, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            //金属泽光
            Main.EntitySpriteDraw(crown, pos, null, KingSlimeGelFX.CrownGold with { A = 0 } * (0.35f * fade),
                Projectile.rotation, origin, 1.03f, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>
        /// 瞄准指引柱：顶点 quad + GuideTech(呼吸金芯/下行锁定箭标/收拢侧轨/落点结穴)，
        /// 替换旧像素条。瞄准期侧轨随蓄势收向落点，天坠期并拢提亮
        /// </summary>
        private void DrawGuideColumn(Vector2 screenPos, float fade) {
            float lockX = Mode == ModeSlam ? Projectile.ai[2] : Projectile.Center.X;
            Vector2 ground = KingSlimeGelFX.FindGroundBelow(new Vector2(lockX, Projectile.Center.Y), 90);
            float height = ground.Y - Projectile.Center.Y;
            if (height < 40f) {
                return;
            }

            Effect fx = EffectLoader.BKSCrownFX?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                DrawGuideFallback(lockX, height, fade);
                return;
            }

            bool locking = Mode == ModeSlam;
            fx.CurrentTechnique = fx.Techniques["GuideTech"];
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.173f % 1f);
            fx.Parameters["uOpacity"]?.SetValue((locking ? 0.9f : 0.6f) * fade);
            fx.Parameters["uProg"]?.SetValue(MathHelper.Clamp(ModeTimer / TelegraphTime, 0f, 1f));
            fx.Parameters["uLock"]?.SetValue(locking ? 1f : 0f);

            //quad：uv.y 0=王冠端 1=地面端(落点向下延伸少许包住结穴)
            float halfW = locking ? 84f : 66f;
            Vector2 top = new Vector2(lockX, Projectile.Center.Y);
            Vector2 bottom = new Vector2(lockX, ground.Y + 14f);
            DrawFXQuad(fx, noise,
                new Vector2(top.X - halfW, top.Y), new Vector2(top.X + halfW, top.Y),
                new Vector2(bottom.X - halfW, bottom.Y), new Vector2(bottom.X + halfW, bottom.Y));
        }

        /// <summary>大招指挥光环：解析成形，向外羽化在 r=0.98 前归零无贴图边界</summary>
        private void DrawDecreeHalo(float fade) {
            Effect fx = EffectLoader.BKSCrownFX?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return; //衬光缺席可接受，不回退到八边形贴图
            }

            fx.CurrentTechnique = fx.Techniques["HaloTech"];
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.173f % 1f);
            fx.Parameters["uOpacity"]?.SetValue(0.85f * fade);
            fx.Parameters["uProg"]?.SetValue(MathHelper.Clamp(ModeTimer / 36f, 0f, 1f));
            fx.Parameters["uLock"]?.SetValue(0f);

            const float half = 250f;
            Vector2 c = Projectile.Center;
            DrawFXQuad(fx, noise,
                c + new Vector2(-half, -half), c + new Vector2(half, -half),
                c + new Vector2(-half, half), c + new Vector2(half, half));
        }

        /// <summary>顶点 quad 提交：Additive + 显式 s1 噪声绑定，设备状态自管</summary>
        private static void DrawFXQuad(Effect fx, Texture2D noise,
            Vector2 tl, Vector2 tr, Vector2 bl, Vector2 br) {
            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            //采样器合同：显式绑 s1(shader 内 register)，参数式绑定禁用
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(tl.ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(tr.ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(bl.ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(br.ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>着色器缺失回退：旧式细金柱示意落点</summary>
        private void DrawGuideFallback(float lockX, float height, float fade) {
            Texture2D pixel = InnoVault.VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f);
            float alpha = (Mode == ModeSlam ? 0.5f : 0.22f + pulse * 0.14f) * fade;
            Vector2 top = new Vector2(lockX, Projectile.Center.Y) - Main.screenPosition;
            Main.spriteBatch.Draw(pixel, top, null, KingSlimeGelFX.CrownGold with { A = 0 } * alpha, 0f,
                new Vector2(pixel.Width * 0.5f, 0f), new Vector2(3f / pixel.Width, height / pixel.Height), SpriteEffects.None, 0f);
        }
    }
}
