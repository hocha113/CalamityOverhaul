using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles
{
    public abstract class Lightning : ModProjectile
    {
        [VaultLoaden(CWRConstant.Masking + "ThunderTrail")]
        private static Asset<Texture2D> ThunderTrailTex = null;
        public override string Texture => CWRConstant.Masking + "StarTexture";

        #region 状态枚举
        public enum LightningState
        {
            Initializing = 0,  //初始化
            Striking = 1,      //劈击过程
            Lingering = 2,     //停留（持续可见）
            Fading = 3         //消失
        }
        #endregion

        #region 核心属性
        public ref float State => ref Projectile.ai[0];
        public ref float Hited => ref Projectile.ai[1];
        public ref float Timer => ref Projectile.localAI[0];
        public ref float ThunderWidth => ref Projectile.localAI[1];
        public ref float ThunderAlpha => ref Projectile.localAI[2];

        public Vector2 TargetPosition { get; set; }
        protected bool hasSpawned;
        /// <summary>淡出渐变</summary>
        public float FadeValue { get; set; } = 0;
        public ThunderTrail MainTrail { get; protected set; }
        public List<ThunderTrail> BranchTrails { get; protected set; } = new();
        public LinkedList<Vector2> TrailPoints { get; protected set; } = new();
        /// <summary>强度0-1</summary>
        public float Intensity { get; set; } = 1f;
        #endregion

        #region 可配置参数
        public virtual Asset<Texture2D> LightningTexture => ThunderTrailTex;
        public virtual int MaxBranches => 3;
        public virtual float BranchProbability => 0.12f;
        public virtual float BranchLengthRatio => 0.5f;
        public virtual float BaseSpeed => 16f;
        public virtual int LingerTime => 25; //约0.4秒
        /// <summary>消失时间(帧)</summary>
        public virtual int FadeTime => 15; //快速消失
        public virtual float BaseWidth => 45f; //64*0.7
        public virtual float MinBranchWidthRatio => 0.4f;
        public virtual float MaxBranchWidthRatio => 0.7f;
        #endregion

        #region 虚拟方法
        public virtual Color GetLightningColor(float factor) => new Color(103, 255, 255);

        /// <summary>宽度(强度×位置)</summary>
        public virtual float GetLightningWidth(float factor) {
            //Sin 控宽，中部略粗
            float curve = MathF.Sin(factor * MathHelper.Pi);
            float shapeFactor = curve * (0.6f + 0.4f * MathF.Sin(factor * MathHelper.Pi * 0.5f));
            return ThunderWidth * shapeFactor * Intensity;
        }

        public virtual float GetAlpha(float factor) {
            if (factor < FadeValue)
                return 0;

            float alpha = ThunderAlpha * (factor - FadeValue) / (1 - FadeValue);

            return alpha * (0.7f + 0.3f * Intensity);
        }

        public abstract Vector2 FindTargetPosition();

        public virtual void OnStrike() { }

        public virtual void OnHit() { }
        #endregion

        #region 基础设置
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 600;
            SetLightningDefaults();
        }

        public virtual void SetLightningDefaults() { }
        #endregion

        #region 核心AI逻辑
        public override void AI() {
            if (!hasSpawned) {
                hasSpawned = true;
                TargetPosition = FindTargetPosition();
                if (TargetPosition == default) {
                    Projectile.Kill();
                    return;
                }
            }

            Lighting.AddLight(Projectile.Center, GetLightningColor(0.5f).ToVector3() * 0.8f * Intensity);

            switch ((LightningState)State) {
                case LightningState.Initializing:
                    InitializeStrike();
                    break;
                case LightningState.Striking:
                    UpdateStrike();
                    break;
                case LightningState.Lingering:
                    UpdateLinger();
                    break;
                case LightningState.Fading:
                    UpdateFade();
                    break;
            }
        }

        protected virtual void InitializeStrike() {
            State = (float)LightningState.Striking;
            ThunderAlpha = 1f;
            ThunderWidth = BaseWidth;
            Projectile.extraUpdates = 6;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 1000;

            Projectile.velocity = (TargetPosition - Projectile.Center).SafeNormalize(Vector2.Zero) * BaseSpeed;

            TrailPoints.Clear();
            TrailPoints.AddLast(Projectile.Center);

            if (LightningTexture != null) {
                MainTrail = new ThunderTrail(LightningTexture, GetLightningWidth, GetLightningColor, GetAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 3,
                    BasePositions = new Vector2[] { Projectile.Center, Projectile.Center, Projectile.Center }
                };
                MainTrail.SetRange((0, 6)); //减小随机偏移
                MainTrail.SetExpandWidth(5); //减小扩展宽度
            }

            Projectile.netUpdate = true;
        }

        protected virtual void UpdateStrike() {
            Timer++;

            float distance = Projectile.Center.Distance(TargetPosition);
            float baseSpeed = Projectile.velocity.Length();

            if (distance < baseSpeed * 2f) {
                StartLinger();
                return;
            }

            UpdateStrikeMovement();

            UpdateTrails();

            //降频分叉
            if (Timer % 12 == 0 && Main.rand.NextFloat() < BranchProbability && BranchTrails.Count < MaxBranches) {
                CreateBranch();
            }
        }

        protected virtual void UpdateStrikeMovement() {
            float baseSpeed = Projectile.velocity.Length();
            float distance = Projectile.Center.Distance(TargetPosition);

            float selfAngle = Projectile.velocity.ToRotation();
            float targetAngle = (TargetPosition - Projectile.Center).ToRotation();
            float trackingFactor = 1 - Math.Clamp(distance / 500, 0f, 1f);

            float newAngle = MathHelper.Lerp(selfAngle, targetAngle, 0.8f + 0.2f * trackingFactor);

            //压低扰动
            float sinOffset = MathF.Sin(Timer * 0.35f) * 0.4f;
            newAngle += sinOffset;

            if (Timer % 8 == 0) {
                float randomAngle = Main.rand.NextFloat(-0.3f, 0.3f);
                newAngle += randomAngle;
            }

            Projectile.velocity = newAngle.ToRotationVector2() * baseSpeed;

            Projectile.position += new Vector2(MathF.Sin(Timer * 0.25f), MathF.Cos(Timer * 0.2f)) * 1.2f;
        }

        protected virtual void UpdateTrails() {
            if (MainTrail != null) {
                TrailPoints.AddLast(Projectile.Center);

                if (TrailPoints.Count > 100) {
                    TrailPoints.RemoveFirst();
                }

                if (Timer % Math.Max(1, Projectile.MaxUpdates / 2) == 0) {
                    MainTrail.BasePositions = TrailPoints.ToArray();
                    if (MainTrail.BasePositions.Length > 2) {
                        MainTrail.RandomThunder();
                    }
                }
            }

            foreach (var branch in BranchTrails) {
                if (Timer % 8 == 0) {
                    branch.RandomThunder();
                }
            }
        }

        protected virtual void CreateBranch() {
            if (LightningTexture == null || TrailPoints.Count < 5) return;

            var points = TrailPoints.ToArray();

            //分叉点取前2/3段
            int maxIndex = (int)(points.Length * 0.67f);
            int branchIndex = Main.rand.Next(Math.Max(5, points.Length / 3), maxIndex);
            Vector2 branchStart = points[branchIndex];

            List<Vector2> branchPoints = new List<Vector2> { branchStart };

            Vector2 mainDirection = (TargetPosition - Projectile.Center).SafeNormalize(Vector2.UnitY);

            //分叉偏30-60度
            float sideSign = Main.rand.NextBool() ? 1 : -1;
            float branchAngle = mainDirection.ToRotation() + sideSign * Main.rand.NextFloat(0.5f, 1.0f);

            //分叉较短
            int branchLength = (int)(TrailPoints.Count * BranchLengthRatio * Main.rand.NextFloat(0.5f, 0.8f));
            branchLength = Math.Max(8, Math.Min(branchLength, 125)); //限制长度

            Vector2 currentPos = branchStart;
            Vector2 branchDirection = branchAngle.ToRotationVector2();

            for (int i = 0; i < branchLength; i++) {
                float progressFactor = i / (float)branchLength;
                branchAngle += 0.05f * sideSign * (1f - progressFactor);
                float downwardBias = 0.1f * progressFactor;
                branchDirection = branchAngle.ToRotationVector2();
                branchDirection.Y += downwardBias;
                branchDirection = branchDirection.SafeNormalize(Vector2.UnitY);

                //偏移递减
                float offset = Main.rand.NextFloat(-12f, 12f) * (1f - progressFactor * 0.5f);
                Vector2 perpendicular = branchDirection.RotatedBy(MathHelper.PiOver2);

                float stepSize = Main.rand.NextFloat(10f, 14f) * (1f - progressFactor * 0.3f);
                currentPos += branchDirection * stepSize + perpendicular * offset;
                branchPoints.Add(currentPos);

                //越远越易提前结束
                if (Main.rand.NextFloat() < 0.03f + progressFactor * 0.07f) break;
            }

            if (branchPoints.Count > 3) {
                float widthRatio = Main.rand.NextFloat(MinBranchWidthRatio, MaxBranchWidthRatio);

                ThunderTrail branch = new ThunderTrail(LightningTexture,
                    factor => GetLightningWidth(factor) * widthRatio * 0.8f, //分叉更细
                    factor => GetLightningColor(factor) * Main.rand.NextFloat(0.75f, 0.95f),
                    GetAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                    BasePositions = branchPoints.ToArray()
                };
                branch.SetRange((0, 4)); //更小的随机范围
                branch.SetExpandWidth(3);
                branch.RandomThunder();

                BranchTrails.Add(branch);
            }
        }

        protected virtual void StartLinger() {
            State = (float)LightningState.Lingering;
            Timer = 0;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 0;
            Hited = 1;

            if (MainTrail != null && TrailPoints.Count > 2) {
                MainTrail.BasePositions = TrailPoints.ToArray();
                MainTrail.RandomThunder();
            }

            OnStrike();
            OnHit();
            Projectile.netUpdate = true;
        }

        protected virtual void UpdateLinger() {
            Timer++;

            //满亮度停留
            if (MainTrail != null) {
                MainTrail.CanDraw = true;
            }

            foreach (var branch in BranchTrails) {
                branch.CanDraw = true;
            }

            if (Timer % 12 == 0 && Timer < LingerTime * 0.6f) {
                MainTrail?.RandomThunder();
                foreach (var branch in BranchTrails) {
                    branch.RandomThunder();
                }
            }

            if (Timer > LingerTime) {
                StartFade();
            }
        }

        protected virtual void StartFade() {
            State = (float)LightningState.Fading;
            Timer = 0;
            Projectile.timeLeft = FadeTime + 10;
            //入淡出沿一次性同步；淡出本身由 Timer 各端本地推导，不再逐帧发包
            Projectile.netUpdate = true;
        }

        protected virtual void UpdateFade() {
            Timer++;

            //线性淡出
            FadeValue = Timer / FadeTime;

            ThunderWidth = BaseWidth * (1f - FadeValue * 0.6f);

            ThunderAlpha = 1f - FadeValue;

            if (MainTrail != null) {
                MainTrail.CanDraw = true;
            }

            foreach (var branch in BranchTrails) {
                branch.CanDraw = true;
            }

            if (Timer > FadeTime) {
                Projectile.Kill();
            }
        }
        #endregion

        #region 碰撞判定
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == (float)LightningState.Striking) {
                StartLinger();
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (State == (float)LightningState.Striking) {
                StartLinger();
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (State == (float)LightningState.Striking) {
                StartLinger();
            }
        }

        public override bool? CanDamage() => State == (float)LightningState.Striking && Hited == 0;
        #endregion

        #region 网络同步
        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(Timer);
            writer.Write(FadeValue);
            writer.WriteVector2(TargetPosition);
            writer.Write(hasSpawned);
            writer.Write(Intensity);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Timer = reader.ReadSingle();
            FadeValue = reader.ReadSingle();
            TargetPosition = reader.ReadVector2();
            hasSpawned = reader.ReadBoolean();
            Intensity = reader.ReadSingle();
        }
        #endregion

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            if (Hited == 0 && State == (float)LightningState.Striking) {
                DrawLightningCore(lightColor);
            }

            if (State > (float)LightningState.Initializing) {
                DrawTrails();
            }

            return false;
        }

        protected virtual void DrawLightningCore(Color lightColor) {
        }

        protected virtual void DrawTrails() {
            if (MainTrail != null && ((LightningState)State != LightningState.Striking || Timer >= 3)) {
                MainTrail.DrawThunder(Main.instance.GraphicsDevice);
            }

            foreach (var branch in BranchTrails) {
                branch?.DrawThunder(Main.instance.GraphicsDevice);
            }
        }
        #endregion

        #region 工具方法
        public static float Smoother(int timer, int maxTime) {
            if (maxTime <= 0) return 1f;
            float factor = Math.Clamp((float)timer / maxTime, 0f, 1f);
            return factor * factor;
        }
        #endregion
    }
}
