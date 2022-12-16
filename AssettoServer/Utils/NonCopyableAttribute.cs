using System;

namespace AssettoServer.Utils;

// TODO Replace once https://github.com/dotnet/runtime/issues/50389 has landed
[AttributeUsage(AttributeTargets.Struct)]
internal class NonCopyableAttribute : Attribute { }
