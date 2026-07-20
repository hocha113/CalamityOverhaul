// ============================================================================
//FishHoneyCore.fx 蜜诏核心：悬浮的粘稠蜜团
//SDF 圆液团：整数谐波轮廓抖动（粘稠 wobble）+ 底部重力垂坠 + 薄边透光色阶
//（边薄=暖金、心厚=深琥珀）+ 笛卡尔噪声内部气泡 + 琥珀高光带缓扫 + 顶部定点光泽
//+ 噪声侵蚀现形/溶解。蜜是半透明液体不是光源：预乘 alpha 输出配 BlendState.AlphaBlend，
//高光只是小面积暖白点，无大面积加色
//quad 局部 uv 0..1，uSizePx 像素尺寸
//极角审计：theta 仅进 sin(k*theta)（k=1,2,3,5 全整数）无缝；噪声输入全笛卡尔
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;     //秒
float uSeed;     //每核心随机相位
float uReveal;   //0..1 现形进度
float uDissolve; //0..1 溶解进度
float uWobble;   //轮廓抖动幅度增益（入场/溶解/产蜂脉冲叠加）
float uSquash;   //产蜂挤压脉冲 0..1
float2 uSizePx;  //quad 像素尺寸

float3 uColDeep;  //深琥珀（厚蜜）
float3 uColBody;  //蜜橙（主体）
float3 uColGold;  //暖金（薄蜜边）
float3 uColGlint; //暖白高光点

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

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 p = (uv - 0.5) * uSizePx;
    //产蜂挤压：横胀纵压的一拍
    p.x *= 1.0 - 0.10 * uSquash;
    p.y *= 1.0 + 0.14 * uSquash;

    float t = uTime;
    float R0 = uSizePx.x * 0.30;
    float theta = atan2(p.y, p.x);

    //粘稠轮廓：慢速整数谐波抖动 + 底部垂坠（y 向下，sin(theta) 底部为正）
    float wob = 0.045 + uWobble;
    float rMod = 1.0
        + wob * sin(3.0 * theta + t * 2.1 + uSeed)
        + wob * 0.6 * sin(5.0 * theta - t * 1.4 + uSeed * 2.0)
        + 0.05 * sin(2.0 * theta + t * 0.8)
        + 0.10 * (0.5 + 0.5 * sin(theta));
    float R = R0 * rMod;
    float r = length(p);
    float d = r - R;

    //现形：噪声高处先成形，辅以自心向外偏置
    float nA = tex2D(noiseSamp, uv * 1.9 + uSeed * 5.3 + float2(t * 0.02, t * 0.013)).r;
    float revealField = nA * 0.55 + (1.0 - saturate(r / R0)) * 0.45;
    float appear = smoothstep(1.0 - uReveal * 1.15 - 0.10, 1.0 - uReveal * 1.15 + 0.04, revealField);

    //溶解：噪声阈值侵蚀，前沿留一圈暖金薄蜜
    float nB = tex2D(noiseSamp, uv * 3.3 + uSeed * 9.1).r;
    float front = uDissolve * 1.14;
    float keep = smoothstep(front - 0.08, front + 0.02, nB);
    float thinEdge = smoothstep(front - 0.14, front - 0.02, nB) * (1.0 - smoothstep(front + 0.02, front + 0.10, nB));

    //体 alpha：边缘羽化
    float body = 1.0 - smoothstep(-1.5, 1.5, d);

    //厚度近似：0 边 → 1 心
    float thick = saturate(-d / R);

    //薄边透光色阶：暖金薄边 → 蜜橙 → 深琥珀厚心
    float3 col = lerp(uColGold, uColBody, smoothstep(0.0, 0.42, thick));
    col = lerp(col, uColDeep, smoothstep(0.35, 1.0, thick));

    //内部气泡：两尺度笛卡尔噪声缓慢漂移，亮泡提金、暗斑压深
    float bub1 = tex2D(noiseSamp, p / uSizePx * 2.6 + float2(t * 0.015, -t * 0.028) + uSeed).r;
    float bub2 = tex2D(noiseSamp, p / uSizePx * 5.1 + float2(-t * 0.02, t * 0.011) + uSeed * 3.0).r;
    float cells = smoothstep(0.62, 0.78, bub1) * 0.5 + smoothstep(0.70, 0.86, bub2) * 0.5;
    col = lerp(col, uColGold, cells * 0.35 * thick);
    col *= 1.0 - smoothstep(0.55, 0.35, bub1) * 0.12 * thick;

    //琥珀高光带缓扫：固定斜轴上的窄带随时间平移（笛卡尔投影，无极角）
    float axis = dot(p, float2(0.821, -0.571)) / R0;
    float sweepPos = frac(t * 0.14 + uSeed * 0.7) * 2.6 - 1.3;
    float band = exp(-pow((axis - sweepPos) * 4.2, 2.0));
    col += uColGlint * band * 0.28 * thick;

    //顶部定点光泽：光来自上方的小面积高光
    float2 hl = p - float2(-R0 * 0.30, -R0 * 0.38);
    float spot = exp(-dot(hl, hl) / (R0 * R0 * 0.035));
    col += uColGlint * spot * 0.55;

    //溶解前沿薄蜜提亮
    col = lerp(col, uColGold, thinEdge * 0.5);

    //蜜体半透明：厚处更实（液体非光源，压着透明度走）
    float a = body * appear * keep * (0.62 + 0.30 * thick);
    return float4(col * a, a) * input.Color;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
