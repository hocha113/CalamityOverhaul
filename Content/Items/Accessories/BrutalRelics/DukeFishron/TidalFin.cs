using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.DukeFishron
{
    /// <summary>
    /// 潮汐之鳍：猪龙鱼公爵残酷遗物。按饰品技能键向光标方向连锁冲刺（基础两段，
    /// 每段可重新瞄准），每段拖出持留的海啸水墙，末段落点掀起潮汐龙卷；
    /// 雨天/浸水时段数+1 且全段伤害强化。冲刺全程无敌保留（2026-08-29 用户终审
    /// 回退案），代价换成 12 秒整链冷却与非雨天减段
    /// </summary>
    internal class TidalFin : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //框架 §9 T4 梯度统一 75 金
            Item.value = Item.buyPrice(0, 75, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<TidalFinPlayer>().Equipped = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            base.ModifyTooltips(tooltips);
            tooltips.InsertHotkeyBinding(CWRKeySystem.Accessory_Skills, "[KEY]",
                CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.Accessory_Skills.DisplayName}]");
        }
    }

    /// <summary>
    /// 连锁冲刺状态机（非雨天两段/雨天浸水三段）。全部状态只活在 owner 端本地
    /// （键位只在本机响应，玩家位移走原版玩家同步），旁观端的可见性由海啸水墙/龙卷弹幕承载
    /// </summary>
    internal class TidalFinPlayer : ModPlayer
    {
        #region 参数
        /// <summary>前摇收缩帧数：短促收缩→一帧爆发，禁匀速位移</summary>
        private const int WindupTime = 7;
        /// <summary>单段冲刺帧数</summary>
        internal const int DashTime = 15;
        /// <summary>硬刹帧数</summary>
        private const int BrakeTime = 3;
        /// <summary>段间接续窗口</summary>
        private const int ChainWindowTime = 36;
        /// <summary>整链冷却（12s：全程无敌保留的代价，回退案）</summary>
        private const int CooldownTime = 720;
        /// <summary>爆发瞬间速度（过冲），随后指数回落巡航</summary>
        private const float BurstSpeed = 46f;
        private const float CruiseSpeed = 33f;

        /// <summary>水墙单跳基数（生成时吃 Generic 加成，长寿命期 owner 逐帧刷新）</summary>
        internal const int WallDamage = 140;
        /// <summary>龙卷单跳基数（同上）</summary>
        internal const int TornadoDamage = 200;
        /// <summary>雨天/浸水强化倍率</summary>
        internal const float EmpowerMult = 1.5f;
        #endregion

        #region 状态（全实例字段，禁 static）
        /// <summary>本帧装备中，物品钩子逐帧点亮</summary>
        public bool Equipped;
        /// <summary>0 待机 / 1 前摇 / 2 冲刺 / 3 刹车</summary>
        private int phase;
        private int phaseTimer;
        /// <summary>当前段序（1 起），0=链未开始</summary>
        private int stage;
        private int maxStages;
        private bool empowered;
        private Vector2 dashDir;
        /// <summary>段间等待接续的剩余帧</summary>
        private int chainWindow;
        private int cooldown;
        /// <summary>冲刺末尾缓冲的接续输入</summary>
        private bool pressBuffered;
        #endregion

        private float DamageMult => empowered ? EmpowerMult : 1f;

        public override void ResetEffects() => Equipped = false;

        public override void UpdateDead() {
            //死亡直接掐断链条，冷却照走
            ResetChain(startCooldown: stage > 0);
        }

        private void ResetChain(bool startCooldown) {
            if (startCooldown && cooldown <= 0) {
                cooldown = CooldownTime;
            }
            phase = 0;
            phaseTimer = 0;
            stage = 0;
            chainWindow = 0;
            pressBuffered = false;
        }

        public override void ProcessTriggers(TriggersSet triggersSet) {
            if (!Equipped || Player.dead || CWRKeySystem.Accessory_Skills == null) {
                return;
            }
            if (!CWRKeySystem.Accessory_Skills.JustPressed) {
                return;
            }
            //坐骑上不响应，避免与坐骑运动打架
            if (Player.mount?.Active == true) {
                return;
            }

            if (phase == 0 && stage == 0 && cooldown <= 0) {
                StartChain();
            }
            else if ((phase == 3 || (phase == 0 && chainWindow > 0)) && stage < maxStages) {
                StartStage(stage + 1);
            }
            else if (phase == 2 && phaseTimer >= DashTime - 10 && stage < maxStages) {
                //冲刺末尾提前按下，缓冲到刹车帧接续
                pressBuffered = true;
            }
        }

        private void StartChain() {
            empowered = Main.raining || Player.wet;
            //回退案段数：非雨天 2 段，雨天/浸水 3 段（超模无敌压进条件窗口）
            maxStages = empowered ? 3 : 2;
            Player.RemoveAllGrapplingHooks();
            StartStage(1);
        }

        private void StartStage(int nextStage) {
            stage = nextStage;
            phase = 1;
            phaseTimer = 0;
            chainWindow = 0;
            pressBuffered = false;
            //鳍张开的挤水声：公爵冲刺前摇的同款语汇
            SoundEngine.PlaySound(SoundID.NPCHit14 with { Pitch = -0.4f, Volume = 0.55f, MaxInstances = 3 }, Player.Center);
        }

        public override void PreUpdateMovement() {
            //状态机只在 owner 端推进；服务端/旁观端此处恒为待机
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
                if (cooldown == 0 && Equipped) {
                    //冷却就绪的轻水声提示
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = 0.45f, Volume = 0.5f }, Player.Center);
                }
            }
            if (!Equipped) {
                ResetChain(startCooldown: stage > 0);
                return;
            }

            switch (phase) {
                case 1: TickWindup(); break;
                case 2: TickDashing(); break;
                case 3: TickBrake(); break;
                default: TickIdle(); break;
            }
        }

        private void TickIdle() {
            if (chainWindow > 0) {
                chainWindow--;
                if (chainWindow == 0) {
                    //窗口过期，链条提前收束（无龙卷），冷却照付
                    ResetChain(startCooldown: true);
                }
            }
        }

        /// <summary>前摇：收缩迟滞+末段猛吸（pow(t,8)），期间持续跟瞄光标</summary>
        private void TickWindup() {
            phaseTimer++;
            float t = phaseTimer / (float)WindupTime;
            Vector2 aim = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX * Player.direction);

            Player.velocity *= 0.62f;
            Player.velocity -= aim * MathF.Pow(t, 8f) * 4.5f;
            Player.GivePlayerImmuneState(6, true);
            Player.fallStart = (int)(Player.position.Y / 16f);

            //内聚水汽（owner 本地；旁观端从爆发帧的水墙起看）
            if (phaseTimer % 2 == 0) {
                FishronMotionFX.SpawnChargeGatherFX(Player.Center, t, 80f);
            }

            if (phaseTimer >= WindupTime) {
                BurstDash(aim);
            }
        }

        /// <summary>爆发帧：一帧写满冲量，锁死方向，同时铺设水墙弹幕</summary>
        private void BurstDash(Vector2 aim) {
            dashDir = aim;
            phase = 2;
            phaseTimer = 0;
            Player.velocity = dashDir * BurstSpeed;

            //基数吃玩家总伤加成（框架 §1 口径），长寿命期由墙自身逐帧刷新
            int dmg = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(WallDamage * DamageMult);
            Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, dashDir,
                ModContent.ProjectileType<TidalFinTsunamiProj>(), dmg, 2f, Player.whoAmI,
                stage, empowered ? 1f : 0f);
        }

        /// <summary>冲刺：初段复合加速→指数回巡航，近零转向，直线即承诺</summary>
        private void TickDashing() {
            phaseTimer++;
            float speed = Player.velocity.Length();
            speed = phaseTimer <= 6 ? speed * 1.012f : MathHelper.Lerp(speed, CruiseSpeed, 0.08f);
            Player.velocity = dashDir * speed;

            Player.GivePlayerImmuneState(10, true);
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.maxFallSpeed = Math.Max(Player.maxFallSpeed, speed);

            if (phaseTimer >= DashTime) {
                EndDash();
            }
        }

        private void EndDash() {
            phase = 3;
            phaseTimer = 0;

            if (stage >= maxStages) {
                //末段落点起潮汐龙卷，整链收束进冷却
                int dmg = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(TornadoDamage * DamageMult);
                Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, Vector2.Zero,
                    ModContent.ProjectileType<TidalFinTornadoProj>(), dmg, 4f, Player.whoAmI,
                    empowered ? 1f : 0f);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 0.55f, Pitch = 0.2f, MaxInstances = 2 }, Player.Center);
                cooldown = CooldownTime;
            }
        }

        /// <summary>硬刹：三帧半速衰减，随后交还控制并开接续窗口</summary>
        private void TickBrake() {
            phaseTimer++;
            Player.velocity *= 0.55f;
            Player.GivePlayerImmuneState(6, true);
            Player.fallStart = (int)(Player.position.Y / 16f);
            //刹车逆速水花（SpawnBrakeSpray 只收 NPC，这里自铺）
            if (Player.velocity.Length() > 5f) {
                FishronMotionFX.SpawnSprayCone(Player.Center + Main.rand.NextVector2Circular(20f, 14f),
                    -Player.velocity.SafeNormalize(Vector2.Zero), 2, 3f, 8f, 0.8f, 0.8f);
            }

            if (phaseTimer >= BrakeTime) {
                phase = 0;
                phaseTimer = 0;
                if (stage >= maxStages) {
                    stage = 0;
                }
                else if (pressBuffered) {
                    StartStage(stage + 1);
                }
                else {
                    chainWindow = ChainWindowTime;
                }
            }
        }
    }

    /// <summary>
    /// 强化触发时的风暴天光一闪：短暂借用 FishronStormSky 着色器画一帧雨幕雷闪，
    /// 26 帧衰减即收，克制不接管画面。纯本机表现，触发源是水墙弹幕的首帧（各端自播）
    /// </summary>
    internal sealed class TidalFinStormFlashRender : RenderHandle
    {
        /// <summary>认领槽位 1.852（错开环境渲染 NyxdepthAmbientRender 的 1.85）</summary>
        public override float Weight => 1.852f;

        private const int FlashDuration = 26;
        //纯表现计时（每端本地，非游戏状态）
        private static int flashTimer;
        private static float flashX = 0.5f;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        /// <summary>压入一次天光闪（客户端表现）</summary>
        public static void Push(Vector2 worldPos) {
            if (VaultUtils.isServer) {
                return;
            }
            flashTimer = FlashDuration;
            flashX = MathHelper.Clamp((worldPos.X - Main.screenPosition.X) / Main.screenWidth, 0.1f, 0.9f);
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu) {
                flashTimer = 0;
                return;
            }
            if (flashTimer <= 0) {
                return;
            }
            flashTimer--;

            Effect shader = EffectLoader.FishronStormSky?.Value;
            if (shader == null || noiseTex == null
                || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                flashTimer = 0;
                return;
            }

            //雷电式包络：瞬亮后指数退潮，峰值 alpha 被 uIntensity 钳在 0.3 以下
            float t = flashTimer / (float)FlashDuration;
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uIntensity"]?.SetValue(0.26f * t);
            shader.Parameters["uRain"]?.SetValue(0.5f * t);
            shader.Parameters["uFlash"]?.SetValue(0.85f * t * t);
            shader.Parameters["uFlashX"]?.SetValue(flashX);
            shader.Parameters["uAspectRatio"]?.SetValue(vpW / (float)vpH);
            //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图（合同同 FishronStormSky）
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, vpW, vpH), Color.White);
            spriteBatch.End();
        }
    }
}
