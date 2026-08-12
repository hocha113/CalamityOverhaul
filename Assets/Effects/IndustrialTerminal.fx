// ============================================================================
// IndustrialTerminal.fx  工业域机器界面共享机壳底(勘探终端/发电机系列)
// 材质："矿场野外仪器的切角钢壳"——拉丝暗钢 + 氧化锈斑 + 磨亮切角棱线,
// 不是发光面板,也不是纯色填充;亮度只出现在顶缘受光与棱线磨损处
// uMode: 0 主机壳(暗钢) 1 铭牌/小件(黄铜)
// uHeat: 0..1 机壳受热,底缘向上沁暖 + 极轻热浪(热力炉体用,常温机器传 0)
// 预乘输出 + AlphaBlend;切角在 shader 内切,和 C# 的 Chamfer 常量对齐
// 直线算术无动态分支;噪声全部 hash 手拼,不吃采样器
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;  //面板像素尺寸
float uChamfer;      //切角边长 px
float uMode;         //0 主机壳 1 黄铜铭牌
float uHeat;         //0..1 机壳受热度

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

//两档足够的平滑值噪声,输入是像素坐标缩放
float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 px = uv * uResolution;
    float2 toEdge = min(px, uResolution - px);

    //切角遮罩:四角按 |x|+|y| 斜切,和 C# 的切角描边同一条斜边
    float corner = toEdge.x + toEdge.y;
    float inside = step(uChamfer, corner) * step(0.0, min(toEdge.x, toEdge.y));
    //到外轮廓(含斜边)的近似距离
    float edgeDist = min(min(toEdge.x, toEdge.y), (corner - uChamfer) * 0.7071);

    float brass = saturate(uMode);

    //———— 基色:暗暖钢 / 黄铜 ————
    float3 steel = float3(0.058, 0.049, 0.043);
    float3 brassBase = float3(0.120, 0.088, 0.044);
    float3 base = lerp(steel, brassBase, brass);

    //———— 拉丝:逐行细纹,分段断续(横向的加工痕,不是网格) ————
    float row = floor(px.y);
    float seg = floor(px.x / 42.0);
    float streak = hash21(float2(row * 0.731, seg * 1.173)) * 0.55
                 + hash11(row * 0.317) * 0.45;
    float brush = (streak - 0.5) * (0.14 - brass * 0.05);

    //———— 氧化噪斑:低频云 + 中频碎斑,沉向锈色 ————
    float mottle = vnoise(px * 0.017) * 0.62 + vnoise(px * 0.071) * 0.38;
    float rustAmt = smoothstep(0.58, 0.95, mottle) * (1.0 - brass * 0.65);
    float3 rustTint = float3(0.115, 0.055, 0.028);

    //———— 顶部受光 + vignette + 极低幅呼吸(低调稳定) ————
    float topLight = 1.0 - smoothstep(0.0, 68.0, px.y);
    float2 cuv = uv - 0.5;
    float vig = 1.0 - dot(cuv, cuv) * 0.60;
    float breath = 1.0 + 0.02 * sin(uTime * 0.5);

    float3 col = base * (1.0 + brush) * vig * breath;
    col += base * topLight * 0.55;
    col = lerp(col, rustTint * (0.35 + 0.65 * mottle), rustAmt * 0.38);

    //———— 机壳受热:底缘向上沁暖,叠一丝慢热浪的亮度摆动 ————
    float heatBase = pow(saturate(uv.y), 2.2) * uHeat;
    float heatWaver = sin(px.x * 0.11 + uTime * 2.1) * sin(uTime * 1.3 + uv.y * 9.0);
    col += float3(0.215, 0.088, 0.030) * heatBase * (0.62 + 0.12 * heatWaver);

    //———— 逐像素微粒噪,压住渐变条带 ————
    col *= 1.0 + (hash21(px) - 0.5) * 0.030;

    //———— 切角棱线:外缘一线磨亮(黄铜暖),内侧一线沉影(机加工读法) ————
    float rimHi = 1.0 - smoothstep(0.0, 1.8, edgeDist);
    float rimLo = smoothstep(1.8, 3.4, edgeDist) * (1.0 - smoothstep(3.4, 6.4, edgeDist));
    col += float3(0.235, 0.150, 0.068) * rimHi * (0.55 + brass * 0.25);
    col *= 1.0 - rimLo * 0.34;

    //铭牌整体更亮一点,读作抛过光的黄铜
    col *= 1.0 + brass * 0.35;

    float a = lerp(0.965, 0.985, brass);
    return float4(col, a) * inside * uAlpha * input.Color;
}

technique Technique1
{
    pass IndustrialTerminalPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
