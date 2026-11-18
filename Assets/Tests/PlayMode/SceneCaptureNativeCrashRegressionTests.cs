using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ByteDance.PICO.XR;
using NUnit.Framework;

public class SceneCaptureNativeCrashRegressionTests
{
    private static readonly Dictionary<ushort, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null))
        .ToDictionary(opCode => unchecked((ushort)opCode.Value));

    [TestCase("LocateAnchorRaw")]
    [TestCase("LocateAnchorAligned")]
    public void SceneCaptureProvider_DoesNotCallCrashProneLocateVariants(string methodName)
    {
        var callers = GetMethods(typeof(PicoSceneCaptureFloorAreaProvider))
            .Where(method => Calls(method, typeof(PXR_MixedReality), methodName))
            .Select(method => $"{method.DeclaringType.FullName}.{method.Name}")
            .ToArray();

        Assert.IsEmpty(callers,
            $"{methodName} can dereference a null native XRRuntime pointer for scene anchors. " +
            $"Use the SceneAnchorData-backed {nameof(PXR_MixedReality.LocateAnchor)} instead.");
    }

    private static IEnumerable<MethodBase> GetMethods(Type providerType)
    {
        var types = new[] { providerType }
            .Concat(providerType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
        return types.SelectMany(type => type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)));
    }

    private static bool Calls(MethodBase method, Type targetType, string targetName)
    {
        var body = method.GetMethodBody();
        if (body == null)
        {
            return false;
        }

        var il = body.GetILAsByteArray();
        var position = 0;
        while (position < il.Length)
        {
            ushort value = il[position++];
            if (value == 0xFE)
            {
                value = (ushort)(0xFE00 | il[position++]);
            }

            var opCode = OpCodesByValue[value];
            if (opCode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, position);
                var target = method.Module.ResolveMethod(token);
                if (target.DeclaringType == targetType && target.Name == targetName)
                {
                    return true;
                }
            }

            position += OperandSize(opCode.OperandType, il, position);
        }

        return false;
    }

    private static int OperandSize(OperandType operandType, byte[] il, int position)
    {
        switch (operandType)
        {
            case OperandType.InlineNone:
                return 0;
            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                return 1;
            case OperandType.InlineVar:
                return 2;
            case OperandType.InlineI8:
            case OperandType.InlineR:
                return 8;
            case OperandType.InlineSwitch:
                return 4 + BitConverter.ToInt32(il, position) * 4;
            default:
                return 4;
        }
    }
}
