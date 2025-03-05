﻿using AssettoServer.Shared.Model;
using Microsoft.Extensions.ObjectPool;
using TrafficAiPlugin.Shared;

namespace ReplayPlugin.Data;

public class ReplayFrameState : IResettable
{
    public readonly List<ValueTuple<byte, CarStatus>> PlayerCars = [];
    public readonly List<ValueTuple<byte, CarStatus>> AiCars = [];
    public readonly Dictionary<IAiState, short> AiStateMapping = [];
    public readonly Dictionary<byte, List<short>> AiFrameMapping = [];

    public bool TryReset()
    {
        PlayerCars.Clear();
        AiCars.Clear();
        AiStateMapping.Clear();
        AiFrameMapping.Clear();

        return true;
    }
}
