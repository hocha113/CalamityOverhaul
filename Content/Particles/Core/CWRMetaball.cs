using CalamityMod.Effects;
using CalamityMod.Graphics.Metaballs;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Common.Effects;
using System.Linq;

namespace CalamityOverhaul.Content.Particles.Core
{
    internal abstract class CWRMetaball : ModType
    {
        internal List<ManagedRenderTarget> LayerTargets = new();

        /// <summary>
        /// 必需的实用程序，用于确定此元球是否有任何内容可绘制。<br></br>
        /// 这是为了提高效率而存在，确保在不需要时尽可能少执行操作
        /// </summary>
        public abstract bool DrawActiveness {
            get;
        }

        /// <summary>
        /// 所有纹理的集合，用于在元球内容上绘制
        /// </summary>
        public abstract IEnumerable<Texture2D> Layers {
            get;
        }

        /// <summary>
        /// 元球应该绘制的绘制层
        /// </summary>
        public abstract MetaballDrawLayer DrawContext {
            get;
        }

        /// <summary>
        /// 元球在空气和粒子内容之间绘制的边缘颜色
        /// </summary>
        public abstract Color EdgeColor {
            get;
        }

        /// <summary>
        /// 是否应将来自<see cref="Layers"/>的图层覆盖内容固定到屏幕<br></br>
        /// 当为true时，纹理将静态绘制到屏幕上，不考虑世界位置
        /// </summary>
        public virtual bool FixedToScreen => false;

        /// <summary>
        /// 可选的、可重写的方法，用于根据需要清除粒子实例。这在诸如世界卸载等上下文中会被自动使用
        /// </summary>
        public virtual void ClearInstances() { }

        /// <summary>
        /// 可选的、可重写的方法，用于更新元球集。这对于实现随时间更新效果或保持所有特殊数据粒子类型更新非常有用
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// 可选的、可重写的方法，用于在绘制时使图层偏移，以允许图层特定的动画效果。默认为<see cref="Vector2.Zero"/>，即无动画
        /// </summary>
        public virtual Vector2 GetLayerDrawOffset(int layerIndex) => Vector2.Zero;

        /// <summary>
        /// 可选的、可重写的方法，允许在绘制单个原始元球实例<i>(而不是最终结果)</i>之前准备<see cref="SpriteBatch"/><br></br>
        /// 例如，可以使元球粒子以<see cref="BlendState.Additive"/>模式绘制
        /// </summary>
        /// <param name="spriteBatch">用于<see cref="Main.spriteBatch"/>的缩写参数。</param>
        public virtual void PrepareSpriteBatch(SpriteBatch spriteBatch) { }

        /// <summary>
        /// 可选的、可重写的方法，用于为渲染目标进行准备。默认为使用典型的纹理覆盖行为。
        /// </summary>
        /// <param name="layerIndex">应该准备的图层索引。</param>
        public virtual void PrepareShaderForTarget(int layerIndex) {
            // 将图形设备和着色器存储在易于使用的本地变量中
            var metaballShader = EffectsRegistry.EdgeDyeingShader;
            var gd = Main.instance.GraphicsDevice;

            // 获取图层纹理。这是将覆盖屏幕上灰度内容的纹理
            Texture2D layerTexture = Layers.ElementAt(layerIndex);

            // 计算图层滚动偏移量。这用于确保给定元球的纹理内容具有视差效果，而不是无论世界位置如何都保持在屏幕上静态
            // 这可以选择性地由元球进行切换关闭
            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            Vector2 layerScrollOffset = Main.screenPosition / screenSize + GetLayerDrawOffset(layerIndex);
            if (FixedToScreen)
                layerScrollOffset = Vector2.Zero;

            // 提供着色器参数值
            metaballShader.Parameters["layerSize"]?.SetValue(layerTexture.Size());
            metaballShader.Parameters["screenSize"]?.SetValue(screenSize);
            metaballShader.Parameters["layerOffset"]?.SetValue(layerScrollOffset);
            metaballShader.Parameters["edgeColor"]?.SetValue(EdgeColor.ToVector4());
            metaballShader.Parameters["singleFrameScreenOffset"]?.SetValue((Main.screenLastPosition - Main.screenPosition) / screenSize);

            // 将元球的层纹理提供给图形设备，以便着色器可以读取它
            gd.Textures[1] = layerTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            // 应用元球着色器
            metaballShader.CurrentTechnique.Passes[0].Apply();
        }

        /// <summary>
        /// 必需的可重写方法，旨在绘制所有元球实例
        /// </summary>
        public abstract void DrawInstances();

        /// <summary>
        /// 注册这个类型的封闭包以用于自定义管理
        /// </summary>
        protected sealed override void Register() {
            // 注册这个元球模型TML的内置ModType处理程序
            ModTypeLookup<CWRMetaball>.Register(this);

            // 将此元球实例存储在个性化管理器中，以便在呈现时对其进行跟踪
            if (!CWRMetaballManager.metaballs.Contains(this))
                CWRMetaballManager.metaballs.Add(this);

            // 禁止在服务器上创建渲染目标
            if (Main.netMode == NetmodeID.Server)
                return;

            // 生成渲染目标
            Main.QueueMainThreadAction(() => {
                // 加载渲染目标，这是最终的一步
                int layerCount = Layers.Count();
                for (int i = 0; i < layerCount; i++)
                    LayerTargets.Add(new(true, ManagedRenderTarget.CreateScreenSizedTarget));
            });
        }

        /// <summary>
        /// 释放<see cref="LayerTargets"/>占用的所有不受管制的GPU资源。这在模组卸载时会自动调用<br></br>
        /// <i>如果手动调用此方法，必须在这之后重新创建图层目标</i>
        /// </summary>
        public void Dispose() {
            for (int i = 0; i < LayerTargets.Count; i++)
                LayerTargets[i]?.Dispose();
        }
    }
}
