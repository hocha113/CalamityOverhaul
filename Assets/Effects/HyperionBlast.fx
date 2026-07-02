// ============================================================================
//HyperionBlast.fx 海伯利昂爆燃着色器
//世界空间顶点四边形(UV 0..1),经transformMatrix入屏,Additive混合
//双technique显式选择(禁uniform分支):BlastTech=命中爆燃 MuzzleTech=定向枪口炬
//角向输入仅以整数倍角频喂入wrap采样器或cos(k*ang),k∈Z,规避极角接缝
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float  uProgress;    //0=爆发开始 1=消散完毕
float  uIntensity;   //外部强度倍率
float  uSeed;        //每次爆发的噪声相位偏移0-1
float2 uDirection;   //爆燃入射/枪口朝向单位向量(UV系,+X=世界+X)
float3 coreColor;    //白炽核心色(线性HDR)
float3 sheathColor;  //等离子主色
float3 emberColor;   //余烬边缘色
texture uNoiseTex;   //Perlin灰度

sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

#define PI 3.14159265

struct VSInput
{
    float4 Position  : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color     : COLOR0;
};

struct PSInput
{
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position  = mul(v.Position, transformMatrix);
    o.Color     = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

//---------------------------------------------------------------- 命中爆燃
float4 BlastPS(PSInput input) : COLOR0
{
    float2 uv  = input.TexCoords - 0.5;
    float  r   = length(uv) * 2.0;
    float  ang = atan2(uv.y, uv.x);
    float  angN = (ang + PI) / (2.0 * PI);    //0..1,仅作整数倍频wrap采样输入

    float life = saturate(uProgress);
    float inv  = 1.0 - life;

    //1. 中心白炽闪光:前1/3寿命内指数塌缩
    float flashLife = saturate(1.0 - life * 3.0);
    float flash = pow(saturate(1.0 - r * 1.9), 4.0) * flashLife;

    //2. 主冲击环:扩张减速曲线,厚度随扩张变薄
    float ringR = 1.0 - inv * inv;            //先快后慢
    float ringW = lerp(0.20, 0.045, life);
    float ring  = exp(-pow((r - ringR * 0.92) / ringW, 2.0)) * inv;

    //3. 二次余烬环:滞后主环,更暗更厚
    float ring2R = ringR * 0.55;
    float ring2  = exp(-pow((r - ring2R) / (ringW * 2.4), 2.0)) * inv * 0.7;

    //4. 放射火花丝:角向噪声(3/9整数倍频,wrap连续)+径向窗
    float2 sUV1 = float2(angN * 3.0 + uSeed, life * 0.22 + uSeed * 0.61);
    float2 sUV2 = float2(angN * 9.0 + uSeed * 1.7, life * 0.5 + 0.13);
    float sN1 = tex2D(noiseSamp, sUV1).r;
    float sN2 = tex2D(noiseSamp, sUV2).r;
    float spikeMask   = pow(sN1, 4.0) * pow(sN2, 2.0);
    float spikeRadial = smoothstep(ringR * 1.5 + 0.15, 0.04, r) * smoothstep(0.02, 0.15, r);
    float spikes = spikeMask * spikeRadial * inv * 3.2;

    //5. 入射方向热浪:沿-uDirection偏置的扁椭圆(笛卡尔,无接缝)
    float along = dot(uv, uDirection);
    float side  = dot(uv, float2(-uDirection.y, uDirection.x));
    float heatR = sqrt(along * along * 0.4 + side * side * 1.8);
    float heat  = exp(-heatR * heatR * 14.0) * inv * (smoothstep(0.12, -0.42, along) * 0.65 + 0.35);

    //边界淡出防方块边
    float fade = smoothstep(1.0, 0.72, r);

    float3 col = 0;
    col += coreColor   * flash  * 4.0;
    col += sheathColor * ring   * 2.6;
    col += emberColor  * ring   * ring * 2.2;
    col += emberColor  * ring2  * 1.3;
    col += sheathColor * spikes;
    col += emberColor  * spikes * spikes * 1.2;
    col += sheathColor * heat   * 1.1;
    col += coreColor   * heat   * heat * 1.5;

    col *= fade * uIntensity;

    float a = saturate(flash * 1.7 + ring + spikes * 0.6 + heat) * fade * uIntensity;
    return float4(col, a);
}

//---------------------------------------------------------------- 定向枪口炬
float4 MuzzlePS(PSInput input) : COLOR0
{
    float2 uv  = input.TexCoords - 0.5;
    float  r   = length(uv) * 2.0;
    float  ang = atan2(uv.y, uv.x);

    float life = saturate(uProgress);
    float inv  = 1.0 - life;
    float inv2 = inv * inv;

    //沿枪口方向的余弦锥:cosA>0限定前向半球,幂次收窄
    float2 dir = uDirection;
    float cosA = dot(uv / max(r * 0.5, 1e-4), dir);    //-1..1
    float cone     = pow(saturate(cosA), 6.0);
    float coneWide = pow(saturate(cosA), 2.2);

    //主炬瓣:前向长瓣,随寿命缩短
    float lobeLen = 0.95 * inv2 + 0.12;
    float lobe = cone * smoothstep(lobeLen, lobeLen * 0.15, r);

    //侧向花瓣:cos(4*ang)整数倍角频,连续;仅在近喷口处
    float petal = pow(abs(cos(ang * 4.0)), 3.0) * coneWide * smoothstep(0.55 * inv + 0.08, 0.02, r);

    //白炽核:紧贴喷口
    float core = pow(saturate(1.0 - r * 3.2), 3.0) * inv;

    //激波环:小半径快速掠出
    float ringR = life * 0.75 + 0.08;
    float ring  = exp(-pow((r - ringR) / 0.075, 2.0)) * inv2 * 0.8;

    //火药噪声斑驳,整数倍频wrap采样
    float2 nUV = float2((ang + PI) / (2.0 * PI) * 6.0 + uSeed, r * 0.9 - life * 0.5 + uSeed);
    float grain = lerp(0.6, 1.4, tex2D(noiseSamp, nUV).r);

    float3 col = 0;
    col += coreColor   * core  * 4.5;
    col += coreColor   * lobe  * grain * 2.6;
    col += sheathColor * lobe  * 1.6;
    col += sheathColor * petal * grain * 1.8;
    col += emberColor  * ring  * 2.0;

    float fade = smoothstep(1.0, 0.7, r);
    col *= fade * uIntensity;

    float a = saturate(core * 1.5 + lobe + petal * 0.7 + ring) * fade * uIntensity;
    return float4(col, a);
}

technique BlastTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader  = compile ps_3_0 BlastPS();
    }
}

technique MuzzleTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader  = compile ps_3_0 MuzzlePS();
    }
}
