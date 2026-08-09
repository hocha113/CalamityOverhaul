// ============================================================================
//LonginusCross.fx 朗基努斯十字光柱
//单 quad SDF 拉丁十字；Additive；所有分量在画布 92% 内自然归零，另设边界保险
//uv.x 0=上端 1=下端 沿主轴；uv.y 0~1 横截
//展开编舞：竖柱前沿数帧 racing 拔出(带白热尖) → 横臂后半程弹出 → 辉团随完成度渐显
//uGrow 0~1 生长；uDissolve 0~1 噪声侵蚀；uFill 自下而上点亮(计量)；uHot 瞬态过曝(消费端给包络)
//uAspect=半长/半宽；uWidth=柱体半厚(横向归一单位)
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
    float X = (uv.x - 0.5) * 2.0 * uAspect; //沿主轴，上负下正
    float Y = (uv.y - 0.5) * 2.0;
    float tAx = abs(X) / uAspect;

    //画布边界保险，任何分量不触边
    float guard = smoothstep(1.0, 0.90, tAx) * smoothstep(1.0, 0.90, abs(Y));

    float n = tex2D(noiseSamp, float2(X * 0.10 + uTime * 0.05, Y * 0.8 + 0.37)).r;

    //---- 竖柱：92% 长度处收细归零，生长前沿 racing ----
    float lenT = saturate(tAx / 0.92);
    float growFront = saturate(uGrow * 1.12);
    float pReveal = 1.0 - smoothstep(growFront - 0.05, growFront + 0.02, lenT);
    float pillarEnv = pow(saturate(1.0 - lenT), 0.35);

    float wPillar = uWidth * pow(saturate(1.0 - lenT), 0.72);
    float dPillar = abs(Y) - wPillar;
    float coreP = smoothstep(0.0, -uWidth * 0.5, dPillar) * pillarEnv * pReveal;
    float sheathP = smoothstep(uWidth * 1.30, -uWidth * 0.2, dPillar) * pillarEnv * pReveal;

    //生长前沿白热尖端，完成后熄灭
    float pTip = exp2(-abs(lenT - growFront) * 22.0) * saturate(1.0 - uGrow * 1.05)
        * smoothstep(uWidth * 2.2, 0.0, abs(Y));

    //---- 横臂：uGrow 后半段弹出，臂尖自然收细 ----
    float xArm = -uAspect * 0.28;
    float armEnv = saturate(uGrow * 2.8 - 1.55);
    float armSpan = 0.78 * armEnv;
    float aT = saturate(abs(Y) / max(armSpan, 0.001));
    float armAxEnv = pow(saturate(1.0 - aT), 0.35);

    float wArm = uWidth * 1.55 * pow(saturate(1.0 - aT), 0.80);
    float dArm = abs(X - xArm) - wArm;
    float coreA = smoothstep(0.0, -uWidth * 0.5, dArm) * armAxEnv;
    float sheathA = smoothstep(uWidth * 1.30, -uWidth * 0.2, dArm) * armAxEnv;

    //臂尖展开热点，弹出中段最亮
    float armDeploying = saturate(armEnv * (1.0 - armEnv) * 4.0);
    float aTip = exp2(-(abs(abs(Y) - armSpan) * 14.0 + abs(X - xArm) * 10.0)) * armDeploying;

    float core = max(coreP, coreA);
    float sheath = max(sheathP, sheathA);

    //---- 辉团：紧凑高斯半径∝柱宽，随完成度渐显 ----
    float bloomEnv = pow(saturate(uGrow), 2.4);
    float ww = max(uWidth * uWidth * 2.0, 0.0001);
    float2 dRoot = float2(X * 1.5, Y);
    float bloomRoot = exp2(-dot(dRoot, dRoot) / ww) * bloomEnv;
    float2 dArmC = float2((X - xArm) * 1.5, Y);
    float bloomArm = exp2(-dot(dArmC, dArmC) / (ww * 1.3)) * bloomEnv * saturate(armEnv * 1.5);

    //沿柱流光
    float flow = tex2D(noiseSamp, float2(X * 0.22 - uTime * 0.85, Y * 1.4)).r;
    float streak = smoothstep(0.55, 0.85, flow) * sheath * 0.30;

    //瞬态过曝由消费端包络驱动
    float flash = 1.0 + uHot * 1.6;

    //噪声侵蚀消散，端点先碎
    float keep = smoothstep(uDissolve - 0.10, uDissolve + 0.10, n * 0.72 + 0.24 - uDissolve * tAx * 0.35);

    //自下而上点亮(uv.x=1 为下端)
    float lit = smoothstep(1.0 - uFill - 0.05, 1.0 - uFill + 0.05, uv.x);
    float litMask = lerp(0.20, 1.0, lit);

    float3 cCore = float3(1.32, 1.15, 0.88);
    float3 cSheath = float3(1.02, 0.40, 0.13);
    float3 cOuter = float3(0.52, 0.09, 0.09);

    float3 color = cOuter * sheath * 0.55;
    color += cSheath * (sheath * sheath * 0.95 + streak);
    color += cCore * core * (0.85 + uHot * 0.9);
    color += cCore * (bloomRoot * 0.9 + bloomArm * 0.8) * flash;
    color += cCore * (pTip * 1.4 + aTip * 1.2);

    float alpha = saturate(sheath * 0.5 + core * 0.95 + bloomRoot * 0.6 + bloomArm * 0.5 + pTip * 0.6 + aTip * 0.5);
    float gate = keep * litMask * guard;

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
