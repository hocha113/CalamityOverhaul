// ============================================================================
//HeadlessShadeCut.fx 无头鬼影的斩痕（穿体直线切口）
//立场沿用 OniFinaleWound 的创口语法：画"被撕开的一道口子"，不画发光能量带。
//材质换成影：没有白热镶边，缝心比影体还黑（"黑纸撕开一道、缝底下是空的"），
//  骨白只作 ≤2px 结构细线且只在新生几帧存在，之后整条转暗留成疤。
//非对称是"斩"区别于"激光"的关键：uFlip 侧被噪声撕出毛口并沿刀线错位（出刀侧），
//  另一侧收得干净（入刀侧）。厚度包络的峰不在中点而在 uPeak——力点写进轮廓。
//刻意不带 uTime：斩痕是已经发生完的事件的遗迹，不允许沿线流动。
//无极角运算——极角审计免除。预乘 alpha 输出，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uOpen;        //0..~1.06 创面厚度进度（含过冲）
float uHeal;        //0..1 愈合：两端针尖向中心捏合
float uSweepEdge;   //0..1.25 沿线揭开前沿（切口追在刀过之后裂开）
float uTear;        //0..1 骨白撕口新生强度，只在头几帧
float uOpacity;
float uFlip;        //+1/-1 毛口在哪一侧
float uSlide;       //0..~0.06 两唇沿刀线错位量
float uPeak;        //0..1 厚度力点位置
float uSeed;        //实例随机相位

float3 uColVoid;    //缝心虚空，比影体更黑
float3 uColBody;    //影体
float3 uColFray;    //毛口，配色里唯一留紫的地方
float3 uColRim;     //骨白冷青细线

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
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
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
    float ucRaw = input.TexCoords.x;
    float2 p = (input.TexCoords - 0.5) * 2.0;

    //沿线揭开：切口只存在于刀过之后
    float reveal = smoothstep(uSweepEdge + 0.03, uSweepEdge - 0.10, ucRaw);

    //愈合：两端针尖向中心行进，剩余创面重参数化保住轮廓形状
    float ext = max(1.0 - uHeal, 1e-3);
    float xh = p.x / ext;
    float alive = step(abs(xh), 1.0);
    float uc = saturate(xh * 0.5 + 0.5);

    //非对称厚度包络：入刀端收成针尖，力点在 uPeak，出刀端拖长毛尾
    float peak = clamp(uPeak, 0.08, 0.92);
    float tIn = saturate(uc / peak);
    float tOut = saturate((uc - peak) / max(1.0 - peak, 1e-3));
    float isEntry = step(uc, peak);
    float env = lerp(pow(1.0 - tOut, 0.50), pow(tIn, 1.15), isEntry);

    float faceW = env * uOpen * 0.50;
    float s = abs(p.y) / max(faceW, 1e-4);

    //两唇错位：出刀侧的毛口沿刀线滑过一小段，读作两片刚刚错开
    float tornSide = saturate(p.y * uFlip * 7.0);
    float slideU = ucRaw + tornSide * uSlide;
    float n1 = tex2D(noiseSamp, float2(slideU * 2.9 + uSeed, 0.19 + uSeed * 0.53)).r;
    float n2 = tex2D(noiseSamp, float2(slideU * 7.1 - uSeed * 1.7, 0.67 + uSeed * 0.21)).r;
    float jag = (n1 - 0.5) * 0.72 + (n2 - 0.5) * 0.28;

    //入刀侧收净，出刀侧放宽且被噪声撕出参差
    float edge = lerp(0.58, 1.05 + jag * 0.62, tornSide);
    float aOut = smoothstep(edge, edge - 0.36 - tornSide * 0.24, s);

    //骨白细线：贴着缝心一条窄光，针尖处随包络收没
    float rim = exp(-pow(s / 0.11, 2.0)) * smoothstep(0.010, 0.075, faceW);

    //色带：缝心虚空 → 影体，毛口侧补一点冷紫纤维
    float sc = saturate(s);
    float3 col = lerp(uColVoid, uColBody, smoothstep(0.0, 0.55, sc));
    col += uColFray * (tornSide * n2 * (1.0 - sc) * 0.55);
    //撕口新生时本体压得更暗，白只在细线上——结构白，不是整体提亮
    col *= 1.0 - uTear * 0.38;
    col += uColRim * rim * uTear;

    float a = max(aOut, rim * uTear * 0.90);
    a = saturate(a * reveal * alive) * uOpacity;

    //细线在 alpha 之外再给一点增益，暗底上才看得见"斩透了"
    float glowBoost = rim * uTear * 0.34 * uOpacity * reveal * alive;
    return float4(col * a + uColRim * glowBoost * 0.80, saturate(a + glowBoost));
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
