// ============================================================================
//GatlinTracer.fx 加特林曳光弹拖尾着色器
//Trail条带渲染，专为高速穿甲弹设计的白炽核心+琥珀电浆鞘+飞散微丝三层结构
//UV.x 0=弹头 1=弹尾     UV.y 0=上边 1=下边
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;       //整体透明度
float coreBoost;       //弹头白炽核心强度倍增，默认1，命中前瞬可短暂提升
texture uNoiseTex;     //等离子滚动噪声，单通道灰度即可

sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

struct VSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

struct PSInput
{
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position  = mul(v.Position, transformMatrix);
    o.Color     = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    //along 0=弹头 1=弹尾，headT 1在头端0在尾端
    float along   = input.TexCoords.x;
    float headT   = 1.0 - along;
    float cross_  = input.TexCoords.y;
    float d       = abs(cross_ - 0.5) * 2.0;    //0=中心 1=边缘

    //三层截面权重：核心极窄、电浆鞘中宽、外焰宽
    float core    = pow(saturate(1.0 - d), 22.0);
    float plasma  = pow(saturate(1.0 - d), 6.0);
    float halo    = pow(saturate(1.0 - d), 2.0);

    //沿弹道的能量衰减：头端密集、尾端快速收敛避免像粗管子
    float alongProfile = headT * headT;          //平方曲线让尾端更细
    float coreProfile  = pow(headT, 0.35);       //核心保持到较远
    float haloProfile  = pow(headT, 1.8);

    //滚动等离子噪声两层，产生可辨识的电浆流线与微闪
    float2 uv1 = float2(along * 2.5 - uTime * 1.3, cross_ * 1.8 + uTime * 0.17);
    float2 uv2 = float2(along * 7.0 - uTime * 3.1, cross_ * 2.7 - uTime * 0.42);
    float n1 = tex2D(noiseSamp, uv1).r;
    float n2 = tex2D(noiseSamp, uv2).r;

    //电浆鞘调制：让中间层产生带状不均，破除平滑管感
    float sheathMod = lerp(0.55, 1.45, n1);
    float filament  = saturate(pow(n2, 3.5) * 2.4 - 0.3);    //高频微丝
    //电浆鞘截面：越靠近边缘越薄，中部最密
    float sheathProfile = plasma * sheathMod * alongProfile;
    //微丝只在边缘带出现，强调拖尾的"飞散"质感
    float sheathEdge    = smoothstep(0.15, 0.85, d) * halo;

    //颜色配方：线性空间下大于1的值在Additive混合后会被屏幕饱和成纯白，形成HDR灼烧质感
    float3 hotWhite   = float3(1.35, 1.25, 1.05);
    float3 amber      = float3(1.10, 0.58, 0.18);
    float3 deepEmber  = float3(0.95, 0.22, 0.05);
    float3 charRed    = float3(0.55, 0.08, 0.02);

    float3 col = 0;
    //1. 白炽核心，头端极亮、尾端快速降温
    col += hotWhite * core * coreProfile * 4.5 * coreBoost;
    //2. 电浆鞘，带噪声调制形成流动感
    col += amber    * sheathProfile * 1.9;
    //3. 外焰环，从琥珀过渡到深红，模拟热气流散逸
    float3 haloCol = lerp(deepEmber, amber, haloProfile);
    col += haloCol * halo * haloProfile * 0.85;
    //4. 高频微丝：在边缘带上附加一些更亮更细的电浆线
    col += amber * filament * sheathEdge * 1.6;
    //5. 尾端炭化过渡：靠近尾部时抹上一点暗红防止突然切断
    col += charRed * halo * (along * along) * 0.35;

    //整体强度与端头调制
    col *= fadeAlpha;

    //Additive混合下alpha不参与，但我们依旧给出一个非零值让引擎保留像素
    float a = saturate((core * 1.8 + sheathProfile * 0.9 + halo * 0.4) * fadeAlpha);
    return float4(col, a);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader  = compile ps_3_0 PixelShaderFunction();
    }
};
