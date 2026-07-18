// ============================================================================
//SHPCModErgoEcho.fx 人体工学枪托·人枪合一残影
//采样 s0 武器贴图 + s1 噪声；多重相位残影：轴向相位分离 + 流线扫描 + 噪声消融
//全程笛卡尔 UV（无 atan2/theta/极坐标），无接缝风险
// ============================================================================

//贴图 u 轴恒为枪尾→枪口（垂直翻转只镜像 y），故流向无需随朝向切换
float uTime;        //全局时间
float uBeat;        //0~1 韵律脉动
float uPhase;       //0~1 残影相位：越大越陈旧，分离与消融越强
float uOpacity;     //整体不透明度
float3 uCoreColor;  //核心香槟白
float3 uEdgeColor;  //边缘暗金

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

//SpriteBatch 自动将武器贴图绑定到 register(s0)
sampler baseSamp : register(s0);

struct PSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;

    //A. 相位分离：沿枪身轴向的三重采样错位，残影越旧错位越大，节拍推动
    float split = uPhase * (0.012 + 0.010 * uBeat);
    float2 offset = float2(split, 0.0);
    float4 texC = tex2D(baseSamp, uv);
    float aFwd = tex2D(baseSamp, uv + offset).a;
    float aBack = tex2D(baseSamp, uv - offset).a;

    //B. 流线扫描：沿枪身向枪口推进的亮带（纯 x 轴函数）
    float band = 0.5 + 0.5 * sin(uv.x * 22.0 - uTime * 9.0 + uPhase * 5.0);
    band = pow(band, 3.0);

    //C. 噪声消融：残影越旧被气流撕碎得越厉害
    float2 nUV = uv * float2(1.6, 3.2) + float2(-uTime * 0.35, uPhase * 0.77);
    float n = tex2D(noiseSamp, nUV).r;
    float cut = uPhase * 0.42;
    float keep = smoothstep(cut, cut + 0.30, n * 0.55 + texC.a * 0.55);

    //D. 主体颜色：亮度映射边缘暗金→核心香槟白，叠加流线与节拍呼吸
    float lum = dot(texC.rgb, float3(0.299, 0.587, 0.114));
    float3 col = lerp(uEdgeColor, uCoreColor, saturate(lum * 1.25))
        * texC.a * (0.42 + 0.34 * band + 0.24 * uBeat);

    //E. 相位彩边：残影轮廓处的冷暖裂隙，构成"重影"辨识度
    col += float3(1.0, 0.62, 0.30) * saturate(aFwd - texC.a) * 0.85;
    col += float3(0.35, 0.65, 1.0) * saturate(aBack - texC.a) * 0.70;

    float mask = max(texC.a, max(aFwd, aBack));
    float alpha = saturate(mask * keep) * uOpacity;
    return float4(col, alpha) * input.Color;
}

technique Technique1
{
    pass ErgoEchoPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
