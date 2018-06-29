﻿using System.Linq;
using System.Reflection;

using Orleans;
using Orleans.CodeGeneration;

namespace Orleankka.Cluster
{
    static class DashboardIntegration
    {
        public static string Format(MethodInfo method, InvokeMethodRequest request, IGrain grain)
        {
            if (method == null)
                return "Unknown";

            if (!(grain is IActorGrain))
                return method.Name;

            if (method.Name != nameof(IActorGrain.ReceiveAsk) && 
                method.Name != nameof(IActorGrain.ReceiveTell) && 
                method.Name != nameof(IActorGrain.ReceiveNotify))
                return method.Name;

            var argumentType = request.Arguments[0]?.GetType();

            if (argumentType == null)
                return $"{method.Name}(NULL)";

            return argumentType.IsGenericType
                ? $"{argumentType.Name}<{string.Join(",", argumentType.GenericTypeArguments.Select(x => x.Name))}>"
                : argumentType.Name;
        }
    }
}