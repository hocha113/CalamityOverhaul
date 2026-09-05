using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>夺身演出相位：前兆 → 显形 → 处决 → 余韵。</summary>
    internal enum WraithSeizePhase : byte
    {
        None,
        /// <summary>前兆：封印失控的警告拍，玩家行动开始被钉住</summary>
        Omen,
        /// <summary>显形：厉鬼现出真身，玩家完全被制</summary>
        Manifest,
        /// <summary>处决后的余韵：玩家已死，尾迹继续存活</summary>
        Linger,
    }

    /// <summary>
    /// 单只厉鬼的夺身死亡演出。每次夺身由 <see cref="WraithRevivalDeathPlayer"/>
    /// 通过 <see cref="Core.WraithDefinition.CreateDeathPerformance"/> 实例化，演出结束即弃。<br/>
    /// 全程以 180 帧（3 秒）为基准；余韵之外的长尾交给粒子系统，不占用锁定时长。
    /// </summary>
    internal abstract class WraithDeathPerformance
    {
        /// <summary>宿主状态机，实例化后由框架回填</summary>
        internal WraithRevivalDeathPlayer Host { get; set; }

        protected Player Player => Host.Player;
        protected int Timer => Host.SeizeTimer;
        protected byte Seed => Host.SeizeSeed;
        protected WraithSeizePhase Phase => Host.Phase;
        /// <summary>当前相位内推进度 0..1</summary>
        protected float PhaseProgress => Host.PhaseProgress;
        /// <summary>处决帧锚点：玩家死亡时的位置，余韵围绕它展开</summary>
        protected Vector2 DeathAnchor => Host.DeathAnchor;
        /// <summary>开演时探得的地面锚。破土、沉地、落地一律用它，禁止「玩家中心 + 固定偏移」当地面</summary>
        protected SeizureGround Ground => Host.Ground;

        //---- 帧表（3 秒规格：前兆 0.7s / 显形 1.4s / 余韵 1s）----
        //注意这是逻辑帧；HoldFramesAt 声明的顿帧会额外占用真实时间，两者之和仍须压在 200 帧内
        public virtual int OmenEndFrame => 42;
        public virtual int ExecuteFrame => 126;
        public virtual int TotalFrames => 186;

        /// <summary>
        /// 声明本演出的具名拍。<b>只在此处 Add</b>，表在首次推进时封口。<br/>
        /// 一切「到某帧做某事」都应走这里，不要在 <see cref="Update"/> 里写
        /// <c>Timer == X</c> 或 <c>PhaseProgress in [a,b)</c>：前者会被联机快照修正跳过，
        /// 后者按帧落点可能触发零次或两次。
        /// </summary>
        public virtual void BuildBeats(SeizureBeatTable beats) { }

        /// <summary>
        /// 该逻辑帧要不要卡住若干帧不推进（处决顿挫）。<br/>
        /// 必须是纯静态映射，权威端与各客户端各算各的但结果一致，不额外发包。
        /// </summary>
        public virtual int HoldFramesAt(int frame) => 0;

        /// <summary>演出开始（各端本地各调一次）</summary>
        public virtual void OnBegin() { }

        /// <summary>处决帧的本地表现（骨血、爆点、断体）；权威击杀由框架另行执行</summary>
        public virtual void OnExecute() { }

        /// <summary>每帧推进（非 dedServ）</summary>
        public abstract void Update();

        /// <summary>世界空间绘制（RenderHandle 批次内）</summary>
        public abstract void Draw(SpriteBatch sb);

        /// <summary>
        /// 裸设备图元绘制，在精灵批次开始前调用；
        /// 需要 shader 三角带（斩痕、血臂类）的演出重写此方法并自管设备状态。
        /// </summary>
        public virtual void DrawPrimitive(GraphicsDevice device) { }

        /// <summary>本帧是否隐藏玩家本体（被帘罩住、被吞入雨中一类）。</summary>
        public virtual bool HidesPlayer => false;

        //---- 运镜（仅死者本机被读取）----
        public virtual Vector2 CameraFocus
            => Phase == WraithSeizePhase.Linger ? DeathAnchor : Player?.Center ?? DeathAnchor;
        public virtual float CameraZoom => Phase switch {
            WraithSeizePhase.Omen => 1.12f,
            WraithSeizePhase.Manifest => 1.3f,
            WraithSeizePhase.Linger => 1.18f,
            _ => 1f,
        };
        public virtual float CameraFocusLerp => 0.12f;
        public virtual float ShakeIntensity => Phase switch {
            WraithSeizePhase.Omen => 2.5f * PhaseProgress,
            WraithSeizePhase.Manifest => 3.5f,
            _ => 0f,
        };

        /// <summary>
        /// 夺身期间的玩家运动控制（owner 端位置权威，其余端无害）。<br/>
        /// 默认：急减速后钉死原地；需要拖拽/上提玩家的演出重写此方法。
        /// </summary>
        public virtual void UpdatePlayerMotion() {
            if (Player == null || Player.dead) {
                return;
            }
            SeizurePuppet.Brake(Player, 0.5f, freeze: Timer > 6);
        }

        /// <summary>
        /// 受害者体态，在 <c>PostUpdate</c> 阶段应用（原版 <c>PlayerFrame</c> 已跑完）。<br/>
        /// 只有这里写 <c>bodyFrame</c> 才不会被覆盖；写 <see cref="SeizurePuppet"/> 以外的
        /// 玩家绘制字段前先确认它不在 <c>PlayerFrame</c> 的重算范围内。
        /// </summary>
        public virtual void ApplyPose() { }

        /// <summary>
        /// 处决之后的残体驱动。原版把头、身、腿三段抛散并逐帧加重力，
        /// 三段速度公开可写，「尸身怎么散」因此归各鬼自己管，不必另画假尸体。
        /// </summary>
        public virtual void UpdateDeathBody() { }
    }
}
