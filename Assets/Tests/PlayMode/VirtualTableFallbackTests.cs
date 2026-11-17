using System.Collections.Generic;
using System.Reflection;
using ByteDance.PICO.XR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class VirtualTableFallbackTests
{
    private GameObject _root;
    private PicoSceneCaptureFloorAreaProvider _provider;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("VirtualTableProvider");
        _provider = _root.AddComponent<PicoSceneCaptureFloorAreaProvider>();
        _provider.surfaceLabel = PxrSemanticLabel.Table;
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) Object.DestroyImmediate(_root);
    }

    [Test]
    public void EmptyTable_ReturnsConfiguredVirtualAreaAndDiagnostics()
    {
        var result = Apply(new List<PicoFloorArea>(), "test empty result");

        Assert.AreEqual(1, result.Count);
        var area = result[0];
        Assert.AreEqual(4, area.Vertices.Length);
        Assert.Less(Vector3.Distance(new Vector3(0f, 0f, 1.5f), area.PlanarBounds.center), 0.0001f);
        Assert.Less(Vector3.Distance(new Vector3(1.5f, 0f, 1.5f), area.PlanarBounds.size), 0.0001f);
        Assert.AreEqual(0f, area.SurfaceHeight, 0.0001f);
        Assert.AreEqual(1, _provider.DebugFloorAreaCount);
        Assert.AreEqual(0f, _provider.DebugGroundHeight);
        Assert.AreEqual("Virtual table fallback", _provider.DebugGroundHeightMethod);
        StringAssert.Contains("test empty result", _provider.DebugCaptureDetails);
        StringAssert.Contains("Virtual table", _provider.DebugCaptureDetails);
    }

    [Test]
    public void RealAreas_ReturnsOriginalListWithoutVirtualReplacement()
    {
        var real = new List<PicoFloorArea>
        {
            new PicoFloorArea(new[] { Vector3.zero, Vector3.right, Vector3.forward }, useHighestVertexHeight: true)
        };
        var result = Apply(real, "must not fall back");

        Assert.AreSame(real, result);
        Assert.AreEqual("Not detected", _provider.DebugGroundHeightMethod);
    }

    [Test]
    public void FloorOrDisabledFallback_ReturnsOriginalEmptyList()
    {
        var empty = new List<PicoFloorArea>();
        _provider.surfaceLabel = PxrSemanticLabel.Floor;
        Assert.AreSame(empty, Apply(empty, "floor"));

        _provider.surfaceLabel = PxrSemanticLabel.Table;
        _provider.useVirtualTableFallback = false;
        Assert.AreSame(empty, Apply(empty, "disabled"));
    }

    [Test]
    public void InvalidVirtualSize_LogsWarningAndReturnsEmpty()
    {
        _provider.virtualTableSize = Vector2.zero;
        LogAssert.Expect(LogType.Warning, "Virtual table fallback config invalid");

        var result = Apply(new List<PicoFloorArea>(), "invalid config");

        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(0, _provider.DebugFloorAreaCount);
    }

    [Test]
    public void FirstEmptyQuery_RequestsCaptureOnlyOnce()
    {
        var method = typeof(PicoSceneCaptureFloorAreaProvider).GetMethod("ShouldStartSceneCapture", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        Assert.IsTrue((bool)method.Invoke(null, new object[] { 0, false }));
        Assert.IsFalse((bool)method.Invoke(null, new object[] { 0, true }));
        Assert.IsFalse((bool)method.Invoke(null, new object[] { 1, false }));
    }

    private List<PicoFloorArea> Apply(List<PicoFloorArea> areas, string reason)
    {
        var method = typeof(PicoSceneCaptureFloorAreaProvider).GetMethod("ApplyVirtualTableFallback", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (List<PicoFloorArea>)method.Invoke(_provider, new object[] { areas, reason });
    }
}
