using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>支架枪托，站定约1秒进炮台，零散布+射程弹速穿透，移动收架</summary>
    internal sealed class BraceStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //精准钢银
        public override Color TintColor => new(160, 185, 210);

        /// <summary>架设状态，收起→酝酿→展开→炮台→收架</summary>
        internal enum RigState : byte
        {
            Packed,
            Arming,
            Deploying,
            Deployed,
            Retracting,
        }

        //═════ 可调参数 ═════
        private const float StationaryThreshold = 0.6f; //站定速度阈值，与守望枪托一致
        private const int ArmFrames = 55;               //首次架设酝酿帧数（约1秒）
        private const int QuickArmFrames = 20;          //快速重架酝酿帧数
        private const int RearmWindowFrames = 480;      //收架后快速重架窗口 8s
        private const int DeployFrames = 14;            //展开动画帧数
        private const int RetractFrames = 10;           //收架缓冲帧数，期间不重新酝酿
        private const float BaseSpreadAdd = -0.2f;      //携行基础散布
        private const float BaseAttackSpeedAdd = -0.05f;//携行基础攻速（重支架）
        private const float DeploySpreadAdd = -1f;      //架设散布（叠后归零）
        private const float DeployBeamSpeedAdd = 0.6f;  //架设弹速
        private const float DeployBeamLifeAdd = 0.6f;   //架设射程（光束生命）
        private const int DeployPierceAdd = 1;          //架设额外穿透
        private const float DeployHomingAdd = 0.35f;    //架设弹道稳定辅助
        private const int DeployCritAdd = 8;            //架设暴击，On_ModifyWeaponCrit，光束/激光通吃

        internal RigState State { get; private set; } = RigState.Packed;
        private int stateTimer;
        private float tickCarry;
        private uint lastTick;
        private int rearmWindow;
        private float rearmCarry;
        private int armFramesNeed = ArmFrames;

        /// <summary>酝酿进度 0~1，展开后保持 1，供预备光环</summary>
        internal float ArmProgress => State switch {
            RigState.Arming => MathHelper.Clamp(stateTimer / (float)Math.Max(armFramesNeed, 1), 0f, 1f),
            RigState.Deploying or RigState.Deployed => 1f,
            _ => 0f,
        };

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += BaseSpreadAdd;
            ctx.AttackSpeedMul += BaseAttackSpeedAdd;
            //炮台注入弹道拉满+暴击，伤倍率不碰（区隔守望）
            if (State == RigState.Deployed) {
                ctx.SpreadMul += DeploySpreadAdd;
                ctx.BeamSpeedMul += DeployBeamSpeedAdd;
                ctx.BeamLifeMul += DeployBeamLifeAdd;
                ctx.BeamExtraPierce += DeployPierceAdd;
                ctx.HomingMul += DeployHomingAdd;
                ctx.CritAdd += DeployCritAdd;
            }
        }

        public override void OnPlayerUpdate(Player player) {
            if (player == null || !player.active) {
                return;
            }
            //久未 tick 归零，防装回跳过架设
            if (Main.GameUpdateCount - lastTick > 4) {
                ResetState();
                rearmWindow = 0;
                rearmCarry = 0f;
            }
            lastTick = Main.GameUpdateCount;
            TickDown(ref rearmWindow, ref rearmCarry);

            if (player.dead || player.HeldItem == null || player.HeldItem.type != SHPCOverride.ID) {
                ResetState();
                return;
            }

            //须静止且踩实心/平台，排除钩爪/绳索/坐骑悬空
            bool grounded = MathF.Abs(player.velocity.Y) < 0.05f
                && player.grapCount == 0 && !player.pulley
                && !(player.mount.Active && player.mount.CanFly())
                && Collision.SolidCollision(new Vector2(player.position.X, player.Bottom.Y), player.width, 6, true);
            bool still = grounded
                && player.velocity.LengthSquared() < StationaryThreshold * StationaryThreshold;
            UpdateState(still);

            //支架实体，仅 owner 端
            if (player.whoAmI != Main.myPlayer || State == RigState.Packed) {
                return;
            }
            int rigType = ModContent.ProjectileType<SHPCBraceRigProj>();
            if (player.ownedProjectileCounts[rigType] < 1) {
                Projectile.NewProjectile(player.GetSource_FromThis(),
                    player.Bottom, Vector2.Zero, rigType, 0, 0f, player.whoAmI);
            }
        }

        private void UpdateState(bool still) {
            int tick = TickUp(ref tickCarry);
            switch (State) {
                case RigState.Packed:
                    if (still) {
                        State = RigState.Arming;
                        stateTimer = 0;
                        //快速重架窗口内酝酿缩短
                        armFramesNeed = rearmWindow > 0 ? QuickArmFrames : ArmFrames;
                    }
                    break;
                case RigState.Arming:
                    if (!still) {
                        ResetState();
                        break;
                    }
                    stateTimer += tick;
                    if (stateTimer >= armFramesNeed) {
                        State = RigState.Deploying;
                        stateTimer = 0;
                    }
                    break;
                case RigState.Deploying:
                    if (!still) {
                        BeginRetract();
                        break;
                    }
                    stateTimer += tick;
                    if (stateTimer >= DeployFrames) {
                        State = RigState.Deployed;
                        stateTimer = 0;
                    }
                    break;
                case RigState.Deployed:
                    if (!still) {
                        BeginRetract();
                    }
                    break;
                case RigState.Retracting:
                    stateTimer += tick;
                    if (stateTimer >= RetractFrames) {
                        ResetState();
                    }
                    break;
            }
        }

        /// <summary>打断进收架，刷新快速重架窗口</summary>
        private void BeginRetract() {
            State = RigState.Retracting;
            stateTimer = 0;
            rearmWindow = RearmWindowFrames;
            rearmCarry = 0f;
        }

        private void ResetState() {
            State = RigState.Packed;
            stateTimer = 0;
            tickCarry = 0f;
        }

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            //炮台激光染工程绿；数值强化只吃左键光束
            if (State != RigState.Deployed) {
                return;
            }
            laser.ThemeCore = new Color(180, 255, 200);
            laser.ThemeGlow = new Color(60, 220, 125);
            laser.ThemeAura = new Color(10, 95, 50);
            laser.ThemeParticleMain = new Color(135, 255, 175);
            laser.ThemeParticleEdge = new Color(40, 190, 105);
        }

        /// <summary>当前装备的本改件实例，未装备 null</summary>
        internal static BraceStockModule GetOn(Player player) {
            if (player == null) {
                return null;
            }
            SHPCPlayer sp = SHPCPlayer.Get(player);
            if (sp == null) {
                return null;
            }
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                if (sp.GetModule(i)?.ModItem is BraceStockModule m) {
                    return m;
                }
            }
            return null;
        }
    }

    /// <summary>三脚支架，展开/收起+锚定场+后坐吸收，卸改件/换武自毁</summary>
    internal sealed class SHPCBraceRigProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //锚定工程绿 + 钢银金属
        private static readonly Color AnchorMain = new(110, 255, 150);
        private static readonly Color AnchorAccent = new(225, 255, 235);
        private static readonly Color SteelDark = new(88, 98, 118);
        private static readonly Color SteelLight = new(202, 214, 230);

        private const float DeployVisualRate = 1f / 14f;
        private const float RetractVisualRate = 1f / 9f;
        private const float LegSpreadX = 24f;   //展开后前后脚距锚点半宽
        private const float HubHeight = 28f;    //展开后托架抬升高度

        private float visualDeploy;
        private float armGlow;
        private float recoil;
        private float recoilFlash;
        private int prevItemAnimation;
        private float idleParticleTimer;
        private bool retractCued;
        private Vector2 anchorPos;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 8;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            BraceStockModule module = owner != null && owner.active && !owner.dead
                && owner.HeldItem != null && owner.HeldItem.type == SHPCOverride.ID
                ? BraceStockModule.GetOn(owner) : null;
            if (module == null) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 8;

            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                anchorPos = owner.Bottom;
            }

            BraceStockModule.RigState state = module.State;
            //酝酿跟脚底，Deploying 才锁坐标；Packed 残影不跟
            if (state == BraceStockModule.RigState.Arming
                || (state == BraceStockModule.RigState.Packed && visualDeploy <= 0.05f && armGlow <= 0.25f)) {
                anchorPos = owner.Bottom;
            }
            Projectile.Center = anchorPos - new Vector2(0f, 16f);

            armGlow = MathHelper.Lerp(armGlow, module.ArmProgress, 0.35f);

            //视觉插值跟 TimeGear，与 TickUp 同步
            float ts = TimeGear.TimeScale;
            bool wantDeploy = state is BraceStockModule.RigState.Deploying or BraceStockModule.RigState.Deployed;
            float prevVisual = visualDeploy;
            visualDeploy = wantDeploy
                ? MathF.Min(visualDeploy + DeployVisualRate * ts, 1f)
                : MathF.Max(visualDeploy - RetractVisualRate * ts, 0f);

            //关键帧，弹出→落地→锁定
            if (wantDeploy) {
                retractCued = false;
                if (prevVisual < 0.12f && visualDeploy >= 0.12f) {
                    UnfoldFX();
                }
                if (prevVisual < 0.7f && visualDeploy >= 0.7f) {
                    DeployImpactFX();
                }
                if (prevVisual < 0.95f && visualDeploy >= 0.95f) {
                    LockdownFX();
                }
            }
            else if (prevVisual > 0.15f && !retractCued) {
                retractCued = true;
                RetractFX();
            }

            if (state == BraceStockModule.RigState.Packed && visualDeploy <= 0.01f && armGlow <= 0.02f) {
                Projectile.Kill();
                return;
            }

            //炮台吸收后坐；激光持续光柱不做后坐
            bool laserActive = owner.ownedProjectileCounts[ModContent.ProjectileType<CyberPrismLaserProj>()] > 0;
            if (visualDeploy > 0.9f && !laserActive && owner.ItemAnimationActive
                && owner.altFunctionUse != 2 && owner.itemAnimation > prevItemAnimation) {
                recoil = 1f;
                recoilFlash = 1f;
            }
            prevItemAnimation = owner.itemAnimation;
            recoil = MathF.Max(recoil - 0.09f * ts, 0f);
            recoilFlash = MathF.Max(recoilFlash - 0.12f * ts, 0f);

            //炮台稳态，锚点两侧压地能量粒
            if (state == BraceStockModule.RigState.Deployed && visualDeploy > 0.95f
                && Main.netMode != NetmodeID.Server) {
                idleParticleTimer += ts;
                if (idleParticleTimer >= 26f) {
                    idleParticleTimer = 0f;
                    Vector2 pos = anchorPos + new Vector2(Main.rand.NextFloat(-LegSpreadX, LegSpreadX), -2f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(pos,
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-0.7f, -0.2f)),
                        AnchorMain, Main.rand.NextFloat(0.4f, 0.7f))
                        .Configure(AnchorAccent, Main.rand.Next(12, 20));
                }
            }

            Lighting.AddLight(anchorPos - new Vector2(0f, 8f),
                AnchorMain.ToVector3() * (0.14f + 0.3f * visualDeploy));
        }

        /// <summary>支架弹出音</summary>
        private void UnfoldFX() {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.35f, Pitch = -0.45f }, anchorPos);
        }

        /// <summary>脚落地，重音+尘土</summary>
        private void DeployImpactFX() {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = -0.5f }, anchorPos);
            for (int side = -1; side <= 1; side++) {
                Vector2 footPos = anchorPos + new Vector2(side * LegSpreadX, 0f);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(footPos + Main.rand.NextVector2Circular(4f, 2f),
                        new Vector2(Main.rand.NextFloat(-1.2f, 1.2f) + side * 0.8f, Main.rand.NextFloat(-1.4f, -0.4f)),
                        new Color(125, 115, 98), Main.rand.NextFloat(0.5f, 0.8f))
                        .Configure(Main.rand.Next(20, 32), 0.5f, Main.rand.NextFloat(0.01f, 0.03f));
                }
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(footPos,
                        new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2.5f, -1f)),
                        new Color(210, 190, 150), Main.rand.NextFloat(0.35f, 0.6f))
                        .Configure(true, Main.rand.Next(8, 14));
                }
            }
        }

        /// <summary>锁定，上膛音+锚环</summary>
        private void LockdownFX() {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.35f }, anchorPos);
            PRTLoader.NewParticle<PRT_StarPulseRing>(anchorPos - new Vector2(0f, 6f), Vector2.Zero,
                AnchorMain with { A = 0 }, 0.05f).Configure(0.05f, 0.32f, 16);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(anchorPos + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1.6f, -0.5f)),
                    AnchorMain, Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(AnchorAccent, Main.rand.Next(10, 18));
            }
        }

        /// <summary>收架启动短音</summary>
        private void RetractFX() {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.38f, Pitch = 0.75f }, anchorPos);
        }

        public override void OnKill(int timeLeft) {
            //强制打断才放解体火花
            if (Main.netMode == NetmodeID.Server || visualDeploy < 0.25f) {
                return;
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_Spark>(anchorPos - new Vector2(0f, 10f),
                    Main.rand.NextVector2Circular(3f, 2.5f),
                    AnchorMain, Main.rand.NextFloat(0.35f, 0.65f))
                    .Configure(true, Main.rand.Next(8, 14));
            }
        }

        //═════ 绘制 ═════

        private static float Ease(float t) => 1f - MathF.Pow(1f - t, 3f);

        /// <summary>支架几何，锚点+进度推托架/膝/脚</summary>
        private void GetRigPose(out Vector2 hub, out Vector2[] knees, out Vector2[] feet) {
            float rise = Ease(visualDeploy);
            hub = anchorPos + new Vector2(0f, -(9f + (HubHeight - 9f) * rise) + recoil * 3.5f);
            feet = [
                anchorPos + new Vector2(-LegSpreadX * rise, 0f),
                anchorPos + new Vector2(LegSpreadX * rise, 0f),
                anchorPos,
            ];
            knees = [
                Vector2.Lerp(hub, feet[0], 0.45f) + new Vector2(-5f * rise, recoil * 1.5f),
                Vector2.Lerp(hub, feet[1], 0.45f) + new Vector2(5f * rise, recoil * 1.5f),
            ];
        }

        private static void DrawStrut(SpriteBatch sb, Texture2D px, Vector2 a, Vector2 b, float thickness, Color color) {
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(px, a - Main.screenPosition, null, color, d.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(len, thickness), SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawAnchorField();

            if (visualDeploy <= 0.02f) {
                return false;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return false;
            }

            GetRigPose(out Vector2 hub, out Vector2[] knees, out Vector2[] feet);
            float alpha = MathHelper.Clamp(visualDeploy * 1.6f, 0f, 1f);
            Color dark = Color.Lerp(SteelDark, Color.White, 0.1f).MultiplyRGB(Color.Lerp(lightColor, Color.White, 0.35f)) * alpha;
            Color light = SteelLight.MultiplyRGB(Color.Lerp(lightColor, Color.White, 0.45f)) * alpha;

            //三腿，暗底+亮心
            for (int i = 0; i < 2; i++) {
                DrawStrut(Main.spriteBatch, px, hub, knees[i], 4f, dark);
                DrawStrut(Main.spriteBatch, px, knees[i], feet[i], 3.2f, dark);
                DrawStrut(Main.spriteBatch, px, hub, knees[i], 1.4f, light);
                DrawStrut(Main.spriteBatch, px, knees[i], feet[i], 1.2f, light);
                //脚垫
                DrawStrut(Main.spriteBatch, px, feet[i] + new Vector2(-5f, -1f), feet[i] + new Vector2(5f, -1f), 2.6f, dark);
            }
            //中撑直柱
            DrawStrut(Main.spriteBatch, px, hub, feet[2], 3.4f, dark);
            DrawStrut(Main.spriteBatch, px, hub, feet[2], 1.2f, light);

            //托架横梁+V叉
            DrawStrut(Main.spriteBatch, px, hub + new Vector2(-7f, 0f), hub + new Vector2(7f, 0f), 5f, dark);
            DrawStrut(Main.spriteBatch, px, hub + new Vector2(-7f, -1f), hub + new Vector2(7f, -1f), 1.4f, light);
            DrawStrut(Main.spriteBatch, px, hub + new Vector2(0f, -2f), hub + new Vector2(-6f, -10f), 2.2f, dark);
            DrawStrut(Main.spriteBatch, px, hub + new Vector2(0f, -2f), hub + new Vector2(6f, -10f), 2.2f, dark);
            return false;
        }

        /// <summary>地面锚定场，SHPCModBrace.fx</summary>
        private void DrawAnchorField() {
            if (visualDeploy <= 0.01f && armGlow <= 0.02f) {
                return;
            }
            Effect shader = EffectLoader.SHPCModBrace?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) {
                return;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.03f);
            shader.Parameters["fadeAlpha"]?.SetValue(1f);
            shader.Parameters["deployProgress"]?.SetValue(Ease(visualDeploy));
            shader.Parameters["armProgress"]?.SetValue(armGlow);
            shader.Parameters["recoilFlash"]?.SetValue(recoilFlash);
            shader.Parameters["mainColor"]?.SetValue(AnchorMain.ToVector3());
            shader.Parameters["accentColor"]?.SetValue(AnchorAccent.ToVector3());

            //画布中心在地面上，fx GROUND_Y=0.69
            Vector2 drawPos = anchorPos + new Vector2(0f, -12f) - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White, 0f,
                canvas.Size() * 0.5f, new Vector2(170f, 64f), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (visualDeploy <= 0.02f) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null || glow == null) {
                return;
            }

            GetRigPose(out Vector2 hub, out Vector2[] knees, out Vector2[] feet);
            float alpha = MathHelper.Clamp(visualDeploy * 1.5f, 0f, 1f);
            float breathe = 0.7f + 0.3f * MathF.Sin((float)Main.timeForVisualEffects * 0.11f);
            Color energy = AnchorMain * (alpha * (0.55f + recoilFlash * 0.45f));

            //液压杆亮线
            Vector2 hubLow = hub + new Vector2(0f, 4f);
            DrawStrut(spriteBatch, px, hubLow, knees[0], 1.3f, energy * breathe);
            DrawStrut(spriteBatch, px, hubLow, knees[1], 1.3f, energy * breathe);

            //关节光点
            Vector2 glowOrigin = glow.Size() * 0.5f;
            foreach (Vector2 joint in new[] { hub, knees[0], knees[1], feet[0], feet[1] }) {
                spriteBatch.Draw(glow, joint - Main.screenPosition, null,
                    energy * 0.8f, 0f, glowOrigin, 0.09f + recoilFlash * 0.04f, SpriteEffects.None, 0f);
            }

            //托架状态灯
            float lampPulse = breathe + recoilFlash * 0.8f;
            spriteBatch.Draw(glow, hub + new Vector2(0f, -6f) - Main.screenPosition, null,
                Color.Lerp(AnchorMain, AnchorAccent, recoilFlash) * (alpha * lampPulse * 0.9f),
                0f, glowOrigin, 0.16f, SpriteEffects.None, 0f);
        }
    }
}
