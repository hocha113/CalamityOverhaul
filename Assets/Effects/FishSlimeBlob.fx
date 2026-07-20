// ============================================================================
//FishSlimeBlob.fx 胶着行迹凝胶球本体：半透明果冻 blob
//笛卡尔噪声扰动的圆形 SDF + 暗蓝厚缘 + 半透明体色 + 体内悬浮微泡 +
//偏移双点内部高光（透亮感 = AlphaBlend 高光点，非加色发光）+ 爆前充能饱和脉动；
//quad 局部 uv 0..1，C# 端负责旋转与压扁拉伸（形变在几何层，表面波在本 shader）
//极角审计：无 atan2/theta/phi 消费，全部笛卡尔噪声与线性距离场，无缝隙风险。
//预乘 alpha 输出，配 BlendState.AlphaBlend（凝胶有体，能压住背景）
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;          //秒
float uSeed;          //每球随机相位
float uWobble;        //0..1 表面波振幅，震荡时边缘扰动加深
float uCharge;        //0..1 爆前充能，饱和与脉动上升
float uAlpha;         //整体不透明度，出生淡入
float2 uHighlightDir; //内部高光偏移方向（形变局部系单位向量）

float3 uColDeep;      //深蓝厚缘
float3 uColBody;      //主体蓝
float3 uColBright;    //亮蓝内层

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

float noiseTex(float2 uv)
{
    return tex2D(noiseSamp, uv).r;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    //局部坐标：原点球心，quad 半宽映射 1.1
    float2 p = (input.TexCoords - 0.5) * 2.2;
    float r = length(p);

    //====== 表面波：笛卡尔噪声扰动半径 ======
    float nEdge = noiseTex(p * 0.33 + uSeed * 5.1 + float2(uTime * 0.055, -uTime * 0.04));
    float edgeAmp = 0.05 + uWobble * 0.13;
    float radius = 0.88 + (nEdge - 0.5) * 2.0 * edgeAmp;
    float d = r - radius; //<0 体内

    float bodyMask = 1.0 - smoothstep(-0.05, 0.03, d);
    float thick = saturate(-d / radius); //0 缘..1 心 厚度近似

    //====== 体色：暗蓝厚缘 → 体蓝 → 心部微亮 ======
    float3 col = lerp(uColDeep, uColBody, smoothstep(0.0, 0.42, thick));
    col = lerp(col, uColBright, smoothstep(0.55, 1.0, thick) * 0.35);

    //====== 体内悬浮微泡：缓慢漂移的噪声亮斑 ======
    float nBub = noiseTex(p * 1.35 + uSeed * 9.7 + float2(-uTime * 0.03, uTime * 0.05));
    float bubble = smoothstep(0.74, 0.86, nBub) * smoothstep(0.12, 0.4, thick);
    col = lerp(col, uColBright, bubble * 0.5);

    //====== 内部高光：偏移大软斑 + 小锐点，凝胶的透亮感 ======
    float2 hOff = uHighlightDir * radius;
    float dh1 = length(p - hOff * 0.38);
    float dh2 = length(p - hOff * 0.52);
    float pulse = 1.0 + uCharge * 0.6 * sin(uTime * 21.0 + uSeed * 13.0);
    float hl1 = (1.0 - smoothstep(0.05, 0.34, dh1)) * 0.55 * pulse;
    float hl2 = (1.0 - smoothstep(0.0, 0.11, dh2)) * 0.9 * pulse;
    col = lerp(col, uColBright, saturate(hl1));
    col = lerp(col, float3(0.88, 0.97, 1.0), saturate(hl2)); //锐点近白但极小

    //====== 充能：提饱和不提明度 ======
    col = lerp(col, uColBody * 1.15, uCharge * 0.35 * (1.0 - saturate(hl2)));

    //====== 半透明合成：缘略实压住背景，心部略透 ======
    float a = bodyMask * lerp(0.88, 0.62, smoothstep(0.2, 0.9, thick));
    a += (hl1 * 0.25 + hl2 * 0.35) * bodyMask;
    a = saturate(a) * uAlpha;
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
