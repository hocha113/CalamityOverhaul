// ============================================================================
//HeartcarverOrgan.fx 剜出的心脏本体
//世界空间 quad AlphaBlend：SDF 心形 + 闭不上的嘴
//uBeat=收缩包络(1=收缩瞬间) uMouth=嘴张开度(尖叫) uFade=出场/吸收渐隐
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;
float uBeat;
float uMouth;
float uFade;

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
    //归一化局部坐标：+y 向上
    float2 p = input.TexCoords * 2.0 - 1.0;
    p.y = -p.y;

    //心缩：收缩瞬间整体缩小并压扁，附高频颤抖
    float squeeze = 1.0 + 0.16 * uBeat;
    float2 hp = p * 1.30 * squeeze;
    hp.y *= 1.0 + 0.08 * uBeat;
    hp.y -= 0.08; //心形重心居中
    hp.x += 0.012 * uBeat * sin(uTime * 70.0 + uSeed * 20.0);

    //经典心形曲线 (x^2+y^2-1)^3 - x^2*y^3 <= 0
    float x2 = hp.x * hp.x;
    float y2 = hp.y * hp.y;
    float r2 = x2 + y2 - 1.0;
    float f = r2 * r2 * r2 - x2 * y2 * hp.y;

    //肉质轮廓抖动：噪声啃出不规则外缘
    float nEdge = tex2D(noiseSamp, hp * 0.45 + uSeed * 3.1).r;
    f += (nEdge - 0.5) * 0.08;

    float body = smoothstep(0.05, -0.07, f);
    if (body <= 0.003)
    {
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    //调色：黑 / 动脉暗红 / 心肌粉白
    float3 cNight = float3(0.05, 0.008, 0.02);
    float3 cDeep = float3(0.25, 0.015, 0.045);
    float3 cArterial = float3(0.62, 0.05, 0.09);
    float3 cMyocard = float3(1.0, 0.84, 0.86);

    //肌理：噪声大理石纹 + 纵向明暗
    float nMarble = tex2D(noiseSamp, hp * float2(0.6, 0.9) + float2(uSeed * 5.7, uTime * 0.02)).r;
    float grad = saturate(hp.y * 0.4 + 0.55);
    float3 col = lerp(cDeep, cArterial, saturate(0.30 + 0.45 * grad + 0.35 * uBeat));
    col = lerp(col, cMyocard * 0.55, smoothstep(0.58, 0.82, nMarble) * 0.45);

    //左上高光：心肌湿润反光
    float2 hv = hp - float2(-0.34, 0.42);
    float spec = exp(-dot(hv, hv) * 5.5);
    col += cMyocard * spec * (0.30 + 0.25 * uBeat);

    //边缘暗轮廓：黑色收边
    float contour = 1.0 - smoothstep(-0.02, -0.16, f);
    col = lerp(col, cNight, contour * 0.65);

    //==== 闭不上的嘴 ====
    float span = smoothstep(0.95, 0.45, abs(hp.x));
    float lipY = -0.04 - 0.10 * x2;
    float openness = (0.045 + uMouth * 0.20) * span + 1e-4;
    float dm = hp.y - lipY;
    float hole = span * (1.0 - smoothstep(openness * 0.55, openness, abs(dm)));

    //口腔深处：黑里透着搏动的暗红
    float depth = saturate(1.0 - abs(dm) / openness);
    float3 holeCol = lerp(cNight, cArterial * 0.55, uMouth * 0.6 * depth);

    //上唇细齿：苍白牙尖悬在裂口上缘
    float comb = abs(frac(hp.x * 6.5 + uSeed) - 0.5) * 2.0;
    float toothZone = smoothstep(-openness, -openness * 0.25, dm) * step(dm, 0.0);
    float teeth = smoothstep(0.55, 0.18, comb) * toothZone * span;

    //唇缘压暗
    float lipRim = span * (smoothstep(openness * 1.7, openness, abs(dm)) - hole);
    col = lerp(col, cNight, saturate(lipRim) * 0.5);

    col = lerp(col, holeCol, hole);
    col = lerp(col, cMyocard * 0.9, saturate(teeth * hole));

    //心缩瞬间全身充血提亮
    col *= 1.0 + 0.22 * uBeat;

    float alpha = body * uFade;
    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass HeartcarverOrganPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
