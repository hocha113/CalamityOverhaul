sampler formesTexture : register(s1);

float colorMult;
float time;
float radius;
float maxOpacity;
float burnIntensity;

float2 screenPosition;
float2 screenSize;
float2 anchorPoint;
float2 playerPosition;

float projTime;
bool isVmos;

float InverseLerp(float a, float b, float t)
{
    return saturate((t - a) / (b - a));
}

float4 Function(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{   
    float2 worldUV = screenPosition + screenSize * uv;
    float2 provUV = anchorPoint / screenSize;
    float worldDistance = distance(worldUV, anchorPoint);
    float adjustedTime = time * 0.1;
    
    float2 pixelatedUV = worldUV / screenSize;
    pixelatedUV.x -= worldUV.x % (1 / screenSize.x);
    pixelatedUV.y -= worldUV.y % (1 / (screenSize.y / 2) * 2);

    float distToPlayer = distance(playerPosition, worldUV);
    float opacity = burnIntensity;
    opacity += InverseLerp(800, 600, distToPlayer);

    bool border = worldDistance < radius && opacity > 0;
    float colorMult = 1;
    if (border) 
        colorMult = InverseLerp(radius * 0.94, radius, worldDistance);
    opacity = clamp(opacity, 0, maxOpacity); 
    if (colorMult == 1 && (opacity == 0 || worldDistance < radius))
        return sampleColor;
    
    float4 newcolor = float4(1, 0.1, 0.1, 1);
    if (isVmos)
    {
        float num1 = projTime / 30;
        newcolor = float4(0.8f + (num1 * 0.2), 1 - (num1 * 0.9), 0.1, 1) * num1;
    }
    return newcolor * colorMult * opacity;
}

technique Technique1
{
    pass PrimeHaloPass
    {
        PixelShader = compile ps_2_0 Function();
    }
}