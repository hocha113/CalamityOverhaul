// ============================================================================
//SHPCModResonanceWave.fx 共振机匣节拍束驻波护层
//Trail 条带 Additive；uv.x: 0=头部 1=尾端，uv.y: 0~1 横向
//驻波方程 sin(k·x)·cos(ω·t)：波节沿束固定，波腹振幅随时间鼓动；
//再叠一对反向行波细弦（其叠加即驻波）强化"共振"物理身份
//条带 UV 为笛卡尔空间，无极坐标无接缝
// ============================================================================

float4x4 transformMatrix;
float uTime;            //帧域 ×0.045
float fadeAlpha;        //整体透明度 0~1
float waveBoost;        //节奏层 0~1，提振幅与亮度
float3 beatBright;      //洋红亮
float3 beatMain;        //洋红主
float3 beatDeep;        //洋红深

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
    float along = uv.x;                      //0=头部 1=尾端
    float signedCross = uv.y * 2.0 - 1.0;    //-1~1，0=中轴
    float crossDist = abs(signedCross);

    //驻波参数：波节数固定，波腹振幅 cos 鼓动（时间频率与 C# WaveOmega 对齐：0.27/0.045=6）
    float waveK = 14.0;                      //沿束半波数
    float omega = 6.0;
    float spatial = sin(along * waveK * 3.14159);   //波形（波节=0 波腹=±1）
    float envelope = cos(uTime * omega);            //振幅时间鼓动
    float standing = spatial * envelope;            //瞬时驻波位移 -1~1
    float antinode = abs(spatial);                  //波腹强度包络（时不变）
    float ampScale = 0.55 + waveBoost * 0.30;       //节奏层提振幅

    //尾端渐隐与头部收口
    float tailFade = 1.0 - smoothstep(0.55, 1.0, along);
    float headRise = smoothstep(0.0, 0.03, along);

    //=
    //A. 驻波振膜：束轮廓随 |驻波| 鼓缩，波节处收细成结、波腹处鼓成振腹
    //=
    float membraneW = 0.16 + abs(standing) * ampScale * 0.60;
    float membrane = 1.0 - smoothstep(membraneW * 0.35, membraneW, crossDist);
    membrane *= 0.55 + 0.45 * abs(envelope);        //整体亮度随鼓动呼吸

    //=
    //B. 反向行波细弦对：左行+右行正弦弦，叠加即驻波（共振的物理由来）
    //=
    float travelAmp = ampScale * 0.42;
    float phase = along * waveK * 3.14159;
    float y1 = sin(phase - uTime * omega) * travelAmp;
    float y2 = sin(phase + uTime * omega) * travelAmp;
    float strand1 = 1.0 - smoothstep(0.0, 0.055, abs(signedCross - y1));
    float strand2 = 1.0 - smoothstep(0.0, 0.055, abs(signedCross - y2));
    float strands = (strand1 + strand2) * 0.5;

    //=
    //C. 波节亮结：驻波不动点处的高亮束结（节奏感的"拍点"视觉锚）
    //=
    float nodeField = 1.0 - smoothstep(0.0, 0.16, antinode);   //波节处→1
    float node = nodeField * (1.0 - smoothstep(0.0, 0.10, crossDist));
    node *= 0.7 + 0.3 * abs(envelope);

    //=
    //D. 波腹辉光：振幅最大处的柔光洼地，随时间与波腹包络起伏
    //=
    float bellyGlow = antinode * (1.0 - smoothstep(0.05, 0.75, crossDist));
    bellyGlow *= 0.35 + 0.65 * envelope * envelope;

    //=
    //E. 噪声微粒流：沿束流动的能量尘（采样器 wrap，安全）
    //=
    float dust = tex2D(noiseSamp, frac(float2(along * 5.0 - uTime * 0.8, uv.y * 1.3))).r;
    dust = smoothstep(0.55, 0.95, dust) * (1.0 - smoothstep(0.2, 0.8, crossDist)) * 0.5;

    //=
    //合成：波节骨点亮白、行波弦亮、振膜主色、腹辉深色
    //=
    float boost = 1.0 + waveBoost * 0.35;
    float3 color = float3(0.0, 0.0, 0.0);
    color += beatBright * node * 1.10;
    color += beatBright * strands * 0.55;
    color += beatMain   * membrane * 0.52 * boost;
    color += beatMain   * dust * 0.5;
    color += beatDeep   * bellyGlow * 0.85;

    float alpha = saturate(
        membrane * 0.42
        + strands * 0.30
        + node * 0.5
        + bellyGlow * 0.28
        + dust * 0.18
    );
    alpha *= fadeAlpha * tailFade * headRise;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass SHPCModResonanceWavePass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
