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
    /// 闪电基类
    public abstract class Lightning : ModProjectile
    {
        [VaultLoaden(CWRConstant.Masking + "ThunderTrail")]
        private static Asset<Texture2D> ThunderTrailTex = null;
        public override string Texture => CWRConstant.Masking + "StarTexture";

        #region 状态枚举
        /// 闪电状态
        public enum LightningState
        {
            Initializing = 0,  //初始化
            Striking = 1,      //劈击过程
            Lingering = 2,     //停留（持续可见）
            Fading = 3         //消失
        }
        #endregion

        #region 核心属性
        /// <summary>当前状态</summary>
        public ref float State => ref Projectile.ai[0];
        /// <summary>是否已命中目标</summary>
        public ref float Hited => ref Projectile.ai[1];
        /// <summary>计时器</summary>
        public ref float Timer => ref Projectile.localAI[0];
        /// <summary>闪电宽度</summary>
        public ref float ThunderWidth => ref Projectile.localAI[1];
        /// <summary>闪电透明度</summary>
        public ref float ThunderAlpha => ref Projectile.localAI[2];

        /// <summary>目标位置</summary>
        public Vector2 TargetPosition { get; set; }
        /// <summary>是否已生成</summary>
        protected bool hasSpawned;
        /// <summary>渐变值，用于消失效果</summary>
        public float FadeValue { get; set; } = 0;
        /// <summary>闪电轨迹</summary>
        public ThunderTrail MainTrail { get; protected set; }
        /// <summary>分叉轨迹列表</summary>
        public List<ThunderTrail> BranchTrails { get; protected set; } = new();
        /// <summary>轨迹点列表</summary>
        public LinkedList<Vector2> TrailPoints { get; protected set; } = new();
        /// <summary>闪电强度（0-1），影响亮度、宽度等</summary>
        public float Intensity { get; set; } = 1f;
        #endregion

        #region 可配置参数
        /// <summary>闪电纹理</summary>
        public virtual Asset<Texture2D> LightningTexture => ThunderTrailTex;
        /// <summary>最大分叉数量</summary>
        public virtual int MaxBranches => 3;
        /// <summary>分叉概率</summary>
        public virtual float BranchProbability => 0.12f;
        /// <summary>分叉长度比例</summary>
        public virtual float BranchLengthRatio => 0.5f;
        /// <summary>基础移动速度</summary>
        public virtual float BaseSpeed => 16f;
        /// <summary>停留帧数</summary>
        public virtual int LingerTime => 25; //约0.4秒
        /// <summary>消失时间（帧数）</summary>
        public virtual int FadeTime => 15; //快速消失
        /// <summary>基础宽度</summary>
        public virtual float BaseWidth => 45f; //缩小30%：64 * 0.7 ≈ 45
        /// <summary>最小分叉宽度比例</summary>
        public virtual float MinBranchWidthRatio => 0.4f;
        /// <summary>最大分叉宽度比例</summary>
        public virtual float MaxBranchWidthRatio => 0.7f;
        #endregion

        #region 虚拟方法
        /// 闪电颜色
        public virtual Color GetLightningColor(float factor) => new Color(103, 255, 255);

        /// 宽度函数(强度×位置)
        public virtual float GetLightningWidth(float factor) {
            //Sin 曲线控宽，主干中部略粗
            float shapeFactor = curve * (0.6f + 0.4f * MathF.Sin(factor * MathHelper.Pi * 0.5f));
            return ThunderWidth * shapeFactor * Intensity;
        }

        /// 透明度
        public virtual float GetAlpha(float factor) {
            if (factor < FadeValue)
                return 0;

            float alpha = ThunderAlpha * (factor - FadeValue) / (1 - FadeValue);

            //强度调制 alpha
            return alpha * (0.7f + 0.3f * Intensity);
        }

        /// 寻找目标位置
        public abstract Vector2 FindTargetPosition();

        /// 劈击特效
        public virtual void OnStrike() { }

        /// 命中效果
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

        /// 闪电专用默认值
        public virtual void SetLightningDefaults() { }
        #endregion

        #region 核心AI逻辑
        public override void AI() {
            //初始化
            if (!hasSpawned) {
                hasSpawned = true;
                TargetPosition = FindTargetPosition();
                if (TargetPosition == default) {
                    Projectile.Kill();
                    return;
                }
            }

            //添加光源
            Lighting.AddLight(Projectile.Center, GetLightningColor(0.5f).ToVector3() * 0.8f * Intensity);

            //状态机
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

        /// 初始化劈击
        protected virtual void InitializeStrike() {
            State = (float)LightningState.Striking;
            ThunderAlpha = 1f;
            ThunderWidth = BaseWidth;
            Projectile.extraUpdates = 6;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 1000;

            //设置速度
            Projectile.velocity = (TargetPosition - Projectile.Center).SafeNormalize(Vector2.Zero) * BaseSpeed;

            //初始化轨迹
            TrailPoints.Clear();
            TrailPoints.AddLast(Projectile.Center);

            //创建主轨迹
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

        /// 更新劈击
        protected virtual void UpdateStrike() {
            Timer++;

            float distance = Projectile.Center.Distance(TargetPosition);
            float baseSpeed = Projectile.velocity.Length();

            //检查是否到达目标
            if (distance < baseSpeed * 2f) {
                StartLinger();
                return;
            }

            //更新速度和位置
            UpdateStrikeMovement();

            //更新轨迹
            UpdateTrails();

            //降频分叉，分布更自然
            if (Timer % 12 == 0 && Main.rand.NextFloat() < BranchProbability && BranchTrails.Count < MaxBranches) {
                CreateBranch();
            }
        }

        /// 更新劈击移动
        protected virtual void UpdateStrikeMovement() {
            float baseSpeed = Projectile.velocity.Length();
            float distance = Projectile.Center.Distance(TargetPosition);

            //基础朝向
            float selfAngle = Projectile.velocity.ToRotation();
            float targetAngle = (TargetPosition - Projectile.Center).ToRotation();
            float trackingFactor = 1 - Math.Clamp(distance / 500, 0f, 1f);

            //角度插值
            float newAngle = MathHelper.Lerp(selfAngle, targetAngle, 0.8f + 0.2f * trackingFactor);

            //压低扰动，轨迹更直
            float sinOffset = MathF.Sin(Timer * 0.35f) * 0.4f;
            newAngle += sinOffset;

            //减少随机抖动
            if (Timer % 8 == 0) {
                float randomAngle = Main.rand.NextFloat(-0.3f, 0.3f);
                newAngle += randomAngle;
            }

            Projectile.velocity = newAngle.ToRotationVector2() * baseSpeed;

            //减小位置抖动
            Projectile.position += new Vector2(MathF.Sin(Timer * 0.25f), MathF.Cos(Timer * 0.2f)) * 1.2f;
        }

        /// 更新轨迹
        protected virtual void UpdateTrails() {
            if (MainTrail != null) {
                TrailPoints.AddLast(Projectile.Center);

                //限制轨迹点数量
                if (TrailPoints.Count > 100) {
                    TrailPoints.RemoveFirst();
                }

                //更新主轨迹
                if (Timer % Math.Max(1, Projectile.MaxUpdates / 2) == 0) {
                    MainTrail.BasePositions = TrailPoints.ToArray();
                    if (MainTrail.BasePositions.Length > 2) {
                        MainTrail.RandomThunder();
                    }
                }
            }

            //更新分叉轨迹
            foreach (var branch in BranchTrails) {
                if (Timer % 8 == 0) {
                    branch.RandomThunder();
                }
            }
        }

        /// 创建分叉
        protected virtual void CreateBranch() {
            if (LightningTexture == null || TrailPoints.Count < 5) return;

            var points = TrailPoints.ToArray();

            //从前2/3段选择分叉点，避免末端分叉
            int maxIndex = (int)(points.Length * 0.67f);
            int branchIndex = Main.rand.Next(Math.Max(5, points.Length / 3), maxIndex);
            Vector2 branchStart = points[branchIndex];

            List<Vector2> branchPoints = new List<Vector2> { branchStart };

            //计算主干方向
            Vector2 mainDirection = (TargetPosition - Projectile.Center).SafeNormalize(Vector2.UnitY);

            //分叉角度：向两侧偏离30-60度
            float sideSign = Main.rand.NextBool() ? 1 : -1;
            float branchAngle = mainDirection.ToRotation() + sideSign * Main.rand.NextFloat(0.5f, 1.0f);

            //分叉长度：较短，更自然
            int branchLength = (int)(TrailPoints.Count * BranchLengthRatio * Main.rand.NextFloat(0.5f, 0.8f));
            branchLength = Math.Max(8, Math.Min(branchLength, 125)); //限制长度

            Vector2 currentPos = branchStart;
            Vector2 branchDirection = branchAngle.ToRotationVector2();

            for (int i = 0; i < branchLength; i++) {
                //重力偏置
                float progressFactor = i / (float)branchLength;
                branchAngle += 0.05f * sideSign * (1f - progressFactor);
                float downwardBias = 0.1f * progressFactor;
                branchDirection = branchAngle.ToRotationVector2();
                branchDirection.Y += downwardBias;
                branchDirection = branchDirection.SafeNormalize(Vector2.UnitY);

                //随机偏移量递减
                float offset = Main.rand.NextFloat(-12f, 12f) * (1f - progressFactor * 0.5f);
                Vector2 perpendicular = branchDirection.RotatedBy(MathHelper.PiOver2);

                float stepSize = Main.rand.NextFloat(10f, 14f) * (1f - progressFactor * 0.3f);
                currentPos += branchDirection * stepSize + perpendicular * offset;
                branchPoints.Add(currentPos);

                //随机提前结束（越远越容易结束）
                if (Main.rand.NextFloat() < 0.03f + progressFactor * 0.07f) break;
            }

            if (branchPoints.Count > 3) {
                //分叉宽度随机变化
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

        /// 开始停留
        protected virtual void StartLinger() {
            State = (float)LightningState.Lingering;
            Timer = 0;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 0;
            Hited = 1;

            //最终确定轨迹
            if (MainTrail != null && TrailPoints.Count > 2) {
                MainTrail.BasePositions = TrailPoints.ToArray();
                MainTrail.RandomThunder();
            }

            //触发劈击效果
            OnStrike();
            OnHit();
            Projectile.netUpdate = true;
        }

        /// 更新停留
        protected virtual void UpdateLinger() {
            Timer++;

            //持续可见，保持满亮度
            if (MainTrail != null) {
                MainTrail.CanDraw = true;
            }

            foreach (var branch in BranchTrails) {
                branch.CanDraw = true;
            }

            //能量波动
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

        /// 开始消失
        protected virtual void StartFade() {
            State = (float)LightningState.Fading;
            Timer = 0;
            Projectile.timeLeft = FadeTime + 10;
        }

        /// 更新淡出
        protected virtual void UpdateFade() {
            Timer++;

            //快速线性淡出
            FadeValue = Timer / FadeTime;

            //宽度也逐渐缩小
            ThunderWidth = BaseWidth * (1f - FadeValue * 0.6f);

            //透明度快速降低
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

            Projectile.netUpdate = true;
        }
        #endregion

        #region 碰撞处理
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
            //绘制主体（如果未命中）
            if (Hited == 0 && State == (float)LightningState.Striking) {
                DrawLightningCore(lightColor);
            }

            //绘制轨迹
            if (State > (float)LightningState.Initializing) {
                DrawTrails();
            }

            return false;
        }

        /// 绘制闪电核心
        protected virtual void DrawLightningCore(Color lightColor) {
            //子类可重写
        }

        /// 绘制轨迹
        protected virtual void DrawTrails() {
            if (MainTrail != null && ((LightningState)State != LightningState.Striking || Timer >= 3)) {
                MainTrail.DrawThunder(Main.instance.GraphicsDevice);
            }

            //绘制分叉
            foreach (var branch in BranchTrails) {
                branch?.DrawThunder(Main.instance.GraphicsDevice);
            }
        }
        #endregion

        #region 工具方法
        /// 平滑函数
        public static float Smoother(int timer, int maxTime) {
            if (maxTime <= 0) return 1f;
            float factor = Math.Clamp((float)timer / maxTime, 0f, 1f);
            return factor * factor;
        }
        #endregion
    }
}