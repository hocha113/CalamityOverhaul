// ============================================================================
//LonginusCross.fx 朗基努斯十字光柱
//单 quad SDF 拉丁十字：竖柱贯穿全长，横臂偏上；Additive
//uv.x 0=上端 1=下端 沿主轴；uv.y 0~1 横截
//uGrow 0~1 生长包络(竖柱先拔起横臂后展开)；uDissolve 0~1 噪声侵蚀消散
//uFill 0~1 自下而上点亮(计量条用，爆炸恒为1)；uAspect=半长/半宽
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uGrow;
float uDissolve;
float uFill;
float uAspect;
float uWidth;
float uHot;

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
    //横截单位坐标：X 沿主轴(上负下正)，Y 横向
    float X = (uv.x - 0.5) * 2.0 * uAspect;
    float Y = (uv.y - 0.5) * 2.0;
    float tAx = abs(X) / uAspect; //0=中心 1=端点

    float n = tex2D(noiseSamp, float2(X * 0.10 + uTime * 0.05, Y * 0.8 + 0.37)).r;

    //竖柱：向两端幂次收细成针尖
    float wPillar = uWidth * pow(saturate(1.0 - tAx), 0.72);
    float dPillar = abs(Y) - wPillar;

    //横臂：偏上 28%，臂展随生长包络铺开，向臂尖收细
    float xArm = -uAspect * 0.28;
    float armEnv = saturate(uGrow * 2.6 - 1.35);
    float armSpan = armEnv * 0.94;
    float tArm = abs(Y) / max(armSpan, 0.001);
    float wArm = uWidth * 1.9 * pow(saturate(1.0 - tArm), 0.80) * armEnv;
    float dArm = abs(X - xArm) / uAspect * 1.35 - wArm;

    float d = min(dPillar, dArm);

    //生长包络：竖柱自中心向两端拔起
    float growFront = saturate(uGrow * 1.25);
    float grow = smoothstep(growFront + 0.03, growFront - 0.10, tAx);

    //白热核心与外鞘双层
    float core = smoothstep(0.0, -uWidth * 0.55, d);
    float sheath = smoothstep(uWidth * 1.6, -uWidth * 0.2, d);

    //沿柱流光：噪声脊线上行
    float flow = tex2D(noiseSamp, float2(X * 0.22 - uTime * 0.85, Y * 1.4)).r;
    float streak = smoothstep(0.55, 0.85, flow) * sheath * 0.35;

    //交叉点与根部过曝辉团
    float bloomArm = exp2(-(abs(X - xArm) * 0.9 + abs(Y) * 1.4) * 3.2) * armEnv;
    float bloomRoot = exp2(-(abs(X) * 1.1 + abs(Y) * 1.6) * 3.6);
    float flash = 1.0 + 1.8 * pow(saturate(1.0 - uGrow), 1.5);

    //噪声侵蚀消散：柱身自端点向心啃碎
    float keep = smoothstep(uDissolve - 0.10, uDissolve + 0.10, n * 0.72 + 0.24 - uDissolve * tAx * 0.35);

    //自下而上点亮(uv.x=1 为下端)
    float lit = smoothstep(1.0 - uFill - 0.05, 1.0 - uFill + 0.05, uv.x);
    float litMask = lerp(0.20, 1.0, lit);

    float3 cCore = float3(1.32, 1.15, 0.88);
    float3 cSheath = float3(1.02, 0.40, 0.13);
    float3 cOuter = float3(0.52, 0.09, 0.09);

    float3 color = cOuter * sheath * 0.8;
    color += cSheath * (sheath * sheath * 1.1 + streak);
    color += cCore * core * (0.9 + uHot * 0.5) * flash;
    color += cCore * (bloomArm * 0.85 + bloomRoot * 0.75) * flash;

    float alpha = saturate(sheath * 0.6 + core * 0.95 + bloomArm * 0.5 + bloomRoot * 0.45);
    float gate = grow * keep * litMask;

    return float4(color * alpha * gate, alpha * gate) * input.Color;
}

technique Technique1
{
    pass LonginusCrossPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
