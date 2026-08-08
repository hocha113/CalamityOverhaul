// ============================================================================
//LonginusATField.fx 朗基努斯AT力场八边形
//世界锚定 quad 图元 Additive，琥珀橙同心八边形涟漪立场
//p=(uv-0.5)*2 归一 SDF 空间，八边形 SDF 纯代数折叠，无 atan2
//uSpread 0=未展开 → 1=全展开；uShatter 0=完好 → 1=碎裂消散
//uPhase 层间错相；顶点色调制整体
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSpread;
float uShatter;
float uPhase;

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

//正八边形 SDF，r=中心到边距；abs/min/dot 折叠，内负外正
float sdOctagon(float2 p, float r)
{
    const float3 k = float3(-0.9238795325, 0.3826834323, 0.4142135623);
    p = abs(p);
    p -= 2.0 * min(dot(float2(k.x, k.y), p), 0.0) * float2(k.x, k.y);
    p -= 2.0 * min(dot(float2(-k.x, k.y), p), 0.0) * float2(-k.x, k.y);
    p -= float2(clamp(p.x, -k.z * r, k.z * r), r);
    return length(p) * sign(p.y);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;

    //八分面元 id：符号位组合替代极角分扇
    float sx = step(0.0, p.x) * 2.0 - 1.0;
    float sy = step(0.0, p.y) * 2.0 - 1.0;
    float sd = step(abs(p.y), abs(p.x)); //1=横向主导扇区
    float fid = step(0.0, p.x) + step(0.0, p.y) * 2.0 + sd * 4.0;
    float fh = frac(fid * 0.618 + uPhase * 0.371);

    //面元扇心方向，tan22.5=0.4142
    float2 fdir = normalize(float2(lerp(0.4142, 1.0, sd) * sx, lerp(1.0, 0.4142, sd) * sy));

    //碎裂滑移：各面元沿扇心加速外滑，错相
    float slide = uShatter * uShatter * (0.40 + fh * 0.30);
    float2 ps = p - fdir * slide;

    float r = 0.90;
    float d = sdOctagon(ps, r);
    float dN = saturate(1.0 + d / r); //0=中心 1=边缘

    //刚体旋转坐标采噪声，连续安全
    float cs = cos(uTime * 0.21);
    float sn = sin(uTime * 0.21);
    float2 rp = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
    float n = tex2D(noiseSamp, rp * 0.55 + float2(uTime * 0.03, uPhase * 0.7)).r;

    //展开波前：中心向外点亮，前沿白热
    float front = uSpread * 1.08;
    float dW = dN + (n - 0.5) * 0.05;
    float vis = smoothstep(front + 0.02, front - 0.30, dW);
    float frontGlow = exp2(-abs(dW - front) * 13.0) * saturate(1.25 - uSpread);

    //同心八边形主环 + 高频干涉细纹，皆基于距离场故连续
    float major = pow(0.5 + 0.5 * sin(dN * 21.0 - uTime * 3.2 - uPhase * 6.2832), 9.0);
    float fring = 0.5 + 0.5 * sin(dN * 84.0 - uTime * 6.0);

    //边缘菲涅尔亮线与体填充
    float rim = exp2(-abs(d) * 24.0);
    float body = smoothstep(0.015, -0.06, d);

    //裂纹：扇界线距离场，噪声毛口，碎裂瞬间白热后冷却
    float dLines = min(min(abs(p.x), abs(p.y)), abs(abs(p.x) - abs(p.y)) * 0.7071);
    float crack = smoothstep(0.030 + n * 0.018, 0.0, dLines) * body;
    float crackEnv = saturate(uShatter * 8.0) * saturate(1.15 - uShatter * 1.15);

    //面元错相消散
    float facetFade = saturate(1.0 - (uShatter * 1.35 - fh * 0.28));

    float3 cBody = float3(1.00, 0.58, 0.16);
    float3 cRing = float3(1.05, 0.66, 0.22);
    float3 cHot = float3(1.35, 1.08, 0.72);

    float aBody = body * (0.13 + n * 0.05);
    float aRing = major * body * 0.85;
    float aFring = fring * body * 0.05;

    float3 color = cBody * (aBody + aFring) + cRing * (aRing + rim * 0.85);
    color += cHot * (frontGlow * 0.95 + crack * crackEnv);

    float alpha = saturate(aBody + aRing + aFring + rim * 0.6 + frontGlow * 0.55 + crack * crackEnv * 0.85);
    float gate = vis * facetFade * saturate(uSpread * 8.0);
    alpha *= gate;
    color *= gate;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass LonginusATFieldPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
